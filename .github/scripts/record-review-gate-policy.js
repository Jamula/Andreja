'use strict';

const {
  makeObservationEpoch,
  makePolicyEvent,
  pullIdentity,
  repositoryUrl,
  samePullIdentity,
  validatePullIdentity,
} = require('./review-gate-policy');
const {
  ADMIN_PERMISSIONS,
  appendPolicyEvent,
  assertExternalEnvelope,
  completeGeneration,
  evaluatePendingBatch,
  loadPolicyLedger,
  startPendingBatch,
} = require('./review-gate-app');

const OPERATIONS = new Set(['bind-issue', 'reduce-policy', 'break-glass']);

function inputIdentity(inputs) {
  return validatePullIdentity({
    pullNumber: Number(inputs.pull_number),
    headSha: String(inputs.head_sha || '').trim(),
    baseRepositoryId: Number(inputs.base_repository_id),
    baseRepository: String(inputs.base_repository || '').trim(),
    baseRef: String(inputs.base_ref || '').trim(),
    baseSha: String(inputs.base_sha || '').trim(),
  });
}

function boundedReason(value) {
  const reason = String(value || '').trim();
  if (reason.length < 20 || reason.length > 2000) {
    throw new Error('A specific 20-2000 character audit reason is required.');
  }
  return reason;
}

async function requireAdminActor({ client, envelope }) {
  const actor = String(envelope.actor || '');
  if (!actor || actor.endsWith('[bot]')) {
    throw new Error('Policy administration requires an authenticated human actor.');
  }
  const response = await client.rest.repos.getCollaboratorPermissionLevel({
    owner: envelope.repository.owner,
    repo: envelope.repository.name,
    username: actor,
  });
  if (!ADMIN_PERMISSIONS.has(response.data.permission)) {
    throw new Error('Policy administration requires maintain or admin permission.');
  }
  return actor;
}

async function currentState({
  client,
  envelope,
  publisherAppId,
  identity,
}) {
  const response = await client.rest.pulls.get({
    owner: envelope.repository.owner,
    repo: envelope.repository.name,
    pull_number: identity.pullNumber,
  });
  const pullRequest = response.data;
  if (pullRequest.state !== 'open' ||
      !samePullIdentity(pullIdentity(pullRequest), identity)) {
    throw new Error('The supplied PR/head/base identity is stale or incorrect.');
  }
  const ledger = await loadPolicyLedger({
    client,
    envelope,
    pullRequest,
    publisherAppId,
  });
  return { pullRequest, ledger };
}

function baseEventFields({ envelope, identity, actor, operation, data }) {
  return {
    kind: operation,
    repositoryId: Number(envelope.repository.id),
    repository: envelope.repository.fullName,
    pullNumber: identity.pullNumber,
    deliveryId: [
      envelope.delivery.id,
      envelope.delivery.runId,
      operation,
    ].join(':').slice(0, 240),
    actor,
    data,
  };
}

async function bindIssue({
  client,
  envelope,
  command,
  identity,
  actor,
  ledger,
}) {
  const issueNumber = Number(command.issue_number);
  if (!Number.isInteger(issueNumber) || issueNumber <= 0) {
    throw new Error('bind-issue requires a positive issue_number.');
  }
  const issue = await client.rest.issues.get({
    owner: envelope.repository.owner,
    repo: envelope.repository.name,
    issue_number: issueNumber,
  });
  if (issue.data.pull_request) {
    throw new Error('The trusted association target must be an issue, not a PR.');
  }
  const sourceKey = `trusted-issue:${issueNumber}`;
  if (ledger.snapshot.activeSources[sourceKey]) {
    throw new Error(`Issue #${issueNumber} is already actively bound.`);
  }
  const deliveryId = [
    envelope.delivery.id,
    envelope.delivery.runId,
    'bind-issue',
    sourceKey,
  ].join(':').slice(0, 240);
  return makePolicyEvent({
    kind: 'bind-issue',
    repositoryId: Number(envelope.repository.id),
    repository: envelope.repository.fullName,
    pullNumber: identity.pullNumber,
    deliveryId,
    actor,
    data: {
      sourceKey,
      issueNumber,
      identity,
      observationEpoch: makeObservationEpoch(identity, {
        deliveryId,
        eventPath: envelope.delivery.eventPath,
        workerRevision: envelope.worker.revision,
      }),
      reason: boundedReason(command.reason),
      auditUrl: repositoryUrl(
        command.audit_url,
        envelope.repository.fullName),
    },
  });
}

function reductionTargets(value, ledger, identity) {
  let eventIds;
  try {
    eventIds = JSON.parse(String(value || '[]'));
  } catch {
    throw new Error('target_event_ids must be a JSON array.');
  }
  if (!Array.isArray(eventIds) ||
      eventIds.length === 0 ||
      eventIds.some((eventId) => !/^[0-9a-f]{64}$/.test(String(eventId)))) {
    throw new Error('target_event_ids must contain one or more event IDs.');
  }
  const unique = [...new Set(eventIds.map(String))].sort();
  const targets = unique.map((eventId) => {
    const source = Object.values(ledger.snapshot.latestSources)
      .find((candidate) => candidate.eventId === eventId);
    if (!source ||
        source.reduced ||
        !samePullIdentity(source.observationEpoch?.identity, identity)) {
      throw new Error(
        'A reduction target is stale or belongs to another observation epoch.');
    }
    return {
      eventId,
      epochId: source.observationEpoch.id,
    };
  });
  return targets;
}

function reducePolicy({ envelope, command, identity, actor, ledger }) {
  return makePolicyEvent(baseEventFields({
    envelope,
    identity,
    actor,
    operation: 'reduce-policy',
    data: {
      identity,
      expectedPolicyDigest: ledger.snapshot.digest,
      targets: reductionTargets(
        command.target_event_ids,
        ledger,
        identity),
      reason: boundedReason(command.reason),
      auditUrl: repositoryUrl(
        command.audit_url,
        envelope.repository.fullName),
    },
  }));
}

function breakGlass({ envelope, command, identity, actor, ledger }) {
  return makePolicyEvent(baseEventFields({
    envelope,
    identity,
    actor,
    operation: 'break-glass',
    data: {
      identity,
      policyDigest: ledger.snapshot.digest,
      reason: boundedReason(command.reason),
      auditUrl: repositoryUrl(
        command.audit_url,
        envelope.repository.fullName),
    },
  }));
}

async function recordDecision({
  client,
  envelope,
  publisherAppId,
  command,
}) {
  const operation = String(command.operation || '');
  if (!OPERATIONS.has(operation)) {
    throw new Error('operation must be bind-issue, reduce-policy, or break-glass.');
  }
  const identity = inputIdentity(command);
  const actor = await requireAdminActor({ client, envelope });
  const { ledger } = await currentState({
    client,
    envelope,
    publisherAppId,
    identity,
  });
  if (command.expected_policy_digest !== ledger.snapshot.digest) {
    throw new Error(
      'The authenticated historical policy digest changed; inspect and retry.');
  }
  if (!repositoryUrl(command.audit_url, envelope.repository.fullName)) {
    throw new Error('audit_url must be a durable URL in this repository.');
  }
  const event = operation === 'bind-issue'
    ? await bindIssue({
        client,
        envelope,
        command,
        identity,
        actor,
        ledger,
      })
    : operation === 'reduce-policy'
      ? reducePolicy({ envelope, command, identity, actor, ledger })
      : breakGlass({ envelope, command, identity, actor, ledger });
  await appendPolicyEvent({
    client,
    envelope,
    event,
    publisherAppId,
  });
  return event;
}

async function handleAdminDecision({
  client,
  store,
  envelope,
  publisherAppId,
  command,
  evaluatorOptions = {},
}) {
  assertExternalEnvelope(envelope);
  if (envelope.delivery.eventPath !== 'trusted_dispatch') {
    throw new Error('Policy administration is available only at trusted admin ingress.');
  }
  const identity = inputIdentity(command);
  const mapping = await store.getPullMapping(identity.pullNumber);
  if (!mapping || !samePullIdentity({
    pullNumber: mapping.pullNumber,
    headSha: mapping.headSha,
    baseRepositoryId: mapping.baseRepositoryId,
    baseRepository: mapping.baseRepository,
    baseRef: mapping.baseRef,
    baseSha: mapping.baseSha,
  }, identity)) {
    throw new Error('The durable PR mapping does not match the requested exact diff.');
  }
  const generations = await startPendingBatch({
    client,
    store,
    envelope,
    publisherAppId,
    mappings: [mapping],
    associationKind: 'trusted_admin',
  });
  try {
    await recordDecision({
      client,
      envelope,
      publisherAppId,
      command,
    });
  } catch (error) {
    for (const item of generations) {
      await completeGeneration({
        client,
        store,
        envelope,
        checkRun: item.generation,
        publisherAppId,
        result: {
          state: 'rejected',
          reasons: ['The authenticated policy decision was rejected.'],
        },
      });
    }
    throw error;
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

module.exports = {
  OPERATIONS,
  bindIssue,
  boundedReason,
  breakGlass,
  currentState,
  handleAdminDecision,
  inputIdentity,
  recordDecision,
  reducePolicy,
  reductionTargets,
  requireAdminActor,
};
