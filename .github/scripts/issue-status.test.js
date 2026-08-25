'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');

const {
  STATUS,
  KNOWN_STATUSES,
  deriveStatus,
  normalizeStatus,
  resolveEvent,
} = require('./issue-status');
const {
  branchNamesByIssue,
  closingIssueNumbers,
  expandPullRequestPromotions,
  isTrustedPullRequestReference,
  issueNumberFromBranch,
  issueNumbersFromBody,
  localIssueNumbers,
  pullRequestIssueNumbers,
  pullRequestNumbersFromTimeline,
  linkedPullRequestNumbers,
  selectPullRequestsForIssue,
  targetIssueNumbers,
  updateBranchEvidence,
} = require('./run-issue-status');

const fixtures = JSON.parse(fs.readFileSync(
  path.join(__dirname, 'fixtures', 'issue-status-events.json'),
  'utf8'));
const linkedPullRequestFixtures = JSON.parse(fs.readFileSync(
  path.join(__dirname, 'fixtures', 'issue-status-linked-prs.json'),
  'utf8'));

for (const fixture of fixtures) {
  test(fixture.name, () => {
    const result = resolveEvent(fixture);
    assert.equal(result.desiredStatus, fixture.expectedStatus);
    assert.deepEqual(result.blockerLabels, fixture.expectedBlockers || []);

    const resultingStatuses = new Set([
      ...fixture.currentLabels
        .map((label) => typeof label === 'string' ? label : label.name)
        .filter((label) => label.startsWith('status:') &&
          !result.plan.remove.includes(label)),
      ...result.plan.add.filter((label) => label.startsWith('status:')),
    ]);
    assert.deepEqual([...resultingStatuses], [fixture.expectedStatus]);

    for (const label of fixture.expectedRemoved || []) {
      assert.ok(result.plan.remove.includes(label));
    }

    for (const label of fixture.expectedPreserved || []) {
      assert.ok(!result.plan.remove.includes(label));
    }
  });
}

for (const fixture of linkedPullRequestFixtures) {
  test(fixture.name, () => {
    const pullRequests = selectPullRequestsForIssue({
      pullRequests: fixture.pullRequests,
      issueNumber: fixture.issueNumber,
      repository: { owner: 'Jamula', repo: 'Andreja' },
      repositoryFullName: 'Jamula/Andreja',
      defaultBranch: 'main',
      connectedNumbers: new Set(fixture.connectedNumbers),
    });
    assert.equal(deriveStatus({
      issueState: fixture.issueState,
      pullRequests,
      defaultBranch: 'main',
    }), fixture.expectedStatus);
  });
}

test('sidebar linkage does not let an open fork PR write issue status', () => {
  const pullRequests = selectPullRequestsForIssue({
    pullRequests: [{
      number: 10,
      state: 'open',
      isDraft: false,
      merged: false,
      baseRef: 'main',
      headRef: 'feature/ready',
      headRepository: 'Contributor/Fork',
      body: '',
    }],
    issueNumber: 70,
    repository: { owner: 'Jamula', repo: 'Andreja' },
    repositoryFullName: 'Jamula/Andreja',
    defaultBranch: 'main',
    connectedNumbers: new Set([10]),
  });
  assert.deepEqual(pullRequests, []);
});

test('all known statuses normalize and unknown statuses fail closed', () => {
  for (const status of KNOWN_STATUSES) {
    assert.equal(normalizeStatus(status), status);
  }
  assert.throws(() => normalizeStatus('mystery'), /Unknown lifecycle status/);
});

test('branch parser accepts the documented convention only', () => {
  assert.equal(issueNumberFromBranch('squad/70-fix-status'), 70);
  assert.equal(issueNumberFromBranch('copilot/71-fix-status'), 71);
  assert.equal(issueNumberFromBranch('u/cyrusjamula/72-fix-status'), 72);
  assert.equal(issueNumberFromBranch('70-experiment'), null);
  assert.equal(issueNumberFromBranch('feature/70-fix-status'), null);
  assert.equal(issueNumberFromBranch('u/cyrusjamula/fix-status'), null);
  assert.equal(issueNumberFromBranch('feature/no-issue'), null);
  assert.equal(issueNumberFromBranch('feature/v2-status'), null);
});

test('closing keyword parser accepts local and rejects cross-repository references', () => {
  assert.deepEqual(
    [...issueNumbersFromBody(
      'Closes #70\nFixes Jamula/Andreja#71\nResolves: #72\nCloses Other/Andreja#73',
      { owner: 'Jamula', repo: 'Andreja' })],
    [70, 71, 72]);
});

test('edited PR targets issues from both current and previous bodies', () => {
  const numbers = pullRequestIssueNumbers({
    action: 'edited',
    pull_request: { body: 'Closes: #71' },
    changes: { body: { from: 'Fixes: #70' } },
  }, { owner: 'Jamula', repo: 'Andreja' });
  assert.deepEqual([...numbers], [71, 70]);
});

test('GraphQL closing references reject cross-repository number collisions', async () => {
  const github = {
    graphql: async () => ({
      repository: {
        pullRequest: {
          closingIssuesReferences: {
            nodes: [
              {
                number: 70,
                repository: { nameWithOwner: 'Other/Elsewhere' },
              },
              {
                number: 71,
                repository: { nameWithOwner: 'Jamula/Andreja' },
              },
            ],
          },
        },
      },
    }),
  };
  const context = { repo: { owner: 'Jamula', repo: 'Andreja' } };
  const numbers = await closingIssueNumbers({
    github,
    context,
    pullRequest: { number: 10, body: 'Closes #72' },
  });

  assert.deepEqual([...numbers], [72, 71]);
});

test('sidebar-linked PR query rejects cross-repository number collisions', async () => {
  const github = {
    graphql: async () => ({
      repository: {
        issue: {
          closedByPullRequestsReferences: {
            nodes: [
              {
                number: 10,
                repository: { nameWithOwner: 'Other/Elsewhere' },
              },
              {
                number: 11,
                repository: { nameWithOwner: 'Jamula/Andreja' },
              },
            ],
          },
        },
      },
    }),
  };
  const context = {
    repo: { owner: 'Jamula', repo: 'Andreja' },
    payload: { repository: { full_name: 'Jamula/Andreja' } },
  };

  assert.deepEqual(
    [...await linkedPullRequestNumbers({ github, context, issueNumber: 70 })],
    [11]);
});

test('timeline PR references ignore cross-repository number collisions', () => {
  const repositoryUrl = 'https://api.github.com/repos/Jamula/Andreja';
  const events = [
    {
      event: 'cross-referenced',
      source: {
        issue: {
          number: 70,
          repository_url: repositoryUrl,
          pull_request: {},
        },
      },
    },
    {
      event: 'cross-referenced',
      source: {
        issue: {
          number: 70,
          repository_url: 'https://api.github.com/repos/Other/Elsewhere',
          pull_request: {},
        },
      },
    },
  ];
  assert.deepEqual(
    [...pullRequestNumbersFromTimeline(events, repositoryUrl)],
    [70]);
});

test('dependent expansion ignores cross-repository number collisions', () => {
  const repositoryUrl = 'https://api.github.com/repos/Jamula/Andreja';
  const issues = [
    { number: 80, repository_url: repositoryUrl },
    {
      number: 80,
      repository_url: 'https://api.github.com/repos/Other/Elsewhere',
    },
  ];
  assert.deepEqual(localIssueNumbers(issues, repositoryUrl), [80]);
});

test('scheduled evidence excludes open fork PR bodies', () => {
  const timelineNumbers = new Set([10, 11]);
  assert.equal(isTrustedPullRequestReference({
    number: 10,
    merged: false,
    headRepository: 'Other/Fork',
  }, timelineNumbers, 'Jamula/Andreja'), false);
  assert.equal(isTrustedPullRequestReference({
    number: 11,
    merged: true,
    headRepository: 'Other/Fork',
  }, timelineNumbers, 'Jamula/Andreja'), true);
  assert.equal(isTrustedPullRequestReference({
    number: 12,
    merged: false,
    headRepository: 'Jamula/Andreja',
  }, timelineNumbers, 'Jamula/Andreja'), true);
});

test('deleting one issue branch preserves other configured branch evidence', () => {
  const evidence = {
    branchNamesByIssue: branchNamesByIssue([
      'squad/70-one',
      'copilot/70-two',
    ]),
  };

  updateBranchEvidence(evidence, 'delete', 'squad/70-one');
  assert.deepEqual(
    [...evidence.branchNamesByIssue.get(70)],
    ['copilot/70-two']);

  updateBranchEvidence(evidence, 'delete', 'copilot/70-two');
  assert.equal(evidence.branchNamesByIssue.has(70), false);
});

test('stack promotion follows a merged base branch to the default branch', () => {
  const layer = {
    number: 10,
    merged: true,
    mergedAt: '2026-08-24T20:00:00Z',
    baseRef: 'stack/integration',
    headRef: 'stack/layer',
    headRepository: 'Jamula/Andreja',
  };
  const promotion = {
    number: 11,
    state: 'closed',
    merged: true,
    mergedAt: '2026-08-24T21:00:00Z',
    baseRef: 'main',
    headRef: 'stack/integration',
    headRepository: 'Jamula/Andreja',
  };
  const collision = {
    number: 11,
    state: 'closed',
    merged: true,
    mergedAt: '2026-08-24T21:00:00Z',
    baseRef: 'main',
    headRef: 'stack/integration',
    headRepository: 'Other/Elsewhere',
  };
  const expanded = expandPullRequestPromotions(
    [layer],
    [layer, promotion, collision],
    'main',
    'Jamula/Andreja');

  assert.deepEqual(expanded.map((pullRequest) => pullRequest.number), [10, 11]);
  assert.equal(deriveStatus({
    issueState: 'open',
    pullRequests: expanded,
    defaultBranch: 'main',
  }), STATUS.MERGED);
});

test('stack promotion rejects a default merge older than the issue layer', () => {
  const layer = {
    number: 10,
    merged: true,
    mergedAt: '2026-08-24T21:00:00Z',
    baseRef: 'stack/integration',
    headRef: 'stack/layer',
    headRepository: 'Jamula/Andreja',
  };
  const stalePromotion = {
    number: 11,
    state: 'closed',
    merged: true,
    mergedAt: '2026-08-24T20:00:00Z',
    baseRef: 'main',
    headRef: 'stack/integration',
    headRepository: 'Jamula/Andreja',
  };

  assert.deepEqual(
    expandPullRequestPromotions(
      [layer],
      [layer, stalePromotion],
      'main',
      'Jamula/Andreja').map((pullRequest) => pullRequest.number),
    [10]);
});

test('reopening after a stack promotion returns the issue to backlog', () => {
  const layer = {
    number: 10,
    merged: true,
    mergedAt: '2026-08-24T20:00:00Z',
    baseRef: 'stack/integration',
    headRef: 'stack/layer',
    headRepository: 'Jamula/Andreja',
  };
  const promotion = {
    number: 11,
    state: 'closed',
    merged: true,
    mergedAt: '2026-08-24T21:00:00Z',
    baseRef: 'main',
    headRef: 'stack/integration',
    headRepository: 'Jamula/Andreja',
  };
  const expanded = expandPullRequestPromotions(
    [layer],
    [layer, promotion],
    'main',
    'Jamula/Andreja',
    '2026-08-24T22:00:00Z');

  assert.equal(deriveStatus({
    issueState: 'open',
    pullRequests: expanded,
    defaultBranch: 'main',
  }), STATUS.BACKLOG);
});

test('scheduled reconciliation is idempotent after removing stale labels', () => {
  const fixture = fixtures.find(
    ({ name }) => name === 'scheduled reconciliation repairs stale labels');
  const first = resolveEvent(fixture);
  const labels = fixture.currentLabels
    .filter((label) => !first.plan.remove.includes(label))
    .concat(first.plan.add);
  const second = resolveEvent({ ...fixture, currentLabels: labels });

  assert.deepEqual(second.plan, { add: [], remove: [] });
});

test('scheduled reconciliation targets every issue but not pull requests', async () => {
  const github = {
    rest: { issues: { listForRepo: Symbol('listForRepo') } },
    paginate: async () => [
      { number: 70 },
      { number: 71, pull_request: {} },
      { number: 72 },
    ],
  };
  const context = {
    eventName: 'schedule',
    payload: { repository: { url: 'https://api.github.com/repos/Jamula/Andreja' } },
    repo: { owner: 'Jamula', repo: 'Andreja' },
  };

  assert.deepEqual(
    [...await targetIssueNumbers({ github, context })],
    [70, 72]);
});

test('write workflow executes only trusted pinned automation with least privilege', () => {
  const workflow = fs.readFileSync(
    path.join(__dirname, '..', 'workflows', 'issue-status.yml'),
    'utf8');

  assert.match(workflow, /ref: \$\{\{ github\.event\.repository\.default_branch \}\}/);
  assert.match(workflow, /persist-credentials: false/);
  assert.match(
    workflow,
    /github\.event\.pull_request\.head\.repo\.full_name == github\.repository/);
  assert.match(workflow, /contents: read/);
  assert.match(workflow, /issues: write/);
  assert.match(workflow, /pull-requests: read/);
  assert.match(workflow, /schedule:\s*\n\s+- cron: '17 \* \* \* \*'/);
  assert.doesNotMatch(workflow, /contents: write/);
  assert.doesNotMatch(workflow, /createComment/);

  const actionReferences = [...workflow.matchAll(/uses:\s+\S+@(\S+)/g)];
  assert.ok(actionReferences.length > 0);
  for (const [, reference] of actionReferences) {
    assert.match(reference, /^[0-9a-f]{40}$/);
  }
});
