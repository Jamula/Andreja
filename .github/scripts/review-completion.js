'use strict';

const CHECK_NAMES = Object.freeze({
  gate: 'Review completion gate',
  copilot: 'copilot-pull-request-reviewer',
  breakGlass: 'Review completion break-glass',
});

const REVIEW_DOMAINS = Object.freeze([
  'architecture',
  'security',
  'privacy',
  'quality',
]);

const REQUIRED_LABELS = Object.freeze({
  architecture: new Set(['area:architecture', 'review:architecture-required']),
  security: new Set(['area:security', 'review:security-required']),
  privacy: new Set(['area:privacy', 'review:privacy-required']),
  quality: new Set(['review:quality-required']),
});

const COPILOT_LOGIN = 'copilot-pull-request-reviewer[bot]';
const TRUSTED_ACTIONS_APP = 'github-actions';
const EVIDENCE_PREFIX = 'review-evidence:v1:';
const BREAK_GLASS_PREFIX = 'review-break-glass:v1:';

function labelNames(labels = []) {
  return labels.map((label) =>
    typeof label === 'string' ? label : label?.name).filter(Boolean);
}

function requiredDomains(labelSets = []) {
  const labels = new Set(labelSets.flatMap(labelNames));
  return REVIEW_DOMAINS.filter((domain) =>
    [...REQUIRED_LABELS[domain]].some((label) => labels.has(label)));
}

function evidenceCheckName(domain) {
  if (!REVIEW_DOMAINS.includes(domain)) {
    throw new Error(`Unknown review domain: ${domain}`);
  }
  return `Independent ${domain} review evidence`;
}

function evidenceArtifactName(domain, headSha, verdict) {
  if (!REVIEW_DOMAINS.includes(domain) ||
      !new Set(['approved', 'rejected']).has(verdict) ||
      !/^[0-9a-f]{40}$/.test(String(headSha))) {
    throw new Error('Invalid independent-review artifact identity.');
  }
  return `review-evidence-${domain}-${verdict}-${headSha}`;
}

function breakGlassArtifactName(headSha) {
  if (!/^[0-9a-f]{40}$/.test(String(headSha))) {
    throw new Error('Invalid break-glass artifact head SHA.');
  }
  return `review-break-glass-approved-${headSha}`;
}

function latestCheck(checks = [], predicate = () => true) {
  return checks
    .filter(predicate)
    .sort((left, right) => {
      const leftTime = Date.parse(
        left.started_at || left.created_at || left.completed_at || 0);
      const rightTime = Date.parse(
        right.started_at || right.created_at || right.completed_at || 0);
      return rightTime - leftTime || Number(right.id || 0) - Number(left.id || 0);
    })[0] || null;
}

function checkOutcome(check) {
  if (!check) {
    return 'missing';
  }
  if (check.status !== 'completed') {
    return 'pending';
  }
  if (check.conclusion === 'success') {
    return 'success';
  }
  return 'failure';
}

function currentCopilotReview(reviews = [], headSha) {
  return reviews
    .filter((review) =>
      review.user?.login === COPILOT_LOGIN && review.commit_id === headSha)
    .sort((left, right) =>
      Date.parse(right.submitted_at || 0) - Date.parse(left.submitted_at || 0))[0] ||
    null;
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
  headSha,
  copilotCheck = null,
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
        `${path}: review evidence is enforced on each pull-request head before this path.`,
      ],
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

  const breakGlassOutcome = checkOutcome(breakGlass);
  if (breakGlassOutcome === 'success') {
    return {
      state: 'approved',
      reasons: [
        'Current-head break-glass evidence was explicitly recorded by trusted default-branch automation.',
        'Draft and unresolved-thread protections were not bypassed.',
      ],
    };
  }
  if (breakGlassOutcome === 'failure') {
    return {
      state: 'rejected',
      reasons: ['The latest current-head break-glass record is invalid or rejected.'],
    };
  }

  const rejected = [];
  const pending = [];
  const copilotOutcome = checkOutcome(copilotCheck);

  if (copilotOutcome === 'failure') {
    rejected.push('Current-head Copilot reviewer automation failed or was cancelled.');
  } else if (copilotOutcome !== 'success') {
    pending.push('Current-head Copilot reviewer automation has not completed successfully.');
  }

  if (!copilotReview) {
    pending.push(`No Copilot review is bound to current head ${headSha}.`);
  } else if (String(copilotReview.state).toUpperCase() === 'DISMISSED') {
    rejected.push('The current-head Copilot review was dismissed.');
  }

  for (const domain of domains) {
    const outcome = checkOutcome(evidence[domain]);
    if (outcome === 'failure') {
      rejected.push(`Independent ${domain} review evidence rejected the current head.`);
    } else if (outcome !== 'success') {
      pending.push(`Independent ${domain} review evidence is missing for the current head.`);
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
      `Copilot review completed for current head ${headSha}.`,
      'Zero unresolved review threads remain.',
      domains.length === 0
        ? 'No linked issue or pull-request label requires an independent domain review.'
        : `Current-head independent evidence completed for: ${domains.join(', ')}.`,
    ],
  };
}

function mergeReady(requiredChecks = {}) {
  return Object.values(requiredChecks).length > 0 &&
    Object.values(requiredChecks).every((state) => state === 'success');
}

function replayTimingWindow(events = []) {
  const checks = {};
  return events.map((event) => {
    checks[event.context] = event.state;
    return {
      atSeconds: event.atSeconds,
      context: event.context,
      state: event.state,
      mergeReady: mergeReady(checks),
    };
  });
}

module.exports = {
  BREAK_GLASS_PREFIX,
  CHECK_NAMES,
  COPILOT_LOGIN,
  EVIDENCE_PREFIX,
  REQUIRED_LABELS,
  REVIEW_DOMAINS,
  TRUSTED_ACTIONS_APP,
  checkOutcome,
  breakGlassArtifactName,
  currentCopilotReview,
  evaluateReviewCompletion,
  evidenceArtifactName,
  evidenceCheckName,
  labelNames,
  latestCheck,
  mergeReady,
  replayTimingWindow,
  requiredDomains,
  summarizeResult,
};
