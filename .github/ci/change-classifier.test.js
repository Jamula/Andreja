'use strict';

const assert = require('node:assert/strict');
const { execFileSync } = require('node:child_process');
const { createHash } = require('node:crypto');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const {
  ALL_DOMAINS,
  acquireChanges,
  attachTrustedMarkdownBase,
  authorizeLabeledRun,
  authorizeRepositoryDispatchSmoke,
  classifyFiles,
  createBudgetedFetch,
  inspectMarkdownPatch,
  loadPolicy,
  main,
  paginatedFiles,
  shadowSample,
} = require('./change-classifier');

const root = path.resolve(__dirname, '../..');
const policy = loadPolicy(path.join(__dirname, 'change-policy.v1.json'));
const fixtures = require('./fixtures/change-classifier-cases.json');
const {
  classificationName,
  estimatedMinutes,
  replayForcedFullReasons,
} = require('./replay-merged-prs');
let aggregateArtifactSequence = 0;

function githubResponse(body, link = null, rateLimitRemaining = '14999') {
  return {
    ok: true,
    status: 200,
    statusText: 'OK',
    headers: {
      get: (name) => {
        if (name === 'link') return link;
        if (name === 'x-ratelimit-remaining') return rateLimitRemaining;
        if (name === 'x-ratelimit-reset') return '1787750000';
        return null;
      },
    },
    json: async () => body,
  };
}

function runAggregateArtifact(overrides) {
  const workflow = fs.readFileSync(
    path.join(root, '.github/workflows/selective-ci-shadow.yml'),
    'utf8',
  );
  const match = workflow.match(/node <<'NODE'\r?\n([\s\S]*?)\r?\n {10}NODE/);
  assert.ok(match, 'aggregate Node script must remain directly testable');
  aggregateArtifactSequence += 1;
  const artifact = path.join(
    root,
    `artifacts/selective-ci/aggregate-test-${process.pid}-${aggregateArtifactSequence}.json`,
  );
  fs.mkdirSync(path.dirname(artifact), { recursive: true });
  fs.rmSync(artifact, { force: true });
  let status = 0;
  try {
    execFileSync(process.execPath, ['-e', match[1].replace(/^ {10}/gm, '')], {
      cwd: root,
      env: {
        ...process.env,
        AGGREGATE_EVIDENCE_PATH: artifact,
        GITHUB_EVENT_NAME: 'pull_request',
        GITHUB_RUN_ATTEMPT: '1',
        GITHUB_SHA: 'c'.repeat(40),
        EVENT_BASE_SHA: 'a'.repeat(40),
        EVENT_HEAD_SHA: 'b'.repeat(40),
        VALIDATED_REF: 'c'.repeat(40),
        VALIDATED_SHA: 'c'.repeat(40),
        ...overrides,
      },
    });
  } catch (error) {
    status = error.status;
  }
  const evidence = JSON.parse(fs.readFileSync(artifact, 'utf8'));
  fs.rmSync(artifact, { force: true });
  return { evidence, status };
}

function runBootstrapDecision(actor, operator, options = {}) {
  const workflow = fs.readFileSync(
    path.join(root, '.github/workflows/selective-ci-shadow.yml'),
    'utf8',
  );
  const match = workflow.match(
    /node <<'BOOTSTRAP_NODE'\r?\n([\s\S]*?)\r?\n {10}BOOTSTRAP_NODE/,
  );
  assert.ok(match, 'bootstrap Node script must remain directly testable');
  const directory = path.join(root, 'artifacts/selective-ci');
  const eventPath = path.join(directory, `bootstrap-event-${process.pid}.json`);
  const decisionPath = path.join(directory, `bootstrap-decision-${process.pid}.json`);
  const outputPath = path.join(directory, `bootstrap-output-${process.pid}.txt`);
  fs.mkdirSync(directory, { recursive: true });
  fs.writeFileSync(eventPath, JSON.stringify(options.event ?? {
      action: 'labeled',
      label: { name: 'ci:selective-shadow-sample' },
      sender: { login: actor },
      pull_request: {
        labels: [{ name: 'ci:selective-shadow-sample' }],
      },
    }));
  fs.writeFileSync(outputPath, '');
  try {
    execFileSync(
      process.execPath,
      ['-e', match[1].replace(/^ {10}/gm, '')],
      {
        cwd: root,
        env: {
          ...process.env,
          GITHUB_EVENT_NAME: options.eventName ?? 'pull_request_target',
          GITHUB_EVENT_PATH: eventPath,
          GITHUB_ACTOR: actor,
          GITHUB_REF: options.workflowRef ?? 'refs/heads/main',
          GITHUB_SHA: options.workflowSha ?? smokeSha,
          GITHUB_RUN_ID: options.runId ?? '700',
          GITHUB_RUN_ATTEMPT: options.runAttempt ?? '1',
          CHANGE_DECISION_PATH: decisionPath,
          GITHUB_OUTPUT: outputPath,
          SELECTIVE_CI_SAMPLE_OPERATOR: operator,
          SELECTIVE_CI_CONTROLLER_SHA: options.controllerSha ?? smokeSha,
          SELECTIVE_CI_WINDOW_START:
            options.windowStart ?? '2026-08-26T10:00:00Z',
        },
      },
    );
    return JSON.parse(fs.readFileSync(decisionPath, 'utf8'));
  } finally {
    fs.rmSync(eventPath, { force: true });
    fs.rmSync(decisionPath, { force: true });
    fs.rmSync(outputPath, { force: true });
  }
}

for (const fixture of fixtures) {
  test(fixture.name, () => {
    const result = classifyFiles(fixture.files, policy);
    const selected = ALL_DOMAINS.filter((domain) => result.domains[domain].selected).sort();
    assert.deepEqual(selected, fixture.selected.sort());
    assert.equal(result.fullSuite, fixture.fullSuite);
    if (fixture.reasons) {
      assert.deepEqual(
        result.files.flatMap((file) => file.reasons),
        fixture.reasons,
      );
    }
  });
}

test('every ASCII control is rejected in current and previous filenames', () => {
  for (const codePoint of [...Array(32).keys(), 0x7f]) {
    const control = String.fromCharCode(codePoint);
    for (const file of [
      { filename: `docs/current${control}.md`, status: 'modified' },
      {
        filename: 'docs/current.md',
        previous_filename: `docs/previous${control}.md`,
        status: 'modified',
      },
    ]) {
      const result = classifyFiles([file], policy);
      assert.equal(result.fullSuite, true, `U+${codePoint.toString(16).padStart(4, '0')}`);
      assert.ok(result.files[0].reasons.includes('invalid-path'));
    }
  }
});

test('Markdown patch change metadata must be a positive integer', () => {
  const baseFile = {
    filename: 'docs/charter.md',
    status: 'modified',
    additions: 1,
    deletions: 1,
    baseContent: 'old\n',
    patch: '@@ -1 +1 @@\n-old\n+new',
  };
  for (const changes of [undefined, null, 0, -1, 1.5, '2', NaN, Infinity, {}, []]) {
    const inspection = inspectMarkdownPatch({ ...baseFile, changes });
    assert.equal(
      inspection.uncertain,
      'markdown-patch-change-count-invalid',
      `changes=${String(changes)} must fail closed`,
    );
  }
  assert.equal(
    inspectMarkdownPatch({ ...baseFile, changes: 3 }).uncertain,
    'markdown-patch-change-count-mismatch',
  );
  assert.equal(
    inspectMarkdownPatch({ ...baseFile, changes: 1 }).uncertain,
    'markdown-patch-change-count-mismatch',
  );
  assert.equal(inspectMarkdownPatch({ ...baseFile, changes: 2 }).uncertain, undefined);
});

test('all tracked paths have an explicit policy rule', () => {
  const files = execFileSync('git', ['ls-files', '-z'], { cwd: root })
    .toString('utf8')
    .split('\0')
    .filter(Boolean)
    .map((filename) => ({
      filename,
      status: 'modified',
      changes: 2,
      baseContent: 'old prose\n',
      patch: '@@ -1 +1 @@\n-old prose\n+new prose',
    }));
  const result = classifyFiles(files, policy);
  const unknown = result.files.filter((file) => file.rule === 'unclassified').map((file) => file.path);
  assert.deepEqual(unknown, []);
});

test('every policy rule is reachable as the first matching rule', () => {
  const representatives = {
    'ci-security-boundary': '.github/workflows/example.yml',
    'selective-ci-runbook-governance': 'docs/operations/selective-ci.md',
    'central-build-dependency-sdk': 'global.json',
    'dependency-update-configuration': '.github/dependabot.yml',
    'github-governance-boundary': '.github/actions/setup/action.yml',
    'squad-governance-boundary': '.squad/example.md',
    'runtime-configuration-boundary': '.mcp.json',
    'generated-schema': 'generated/example.schema.json',
    'postgres-migration': 'src/Adapters/Andreja.Adapters/PostgreSql/Migrations/Example.cs',
    'docker-compose-iac': 'Dockerfile',
    'supply-chain-evidence': 'scripts/supply-chain/example.ps1',
    powershell: 'scripts/example.ps1',
    'deployed-javascript': 'src/example.js',
    'javascript-browser': 'scripts/browser-harness.js',
    'dotnet-source': 'src/Example.cs',
    'app-web-assets': 'src/example.css',
    'dotnet-test-fixtures': 'tests/example.json',
    'docs-tooling': 'scripts/docs/example.py',
    'known-executable-documentation': 'docs/development.md',
    'public-website-prototype': 'docs/public-website/prototype/index.html',
    documentation: 'docs/example.md',
    'documentation-artifact': 'docs/architecture/andreja-high-level.png',
    'inert-repository-metadata': '.github/FUNDING.yml',
  };
  assert.deepEqual(
    policy.rules.map((rule) => rule.id).sort(),
    Object.keys(representatives).sort(),
  );
  for (const [rule, filename] of Object.entries(representatives)) {
    const result = classifyFiles([{
      filename,
      status: 'modified',
      changes: 2,
      baseContent: 'old prose\n',
      patch: '@@ -1 +1 @@\n-old prose\n+new prose',
    }], policy);
    assert.equal(result.files[0].rule, rule, `${rule} is shadowed by ${result.files[0].rule}`);
  }
});

test('selective CI runbook governance rule is exactly anchored', () => {
  const rule = policy.rules.find(({ id }) => id === 'selective-ci-runbook-governance');
  assert.ok(rule);
  assert.equal(rule.pattern, '^docs/operations/selective-ci\\.md$');
  assert.equal(rule.fullSuite, true);

  const sibling = classifyFiles([{
    filename: 'docs/operations/selective-ci-notes.md',
    status: 'modified',
    changes: 2,
    baseContent: 'old prose\n',
    patch: '@@ -1 +1 @@\n-old prose\n+new prose',
  }], policy);
  assert.equal(sibling.files[0].rule, 'documentation');
  assert.equal(sibling.fullSuite, false);
});

test('generic documentation is Markdown-only with explicit inert artifacts', () => {
  const documentation = policy.rules.find(({ id }) => id === 'documentation');
  const artifacts = policy.rules.find(({ id }) => id === 'documentation-artifact');
  assert.equal(documentation.pattern, '^(docs/.*\\.md|LICENSE)$');
  assert.equal(
    artifacts.pattern,
    '^docs/architecture/andreja-high-level\\.(png|svg|excalidraw|png\\.sha256)$',
  );
  assert.equal(artifacts.caseSensitive, true);
  for (const filename of [
    'docs/example.html',
    'docs/example.py',
    'docs/example.sh',
    'docs/example.ts',
    'docs/example.tsx',
    'docs/example.jsx',
    'docs/example.json',
    'docs/example.yaml',
    'docs/example.png',
    'docs/example.svg',
    'docs/architecture/Andreja-high-level.png',
    'docs/architecture/andreja-high-level.SVG',
    'docs/example.unknown',
  ]) {
    const result = classifyFiles([{ filename, status: 'modified' }], policy);
    assert.equal(result.fullSuite, true, `${filename} must fail closed`);
  }
  const docsJavaScript = classifyFiles(
    [{ filename: 'docs/example.js', status: 'modified' }],
    policy,
  );
  assert.equal(docsJavaScript.files[0].rule, 'javascript-browser');
  assert.deepEqual(
    ALL_DOMAINS.filter((domain) => docsJavaScript.domains[domain].selected),
    ['javascript'],
  );
  for (const filename of [
    'docs/public-website/prototype/index.html',
    'docs/public-website/prototype/app.js',
  ]) {
    const prototype = classifyFiles([{ filename, status: 'modified' }], policy);
    assert.equal(prototype.files[0].rule, 'public-website-prototype');
    assert.equal(prototype.fullSuite, true);
  }
});

test('C sharp and Docker changes can never be docs-only', () => {
  for (const filename of ['src/Andreja.AppHost/Program.cs', 'Dockerfile']) {
    const result = classifyFiles([{ filename, status: 'modified' }], policy);
    assert.equal(result.domains.dotnet.selected, true);
    assert.equal(result.domains.oci.selected, true);
    assert.notDeepEqual(
      ALL_DOMAINS.filter((domain) => result.domains[domain].selected),
      ['docs'],
    );
  }
});

test('classifier changes fail closed to every domain', () => {
  const result = classifyFiles(
    [{ filename: '.github/ci/change-classifier.js', status: 'modified' }],
    policy,
  );
  assert.equal(result.fullSuite, true);
  assert.deepEqual(
    ALL_DOMAINS.filter((domain) => result.domains[domain].selected),
    ALL_DOMAINS,
  );
});

test('MCP runtime configuration can never be classified as docs-only', () => {
  const result = classifyFiles([{ filename: '.mcp.json', status: 'modified' }], policy);
  assert.equal(result.files[0].rule, 'runtime-configuration-boundary');
  assert.equal(result.fullSuite, true);
  assert.deepEqual(
    ALL_DOMAINS.filter((domain) => result.domains[domain].selected),
    ALL_DOMAINS,
  );
});

test('Git attributes repository behavior can never be classified as docs-only', () => {
  const result = classifyFiles([{ filename: '.gitattributes', status: 'modified' }], policy);
  assert.equal(result.files[0].rule, 'runtime-configuration-boundary');
  assert.equal(result.fullSuite, true);
  assert.deepEqual(
    ALL_DOMAINS.filter((domain) => result.domains[domain].selected),
    ALL_DOMAINS,
  );
});

test('PR metadata acquisition follows every pagination link', async () => {
  const calls = [];
  const pages = new Map([
    [
      'https://api.github.com/repos/Jamula/Andreja/pulls/102/files?per_page=100',
      {
        body: [{ filename: 'docs/a.md', status: 'modified', changes: 2, patch: '@@ -1 +1 @@\n-a\n+b' }],
        link: '<https://api.github.com/page/2>; rel="next"',
      },
    ],
    [
      'https://api.github.com/page/2',
      { body: [{ filename: 'src/App.cs', status: 'added' }], link: null },
    ],
    [
      `https://api.github.com/repos/Jamula/Andreja/compare/${'a'.repeat(40)}...${'b'.repeat(40)}?per_page=1`,
      {
        body: {
          merge_base_commit: { sha: 'c'.repeat(40) },
          files: [
            { filename: 'docs/a.md', status: 'modified', changes: 2, patch: '@@ -1 +1 @@\n-a\n+b' },
            { filename: 'src/App.cs', status: 'added' },
          ],
        },
        link: null,
      },
    ],
  ]);
  const fetchImpl = async (url) => {
    calls.push(url);
    const page = pages.get(url);
    return {
      ok: true,
      status: 200,
      statusText: 'OK',
      headers: { get: () => page.link },
      json: async () => page.body,
    };
  };
  const result = await acquireChanges(
    'pull_request_target',
    {
      number: 102,
      pull_request: {
        changed_files: 2,
        base: { sha: 'a'.repeat(40) },
        head: { sha: 'b'.repeat(40) },
        merge_commit_sha: 'd'.repeat(40),
      },
    },
    'Jamula/Andreja',
    'read-only-token',
    fetchImpl,
  );
  assert.equal(result.files.length, 2);
  assert.deepEqual(result.forcedFullReasons, []);
  assert.equal(result.trustedContentSha, 'c'.repeat(40));
  assert.equal(calls.length, 3);
});

const smokeSha = 'd'.repeat(40);
const smokeEvent = {
  action: 'selective-ci-smoke',
  sender: { login: 'Jett-Reno' },
};
const smokeRuntime = (overrides = {}) => ({
  actor: 'Jett-Reno',
  expectedOperator: 'Jett-Reno',
  configuredControllerSha: smokeSha,
  windowStart: '2026-08-26T10:00:00Z',
  now: '2026-08-26T10:05:00Z',
  workflowRef: 'refs/heads/main',
  workflowSha: smokeSha,
  runId: '700',
  runAttempt: '1',
  delay: async () => {},
  ...overrides,
});
const smokeRun = (overrides = {}) => ({
  id: 700,
  event: 'repository_dispatch',
  head_sha: smokeSha,
  head_branch: 'main',
  path: '.github/workflows/selective-ci-shadow.yml',
  head_repository: { full_name: 'Jamula/Andreja' },
  actor: { login: 'Jett-Reno' },
  run_attempt: 1,
  created_at: '2026-08-26T10:01:00Z',
  ...overrides,
});

const labelEvent = {
  action: 'labeled',
  label: { name: 'ci:selective-shadow-sample' },
  sender: { login: 'Jett-Reno' },
  pull_request: {
    number: 118,
    labels: [{ name: 'ci:selective-shadow-sample' }],
  },
};
const labelRuntime = (overrides = {}) => ({
  actor: 'Jett-Reno',
  expectedOperator: 'Jett-Reno',
  configuredControllerSha: smokeSha,
  windowStart: '2026-08-26T10:00:00Z',
  now: '2026-08-26T10:05:00Z',
  workflowRef: 'refs/heads/main',
  workflowSha: smokeSha,
  runId: '800',
  runAttempt: '1',
  delay: async () => {},
  ...overrides,
});
const labeledRun = (overrides = {}) => ({
  id: 800,
  event: 'pull_request_target',
  head_sha: smokeSha,
  head_branch: 'main',
  path: '.github/workflows/selective-ci-shadow.yml',
  head_repository: { full_name: 'Jamula/Andreja' },
  actor: { login: 'Jett-Reno' },
  pull_requests: [{ number: 118 }],
  run_attempt: 1,
  created_at: '2026-08-26T10:01:00Z',
  ...overrides,
});
const labelIssueEvent = (run, overrides = {}) => ({
  id: Number(run.id) + 10000,
  event: 'labeled',
  label: { name: 'ci:selective-shadow-sample' },
  actor: { login: 'Jett-Reno' },
  created_at: run.created_at,
  ...overrides,
});
function labelMetadataFetch(options = {}) {
  const runSnapshots = options.runSnapshots ?? [[[labeledRun()]], [[labeledRun()]]];
  let historyRead = 0;
  let activeSnapshotIndex = 0;
  return async (url) => {
    if (options.failure) return options.failure;
    if (url.includes('/actions/workflows/') && !url.includes('test_snapshot=')) {
      if (url.includes('created=')) {
        assert.match(
          url,
          /event=pull_request_target&created=%3E%3D2026-08-26T10%3A00%3A00Z&per_page=100/,
        );
      }
    }
    if (url.includes('/actions/workflows/')) {
      const snapshotMatch = url.match(/[?&]test_snapshot=(\d+)/);
      const pageMatch = url.match(/[?&]test_page=(\d+)/);
      const snapshotIndex = snapshotMatch ? Number(snapshotMatch[1]) : historyRead++;
      activeSnapshotIndex = snapshotIndex;
      const pageIndex = pageMatch ? Number(pageMatch[1]) : 0;
      const pages = runSnapshots[Math.min(snapshotIndex, runSnapshots.length - 1)];
      const next = pageIndex + 1 < pages.length
        ? `<https://api.github.com/actions/workflows/selective-ci-shadow.yml/runs?test_snapshot=${snapshotIndex}&test_page=${pageIndex + 1}>; rel="next"`
        : null;
      const totalCount = options.totalCounts?.[snapshotIndex] ??
        pages.reduce((sum, page) => sum + page.length, 0);
      return githubResponse(
        { total_count: totalCount, workflow_runs: pages[pageIndex] },
        next,
        options.rateLimitRemaining,
      );
    }
    const issueMatch =
      url.match(/\/issues\/(\d+)\/events/) ||
      url.match(/[?&]test_pull=(\d+)/);
    if (issueMatch) {
      const pullRequestNumber = Number(issueMatch[1]);
      const snapshotMatch = url.match(/[?&]test_snapshot=(\d+)/);
      const pageMatch = url.match(/[?&]test_page=(\d+)/);
      const snapshotIndex = snapshotMatch
        ? Number(snapshotMatch[1])
        : activeSnapshotIndex;
      const pageIndex = pageMatch ? Number(pageMatch[1]) : 0;
      const snapshotRuns = runSnapshots[
        Math.min(snapshotIndex, runSnapshots.length - 1)
      ].flat();
      const configured =
        options.issueEventSnapshots?.[
          Math.min(snapshotIndex, (options.issueEventSnapshots?.length ?? 1) - 1)
        ]?.[pullRequestNumber];
      const pages = configured ?? [
        snapshotRuns
          .filter((run) => run.pull_requests?.[0]?.number === pullRequestNumber)
          .map((run) => labelIssueEvent(run)),
      ];
      const next = pageIndex + 1 < pages.length
        ? `<https://api.github.com/label-events?test_pull=${pullRequestNumber}&test_snapshot=${snapshotIndex}&test_page=${pageIndex + 1}>; rel="next"`
        : null;
      return githubResponse(pages[pageIndex], next, options.rateLimitRemaining);
    }
    throw new Error(`Unexpected label metadata URL: ${url}`);
  };
}

function withAuthorizedLabelHistory(fallback, options = {}) {
  const historyFetch = labelMetadataFetch(options);
  return async (url) =>
    url.includes('/actions/workflows/') ||
    url.includes('/issues/') ||
    url.includes('/label-events?')
      ? historyFetch(url)
      : fallback(url);
}

function configureLabeledMainEnvironment() {
  process.env.GITHUB_ACTOR = 'Jett-Reno';
  process.env.GITHUB_REF = 'refs/heads/main';
  process.env.GITHUB_SHA = smokeSha;
  process.env.GITHUB_RUN_ID = '800';
  process.env.GITHUB_RUN_ATTEMPT = '1';
  process.env.SELECTIVE_CI_SAMPLE_OPERATOR = 'Jett-Reno';
  process.env.SELECTIVE_CI_CONTROLLER_SHA = smokeSha;
  process.env.SELECTIVE_CI_WINDOW_START = '2026-08-26T10:00:00Z';
}

const labelMainRuntime = {
  now: '2026-08-26T10:05:00Z',
  delay: async () => {},
};

test('first and second labeled workflow runs are authorized within N=2', async () => {
  for (const runs of [
    [labeledRun()],
    [labeledRun({ id: 799, pull_requests: [{ number: 117 }] }), labeledRun()],
  ]) {
    const budgetedFetch = createBudgetedFetch(labelMetadataFetch({
      runSnapshots: [[runs], [runs]],
    }));
    const authorization = await authorizeLabeledRun(
      labelEvent,
      'Jamula/Andreja',
      'token',
      budgetedFetch,
      labelRuntime({ requestObservation: budgetedFetch.observation }),
    );
    assert.equal(authorization.authorized, true, JSON.stringify(authorization));
    assert.match(authorization.reason, /^authorized-labeled-run-slot-[12]-of-2$/);
    assert.equal(authorization.labelRunCount, runs.length);
    assert.equal(authorization.authorizedRunCount, runs.length);
    assert.equal(authorization.historySnapshots.length, 2);
  }
});

test('every unrelated labeled run consumes the absolute N=2 cap', async () => {
  const unrelatedLabelRun = labeledRun({
    id: 797,
    pull_requests: [{ number: 115 }],
    created_at: '2026-08-26T10:00:10Z',
  });
  const unrelatedActorRun = labeledRun({
    id: 798,
    pull_requests: [{ number: 116 }],
    actor: { login: 'Other-Operator' },
    created_at: '2026-08-26T10:00:20Z',
  });
  const unrelatedControllerRun = labeledRun({
    id: 799,
    pull_requests: [{ number: 117 }],
    head_sha: 'e'.repeat(40),
    created_at: '2026-08-26T10:00:30Z',
  });
  const runs = [
    unrelatedLabelRun,
    unrelatedActorRun,
    unrelatedControllerRun,
    labeledRun(),
  ];
  const events = {
    115: [[labelIssueEvent(unrelatedLabelRun, { label: { name: 'triage' } })]],
    116: [[labelIssueEvent(unrelatedActorRun, {
      actor: { login: 'Other-Operator' },
    })]],
    117: [[labelIssueEvent(unrelatedControllerRun)]],
    118: [[labelIssueEvent(runs[3])]],
  };
  const budgetedFetch = createBudgetedFetch(labelMetadataFetch({
    runSnapshots: [[runs], [runs]],
    issueEventSnapshots: [events, events],
  }));
  const authorization = await authorizeLabeledRun(
    labelEvent,
    'Jamula/Andreja',
    'token',
    budgetedFetch,
    labelRuntime({ requestObservation: budgetedFetch.observation }),
  );
  assert.equal(authorization.authorized, false);
  assert.equal(authorization.reason, 'label-run-limit-exceeded');
  assert.equal(authorization.labelRunCount, 4);
  assert.equal(authorization.authorizedRunCount, null);
  assert.equal(authorization.authorizedSlot, null);
  assert.equal(authorization.historySnapshots.length, 1);
  assert.equal(budgetedFetch.observation().used, 1);
});

test('a repeated PR identity fails before issue-event expansion or domains', async () => {
  const runs = [
    labeledRun({ id: 799, pull_requests: [{ number: 118 }] }),
    labeledRun(),
  ];
  const budgetedFetch = createBudgetedFetch(labelMetadataFetch({
    runSnapshots: [[runs]],
  }));
  const authorization = await authorizeLabeledRun(
    labelEvent,
    'Jamula/Andreja',
    'token',
    budgetedFetch,
    labelRuntime({ requestObservation: budgetedFetch.observation }),
  );
  assert.equal(authorization.authorized, false);
  assert.equal(authorization.reason, 'label-run-pull-request-repeated');
  assert.equal(authorization.labelRunCount, 2);
  assert.equal(authorization.historySnapshots.length, 1);
  assert.equal(budgetedFetch.observation().used, 1);
});

test('a non-sample label is counted and emits lightweight unavailable evidence', async () => {
  const run = labeledRun();
  const event = {
    ...labelEvent,
    label: { name: 'triage' },
    pull_request: {
      ...labelEvent.pull_request,
      labels: [{ name: 'triage' }],
    },
  };
  const issueEvents = {
    118: [[labelIssueEvent(run, { label: { name: 'triage' } })]],
  };
  const budgetedFetch = createBudgetedFetch(labelMetadataFetch({
    runSnapshots: [[[run]], [[run]]],
    issueEventSnapshots: [issueEvents, issueEvents],
  }));
  const authorization = await authorizeLabeledRun(
    event,
    'Jamula/Andreja',
    'token',
    budgetedFetch,
    labelRuntime({ requestObservation: budgetedFetch.observation }),
  );
  assert.equal(authorization.authorized, false);
  assert.equal(authorization.reason, 'label-name-or-state-mismatch');
  assert.equal(authorization.labelRunCount, 1);
  assert.equal(authorization.authorizedRunCount, 0);
  assert.equal(authorization.historySnapshots.length, 2);
});

test('third labeled workflow run exceeds N=2 before domain classification', async () => {
  const runs = [
    labeledRun({ id: 798, pull_requests: [{ number: 116 }] }),
    labeledRun({ id: 799, pull_requests: [{ number: 117 }] }),
    labeledRun(),
  ];
  const budgetedFetch = createBudgetedFetch(labelMetadataFetch({
    runSnapshots: [[runs]],
  }));
  const authorization = await authorizeLabeledRun(
    labelEvent,
    'Jamula/Andreja',
    'token',
    budgetedFetch,
    labelRuntime({ requestObservation: budgetedFetch.observation }),
  );
  assert.equal(authorization.authorized, false);
  assert.equal(authorization.reason, 'label-run-limit-exceeded');
  assert.equal(authorization.labelRunCount, 3);
  assert.equal(authorization.authorizedRunCount, null);
  assert.equal(authorization.historySnapshots.length, 1);
  assert.equal(budgetedFetch.observation().used, 1);
});

test('third labeled workflow run emits unavailable classification without PR acquisition', async () => {
  const directory = path.join(root, 'artifacts/selective-ci');
  const eventPath = path.join(directory, `label-cap-event-${process.pid}.json`);
  const decisionPath = path.join(directory, `label-cap-decision-${process.pid}.json`);
  fs.mkdirSync(directory, { recursive: true });
  fs.writeFileSync(eventPath, JSON.stringify({
    ...labelEvent,
    number: 118,
    pull_request: {
      number: 118,
      changed_files: 1,
      base: { sha: 'a'.repeat(40) },
      head: { sha: 'b'.repeat(40) },
      merge_commit_sha: 'd'.repeat(40),
      labels: [{ name: 'ci:selective-shadow-sample' }],
    },
  }));
  const runs = [
    labeledRun({ id: 798, pull_requests: [{ number: 116 }] }),
    labeledRun({ id: 799, pull_requests: [{ number: 117 }] }),
    labeledRun(),
  ];
  const requestedUrls = [];
  const metadataFetch = labelMetadataFetch({ runSnapshots: [[runs]] });
  const originalEnvironment = { ...process.env };
  const originalExitCode = process.exitCode;
  process.env.GITHUB_EVENT_NAME = 'pull_request_target';
  process.env.GITHUB_EVENT_PATH = eventPath;
  process.env.GITHUB_REPOSITORY = 'Jamula/Andreja';
  process.env.GITHUB_TOKEN = 'read-only-token';
  process.env.CHANGE_POLICY_PATH = path.join(__dirname, 'change-policy.v1.json');
  process.env.CHANGE_DECISION_PATH = decisionPath;
  configureLabeledMainEnvironment();
  delete process.env.GITHUB_OUTPUT;
  try {
    await main(async (url) => {
      requestedUrls.push(url);
      return metadataFetch(url);
    }, labelMainRuntime);
    const decision = JSON.parse(fs.readFileSync(decisionPath, 'utf8'));
    assert.equal(decision.classificationFailure, 'labeled-run-window-unavailable');
    assert.equal(decision.trustedClassifierAvailable, false);
    assert.equal(decision.labelRunAuthorization.reason, 'label-run-limit-exceeded');
    assert.equal(decision.labelRunAuthorization.labelRunCount, 3);
    assert.equal(decision.mergeCommitProof, null);
    assert.equal(decision.changedFileCount, 0);
    assert.equal(requestedUrls.length, 1);
    assert.ok(requestedUrls.some((url) => url.includes('/actions/workflows/')));
    assert.ok(requestedUrls.every((url) => url.includes('/actions/workflows/')));
    assert.ok(requestedUrls.every((url) => !url.includes('/pulls/')));
    assert.equal(process.exitCode, 1);
  } finally {
    process.env = originalEnvironment;
    process.exitCode = originalExitCode;
    fs.rmSync(eventPath, { force: true });
    fs.rmSync(decisionPath, { force: true });
  }
});

test('labeled workflow run history is complete, paginated, unique, and stable', async () => {
  const pages = [
    [labeledRun({ id: 799, pull_requests: [{ number: 117 }] })],
    [labeledRun()],
  ];
  const paginatedFetch = createBudgetedFetch(labelMetadataFetch({
    runSnapshots: [pages, pages],
  }));
  const paginated = await authorizeLabeledRun(
    labelEvent,
    'Jamula/Andreja',
    'token',
    paginatedFetch,
    labelRuntime({ requestObservation: paginatedFetch.observation }),
  );
  assert.equal(paginated.authorized, true, JSON.stringify(paginated));
  assert.equal(paginated.labelRunCount, 2);
  assert.deepEqual(
    paginated.historySnapshots.map((snapshot) => snapshot.pageCount),
    [2, 2],
  );

  const racedFetch = createBudgetedFetch(labelMetadataFetch({
    runSnapshots: [
      [[labeledRun()]],
      [[labeledRun({ id: 799, pull_requests: [{ number: 117 }] }), labeledRun()]],
    ],
  }));
  const raced = await authorizeLabeledRun(
    labelEvent,
    'Jamula/Andreja',
    'token',
    racedFetch,
    labelRuntime({ requestObservation: racedFetch.observation }),
  );
  assert.equal(raced.authorized, false);
  assert.equal(raced.reason, 'label-run-history-unstable');
  assert.equal(raced.labelRunCount, 2);

  const repeatedPullRuns = [
    labeledRun({ id: 799, created_at: '2026-08-26T10:00:30Z' }),
    labeledRun(),
  ];
  const repeatedPullFetch = createBudgetedFetch(labelMetadataFetch({
    runSnapshots: [[repeatedPullRuns], [repeatedPullRuns]],
  }));
  const repeatedPull = await authorizeLabeledRun(
    labelEvent,
    'Jamula/Andreja',
    'token',
    repeatedPullFetch,
    labelRuntime({ requestObservation: repeatedPullFetch.observation }),
  );
  assert.equal(repeatedPull.authorized, false);
  assert.equal(repeatedPull.reason, 'label-run-pull-request-repeated');
  assert.equal(repeatedPull.labelRunCount, 2);
  assert.equal(repeatedPull.historySnapshots.length, 1);

  const duplicateFetch = createBudgetedFetch(labelMetadataFetch({
    runSnapshots: [[[labeledRun(), labeledRun()]]],
  }));
  const duplicate = await authorizeLabeledRun(
    labelEvent,
    'Jamula/Andreja',
    'token',
    duplicateFetch,
    labelRuntime({ requestObservation: duplicateFetch.observation }),
  );
  assert.equal(duplicate.authorized, false);
  assert.equal(duplicate.reason, 'label-metadata-unavailable');
  assert.match(duplicate.error, /duplicate run ID/);
});

test('labeled workflow guard fails closed on index lag, API failure, and rate limits', async () => {
  const invisibleFetch = createBudgetedFetch(labelMetadataFetch({
    runSnapshots: [[[labeledRun({ id: 799 })]]],
  }));
  const invisible = await authorizeLabeledRun(
    labelEvent,
    'Jamula/Andreja',
    'token',
    invisibleFetch,
    labelRuntime({ requestObservation: invisibleFetch.observation }),
  );
  assert.equal(invisible.authorized, false);
  assert.equal(invisible.reason, 'label-current-run-not-visible');
  assert.equal(invisible.labelRunCount, 1);

  for (const fixture of [
    { status: 500, statusText: 'Internal Server Error', remaining: null },
    { status: 429, statusText: 'Too Many Requests', remaining: '0' },
  ]) {
    const budgetedFetch = createBudgetedFetch(labelMetadataFetch({
      failure: {
        ok: false,
        status: fixture.status,
        statusText: fixture.statusText,
        headers: {
          get: (name) => name === 'x-ratelimit-remaining' ? fixture.remaining : null,
        },
        json: async () => ({}),
      },
    }));
    const authorization = await authorizeLabeledRun(
      labelEvent,
      'Jamula/Andreja',
      'token',
      budgetedFetch,
      labelRuntime({ requestObservation: budgetedFetch.observation }),
    );
    assert.equal(authorization.authorized, false);
    assert.equal(authorization.reason, 'label-metadata-unavailable');
    assert.match(authorization.error, new RegExp(String(fixture.status)));
  }
});

test('labeled workflow guard requires all variables, exact operator, and controller ownership', async () => {
  for (const fixture of [
    { runtime: { expectedOperator: '' }, reason: 'label-operator-unconfigured' },
    { runtime: { configuredControllerSha: '' }, reason: 'label-controller-sha-unconfigured-or-invalid' },
    { runtime: { windowStart: '' }, reason: 'label-window-start-unconfigured' },
    { runtime: { actor: 'jett-reno' }, reason: 'label-operator-mismatch' },
    {
      event: { ...labelEvent, label: { name: 'triage' } },
      reason: 'label-name-or-state-mismatch',
    },
    {
      event: {
        ...labelEvent,
        pull_request: { ...labelEvent.pull_request, labels: [] },
      },
      reason: 'label-name-or-state-mismatch',
    },
    { runtime: { workflowRef: 'refs/tags/v1' }, reason: 'label-workflow-ref-mismatch' },
    { runtime: { workflowSha: 'e'.repeat(40) }, reason: 'label-controller-sha-stale' },
    { runtime: { runAttempt: '2' }, reason: 'label-rerun-forbidden' },
  ]) {
    const budgetedFetch = createBudgetedFetch(labelMetadataFetch());
    const authorization = await authorizeLabeledRun(
      fixture.event ?? labelEvent,
      'Jamula/Andreja',
      'token',
      budgetedFetch,
      labelRuntime({
        ...fixture.runtime,
        requestObservation: budgetedFetch.observation,
      }),
    );
    assert.equal(authorization.authorized, false);
    assert.equal(authorization.reason, fixture.reason);
  }
});

test('labeled run binds current PR, workflow, actor, label event, and first attempt', async () => {
  const fixtures = [
    {
      runs: [labeledRun({ pull_requests: [{ number: 117 }] })],
      reason: 'label-run-pull-request-identity-unavailable',
    },
    {
      runs: [labeledRun({ path: '.github/workflows/other.yml' })],
      reason: 'label-current-run-identity-mismatch',
    },
    {
      runs: [labeledRun({ run_attempt: 2 })],
      reason: 'label-current-run-identity-mismatch',
    },
    {
      runs: [labeledRun()],
      events: {
        118: [[labelIssueEvent(labeledRun(), { label: { name: 'triage' } })]],
      },
      reason: 'label-run-pull-request-identity-unavailable',
    },
    {
      runs: [labeledRun()],
      events: {
        118: [[labelIssueEvent(labeledRun(), {
          actor: { login: 'Other-Operator' },
        })]],
      },
      reason: 'label-run-pull-request-identity-unavailable',
    },
  ];
  for (const fixture of fixtures) {
    const options = {
      runSnapshots: [[fixture.runs]],
      ...(fixture.events ? { issueEventSnapshots: [fixture.events] } : {}),
    };
    const budgetedFetch = createBudgetedFetch(labelMetadataFetch(options));
    const authorization = await authorizeLabeledRun(
      labelEvent,
      'Jamula/Andreja',
      'token',
      budgetedFetch,
      labelRuntime({ requestObservation: budgetedFetch.observation }),
    );
    assert.equal(authorization.authorized, false);
    assert.equal(authorization.reason, fixture.reason);
  }

  const priorRerun = labeledRun({
    id: 799,
    run_attempt: 2,
    pull_requests: [{ number: 117 }],
    created_at: '2026-08-26T10:00:30Z',
  });
  const runs = [priorRerun, labeledRun()];
  const rerunFetch = createBudgetedFetch(labelMetadataFetch({
    runSnapshots: [[runs]],
  }));
  const rerun = await authorizeLabeledRun(
    labelEvent,
    'Jamula/Andreja',
    'token',
    rerunFetch,
    labelRuntime({ requestObservation: rerunFetch.observation }),
  );
  assert.equal(rerun.authorized, false);
  assert.equal(rerun.reason, 'label-authorized-run-rerun-forbidden');
});

test('labeled run fails closed at Actions and issue-event pagination caps', async () => {
  const actionsCapFetch = createBudgetedFetch(labelMetadataFetch({
    totalCounts: [1000],
  }));
  const actionsCap = await authorizeLabeledRun(
    labelEvent,
    'Jamula/Andreja',
    'token',
    actionsCapFetch,
    labelRuntime({ requestObservation: actionsCapFetch.observation }),
  );
  assert.equal(actionsCap.authorized, false);
  assert.equal(actionsCap.reason, 'label-metadata-unavailable');
  assert.match(actionsCap.error, /1,000-run API cap/);

  const eventPages = Array.from({ length: 101 }, (_, index) =>
    index === 100 ? [labelIssueEvent(labeledRun())] : []);
  const issueCapFetch = createBudgetedFetch(labelMetadataFetch({
    runSnapshots: [[[labeledRun()]]],
    issueEventSnapshots: [{ 118: eventPages }],
  }));
  const issueCap = await authorizeLabeledRun(
    labelEvent,
    'Jamula/Andreja',
    'token',
    issueCapFetch,
    labelRuntime({ requestObservation: issueCapFetch.observation }),
  );
  assert.equal(issueCap.authorized, false);
  assert.equal(issueCap.reason, 'label-metadata-unavailable');
  assert.match(issueCap.error, /Issue event pagination exceeded 100 pages/);
});

function smokeMetadataFetch(options = {}) {
  const runSnapshots = options.runSnapshots ?? [[[smokeRun()]], [[smokeRun()]]];
  const defaultBranches = options.defaultBranches ?? ['main', 'main'];
  const branchShas = options.branchShas ?? [smokeSha, smokeSha];
  let repositoryRead = 0;
  let branchRead = 0;
  let historyRead = 0;
  return async (url) => {
    if (/\/repos\/Jamula\/Andreja$/.test(url)) {
      const value = defaultBranches[Math.min(repositoryRead, defaultBranches.length - 1)];
      repositoryRead += 1;
      return githubResponse({ default_branch: value }, null, options.rateLimitRemaining);
    }
    if (url.includes('/branches/main')) {
      const value = branchShas[Math.min(branchRead, branchShas.length - 1)];
      branchRead += 1;
      return githubResponse({ commit: { sha: value } }, null, options.rateLimitRemaining);
    }
    if (options.failure) return options.failure;
    if (url.includes('/actions/workflows/')) {
      assert.match(
        url,
        /event=repository_dispatch&created=%3E%3D2026-08-26T10%3A00%3A00Z&per_page=100/,
      );
    }
    const snapshotMatch = url.match(/[?&]test_snapshot=(\d+)/);
    const pageMatch = url.match(/[?&]test_page=(\d+)/);
    const snapshotIndex = snapshotMatch ? Number(snapshotMatch[1]) : historyRead++;
    const pageIndex = pageMatch ? Number(pageMatch[1]) : 0;
    const pages = runSnapshots[Math.min(snapshotIndex, runSnapshots.length - 1)];
    const next = pageIndex + 1 < pages.length
      ? `<https://api.github.com/smoke-runs?test_snapshot=${snapshotIndex}&test_page=${pageIndex + 1}>; rel="next"`
      : null;
    const totalCount = options.totalCounts?.[snapshotIndex] ??
      pages.reduce((sum, page) => sum + page.length, 0);
    return githubResponse(
      { total_count: totalCount, workflow_runs: pages[pageIndex] },
      next,
      options.rateLimitRemaining,
    );
  };
}

test('first repository dispatch smoke is default-branch owned and authorized', async () => {
  const budgetedFetch = createBudgetedFetch(smokeMetadataFetch());
  const runtime = smokeRuntime({
    requestObservation: budgetedFetch.observation,
  });
  const authorization = await authorizeRepositoryDispatchSmoke(
    smokeEvent,
    'Jamula/Andreja',
    'token',
    budgetedFetch,
    runtime,
  );
  assert.equal(authorization.authorized, true);
  assert.equal(authorization.reason, 'authorized-first-repository-dispatch-smoke');
  assert.equal(authorization.liveDefaultBranchSha, smokeSha);
  assert.equal(authorization.historySnapshots.length, 2);
  assert.deepEqual(
    authorization.historySnapshots.map((snapshot) => snapshot.currentRevisionCount),
    [1, 1],
  );
  assert.equal(authorization.requestBudgetAtAuthorization.used, 6);
});

test('repository dispatch smoke rejects wrong event, operator, controller, and rerun', async () => {
  for (const fixture of [
    { event: { ...smokeEvent, action: 'wrong-smoke' }, reason: 'smoke-event-type-mismatch' },
    { event: { ...smokeEvent, sender: undefined }, reason: 'smoke-operator-missing' },
    { event: { ...smokeEvent, sender: { login: 'jett-reno' } }, reason: 'smoke-operator-mismatch' },
    { runtime: { actor: 'jett-reno' }, reason: 'smoke-operator-mismatch' },
    { runtime: { expectedOperator: '' }, reason: 'smoke-operator-unconfigured' },
    { runtime: { configuredControllerSha: '' }, reason: 'smoke-controller-sha-unconfigured-or-invalid' },
    {
      runtime: { configuredControllerSha: 'not-a-sha' },
      reason: 'smoke-controller-sha-unconfigured-or-invalid',
    },
    { runtime: { windowStart: '' }, reason: 'smoke-window-start-unconfigured' },
    {
      runtime: { windowStart: 'not-a-timestamp' },
      reason: 'smoke-window-start-invalid',
    },
    {
      runtime: { windowStart: '2026-02-30T10:00:00Z' },
      reason: 'smoke-window-start-invalid',
    },
    {
      runtime: { windowStart: '2026-08-26T10:06:00Z' },
      reason: 'smoke-window-start-invalid',
    },
    { runtime: { runAttempt: '2' }, reason: 'smoke-rerun-forbidden' },
  ]) {
    const budgetedFetch = createBudgetedFetch(smokeMetadataFetch());
    const authorization = await authorizeRepositoryDispatchSmoke(
      fixture.event ?? smokeEvent,
      'Jamula/Andreja',
      'token',
      budgetedFetch,
      smokeRuntime({
        ...fixture.runtime,
        requestObservation: budgetedFetch.observation,
      }),
    );
    assert.equal(authorization.authorized, false);
    assert.equal(authorization.reason, fixture.reason);
  }
});

test('repository dispatch smoke rejects branch or tag refs and stale or non-main SHAs', async () => {
  for (const fixture of [
    { runtime: { workflowRef: 'refs/heads/feature' }, reason: 'smoke-workflow-ref-mismatch' },
    { runtime: { workflowRef: 'refs/tags/v1' }, reason: 'smoke-workflow-ref-mismatch' },
    { runtime: { workflowSha: 'e'.repeat(40) }, reason: 'smoke-workflow-sha-stale' },
    {
      runtime: { configuredControllerSha: 'e'.repeat(40) },
      reason: 'smoke-expected-controller-sha-stale',
    },
    {
      fetchOptions: { defaultBranches: ['develop'] },
      reason: 'smoke-default-branch-mismatch',
    },
    { runtime: { runId: '' }, reason: 'smoke-run-id-invalid' },
  ]) {
    const budgetedFetch = createBudgetedFetch(smokeMetadataFetch(fixture.fetchOptions));
    const authorization = await authorizeRepositoryDispatchSmoke(
      fixture.event ?? smokeEvent,
      'Jamula/Andreja',
      'token',
      budgetedFetch,
      smokeRuntime({
        ...fixture.runtime,
        requestObservation: budgetedFetch.observation,
      }),
    );
    assert.equal(authorization.authorized, false);
    assert.equal(authorization.reason, fixture.reason);
  }
});

test('repository dispatch smoke pagination enforces the S=1 run limit', async () => {
  const paginatedFetch = createBudgetedFetch(smokeMetadataFetch({
    runSnapshots: [
      [[], [smokeRun()]],
      [[], [smokeRun()]],
    ],
  }));
  const paginated = await authorizeRepositoryDispatchSmoke(
    smokeEvent,
    'Jamula/Andreja',
    'token',
    paginatedFetch,
    smokeRuntime({ requestObservation: paginatedFetch.observation }),
  );
  assert.equal(paginated.authorized, true);
  assert.equal(paginated.historySnapshots[1].pageCount, 2);

  const repeatedFetch = createBudgetedFetch(smokeMetadataFetch({
    runSnapshots: [
      [[smokeRun({ id: 699 })], [smokeRun()]],
    ],
  }));
  const repeated = await authorizeRepositoryDispatchSmoke(
    smokeEvent,
    'Jamula/Andreja',
    'token',
    repeatedFetch,
    smokeRuntime({ requestObservation: repeatedFetch.observation }),
  );
  assert.equal(repeated.authorized, false);
  assert.equal(repeated.reason, 'smoke-run-limit-exceeded');
  assert.equal(repeated.historySnapshots[0].currentRevisionCount, 2);
  assert.equal(repeated.historySnapshots[0].totalCount, 2);

  const previousControllerFetch = createBudgetedFetch(smokeMetadataFetch({
    runSnapshots: [
      [[smokeRun({ id: 699, head_sha: 'e'.repeat(40) }), smokeRun()]],
      [[smokeRun({ id: 699, head_sha: 'e'.repeat(40) }), smokeRun()]],
    ],
  }));
  const previousController = await authorizeRepositoryDispatchSmoke(
    smokeEvent,
    'Jamula/Andreja',
    'token',
    previousControllerFetch,
    smokeRuntime({ requestObservation: previousControllerFetch.observation }),
  );
  assert.equal(previousController.authorized, false);
  assert.equal(previousController.reason, 'smoke-run-limit-exceeded');
  assert.equal(previousController.historySnapshots[0].totalCount, 2);
  assert.equal(previousController.historySnapshots[0].currentRevisionCount, 1);

  const racedFetch = createBudgetedFetch(smokeMetadataFetch({
    runSnapshots: [
      [[smokeRun()]],
      [[smokeRun(), smokeRun({ id: 701 })]],
    ],
  }));
  const raced = await authorizeRepositoryDispatchSmoke(
    smokeEvent,
    'Jamula/Andreja',
    'token',
    racedFetch,
    smokeRuntime({ requestObservation: racedFetch.observation }),
  );
  assert.equal(raced.authorized, false);
  assert.equal(raced.reason, 'smoke-run-limit-exceeded');
});

test('repository dispatch smoke fails closed on live-main race and Actions API truncation', async () => {
  const mainRaceFetch = createBudgetedFetch(smokeMetadataFetch({
    branchShas: [smokeSha, 'e'.repeat(40)],
  }));
  const mainRace = await authorizeRepositoryDispatchSmoke(
    smokeEvent,
    'Jamula/Andreja',
    'token',
    mainRaceFetch,
    smokeRuntime({ requestObservation: mainRaceFetch.observation }),
  );
  assert.equal(mainRace.reason, 'smoke-live-default-raced');

  const truncatedFetch = createBudgetedFetch(smokeMetadataFetch({
    totalCounts: [2],
  }));
  const truncated = await authorizeRepositoryDispatchSmoke(
    smokeEvent,
    'Jamula/Andreja',
    'token',
    truncatedFetch,
    smokeRuntime({ requestObservation: truncatedFetch.observation }),
  );
  assert.equal(truncated.authorized, false);
  assert.equal(truncated.reason, 'smoke-metadata-unavailable');
  assert.match(truncated.error, /pagination was incomplete/);
});

test('repository dispatch smoke API and rate-limit ambiguity fail unavailable', async () => {
  for (const fixture of [
    { status: 500, statusText: 'Internal Server Error', remaining: null },
    { status: 429, statusText: 'Too Many Requests', remaining: '0' },
  ]) {
    const budgetedFetch = createBudgetedFetch(async () => ({
      ok: false,
      status: fixture.status,
      statusText: fixture.statusText,
      headers: {
        get: (name) => name === 'x-ratelimit-remaining' ? fixture.remaining : null,
      },
      json: async () => ({}),
    }));
    const changes = await acquireChanges(
      'repository_dispatch',
      smokeEvent,
      'Jamula/Andreja',
      'token',
      budgetedFetch,
      smokeRuntime({
        requestObservation: budgetedFetch.observation,
      }),
    );
    assert.equal(changes.smokeAuthorization.authorized, false);
    assert.equal(changes.smokeAuthorization.reason, 'smoke-metadata-unavailable');
    assert.equal(
      changes.classificationFailure,
      fixture.status === 429
        ? 'github-metadata-rate-limit-or-budget'
        : 'repository-dispatch-smoke-unavailable',
    );
  }

  const ambiguousFetch = createBudgetedFetch(smokeMetadataFetch({
    rateLimitRemaining: null,
  }));
  const ambiguous = await authorizeRepositoryDispatchSmoke(
    smokeEvent,
    'Jamula/Andreja',
    'token',
    ambiguousFetch,
    smokeRuntime({ requestObservation: ambiguousFetch.observation }),
  );
  assert.equal(ambiguous.authorized, false);
  assert.equal(ambiguous.reason, 'smoke-rate-limit-observation-unavailable');

  const ambiguousChangesFetch = createBudgetedFetch(smokeMetadataFetch({
    rateLimitRemaining: null,
  }));
  const ambiguousChanges = await acquireChanges(
    'repository_dispatch',
    smokeEvent,
    'Jamula/Andreja',
    'token',
    ambiguousChangesFetch,
    smokeRuntime({ requestObservation: ambiguousChangesFetch.observation }),
  );
  assert.equal(
    ambiguousChanges.classificationFailure,
    'github-metadata-rate-limit-or-budget',
  );

  const exhaustedFetch = createBudgetedFetch(smokeMetadataFetch(), 1);
  const exhausted = await authorizeRepositoryDispatchSmoke(
    smokeEvent,
    'Jamula/Andreja',
    'token',
    exhaustedFetch,
    smokeRuntime({ requestObservation: exhaustedFetch.observation }),
  );
  assert.equal(exhausted.authorized, false);
  assert.equal(exhausted.reason, 'smoke-metadata-unavailable');
  assert.match(exhausted.error, /request budget exceeded/);
  assert.equal(exhaustedFetch.observation().exhausted, true);
});

test('PR target metadata without an immutable merge commit fails closed', async () => {
  const file = { filename: 'docs/a.md', status: 'added', changes: 1, patch: '@@ -0,0 +1 @@\n+prose' };
  const fetchImpl = async (url) => ({
    ok: true,
    status: 200,
    statusText: 'OK',
    headers: { get: () => null },
    json: async () =>
      url.includes('/compare/')
        ? { merge_base_commit: { sha: 'c'.repeat(40) }, files: [file] }
        : [file],
  });
  const changes = await acquireChanges(
    'pull_request_target',
    {
      number: 118,
      pull_request: {
        changed_files: 1,
        base: { sha: 'a'.repeat(40) },
        head: { sha: 'b'.repeat(40) },
      },
    },
    'Jamula/Andreja',
    'read-only-token',
    fetchImpl,
  );
  assert.ok(
    changes.forcedFullReasons.includes('pull-request-merge-commit-sha-unavailable'),
  );
  assert.equal(
    classifyFiles(changes.files, policy, {
      forcedFullReasons: changes.forcedFullReasons,
    }).fullSuite,
    true,
  );
});

test('sample label rejects a null live test-merge SHA before file acquisition', async () => {
  const calls = [];
  const event = {
    number: 118,
    action: 'labeled',
    label: { name: 'ci:selective-shadow-sample' },
    sender: { login: 'Jett-Reno' },
    pull_request: {
      number: 118,
      changed_files: 1,
      base: { sha: 'a'.repeat(40) },
      head: { sha: 'b'.repeat(40) },
      merge_commit_sha: null,
      labels: [{ name: 'ci:selective-shadow-sample' }],
    },
  };
  const changes = await acquireChanges(
    'pull_request_target',
    event,
    'Jamula/Andreja',
    'token',
    async (url) => {
      calls.push(url);
      return githubResponse({
        mergeable: null,
        merge_commit_sha: null,
        base: { sha: 'a'.repeat(40) },
        head: { sha: 'b'.repeat(40) },
      });
    },
  );
  assert.equal(changes.classificationFailure, 'pull-request-merge-integrity-unavailable');
  assert.equal(changes.mergeCommitProof.verified, false);
  assert.equal(changes.mergeCommitProof.reason, 'pull-request-merge-identity-invalid');
  assert.equal(calls.length, 1);
  assert.match(calls[0], /\/pulls\/118$/);
});

test('sample label rejects stale live test-merge metadata', async () => {
  const event = {
    number: 118,
    action: 'labeled',
    label: { name: 'ci:selective-shadow-sample' },
    pull_request: {
      number: 118,
      changed_files: 1,
      base: { sha: 'a'.repeat(40) },
      head: { sha: 'b'.repeat(40) },
      merge_commit_sha: 'd'.repeat(40),
      labels: [{ name: 'ci:selective-shadow-sample' }],
    },
  };
  const changes = await acquireChanges(
    'pull_request_target',
    event,
    'Jamula/Andreja',
    'token',
    async (url) =>
      url.includes('/git/commits/')
        ? githubResponse({
            sha: 'e'.repeat(40),
            parents: [{ sha: 'a'.repeat(40) }, { sha: 'b'.repeat(40) }],
          })
        : githubResponse({
            mergeable: true,
            merge_commit_sha: 'e'.repeat(40),
            base: { sha: 'a'.repeat(40) },
            head: { sha: 'b'.repeat(40) },
          }),
  );
  assert.equal(changes.classificationFailure, 'pull-request-merge-integrity-unavailable');
  assert.equal(changes.mergeCommitProof.reason, 'pull-request-test-merge-stale');
});

test('sample label validates current test-merge parents within the API budget', async () => {
  const base = 'a'.repeat(40);
  const head = 'b'.repeat(40);
  const merge = 'd'.repeat(40);
  const file = {
    filename: 'docs/current.md',
    status: 'added',
    changes: 1,
    patch: '@@ -0,0 +1 @@\n+prose',
  };
  const event = {
    number: 118,
    action: 'labeled',
    label: { name: 'ci:selective-shadow-sample' },
    pull_request: {
      number: 118,
      changed_files: 1,
      base: { sha: base },
      head: { sha: head },
      merge_commit_sha: merge,
      labels: [{ name: 'ci:selective-shadow-sample' }],
    },
  };
  const budgetedFetch = createBudgetedFetch(async (url) => {
    if (url.endsWith('/pulls/118')) {
      return githubResponse({
        mergeable: true,
        merge_commit_sha: merge,
        base: { sha: base },
        head: { sha: head },
      });
    }
    if (url.includes('/git/commits/')) {
      return githubResponse({
        sha: merge,
        parents: [{ sha: base }, { sha: head }],
      });
    }
    if (url.includes('/compare/')) {
      return githubResponse({
        merge_base_commit: { sha: 'c'.repeat(40) },
        files: [file],
      });
    }
    return githubResponse([file]);
  });
  const changes = await acquireChanges(
    'pull_request_target',
    event,
    'Jamula/Andreja',
    'token',
    budgetedFetch,
  );
  assert.deepEqual(changes.forcedFullReasons, []);
  assert.equal(changes.classificationFailure, null);
  assert.equal(changes.mergeCommitProof.verified, true);
  assert.deepEqual(changes.mergeCommitProof.parentShas, [base, head]);
  assert.equal(budgetedFetch.observation().used, 5);
  assert.equal(budgetedFetch.observation().limit, 132);
});

test('sample label rejects missing or extra test-merge parents before file acquisition', async () => {
  const base = 'a'.repeat(40);
  const head = 'b'.repeat(40);
  const merge = 'd'.repeat(40);
  const event = {
    number: 118,
    action: 'labeled',
    label: { name: 'ci:selective-shadow-sample' },
    pull_request: {
      number: 118,
      changed_files: 1,
      base: { sha: base },
      head: { sha: head },
      merge_commit_sha: merge,
      labels: [{ name: 'ci:selective-shadow-sample' }],
    },
  };
  for (const parents of [
    [{ sha: base }],
    [{ sha: base }, { sha: head }, { sha: 'c'.repeat(40) }],
  ]) {
    const requestedUrls = [];
    const changes = await acquireChanges(
      'pull_request_target',
      event,
      'Jamula/Andreja',
      'token',
      async (url) => {
        requestedUrls.push(url);
        return url.includes('/git/commits/')
          ? githubResponse({ sha: merge, parents })
          : githubResponse({
              mergeable: true,
              merge_commit_sha: merge,
              base: { sha: base },
              head: { sha: head },
            });
      },
    );
    assert.equal(changes.classificationFailure, 'pull-request-merge-integrity-unavailable');
    assert.equal(changes.mergeCommitProof.reason, 'pull-request-test-merge-commit-invalid');
    assert.equal(requestedUrls.length, 2);
    assert.equal(requestedUrls.some((url) => /\/files|\/compare\//.test(url)), false);
  }
});

test('sample label rejects reordered test-merge parents', async () => {
  const base = 'a'.repeat(40);
  const head = 'b'.repeat(40);
  const merge = 'd'.repeat(40);
  const event = {
    number: 118,
    action: 'labeled',
    label: { name: 'ci:selective-shadow-sample' },
    pull_request: {
      number: 118,
      changed_files: 1,
      base: { sha: base },
      head: { sha: head },
      merge_commit_sha: merge,
      labels: [{ name: 'ci:selective-shadow-sample' }],
    },
  };
  const changes = await acquireChanges(
    'pull_request_target',
    event,
    'Jamula/Andreja',
    'token',
    async (url) =>
      url.includes('/git/commits/')
        ? githubResponse({
            sha: merge,
            parents: [{ sha: head }, { sha: base }],
          })
        : githubResponse({
            mergeable: true,
            merge_commit_sha: merge,
            base: { sha: base },
            head: { sha: head },
          }),
  );
  assert.equal(changes.classificationFailure, 'pull-request-merge-integrity-unavailable');
  assert.equal(
    changes.mergeCommitProof.reason,
    'pull-request-test-merge-parent-mismatch',
  );
});

test('sample label detects a base/head race after file acquisition', async () => {
  const base = 'a'.repeat(40);
  const head = 'b'.repeat(40);
  const merge = 'd'.repeat(40);
  const racedHead = 'e'.repeat(40);
  const racedMerge = 'f'.repeat(40);
  const file = { filename: 'src/App.cs', status: 'modified', changes: 1 };
  let pullReads = 0;
  const event = {
    number: 118,
    action: 'labeled',
    label: { name: 'ci:selective-shadow-sample' },
    pull_request: {
      number: 118,
      changed_files: 1,
      base: { sha: base },
      head: { sha: head },
      merge_commit_sha: merge,
      labels: [{ name: 'ci:selective-shadow-sample' }],
    },
  };
  const changes = await acquireChanges(
    'pull_request_target',
    event,
    'Jamula/Andreja',
    'token',
    async (url) => {
      if (url.endsWith('/pulls/118')) {
        pullReads += 1;
        return githubResponse({
          mergeable: true,
          merge_commit_sha: pullReads === 1 ? merge : racedMerge,
          base: { sha: base },
          head: { sha: pullReads === 1 ? head : racedHead },
        });
      }
      if (url.includes('/git/commits/')) {
        return githubResponse({
          sha: merge,
          parents: [{ sha: base }, { sha: head }],
        });
      }
      if (url.includes('/compare/')) {
        return githubResponse({
          merge_base_commit: { sha: 'c'.repeat(40) },
          files: [file],
        });
      }
      return githubResponse([file]);
    },
  );
  assert.equal(changes.classificationFailure, 'pull-request-merge-integrity-unavailable');
  assert.equal(changes.mergeCommitProof.reason, 'pull-request-test-merge-stale');
  assert.equal(pullReads, 2);
});

test('PR metadata snapshot races fail closed against the event head', async () => {
  const fetchImpl = async (url) => ({
    ok: true,
    status: 200,
    statusText: 'OK',
    headers: { get: () => null },
    json: async () =>
      url.includes('/compare/')
        ? {
            merge_base_commit: { sha: 'c'.repeat(40) },
            files: [{ filename: 'src/EventHead.cs', status: 'added', changes: 1 }],
          }
        : [{ filename: 'docs/NewHead.md', status: 'added', changes: 1 }],
  });
  const changes = await acquireChanges(
    'pull_request_target',
    {
      number: 118,
      pull_request: {
        changed_files: 1,
        base: { sha: 'a'.repeat(40) },
        head: { sha: 'b'.repeat(40) },
      },
    },
    'Jamula/Andreja',
    'read-only-token',
    fetchImpl,
  );
  assert.equal(changes.files[0].filename, 'src/EventHead.cs');
  assert.ok(changes.forcedFullReasons.includes('pull-request-file-snapshot-mismatch'));
  assert.equal(
    classifyFiles(changes.files, policy, {
      forcedFullReasons: changes.forcedFullReasons,
    }).fullSuite,
    true,
  );
});

test('PR metadata count mismatch fails closed on silent API truncation', async () => {
  const fetchImpl = async (url) => ({
    ok: true,
    status: 200,
    statusText: 'OK',
    headers: { get: () => null },
    json: async () =>
      url.includes('/compare/')
        ? { merge_base_commit: { sha: 'c'.repeat(40) } }
        : [{ filename: 'docs/visible.md', status: 'modified', patch: '@@\n-a\n+b' }],
  });
  const changes = await acquireChanges(
    'pull_request',
    {
      number: 102,
      pull_request: {
        changed_files: 3001,
        base: { sha: 'a'.repeat(40) },
        head: { sha: 'b'.repeat(40) },
      },
    },
    'Jamula/Andreja',
    'read-only-token',
    fetchImpl,
  );
  const result = classifyFiles(changes.files, policy, {
    forcedFullReasons: changes.forcedFullReasons,
  });
  assert.equal(result.fullSuite, true);
  assert.ok(result.fullReasons.includes('pull-request-file-count-mismatch'));
});

test('trusted Markdown base is loaded from the merge-base contents API', async () => {
  const calls = [];
  const fetchImpl = async (url) => {
    calls.push(url);
    return {
      ok: true,
      status: 200,
      statusText: 'OK',
      headers: { get: () => null },
      json: async () => ({
        type: 'file',
        encoding: 'base64',
        content: Buffer.from('trusted base\n').toString('base64'),
      }),
    };
  };
  const files = await attachTrustedMarkdownBase(
    [{ filename: 'docs/help/example file.md', status: 'modified' }],
    'Jamula/Andreja',
    'c'.repeat(40),
    'token',
    fetchImpl,
  );
  assert.equal(files[0].baseContent, 'trusted base\n');
  assert.match(calls[0], /docs\/help\/example%20file\.md\?ref=c{40}$/);
});

test('trusted Markdown Contents requests are bounded to eight concurrent fetches', async () => {
  let active = 0;
  let maximumActive = 0;
  let calls = 0;
  const files = Array.from({ length: 25 }, (_, index) => ({
    filename: `docs/concurrency-${index}.md`,
    status: 'modified',
  }));
  const loaded = await attachTrustedMarkdownBase(
    files,
    'Jamula/Andreja',
    'c'.repeat(40),
    'token',
    async () => {
      calls += 1;
      active += 1;
      maximumActive = Math.max(maximumActive, active);
      await new Promise((resolve) => setTimeout(resolve, 2));
      active -= 1;
      return githubResponse({
        type: 'file',
        encoding: 'base64',
        content: Buffer.from('trusted base\n').toString('base64'),
      });
    },
  );
  assert.equal(calls, 25);
  assert.equal(maximumActive, 8);
  assert.ok(loaded.every((file) => file.baseContent === 'trusted base\n'));
});

test('Markdown fetch workers settle and stop scheduling after a rate-limit failure', async () => {
  let active = 0;
  let calls = 0;
  const files = Array.from({ length: 25 }, (_, index) => ({
    filename: `docs/rate-concurrency-${index}.md`,
    status: 'modified',
  }));
  await assert.rejects(
    attachTrustedMarkdownBase(
      files,
      'Jamula/Andreja',
      'c'.repeat(40),
      'token',
      async () => {
        calls += 1;
        active += 1;
        await new Promise((resolve) => setTimeout(resolve, 2));
        active -= 1;
        return {
          ok: false,
          status: 429,
          statusText: 'Too Many Requests',
          headers: { get: (name) => name === 'x-ratelimit-remaining' ? '0' : null },
          json: async () => ({}),
        };
      },
    ),
    /metadata rate limit exhausted/,
  );
  assert.equal(active, 0);
  assert.equal(calls, 8);
});

test('pagination failures reject rather than returning partial metadata', async () => {
  const fetchImpl = async () => ({
    ok: false,
    status: 403,
    statusText: 'Forbidden',
    headers: { get: () => null },
    json: async () => ({}),
  });
  await assert.rejects(
    paginatedFiles('https://api.github.com/page/1', 'token', fetchImpl),
    /metadata request failed/,
  );
});

test('metadata requests expose and enforce a bounded API budget', async () => {
  const budgetedFetch = createBudgetedFetch(async () => ({
    ok: true,
    status: 200,
    statusText: 'OK',
    headers: {
      get: (name) =>
        name === 'x-ratelimit-remaining' ? '14987' :
          name === 'x-ratelimit-reset' ? '1787723028' : null,
    },
    json: async () => [],
  }), 1);
  await budgetedFetch('https://api.github.com/one', {});
  await assert.rejects(
    budgetedFetch('https://api.github.com/two', {}),
    /request budget exceeded 1/,
  );
  assert.deepEqual(budgetedFetch.observation(), {
    limit: 1,
    used: 1,
    exhausted: true,
    rateLimitResponseObserved: false,
    minimumRateLimitRemaining: 14987,
    rateLimitResetEpoch: 1787723028,
  });
});

test('rate-limit responses fail classification metadata acquisition closed', async () => {
  const fetchImpl = async () => ({
    ok: false,
    status: 429,
    statusText: 'Too Many Requests',
    headers: { get: () => '0' },
    json: async () => ({}),
  });
  await assert.rejects(
    acquireChanges(
      'pull_request_target',
      { number: 118, pull_request: { changed_files: 1 } },
      'Jamula/Andreja',
      'token',
      fetchImpl,
    ),
    /metadata rate limit exhausted/,
  );
});

test('stale sample test-merge proof emits unavailable evidence before domains', async () => {
  const directory = path.join(root, 'artifacts/selective-ci');
  const eventPath = path.join(directory, `stale-merge-event-${process.pid}.json`);
  const decisionPath = path.join(directory, `stale-merge-decision-${process.pid}.json`);
  fs.mkdirSync(directory, { recursive: true });
  fs.writeFileSync(eventPath, JSON.stringify({
    number: 118,
    action: 'labeled',
    label: { name: 'ci:selective-shadow-sample' },
    sender: { login: 'Jett-Reno' },
    pull_request: {
      number: 118,
      changed_files: 1,
      base: { sha: 'a'.repeat(40) },
      head: { sha: 'b'.repeat(40) },
      merge_commit_sha: 'd'.repeat(40),
      labels: [{ name: 'ci:selective-shadow-sample' }],
    },
  }));
  const originalEnvironment = { ...process.env };
  const originalExitCode = process.exitCode;
  process.env.GITHUB_EVENT_NAME = 'pull_request_target';
  process.env.GITHUB_EVENT_PATH = eventPath;
  process.env.GITHUB_REPOSITORY = 'Jamula/Andreja';
  process.env.GITHUB_TOKEN = 'read-only-token';
  configureLabeledMainEnvironment();
  process.env.CHANGE_POLICY_PATH = path.join(__dirname, 'change-policy.v1.json');
  process.env.CHANGE_DECISION_PATH = decisionPath;
  delete process.env.GITHUB_OUTPUT;
  try {
    await main(
      withAuthorizedLabelHistory(async (url) =>
        url.includes('/git/commits/')
          ? githubResponse({
              sha: 'e'.repeat(40),
              parents: [
                { sha: 'a'.repeat(40) },
                { sha: 'b'.repeat(40) },
              ],
            })
          : githubResponse({
              mergeable: true,
              base: { sha: 'a'.repeat(40) },
              head: { sha: 'b'.repeat(40) },
              merge_commit_sha: 'e'.repeat(40),
            })),
      labelMainRuntime,
    );
    const decision = JSON.parse(fs.readFileSync(decisionPath, 'utf8'));
    assert.equal(
      decision.classificationFailure,
      'pull-request-merge-integrity-unavailable',
    );
    assert.equal(decision.trustedClassifierAvailable, false);
    assert.equal(decision.shadowSample.sampled, true);
    assert.equal(decision.mergeCommitProof.verified, false);
    assert.equal(decision.mergeCommitProof.reason, 'pull-request-test-merge-stale');
    assert.equal(decision.apiRequestBudget.used, 6);
    assert.equal(process.exitCode, 1);
  } finally {
    process.env = originalEnvironment;
    process.exitCode = originalExitCode;
    fs.rmSync(eventPath, { force: true });
    fs.rmSync(decisionPath, { force: true });
  }
});

test('sample metadata errors preserve failed proof and disable trusted domains', async () => {
  const directory = path.join(root, 'artifacts/selective-ci');
  const eventPath = path.join(directory, `merge-error-event-${process.pid}.json`);
  const decisionPath = path.join(directory, `merge-error-decision-${process.pid}.json`);
  fs.mkdirSync(directory, { recursive: true });
  fs.writeFileSync(eventPath, JSON.stringify({
    number: 118,
    action: 'labeled',
    label: { name: 'ci:selective-shadow-sample' },
    sender: { login: 'Jett-Reno' },
    pull_request: {
      number: 118,
      changed_files: 1,
      base: { sha: 'a'.repeat(40) },
      head: { sha: 'b'.repeat(40) },
      merge_commit_sha: 'd'.repeat(40),
      labels: [{ name: 'ci:selective-shadow-sample' }],
    },
  }));
  const originalEnvironment = { ...process.env };
  const originalExitCode = process.exitCode;
  process.env.GITHUB_EVENT_NAME = 'pull_request_target';
  process.env.GITHUB_EVENT_PATH = eventPath;
  process.env.GITHUB_REPOSITORY = 'Jamula/Andreja';
  process.env.GITHUB_TOKEN = 'read-only-token';
  configureLabeledMainEnvironment();
  process.env.CHANGE_POLICY_PATH = path.join(__dirname, 'change-policy.v1.json');
  process.env.CHANGE_DECISION_PATH = decisionPath;
  delete process.env.GITHUB_OUTPUT;
  try {
    await main(
      withAuthorizedLabelHistory(async (url) => {
        if (url.endsWith('/pulls/118')) {
          return githubResponse({
            mergeable: true,
            base: { sha: 'a'.repeat(40) },
            head: { sha: 'b'.repeat(40) },
            merge_commit_sha: 'd'.repeat(40),
          });
        }
        if (url.includes('/git/commits/')) {
          return githubResponse({
            sha: 'd'.repeat(40),
            parents: [
              { sha: 'a'.repeat(40) },
              { sha: 'b'.repeat(40) },
            ],
          });
        }
        return {
          ok: false,
          status: 500,
          statusText: 'Internal Server Error',
          headers: { get: () => null },
          json: async () => ({}),
        };
      }),
      labelMainRuntime,
    );
    const decision = JSON.parse(fs.readFileSync(decisionPath, 'utf8'));
    assert.equal(
      decision.classificationFailure,
      'pull-request-merge-integrity-unavailable',
    );
    assert.equal(decision.trustedClassifierAvailable, false);
    assert.equal(decision.mergeCommitProof.verified, false);
    assert.equal(
      decision.mergeCommitProof.reason,
      'pull-request-test-merge-metadata-unavailable',
    );
    assert.match(decision.mergeCommitProof.error, /500 Internal Server Error/);
    assert.equal(decision.apiRequestBudget.used, 7);
    assert.equal(process.exitCode, 1);
  } finally {
    process.env = originalEnvironment;
    process.exitCode = originalExitCode;
    fs.rmSync(eventPath, { force: true });
    fs.rmSync(decisionPath, { force: true });
  }
});

test('Markdown metadata rate limits persist unavailable classification evidence', async () => {
  const directory = path.join(root, 'artifacts/selective-ci');
  const eventPath = path.join(directory, `rate-limit-event-${process.pid}.json`);
  const decisionPath = path.join(directory, `rate-limit-decision-${process.pid}.json`);
  fs.mkdirSync(directory, { recursive: true });
  fs.writeFileSync(eventPath, JSON.stringify({
    number: 118,
    action: 'labeled',
    label: { name: 'ci:selective-shadow-sample' },
    sender: { login: 'Jett-Reno' },
    pull_request: {
      number: 118,
      changed_files: 1,
      base: { sha: 'a'.repeat(40) },
      head: { sha: 'b'.repeat(40) },
      merge_commit_sha: 'd'.repeat(40),
      labels: [{ name: 'ci:selective-shadow-sample' }],
    },
  }));
  const originalEnvironment = { ...process.env };
  const originalExitCode = process.exitCode;
  process.env.GITHUB_EVENT_NAME = 'pull_request_target';
  process.env.GITHUB_EVENT_PATH = eventPath;
  process.env.GITHUB_REPOSITORY = 'Jamula/Andreja';
  process.env.GITHUB_TOKEN = 'read-only-token';
  configureLabeledMainEnvironment();
  process.env.CHANGE_POLICY_PATH = path.join(__dirname, 'change-policy.v1.json');
  process.env.CHANGE_DECISION_PATH = decisionPath;
  delete process.env.GITHUB_OUTPUT;
  const file = {
    filename: 'docs/rate-limit.md',
    status: 'modified',
    additions: 1,
    deletions: 1,
    changes: 2,
    patch: '@@ -1 +1 @@\n-old\n+new',
  };
  const fetchImpl = async (url) => {
    const limited = url.includes('/contents/');
    let body;
    if (url.endsWith('/pulls/118')) {
      body = {
        mergeable: true,
        base: { sha: 'a'.repeat(40) },
        head: { sha: 'b'.repeat(40) },
        merge_commit_sha: 'd'.repeat(40),
      };
    } else if (url.includes('/git/commits/')) {
      body = {
        sha: 'd'.repeat(40),
        parents: [{ sha: 'a'.repeat(40) }, { sha: 'b'.repeat(40) }],
      };
    } else if (url.includes('/compare/')) {
      body = { merge_base_commit: { sha: 'c'.repeat(40) }, files: [file] };
    } else {
      body = [file];
    }
    return {
      ok: !limited,
      status: limited ? 429 : 200,
      statusText: limited ? 'Too Many Requests' : 'OK',
      headers: {
        get: (name) =>
          name === 'x-ratelimit-remaining' ? (limited ? '0' : '14987') :
            name === 'x-ratelimit-reset' ? '1787723028' : null,
      },
      json: async () => body,
    };
  };
  try {
    await main(withAuthorizedLabelHistory(fetchImpl), labelMainRuntime);
    const decision = JSON.parse(fs.readFileSync(decisionPath, 'utf8'));
    assert.equal(decision.classificationFailure, 'github-metadata-rate-limit-or-budget');
    assert.equal(decision.trustedClassifierAvailable, false);
    assert.equal(decision.mergeCommitProof.verified, false);
    assert.equal(
      decision.mergeCommitProof.reason,
      'pull-request-test-merge-recheck-unavailable',
    );
    assert.equal(decision.apiRequestBudget.rateLimitResponseObserved, true);
    assert.equal(decision.apiRequestBudget.used, 10);
    assert.equal(decision.fullSuite, true);
    assert.ok(
      decision.fullReasons.some((reason) =>
        reason.startsWith('trusted-markdown-base-error:GitHub metadata rate limit exhausted')),
    );
    assert.equal(process.exitCode, 1);
  } finally {
    process.env = originalEnvironment;
    process.exitCode = originalExitCode;
    fs.rmSync(eventPath, { force: true });
    fs.rmSync(decisionPath, { force: true });
  }
});

test('Markdown Contents HTTP errors disable sampled domains and merge proof', async () => {
  const directory = path.join(root, 'artifacts/selective-ci');
  const eventPath = path.join(directory, `contents-error-event-${process.pid}.json`);
  const decisionPath = path.join(directory, `contents-error-decision-${process.pid}.json`);
  fs.mkdirSync(directory, { recursive: true });
  fs.writeFileSync(eventPath, JSON.stringify({
    number: 118,
    action: 'labeled',
    label: { name: 'ci:selective-shadow-sample' },
    sender: { login: 'Jett-Reno' },
    pull_request: {
      number: 118,
      changed_files: 1,
      base: { sha: 'a'.repeat(40) },
      head: { sha: 'b'.repeat(40) },
      merge_commit_sha: 'd'.repeat(40),
      labels: [{ name: 'ci:selective-shadow-sample' }],
    },
  }));
  const originalEnvironment = { ...process.env };
  const originalExitCode = process.exitCode;
  process.env.GITHUB_EVENT_NAME = 'pull_request_target';
  process.env.GITHUB_EVENT_PATH = eventPath;
  process.env.GITHUB_REPOSITORY = 'Jamula/Andreja';
  process.env.GITHUB_TOKEN = 'read-only-token';
  configureLabeledMainEnvironment();
  process.env.CHANGE_POLICY_PATH = path.join(__dirname, 'change-policy.v1.json');
  process.env.CHANGE_DECISION_PATH = decisionPath;
  delete process.env.GITHUB_OUTPUT;
  const file = {
    filename: 'docs/contents-error.md',
    status: 'modified',
    additions: 1,
    deletions: 1,
    changes: 2,
    patch: '@@ -1 +1 @@\n-old\n+new',
  };
  try {
    await main(
      withAuthorizedLabelHistory(async (url) => {
        if (url.endsWith('/pulls/118')) {
          return githubResponse({
            mergeable: true,
            base: { sha: 'a'.repeat(40) },
            head: { sha: 'b'.repeat(40) },
            merge_commit_sha: 'd'.repeat(40),
          });
        }
        if (url.includes('/git/commits/')) {
          return githubResponse({
            sha: 'd'.repeat(40),
            parents: [
              { sha: 'a'.repeat(40) },
              { sha: 'b'.repeat(40) },
            ],
          });
        }
        if (url.includes('/compare/')) {
          return githubResponse({
            merge_base_commit: { sha: 'c'.repeat(40) },
            files: [file],
          });
        }
        if (url.includes('/contents/')) {
          return {
            ok: false,
            status: 500,
            statusText: 'Internal Server Error',
            headers: { get: () => null },
            json: async () => ({}),
          };
        }
        return githubResponse([file]);
      }),
      labelMainRuntime,
    );
    const decision = JSON.parse(fs.readFileSync(decisionPath, 'utf8'));
    assert.equal(decision.classificationFailure, 'github-metadata-unavailable');
    assert.equal(decision.trustedClassifierAvailable, false);
    assert.equal(decision.mergeCommitProof.verified, false);
    assert.equal(
      decision.mergeCommitProof.reason,
      'pull-request-test-merge-recheck-unavailable',
    );
    assert.match(decision.mergeCommitProof.error, /500 Internal Server Error/);
    assert.equal(decision.apiRequestBudget.used, 10);
    assert.equal(process.exitCode, 1);
  } finally {
    process.env = originalEnvironment;
    process.exitCode = originalExitCode;
    fs.rmSync(eventPath, { force: true });
    fs.rmSync(decisionPath, { force: true });
  }
});

test('final test-merge recheck preserves HTTP and rate-limit errors', async () => {
  const cases = [
    {
      status: 500,
      statusText: 'Internal Server Error',
      expectedFailure: 'pull-request-merge-integrity-unavailable',
    },
    {
      status: 429,
      statusText: 'Too Many Requests',
      expectedFailure: 'github-metadata-rate-limit-or-budget',
    },
  ];
  for (const fixture of cases) {
    const directory = path.join(root, 'artifacts/selective-ci');
    const eventPath = path.join(
      directory,
      `final-merge-${fixture.status}-event-${process.pid}.json`,
    );
    const decisionPath = path.join(
      directory,
      `final-merge-${fixture.status}-decision-${process.pid}.json`,
    );
    fs.mkdirSync(directory, { recursive: true });
    fs.writeFileSync(eventPath, JSON.stringify({
      number: 118,
      action: 'labeled',
      label: { name: 'ci:selective-shadow-sample' },
      sender: { login: 'Jett-Reno' },
      pull_request: {
        number: 118,
        changed_files: 1,
        base: { sha: 'a'.repeat(40) },
        head: { sha: 'b'.repeat(40) },
        merge_commit_sha: 'd'.repeat(40),
        labels: [{ name: 'ci:selective-shadow-sample' }],
      },
    }));
    const originalEnvironment = { ...process.env };
    const originalExitCode = process.exitCode;
    process.env.GITHUB_EVENT_NAME = 'pull_request_target';
    process.env.GITHUB_EVENT_PATH = eventPath;
    process.env.GITHUB_REPOSITORY = 'Jamula/Andreja';
    process.env.GITHUB_TOKEN = 'read-only-token';
    configureLabeledMainEnvironment();
    process.env.CHANGE_POLICY_PATH = path.join(__dirname, 'change-policy.v1.json');
    process.env.CHANGE_DECISION_PATH = decisionPath;
    delete process.env.GITHUB_OUTPUT;
    let pullReads = 0;
    const file = {
      filename: 'docs/added.md',
      status: 'added',
      changes: 1,
      patch: '@@ -0,0 +1 @@\n+prose',
    };
    try {
      await main(withAuthorizedLabelHistory(async (url) => {
        if (url.endsWith('/pulls/118')) {
          pullReads += 1;
          if (pullReads === 3) {
            return {
              ok: false,
              status: fixture.status,
              statusText: fixture.statusText,
              headers: {
                get: (name) =>
                  name === 'x-ratelimit-remaining' && fixture.status === 429
                    ? '0'
                    : null,
              },
              json: async () => ({}),
            };
          }
          return githubResponse({
            mergeable: true,
            base: { sha: 'a'.repeat(40) },
            head: { sha: 'b'.repeat(40) },
            merge_commit_sha: 'd'.repeat(40),
          });
        }
        if (url.includes('/git/commits/')) {
          return githubResponse({
            sha: 'd'.repeat(40),
            parents: [
              { sha: 'a'.repeat(40) },
              { sha: 'b'.repeat(40) },
            ],
          });
        }
        if (url.includes('/compare/')) {
          return githubResponse({
            merge_base_commit: { sha: 'c'.repeat(40) },
            files: [file],
          });
        }
        return githubResponse([file]);
      }), labelMainRuntime);
      const decision = JSON.parse(fs.readFileSync(decisionPath, 'utf8'));
      assert.equal(decision.classificationFailure, fixture.expectedFailure);
      assert.equal(decision.trustedClassifierAvailable, false);
      assert.equal(decision.mergeCommitProof.verified, false);
      assert.equal(
        decision.mergeCommitProof.reason,
        'pull-request-test-merge-recheck-unavailable',
      );
      assert.match(
        decision.mergeCommitProof.error,
        new RegExp(`${fixture.status} ${fixture.statusText}`),
      );
      assert.equal(decision.apiRequestBudget.used, 10);
      assert.equal(process.exitCode, 1);
    } finally {
      process.env = originalEnvironment;
      process.exitCode = originalExitCode;
      fs.rmSync(eventPath, { force: true });
      fs.rmSync(decisionPath, { force: true });
    }
  }
});

test('merge-group compare never follows commit pagination links and fails closed', async () => {
  const calls = [];
  const fetchImpl = async (url) => {
    calls.push(url);
    return {
      ok: true,
      status: 200,
      statusText: 'OK',
      headers: {
        get: () => '<https://api.github.com/compare/page/2>; rel="next", <https://api.github.com/compare/page/4>; rel="last"',
      },
      json: async () => ({
        files: [{ filename: 'docs/charter.md', status: 'modified', changes: 2, patch: '@@ -1 +1 @@\n-a\n+b' }],
        commits: new Array(100).fill({ sha: 'a'.repeat(40) }),
        merge_base_commit: { sha: 'c'.repeat(40) },
      }),
    };
  };
  const changes = await acquireChanges(
    'merge_group',
    { merge_group: { base_sha: 'a'.repeat(40), head_sha: 'b'.repeat(40) } },
    'Jamula/Andreja',
    'token',
    fetchImpl,
  );
  assert.equal(calls.length, 1);
  assert.equal(changes.files.length, 1);
  assert.ok(changes.forcedFullReasons.includes('merge-group-compare-link-uncertainty'));
});

test('merge-group compare forces full at the 300-file response limit', async () => {
  const fetchImpl = async () => ({
    ok: true,
    status: 200,
    statusText: 'OK',
    headers: { get: () => null },
    json: async () => ({
      files: Array.from({ length: 300 }, (_, index) => ({
        filename: `docs/file-${index}.md`,
        status: 'added',
        changes: 1,
        patch: '@@ -0,0 +1 @@\n+prose',
      })),
      merge_base_commit: { sha: 'c'.repeat(40) },
    }),
  });
  const changes = await acquireChanges(
    'merge_group',
    { merge_group: { base_sha: 'a'.repeat(40), head_sha: 'b'.repeat(40) } },
    'Jamula/Andreja',
    'token',
    fetchImpl,
  );
  assert.ok(changes.forcedFullReasons.includes('merge-group-compare-file-limit'));
});

test('merge-group compare accepts one complete response below the file limit', async () => {
  const fetchImpl = async () => ({
    ok: true,
    status: 200,
    statusText: 'OK',
    headers: { get: () => null },
    json: async () => ({
      files: [{ filename: 'src/App.cs', status: 'modified' }],
      merge_base_commit: { sha: 'c'.repeat(40) },
    }),
  });
  const changes = await acquireChanges(
    'merge_group',
    { merge_group: { base_sha: 'a'.repeat(40), head_sha: 'b'.repeat(40) } },
    'Jamula/Andreja',
    'token',
    fetchImpl,
  );
  assert.deepEqual(changes.forcedFullReasons, []);
  assert.equal(changes.trustedContentSha, 'c'.repeat(40));
});

test('merge-group compare fails closed when merge base is unavailable', async () => {
  const fetchImpl = async () => ({
    ok: true,
    status: 200,
    statusText: 'OK',
    headers: { get: () => null },
    json: async () => ({ files: [{ filename: 'src/App.cs', status: 'modified' }] }),
  });
  const changes = await acquireChanges(
    'merge_group',
    { merge_group: { base_sha: 'a'.repeat(40), head_sha: 'b'.repeat(40) } },
    'Jamula/Andreja',
    'token',
    fetchImpl,
  );
  assert.equal(changes.trustedContentSha, null);
  assert.ok(changes.forcedFullReasons.includes('merge-group-merge-base-unavailable'));
});

test('captured PR 103 Markdown inline code now fails closed', () => {
  const file = require('./fixtures/pr-103-markdown.json');
  const inspection = inspectMarkdownPatch(file);
  assert.equal(inspection.changedLineCount, 372);
  assert.equal(inspection.uncertain, undefined);
  assert.equal(inspection.executable, true);
  const result = classifyFiles([file], policy);
  assert.equal(result.fullSuite, true);
  assert.deepEqual(
    ALL_DOMAINS.filter((domain) => result.domains[domain].selected),
    ALL_DOMAINS,
  );
});

test('workflow isolates authorized samples from cancelable topology runs', () => {
  const workflow = fs.readFileSync(
    path.join(root, '.github/workflows/selective-ci-shadow.yml'),
    'utf8',
  );
  assert.match(workflow, /format\('sample-pr-\{0\}', github\.event\.pull_request\.number\)/);
  assert.match(
    workflow,
    /format\([\s\S]+?'topology-\{0\}-\{1\}',[\s\S]+?github\.event_name,/,
  );
  assert.match(
    workflow,
    /github\.event_name == 'repository_dispatch' &&\s+format\('smoke-\{0\}', vars\.SELECTIVE_CI_CONTROLLER_SHA\)/,
  );
  assert.match(
    workflow,
    /vars\.SELECTIVE_CI_SAMPLE_OPERATOR != ''[\s\S]+github\.event\.sender\.login == vars\.SELECTIVE_CI_SAMPLE_OPERATOR/,
  );
  assert.match(workflow, /^\s+pull_request_target:\s*$/m);
  assert.match(workflow, /^\s+repository_dispatch:\s*$/m);
  assert.match(workflow, /^\s+types: \[selective-ci-smoke\]\s*$/m);
  assert.doesNotMatch(workflow, /workflow_dispatch/);
  assert.doesNotMatch(workflow, /^\s+pull_request:\s*$/m);
  assert.doesNotMatch(workflow, /^\s+merge_group:\s*$/m);
  assert.doesNotMatch(workflow, /^\s+schedule:\s*$/m);
  assert.match(
    workflow,
    /cancel-in-progress: >-[\s\S]+github\.event_name == 'pull_request_target' &&[\s\S]+!\([\s\S]+vars\.SELECTIVE_CI_SAMPLE_OPERATOR != ''[\s\S]+github\.event\.sender\.login == vars\.SELECTIVE_CI_SAMPLE_OPERATOR[\s\S]+\)/,
  );
  assert.doesNotMatch(workflow, /^\s+push:\s*$/m);
  assert.match(workflow, /types: \[labeled\]/);
  assert.doesNotMatch(workflow, /types: \[[^\]]*(opened|synchronize|reopened)/);
  assert.equal(
    workflow.match(/if: needs\.classify\.outputs\.shadow_sampled == 'true' && needs\.classify\.outputs\.trusted_classifier == 'true' && needs\.classify\.outputs\.[a-z]+ == 'true'/g)?.length,
    ALL_DOMAINS.length,
  );
  assert.match(workflow, /'trusted_classifier=false'/);
  assert.match(workflow, /trusted_classifier: \$\{\{ steps\.classify\.outputs\.trusted_classifier \}\}/);
  assert.match(
    workflow,
    /label_run_authorization: \$\{\{ steps\.classify\.outputs\.label_run_authorization \}\}/,
  );
  assert.match(workflow, /labelPreconditionFailed/);
  assert.match(workflow, /labelRunAuthorization/);
  assert.match(
    workflow,
    /-SkipPostgreSql -OutputPath artifacts\/vulnerability\/solution\.json/,
  );
  assert.match(
    workflow,
    /-OnlyPostgreSql -OutputPath artifacts\/vulnerability\/postgresql\.json/,
  );
  assert.equal(
    workflow.match(/name: selective-ci-nuget-(solution|postgresql)-\$\{\{ github\.run_attempt \}\}/g)?.length,
    2,
  );
  assert.equal(
    workflow.match(/name: Upload bounded (solution|PostgreSQL) vulnerability evidence[\s\S]{0,400}?retention-days: 14/g)?.length,
    2,
  );
  assert.equal(
    workflow.match(/missing-pull-request-merge-sha/g)?.length,
    ALL_DOMAINS.length + 2,
  );
  assert.match(workflow, /EVENT_BASE_SHA: \$\{\{ github\.event\.pull_request\.base\.sha \|\| github\.event\.merge_group\.base_sha \|\| github\.event\.before \|\| github\.sha \}\}/);
  assert.match(workflow, /EVENT_HEAD_SHA: \$\{\{ github\.event\.pull_request\.head\.sha \|\| github\.event\.merge_group\.head_sha \|\| github\.event\.after \|\| github\.sha \}\}/);
  assert.equal(
    workflow.match(/VALIDATED_(?:REF|SHA): \$\{\{ github\.event_name == 'pull_request_target' && \(github\.event\.pull_request\.merge_commit_sha \|\| 'missing-pull-request-merge-sha'\) \|\| github\.event\.merge_group\.head_sha \|\| github\.sha \}\}/g)?.length,
    2,
  );
  assert.match(workflow, /permissions:\s*\{\}/);
  assert.match(workflow, /permissions:\r?\n\s+actions: read\r?\n\s+contents: read\r?\n\s+pull-requests: read/);
  assert.match(workflow, /SELECTIVE_CI_CONTROLLER_SHA: \$\{\{ vars\.SELECTIVE_CI_CONTROLLER_SHA \}\}/);
  assert.match(workflow, /SELECTIVE_CI_WINDOW_START: \$\{\{ vars\.SELECTIVE_CI_WINDOW_START \}\}/);
  assert.doesNotMatch(workflow, /client_payload/);
  assert.match(workflow, /trusted-classifier-unavailable-on-default-branch/);
  assert.match(workflow, /smoke_authorization: \$\{\{ steps\.classify\.outputs\.smoke_authorization \}\}/);
  assert.match(workflow, /API_REQUEST_BUDGET: \$\{\{ needs\.classify\.outputs\.api_request_budget \}\}/);
});

test('only an authorized operator sample-label event enables PR shadow work', () => {
  const labeled = {
    action: 'labeled',
    label: { name: 'ci:selective-shadow-sample' },
    pull_request: { labels: [{ name: 'ci:selective-shadow-sample' }] },
    sender: { login: 'Jett-Reno' },
  };
  assert.deepEqual(
    shadowSample(
      'pull_request_target',
      labeled,
      'Jett-Reno',
      null,
      {
        authorized: true,
        reason: 'authorized-labeled-run-slot-1-of-2',
      },
    ),
    {
    sampled: true,
    reason: 'authorized-labeled-run-slot-1-of-2',
    observedActor: 'Jett-Reno',
    expectedOperator: 'Jett-Reno',
    },
  );
  assert.deepEqual(
    shadowSample(
      'pull_request_target',
      { ...labeled, sender: { login: 'jett-reno' } },
      'Jett-Reno',
    ),
    {
      sampled: false,
      reason: 'sample-operator-mismatch',
      observedActor: 'jett-reno',
      expectedOperator: 'Jett-Reno',
    },
  );
  assert.deepEqual(shadowSample('pull_request_target', labeled, 'other-operator'), {
    sampled: false,
    reason: 'sample-operator-mismatch',
    observedActor: 'Jett-Reno',
    expectedOperator: 'other-operator',
  });
  assert.deepEqual(shadowSample('pull_request_target', labeled), {
    sampled: false,
    reason: 'sample-operator-unconfigured',
    observedActor: 'Jett-Reno',
    expectedOperator: null,
  });
  assert.equal(
    shadowSample(
      'pull_request_target',
      { ...labeled, action: 'synchronize' },
      'Jett-Reno',
    ).sampled,
    false,
  );
  assert.equal(
    shadowSample('pull_request_target', {
      ...labeled,
      label: { name: 'untrusted-lookalike' },
    }, 'Jett-Reno').sampled,
    false,
  );
  assert.deepEqual(shadowSample('schedule', {}, 'jett-reno'), {
    sampled: false,
    reason: 'unsupported-shadow-event',
    observedActor: null,
    expectedOperator: 'jett-reno',
  });
  assert.equal(
    shadowSample('repository_dispatch', smokeEvent, 'Jett-Reno', {
      authorized: true,
      reason: 'authorized-first-repository-dispatch-smoke',
      sender: 'Jett-Reno',
      expectedOperator: 'Jett-Reno',
    }).sampled,
    true,
  );
  assert.equal(
    shadowSample('repository_dispatch', smokeEvent, 'Jett-Reno', {
      authorized: false,
      reason: 'smoke-run-limit-exceeded',
      sender: 'Jett-Reno',
      expectedOperator: 'Jett-Reno',
    }).sampled,
    false,
  );
  assert.equal(
    classifyFiles([{ filename: 'src/App.cs', status: 'modified' }], policy)
      .trustedClassifierAvailable,
    true,
  );
  const classifierSource = fs.readFileSync(
    path.join(root, '.github/ci/change-classifier.js'),
    'utf8',
  );
  assert.match(classifierSource, /observedActor === configuredOperator/);
  assert.doesNotMatch(classifierSource, /observedActor\.toLowerCase\(\)/);
});

test('bootstrap operator comparison preserves exact canonical-case reasons', () => {
  const exact = runBootstrapDecision('Jett-Reno', 'Jett-Reno');
  assert.equal(exact.shadowSample.sampled, true);
  assert.equal(exact.shadowSample.reason, 'authorized-sample-label-event');
  const wrongCase = runBootstrapDecision('jett-reno', 'Jett-Reno');
  assert.equal(wrongCase.shadowSample.sampled, false);
  assert.equal(wrongCase.shadowSample.reason, 'sample-operator-mismatch');
  assert.equal(wrongCase.shadowSample.observedActor, 'jett-reno');
  assert.equal(wrongCase.shadowSample.expectedOperator, 'Jett-Reno');
});

test('bootstrap never authorizes repository dispatch without the trusted classifier', () => {
  const decision = runBootstrapDecision('Jett-Reno', 'Jett-Reno', {
    eventName: 'repository_dispatch',
    event: smokeEvent,
  });
  assert.equal(decision.shadowSample.sampled, false);
  assert.equal(
    decision.shadowSample.reason,
    'trusted-classifier-unavailable-on-default-branch',
  );
  assert.equal(decision.smokeAuthorization.authorized, false);
  assert.equal(decision.smokeAuthorization.sender, 'Jett-Reno');
  assert.equal(decision.smokeAuthorization.workflowRef, 'refs/heads/main');
  assert.equal(decision.smokeAuthorization.workflowSha, smokeSha);
  assert.equal(decision.smokeAuthorization.expectedControllerSha, smokeSha);
  assert.equal(decision.smokeAuthorization.windowStart, '2026-08-26T10:00:00Z');
  assert.deepEqual(decision.smokeAuthorization.historySnapshots, []);
});

test('aggregate artifact marks every domain unavailable when classification fails', () => {
  const { evidence, status } = runAggregateArtifact({
    CLASSIFY_RESULT: 'failure',
    SHADOW_SAMPLED: 'false',
  });
  assert.equal(status, 1, 'classification failure must fail the aggregate');
  assert.equal(evidence.classificationResult, 'failure');
  for (const domain of Object.values(evidence.domains)) {
    assert.equal(domain.disposition, 'unavailable');
    assert.equal(domain.reason, 'classification-unavailable');
  }
});

test('labeled bootstrap is an unavailable precondition failure', () => {
  const { evidence, status } = runAggregateArtifact({
    CLASSIFY_RESULT: 'success',
    SHADOW_SAMPLED: 'true',
    TRUSTED_CLASSIFIER: 'false',
    DOCS_SELECTED: 'true',
    DOTNET_SELECTED: 'true',
  });
  assert.equal(status, 1);
  assert.equal(evidence.shadowSampled, true);
  assert.equal(evidence.trustedClassifierAvailable, false);
  assert.equal(evidence.samplePreconditionFailed, true);
  assert.equal(evidence.samplingReason, 'trusted-classifier-unavailable-on-base');
  for (const domain of Object.values(evidence.domains)) {
    assert.equal(domain.scheduled, false);
    assert.equal(domain.disposition, 'unavailable');
    assert.equal(domain.reason, 'trusted-classifier-unavailable-on-base');
  }
});

test('repository-dispatch bootstrap is unavailable without trusted authorization', () => {
  const { evidence, status } = runAggregateArtifact({
    GITHUB_EVENT_NAME: 'repository_dispatch',
    CLASSIFY_RESULT: 'success',
    SHADOW_SAMPLED: 'false',
    TRUSTED_CLASSIFIER: 'false',
    SMOKE_AUTHORIZATION: JSON.stringify({
      authorized: false,
      reason: 'trusted-classifier-unavailable-on-default-branch',
    }),
  });
  assert.equal(status, 1);
  assert.equal(evidence.smokePreconditionFailed, true);
  for (const domain of Object.values(evidence.domains)) {
    assert.equal(domain.scheduled, false);
    assert.equal(domain.disposition, 'unavailable');
    assert.equal(domain.reason, 'trusted-classifier-unavailable-on-default-branch');
  }
});

test('labeled-run history failure makes aggregate evidence unavailable before domains', () => {
  const { evidence, status } = runAggregateArtifact({
    GITHUB_EVENT_NAME: 'pull_request_target',
    CLASSIFY_RESULT: 'failure',
    SHADOW_SAMPLED: 'true',
    TRUSTED_CLASSIFIER: 'false',
    LABEL_RUN_AUTHORIZATION: JSON.stringify({
      authorized: false,
      reason: 'label-current-run-not-visible',
      labelRunCount: 1,
    }),
    DOCS_SELECTED: 'true',
  });
  assert.equal(status, 1);
  assert.equal(evidence.labelPreconditionFailed, true);
  assert.equal(evidence.labelRunAuthorization.labelRunCount, 1);
  assert.equal(evidence.samplingReason, 'trusted-classifier-unavailable-on-base');
  for (const domain of Object.values(evidence.domains)) {
    assert.equal(domain.scheduled, false);
    assert.equal(domain.disposition, 'unavailable');
  }
});

test('ordinary unsampled bootstrap remains successful topology evidence', () => {
  const { evidence, status } = runAggregateArtifact({
    CLASSIFY_RESULT: 'success',
    SHADOW_SAMPLED: 'false',
    TRUSTED_CLASSIFIER: 'false',
    DOCS_SELECTED: 'true',
    DOTNET_SELECTED: 'true',
  });
  assert.equal(status, 0);
  assert.equal(evidence.shadowSampled, false);
  assert.equal(evidence.trustedClassifierAvailable, false);
  assert.equal(evidence.samplePreconditionFailed, false);
  for (const domain of Object.values(evidence.domains)) {
    assert.equal(domain.scheduled, false);
    assert.equal(domain.disposition, 'not-applicable');
    assert.equal(domain.reason, 'shadow-not-sampled');
  }
});

test('trusted labeled sample schedules selected domains normally', () => {
  const { evidence, status } = runAggregateArtifact({
    CLASSIFY_RESULT: 'success',
    SHADOW_SAMPLED: 'true',
    TRUSTED_CLASSIFIER: 'true',
    LABEL_RUN_AUTHORIZATION: JSON.stringify({
      authorized: true,
      reason: 'authorized-labeled-run-within-window',
      labelRunCount: 1,
    }),
    DOCS_SELECTED: 'true',
    DOCS_RESULT: 'success',
  });
  assert.equal(status, 0);
  assert.equal(evidence.samplePreconditionFailed, false);
  assert.equal(evidence.domains.docs.scheduled, true);
  assert.equal(evidence.domains.docs.disposition, 'passed');
  assert.equal(evidence.domains.dotnet.scheduled, false);
  assert.equal(evidence.domains.dotnet.disposition, 'not-applicable');
});

test('aggregate records smoke authorization and request-budget evidence', () => {
  const smokeAuthorization = {
    authorized: true,
    reason: 'authorized-first-repository-dispatch-smoke',
    sender: 'Jett-Reno',
    actor: 'Jett-Reno',
    expectedOperator: 'Jett-Reno',
    liveDefaultBranchSha: smokeSha,
    historySnapshots: [
      { label: 'initial', currentRevisionCount: 1 },
      { label: 'race-recheck', currentRevisionCount: 1 },
    ],
  };
  const requestBudget = {
    limit: 132,
    used: 3,
    exhausted: false,
    rateLimitResponseObserved: false,
  };
  const { evidence, status } = runAggregateArtifact({
    GITHUB_EVENT_NAME: 'repository_dispatch',
    CLASSIFY_RESULT: 'success',
    SHADOW_SAMPLED: 'true',
    TRUSTED_CLASSIFIER: 'true',
    SMOKE_AUTHORIZATION: JSON.stringify(smokeAuthorization),
    API_REQUEST_BUDGET: JSON.stringify(requestBudget),
  });
  assert.equal(status, 0);
  assert.deepEqual(evidence.smokeAuthorization, smokeAuthorization);
  assert.deepEqual(evidence.apiRequestBudget, requestBudget);
  assert.equal(
    evidence.samplingReason,
    'authorized-first-repository-dispatch-smoke',
  );
});

test('aggregate PR sample attributes the event and validated merge revisions', () => {
  const eventBaseSha = 'a'.repeat(40);
  const eventHeadSha = 'b'.repeat(40);
  const mergeSha = 'd'.repeat(40);
  const { evidence, status } = runAggregateArtifact({
    GITHUB_EVENT_NAME: 'pull_request_target',
    GITHUB_SHA: eventBaseSha,
    EVENT_BASE_SHA: eventBaseSha,
    EVENT_HEAD_SHA: eventHeadSha,
    VALIDATED_REF: mergeSha,
    VALIDATED_SHA: mergeSha,
    CLASSIFY_RESULT: 'success',
    SHADOW_SAMPLED: 'true',
    TRUSTED_CLASSIFIER: 'true',
    LABEL_RUN_AUTHORIZATION: JSON.stringify({
      authorized: true,
      reason: 'authorized-labeled-run-within-window',
      labelRunCount: 1,
    }),
    DOCS_SELECTED: 'true',
    DOCS_RESULT: 'success',
  });
  assert.equal(status, 0);
  assert.deepEqual(evidence.revision, {
    eventBaseSha,
    eventHeadSha,
    validatedRef: mergeSha,
    validatedSha: mergeSha,
    validationRevisionAvailable: true,
    validationRevisionReason: 'exact-validation-revision',
  });
  assert.notEqual(evidence.revision.validatedSha, eventBaseSha);
  assert.equal(Object.hasOwn(evidence, 'sha'), false);
  assert.equal(evidence.labelRunAuthorization.labelRunCount, 1);
});

test('aggregate PR sample fails closed without an exact merge revision', () => {
  const { evidence, status } = runAggregateArtifact({
    GITHUB_EVENT_NAME: 'pull_request_target',
    VALIDATED_REF: 'missing-pull-request-merge-sha',
    VALIDATED_SHA: '',
    CLASSIFY_RESULT: 'success',
    SHADOW_SAMPLED: 'true',
    TRUSTED_CLASSIFIER: 'true',
    LABEL_RUN_AUTHORIZATION: JSON.stringify({
      authorized: true,
      reason: 'authorized-labeled-run-within-window',
      labelRunCount: 2,
    }),
    DOCS_SELECTED: 'true',
    DOCS_RESULT: 'success',
  });
  assert.equal(status, 1);
  assert.equal(evidence.revision.validatedRef, null);
  assert.equal(evidence.revision.validatedSha, null);
  assert.equal(evidence.revision.validationRevisionAvailable, false);
  assert.equal(
    evidence.revision.validationRevisionReason,
    'exact-validation-revision-unavailable',
  );
  assert.equal(evidence.domains.docs.disposition, 'unavailable');
  assert.equal(evidence.domains.docs.reason, 'exact-validation-revision-unavailable');
});

test('selective CI runbook preserves the approved sample and budget gates', () => {
  const runbook = fs.readFileSync(
    path.join(root, 'docs/operations/selective-ci.md'),
    'utf8',
  );
  const testingMatrix = fs.readFileSync(
    path.join(root, 'docs/testing-matrix.md'),
    'utf8',
  );
  assert.match(runbook, /one pre-identified, naturally useful prose-only PR/);
  assert.match(runbook, /one full-suite\s+PR/);
  assert.match(runbook, /No synthetic or no-op change/);
  assert.match(runbook, /maximum of the smoke, docs, and full\s+trusted runs/);
  assert.match(runbook, /N <= 2/);
  assert.match(runbook, /S=1/);
  assert.match(runbook, /pre-approved `F=3`/);
  assert.match(runbook, /`F=3` as a\s+budget ceiling/);
  assert.match(runbook, /target remains modeled `F=2`/);
  assert.match(runbook, /4-minute total docs run[\s\S]+misses the modeled 3-minute \/ 81\.25% savings target/);
  assert.match(runbook, /approval may authorize only\s+a new evidence window[\s\S]+cannot waive the target/);
  assert.match(runbook, /38 minutes \/ USD 0\.304/);
  assert.match(runbook, /51 minutes \/ USD 0\.408/);
  assert.match(runbook, /64 minutes \/ USD 0\.512/);
  assert.match(runbook, /200 job-minutes for one full run/);
  assert.match(runbook, /600 job-minutes \/ USD 4\.800/);
  assert.match(runbook, /exceeds 6 minutes.*exceeds 32 minutes/s);
  assert.match(runbook, /[Aa]ny observed `F_i >= 4`/);
  assert.match(runbook, /N=2` is the hard cap/);
  assert.match(runbook, /every labeled event[\s\S]+counts against `N`/);
  assert.match(runbook, /terminally disable/);
  assert.match(runbook, /no routine pause\/re-enable path/);
  assert.match(runbook, /ordinary PRs emit no shadow contexts/);
  assert.match(runbook, /[Ss]table\s+every-PR contexts are future promotion scope/);
  assert.match(runbook, /run the exact\s+squash-merged classifier and policy against both current candidate/);
  assert.match(runbook, /repository_dispatch` type\s+`selective-ci-smoke`/);
  const candidateGate = runbook.indexOf('2. Before any charged dispatch');
  const chargedSmoke = runbook.indexOf('3. Before provisioning any label');
  assert.ok(candidateGate >= 0 && chargedSmoke > candidateGate);
  assert.match(runbook, /no natural prose candidate exists[\s\S]+do \*\*not\*\* dispatch or\s+spend `S`/);
  assert.match(runbook, /read-only local\/GitHub metadata[\s\S]+exact\s+squash-merged classifier and policy/);
  assert.match(runbook, /schemaVersion: 2/);
  assert.match(runbook, /no-unexpected-label precondition/);
  assert.match(runbook, /issue-status\.yml[\s\S]+PR-to-PR closing-reference residual/);
  assert.match(runbook, /Before the window and again before each sample[\s\S]+every open PR body/);
  assert.match(runbook, /every open PR body, closing reference, and current label/);
  assert.doesNotMatch(runbook, /confirm that none can\s+apply any PR label/);
  assert.match(runbook, /Record `merged_at` as\s+`<START>` before any workflow action/);
  assert.match(runbook, /every labeled event and labeled\s+workflow run at or after `<START>` counts against `N`/);
  assert.match(runbook, /Require zero repository-dispatch\s+smoke runs, zero labeled workflow runs, and zero label events/);
  assert.match(runbook, /@?\(\$commit\.parents\)\.Count -ne 2/);
  assert.doesNotMatch(runbook, /@?\(\$commit\.parents\)\.Count -lt 2/);
  assert.match(
    runbook,
    /\$reference = \[regex\]::new\([\s\S]+RegexOptions\]::IgnoreCase\)/,
  );
  assert.match(
    runbook,
    /\$match\.Groups\['owner'\]\.Success[\s\S]+-ine 'Jamula'[\s\S]+-ine 'Andreja'[\s\S]+continue/,
  );
  assert.match(runbook, /waits for that[\s\S]+does not remove, reapply, or apply it elsewhere/);
  assert.match(
    runbook,
    /gh workflow disable selective-ci-shadow\.yml --repo Jamula\/Andreja/,
  );
  assert.match(
    runbook,
    /gh api repos\/Jamula\/Andreja\/actions\/workflows\/selective-ci-shadow\.yml --jq '\.state'/,
  );
  assert.doesNotMatch(runbook, /gh workflow view[\s\S]{0,120}--json/);
  assert.match(runbook, /positive\s+docs and full evidence[\s\S]+separate FinOps approval/);
  assert.match(runbook, /0% replay[\s\S]+8\.75%/);
  assert.match(runbook, /SELECTIVE_CI_SAMPLE_OPERATOR/);
  assert.match(runbook, /observedActor.*expectedOperator/s);
  assert.match(runbook, /sample-pr-<number>.*cancellation disabled/s);
  assert.match(runbook, /queues[\s\S]+history guard rejects[\s\S]+repeated\s+PR identity/);
  assert.match(runbook, /variables[\s\S]+before \*\*any\*\* label/);
  assert.match(runbook, /labelRunCount[\s\S]+authorizedRunCount[\s\S]+authorizedSlot/);
  assert.match(runbook, /Current-run indexing lag fails closed[\s\S]+do not retry/);
  assert.match(runbook, /[Aa] third raw labeled run[\s\S]+repeat[\s\S]+rerun[\s\S]+duplicate\/ambiguous binding/);
  assert.match(runbook, /\$labelPages = gh api --paginate --slurp[\s\S]+ConvertFrom-Json/);
  assert.doesNotMatch(runbook, /--slurp[\s\S]{0,250}--jq/);
  assert.match(runbook, /exact\s+squash-merged controller SHA[\s\S]+only trusted controller revision/);
  assert.match(runbook, /GET \/repos\/Jamula\/Andreja\/issues\/events/);
  assert.match(
    runbook,
    /Select-Object created_at,[\s\S]+Name='actor'[\s\S]+Name='pull'[\s\S]+Name='label'; Expression=\{\$_.label\.name\}/,
  );
  for (const workflow of [
    'issue-status.yml',
    'squad-heartbeat.yml',
    'squad-issue-assign.yml',
    'squad-label-enforce.yml',
    'squad-triage.yml',
    'sync-squad-labels.yml',
  ]) {
    assert.match(runbook, new RegExp(workflow.replace('.', '\\.')));
  }
  assert.match(runbook, /pulls\/\{number\}\/files[\s\S]+follows every file-page link/);
  assert.match(runbook, /merge group has no PR files\/count\s+endpoint[\s\S]+any compare link/);
  assert.match(runbook, /mergeable` is non-null\/true[\s\S]+parent 0\/parent 1/);
  assert.match(runbook, /requires exactly two parents/);
  assert.match(runbook, /present patch requires[\s\S]+positive integer exactly equal[\s\S]+Missing, fractional, zero, negative/);
  assert.match(runbook, /mergeCommitProof\.verified=true/);
  assert.match(runbook, /at most eight concurrent requests/);
  assert.match(runbook, /exact canonical GitHub login casing/);
  assert.match(runbook, /SELECTIVE_CI_CONTROLLER_SHA/);
  assert.match(runbook, /SELECTIVE_CI_WINDOW_START/);
  assert.match(runbook, /gh api --method POST repos\/Jamula\/Andreja\/dispatches/);
  assert.match(runbook, /intentionally carries no controller SHA in\s+`client_payload`/);
  assert.doesNotMatch(runbook, /workflow_dispatch/);
  assert.doesNotMatch(runbook, /schedule\/manual|manual events/);
  assert.match(runbook, /smoke\s+count `1`/);
  assert.match(runbook, /static workflow-expression and fixture assertions\s+only/);
  assert.match(runbook, /32924008713[\s\S]+fab046f6608fc93b032ed7e618b57f2547c88bdc[\s\S]+pre-trusted-classifier-gate/);
  assert.doesNotMatch(runbook, /ordinary\s+unlabelled bootstrap/);
  assert.doesNotMatch(runbook, /cb7a434|95d7450|N <= 25|N >= 20|81 minutes|94 minutes|118 minutes|1,060|every 12 hours|remaining seven|five eligible docs|five full/);
  assert.match(testingMatrix, /maximum of smoke, docs, and full trusted runs/);
  assert.match(testingMatrix, /only `labeled` and `repository_dispatch: selective-ci-smoke`/);
  assert.match(testingMatrix, /38 planned \/ 51 fail-closed ceiling \/ 64 with 25% headroom/);
  assert.match(testingMatrix, /N<=2/);
  assert.match(testingMatrix, /timeout-derived full-run bound is 200 job-minutes/);
  assert.match(testingMatrix, /all six label-write-capable workflows/);
  assert.match(testingMatrix, /`F=3` is an uncertainty budget ceiling/);
  assert.match(testingMatrix, /eight-concurrent Markdown Contents cap/);
  assert.match(testingMatrix, /Markdown-only generic docs/);
  assert.match(testingMatrix, /No qualifying natural prose candidate terminally disables without dispatching or spending `S`/);
  assert.doesNotMatch(testingMatrix, /opened|N<=25|81 planned|94 fail-closed|remaining seven|5 docs \+ 5 full/);
});

test('historical replay economics use the documented per-domain model', () => {
  const docs = classifyFiles(
    [{
      filename: 'docs/charter.md',
      status: 'modified',
      changes: 2,
      baseContent: 'old\n',
      patch: '@@ -1 +1 @@\n-old\n+new',
    }],
    policy,
  );
  const source = classifyFiles([{ filename: 'src/App.cs', status: 'modified' }], policy);
  const full = classifyFiles([{ filename: '.github/dependabot.yml', status: 'modified' }], policy);
  assert.equal(classificationName(docs), 'docs-only');
  assert.equal(estimatedMinutes(docs), 3);
  assert.equal(classificationName(source), 'partial');
  assert.equal(estimatedMinutes(source), 10);
  assert.equal(classificationName(full), 'full');
  assert.equal(estimatedMinutes(full), 16);
});

test('historical replay fails closed at the exact GitHub 3000-file cap', () => {
  const files = Array.from({ length: 3000 }, (_, index) => ({
    filename: `docs/replay-${index}.md`,
    status: 'added',
    changes: 1,
    patch: '@@ -0,0 +1 @@\n+prose',
  }));
  const forcedFullReasons = replayForcedFullReasons(
    { changed_files: files.length },
    files,
    'c'.repeat(40),
  );
  assert.deepEqual(forcedFullReasons, ['pull-request-file-limit']);
  const result = classifyFiles(files, policy, { forcedFullReasons });
  assert.equal(result.fullSuite, true);
  assert.ok(result.fullReasons.includes('pull-request-file-limit'));
});

test('recorded historical replay is internally consistent with the current policy', () => {
  const evidence = require('./evidence/recent-merged-pr-replay.json');
  const policyBytes = fs
    .readFileSync(path.join(__dirname, 'change-policy.v1.json'), 'utf8')
    .replace(/\r\n/g, '\n');
  const classifierBytes = fs
    .readFileSync(path.join(__dirname, 'change-classifier.js'), 'utf8')
    .replace(/\r\n/g, '\n');
  assert.equal(evidence.sample.count, 20);
  assert.equal(
    Object.values(evidence.portfolio.counts).reduce((sum, count) => sum + count, 0),
    evidence.sample.count,
  );
  assert.equal(evidence.portfolio.counts['docs-only'], 0);
  assert.equal(evidence.portfolio.counts.partial, 0);
  assert.equal(evidence.portfolio.counts.full, 20);
  assert.equal(evidence.portfolio.expectedRoundedMinutesSaved, 0);
  assert.equal(evidence.portfolio.expectedSavingsShare, 0);
  assert.equal(
    evidence.policySha256,
    createHash('sha256').update(policyBytes).digest('hex'),
  );
  assert.equal(
    evidence.classifierSha256,
    createHash('sha256').update(classifierBytes).digest('hex'),
  );
});

test('push, schedule, unauthorized repository dispatch, and unsupported events force full safety', async () => {
  for (const eventName of ['push', 'schedule', 'repository_dispatch', 'unsupported']) {
    const changes = await acquireChanges(eventName, {}, 'Jamula/Andreja', 'token');
    const result = classifyFiles(changes.files, policy, {
      eventName,
      forcedFullReasons: changes.forcedFullReasons,
    });
    assert.equal(result.fullSuite, true);
    assert.deepEqual(
      ALL_DOMAINS.filter((domain) => result.domains[domain].selected),
      ALL_DOMAINS,
    );
  }
});
