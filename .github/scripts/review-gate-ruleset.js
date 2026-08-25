'use strict';

const crypto = require('node:crypto');
const { spawnSync } = require('node:child_process');
const fs = require('node:fs');

const {
  CHECK_NAME,
} = require('./review-gate-policy');
const {
  externalIdForProvenance,
  validateGenerationProvenance,
} = require('./review-gate-app');

const ROLLOUT_BLOCKER_CODE =
  'EXTERNAL_REVIEW_GATE_WORKER_NOT_INDEPENDENTLY_PROVISIONED';
const ROLLOUT_BLOCKER =
  'Ruleset apply is hard-disabled: #104 lacks an independently hosted, ' +
  'non-Actions GitHub App worker and the required live negative canaries.';
const ACTIVATION_PREREQUISITES = Object.freeze([
  'Exact non-Actions GitHub App installation identity dedicated to this repository.',
  'Least-privilege permission attestation for checks write, pull requests read, issues read/write, administration read, metadata read, and contents read.',
  'Independently deployed worker revision and run provenance with durable PR/base/issue/reviewer/generation mappings.',
  'Newest pending generation evidence for every affected PR head before mutable metadata reads.',
  'Startup failure, dropped-delivery recovery, rate-limit, stale-writer, and wrong-App negative canaries.',
  'Reviewer authorization revocation plus periodic full-reconciliation canaries.',
  'A real merge-queue merge_group canary with exact constituent PR and current-base revalidation.',
  'Distinct pull-request and default/base-push canaries; neither may substitute for merge_group evidence.',
  'No merge-ready interval across push, retarget, base advance, policy change, thread reopening, or worker restart.',
]);
const REQUIRED_BASELINE_CHECKS = Object.freeze([
  { context: 'Build and test (Debug)', integration_id: 15368 },
  { context: 'Build and test (Release)', integration_id: 15368 },
  { context: 'Format verification', integration_id: 15368 },
  { context: 'NuGet vulnerability audit', integration_id: 15368 },
  { context: 'C# SAST (DevSkim)', integration_id: 15368 },
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

function digest(value) {
  return crypto.createHash('sha256')
    .update(JSON.stringify(stableValue(value)))
    .digest('hex');
}

function statusRule(ruleset) {
  return ruleset.rules.find((rule) => rule.type === 'required_status_checks');
}

function statusChecks(ruleset) {
  return statusRule(ruleset)?.parameters?.required_status_checks || [];
}

function sameCheck(left, right) {
  return left.context === right.context &&
    Number(left.integration_id || 0) === Number(right.integration_id || 0);
}

function assertSafetyPolicy(ruleset) {
  const pullRequest = ruleset.rules.find((rule) => rule.type === 'pull_request');
  const status = statusRule(ruleset);
  if (ruleset.target !== 'branch' ||
      ruleset.enforcement !== 'active' ||
      JSON.stringify(ruleset.conditions?.ref_name?.include) !==
        JSON.stringify(['~DEFAULT_BRANCH']) ||
      (ruleset.conditions?.ref_name?.exclude || []).length !== 0 ||
      (ruleset.bypass_actors || []).length !== 0 ||
      !pullRequest ||
      pullRequest.parameters.required_approving_review_count !== 0 ||
      pullRequest.parameters.required_review_thread_resolution !== true ||
      !status ||
      status.parameters.strict_required_status_checks_policy !== true) {
    throw new Error('The ruleset no longer satisfies the fail-closed safety policy.');
  }
  const checks = statusChecks(ruleset);
  for (const required of REQUIRED_BASELINE_CHECKS) {
    if (!checks.some((check) => sameCheck(check, required))) {
      throw new Error(
        `The existing required check ${required.context} is missing or changed.`);
    }
  }
  const keys = checks.map((check) =>
    `${check.context}:${Number(check.integration_id || 0)}`);
  if (new Set(keys).size !== keys.length) {
    throw new Error('The ruleset contains duplicate required-check identities.');
  }
}

function gateCheck(appId, checkName = CHECK_NAME) {
  const numericAppId = Number(appId);
  if (!Number.isInteger(numericAppId) || numericAppId <= 0 ||
      !String(checkName || '').trim()) {
    throw new Error('An exact numeric App ID and check name are required.');
  }
  return {
    context: String(checkName),
    integration_id: numericAppId,
  };
}

function updatePayload(ruleset, rules) {
  return {
    name: ruleset.name,
    target: ruleset.target,
    enforcement: ruleset.enforcement,
    bypass_actors: ruleset.bypass_actors || [],
    conditions: ruleset.conditions,
    rules,
  };
}

function withChecks(ruleset, checks) {
  return ruleset.rules.map((rule) => rule.type !== 'required_status_checks'
    ? rule
    : {
        ...rule,
        parameters: {
          ...rule.parameters,
          required_status_checks: checks,
        },
      });
}

function blockedActivation() {
  return {
    state: 'BLOCKED',
    code: ROLLOUT_BLOCKER_CODE,
    reason: ROLLOUT_BLOCKER,
    prerequisites: [...ACTIVATION_PREREQUISITES],
  };
}

function rollbackSnapshot(ruleset) {
  const payload = updatePayload(ruleset, ruleset.rules);
  return {
    rulesetId: Number(ruleset.id),
    digest: digest(payload),
    payload,
    instruction:
      'Preserve this snapshot for a future independently authorized rollback; ' +
      'this revision contains no mutation command.',
  };
}

function planRollout(ruleset, { appId, checkName = CHECK_NAME }) {
  assertSafetyPolicy(ruleset);
  const desired = gateCheck(appId, checkName);
  const checks = statusChecks(ruleset);
  if (checks.some((check) => check.context === desired.context)) {
    throw new Error(
      'A required check with the review-gate context already exists; refusing ambiguity.');
  }
  const payload = updatePayload(
    ruleset,
    withChecks(ruleset, [...checks, desired]));
  return {
    operation: 'plan-rollout-only',
    activation: blockedActivation(),
    beforeDigest: digest(updatePayload(ruleset, ruleset.rules)),
    proposedDigest: digest(payload),
    preservedCheckCount: checks.length,
    proposedCheck: desired,
    proposedPayload: payload,
    rollbackSnapshot: rollbackSnapshot(ruleset),
  };
}

function planRollback(ruleset, { appId, checkName = CHECK_NAME }) {
  assertSafetyPolicy(ruleset);
  const desired = gateCheck(appId, checkName);
  const checks = statusChecks(ruleset);
  const matches = checks.filter((check) => sameCheck(check, desired));
  if (matches.length !== 1) {
    throw new Error('The exact review-gate App check is absent or ambiguous.');
  }
  const payload = updatePayload(
    ruleset,
    withChecks(ruleset, checks.filter((check) => !sameCheck(check, desired))));
  return {
    operation: 'plan-rollback-only',
    activation: blockedActivation(),
    beforeDigest: digest(updatePayload(ruleset, ruleset.rules)),
    proposedDigest: digest(payload),
    preservedCheckCount: checks.length - 1,
    proposedRemoval: desired,
    proposedPayload: payload,
    rollbackSnapshot: rollbackSnapshot(ruleset),
  };
}

function parseArguments(argv) {
  const [operation, ...rest] = argv;
  const options = {};
  for (let index = 0; index < rest.length; index += 2) {
    const key = rest[index];
    const value = rest[index + 1];
    if (!key?.startsWith('--') || value === undefined) {
      throw new Error('Every option must use --name value syntax.');
    }
    options[key.slice(2)] = value;
  }
  return { operation, options };
}

function ghRead(args) {
  const response = spawnSync('gh', args, {
    encoding: 'utf8',
    maxBuffer: 8 * 1024 * 1024,
  });
  if (response.status !== 0) {
    const error = new Error('GitHub CLI rejected the read-only ruleset query.');
    error.status = response.status;
    throw error;
  }
  return response.stdout;
}

function parseIncludedResponse(output) {
  const etag = String(output).match(/^etag:\s*(.+)$/im)?.[1]?.trim();
  const bodyStart = String(output).indexOf('{');
  if (!etag || bodyStart < 0) {
    throw new Error('GitHub did not return a ruleset ETag and JSON body.');
  }
  return {
    etag,
    ruleset: JSON.parse(String(output).slice(bodyStart)),
  };
}

function loadReadOnly(options) {
  if (options.input) {
    return {
      etag: null,
      ruleset: JSON.parse(fs.readFileSync(options.input, 'utf8')),
    };
  }
  if (!options.repo || !options['ruleset-id']) {
    throw new Error('--repo and --ruleset-id are required for live read-only plans.');
  }
  return parseIncludedResponse(ghRead([
    'api',
    '-i',
    `repos/${options.repo}/rulesets/${options['ruleset-id']}`,
  ]));
}

function verifyCanaryRun(run, {
  checkRunId,
  headSha,
  appId,
  expectedProvenance,
  expectedEventPath,
  expectedAssociationKind,
  titlePrefix,
}) {
  validateGenerationProvenance(expectedProvenance);
  if (!Number.isInteger(checkRunId) ||
      checkRunId <= 0 ||
      !/^[0-9a-f]{40}$/.test(headSha) ||
      expectedProvenance.headSha !== headSha ||
      expectedProvenance.eventPath !== expectedEventPath ||
      expectedProvenance.association.kind !== expectedAssociationKind) {
    throw new Error('The canary expectation lacks exact event-path provenance.');
  }
  if (expectedEventPath === 'merge_group' &&
      (!Array.isArray(expectedProvenance.association.pullNumbers) ||
       expectedProvenance.association.pullNumbers.length === 0)) {
    throw new Error('Only a real merge group with constituent PRs counts.');
  }
  if (run.id !== checkRunId ||
      run.name !== CHECK_NAME ||
      run.head_sha !== headSha ||
      Number(run.app?.id) !== Number(appId) ||
      run.status !== 'completed' ||
      run.conclusion !== 'success' ||
      run.external_id !== externalIdForProvenance(expectedProvenance) ||
      !String(run.output?.title || '').startsWith(titlePrefix)) {
    throw new Error('The live canary lacks exact successful App-check provenance.');
  }
  return {
    checkRunId,
    headSha,
    appId: Number(appId),
    eventPath: expectedEventPath,
    associationKind: expectedAssociationKind,
    externalId: run.external_id,
    workerRevision: expectedProvenance.workerRevision,
    deliveryId: expectedProvenance.deliveryId,
    runId: expectedProvenance.runId,
  };
}

function main(argv = process.argv.slice(2)) {
  const { operation, options } = parseArguments(argv);
  if (String(operation || '').startsWith('apply')) {
    throw new Error(`${ROLLOUT_BLOCKER_CODE}: ${ROLLOUT_BLOCKER}`);
  }
  if (!new Set(['plan-rollout', 'plan-rollback']).has(operation)) {
    throw new Error(
      'Use plan-rollout or plan-rollback. Mutation is intentionally unavailable.');
  }
  const live = loadReadOnly(options);
  const parameters = {
    appId: Number(options['app-id']),
    checkName: CHECK_NAME,
  };
  const plan = operation === 'plan-rollback'
    ? planRollback(live.ruleset, parameters)
    : planRollout(live.ruleset, parameters);
  const output = {
    ...plan,
    observedEtag: live.etag,
    rollbackSnapshot: {
      ...plan.rollbackSnapshot,
      observedEtag: live.etag,
    },
  };
  process.stdout.write(`${JSON.stringify(output, null, 2)}\n`);
  return output;
}

if (require.main === module) {
  try {
    main();
  } catch (error) {
    process.stderr.write(`${error.message}\n`);
    process.exitCode = 1;
  }
}

module.exports = {
  ACTIVATION_PREREQUISITES,
  REQUIRED_BASELINE_CHECKS,
  ROLLOUT_BLOCKER,
  ROLLOUT_BLOCKER_CODE,
  assertSafetyPolicy,
  blockedActivation,
  digest,
  gateCheck,
  loadReadOnly,
  main,
  parseArguments,
  parseIncludedResponse,
  planRollback,
  planRollout,
  rollbackSnapshot,
  sameCheck,
  statusChecks,
  updatePayload,
  verifyCanaryRun,
};
