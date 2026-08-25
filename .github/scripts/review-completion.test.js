'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');

const {
  BREAK_GLASS_PREFIX,
  CHECK_NAMES,
  EVIDENCE_PREFIX,
  currentCopilotReview,
  evaluateReviewCompletion,
  evidenceCheckName,
  mergeReady,
  replayTimingWindow,
  requiredDomains,
} = require('./review-completion');
const {
  listReviewThreads,
  isTrustedWorkflowCheck,
  dispatchLinkedIssue,
  ensureGateCheck,
  sanitizedApiFailure,
  shouldPoll,
  workflowRunId,
} = require('./run-review-completion');
const {
  BREAK_GLASS_CONFIRMATION,
  recordBreakGlass,
  recordReviewEvidence,
  repositoryEvidenceUrl,
} = require('./record-review-evidence');

const fixtures = JSON.parse(fs.readFileSync(
  path.join(__dirname, 'fixtures', 'review-completion-scenarios.json'),
  'utf8'));
const HEAD = 'a'.repeat(40);
const OLD_HEAD = 'b'.repeat(40);
const successCheck = {
  id: 10,
  status: 'completed',
  conclusion: 'success',
  app: { slug: 'github-actions' },
};
const currentReview = {
  state: 'COMMENTED',
  commit_id: HEAD,
  submitted_at: '2026-08-25T05:00:00Z',
  user: { login: 'copilot-pull-request-reviewer[bot]' },
};

function approved(overrides = {}) {
  return evaluateReviewCompletion({
    headSha: HEAD,
    copilotCheck: successCheck,
    copilotReview: currentReview,
    unresolvedThreads: 0,
    ...overrides,
  });
}

test('draft pull requests fail closed', () => {
  assert.deepEqual(approved({ draft: true }), {
    state: 'rejected',
    reasons: ['Draft pull requests are not ready for merge.'],
  });
});

test('current-head Copilot check and review are both required', () => {
  assert.equal(approved({ copilotCheck: null }).state, 'pending');
  assert.equal(approved({ copilotReview: null }).state, 'pending');
  assert.equal(approved({
    copilotCheck: { status: 'completed', conclusion: 'failure' },
  }).state, 'rejected');
});

test('historical Copilot review does not satisfy a new head', () => {
  const review = currentCopilotReview([{
    ...currentReview,
    commit_id: OLD_HEAD,
  }], HEAD);
  assert.equal(review, null);
  assert.equal(approved({ copilotReview: review }).state, 'pending');
});

test('author self-approval is never treated as Copilot completion', () => {
  const review = currentCopilotReview([{
    state: 'APPROVED',
    commit_id: HEAD,
    submitted_at: '2026-08-25T05:00:00Z',
    user: { login: 'cyrusjamula' },
  }], HEAD);
  assert.equal(review, null);
  assert.equal(approved({ copilotReview: review }).state, 'pending');
});

test('zero unresolved review threads is mandatory', () => {
  const result = approved({ unresolvedThreads: 1 });
  assert.equal(result.state, 'rejected');
  assert.match(result.reasons[0], /1 unresolved/);
});

test('issue and PR labels select independent review domains', () => {
  assert.deepEqual(requiredDomains([
    [{ name: 'area:architecture' }, { name: 'area:security' }],
    ['review:privacy-required', 'review:quality-required'],
  ]), ['architecture', 'security', 'privacy', 'quality']);
});

test('each selected independent domain needs trusted current-head evidence', () => {
  const missing = approved({
    domains: ['architecture'],
    evidence: {},
  });
  assert.equal(missing.state, 'pending');

  const rejected = approved({
    domains: ['architecture'],
    evidence: {
      architecture: { status: 'completed', conclusion: 'failure' },
    },
  });
  assert.equal(rejected.state, 'rejected');

  const complete = approved({
    domains: ['architecture'],
    evidence: { architecture: successCheck },
  });
  assert.equal(complete.state, 'approved');
});

test('break-glass can replace unavailable automation but not hard protections', () => {
  const breakGlass = { status: 'completed', conclusion: 'success' };
  assert.equal(evaluateReviewCompletion({
    headSha: HEAD,
    breakGlass,
  }).state, 'approved');
  assert.equal(evaluateReviewCompletion({
    headSha: HEAD,
    breakGlass,
    draft: true,
  }).state, 'rejected');
  assert.equal(evaluateReviewCompletion({
    headSha: HEAD,
    breakGlass,
    unresolvedThreads: 1,
  }).state, 'rejected');
});

test('merge-group and default-branch paths report explicit not-applicable success', () => {
  for (const pathName of ['merge_group', 'default_branch']) {
    const result = evaluateReviewCompletion({ path: pathName });
    assert.equal(result.state, 'not_applicable');
    assert.match(result.reasons[0], new RegExp(pathName));
  }
});

test('historical PR 100 and 101 timing window cannot become merge-ready early', () => {
  const fixture = fixtures[0];
  const timeline = replayTimingWindow(fixture.events);
  const firstReady = timeline.find((event) => event.mergeReady);
  assert.equal(firstReady.atSeconds, fixture.firstReadyAtSeconds);
  assert.ok(timeline
    .filter((event) => event.atSeconds < fixture.firstReadyAtSeconds)
    .every((event) => !event.mergeReady));
  assert.equal(mergeReady({
    'Build and test': 'success',
    'Review completion gate': 'pending',
  }), false);
});

test('new push immediately closes the prior merge-ready window', () => {
  const timeline = replayTimingWindow(fixtures[1].events);
  const atPush = timeline.find((event) => event.atSeconds === 300);
  assert.equal(atPush.mergeReady, false);
  assert.equal(timeline.at(-1).mergeReady, true);
});

test('review thread GraphQL pagination reads every page', async () => {
  const cursors = [];
  const github = {
    graphql: async (_query, variables) => {
      cursors.push(variables.cursor);
      return {
        repository: {
          pullRequest: {
            reviewThreads: variables.cursor === null
              ? {
                  nodes: [{ isResolved: true }],
                  pageInfo: { hasNextPage: true, endCursor: 'next' },
                }
              : {
                  nodes: [{ isResolved: false }],
                  pageInfo: { hasNextPage: false, endCursor: null },
                },
          },
        },
      };
    },
  };
  const context = { repo: { owner: 'Jamula', repo: 'Andreja' } };
  const threads = await listReviewThreads({
    github,
    context,
    pullNumber: 104,
  });
  assert.deepEqual(cursors, [null, 'next']);
  assert.deepEqual(threads.map((thread) => thread.isResolved), [true, false]);
});

test('workflow-run evidence identity is bound to its run URL and prefix', () => {
  const check = {
    app: { slug: 'github-actions' },
    external_id: `${EVIDENCE_PREFIX}architecture:${HEAD}:12345`,
    details_url: 'https://github.com/Jamula/Andreja/actions/runs/12345',
  };
  assert.equal(workflowRunId(
    check,
    `${EVIDENCE_PREFIX}architecture:${HEAD}:`,
  ), 12345);
  assert.equal(workflowRunId(
    { ...check, details_url: 'https://example.test/actions/runs/12345' },
    `${EVIDENCE_PREFIX}architecture:${HEAD}:`,
  ), null);
  assert.equal(workflowRunId(
    { ...check, app: { slug: 'other' } },
    `${EVIDENCE_PREFIX}architecture:${HEAD}:`,
  ), null);
});

test('trusted evidence also requires an unexpired workflow-bound artifact', async () => {
  const check = {
    app: { slug: 'github-actions' },
    external_id: `${EVIDENCE_PREFIX}architecture:${HEAD}:12345`,
    details_url: 'https://github.com/Jamula/Andreja/actions/runs/12345',
  };
  const context = {
    payload: {
      repository: {
        default_branch: 'main',
        full_name: 'Jamula/Andreja',
      },
    },
    repo: { owner: 'Jamula', repo: 'Andreja' },
  };
  const fakeGithub = (artifacts) => ({
    paginate: async () => artifacts,
    rest: {
      actions: {
        getWorkflowRun: async () => ({
          data: {
            event: 'workflow_dispatch',
            status: 'completed',
            conclusion: 'success',
            head_branch: 'main',
            path: '.github/workflows/record-review-evidence.yml',
            repository: { full_name: 'Jamula/Andreja' },
          },
        }),
        listWorkflowRunArtifacts() {},
      },
    },
  });
  const parameters = {
    context,
    check,
    prefix: `${EVIDENCE_PREFIX}architecture:${HEAD}:`,
    workflowPath: '.github/workflows/record-review-evidence.yml',
    artifactName: `review-evidence-architecture-approved-${HEAD}`,
    cache: new Map(),
  };
  assert.equal(await isTrustedWorkflowCheck({
    ...parameters,
    github: fakeGithub([{
      name: parameters.artifactName,
      expired: false,
    }]),
  }), true);
  assert.equal(await isTrustedWorkflowCheck({
    ...parameters,
    github: fakeGithub([{
      name: parameters.artifactName,
      expired: true,
    }]),
    cache: new Map(),
  }), false);
});

test('in-progress evidence workflow state is not cached across gate polls', async () => {
  let calls = 0;
  const artifactName = `review-evidence-architecture-approved-${HEAD}`;
  const github = {
    paginate: async () => [{
      name: artifactName,
      expired: false,
    }],
    rest: {
      actions: {
        getWorkflowRun: async () => {
          calls += 1;
          return {
            data: {
              event: 'workflow_dispatch',
              status: calls === 1 ? 'in_progress' : 'completed',
              conclusion: calls === 1 ? null : 'success',
              head_branch: 'main',
              path: '.github/workflows/record-review-evidence.yml',
              repository: { full_name: 'Jamula/Andreja' },
            },
          };
        },
        listWorkflowRunArtifacts() {},
      },
    },
  };
  const parameters = {
    github,
    context: {
      payload: {
        repository: {
          default_branch: 'main',
          full_name: 'Jamula/Andreja',
        },
      },
      repo: { owner: 'Jamula', repo: 'Andreja' },
    },
    check: {
      app: { slug: 'github-actions' },
      external_id: `${EVIDENCE_PREFIX}architecture:${HEAD}:12345`,
      details_url: 'https://github.com/Jamula/Andreja/actions/runs/12345',
    },
    prefix: `${EVIDENCE_PREFIX}architecture:${HEAD}:`,
    workflowPath: '.github/workflows/record-review-evidence.yml',
    artifactName,
    cache: new Map(),
  };
  assert.equal(await isTrustedWorkflowCheck(parameters), false);
  assert.equal(await isTrustedWorkflowCheck(parameters), true);
  assert.equal(calls, 2);
});

test('only single-PR metadata events poll for asynchronous completion', () => {
  assert.equal(shouldPoll('pull_request_target', 1), true);
  assert.equal(shouldPoll('workflow_run', 1), false);
  assert.equal(shouldPoll('issues', 2), false);
});

test('linked issue changes dispatch serialized reevaluation only for closing PRs', async () => {
  const dispatches = [];
  const github = {
    paginate: async () => [
      { number: 201, body: 'Closes #104' },
      { number: 202, body: 'Closes #105' },
    ],
    graphql: async () => ({
      repository: {
        pullRequest: {
          closingIssuesReferences: {
            nodes: [],
            pageInfo: { hasNextPage: false, endCursor: null },
          },
        },
      },
    }),
    rest: {
      pulls: { list() {} },
      actions: {
        createWorkflowDispatch: async (request) => dispatches.push(request),
      },
    },
  };
  await dispatchLinkedIssue({
    github,
    context: {
      payload: {
        issue: { number: 104 },
        repository: {
          default_branch: 'main',
          full_name: 'Jamula/Andreja',
        },
      },
      repo: { owner: 'Jamula', repo: 'Andreja' },
    },
    core: { info() {} },
  });
  assert.deepEqual(dispatches.map((request) => request.inputs.pr_number), ['201']);
  assert.equal(dispatches[0].ref, 'main');
});

test('a completed gate is replaced rather than reopened for late evidence', async () => {
  const creates = [];
  const updates = [];
  const github = {
    paginate: async () => [{
      id: 99,
      name: CHECK_NAMES.gate,
      status: 'completed',
      conclusion: 'failure',
      external_id: `review-completion-gate:v1:pr-104:${HEAD}`,
      started_at: '2026-08-25T05:00:00Z',
      app: { slug: 'github-actions' },
    }],
    rest: {
      checks: {
        listForRef() {},
        create: async (request) => {
          creates.push(request);
          return { data: { id: 100, ...request } };
        },
        update: async (request) => updates.push(request),
      },
    },
  };
  const context = {
    repo: { owner: 'Jamula', repo: 'Andreja' },
    runId: 12346,
    serverUrl: 'https://github.com',
  };
  const check = await ensureGateCheck({
    github,
    context,
    headSha: HEAD,
    identity: 'pr-104',
  });
  assert.equal(check.id, 100);
  assert.equal(creates.length, 1);
  assert.equal(updates.length, 0);
});

test('rate limits and API outages expose fail-closed categories', () => {
  assert.match(sanitizedApiFailure({ status: 429 }), /rate-limited/);
  assert.match(sanitizedApiFailure(new Error('private diagnostic')), /failed closed/);
  assert.doesNotMatch(
    sanitizedApiFailure(new Error('private diagnostic')),
    /private diagnostic/);
});

function recorderContext(inputs, overrides = {}) {
  return {
    actor: 'cyrusjamula',
    payload: { inputs },
    repo: { owner: 'Jamula', repo: 'Andreja' },
    runId: 12345,
    serverUrl: 'https://github.com',
    ...overrides,
  };
}

function recorderGithub({
  author = 'pr-author',
  permission = 'admin',
  headSha = HEAD,
} = {}) {
  const created = [];
  return {
    created,
    api: {
      rest: {
        pulls: {
          get: async () => ({
            data: {
              state: 'open',
              head: { sha: headSha },
              user: { login: author },
            },
          }),
        },
        repos: {
          getCollaboratorPermissionLevel: async () => ({
            data: { permission },
          }),
        },
        checks: {
          create: async (request) => {
            created.push(request);
            return { data: request };
          },
        },
      },
    },
  };
}

test('review evidence rejects author self-review and stale heads', async () => {
  const baseInputs = {
    pr_number: '104',
    head_sha: HEAD,
    domain: 'architecture',
    verdict: 'approved',
    reviewer: 'cyrusjamula',
    evidence_url: 'https://github.com/Jamula/Andreja/issues/104',
    summary: 'Independent architecture artifact reviewed.',
  };
  const self = recorderGithub({ author: 'cyrusjamula' });
  await assert.rejects(
    recordReviewEvidence({
      github: self.api,
      context: recorderContext(baseInputs),
      core: { info() {} },
    }),
    /author cannot be the independent reviewer/);

  const stale = recorderGithub({ headSha: OLD_HEAD });
  await assert.rejects(
    recordReviewEvidence({
      github: stale.api,
      context: recorderContext({
        ...baseInputs,
        reviewer: 'Spock',
      }),
      core: { info() {} },
    }),
    /supplied SHA is stale/);
});

test('review evidence creates a current-head durable check', async () => {
  const fake = recorderGithub();
  const artifactDirectory = path.join(__dirname, 'test-quality-artifact');
  fs.rmSync(artifactDirectory, { recursive: true, force: true });
  const outputs = {};
  try {
    await recordReviewEvidence({
      github: fake.api,
      context: recorderContext({
        pr_number: '104',
        head_sha: HEAD,
        domain: 'quality',
        verdict: 'approved',
        reviewer: 'Data',
        evidence_url: 'https://github.com/Jamula/Andreja/pull/104#issuecomment-1',
        summary: 'The fixture race and failure paths passed.',
      }),
      core: {
        info() {},
        setOutput(name, value) { outputs[name] = value; },
      },
      artifactDirectory,
    });
    assert.equal(fake.created.length, 1);
    assert.equal(fake.created[0].name, evidenceCheckName('quality'));
    assert.equal(fake.created[0].head_sha, HEAD);
    assert.equal(
      fake.created[0].external_id,
      `${EVIDENCE_PREFIX}quality:${HEAD}:12345`);
    assert.equal(
      outputs['artifact-name'],
      `review-evidence-quality-approved-${HEAD}`);
    assert.ok(fs.existsSync(outputs['artifact-path']));
  } finally {
    fs.rmSync(artifactDirectory, { recursive: true, force: true });
  }
});

test('break-glass requires a human maintainer and durable current-head audit', async () => {
  const inputs = {
    pr_number: '104',
    head_sha: HEAD,
    confirmation: BREAK_GLASS_CONFIRMATION,
    reason: 'Reviewer automation is unavailable during an urgent incident.',
    incident_url: 'https://github.com/Jamula/Andreja/issues/104',
  };
  const bot = recorderGithub();
  await assert.rejects(
    recordBreakGlass({
      github: bot.api,
      context: recorderContext(inputs, { actor: 'emergency[bot]' }),
      core: { warning() {} },
    }),
    /explicit human/);

  const reader = recorderGithub({ permission: 'push' });
  await assert.rejects(
    recordBreakGlass({
      github: reader.api,
      context: recorderContext(inputs),
      core: { warning() {} },
    }),
    /maintain or admin/);

  const maintainer = recorderGithub();
  const artifactDirectory = path.join(__dirname, 'test-break-glass-artifact');
  fs.rmSync(artifactDirectory, { recursive: true, force: true });
  try {
    await recordBreakGlass({
      github: maintainer.api,
      context: recorderContext(inputs),
      core: { warning() {}, setOutput() {} },
      artifactDirectory,
    });
    assert.equal(maintainer.created[0].name, CHECK_NAMES.breakGlass);
    assert.equal(
      maintainer.created[0].external_id,
      `${BREAK_GLASS_PREFIX}${HEAD}:12345`);
  } finally {
    fs.rmSync(artifactDirectory, { recursive: true, force: true });
  }
});

test('evidence URLs remain repository-local', () => {
  const context = recorderContext({});
  assert.equal(
    repositoryEvidenceUrl(
      'https://github.com/Jamula/Andreja/issues/104',
      context),
    'https://github.com/Jamula/Andreja/issues/104');
  assert.throws(
    () => repositoryEvidenceUrl('https://example.test/evidence', context),
    /this GitHub repository/);
});

test('privileged workflows execute only pinned trusted default-branch automation', () => {
  const workflows = [
    'review-completion.yml',
    'record-review-evidence.yml',
    'record-review-break-glass.yml',
  ].map((file) => ({
    file,
    source: fs.readFileSync(
      path.join(__dirname, '..', 'workflows', file),
      'utf8'),
  }));

  for (const { file, source } of workflows) {
    assert.match(source, /ref: \$\{\{ github\.event\.repository\.default_branch \}\}/);
    assert.match(source, /persist-credentials: false/);
    assert.doesNotMatch(source, /^\s+contents:\s+write\s*$/m);
    assert.doesNotMatch(source, /\bsecrets\./);
    for (const match of source.matchAll(/uses:\s*[^@\s]+@([^\s]+)/g)) {
      assert.match(match[1], /^[0-9a-f]{40}$/, `${file} action must be SHA-pinned`);
    }
  }

  const gate = workflows.find(({ file }) => file === 'review-completion.yml').source;
  assert.match(gate, /^\s{2}pull_request_target:\s*$/m);
  assert.match(gate, /^\s{2}merge_group:\s*$/m);
  assert.match(gate, /^\s{2}push:\s*$/m);
  assert.doesNotMatch(gate, /^\s{2}pull_request:\s*$/m);

  for (const file of ['record-review-evidence.yml', 'record-review-break-glass.yml']) {
    const source = workflows.find((entry) => entry.file === file).source;
    assert.match(source, /^\s{2}workflow_dispatch:\s*$/m);
    assert.doesNotMatch(source, /^\s{2}pull_request(?:_target)?:\s*$/m);
  }
});

test('active and installed label sync define every explicit review requirement', () => {
  const sources = [
    path.join(__dirname, '..', 'workflows', 'sync-squad-labels.yml'),
    path.join(
      __dirname,
      '..',
      '..',
      '.squad',
      'templates',
      'workflows',
      'sync-squad-labels.yml'),
  ].map((file) => fs.readFileSync(file, 'utf8'));
  for (const domain of ['architecture', 'security', 'privacy', 'quality']) {
    for (const source of sources) {
      assert.match(source, new RegExp(`review:${domain}-required`));
    }
  }
});
