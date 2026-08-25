'use strict';

const crypto = require('node:crypto');

const {
  CHECK_EXTERNAL_PREFIX,
  CHECK_NAME,
  CONTRACT_REVISION,
  COPILOT_REVIEWER,
  REVIEW_DOMAINS,
  domainAttestationEvidence,
  evaluateReviewCompletion,
  foldPolicyEvents,
  latestDomainReview,
  makeObservationEpoch,
  makePolicyEvent,
  parsePolicyEventComment,
  policyEventComment,
  pullIdentity,
  repositoryUrl,
  requiredDomains,
  reviewMarkers,
  samePullIdentity,
  securityDigest,
  summarizeResult,
  trustedPolicyEvents,
  validateEvidenceBinding,
  validatePullIdentity,
} = require('./review-gate-policy');

const AUTHORIZED_REVIEW_PERMISSIONS = new Set(['write', 'maintain', 'admin']);
const ADMIN_PERMISSIONS = new Set(['maintain', 'admin']);
const DEFAULT_POLL_SECONDS = 30;
const DEFAULT_MAX_WAIT_SECONDS = 12 * 60;
const DEFAULT_STABILITY_SECONDS = 5;
const EVENT_PATHS = new Set([
  'pull_request',
  'pull_request_review',
  'pull_request_review_comment',
  'pull_request_review_thread',
  'issue_comment',
  'issues',
  'push',
  'member',
  'membership',
  'organization',
  'reconciliation',
  'merge_group',
  'specialist_attestation',
  'trusted_dispatch',
]);
const WEBHOOK_PATHS = new Set([
  'pull_request',
  'pull_request_review',
  'pull_request_review_comment',
  'pull_request_review_thread',
  'issue_comment',
  'issues',
  'push',
  'member',
  'membership',
  'organization',
  'merge_group',
]);

function sanitizedApiFailure(error) {
  if (error?.status === 403 || error?.status === 429) {
    return 'GitHub metadata API was rate-limited or unavailable; the App check failed closed.';
  }
  return 'GitHub metadata evaluation failed; the App check failed closed.';
}

function requireStoreMethod(store, name) {
  if (typeof store?.[name] !== 'function') {
    throw new Error(`The external worker requires durable store method ${name}.`);
  }
  return store[name].bind(store);
}

function assertExternalEnvelope(envelope) {
  const repository = envelope?.repository;
  const delivery = envelope?.delivery;
  const worker = envelope?.worker;
  if (!repository ||
      !Number.isInteger(Number(repository.id)) ||
      Number(repository.id) <= 0 ||
      !repository.fullName ||
      !repository.owner ||
      !repository.name ||
      !repository.defaultBranch ||
      !delivery ||
      !delivery.id ||
      !delivery.runId ||
      !EVENT_PATHS.has(delivery.eventPath) ||
      delivery.authenticated !== true ||
      !worker ||
      worker.hostKind !== 'independent-app-worker' ||
      !/^[0-9a-f]{64}$/.test(String(worker.revision || '')) ||
      !worker.instanceId) {
    throw new Error(
      'The event lacks authenticated independent-worker delivery provenance.');
  }
  if (WEBHOOK_PATHS.has(delivery.eventPath) &&
      delivery.source !== 'github-app-webhook') {
    throw new Error('GitHub webhook events require verified App-webhook provenance.');
  }
  if (delivery.eventPath === 'reconciliation' &&
      delivery.source !== 'trusted-scheduler') {
    throw new Error('Reconciliation requires the independently hosted scheduler.');
  }
  if (delivery.eventPath === 'trusted_dispatch' &&
      delivery.source !== 'trusted-admin') {
    throw new Error('Manual dispatch requires the independently hosted admin ingress.');
  }
  if (delivery.eventPath === 'specialist_attestation' &&
      delivery.source !== 'trusted-specialist-broker') {
    throw new Error(
      'Specialist evidence requires the independently hosted artifact broker.');
  }
  if (String(delivery.source).includes('actions')) {
    throw new Error('Repository Actions cannot host or authenticate this publisher.');
  }
  return envelope;
}

function repoParameters(envelope) {
  return {
    owner: envelope.repository.owner,
    repo: envelope.repository.name,
  };
}

function eventDeliveryId(envelope, suffix = '') {
  const value = [
    envelope.delivery.id,
    envelope.delivery.runId,
    envelope.delivery.eventPath,
    suffix,
  ].join(':');
  return value.slice(0, 240);
}

function eventEpoch(envelope, identity, sourceKey) {
  const deliveryId = eventDeliveryId(envelope, sourceKey);
  return makeObservationEpoch(identity, {
    deliveryId,
    eventPath: envelope.delivery.eventPath,
    workerRevision: envelope.worker.revision,
  });
}

async function listReviewThreads({ client, envelope, pullNumber }) {
  const threads = [];
  const seenCursors = new Set();
  let cursor = null;
  do {
    const result = await client.graphql(
      `query($owner: String!, $repo: String!, $number: Int!, $cursor: String) {
        repository(owner: $owner, name: $repo) {
          pullRequest(number: $number) {
            reviewThreads(first: 100, after: $cursor) {
              nodes {
                id
                isResolved
                comments(last: 1) {
                  nodes { id updatedAt }
                }
              }
              pageInfo { hasNextPage endCursor }
            }
          }
        }
      }`,
      {
        owner: envelope.repository.owner,
        repo: envelope.repository.name,
        number: pullNumber,
        cursor,
      });
    const connection = result.repository?.pullRequest?.reviewThreads;
    if (!connection) {
      throw new Error('Review-thread metadata was unavailable.');
    }
    threads.push(...connection.nodes);
    if (!connection.pageInfo.hasNextPage) {
      break;
    }
    cursor = connection.pageInfo.endCursor;
    if (!cursor || seenCursors.has(cursor)) {
      throw new Error('Review-thread pagination did not advance.');
    }
    seenCursors.add(cursor);
  } while (true);
  return threads;
}

async function listPolicyComments({ client, envelope, pullNumber }) {
  return client.paginate(client.rest.issues.listComments, {
    ...repoParameters(envelope),
    issue_number: pullNumber,
    per_page: 100,
  });
}

async function loadPolicyLedger({
  client,
  envelope,
  pullRequest,
  publisherAppId,
}) {
  const comments = await listPolicyComments({
    client,
    envelope,
    pullNumber: pullRequest.number,
  });
  const identity = pullIdentity(pullRequest);
  const trusted = trustedPolicyEvents(comments, {
    appId: publisherAppId,
    repositoryId: envelope.repository.id,
    repository: envelope.repository.fullName,
    pullNumber: pullRequest.number,
  });
  return {
    ...trusted,
    comments,
    snapshot: foldPolicyEvents(
      trusted.events,
      trusted.errors,
      { identity }),
  };
}

async function appendPolicyEvent({
  client,
  envelope,
  event,
  publisherAppId,
}) {
  const response = await client.rest.issues.createComment({
    ...repoParameters(envelope),
    issue_number: event.pullNumber,
    body: policyEventComment(event),
  });
  if (Number(response.data?.performed_via_github_app?.id) !==
      Number(publisherAppId)) {
    throw new Error('Policy event publisher does not match the configured GitHub App.');
  }
  return response.data;
}

async function recordCopilotAttestationFromEvent({
  client,
  envelope,
  publisherAppId,
}) {
  if (envelope.delivery.eventPath !== 'pull_request_review' ||
      envelope.payload?.action !== 'submitted') {
    return null;
  }
  const review = envelope.payload.review;
  const pullRequest = envelope.payload.pull_request;
  if (Number(review?.user?.id) !== COPILOT_REVIEWER.id ||
      review?.user?.login !== COPILOT_REVIEWER.login ||
      review?.user?.type !== COPILOT_REVIEWER.type) {
    return null;
  }
  const identity = pullIdentity(pullRequest);
  if (review.commit_id !== identity.headSha) {
    throw new Error('The Copilot review event is not attached to the current head.');
  }
  const event = makePolicyEvent({
    kind: 'copilot-attestation',
    repositoryId: Number(envelope.repository.id),
    repository: envelope.repository.fullName,
    pullNumber: pullRequest.number,
    deliveryId: eventDeliveryId(envelope, `copilot-review:${review.id}`),
    actor: COPILOT_REVIEWER.login,
    data: {
      identity,
      reviewId: Number(review.id),
      reviewerId: COPILOT_REVIEWER.id,
      reviewerLogin: COPILOT_REVIEWER.login,
      reviewSubmittedAt: review.submitted_at,
    },
  });
  await appendPolicyEvent({ client, envelope, event, publisherAppId });
  return event;
}

async function restoreDeletedPolicyEventFromEvent({
  client,
  envelope,
  publisherAppId,
}) {
  if (envelope.delivery.eventPath !== 'issue_comment' ||
      envelope.payload?.action !== 'deleted' ||
      !envelope.payload.issue?.pull_request ||
      Number(envelope.payload.comment?.performed_via_github_app?.id) !==
        Number(publisherAppId)) {
    return null;
  }
  const parsed = parsePolicyEventComment(envelope.payload.comment.body);
  if (parsed.error ||
      Number(parsed.event.pullNumber) !== Number(envelope.payload.issue.number)) {
    throw new Error('A deleted App policy event could not be restored safely.');
  }
  await appendPolicyEvent({
    client,
    envelope,
    event: parsed.event,
    publisherAppId,
  });
  return parsed.event;
}

async function labelsForIssue({ client, envelope, issueNumber }) {
  return client.paginate(client.rest.issues.listLabelsOnIssue, {
    ...repoParameters(envelope),
    issue_number: issueNumber,
    per_page: 100,
  });
}

function observationEvent({
  envelope,
  pullRequest,
  kind,
  sourceKey,
  data,
}) {
  const identity = pullIdentity(pullRequest);
  const observationEpoch = eventEpoch(envelope, identity, sourceKey);
  return makePolicyEvent({
    kind,
    repositoryId: Number(envelope.repository.id),
    repository: envelope.repository.fullName,
    pullNumber: pullRequest.number,
    deliveryId: observationEpoch.deliveryId,
    actor: envelope.actor || 'github-app',
    data: {
      sourceKey,
      observationEpoch,
      ...data,
    },
  });
}

function sourceDefinition(event) {
  if (event.kind === 'bind-issue') {
    return {
      kind: event.kind,
      sourceKey: event.sourceKey,
      issueNumber: Number(event.issueNumber),
      identity: event.identity,
      reason: event.reason,
      auditUrl: event.auditUrl,
    };
  }
  return {
    kind: event.kind,
    sourceKey: event.sourceKey,
    domain: event.domain,
    sourceKind: event.sourceKind,
    sourceNumber: Number(event.sourceNumber),
  };
}

async function observeCurrentRequirements({
  client,
  envelope,
  pullRequest,
  publisherAppId,
}) {
  let ledger = await loadPolicyLedger({
    client,
    envelope,
    pullRequest,
    publisherAppId,
  });
  if (ledger.snapshot.errors.length > 0) {
    return ledger;
  }
  const identity = pullIdentity(pullRequest);
  const desired = new Map();
  for (const [sourceKey, eventId] of Object.entries(
    ledger.snapshot.activeSources)) {
    const event = ledger.events.find((candidate) => candidate.eventId === eventId);
    if (event) {
      desired.set(sourceKey, sourceDefinition(event));
    }
  }

  const pullLabels = await labelsForIssue({
    client,
    envelope,
    issueNumber: pullRequest.number,
  });
  for (const domain of requiredDomains([pullLabels])) {
    const sourceKey = `pull:${pullRequest.number}:domain:${domain}`;
    desired.set(sourceKey, {
      kind: 'require-domain',
      sourceKey,
      domain,
      sourceKind: 'pull-label',
      sourceNumber: pullRequest.number,
    });
  }
  for (const issueNumber of ledger.snapshot.associations) {
    const labels = await labelsForIssue({ client, envelope, issueNumber });
    for (const domain of requiredDomains([labels])) {
      const sourceKey = `issue:${issueNumber}:domain:${domain}`;
      desired.set(sourceKey, {
        kind: 'require-domain',
        sourceKey,
        domain,
        sourceKind: 'issue-label',
        sourceNumber: issueNumber,
      });
    }
  }

  const observations = [];
  for (const source of desired.values()) {
    const latest = ledger.snapshot.latestSources[source.sourceKey];
    if (latest &&
        !latest.reduced &&
        samePullIdentity(latest.observationEpoch?.identity, identity)) {
      continue;
    }
    const data = source.kind === 'bind-issue'
      ? {
          issueNumber: source.issueNumber,
          identity,
          reason: source.reason ||
            'Authenticated policy association carried to the current diff.',
          auditUrl: source.auditUrl ||
            `https://github.com/${envelope.repository.fullName}/pull/` +
              `${pullRequest.number}`,
        }
      : {
          domain: source.domain,
          sourceKind: source.sourceKind,
          sourceNumber: source.sourceNumber,
        };
    observations.push(observationEvent({
      envelope,
      pullRequest,
      kind: source.kind,
      sourceKey: source.sourceKey,
      data,
    }));
  }
  for (const event of observations) {
    await appendPolicyEvent({ client, envelope, event, publisherAppId });
  }
  if (observations.length > 0) {
    ledger = await loadPolicyLedger({
      client,
      envelope,
      pullRequest,
      publisherAppId,
    });
  }
  return ledger;
}

async function reviewerPermission({ client, envelope, login, cache }) {
  if (!cache.has(login)) {
    const response = await client.rest.repos.getCollaboratorPermissionLevel({
      ...repoParameters(envelope),
      username: login,
    });
    cache.set(login, response.data.permission);
  }
  return cache.get(login);
}

async function domainReviewEvidence({
  client,
  envelope,
  reviews,
  domain,
  identity,
  policyDigest,
  author,
  permissionCache,
}) {
  const review = latestDomainReview(reviews, domain);
  if (!review) {
    return { outcome: 'missing', candidateId: null };
  }
  const markers = reviewMarkers(review.body)
    .filter((marker) => marker.domain === domain);
  if (markers.length !== 1 || markers[0].error) {
    return {
      outcome: 'failure',
      candidateId: review.id,
      reason: markers[0]?.error ||
        `The newest ${domain} review contains ambiguous evidence markers.`,
    };
  }
  const reviewer = String(review.user?.login || '');
  if (!reviewer || review.user?.type !== 'User') {
    return {
      outcome: 'failure',
      candidateId: review.id,
      reason: `The newest ${domain} review is not from an authenticated human.`,
    };
  }
  if (reviewer.toLowerCase() === String(author || '').toLowerCase()) {
    return {
      outcome: 'failure',
      candidateId: review.id,
      reason: `The PR author cannot provide independent ${domain} evidence.`,
    };
  }
  const permission = await reviewerPermission({
    client,
    envelope,
    login: reviewer,
    cache: permissionCache,
  });
  if (!AUTHORIZED_REVIEW_PERMISSIONS.has(permission)) {
    return {
      outcome: 'failure',
      candidateId: review.id,
      reason: `The newest ${domain} reviewer is not authorized for this repository.`,
    };
  }
  const bindingError = validateEvidenceBinding(markers[0].binding, {
    domain,
    identity,
    policyDigest,
    repository: identity.baseRepository,
  });
  if (bindingError) {
    return {
      outcome: 'failure',
      candidateId: review.id,
      reason: bindingError,
    };
  }
  if (review.commit_id !== identity.headSha) {
    return {
      outcome: 'failure',
      candidateId: review.id,
      reason: `The newest ${domain} review is not attached to the current head.`,
    };
  }
  const state = String(review.state || '').toUpperCase();
  if (state === 'APPROVED') {
    return {
      outcome: 'success',
      candidateId: review.id,
      reviewer,
      binding: markers[0].binding,
    };
  }
  if (state === 'CHANGES_REQUESTED' || state === 'DISMISSED') {
    return {
      outcome: 'failure',
      candidateId: review.id,
      reviewer,
      reason: `The newest independent ${domain} review is ${state.toLowerCase()}.`,
    };
  }
  return {
    outcome: 'pending',
    candidateId: review.id,
    reviewer,
    reason: `The newest independent ${domain} review has not approved the current policy.`,
  };
}

async function validateAndRecordSpecialistArtifact({
  client,
  envelope,
  pullRequest,
  publisherAppId,
  allowlist = [],
}) {
  const request = envelope.payload?.attestation || {};
  const identity = pullIdentity(pullRequest);
  const ledger = await loadPolicyLedger({
    client,
    envelope,
    pullRequest,
    publisherAppId,
  });
  const allowed = allowlist.find((candidate) =>
    Number(candidate.appId) === Number(request.attester?.appId) &&
    candidate.slug === request.attester?.slug &&
    candidate.workflowRevision === request.attester?.workflowRevision);
  if (!REVIEW_DOMAINS.includes(request.domain) ||
      !new Set(['approved', 'rejected', 'pending']).has(request.outcome) ||
      !allowed ||
      !samePullIdentity(request.identity, identity) ||
      request.policyDigest !== ledger.snapshot.digest ||
      !repositoryUrl(request.evidenceUrl, identity.baseRepository) ||
      !String(request.summary || '').trim() ||
      String(request.summary).length > 2000) {
    throw new Error('The specialist attestation request is stale or unauthorized.');
  }
  const run = await client.getSpecialistRun({
    repository: envelope.repository.fullName,
    appId: Number(request.attester.appId),
    runId: Number(request.attester.runId),
  });
  const artifactId = Number(request.artifact?.id);
  if (Number(run?.appId) !== Number(request.attester.appId) ||
      run?.slug !== request.attester.slug ||
      Number(run?.runId) !== Number(request.attester.runId) ||
      Number(run?.runAttempt) !== Number(request.attester.runAttempt) ||
      run?.workflowRevision !== request.attester.workflowRevision ||
      run?.repository !== identity.baseRepository ||
      run?.headSha !== identity.headSha ||
      run?.status !== 'completed' ||
      run?.conclusion !== 'success' ||
      !Array.isArray(run?.artifactIds) ||
      !run.artifactIds.map(Number).includes(artifactId)) {
    throw new Error('The specialist run provenance did not match the exact diff.');
  }
  const artifact = await client.downloadSpecialistArtifact({
    repository: envelope.repository.fullName,
    runId: Number(request.attester.runId),
    artifactId,
  });
  const bytes = Buffer.isBuffer(artifact?.bytes)
    ? artifact.bytes
    : Buffer.from(artifact?.bytes || '');
  if (artifact?.expired ||
      Number(artifact?.id) !== artifactId ||
      artifact?.name !== request.artifact?.name ||
      bytes.length === 0 ||
      bytes.length > 1024 * 1024) {
    throw new Error('The specialist artifact was unavailable or outside safe bounds.');
  }
  const sha256 = crypto.createHash('sha256').update(bytes).digest('hex');
  if (sha256 !== request.artifact.sha256) {
    throw new Error('The downloaded specialist artifact digest did not match.');
  }
  let manifest;
  try {
    manifest = JSON.parse(bytes.toString('utf8'));
  } catch {
    throw new Error('The specialist artifact is not a valid evidence manifest.');
  }
  if (manifest.schemaVersion !== 1 ||
      manifest.kind !== 'andreja-review-evidence' ||
      manifest.domain !== request.domain ||
      manifest.outcome !== request.outcome ||
      !samePullIdentity(manifest.identity, identity) ||
      manifest.policyDigest !== ledger.snapshot.digest ||
      manifest.evidenceUrl !== request.evidenceUrl ||
      manifest.summary !== request.summary) {
    throw new Error('The downloaded specialist manifest is stale or malformed.');
  }
  const event = makePolicyEvent({
    kind: 'domain-attestation',
    repositoryId: Number(envelope.repository.id),
    repository: envelope.repository.fullName,
    pullNumber: identity.pullNumber,
    deliveryId: eventDeliveryId(
      envelope,
      `specialist:${request.attester.appId}:${request.attester.runId}`),
    actor: `${request.attester.slug}[app]`,
    data: {
      domain: request.domain,
      identity,
      policyDigest: ledger.snapshot.digest,
      outcome: request.outcome,
      attester: {
        appId: Number(request.attester.appId),
        slug: request.attester.slug,
        runId: Number(request.attester.runId),
        runAttempt: Number(request.attester.runAttempt),
        workflowRevision: request.attester.workflowRevision,
      },
      artifact: {
        id: artifactId,
        name: artifact.name,
        sha256,
        manifestDigest: securityDigest(manifest),
        downloadedAt: new Date().toISOString(),
      },
      evidenceUrl: request.evidenceUrl,
      summary: request.summary,
    },
  });
  await appendPolicyEvent({
    client,
    envelope,
    event,
    publisherAppId,
  });
  return event;
}

function newestDomainCandidate(human, automated) {
  const candidates = [human, automated]
    .filter((candidate) => candidate && candidate.outcome !== 'missing')
    .sort((left, right) =>
      Number(right.observedAt || 0) - Number(left.observedAt || 0) ||
      (right.outcome === 'failure' ? 1 : 0) -
        (left.outcome === 'failure' ? 1 : 0) ||
      String(right.candidateId || '').localeCompare(
        String(left.candidateId || '')));
  return candidates[0] || { outcome: 'missing', candidateId: null, observedAt: 0 };
}

function reviewSecurityState(reviews) {
  return reviews.map((review) => ({
    id: Number(review.id),
    login: review.user?.login || '',
    userId: Number(review.user?.id || 0),
    type: review.user?.type || '',
    state: review.state || '',
    commitId: review.commit_id || '',
    submittedAt: review.submitted_at || '',
    bodyDigest: securityDigest(String(review.body || '')),
  })).sort((left, right) => left.id - right.id);
}

function threadSecurityState(threads) {
  return threads.map((thread) => ({
    id: thread.id,
    isResolved: Boolean(thread.isResolved),
    comments: thread.comments?.nodes || [],
  })).sort((left, right) => String(left.id).localeCompare(String(right.id)));
}

function basicPullMapping(envelope, pullRequest) {
  const identity = pullIdentity(pullRequest);
  return {
    repositoryId: Number(envelope.repository.id),
    repository: envelope.repository.fullName,
    pullNumber: identity.pullNumber,
    headSha: identity.headSha,
    baseRepositoryId: identity.baseRepositoryId,
    baseRepository: identity.baseRepository,
    baseRef: identity.baseRef,
    baseSha: identity.baseSha,
    issueNumbers: [],
    reviewerLogins: [],
    open: pullRequest.state === 'open',
  };
}

function mappingIdentity(mapping) {
  return validatePullIdentity({
    pullNumber: mapping.pullNumber,
    headSha: mapping.headSha,
    baseRepositoryId: mapping.baseRepositoryId,
    baseRepository: mapping.baseRepository,
    baseRef: mapping.baseRef,
    baseSha: mapping.baseSha,
  });
}

async function persistPullMapping(store, mapping) {
  return requireStoreMethod(store, 'upsertPullMapping')(mapping);
}

async function evaluatePullSnapshot({
  client,
  store,
  envelope,
  pullNumber,
  expectedHeadSha,
  publisherAppId,
  requiredBase = null,
}) {
  const pullResponse = await client.rest.pulls.get({
    ...repoParameters(envelope),
    pull_number: pullNumber,
  });
  const pullRequest = pullResponse.data;
  const identity = pullIdentity(pullRequest);
  if (identity.headSha !== expectedHeadSha) {
    return {
      stale: true,
      pullNumber,
      result: {
        state: 'rejected',
        reasons: ['The PR head changed during this App generation.'],
      },
      fingerprint: null,
    };
  }
  if (requiredBase &&
      (Number(identity.baseRepositoryId) !== Number(requiredBase.repositoryId) ||
       identity.baseRepository.toLowerCase() !==
         String(requiredBase.repository).toLowerCase() ||
       identity.baseRef !== requiredBase.ref ||
       identity.baseSha !== requiredBase.sha)) {
    return {
      stale: true,
      pullNumber,
      result: {
        state: 'rejected',
        reasons: ['The PR base is not the exact current merge/base-push identity.'],
      },
      fingerprint: null,
    };
  }
  const ledger = await observeCurrentRequirements({
    client,
    envelope,
    pullRequest,
    publisherAppId,
  });
  const [reviews, threads] = await Promise.all([
    client.paginate(client.rest.pulls.listReviews, {
      ...repoParameters(envelope),
      pull_number: pullNumber,
      per_page: 100,
    }),
    listReviewThreads({ client, envelope, pullNumber }),
  ]);
  const permissionCache = new Map();
  const entries = await Promise.all(ledger.snapshot.domains.map(async (domain) => {
    const human = await domainReviewEvidence({
      client,
      envelope,
      reviews,
      domain,
      identity,
      policyDigest: ledger.snapshot.digest,
      author: pullRequest.user?.login,
      permissionCache,
    });
    const humanReview = latestDomainReview(reviews, domain);
    human.observedAt = Date.parse(
      humanReview?.submitted_at || humanReview?.updated_at || 0) || 0;
    const automated = domainAttestationEvidence(
      ledger.events,
      domain,
      identity,
      ledger.snapshot.digest,
      identity.baseRepository);
    return [domain, newestDomainCandidate(human, automated)];
  }));
  const domainEvidence = Object.fromEntries(entries);
  const unresolvedThreads = threads.filter((thread) => !thread.isResolved).length;
  const result = evaluateReviewCompletion({
    pullRequest,
    policy: ledger.snapshot,
    reviews,
    unresolvedThreads,
    domainEvidence,
    policyEvents: ledger.events,
  });
  const securityState = {
    identity,
    state: pullRequest.state,
    draft: Boolean(pullRequest.draft),
    author: pullRequest.user?.login || '',
    policy: ledger.snapshot,
    reviews: reviewSecurityState(reviews),
    threads: threadSecurityState(threads),
    domainEvidence,
    result,
  };
  await persistPullMapping(store, {
    ...basicPullMapping(envelope, pullRequest),
    issueNumbers: ledger.snapshot.associations,
    reviewerLogins: [...new Set(reviews
      .filter((review) => review.user?.type === 'User')
      .map((review) => review.user.login)
      .filter(Boolean))].sort(),
  });
  return {
    stale: false,
    pullNumber,
    identity,
    policy: ledger.snapshot,
    result,
    fingerprint: securityDigest(securityState),
    securityState,
  };
}

async function openPullsSharingHead({ client, envelope, headSha }) {
  const pulls = await client.paginate(client.rest.pulls.list, {
    ...repoParameters(envelope),
    state: 'open',
    per_page: 100,
  });
  return pulls
    .filter((pullRequest) => pullRequest.head?.sha === headSha)
    .sort((left, right) => left.number - right.number);
}

function aggregateSnapshots(snapshots, headSha) {
  const rejected = snapshots.filter((snapshot) =>
    snapshot.stale || snapshot.result.state === 'rejected');
  const pending = snapshots.filter((snapshot) =>
    !snapshot.stale && snapshot.result.state === 'pending');
  let result;
  if (snapshots.length === 0) {
    result = {
      state: 'rejected',
      reasons: [`No open PR is bound to head ${headSha}.`],
    };
  } else if (rejected.length > 0) {
    result = {
      state: 'rejected',
      reasons: rejected.flatMap((snapshot) =>
        snapshot.result.reasons.map((reason) =>
          `PR #${snapshot.pullNumber || 'unknown'}: ${reason}`)),
    };
  } else if (pending.length > 0) {
    result = {
      state: 'pending',
      reasons: pending.flatMap((snapshot) =>
        snapshot.result.reasons.map((reason) =>
          `PR #${snapshot.pullNumber}: ${reason}`)),
    };
  } else {
    result = {
      state: 'approved',
      reasons: [
        `Every open PR sharing head ${headSha} passed its exact PR/base/policy review.`,
        `Evaluated PRs: ${snapshots.map((snapshot) =>
          `#${snapshot.pullNumber}`).join(', ')}.`,
      ],
    };
  }
  return {
    result,
    snapshots,
    fingerprint: securityDigest({
      headSha,
      pulls: snapshots.map((snapshot) => ({
        pullNumber: snapshot.pullNumber,
        fingerprint: snapshot.fingerprint,
        result: snapshot.result,
      })),
    }),
  };
}

async function evaluateHeadSnapshot({
  client,
  store,
  envelope,
  headSha,
  publisherAppId,
  requiredBase = null,
}) {
  const pulls = await openPullsSharingHead({ client, envelope, headSha });
  const snapshots = [];
  for (const pullRequest of pulls) {
    snapshots.push(await evaluatePullSnapshot({
      client,
      store,
      envelope,
      pullNumber: pullRequest.number,
      expectedHeadSha: headSha,
      publisherAppId,
      requiredBase,
    }));
  }
  return aggregateSnapshots(snapshots, headSha);
}

async function sleep(milliseconds) {
  await new Promise((resolve) => setTimeout(resolve, milliseconds));
}

async function evaluateHeadUntilStable({
  client,
  store,
  envelope,
  headSha,
  publisherAppId,
  requiredBase = null,
  poll = true,
  pollSeconds = DEFAULT_POLL_SECONDS,
  maxWaitSeconds = DEFAULT_MAX_WAIT_SECONDS,
  stabilitySeconds = DEFAULT_STABILITY_SECONDS,
  sleepFunction = sleep,
  now = () => Date.now(),
}) {
  const deadline = now() + maxWaitSeconds * 1000;
  let snapshot;
  while (true) {
    snapshot = await evaluateHeadSnapshot({
      client,
      store,
      envelope,
      headSha,
      publisherAppId,
      requiredBase,
    });
    if (snapshot.result.state === 'rejected') {
      return snapshot;
    }
    if (snapshot.result.state === 'approved') {
      await sleepFunction(stabilitySeconds * 1000);
      const confirmation = await evaluateHeadSnapshot({
        client,
        store,
        envelope,
        headSha,
        publisherAppId,
        requiredBase,
      });
      if (confirmation.result.state === 'rejected') {
        return confirmation;
      }
      if (confirmation.result.state === 'approved' &&
          confirmation.fingerprint === snapshot.fingerprint) {
        return confirmation;
      }
      snapshot = confirmation;
    }
    if (!poll) {
      return snapshot;
    }
    if (now() >= deadline) {
      return {
        ...snapshot,
        result: {
          state: 'rejected',
          reasons: [
            ...snapshot.result.reasons,
            `Review automation did not complete within ${maxWaitSeconds} seconds.`,
          ],
        },
      };
    }
    await sleepFunction(pollSeconds * 1000);
  }
}

function normalizedTargets(mappings, baseOverride = null) {
  return [...mappings]
    .map((mapping) => {
      const identity = mappingIdentity(mapping);
      return {
        ...identity,
        ...(baseOverride
          ? {
              baseRepositoryId: Number(baseOverride.repositoryId),
              baseRepository: baseOverride.repository,
              baseRef: baseOverride.ref,
              baseSha: baseOverride.sha,
            }
          : {}),
      };
    })
    .sort((left, right) => left.pullNumber - right.pullNumber);
}

function validateGenerationProvenance(provenance) {
  if (provenance.schemaVersion !== 4 ||
      provenance.contractRevision !== CONTRACT_REVISION ||
      !EVENT_PATHS.has(provenance.eventPath) ||
      !/^[0-9a-f]{64}$/.test(String(provenance.workerRevision || '')) ||
      !provenance.workerInstanceId ||
      !provenance.deliveryId ||
      !provenance.runId ||
      !/^[0-9a-f]{40}$/.test(String(provenance.headSha || '')) ||
      !Number.isInteger(Number(provenance.sequence)) ||
      Number(provenance.sequence) <= 0 ||
      !provenance.generationId ||
      !provenance.association?.kind ||
      !Number.isInteger(Number(provenance.base?.repositoryId)) ||
      !provenance.base?.repository ||
      !provenance.base?.ref ||
      !/^[0-9a-f]{40}$/.test(String(provenance.base?.sha || '')) ||
      !Array.isArray(provenance.targets)) {
    throw new Error('The App-check generation provenance is incomplete.');
  }
  for (const target of provenance.targets) {
    validatePullIdentity(target);
  }
  return provenance;
}

function externalIdForProvenance(provenance) {
  validateGenerationProvenance(provenance);
  return `${CHECK_EXTERNAL_PREFIX}:${securityDigest(provenance)}`;
}

function generationBase(mappings, baseOverride, envelope) {
  if (baseOverride) {
    return {
      repositoryId: Number(baseOverride.repositoryId),
      repository: baseOverride.repository,
      ref: baseOverride.ref,
      sha: baseOverride.sha,
    };
  }
  const first = mappings[0];
  if (first) {
    return {
      repositoryId: Number(first.baseRepositoryId),
      repository: first.baseRepository,
      ref: first.baseRef,
      sha: first.baseSha,
    };
  }
  const payloadBase = envelope.payload?.merge_group || {};
  return {
    repositoryId: Number(envelope.repository.id),
    repository: envelope.repository.fullName,
    ref: String(payloadBase.base_ref || envelope.repository.defaultBranch)
      .replace(/^refs\/heads\//, ''),
    sha: String(payloadBase.base_sha || envelope.payload?.after || ''),
  };
}

async function createGeneration({
  client,
  store,
  envelope,
  headSha,
  publisherAppId,
  mappings = [],
  association,
  baseOverride = null,
}) {
  assertExternalEnvelope(envelope);
  const reserve = requireStoreMethod(store, 'reserveGeneration');
  const reservation = await reserve({
    repositoryId: Number(envelope.repository.id),
    headSha,
    eventPath: envelope.delivery.eventPath,
    deliveryId: envelope.delivery.id,
  });
  if (!reservation?.generationId ||
      !Number.isInteger(Number(reservation.sequence)) ||
      Number(reservation.sequence) <= 0) {
    throw new Error('The durable store did not reserve a monotonic generation.');
  }
  const provenance = validateGenerationProvenance({
    schemaVersion: 4,
    contractRevision: CONTRACT_REVISION,
    eventPath: envelope.delivery.eventPath,
    workerRevision: envelope.worker.revision,
    workerInstanceId: envelope.worker.instanceId,
    deliveryId: envelope.delivery.id,
    runId: envelope.delivery.runId,
    association,
    targets: normalizedTargets(mappings, baseOverride),
    headSha,
    base: generationBase(mappings, baseOverride, envelope),
    sequence: Number(reservation.sequence),
    generationId: reservation.generationId,
  });
  const externalId = externalIdForProvenance(provenance);
  const response = await client.rest.checks.create({
    ...repoParameters(envelope),
    name: CHECK_NAME,
    head_sha: headSha,
    status: 'in_progress',
    started_at: new Date().toISOString(),
    external_id: externalId,
    output: {
      title: 'Pending — external policy evaluation started',
      summary: [
        'The independently hosted review-gate App published a pending generation.',
        `Event path: ${provenance.eventPath}.`,
        `Generation: ${provenance.sequence}.`,
        `Provenance digest: ${externalId.slice(CHECK_EXTERNAL_PREFIX.length + 1)}.`,
      ].join('\n'),
    },
  });
  const checkRun = response.data;
  if (Number(checkRun.app?.id) !== Number(publisherAppId)) {
    throw new Error('The check publisher does not match the provisioned App ID.');
  }
  await requireStoreMethod(store, 'activateGeneration')({
    generationId: reservation.generationId,
    checkRunId: Number(checkRun.id),
    externalId,
    provenance,
  });
  return {
    ...checkRun,
    generationId: reservation.generationId,
    sequence: Number(reservation.sequence),
    provenance,
  };
}

async function listAppGenerations({
  client,
  envelope,
  headSha,
  publisherAppId,
}) {
  const runs = await client.paginate(client.rest.checks.listForRef, {
    ...repoParameters(envelope),
    ref: headSha,
    check_name: CHECK_NAME,
    per_page: 100,
  });
  return runs.filter((run) =>
    run.name === CHECK_NAME &&
    Number(run.app?.id) === Number(publisherAppId) &&
    String(run.external_id || '').startsWith(`${CHECK_EXTERNAL_PREFIX}:`))
    .sort((left, right) => Number(left.id) - Number(right.id));
}

async function completeGeneration({
  client,
  store,
  envelope,
  checkRun,
  publisherAppId,
  result,
}) {
  const newest = await requireStoreMethod(store, 'getNewestGeneration')({
    repositoryId: Number(envelope.repository.id),
    headSha: checkRun.head_sha,
  });
  if (newest?.generationId !== checkRun.generationId) {
    return false;
  }
  const before = await listAppGenerations({
    client,
    envelope,
    headSha: checkRun.head_sha,
    publisherAppId,
  });
  if (Number(before.at(-1)?.id) !== Number(checkRun.id) ||
      before.at(-1)?.external_id !== externalIdForProvenance(checkRun.provenance)) {
    return false;
  }
  const conclusion = result.state === 'approved' ||
    result.state === 'not_applicable'
    ? 'success'
    : 'failure';
  await client.rest.checks.update({
    ...repoParameters(envelope),
    check_run_id: checkRun.id,
    status: 'completed',
    conclusion,
    completed_at: new Date().toISOString(),
    output: {
      title: result.state === 'approved'
        ? 'Approved — exact policy complete'
        : result.state === 'not_applicable'
          ? 'Not applicable — base path reconciled'
          : result.state === 'pending'
            ? 'Pending — review policy incomplete'
            : 'Rejected — review policy incomplete',
      summary: summarizeResult(result),
    },
  });
  const afterNewest = await requireStoreMethod(store, 'getNewestGeneration')({
    repositoryId: Number(envelope.repository.id),
    headSha: checkRun.head_sha,
  });
  const after = await listAppGenerations({
    client,
    envelope,
    headSha: checkRun.head_sha,
    publisherAppId,
  });
  return afterNewest?.generationId === checkRun.generationId &&
    Number(after.at(-1)?.id) === Number(checkRun.id);
}

function groupMappingsByHead(mappings) {
  const groups = new Map();
  for (const mapping of mappings) {
    const identity = mappingIdentity(mapping);
    const values = groups.get(identity.headSha) || [];
    values.push(mapping);
    groups.set(identity.headSha, values);
  }
  return [...groups.entries()]
    .sort(([left], [right]) => left.localeCompare(right))
    .map(([headSha, values]) => ({
      headSha,
      mappings: values.sort((left, right) => left.pullNumber - right.pullNumber),
    }));
}

async function startPendingBatch({
  client,
  store,
  envelope,
  publisherAppId,
  mappings,
  associationKind,
  baseOverride = null,
}) {
  const generations = [];
  for (const group of groupMappingsByHead(mappings)) {
    generations.push({
      ...group,
      generation: await createGeneration({
        client,
        store,
        envelope,
        headSha: group.headSha,
        publisherAppId,
        mappings: group.mappings,
        association: {
          kind: associationKind,
          pullNumbers: group.mappings.map((mapping) => mapping.pullNumber),
        },
        baseOverride,
      }),
    });
  }
  return generations;
}

async function evaluatePendingBatch({
  client,
  store,
  envelope,
  publisherAppId,
  generations,
  evaluatorOptions = {},
  requiredBase = null,
}) {
  const results = [];
  for (const item of generations) {
    let aggregate;
    try {
      aggregate = await evaluateHeadUntilStable({
        client,
        store,
        envelope,
        headSha: item.headSha,
        publisherAppId,
        requiredBase,
        ...evaluatorOptions,
      });
    } catch (error) {
      aggregate = {
        result: {
          state: 'rejected',
          reasons: [sanitizedApiFailure(error)],
        },
      };
    }
    const completed = await completeGeneration({
      client,
      store,
      envelope,
      checkRun: item.generation,
      publisherAppId,
      result: aggregate.result,
    });
    results.push({
      ...aggregate,
      superseded: !completed,
      generation: item.generation,
    });
  }
  return results;
}

async function completePendingBatch({
  client,
  store,
  envelope,
  publisherAppId,
  generations,
  result,
}) {
  const completed = [];
  for (const item of generations) {
    const current = await completeGeneration({
      client,
      store,
      envelope,
      checkRun: item.generation,
      publisherAppId,
      result,
    });
    completed.push({
      result,
      generation: item.generation,
      superseded: !current,
    });
  }
  return completed;
}

async function publishAndEvaluateMappings({
  client,
  store,
  envelope,
  publisherAppId,
  mappings,
  associationKind,
  evaluatorOptions = {},
  beforeEvaluate = null,
  baseOverride = null,
  requiredBase = null,
}) {
  const generations = await startPendingBatch({
    client,
    store,
    envelope,
    publisherAppId,
    mappings,
    associationKind,
    baseOverride,
  });
  if (beforeEvaluate) {
    try {
      await beforeEvaluate();
    } catch (error) {
      return completePendingBatch({
        client,
        store,
        envelope,
        publisherAppId,
        generations,
        result: {
          state: 'rejected',
          reasons: [sanitizedApiFailure(error)],
        },
      });
    }
  }
  return evaluatePendingBatch({
    client,
    store,
    envelope,
    publisherAppId,
    generations,
    evaluatorOptions,
    requiredBase,
  });
}

async function mappingsForPull(store, pullNumber) {
  const mapping = await requireStoreMethod(store, 'getPullMapping')(pullNumber);
  return mapping ? [mapping] : [];
}

async function handlePullRequestEvent(options) {
  const { envelope, store } = options;
  assertExternalEnvelope(envelope);
  const pullRequest = envelope.payload?.pull_request;
  const mapping = basicPullMapping(envelope, pullRequest);
  await persistPullMapping(store, mapping);
  const result = await publishAndEvaluateMappings({
    ...options,
    mappings: [mapping],
    associationKind: 'pull_request',
  });
  if (pullRequest.state !== 'open' || envelope.payload.action === 'closed') {
    await requireStoreMethod(store, 'closePullMapping')(pullRequest.number);
  }
  return result;
}

async function handleReviewEvent(options) {
  const { envelope, store, client, publisherAppId } = options;
  assertExternalEnvelope(envelope);
  const pullRequest = envelope.payload?.pull_request;
  const mapping = basicPullMapping(envelope, pullRequest);
  await persistPullMapping(store, mapping);
  return publishAndEvaluateMappings({
    ...options,
    mappings: [mapping],
    associationKind: 'pull_request_review',
    beforeEvaluate: () => recordCopilotAttestationFromEvent({
      client,
      envelope,
      publisherAppId,
    }),
  });
}

async function commentMappings(store, envelope) {
  if (envelope.payload?.pull_request) {
    return [basicPullMapping(envelope, envelope.payload.pull_request)];
  }
  const pullNumber = Number(
    envelope.payload?.issue?.pull_request
      ? envelope.payload.issue.number
      : envelope.payload?.pull_number || 0);
  return pullNumber > 0 ? mappingsForPull(store, pullNumber) : [];
}

async function handleCommentEvent(options) {
  const { envelope, store, client, publisherAppId } = options;
  assertExternalEnvelope(envelope);
  const mappings = await commentMappings(store, envelope);
  if (mappings.length === 0) {
    throw new Error('No durable PR mapping matched the comment event.');
  }
  return publishAndEvaluateMappings({
    ...options,
    mappings,
    associationKind: envelope.delivery.eventPath,
    beforeEvaluate: () => restoreDeletedPolicyEventFromEvent({
      client,
      envelope,
      publisherAppId,
    }),
  });
}

async function handleIssueEvent(options) {
  const { envelope, store } = options;
  assertExternalEnvelope(envelope);
  const issueNumber = Number(envelope.payload?.issue?.number);
  const mappings = await requireStoreMethod(
    store,
    'listPullMappingsByIssue')(issueNumber);
  if (mappings.length === 0) {
    throw new Error('No durable issue-to-PR policy mapping matched this event.');
  }
  return publishAndEvaluateMappings({
    ...options,
    mappings,
    associationKind: 'issue',
  });
}

function membershipLogin(envelope) {
  return String(
    envelope.payload?.member?.login ||
    envelope.payload?.membership?.user?.login ||
    envelope.payload?.user?.login ||
    '').trim();
}

async function handleMembershipEvent(options) {
  const { envelope, store } = options;
  assertExternalEnvelope(envelope);
  const login = membershipLogin(envelope);
  let mappings = login
    ? await requireStoreMethod(store, 'listPullMappingsByReviewer')(login)
    : [];
  if (mappings.length === 0) {
    mappings = await requireStoreMethod(store, 'listOpenPullMappings')();
  }
  if (mappings.length === 0) {
    return [];
  }
  return publishAndEvaluateMappings({
    ...options,
    mappings,
    associationKind: 'membership',
  });
}

function baseFromPush(envelope) {
  const ref = String(envelope.payload?.ref || '');
  const baseRef = ref.replace(/^refs\/heads\//, '');
  const sha = String(envelope.payload?.after || '');
  if (!baseRef || !/^[0-9a-f]{40}$/.test(sha)) {
    throw new Error('A base push requires an exact branch ref and after SHA.');
  }
  return {
    repositoryId: Number(envelope.repository.id),
    repository: envelope.repository.fullName,
    ref: baseRef,
    sha,
  };
}

function notApplicableBaseResult(base, reevaluatedCount) {
  return {
    state: 'not_applicable',
    reasons: [
      `Default/base path ${base.repository}:${base.ref}@${base.sha} was reconciled.`,
      `${reevaluatedCount} affected PR head generation(s) were published pending before reevaluation.`,
      'This base-push result is not merge-group evidence.',
    ],
  };
}

async function handleBasePushEvent(options) {
  const {
    client,
    store,
    envelope,
    publisherAppId,
    evaluatorOptions = {},
  } = options;
  assertExternalEnvelope(envelope);
  const base = baseFromPush(envelope);
  const mappings = await requireStoreMethod(
    store,
    'listPullMappingsByBase')({
    repositoryId: base.repositoryId,
    ref: base.ref,
  });
  const pending = await startPendingBatch({
    client,
    store,
    envelope,
    publisherAppId,
    mappings,
    associationKind: 'base_push',
    baseOverride: base,
  });
  const baseGeneration = await createGeneration({
    client,
    store,
    envelope,
    headSha: base.sha,
    publisherAppId,
    mappings: [],
    association: { kind: 'default_branch', ref: base.ref },
    baseOverride: base,
  });
  const results = await evaluatePendingBatch({
    client,
    store,
    envelope,
    publisherAppId,
    generations: pending,
    evaluatorOptions,
    requiredBase: base,
  });
  await completeGeneration({
    client,
    store,
    envelope,
    checkRun: baseGeneration,
    publisherAppId,
    result: notApplicableBaseResult(base, pending.length),
  });
  return { baseGeneration, results };
}

function mergeGroupIdentity(envelope) {
  const group = envelope.payload?.merge_group || {};
  const headSha = String(group.head_sha || '');
  const baseSha = String(group.base_sha || '');
  const baseRef = String(group.base_ref || '').replace(/^refs\/heads\//, '');
  const id = String(group.id || group.head_ref || '');
  if (!/^[0-9a-f]{40}$/.test(headSha) ||
      !/^[0-9a-f]{40}$/.test(baseSha) ||
      !baseRef ||
      !id) {
    throw new Error('The merge-group event lacks exact group/head/base identity.');
  }
  return {
    id,
    headSha,
    base: {
      repositoryId: Number(envelope.repository.id),
      repository: envelope.repository.fullName,
      ref: baseRef,
      sha: baseSha,
    },
  };
}

function aggregateMergeGroup(snapshots, group) {
  if (snapshots.length === 0) {
    return {
      result: {
        state: 'rejected',
        reasons: ['The merge group has no durably mapped constituent PRs.'],
      },
      snapshots,
      fingerprint: securityDigest({ group, snapshots: [] }),
    };
  }
  const rejected = snapshots.filter((snapshot) =>
    snapshot.stale || snapshot.result.state === 'rejected');
  const pending = snapshots.filter((snapshot) =>
    !snapshot.stale && snapshot.result.state === 'pending');
  const result = rejected.length > 0
    ? {
        state: 'rejected',
        reasons: rejected.flatMap((snapshot) =>
          snapshot.result.reasons.map((reason) =>
            `Merge-group PR #${snapshot.pullNumber}: ${reason}`)),
      }
    : pending.length > 0
      ? {
          state: 'pending',
          reasons: pending.flatMap((snapshot) =>
            snapshot.result.reasons.map((reason) =>
              `Merge-group PR #${snapshot.pullNumber}: ${reason}`)),
        }
      : {
          state: 'approved',
          reasons: [
            `Every constituent PR was revalidated for merge group ${group.id}.`,
            `Current base: ${group.base.repository}:${group.base.ref}@${group.base.sha}.`,
          ],
        };
  return {
    result,
    snapshots,
    fingerprint: securityDigest({
      group,
      snapshots: snapshots.map((snapshot) => ({
        pullNumber: snapshot.pullNumber,
        fingerprint: snapshot.fingerprint,
        result: snapshot.result,
      })),
    }),
  };
}

async function evaluateMergeGroupSnapshot({
  client,
  store,
  envelope,
  publisherAppId,
  group,
  mappings,
}) {
  const liveBase = await client.rest.repos.getBranch({
    ...repoParameters(envelope),
    branch: group.base.ref,
  });
  if (liveBase.data?.commit?.sha !== group.base.sha) {
    return aggregateMergeGroup([{
      stale: true,
      pullNumber: 0,
      result: {
        state: 'rejected',
        reasons: ['The merge-group base is not the current base branch tip.'],
      },
      fingerprint: null,
    }], group);
  }
  const resolved = await client.resolveMergeGroupConstituents({
    repository: envelope.repository.fullName,
    mergeGroupId: group.id,
    headSha: group.headSha,
  });
  const durableNumbers = mappings.map((mapping) => mapping.pullNumber).sort(
    (left, right) => left - right);
  const resolvedNumbers = [...resolved].map(Number).sort(
    (left, right) => left - right);
  if (JSON.stringify(durableNumbers) !== JSON.stringify(resolvedNumbers)) {
    return aggregateMergeGroup([{
      stale: true,
      pullNumber: 0,
      result: {
        state: 'rejected',
        reasons: ['Durable and live merge-group constituent mappings disagree.'],
      },
      fingerprint: null,
    }], group);
  }
  const snapshots = [];
  for (const mapping of mappings) {
    snapshots.push(await evaluatePullSnapshot({
      client,
      store,
      envelope,
      pullNumber: mapping.pullNumber,
      expectedHeadSha: mapping.headSha,
      publisherAppId,
      requiredBase: group.base,
    }));
  }
  return aggregateMergeGroup(snapshots, group);
}

async function evaluateMergeGroupUntilStable({
  client,
  store,
  envelope,
  publisherAppId,
  group,
  mappings,
  stabilitySeconds = DEFAULT_STABILITY_SECONDS,
  sleepFunction = sleep,
}) {
  const first = await evaluateMergeGroupSnapshot({
    client,
    store,
    envelope,
    publisherAppId,
    group,
    mappings,
  });
  if (first.result.state !== 'approved') {
    return first;
  }
  await sleepFunction(stabilitySeconds * 1000);
  const second = await evaluateMergeGroupSnapshot({
    client,
    store,
    envelope,
    publisherAppId,
    group,
    mappings,
  });
  if (second.result.state === 'approved' &&
      second.fingerprint === first.fingerprint) {
    return second;
  }
  return {
    ...second,
    result: second.result.state === 'approved'
      ? {
          state: 'rejected',
          reasons: ['The second merge-group snapshot changed during stabilization.'],
        }
      : second.result,
  };
}

async function handleMergeGroupEvent(options) {
  const {
    client,
    store,
    envelope,
    publisherAppId,
    evaluatorOptions = {},
  } = options;
  assertExternalEnvelope(envelope);
  const group = mergeGroupIdentity(envelope);
  const mappings = await requireStoreMethod(
    store,
    'resolveMergeGroupConstituents')(group);
  const generation = await createGeneration({
    client,
    store,
    envelope,
    headSha: group.headSha,
    publisherAppId,
    mappings,
    association: {
      kind: 'merge_group',
      mergeGroupId: group.id,
      pullNumbers: mappings.map((mapping) => mapping.pullNumber).sort(
        (left, right) => left - right),
    },
    baseOverride: group.base,
  });
  let aggregate;
  try {
    aggregate = await evaluateMergeGroupUntilStable({
      client,
      store,
      envelope,
      publisherAppId,
      group,
      mappings,
      ...evaluatorOptions,
    });
  } catch (error) {
    aggregate = {
      result: {
        state: 'rejected',
        reasons: [sanitizedApiFailure(error)],
      },
    };
  }
  const completed = await completeGeneration({
    client,
    store,
    envelope,
    checkRun: generation,
    publisherAppId,
    result: aggregate.result,
  });
  return { ...aggregate, generation, superseded: !completed };
}

async function handleReconciliation(options) {
  const { client, store, envelope, publisherAppId } = options;
  assertExternalEnvelope(envelope);
  const known = await requireStoreMethod(store, 'listOpenPullMappings')();
  const knownPending = await startPendingBatch({
    client,
    store,
    envelope,
    publisherAppId,
    mappings: known,
    associationKind: 'reconciliation',
  });

  let livePulls;
  try {
    livePulls = await client.paginate(client.rest.pulls.list, {
      ...repoParameters(envelope),
      state: 'open',
      per_page: 100,
    });
  } catch (error) {
    return completePendingBatch({
      client,
      store,
      envelope,
      publisherAppId,
      generations: knownPending,
      result: {
        state: 'rejected',
        reasons: [sanitizedApiFailure(error)],
      },
    });
  }
  const knownNumbers = new Set(known.map((mapping) => mapping.pullNumber));
  const discovered = livePulls
    .filter((pullRequest) => !knownNumbers.has(pullRequest.number))
    .map((pullRequest) => basicPullMapping(envelope, pullRequest));
  for (const mapping of discovered) {
    await persistPullMapping(store, mapping);
  }
  const discoveredPending = await startPendingBatch({
    client,
    store,
    envelope,
    publisherAppId,
    mappings: discovered,
    associationKind: 'reconciliation-discovery',
  });
  return evaluatePendingBatch({
    ...options,
    generations: [...knownPending, ...discoveredPending],
  });
}

async function handleSpecialistAttestation(options) {
  const {
    client,
    store,
    envelope,
    publisherAppId,
    evaluatorOptions = {},
    specialistAllowlist = [],
  } = options;
  assertExternalEnvelope(envelope);
  const pullNumber = Number(envelope.payload?.pull_number);
  const mapping = await store.getPullMapping(pullNumber);
  if (!mapping) {
    throw new Error('Specialist evidence requires a durable open-PR mapping.');
  }
  const generations = await startPendingBatch({
    client,
    store,
    envelope,
    publisherAppId,
    mappings: [mapping],
    associationKind: 'specialist_attestation',
  });
  try {
    const response = await client.rest.pulls.get({
      ...repoParameters(envelope),
      pull_number: pullNumber,
    });
    await validateAndRecordSpecialistArtifact({
      client,
      envelope,
      pullRequest: response.data,
      publisherAppId,
      allowlist: specialistAllowlist,
    });
  } catch {
    const result = {
      state: 'rejected',
      reasons: ['The downloaded specialist evidence failed closed validation.'],
    };
    for (const item of generations) {
      await completeGeneration({
        client,
        store,
        envelope,
        checkRun: item.generation,
        publisherAppId,
        result,
      });
    }
    return generations.map((item) => ({
      result,
      generation: item.generation,
      superseded: false,
    }));
  }
  return evaluatePendingBatch({
    client,
    store,
    envelope,
    publisherAppId,
    generations,
    evaluatorOptions,
  });
}

async function handleTrustedDispatch(options) {
  const { client, store, envelope } = options;
  assertExternalEnvelope(envelope);
  const actor = String(envelope.actor || '');
  if (!actor || actor.endsWith('[bot]')) {
    throw new Error('Trusted dispatch requires an authenticated human actor.');
  }
  const permission = await client.rest.repos.getCollaboratorPermissionLevel({
    ...repoParameters(envelope),
    username: actor,
  });
  if (!ADMIN_PERMISSIONS.has(permission.data.permission)) {
    throw new Error('Trusted dispatch requires maintain or admin permission.');
  }
  const numbers = [...new Set((envelope.payload?.pull_numbers || [])
    .map(Number)
    .filter((number) => Number.isInteger(number) && number > 0))];
  const mappings = [];
  for (const number of numbers) {
    mappings.push(...await mappingsForPull(store, number));
  }
  if (mappings.length === 0) {
    throw new Error('Trusted dispatch requires a durably mapped open PR.');
  }
  return publishAndEvaluateMappings({
    ...options,
    mappings,
    associationKind: 'trusted_dispatch',
  });
}

async function handleEvent(options) {
  const { envelope, publisherAppId, store } = options;
  assertExternalEnvelope(envelope);
  if (!Number.isInteger(Number(publisherAppId)) ||
      Number(publisherAppId) <= 0) {
    throw new Error('The exact provisioned checks-writer App ID is required.');
  }
  const handlers = {
    pull_request: handlePullRequestEvent,
    pull_request_review: handleReviewEvent,
    pull_request_review_comment: handleCommentEvent,
    pull_request_review_thread: handleCommentEvent,
    issue_comment: handleCommentEvent,
    issues: handleIssueEvent,
    push: handleBasePushEvent,
    member: handleMembershipEvent,
    membership: handleMembershipEvent,
    organization: handleMembershipEvent,
    reconciliation: handleReconciliation,
    merge_group: handleMergeGroupEvent,
    specialist_attestation: handleSpecialistAttestation,
    trusted_dispatch: handleTrustedDispatch,
  };
  const handler = handlers[envelope.delivery.eventPath];
  if (!handler) {
    throw new Error('The event path has no fail-closed external-worker handler.');
  }
  const deliveryKey = {
    repositoryId: Number(envelope.repository.id),
    deliveryId: envelope.delivery.id,
    eventPath: envelope.delivery.eventPath,
    workerRevision: envelope.worker.revision,
  };
  const claimed = await requireStoreMethod(store, 'claimDelivery')(deliveryKey);
  if (!claimed) {
    return {
      duplicate: true,
      deliveryId: envelope.delivery.id,
      reason: 'Authenticated delivery was already claimed; no second writer ran.',
    };
  }
  try {
    const result = await handler(options);
    await requireStoreMethod(store, 'completeDelivery')(deliveryKey);
    return result;
  } catch (error) {
    await requireStoreMethod(store, 'failDelivery')({
      ...deliveryKey,
      reason: sanitizedApiFailure(error),
    });
    throw error;
  }
}

module.exports = {
  ADMIN_PERMISSIONS,
  AUTHORIZED_REVIEW_PERMISSIONS,
  DEFAULT_MAX_WAIT_SECONDS,
  DEFAULT_POLL_SECONDS,
  DEFAULT_STABILITY_SECONDS,
  EVENT_PATHS,
  aggregateMergeGroup,
  aggregateSnapshots,
  appendPolicyEvent,
  assertExternalEnvelope,
  basicPullMapping,
  completeGeneration,
  createGeneration,
  domainReviewEvidence,
  evaluateHeadSnapshot,
  evaluateHeadUntilStable,
  evaluateMergeGroupSnapshot,
  evaluateMergeGroupUntilStable,
  evaluatePendingBatch,
  evaluatePullSnapshot,
  eventDeliveryId,
  externalIdForProvenance,
  handleBasePushEvent,
  handleCommentEvent,
  handleEvent,
  handleIssueEvent,
  handleMembershipEvent,
  handleMergeGroupEvent,
  handlePullRequestEvent,
  handleReconciliation,
  handleReviewEvent,
  handleSpecialistAttestation,
  handleTrustedDispatch,
  listAppGenerations,
  listPolicyComments,
  listReviewThreads,
  loadPolicyLedger,
  mappingIdentity,
  newestDomainCandidate,
  notApplicableBaseResult,
  observeCurrentRequirements,
  openPullsSharingHead,
  persistPullMapping,
  publishAndEvaluateMappings,
  recordCopilotAttestationFromEvent,
  restoreDeletedPolicyEventFromEvent,
  sanitizedApiFailure,
  startPendingBatch,
  validateAndRecordSpecialistArtifact,
  validateGenerationProvenance,
};
