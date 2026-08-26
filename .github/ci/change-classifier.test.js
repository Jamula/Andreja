'use strict';

const assert = require('node:assert/strict');
const { execFileSync } = require('node:child_process');
const path = require('node:path');
const test = require('node:test');
const {
  ALL_DOMAINS,
  acquireChanges,
  classifyFiles,
  loadPolicy,
  paginatedFiles,
} = require('./change-classifier');

const root = path.resolve(__dirname, '../..');
const policy = loadPolicy(path.join(__dirname, 'change-policy.v1.json'));
const fixtures = require('./fixtures/change-classifier-cases.json');

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
    .map((filename) => ({ filename, status: 'modified', patch: '@@\n-old prose\n+new prose' }));
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
        body: [{ filename: 'docs/a.md', status: 'modified', patch: '@@\n-a\n+b' }],
        link: '<https://api.github.com/page/2>; rel="next"',
      },
    ],
    [
      'https://api.github.com/page/2',
      { body: [{ filename: 'src/App.cs', status: 'added' }], link: null },
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
    { number: 102, pull_request: { changed_files: 2 } },
    'Jamula/Andreja',
    'read-only-token',
    fetchImpl,
  );
  assert.equal(result.files.length, 2);
  assert.deepEqual(result.forcedFullReasons, []);
  assert.equal(calls.length, 2);
});

test('PR metadata count mismatch fails closed on silent API truncation', async () => {
  const fetchImpl = async () => ({
    ok: true,
    status: 200,
    statusText: 'OK',
    headers: { get: () => null },
    json: async () => [{ filename: 'docs/visible.md', status: 'modified', patch: '@@\n-a\n+b' }],
  });
  const changes = await acquireChanges(
    'pull_request',
    { number: 102, pull_request: { changed_files: 3001 } },
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
