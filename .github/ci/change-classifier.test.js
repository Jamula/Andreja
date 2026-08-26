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
  inspectMarkdownPatch,
  loadPolicy,
  paginatedFiles,
} = require('./change-classifier');

const root = path.resolve(__dirname, '../..');
const policy = loadPolicy(path.join(__dirname, 'change-policy.v1.json'));
const fixtures = require('./fixtures/change-classifier-cases.json');
const { classificationName, estimatedMinutes } = require('./replay-merged-prs');

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
      { body: { merge_base_commit: { sha: 'c'.repeat(40) } }, link: null },
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
    'pull_request',
    {
      number: 102,
      pull_request: {
        changed_files: 2,
        base: { sha: 'a'.repeat(40) },
        head: { sha: 'b'.repeat(40) },
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

test('captured PR 103 Markdown patch counts all bullet body lines exactly', () => {
  const file = require('./fixtures/pr-103-markdown.json');
  const inspection = inspectMarkdownPatch(file);
  assert.equal(inspection.changedLineCount, 372);
  assert.equal(inspection.uncertain, undefined);
  assert.equal(inspection.executable, false);
  const result = classifyFiles([file], policy);
  assert.equal(result.fullSuite, false);
  assert.deepEqual(
    ALL_DOMAINS.filter((domain) => result.domains[domain].selected),
    ['docs'],
  );
});

test('workflow isolates event concurrency and handles zero-before push safely', () => {
  const workflow = fs.readFileSync(
    path.join(root, '.github/workflows/selective-ci-shadow.yml'),
    'utf8',
  );
  assert.match(workflow, /group: selective-ci-shadow-\$\{\{ github\.event_name \}\}-/);
  assert.equal(
    workflow.match(/github\.event\.before != '0000000000000000000000000000000000000000'/g)?.length,
    2,
  );
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

test('recorded historical replay is internally consistent with the current policy', () => {
  const evidence = require('./evidence/recent-merged-pr-replay.json');
  const policyBytes = fs
    .readFileSync(path.join(__dirname, 'change-policy.v1.json'), 'utf8')
    .replace(/\r\n/g, '\n');
  assert.equal(evidence.sample.count, 20);
  assert.equal(
    Object.values(evidence.portfolio.counts).reduce((sum, count) => sum + count, 0),
    evidence.sample.count,
  );
  assert.equal(evidence.portfolio.counts['docs-only'], 2);
  assert.equal(evidence.portfolio.counts.partial, 1);
  assert.equal(evidence.portfolio.expectedRoundedMinutesSaved, 28);
  assert.equal(evidence.portfolio.expectedSavingsShare, 0.0875);
  assert.equal(
    evidence.policySha256,
    createHash('sha256').update(policyBytes).digest('hex'),
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
