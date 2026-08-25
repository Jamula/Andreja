'use strict';

const crypto = require('node:crypto');
const { spawnSync } = require('node:child_process');
const fs = require('node:fs');

const {
  CHECK_EXTERNAL_PREFIX,
  CHECK_NAME,
} = require('./review-gate-policy');

const REQUIRED_BASELINE_CHECKS = Object.freeze([
  { context: 'Build and test (Debug)', integration_id: 15368 },
  { context: 'Build and test (Release)', integration_id: 15368 },
  { context: 'Format verification', integration_id: 15368 },
  { context: 'NuGet vulnerability audit', integration_id: 15368 },
  { context: 'C# SAST (DevSkim)', integration_id: 15368 },
]);
const TRUSTED_PATHS = Object.freeze([
  '.github/workflows/review-gate-app.yml',
  '.github/workflows/review-gate-app-admin.yml',
  '.github/scripts/review-gate-policy.js',
  '.github/scripts/review-gate-app.js',
  '.github/scripts/record-review-gate-policy.js',
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
    throw new Error('The live ruleset no longer satisfies the fail-closed safety policy.');
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
    throw new Error('The live ruleset contains duplicate required-check identities.');
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
    operation: 'rollout',
    beforeDigest: digest(updatePayload(ruleset, ruleset.rules)),
    afterDigest: digest(payload),
    preservedCheckCount: checks.length,
    addedCheck: desired,
    payload,
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
    operation: 'rollback',
    beforeDigest: digest(updatePayload(ruleset, ruleset.rules)),
    afterDigest: digest(payload),
    preservedCheckCount: checks.length - 1,
    removedCheck: desired,
    payload,
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

function gh(args, input) {
  const response = spawnSync('gh', args, {
    encoding: 'utf8',
    input,
    maxBuffer: 8 * 1024 * 1024,
  });
  if (response.status !== 0) {
    const error = new Error('GitHub CLI rejected the guarded ruleset operation.');
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

function loadLive(options, { allowInput = true } = {}) {
  if (options.input) {
    if (!allowInput) {
      throw new Error('--input snapshots are forbidden for apply operations.');
    }
    return {
      etag: null,
      ruleset: JSON.parse(fs.readFileSync(options.input, 'utf8')),
    };
  }
  if (!options.repo || !options['ruleset-id']) {
    throw new Error('--repo and --ruleset-id are required for live operations.');
  }
  return parseIncludedResponse(gh([
    'api',
    '-i',
    `repos/${options.repo}/rulesets/${options['ruleset-id']}`,
  ]));
}

function repositoryUrl(value, repo) {
  const url = new URL(String(value || ''));
  if (url.protocol !== 'https:' || url.hostname !== 'github.com' ||
      !url.pathname.toLowerCase().startsWith(`/${repo}/`.toLowerCase())) {
    throw new Error('Canary/audit evidence must be a durable URL in this repository.');
  }
  return url.toString();
}

function verifyMainAndTrustedFiles(options) {
  if (!options['expected-main-sha'] ||
      !/^[0-9a-f]{40}$/.test(options['expected-main-sha'])) {
    throw new Error('--expected-main-sha is required.');
  }
  const mainSha = gh([
    'api',
    `repos/${options.repo}/commits/main`,
    '--jq',
    '.sha',
  ]).trim();
  if (mainSha !== options['expected-main-sha']) {
    throw new Error('The live main tip changed; restart rollout planning.');
  }
  for (const path of TRUSTED_PATHS) {
    const encoded = path.split('/').map(encodeURIComponent).join('/');
    const sha = gh([
      'api',
      `repos/${options.repo}/contents/${encoded}?ref=${mainSha}`,
      '--jq',
      '.sha',
    ]).trim();
    if (!/^[0-9a-f]{40}$/.test(sha)) {
      throw new Error(`Trusted default-branch file ${path} is unavailable.`);
    }
  }
}

function verifyCanaryRun(run, {
  checkRunId,
  headSha,
  appId,
  checkName,
  titlePrefix,
}) {
  if (!Number.isInteger(checkRunId) || checkRunId <= 0 ||
      !/^[0-9a-f]{40}$/.test(headSha)) {
    throw new Error('An exact live canary check-run ID and head SHA are required.');
  }
  if (run.name !== checkName ||
      run.head_sha !== headSha ||
      Number(run.app?.id) !== appId ||
      run.status !== 'completed' ||
      run.conclusion !== 'success' ||
      !String(run.external_id || '').startsWith(`${CHECK_EXTERNAL_PREFIX}:`) ||
      !String(run.output?.title || '').startsWith(titlePrefix)) {
    throw new Error('The live canary lacks exact successful App-check provenance.');
  }
  return {
    checkRunId,
    headSha,
    appId,
    externalId: run.external_id,
  };
}

function verifyCanary(options, {
  checkRunIdOption,
  headSha,
  titlePrefix,
}) {
  const checkRunId = Number(options[checkRunIdOption]);
  if (!Number.isInteger(checkRunId) || checkRunId <= 0 ||
      !/^[0-9a-f]{40}$/.test(headSha)) {
    throw new Error('An exact live canary check-run ID and head SHA are required.');
  }
  const run = JSON.parse(gh([
    'api',
    `repos/${options.repo}/check-runs/${checkRunId}`,
  ]));
  return verifyCanaryRun(run, {
    checkRunId,
    headSha,
    appId: Number(options['app-id']),
    checkName: CHECK_NAME,
    titlePrefix,
  });
}

function verifyCanaries(options) {
  return {
    pullRequest: verifyCanary(options, {
      checkRunIdOption: 'pr-canary-check-run-id',
      headSha: String(options['pr-canary-head-sha'] || ''),
      titlePrefix: 'Approved',
    }),
    mergeGroup: verifyCanary(options, {
      checkRunIdOption: 'merge-group-canary-check-run-id',
      headSha: String(options['merge-group-canary-head-sha'] || ''),
      titlePrefix: 'Not applicable',
    }),
    defaultBranch: verifyCanary(options, {
      checkRunIdOption: 'main-check-run-id',
      headSha: String(options['expected-main-sha'] || ''),
      titlePrefix: 'Not applicable',
    }),
  };
}

function applyPlanWithClient({
  etag,
  plan,
  client,
}) {
  if (!etag) {
    throw new Error('A live ETag is required for mutation.');
  }
  client.update(plan.payload, etag);
  const after = client.load();
  assertSafetyPolicy(after.ruleset);
  if (digest(updatePayload(after.ruleset, after.ruleset.rules)) !==
      digest(plan.payload)) {
    throw new Error('Post-update ruleset verification failed closed.');
  }
  return after;
}

function cliClient(options) {
  return {
    update(payload, etag) {
      gh([
        'api',
        `repos/${options.repo}/rulesets/${options['ruleset-id']}`,
        '--method',
        'PUT',
        '-H',
        `If-Match: ${etag}`,
        '--input',
        '-',
      ], JSON.stringify(payload));
    },
    load() {
      return loadLive({ ...options, input: undefined }, { allowInput: false });
    },
  };
}

function assertApplySnapshot(live, options) {
  const currentDigest = digest(updatePayload(live.ruleset, live.ruleset.rules));
  if (!options['expected-ruleset-digest'] ||
      options['expected-ruleset-digest'] !== currentDigest) {
    throw new Error('The live ruleset digest changed; rerun plan against live state.');
  }
  if (!options['expected-etag'] || options['expected-etag'] !== live.etag) {
    throw new Error('The live ruleset ETag changed; rerun plan against live state.');
  }
}

function main(argv = process.argv.slice(2)) {
  const { operation, options } = parseArguments(argv);
  if (!new Set([
    'plan-rollout',
    'apply-rollout',
    'plan-rollback',
    'apply-rollback',
  ]).has(operation)) {
    throw new Error(
      'Use plan-rollout, apply-rollout, plan-rollback, or apply-rollback.');
  }
  const isApply = operation.startsWith('apply-');
  const isRollback = operation.endsWith('rollback');
  if (isApply && options.input) {
    throw new Error('--input snapshots are forbidden for apply operations.');
  }
  const live = loadLive(options, { allowInput: !isApply });
  assertSafetyPolicy(live.ruleset);
  const parameters = {
    appId: Number(options['app-id']),
    checkName: CHECK_NAME,
  };
  const plan = isRollback
    ? planRollback(live.ruleset, parameters)
    : planRollout(live.ruleset, parameters);
  const output = {
    ...plan,
    etag: live.etag,
  };
  if (!isApply) {
    process.stdout.write(`${JSON.stringify(output, null, 2)}\n`);
    return output;
  }

  assertApplySnapshot(live, options);
  verifyMainAndTrustedFiles(options);
  repositoryUrl(options['evidence-url'], options.repo);
  if (!isRollback) {
    output.canaries = verifyCanaries(options);
  }
  const confirmation = isRollback
    ? 'ROLLBACK EXACT APP CHECK'
    : 'APPLY EXACT APP CHECK';
  if (options.confirm !== confirmation) {
    throw new Error(`The exact confirmation "${confirmation}" is required.`);
  }
  verifyMainAndTrustedFiles(options);
  const after = applyPlanWithClient({
    etag: live.etag,
    plan,
    client: cliClient(options),
  });
  verifyMainAndTrustedFiles(options);
  const evidence = {
    operation: plan.operation,
    beforeEtag: live.etag,
    afterEtag: after.etag,
    beforeDigest: plan.beforeDigest,
    afterDigest: plan.afterDigest,
    preservedCheckCount: plan.preservedCheckCount,
    appId: parameters.appId,
    checkName: parameters.checkName,
    mainSha: options['expected-main-sha'],
    evidenceUrl: repositoryUrl(options['evidence-url'], options.repo),
    canaries: output.canaries || null,
  };
  process.stdout.write(`${JSON.stringify(evidence, null, 2)}\n`);
  return evidence;
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
  CHECK_NAME,
  REQUIRED_BASELINE_CHECKS,
  TRUSTED_PATHS,
  applyPlanWithClient,
  assertApplySnapshot,
  assertSafetyPolicy,
  digest,
  gateCheck,
  loadLive,
  main,
  parseArguments,
  parseIncludedResponse,
  planRollback,
  planRollout,
  repositoryUrl,
  sameCheck,
  statusChecks,
  updatePayload,
  verifyCanary,
  verifyCanaries,
  verifyCanaryRun,
};
