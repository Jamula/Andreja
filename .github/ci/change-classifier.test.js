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

for (const fixture of fixtures) {
  test(fixture.name, () => {
    const result = classifyFiles(fixture.files, policy);
    const selected = ALL_DOMAINS.filter((domain) => result.domains[domain].selected).sort();
    assert.deepEqual(selected, fixture.selected.sort());
    assert.equal(result.fullSuite, fixture.fullSuite);
  });
}

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
    documentation: 'docs/example.md',
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

test('Markdown metadata rate limits persist unavailable classification evidence', async () => {
  const directory = path.join(root, 'artifacts/selective-ci');
  const eventPath = path.join(directory, `rate-limit-event-${process.pid}.json`);
  const decisionPath = path.join(directory, `rate-limit-decision-${process.pid}.json`);
  fs.mkdirSync(directory, { recursive: true });
  fs.writeFileSync(eventPath, JSON.stringify({
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
  }));
  const originalEnvironment = { ...process.env };
  const originalExitCode = process.exitCode;
  process.env.GITHUB_EVENT_NAME = 'pull_request_target';
  process.env.GITHUB_EVENT_PATH = eventPath;
  process.env.GITHUB_REPOSITORY = 'Jamula/Andreja';
  process.env.GITHUB_TOKEN = 'read-only-token';
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
    const body = url.includes('/compare/')
      ? { merge_base_commit: { sha: 'c'.repeat(40) }, files: [file] }
      : [file];
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
    await main(fetchImpl);
    const decision = JSON.parse(fs.readFileSync(decisionPath, 'utf8'));
    assert.equal(decision.classificationFailure, 'github-metadata-rate-limit-or-budget');
    assert.equal(decision.apiRequestBudget.rateLimitResponseObserved, true);
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

test('workflow bounds PR sampling and never cancels scheduled safety runs', () => {
  const workflow = fs.readFileSync(
    path.join(root, '.github/workflows/selective-ci-shadow.yml'),
    'utf8',
  );
  assert.match(workflow, /group: selective-ci-shadow-\$\{\{ github\.event_name \}\}-/);
  assert.match(workflow, /^\s+pull_request_target:\s*$/m);
  assert.doesNotMatch(workflow, /^\s+pull_request:\s*$/m);
  assert.doesNotMatch(workflow, /^\s+merge_group:\s*$/m);
  assert.match(workflow, /cancel-in-progress: \$\{\{ github\.event_name == 'pull_request_target' \}\}/);
  assert.doesNotMatch(workflow, /^\s+push:\s*$/m);
  assert.match(workflow, /types: \[opened, labeled\]/);
  assert.doesNotMatch(workflow, /types: \[[^\]]*(synchronize|reopened)/);
  assert.equal(
    workflow.match(/if: needs\.classify\.outputs\.shadow_sampled == 'true' && needs\.classify\.outputs\.trusted_classifier == 'true' && needs\.classify\.outputs\.[a-z]+ == 'true'/g)?.length,
    ALL_DOMAINS.length,
  );
  assert.match(workflow, /echo "trusted_classifier=false"/);
  assert.match(workflow, /trusted_classifier: \$\{\{ steps\.classify\.outputs\.trusted_classifier \}\}/);
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
  assert.match(workflow, /permissions:\r?\n\s+contents: read\r?\n\s+pull-requests: read/);
});

test('only the trusted sample-label event enables PR shadow work', () => {
  const labeled = {
    action: 'labeled',
    label: { name: 'ci:selective-shadow-sample' },
    pull_request: { labels: [{ name: 'ci:selective-shadow-sample' }] },
  };
  assert.deepEqual(shadowSample('pull_request_target', labeled), {
    sampled: true,
    reason: 'maintainer-sample-label-event',
  });
  assert.equal(
    shadowSample('pull_request_target', { ...labeled, action: 'synchronize' }).sampled,
    false,
  );
  assert.equal(
    shadowSample('pull_request_target', {
      ...labeled,
      label: { name: 'untrusted-lookalike' },
    }).sampled,
    false,
  );
  assert.equal(shadowSample('schedule', {}).sampled, true);
  assert.equal(
    classifyFiles([{ filename: 'src/App.cs', status: 'modified' }], policy)
      .trustedClassifierAvailable,
    true,
  );
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
});

test('aggregate PR sample fails closed without an exact merge revision', () => {
  const { evidence, status } = runAggregateArtifact({
    GITHUB_EVENT_NAME: 'pull_request_target',
    VALIDATED_REF: 'missing-pull-request-merge-sha',
    VALIDATED_SHA: '',
    CLASSIFY_RESULT: 'success',
    SHADOW_SAMPLED: 'true',
    TRUSTED_CLASSIFIER: 'true',
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
  assert.match(runbook, /first three valid trusted samples/);
  assert.match(runbook, /maximum of those three observations/);
  assert.match(runbook, /remaining seven/);
  assert.match(runbook, /M=0/);
  assert.match(runbook, /no current `merge_group`\s+trigger/);
  assert.match(runbook, /shadow makes no merge-group context claim/);
  assert.match(runbook, /323 minutes \/ USD 2\.584/);
  assert.match(runbook, /388 minutes \/ USD 3\.104/);
  assert.match(runbook, /485 rounded minutes \/ USD 3\.880/);
  assert.match(runbook, /200 job-minutes for one full run/);
  assert.match(runbook, /4,400.*job-minutes \/ USD 35\.200/);
  assert.match(runbook, /exceeds 6 minutes.*exceeds 32 minutes/s);
  assert.match(runbook, /observed `F_i >= 4`/);
  assert.match(runbook, /N=100` is a hard cap/);
  assert.match(runbook, /outside the original `S <= 3`/);
  assert.match(runbook, /no continuation or replacement window/);
  assert.match(runbook, /workflow_dispatch.*Charge it against `S`/s);
  assert.match(runbook, /schemaVersion: 2/);
  assert.match(runbook, /no-automation precondition/);
  assert.match(runbook, /waits for that[\s\S]+does not remove, reapply, or apply it elsewhere/);
  assert.match(runbook, /continuation budget[\s\S]+every retained automatic trigger/);
  assert.match(runbook, /gh workflow disable selective-ci-shadow\.yml/);
  assert.match(runbook, /Promotion remains blocked while it is disabled/);
  assert.match(runbook, /\$pages = gh api --paginate --slurp[\s\S]+ConvertFrom-Json/);
  assert.doesNotMatch(runbook, /--slurp[\s\S]{0,250}--jq/);
  assert.match(runbook, /final `pull_request_target` YAML at commit `cb7a434` has never\s+executed/);
  assert.match(runbook, /GET \/repos\/Jamula\/Andreja\/issues\/events/);
  assert.doesNotMatch(runbook, /M <= 3|371 minutes|436 minutes|545 rounded minutes|remaining nine|repos\/cyrusjamula\/Andreja/);
  assert.match(testingMatrix, /maximum of the first three valid trusted samples/);
  assert.match(testingMatrix, /remaining seven/);
  assert.match(testingMatrix, /M=0/);
  assert.match(testingMatrix, /no `merge_group` trigger/);
  assert.match(testingMatrix, /323 planned \/ 388 fail-closed ceiling \/ 485 with 25% headroom/);
  assert.match(testingMatrix, /timeout-derived full-run bound is 200 job-minutes/);
  assert.doesNotMatch(testingMatrix, /M <= 3|371 planned|remaining nine/);
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

test('push, schedule, dispatch, and unsupported events force full safety', async () => {
  for (const eventName of ['push', 'schedule', 'workflow_dispatch', 'repository_dispatch']) {
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
