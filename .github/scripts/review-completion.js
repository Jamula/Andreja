'use strict';

const crypto = require('node:crypto');

const CHECK_NAMES = Object.freeze({
  copilot: 'copilot-pull-request-reviewer',
});

const REVIEW_DOMAINS = Object.freeze([
  'architecture',
  'security',
  'privacy',
  'quality',
]);

const REQUIRED_LABELS = Object.freeze({
  architecture: new Set([
    'area:architecture',
    'review:architecture-required',
  ]),
  security: new Set([
    'area:security',
    'review:security-required',
  ]),
  privacy: new Set([
    'area:privacy',
    'review:privacy-required',
  ]),
  quality: new Set(['review:quality-required']),
});

const COPILOT_LOGIN = 'copilot-pull-request-reviewer[bot]';
const REVIEW_MARKER_PREFIX = 'andreja-review-evidence:v2:';
const BREAK_GLASS_KIND = 'review-break-glass';
const BREAK_GLASS_SCHEMA_VERSION = 2;

function labelNames(labels = []) {
  return labels.map((label) =>
    typeof label === 'string' ? label : label?.name).filter(Boolean);
}

function requiredDomains(labelSets = []) {
  const labels = new Set(labelSets.flatMap(labelNames));
  return REVIEW_DOMAINS.filter((domain) =>
    [...REQUIRED_LABELS[domain]].some((label) => labels.has(label)));
}

function pullIdentity(pullRequest) {
  const identity = {
    pullNumber: Number(pullRequest?.number),
    headSha: String(pullRequest?.head?.sha || ''),
    baseRepositoryId: Number(pullRequest?.base?.repo?.id),
    baseRepository: String(pullRequest?.base?.repo?.full_name || ''),
    baseRef: String(pullRequest?.base?.ref || ''),
    baseSha: String(pullRequest?.base?.sha || ''),
  };
  if (!Number.isInteger(identity.pullNumber) || identity.pullNumber <= 0 ||
      !/^[0-9a-f]{40}$/.test(identity.headSha) ||
      !Number.isInteger(identity.baseRepositoryId) ||
      identity.baseRepositoryId <= 0 ||
      !identity.baseRepository ||
      !identity.baseRef ||
      !/^[0-9a-f]{40}$/.test(identity.baseSha)) {
    throw new Error('The pull request did not expose a complete diff identity.');
  }
  return identity;
}

function samePullIdentity(left, right) {
  return left.pullNumber === right.pullNumber &&
    left.headSha === right.headSha &&
    left.baseRepositoryId === right.baseRepositoryId &&
    left.baseRepository.toLowerCase() === right.baseRepository.toLowerCase() &&
    left.baseRef === right.baseRef &&
    left.baseSha === right.baseSha;
}

function reviewMarker(domain, binding) {
  if (!REVIEW_DOMAINS.includes(domain)) {
    throw new Error(`Unknown review domain: ${domain}`);
  }
  return `<!-- ${REVIEW_MARKER_PREFIX}${domain} ${JSON.stringify(binding)} -->`;
}

function reviewMarkers(body = '') {
  const expression = new RegExp(
    `<!--\\s*${REVIEW_MARKER_PREFIX}` +
    `(${REVIEW_DOMAINS.join('|')})\\s+([\\s\\S]*?)-->`,
    'g');
  return [...String(body).matchAll(expression)].map((match) => {
    try {
      return {
        domain: match[1],
        binding: JSON.parse(match[2].trim()),
        error: null,
      };
    } catch {
      return {
        domain: match[1],
        binding: null,
        error: 'The review evidence marker contains invalid JSON.',
      };
    }
  });
}

function reviewTime(review) {
  return Date.parse(review?.submitted_at || review?.updated_at || 0) || 0;
}

function latestReview(reviews = [], predicate = () => true) {
  return reviews
    .filter(predicate)
    .sort((left, right) =>
      reviewTime(right) - reviewTime(left) ||
      Number(right.id || 0) - Number(left.id || 0))[0] || null;
}

function latestCopilotReview(reviews = []) {
  return latestReview(reviews, (review) =>
    review.user?.login === COPILOT_LOGIN);
}

function latestDomainReview(reviews = [], domain) {
  if (!REVIEW_DOMAINS.includes(domain)) {
    throw new Error(`Unknown review domain: ${domain}`);
  }
  const candidatePrefix = `${REVIEW_MARKER_PREFIX}${domain}`;
  return latestReview(reviews, (review) =>
    String(review.body || '').includes(candidatePrefix));
}

function evidenceOutcome(evidence) {
  return evidence?.outcome || 'missing';
}

function summarizeResult(result) {
  const heading = {
    approved: 'Approved',
    pending: 'Pending',
    rejected: 'Rejected',
    not_applicable: 'Not applicable',
  }[result.state] || result.state;
  return [
    `### ${heading}`,
    '',
    ...result.reasons.map((reason) => `- ${reason}`),
  ].join('\n');
}

function evaluateReviewCompletion({
  draft = false,
  open = true,
  identity,
  copilotReview = null,
  unresolvedThreads = 0,
  domains = [],
  evidence = {},
  breakGlass = null,
  path = 'pull_request',
} = {}) {
  if (path !== 'pull_request') {
    return {
      state: 'not_applicable',
      reasons: [
        `${path}: review evidence is enforced on each pull-request diff before this path.`,
      ],
    };
  }

  if (!open) {
    return {
      state: 'rejected',
      reasons: ['Only an open pull request can satisfy the review policy.'],
    };
  }
  if (draft) {
    return {
      state: 'rejected',
      reasons: ['Draft pull requests are not ready for merge.'],
    };
  }
  if (unresolvedThreads > 0) {
    return {
      state: 'rejected',
      reasons: [
        `${unresolvedThreads} unresolved review thread(s) remain on the pull request.`,
      ],
    };
  }

  const breakGlassState = evidenceOutcome(breakGlass);
  if (breakGlassState === 'success') {
    return {
      state: 'approved',
      reasons: [
        'A current-diff break-glass decision was recorded by an authenticated maintainer.',
        'Draft and unresolved-thread protections were not bypassed.',
      ],
    };
  }
  if (breakGlassState === 'failure') {
    return {
      state: 'rejected',
      reasons: [breakGlass.reason ||
        'The newest current-diff break-glass attempt is invalid or failed.'],
    };
  }
  if (breakGlassState === 'pending') {
    return {
      state: 'pending',
      reasons: ['The newest current-diff break-glass attempt is still running.'],
    };
  }

  const rejected = [];
  const pending = [];
  if (!copilotReview) {
    pending.push('Copilot has not submitted a review for the current diff.');
  } else if (copilotReview.commit_id !== identity.headSha) {
    pending.push(`The newest Copilot review is not bound to current head ${identity.headSha}.`);
  } else if (String(copilotReview.state).toUpperCase() === 'DISMISSED') {
    rejected.push('The newest current-head Copilot review was dismissed.');
  }

  for (const domain of domains) {
    const domainState = evidenceOutcome(evidence[domain]);
    if (domainState === 'failure') {
      rejected.push(evidence[domain].reason ||
        `The newest independent ${domain} review evidence is invalid or rejected.`);
    } else if (domainState === 'pending') {
      pending.push(evidence[domain].reason ||
        `Independent ${domain} review evidence is incomplete.`);
    } else if (domainState !== 'success') {
      pending.push(`Independent ${domain} review evidence is missing for the current diff.`);
    }
  }

  if (rejected.length > 0) {
    return { state: 'rejected', reasons: [...rejected, ...pending] };
  }
  if (pending.length > 0) {
    return { state: 'pending', reasons: pending };
  }
  return {
    state: 'approved',
    reasons: [
      `Copilot review completed for current head ${identity.headSha}.`,
      `Reviewed base is ${identity.baseRepository}:${identity.baseRef}@${identity.baseSha}.`,
      'Zero unresolved review threads remain.',
      domains.length === 0
        ? 'No PR or closing-issue label requires an independent domain review.'
        : `Authenticated current-diff evidence completed for: ${domains.join(', ')}.`,
    ],
  };
}

function stableValue(value) {
  if (Array.isArray(value)) {
    return value.map(stableValue);
  }
  if (value && typeof value === 'object') {
    return Object.fromEntries(Object.keys(value).sort().map((key) =>
      [key, stableValue(value[key])]));
  }
  return value;
}

function securityFingerprint(value) {
  return crypto.createHash('sha256')
    .update(JSON.stringify(stableValue(value)))
    .digest('hex');
}

function breakGlassRunTitle(identity) {
  return [
    'review-break-glass:v2',
    `pr=${identity.pullNumber}`,
    `head=${identity.headSha}`,
    `baseRepo=${identity.baseRepositoryId}`,
    `baseRef=${identity.baseRef}`,
    `base=${identity.baseSha}`,
  ].join(':');
}

function breakGlassArtifactName(identity) {
  return [
    'review-break-glass-v2',
    `pr-${identity.pullNumber}`,
    identity.headSha,
    `repo-${identity.baseRepositoryId}`,
    identity.baseSha,
  ].join('-');
}

module.exports = {
  BREAK_GLASS_KIND,
  BREAK_GLASS_SCHEMA_VERSION,
  CHECK_NAMES,
  COPILOT_LOGIN,
  REQUIRED_LABELS,
  REVIEW_DOMAINS,
  REVIEW_MARKER_PREFIX,
  breakGlassArtifactName,
  breakGlassRunTitle,
  evaluateReviewCompletion,
  evidenceOutcome,
  labelNames,
  latestCopilotReview,
  latestDomainReview,
  latestReview,
  pullIdentity,
  requiredDomains,
  reviewMarker,
  reviewMarkers,
  samePullIdentity,
  securityFingerprint,
  summarizeResult,
};
