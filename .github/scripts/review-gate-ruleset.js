'use strict';

const crypto = require('node:crypto');
const { spawnSync } = require('node:child_process');
const fs = require('node:fs');

const REQUIRED_WORKFLOW_PATH = '.github/workflows/review-completion.yml';
const EXPECTED_STATUS_CONTEXTS = Object.freeze([
  'Build and test (Debug)',
  'Build and test (Release)',
  'Format verification',
  'NuGet vulnerability audit',
  'C# SAST (DevSkim)',
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

function statusContexts(ruleset) {
  const rule = ruleset.rules.find((candidate) =>
    candidate.type === 'required_status_checks');
  return rule?.parameters?.required_status_checks
    ?.map((check) => `${check.context}:${check.integration_id || ''}`)
    .sort() || [];
}

function assertCommonPolicy(ruleset) {
  const pullRequest = ruleset.rules.find((rule) => rule.type === 'pull_request');
  const expectedChecks = EXPECTED_STATUS_CONTEXTS
    .map((context) => `${context}:15368`)
    .sort();
  const statusRule = ruleset.rules.find((rule) =>
    rule.type === 'required_status_checks');
  if (ruleset.target !== 'branch' ||
      ruleset.enforcement !== 'active' ||
      JSON.stringify(ruleset.conditions?.ref_name?.include) !==
        JSON.stringify(['~DEFAULT_BRANCH']) ||
      (ruleset.conditions?.ref_name?.exclude || []).length !== 0 ||
      (ruleset.bypass_actors || []).length !== 0 ||
      !pullRequest ||
      pullRequest.parameters.required_approving_review_count !== 0 ||
      pullRequest.parameters.required_review_thread_resolution !== true ||
      !statusRule ||
      statusRule.parameters.strict_required_status_checks_policy !== true ||
      JSON.stringify(statusContexts(ruleset)) !== JSON.stringify(expectedChecks)) {
    throw new Error(
      'The live ruleset no longer matches the reviewed fail-closed baseline.');
  }
}

function workflowRule(repositoryId, workflowSha) {
  if (!Number.isInteger(repositoryId) || repositoryId <= 0 ||
      !/^[0-9a-f]{40}$/.test(String(workflowSha))) {
    throw new Error('An exact repository ID and trusted workflow SHA are required.');
  }
  return {
    type: 'workflows',
    parameters: {
      do_not_enforce_on_create: false,
      workflows: [{
        path: REQUIRED_WORKFLOW_PATH,
        repository_id: repositoryId,
        sha: workflowSha,
      }],
    },
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

function planRollout(ruleset, { repositoryId, workflowSha }) {
  assertCommonPolicy(ruleset);
  if (ruleset.rules.some((rule) => rule.type === 'workflows')) {
    throw new Error('A workflows rule already exists; refusing a cumulative edit.');
  }
  const payload = updatePayload(
    ruleset,
    [...ruleset.rules, workflowRule(repositoryId, workflowSha)]);
  return {
    operation: 'rollout',
    beforeDigest: digest(updatePayload(ruleset, ruleset.rules)),
    afterDigest: digest(payload),
    payload,
  };
}

function exactWorkflowRule(rule, { repositoryId, workflowSha }) {
  const workflows = rule?.parameters?.workflows;
  return rule?.type === 'workflows' &&
    rule.parameters.do_not_enforce_on_create === false &&
    Array.isArray(workflows) &&
    workflows.length === 1 &&
    workflows[0].path === REQUIRED_WORKFLOW_PATH &&
    Number(workflows[0].repository_id) === repositoryId &&
    workflows[0].sha === workflowSha &&
    (workflows[0].ref === undefined || workflows[0].ref === null);
}

function planRollback(ruleset, { repositoryId, workflowSha }) {
  assertCommonPolicy(ruleset);
  const workflowRules = ruleset.rules.filter((rule) => rule.type === 'workflows');
  if (workflowRules.length !== 1 ||
      !exactWorkflowRule(workflowRules[0], { repositoryId, workflowSha })) {
    throw new Error(
      'The live required-workflow rule differs from the reviewed rollout; refusing rollback.');
  }
  const payload = updatePayload(
    ruleset,
    ruleset.rules.filter((rule) => rule.type !== 'workflows'));
  return {
    operation: 'rollback',
    beforeDigest: digest(updatePayload(ruleset, ruleset.rules)),
    afterDigest: digest(payload),
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
    maxBuffer: 4 * 1024 * 1024,
  });
  if (response.status !== 0) {
    throw new Error('GitHub CLI rejected the ruleset operation.');
  }
  return response.stdout;
}

function repositoryUrl(value, repo) {
  const url = new URL(String(value || ''));
  if (url.protocol !== 'https:' || url.hostname !== 'github.com' ||
      !url.pathname.toLowerCase().startsWith(`/${repo}/`.toLowerCase())) {
    throw new Error('Canary/audit evidence must be a durable URL in this repository.');
  }
  return url.toString();
}

function loadLive(options) {
  if (options.input) {
    return JSON.parse(fs.readFileSync(options.input, 'utf8'));
  }
  if (!options.repo || !options['ruleset-id']) {
    throw new Error('--repo and --ruleset-id are required for live operations.');
  }
  return JSON.parse(gh([
    'api',
    `repos/${options.repo}/rulesets/${options['ruleset-id']}`,
  ]));
}

function verifyLiveGuards(live, options) {
  if (!options['expected-updated-at'] ||
      live.updated_at !== options['expected-updated-at']) {
    throw new Error('The live ruleset timestamp changed; restart rollout planning.');
  }
  if (!options['expected-main-sha']) {
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
  const live = loadLive(options);
  const parameters = {
    repositoryId: Number(options['repository-id']),
    workflowSha: options['workflow-sha'] || options['expected-main-sha'],
  };
  const isRollback = operation.endsWith('rollback');
  if (!isRollback && parameters.workflowSha !== options['expected-main-sha']) {
    throw new Error('Rollout must pin the exact verified live main SHA.');
  }
  const plan = isRollback
    ? planRollback(live, parameters)
    : planRollout(live, parameters);
  if (operation.startsWith('plan-')) {
    process.stdout.write(`${JSON.stringify(plan, null, 2)}\n`);
    return plan;
  }

  verifyLiveGuards(live, options);
  repositoryUrl(options['evidence-url'], options.repo);
  const requiredConfirmation = isRollback
    ? 'ROLLBACK EXACT REQUIRED WORKFLOW'
    : 'APPLY EXACT REQUIRED WORKFLOW';
  if (options.confirm !== requiredConfirmation) {
    throw new Error(`The exact confirmation "${requiredConfirmation}" is required.`);
  }
  gh([
    'api',
    `repos/${options.repo}/rulesets/${options['ruleset-id']}`,
    '--method',
    'PUT',
    '--input',
    '-',
  ], JSON.stringify(plan.payload));
  const after = loadLive({ ...options, input: undefined });
  assertCommonPolicy(after);
  const afterWorkflowRules = after.rules.filter((rule) => rule.type === 'workflows');
  const workflowStateMatches = isRollback
    ? afterWorkflowRules.length === 0
    : afterWorkflowRules.length === 1 &&
      exactWorkflowRule(afterWorkflowRules[0], parameters);
  const afterWithoutWorkflow = updatePayload(
    after,
    after.rules.filter((rule) => rule.type !== 'workflows'));
  const plannedWithoutWorkflow = updatePayload(
    live,
    live.rules.filter((rule) => rule.type !== 'workflows'));
  if (!workflowStateMatches ||
      digest(afterWithoutWorkflow) !== digest(plannedWithoutWorkflow)) {
    throw new Error('Post-update ruleset verification failed closed.');
  }
  process.stdout.write(
    `${operation} completed and the full ruleset payload was revalidated.\n`);
  return plan;
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
  EXPECTED_STATUS_CONTEXTS,
  REQUIRED_WORKFLOW_PATH,
  assertCommonPolicy,
  digest,
  main,
  planRollback,
  planRollout,
  statusContexts,
  workflowRule,
};
