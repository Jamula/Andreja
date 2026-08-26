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

function githubResponse(body, link = null) {
  return {
    ok: true,
    status: 200,
    statusText: 'OK',
    headers: { get: (name) => name === 'link' ? link : null },
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

function runBootstrapDecision(actor, operator) {
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
  fs.writeFileSync(eventPath, JSON.stringify({
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
          GITHUB_EVENT_NAME: 'pull_request_target',
          GITHUB_EVENT_PATH: eventPath,
          CHANGE_DECISION_PATH: decisionPath,
          GITHUB_OUTPUT: outputPath,
          SELECTIVE_CI_SAMPLE_OPERATOR: operator,
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

test('sample label rejects a test-merge parent mismatch', async () => {
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
            parents: [{ sha: 'c'.repeat(40) }, { sha: head }],
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
  process.env.SELECTIVE_CI_SAMPLE_OPERATOR = 'Jett-Reno';
  process.env.CHANGE_POLICY_PATH = path.join(__dirname, 'change-policy.v1.json');
  process.env.CHANGE_DECISION_PATH = decisionPath;
  delete process.env.GITHUB_OUTPUT;
  try {
    await main(async (url) =>
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
          }),
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
    assert.equal(decision.apiRequestBudget.used, 2);
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
  process.env.SELECTIVE_CI_SAMPLE_OPERATOR = 'Jett-Reno';
  process.env.CHANGE_POLICY_PATH = path.join(__dirname, 'change-policy.v1.json');
  process.env.CHANGE_DECISION_PATH = decisionPath;
  delete process.env.GITHUB_OUTPUT;
  try {
    await main(async (url) => {
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
    });
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
    assert.equal(decision.apiRequestBudget.used, 3);
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
    await main(fetchImpl);
    const decision = JSON.parse(fs.readFileSync(decisionPath, 'utf8'));
    assert.equal(decision.classificationFailure, 'github-metadata-rate-limit-or-budget');
    assert.equal(decision.trustedClassifierAvailable, false);
    assert.equal(decision.mergeCommitProof.verified, false);
    assert.equal(
      decision.mergeCommitProof.reason,
      'pull-request-test-merge-recheck-unavailable',
    );
    assert.equal(decision.apiRequestBudget.rateLimitResponseObserved, true);
    assert.equal(decision.apiRequestBudget.used, 6);
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
  process.env.SELECTIVE_CI_SAMPLE_OPERATOR = 'Jett-Reno';
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
    await main(async (url) => {
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
    });
    const decision = JSON.parse(fs.readFileSync(decisionPath, 'utf8'));
    assert.equal(decision.classificationFailure, 'github-metadata-unavailable');
    assert.equal(decision.trustedClassifierAvailable, false);
    assert.equal(decision.mergeCommitProof.verified, false);
    assert.equal(
      decision.mergeCommitProof.reason,
      'pull-request-test-merge-recheck-unavailable',
    );
    assert.match(decision.mergeCommitProof.error, /500 Internal Server Error/);
    assert.equal(decision.apiRequestBudget.used, 6);
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
    process.env.SELECTIVE_CI_SAMPLE_OPERATOR = 'Jett-Reno';
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
      await main(async (url) => {
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
      });
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
      assert.equal(decision.apiRequestBudget.used, 6);
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
    /vars\.SELECTIVE_CI_SAMPLE_OPERATOR != ''[\s\S]+github\.event\.sender\.login == vars\.SELECTIVE_CI_SAMPLE_OPERATOR/,
  );
  assert.match(workflow, /^\s+pull_request_target:\s*$/m);
  assert.match(workflow, /^\s+workflow_dispatch:\s*$/m);
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

test('only an authorized operator sample-label event enables PR shadow work', () => {
  const labeled = {
    action: 'labeled',
    label: { name: 'ci:selective-shadow-sample' },
    pull_request: { labels: [{ name: 'ci:selective-shadow-sample' }] },
    sender: { login: 'Jett-Reno' },
  };
  assert.deepEqual(shadowSample('pull_request_target', labeled, 'Jett-Reno'), {
    sampled: true,
    reason: 'authorized-sample-label-event',
    observedActor: 'Jett-Reno',
    expectedOperator: 'Jett-Reno',
  });
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
    sampled: true,
    reason: 'non-pull-request-full-safety',
    observedActor: null,
    expectedOperator: 'jett-reno',
  });
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
  assert.match(runbook, /all labeled events/);
  assert.match(runbook, /terminally disable/);
  assert.match(runbook, /no routine pause\/re-enable path/);
  assert.match(runbook, /ordinary PRs emit no shadow contexts/);
  assert.match(runbook, /[Ss]table\s+every-PR contexts are future promotion scope/);
  assert.match(runbook, /run the exact\s+squash-merged classifier and policy against both current candidate/);
  assert.match(runbook, /workflow_dispatch.*Charge it against `S`/s);
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
  assert.match(runbook, /Immediately before dispatch[\s\S]+zero\s+labeled workflow runs and zero label events/);
  assert.match(runbook, /waits for that[\s\S]+does not remove, reapply, or apply it elsewhere/);
  assert.match(runbook, /gh workflow disable selective-ci-shadow\.yml/);
  assert.match(runbook, /positive\s+docs and full evidence[\s\S]+separate FinOps approval/);
  assert.match(runbook, /0% replay[\s\S]+8\.75%/);
  assert.match(runbook, /SELECTIVE_CI_SAMPLE_OPERATOR/);
  assert.match(runbook, /observedActor.*expectedOperator/s);
  assert.match(runbook, /sample-pr-<number>.*cancellation disabled/s);
  assert.match(runbook, /\$pages = gh api --paginate --slurp[\s\S]+ConvertFrom-Json/);
  assert.doesNotMatch(runbook, /--slurp[\s\S]{0,250}--jq/);
  assert.match(runbook, /exact\s+squash-merged controller SHA[\s\S]+only trusted controller revision/);
  assert.match(runbook, /GET \/repos\/Jamula\/Andreja\/issues\/events/);
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
  assert.match(runbook, /mergeCommitProof\.verified=true/);
  assert.match(runbook, /at most eight concurrent requests/);
  assert.match(runbook, /exact canonical GitHub login casing/);
  assert.match(runbook, /static workflow-expression and fixture assertions\s+only/);
  assert.match(runbook, /32924008713[\s\S]+fab046f6608fc93b032ed7e618b57f2547c88bdc[\s\S]+pre-trusted-classifier-gate/);
  assert.doesNotMatch(runbook, /ordinary\s+unlabelled bootstrap/);
  assert.doesNotMatch(runbook, /cb7a434|95d7450|N <= 25|N >= 20|81 minutes|94 minutes|118 minutes|1,060|every 12 hours|remaining seven|five eligible docs|five full/);
  assert.match(testingMatrix, /maximum of smoke, docs, and full trusted runs/);
  assert.match(testingMatrix, /only `labeled` and `workflow_dispatch`/);
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
