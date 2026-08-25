'use strict';

const {
  makePolicyEvent,
  pullIdentity,
  repositoryUrl,
  samePullIdentity,
} = require('./review-gate-policy');
const {
  appendPolicyEvent,
  assertTrustedDispatchRef,
  loadPolicyLedger,
  run,
} = require('./review-gate-app');

const ADMIN_PERMISSIONS = new Set(['maintain', 'admin']);
const OPERATIONS = new Set(['bind-issue', 'reduce-policy', 'break-glass']);

function inputIdentity(inputs) {
  const identity = {
    pullNumber: Number(inputs.pr_number),
    headSha: String(inputs.head_sha || '').trim(),
    baseRepositoryId: Number(inputs.base_repository_id),
    baseRepository: String(inputs.base_repository || '').trim(),
    baseRef: String(inputs.base_ref || '').trim(),
    baseSha: String(inputs.base_sha || '').trim(),
  };
  if (!Number.isInteger(identity.pullNumber) || identity.pullNumber <= 0 ||
      !/^[0-9a-f]{40}$/.test(identity.headSha) ||
      !Number.isInteger(identity.baseRepositoryId) ||
      identity.baseRepositoryId <= 0 ||
      !identity.baseRepository ||
      !identity.baseRef ||
      !/^[0-9a-f]{40}$/.test(identity.baseSha)) {
    throw new Error('A complete exact PR/head/base identity is required.');
  }
  return identity;
}

function boundedReason(value) {
  const reason = String(value || '').trim();
  if (reason.length < 20 || reason.length > 2000) {
    throw new Error('A specific 20-2000 character audit reason is required.');
  }
  return reason;
}

async function requireAdminActor({ github, context }) {
  const actor = String(context.actor || '');
  if (!actor || actor.endsWith('[bot]')) {
    throw new Error('Policy administration requires an authenticated human actor.');
  }
  const response = await github.rest.repos.getCollaboratorPermissionLevel({
    ...context.repo,
    username: actor,
  });
  if (!ADMIN_PERMISSIONS.has(response.data.permission)) {
    throw new Error('Policy administration requires maintain or admin permission.');
  }
  return actor;
}

async function currentState({
  github,
  context,
  expectedAppId,
  identity,
}) {
  const response = await github.rest.pulls.get({
    ...context.repo,
    pull_number: identity.pullNumber,
  });
  const pullRequest = response.data;
  if (pullRequest.state !== 'open' ||
      !samePullIdentity(pullIdentity(pullRequest), identity)) {
    throw new Error('The supplied PR/head/base identity is stale or incorrect.');
  }
  const ledger = await loadPolicyLedger({
    github,
    context,
    pullRequest,
    expectedAppId,
  });
  return { pullRequest, ledger };
}

function baseEventFields({ context, identity, actor, operation, data }) {
  return {
    kind: operation,
    repositoryId: Number(context.payload.repository.id),
    repository: context.payload.repository.full_name,
    pullNumber: identity.pullNumber,
    deliveryId: [
      context.runId || 'no-run',
      context.runAttempt || 1,
      operation,
    ].join(':'),
    actor,
    data,
  };
}

async function bindIssue({
  github,
  context,
  inputs,
  identity,
  actor,
  ledger,
}) {
  const issueNumber = Number(inputs.issue_number);
  if (!Number.isInteger(issueNumber) || issueNumber <= 0) {
    throw new Error('bind-issue requires a positive issue_number.');
  }
  const issue = await github.rest.issues.get({
    ...context.repo,
    issue_number: issueNumber,
  });
  if (issue.data.pull_request) {
    throw new Error('The trusted association target must be an issue, not a PR.');
  }
  const sourceKey = `trusted-issue:${issueNumber}`;
  if (ledger.snapshot.activeSources[sourceKey]) {
    throw new Error(`Issue #${issueNumber} is already actively bound.`);
  }
  return makePolicyEvent(baseEventFields({
    context,
    identity,
    actor,
    operation: 'bind-issue',
    data: {
      sourceKey,
      issueNumber,
      identity,
      reason: boundedReason(inputs.reason),
      auditUrl: repositoryUrl(
        inputs.audit_url,
        context.payload.repository.full_name),
    },
  }));
}

function reductionTargets(value, ledger) {
  let targets;
  try {
    targets = JSON.parse(String(value || '[]'));
  } catch {
    throw new Error('target_event_ids must be a JSON array.');
  }
  if (!Array.isArray(targets) || targets.length === 0 ||
      targets.some((target) => !/^[0-9a-f]{64}$/.test(String(target)))) {
    throw new Error('target_event_ids must contain one or more event IDs.');
  }
  const unique = [...new Set(targets.map(String))].sort();
  for (const target of unique) {
    if (!ledger.snapshot.observationIds.includes(target)) {
      throw new Error('A policy reduction target is stale or no longer active.');
    }
  }
  return unique;
}

function reducePolicy({ context, inputs, identity, actor, ledger }) {
  return makePolicyEvent(baseEventFields({
    context,
    identity,
    actor,
    operation: 'reduce-policy',
    data: {
      identity,
      expectedPolicyDigest: ledger.snapshot.digest,
      targetEventIds: reductionTargets(inputs.target_event_ids, ledger),
      reason: boundedReason(inputs.reason),
      auditUrl: repositoryUrl(
        inputs.audit_url,
        context.payload.repository.full_name),
    },
  }));
}

function breakGlass({ context, inputs, identity, actor, ledger }) {
  return makePolicyEvent(baseEventFields({
    context,
    identity,
    actor,
    operation: 'break-glass',
    data: {
      identity,
      policyDigest: ledger.snapshot.digest,
      reason: boundedReason(inputs.reason),
      auditUrl: repositoryUrl(
        inputs.audit_url,
        context.payload.repository.full_name),
    },
  }));
}

async function record({
  github,
  context,
  core,
  expectedAppId = Number(process.env.REVIEW_GATE_APP_ID),
  evaluatorOptions = {},
}) {
  assertTrustedDispatchRef(context);
  const inputs = context.payload.inputs || {};
  const operation = String(inputs.operation || '');
  if (!OPERATIONS.has(operation)) {
    throw new Error('operation must be bind-issue, reduce-policy, or break-glass.');
  }
  const identity = inputIdentity(inputs);
  const actor = await requireAdminActor({ github, context });
  const { ledger } = await currentState({
    github,
    context,
    expectedAppId,
    identity,
  });
  if (inputs.expected_policy_digest !== ledger.snapshot.digest) {
    throw new Error(
      'The authenticated policy digest changed; inspect the latest App check and retry.');
  }
  if (!repositoryUrl(inputs.audit_url, context.payload.repository.full_name)) {
    throw new Error('audit_url must be a durable URL in this repository.');
  }

  let event;
  if (operation === 'bind-issue') {
    event = await bindIssue({
      github,
      context,
      inputs,
      identity,
      actor,
      ledger,
    });
  } else if (operation === 'reduce-policy') {
    event = reducePolicy({ context, inputs, identity, actor, ledger });
  } else {
    event = breakGlass({ context, inputs, identity, actor, ledger });
  }
  await appendPolicyEvent({ github, context, event, expectedAppId });
  core.warning(
    `${operation} recorded by the review-gate App for PR #${identity.pullNumber}.`);
  return run({
    github,
    context,
    core,
    expectedAppId,
    evaluatorOptions,
  });
}

module.exports = {
  ADMIN_PERMISSIONS,
  OPERATIONS,
  bindIssue,
  boundedReason,
  breakGlass,
  currentState,
  inputIdentity,
  record,
  reducePolicy,
  reductionTargets,
  requireAdminActor,
};
