'use strict';

const crypto = require('node:crypto');

const CHECK_NAME = 'Andreja review policy';
const CHECK_EXTERNAL_PREFIX = 'andreja-review-gate:v5';
const POLICY_EVENT_MARKER_PREFIX = 'andreja-review-policy-event:v5:';
const REVIEW_MARKER_PREFIX = 'andreja-review-evidence:v5:';
const POLICY_EVENT_SCHEMA_VERSION = 5;
const CONTRACT_REVISION = 'review-gate-contract-v5';
const COPILOT_REVIEWER = Object.freeze({
  id: 175728472,
  login: 'copilot-pull-request-reviewer[bot]',
  type: 'Bot',
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
const POLICY_EVENT_KINDS = new Set([
  'bind-issue',
  'require-domain',
  'copilot-attestation',
  'domain-attestation',
  'reduce-policy',
  'break-glass',
]);

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

function securityDigest(value) {
  return crypto.createHash('sha256')
    .update(JSON.stringify(stableValue(value)))
    .digest('hex');
}

function labelNames(labels = []) {
  return labels
    .map((label) => typeof label === 'string' ? label : label?.name)
    .filter(Boolean);
}

function requiredDomains(labelSets = []) {
  const labels = new Set(labelSets.flatMap(labelNames));
  return REVIEW_DOMAINS.filter((domain) =>
    [...REQUIRED_LABELS[domain]].some((label) => labels.has(label)));
}

function validatePullIdentity(identity) {
  const normalized = {
    pullNumber: Number(identity?.pullNumber),
    headRepositoryId: Number(identity?.headRepositoryId),
    headRepository: String(identity?.headRepository || ''),
    headRef: String(identity?.headRef || ''),
    headSha: String(identity?.headSha || ''),
    baseRepositoryId: Number(identity?.baseRepositoryId),
    baseRepository: String(identity?.baseRepository || ''),
    baseRef: String(identity?.baseRef || ''),
    baseSha: String(identity?.baseSha || ''),
  };
  if (!Number.isInteger(normalized.pullNumber) ||
      normalized.pullNumber <= 0 ||
      !Number.isInteger(normalized.headRepositoryId) ||
      normalized.headRepositoryId <= 0 ||
      !normalized.headRepository ||
      !normalized.headRef ||
      !/^[0-9a-f]{40}$/.test(normalized.headSha) ||
      !Number.isInteger(normalized.baseRepositoryId) ||
      normalized.baseRepositoryId <= 0 ||
      !normalized.baseRepository ||
      !normalized.baseRef ||
      !/^[0-9a-f]{40}$/.test(normalized.baseSha)) {
    throw new Error('The pull request did not expose a complete diff identity.');
  }
  normalized.diffIdentity = securityDigest({
    pullNumber: normalized.pullNumber,
    headRepositoryId: normalized.headRepositoryId,
    headRepository: normalized.headRepository.toLowerCase(),
    headRef: normalized.headRef,
    headSha: normalized.headSha,
    baseRepositoryId: normalized.baseRepositoryId,
    baseRepository: normalized.baseRepository.toLowerCase(),
    baseRef: normalized.baseRef,
    baseSha: normalized.baseSha,
  });
  if (identity?.diffIdentity &&
      String(identity.diffIdentity) !== normalized.diffIdentity) {
    throw new Error('The pull request diff identity digest is invalid.');
  }
  return normalized;
}

function pullIdentity(pullRequest) {
  return validatePullIdentity({
    pullNumber: Number(pullRequest?.number),
    headRepositoryId: Number(pullRequest?.head?.repo?.id),
    headRepository: String(pullRequest?.head?.repo?.full_name || ''),
    headRef: String(pullRequest?.head?.ref || ''),
    headSha: String(pullRequest?.head?.sha || ''),
    baseRepositoryId: Number(pullRequest?.base?.repo?.id),
    baseRepository: String(pullRequest?.base?.repo?.full_name || ''),
    baseRef: String(pullRequest?.base?.ref || ''),
    baseSha: String(pullRequest?.base?.sha || ''),
  });
}

function samePullIdentity(left, right) {
  try {
    return validatePullIdentity(left).diffIdentity ===
      validatePullIdentity(right).diffIdentity;
  } catch {
    return false;
  }
}

function repositoryUrl(value, repository) {
  let url;
  try {
    url = new URL(String(value || ''));
  } catch {
    return null;
  }
  const prefix = `/${repository}/`;
  return url.protocol === 'https:' &&
    url.hostname === 'github.com' &&
    url.pathname.toLowerCase().startsWith(prefix.toLowerCase())
    ? url.toString()
    : null;
}

function makeObservationEpoch(identity, {
  deliveryId,
  eventPath,
  workerRevision,
}) {
  const normalizedIdentity = validatePullIdentity(identity);
  const normalized = {
    identity: normalizedIdentity,
    deliveryId: String(deliveryId || ''),
    eventPath: String(eventPath || ''),
    workerRevision: String(workerRevision || ''),
  };
  if (!normalized.deliveryId ||
      !normalized.eventPath ||
      !/^[0-9a-f]{64}$/.test(normalized.workerRevision)) {
    throw new Error(
      'An observation epoch requires delivery, event path, and worker revision.');
  }
  return {
    ...normalized,
    id: securityDigest(normalized),
  };
}

function validObservationEpoch(epoch, identity = epoch?.identity) {
  try {
    const expected = makeObservationEpoch(validatePullIdentity(identity), {
      deliveryId: epoch?.deliveryId,
      eventPath: epoch?.eventPath,
      workerRevision: epoch?.workerRevision,
    });
    return epoch?.id === expected.id &&
      samePullIdentity(epoch?.identity, expected.identity);
  } catch {
    return false;
  }
}

function policyEventDigest(event) {
  return securityDigest(event);
}

function makePolicyEvent(fields) {
  const event = {
    schemaVersion: POLICY_EVENT_SCHEMA_VERSION,
    kind: fields.kind,
    repositoryId: Number(fields.repositoryId),
    repository: String(fields.repository || ''),
    pullNumber: Number(fields.pullNumber),
    deliveryId: String(fields.deliveryId || ''),
    createdAt: String(fields.createdAt || new Date().toISOString()),
    actor: String(fields.actor || ''),
    ...fields.data,
  };
  if (!POLICY_EVENT_KINDS.has(event.kind) ||
      !Number.isInteger(event.repositoryId) ||
      event.repositoryId <= 0 ||
      !event.repository ||
      !Number.isInteger(event.pullNumber) ||
      event.pullNumber <= 0 ||
      !event.deliveryId ||
      !Number.isFinite(Date.parse(event.createdAt)) ||
      !event.actor) {
    throw new Error('Policy event metadata is incomplete.');
  }
  if ((event.kind === 'bind-issue' || event.kind === 'require-domain') &&
      (!event.sourceKey ||
       !validObservationEpoch(event.observationEpoch) ||
       event.deliveryId !== event.observationEpoch.deliveryId ||
       !samePullIdentity(event.observationEpoch.identity, {
         ...event.observationEpoch.identity,
         pullNumber: event.pullNumber,
       }))) {
    throw new Error(
      'Policy observations require a unique exact-diff observation epoch.');
  }
  event.eventId = policyEventDigest(event);
  event.integrityDigest = securityDigest(event);
  return event;
}

function policyEventComment(event) {
  const title = {
    'bind-issue': `Trusted issue #${event.issueNumber} bound`,
    'require-domain': `${event.domain} review requirement recorded`,
    'copilot-attestation': 'Exact-diff Copilot review attested',
    'domain-attestation': `${event.domain} automation review attested`,
    'reduce-policy': 'Audited policy reduction recorded',
    'break-glass': 'Exact-diff break-glass recorded',
  }[event.kind];
  const encoded = Buffer.from(
    JSON.stringify(stableValue(event)),
    'utf8').toString('base64url');
  return [
    `### Andreja review policy — ${title}`,
    '',
    'This record was published by the dedicated review-gate GitHub App.',
    '',
    `<!-- ${POLICY_EVENT_MARKER_PREFIX}${encoded} -->`,
  ].join('\n');
}

function policyEventValidationError(event) {
  if (!event || typeof event !== 'object' || Array.isArray(event)) {
    return 'The App-authored policy event is malformed.';
  }
  const eventId = event.eventId;
  const integrityDigest = event.integrityDigest;
  const signed = { ...event };
  delete signed.integrityDigest;
  const eventKey = { ...signed };
  delete eventKey.eventId;
  if (!POLICY_EVENT_KINDS.has(event.kind) ||
      event.schemaVersion !== POLICY_EVENT_SCHEMA_VERSION ||
      !/^[0-9a-f]{64}$/.test(String(eventId || '')) ||
      !/^[0-9a-f]{64}$/.test(String(integrityDigest || '')) ||
      policyEventDigest(eventKey) !== eventId ||
      securityDigest(signed) !== integrityDigest ||
      ((event.kind === 'bind-issue' || event.kind === 'require-domain') &&
       !validObservationEpoch(event.observationEpoch))) {
    return 'The App-authored policy event failed integrity validation.';
  }
  return null;
}

function parsePolicyEventComment(body) {
  const expression = new RegExp(
    `<!--\\s*${POLICY_EVENT_MARKER_PREFIX}([A-Za-z0-9_-]+)\\s*-->`,
    'g');
  const matches = [...String(body || '').matchAll(expression)];
  if (matches.length !== 1) {
    return {
      event: null,
      error: 'The App-authored policy comment must contain exactly one event.',
    };
  }
  let event;
  try {
    event = JSON.parse(Buffer.from(matches[0][1], 'base64url').toString('utf8'));
  } catch {
    return { event: null, error: 'The App-authored policy event is malformed.' };
  }
  const validationError = policyEventValidationError(event);
  if (validationError) {
    return { event: null, error: validationError };
  }
  return { event, error: null };
}

function trustedPolicyEvents(comments, {
  appId,
  repositoryId,
  repository,
  pullNumber,
}) {
  const events = [];
  const errors = [];
  for (const comment of comments || []) {
    const body = String(comment.body || '');
    if (Number(comment.performed_via_github_app?.id) !== Number(appId)) {
      continue;
    }
    if (!body.includes(POLICY_EVENT_MARKER_PREFIX)) {
      errors.push(
        'An App-authored policy projection is missing its canonical marker.');
      continue;
    }
    const parsed = parsePolicyEventComment(body);
    if (parsed.error) {
      errors.push(parsed.error);
      continue;
    }
    const event = parsed.event;
    if (Number(event.repositoryId) !== Number(repositoryId) ||
        event.repository.toLowerCase() !== String(repository).toLowerCase() ||
        Number(event.pullNumber) !== Number(pullNumber)) {
      errors.push('An App-authored policy event is bound to a different repository or PR.');
      continue;
    }
    events.push({
      ...event,
      commentId: Number(comment.id),
      commentUrl: String(comment.html_url || ''),
    });
  }
  return { events, errors };
}

function policySnapshot(
  latestBySource,
  reduced,
  reductionIds,
  errors,
  identity = null,
) {
  const latest = [...latestBySource.values()]
    .sort((left, right) => left.sourceKey.localeCompare(right.sourceKey));
  const active = latest.filter((event) => !reduced.has(event.eventId));
  const associations = [...new Set(active
    .filter((event) => event.kind === 'bind-issue')
    .map((event) => Number(event.issueNumber))
    .filter((number) => Number.isInteger(number) && number > 0))]
    .sort((left, right) => left - right);
  const domains = REVIEW_DOMAINS.filter((domain) => active.some((event) =>
    event.kind === 'require-domain' && event.domain === domain));
  const activeSources = Object.fromEntries(active.map((event) => [
    String(event.sourceKey || event.eventId),
    event.eventId,
  ]));
  const observationIds = active.map((event) => event.eventId).sort();
  const latestSources = Object.fromEntries(latest.map((event) => [
    event.sourceKey,
    {
      eventId: event.eventId,
      kind: event.kind,
      issueNumber: Number(event.issueNumber || 0),
      domain: event.domain || null,
      sourceKind: event.sourceKind || null,
      sourceNumber: Number(event.sourceNumber || 0),
      identity: event.identity || null,
      reason: event.reason || null,
      auditUrl: event.auditUrl || null,
      observationEpoch: event.observationEpoch,
      reduced: reduced.has(event.eventId),
    },
  ]));
  const normalizedIdentity = identity ? validatePullIdentity(identity) : null;
  const currentEpochComplete = !normalizedIdentity || latest.every((event) =>
    samePullIdentity(event.observationEpoch?.identity, normalizedIdentity));
  const snapshot = {
    initialized: associations.length > 0,
    associations,
    domains,
    activeSources,
    latestSources,
    observationIds,
    reductionIds: [...reductionIds].sort(),
    currentIdentity: normalizedIdentity,
    currentEpochComplete,
    errors: [...errors],
  };
  snapshot.digest = securityDigest({
    identity: normalizedIdentity,
    associations,
    domains,
    observationIds,
    reductionIds: snapshot.reductionIds,
  });
  return snapshot;
}

function foldPolicyEvents(events, errors = [], { identity = null } = {}) {
  const ordered = [...events].sort((left, right) =>
    Date.parse(left.createdAt) - Date.parse(right.createdAt) ||
    left.eventId.localeCompare(right.eventId));
  const foldErrors = [...errors];
  const latestBySource = new Map();
  const reduced = new Set();
  const reductionIds = [];
  for (const event of ordered) {
    if (event.kind === 'bind-issue' || event.kind === 'require-domain') {
      const validKindData = event.kind === 'bind-issue'
        ? Number.isInteger(Number(event.issueNumber)) &&
          Number(event.issueNumber) > 0 &&
          samePullIdentity(event.identity, event.observationEpoch?.identity) &&
          String(event.reason || '').trim().length >= 20 &&
          String(event.reason || '').length <= 2000 &&
          Boolean(repositoryUrl(event.auditUrl, event.repository))
        : REVIEW_DOMAINS.includes(event.domain) &&
          Number.isInteger(Number(event.sourceNumber)) &&
          Number(event.sourceNumber) > 0 &&
          new Set(['pull-label', 'issue-label']).has(event.sourceKind);
      if (!event.sourceKey ||
          !validKindData ||
          !validObservationEpoch(event.observationEpoch) ||
          Number(event.observationEpoch.identity.pullNumber) !==
            Number(event.pullNumber)) {
        foldErrors.push(
          'An authenticated policy observation has an invalid exact-diff epoch.');
        continue;
      }
      latestBySource.set(event.sourceKey, event);
      continue;
    }
    if (event.kind !== 'reduce-policy') {
      continue;
    }
    let reductionIdentity;
    try {
      reductionIdentity = validatePullIdentity(event.identity);
    } catch {
      foldErrors.push('A policy reduction has an invalid exact-diff identity.');
      continue;
    }
    const before = policySnapshot(
      latestBySource,
      reduced,
      reductionIds,
      foldErrors,
      reductionIdentity);
    const targets = Array.isArray(event.targets) ? event.targets : [];
    const validTargets = targets.length > 0 && targets.every((target) => {
      const observation = [...latestBySource.values()].find((candidate) =>
        candidate.eventId === target?.eventId);
      return observation &&
        !reduced.has(observation.eventId) &&
        target.epochId === observation.observationEpoch?.id &&
        samePullIdentity(
          observation.observationEpoch?.identity,
          reductionIdentity);
    });
    if (event.expectedPolicyDigest !== before.digest ||
        !validTargets ||
        new Set(targets.map((target) => target.eventId)).size !== targets.length ||
        String(event.reason || '').trim().length < 20 ||
        String(event.reason || '').length > 2000 ||
        !repositoryUrl(event.auditUrl, event.repository) ||
        String(event.actor || '').endsWith('[bot]')) {
      foldErrors.push(
        'A policy reduction failed historical digest or observation-epoch validation.');
      continue;
    }
    for (const target of targets) {
      reduced.add(target.eventId);
    }
    reductionIds.push(event.eventId);
  }
  return policySnapshot(
    latestBySource,
    reduced,
    reductionIds,
    foldErrors,
    identity);
}

function reviewMarker(domain, binding) {
  if (!REVIEW_DOMAINS.includes(domain)) {
    throw new Error(`Unknown review domain: ${domain}`);
  }
  const encoded = Buffer.from(
    JSON.stringify(stableValue(binding)),
    'utf8').toString('base64url');
  return `<!-- ${REVIEW_MARKER_PREFIX}${domain}:${encoded} -->`;
}

function reviewMarkers(body) {
  const expression = new RegExp(
    `<!--\\s*${REVIEW_MARKER_PREFIX}` +
    `(${REVIEW_DOMAINS.join('|')}):([A-Za-z0-9_-]+)\\s*-->`,
    'g');
  return [...String(body || '').matchAll(expression)].map((match) => {
    try {
      return {
        domain: match[1],
        binding: JSON.parse(Buffer.from(match[2], 'base64url').toString('utf8')),
        error: null,
      };
    } catch {
      return {
        domain: match[1],
        binding: null,
        error: 'The newest review marker is malformed.',
      };
    }
  });
}

function reviewTime(review) {
  return Date.parse(review?.submitted_at || review?.updated_at || 0) || 0;
}

function latestReview(reviews, predicate) {
  return [...(reviews || [])]
    .filter(predicate)
    .sort((left, right) =>
      reviewTime(right) - reviewTime(left) ||
      Number(right.id || 0) - Number(left.id || 0))[0] || null;
}

function latestCopilotReview(reviews) {
  return latestReview(reviews, (review) =>
    Number(review.user?.id) === COPILOT_REVIEWER.id &&
    review.user?.login === COPILOT_REVIEWER.login &&
    review.user?.type === COPILOT_REVIEWER.type);
}

function latestDomainReview(reviews, domain) {
  return latestReview(reviews, (review) =>
    String(review.body || '').includes(`${REVIEW_MARKER_PREFIX}${domain}:`));
}

function validateEvidenceBinding(binding, {
  domain,
  identity,
  policyDigest,
  repository,
}) {
  if (!binding ||
      binding.schemaVersion !== POLICY_EVENT_SCHEMA_VERSION ||
      binding.kind !== 'independent-review' ||
      binding.domain !== domain ||
      !samePullIdentity(binding, identity) ||
      binding.policyDigest !== policyDigest) {
    return 'The newest review marker is not bound to this exact PR, diff, and policy.';
  }
  if (!repositoryUrl(binding.evidenceUrl, repository)) {
    return 'The newest review marker lacks a repository-local evidence URL.';
  }
  const summary = String(binding.summary || '').trim();
  if (!summary || summary.length > 2000) {
    return 'The newest review marker lacks a bounded evidence summary.';
  }
  return null;
}

function currentBreakGlass(events, identity, policyDigest, repository) {
  const candidate = [...(events || [])]
    .filter((event) => event.kind === 'break-glass' &&
      Number(event.pullNumber) === identity.pullNumber)
    .sort((left, right) =>
      Date.parse(right.createdAt) - Date.parse(left.createdAt) ||
      right.eventId.localeCompare(left.eventId))[0];
  if (!candidate ||
      !samePullIdentity(candidate.identity, identity) ||
      candidate.policyDigest !== policyDigest) {
    return null;
  }
  if (!repositoryUrl(candidate.auditUrl, repository) ||
      String(candidate.reason || '').trim().length < 20 ||
      String(candidate.reason || '').length > 2000 ||
      !candidate.actor ||
      String(candidate.actor).endsWith('[bot]')) {
    return { outcome: 'failure', reason: 'The current break-glass record is invalid.' };
  }
  return { outcome: 'success', actor: candidate.actor, eventId: candidate.eventId };
}

function currentCopilotAttestation(events, review, identity) {
  if (!review) {
    return null;
  }
  return [...(events || [])]
    .filter((event) =>
      event.kind === 'copilot-attestation' &&
      Number(event.reviewId) === Number(review.id) &&
      Number(event.reviewerId) === COPILOT_REVIEWER.id &&
      event.reviewerLogin === COPILOT_REVIEWER.login &&
      samePullIdentity(event.identity, identity))
    .sort((left, right) =>
      Date.parse(right.createdAt) - Date.parse(left.createdAt) ||
      right.eventId.localeCompare(left.eventId))[0] || null;
}

function domainAttestationEvidence(
  events,
  domain,
  identity,
  policyDigest,
  repository,
) {
  const candidate = [...(events || [])]
    .filter((event) =>
      event.kind === 'domain-attestation' &&
      event.domain === domain &&
      Number(event.pullNumber) === identity.pullNumber)
    .sort((left, right) =>
      Date.parse(right.createdAt) - Date.parse(left.createdAt) ||
      right.eventId.localeCompare(left.eventId))[0];
  if (!candidate) {
    return { outcome: 'missing', candidateId: null, observedAt: 0 };
  }
  if (!samePullIdentity(candidate.identity, identity) ||
      candidate.policyDigest !== policyDigest ||
      !repositoryUrl(candidate.evidenceUrl, repository) ||
      !Number.isInteger(Number(candidate.attester?.appId)) ||
      Number(candidate.attester.appId) <= 0 ||
      !candidate.attester?.slug ||
      !Number.isInteger(Number(candidate.attester?.runId)) ||
      Number(candidate.attester.runId) <= 0 ||
      !Number.isInteger(Number(candidate.attester?.runAttempt)) ||
      Number(candidate.attester.runAttempt) <= 0 ||
      !/^[0-9a-f]{40}$/.test(String(candidate.attester?.workflowRevision || '')) ||
      !Number.isInteger(Number(candidate.artifact?.id)) ||
      Number(candidate.artifact.id) <= 0 ||
      !candidate.artifact?.name ||
      !/^[0-9a-f]{64}$/.test(String(candidate.artifact?.sha256 || '')) ||
      !/^[0-9a-f]{64}$/.test(String(candidate.artifact?.manifestDigest || '')) ||
      !Number.isFinite(Date.parse(candidate.artifact?.downloadedAt || '')) ||
      !String(candidate.summary || '').trim() ||
      String(candidate.summary).length > 2000) {
    return {
      outcome: 'failure',
      candidateId: candidate.eventId,
      observedAt: Date.parse(candidate.createdAt) || 0,
      reason: `The newest automated ${domain} attestation is stale or invalid.`,
    };
  }
  if (candidate.outcome === 'approved') {
    return {
      outcome: 'success',
      candidateId: candidate.eventId,
      observedAt: Date.parse(candidate.createdAt) || 0,
      reviewer: `${candidate.attester.slug}[app]`,
      binding: candidate,
    };
  }
  return {
    outcome: candidate.outcome === 'rejected' ? 'failure' : 'pending',
    candidateId: candidate.eventId,
    observedAt: Date.parse(candidate.createdAt) || 0,
    reason: `The newest automated ${domain} attestation is ` +
      `${candidate.outcome || 'incomplete'}.`,
  };
}

function evaluateReviewCompletion({
  pullRequest,
  policy,
  reviews = [],
  unresolvedThreads = 0,
  domainEvidence = {},
  policyEvents = [],
  path = 'pull_request',
}) {
  if (path !== 'pull_request') {
    return {
      state: 'rejected',
      reasons: [
        `${path}: policy must be revalidated by its explicit external-worker handler.`,
      ],
    };
  }
  const identity = pullIdentity(pullRequest);
  const policyContext = `Authenticated policy digest ${policy.digest}; ` +
    `active event IDs: ${policy.observationIds.join(', ') || 'none'}.`;
  if (pullRequest.state !== 'open') {
    return {
      state: 'rejected',
      reasons: ['Only an open PR can satisfy the gate.', policyContext],
    };
  }
  if (pullRequest.draft) {
    return {
      state: 'rejected',
      reasons: ['Draft PRs are not ready for merge.', policyContext],
    };
  }
  if (unresolvedThreads > 0) {
    return {
      state: 'rejected',
      reasons: [
        `${unresolvedThreads} unresolved review thread(s) remain.`,
        policyContext,
      ],
    };
  }
  if (policy.errors.length > 0) {
    return {
      state: 'rejected',
      reasons: [
        'The authenticated policy ledger is malformed; manual repair is required.',
        policyContext,
      ],
    };
  }
  if (policy.currentEpochComplete === false) {
    return {
      state: 'pending',
      reasons: [
        'Authenticated policy observations have not been carried to this exact diff epoch.',
        policyContext,
      ],
    };
  }
  if (!policy.initialized) {
    return {
      state: 'rejected',
      reasons: [
        'No authenticated issue-policy association has been recorded by the review-gate App.',
        policyContext,
      ],
    };
  }

  const breakGlass = currentBreakGlass(
    policyEvents,
    identity,
    policy.digest,
    identity.baseRepository);
  if (breakGlass?.outcome === 'failure') {
    return { state: 'rejected', reasons: [breakGlass.reason] };
  }
  if (breakGlass?.outcome === 'success') {
    return {
      state: 'approved',
      reasons: [
        'An authenticated human break-glass record matches the exact PR, diff, and policy.',
        'Draft and unresolved-thread protections remain enforced.',
        policyContext,
      ],
    };
  }

  const rejected = [];
  const pending = [];
  const copilot = latestCopilotReview(reviews);
  const copilotAttestation = currentCopilotAttestation(
    policyEvents,
    copilot,
    identity);
  const copilotState = String(copilot?.state || '').toUpperCase();
  if (!copilot) {
    pending.push('GitHub Copilot has not submitted an authenticated review.');
  } else if (copilot.commit_id !== identity.headSha) {
    pending.push(`The newest Copilot review is not bound to head ${identity.headSha}.`);
  } else if (copilotState === 'DISMISSED' ||
      copilotState === 'CHANGES_REQUESTED') {
    rejected.push(`The newest current-head Copilot review is ${copilotState.toLowerCase()}.`);
  } else if (!new Set(['COMMENTED', 'APPROVED']).has(copilotState)) {
    pending.push('The newest current-head Copilot review has not completed.');
  } else if (!copilotAttestation) {
    pending.push(
      'The newest Copilot review lacks an App attestation for this exact PR/head/base.');
  }

  for (const domain of policy.domains) {
    const evidence = domainEvidence[domain];
    if (evidence?.outcome === 'failure') {
      rejected.push(evidence.reason ||
        `The newest ${domain} review evidence is invalid or rejected.`);
    } else if (evidence?.outcome !== 'success') {
      pending.push(evidence?.reason ||
        `Independent ${domain} review evidence is missing for the current policy.`);
    }
  }
  if (rejected.length > 0) {
    return {
      state: 'rejected',
      reasons: [...rejected, ...pending, policyContext],
    };
  }
  if (pending.length > 0) {
    return { state: 'pending', reasons: [...pending, policyContext] };
  }
  return {
    state: 'approved',
    reasons: [
      `Copilot review completed for current head ${identity.headSha}.`,
      `Reviewed base is ${identity.baseRepository}:${identity.baseRef}@${identity.baseSha}.`,
      `Authenticated policy ${policy.digest} requires issue(s) ` +
        `${policy.associations.join(', ')} and domain(s) ` +
        `${policy.domains.join(', ') || 'none'}.`,
      `Active policy event IDs: ${policy.observationIds.join(', ') || 'none'}.`,
      'Zero unresolved review threads remain.',
    ],
  };
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
    ...(result.reasons || []).map((reason) => `- ${reason}`),
  ].join('\n');
}

module.exports = {
  CHECK_EXTERNAL_PREFIX,
  CHECK_NAME,
  CONTRACT_REVISION,
  COPILOT_REVIEWER,
  POLICY_EVENT_MARKER_PREFIX,
  POLICY_EVENT_SCHEMA_VERSION,
  REQUIRED_LABELS,
  REVIEW_DOMAINS,
  REVIEW_MARKER_PREFIX,
  currentBreakGlass,
  currentCopilotAttestation,
  domainAttestationEvidence,
  evaluateReviewCompletion,
  foldPolicyEvents,
  labelNames,
  latestCopilotReview,
  latestDomainReview,
  latestReview,
  makePolicyEvent,
  makeObservationEpoch,
  parsePolicyEventComment,
  policyEventValidationError,
  policySnapshot,
  policyEventDigest,
  policyEventComment,
  pullIdentity,
  repositoryUrl,
  requiredDomains,
  reviewMarker,
  reviewMarkers,
  samePullIdentity,
  securityDigest,
  stableValue,
  summarizeResult,
  trustedPolicyEvents,
  validObservationEpoch,
  validatePullIdentity,
  validateEvidenceBinding,
};
