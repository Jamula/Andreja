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
  LABELS,
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
  markLandedOnDefaultBranch,
  markVerifiedPromotions,
  normalizePullRequest,
  run,
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

test('branch documentation uses the Squad v0.13.0 issue convention', () => {
  const documentedPaths = [
    path.join(__dirname, '..', 'copilot-instructions.md'),
    path.join(
      __dirname,
      '..',
      '..',
      '.squad',
      'templates',
      'copilot-instructions.md'),
    path.join(__dirname, '..', '..', '.squad', 'templates', 'issue-lifecycle.md'),
  ];

  for (const documentedPath of documentedPaths) {
    const content = fs.readFileSync(documentedPath, 'utf8');
    assert.match(content, /squad\/\{issue-number\}-\{kebab-case-slug\}/);
    assert.doesNotMatch(content, /squad\/(?:\*|\{slug\})/);
  }

  const copilotBranchDocumentationPaths = [
    path.join(__dirname, '..', 'agents', 'squad.agent.md'),
    path.join(__dirname, '..', '..', '.squad', 'templates', 'squad.agent.md.template'),
    path.join(__dirname, '..', '..', '.squad', 'templates', 'copilot-agent.md'),
  ];
  for (const documentedPath of copilotBranchDocumentationPaths) {
    const content = fs.readFileSync(documentedPath, 'utf8');
    assert.match(content, /copilot\/\{issue-number\}-\{slug\}/);
    assert.doesNotMatch(content, /copilot\/\*/);
  }

  const statusDocumentation = fs.readFileSync(
    path.join(__dirname, '..', '..', 'docs', 'help', 'issue-status.md'),
    'utf8',
  );
  assert.match(statusDocumentation, /squad\/\{issue-number\}-\{slug\}/);
  assert.match(statusDocumentation, /copilot\/\{issue-number\}-\{slug\}/);
  assert.match(statusDocumentation, /u\/\{account\}\/\{issue-number\}-\{slug\}/);

  const workflowPaths = [
    path.join(__dirname, '..', 'workflows', 'squad-issue-assign.yml'),
    path.join(
      __dirname,
      '..',
      '..',
      '.squad',
      'templates',
      'workflows',
      'squad-issue-assign.yml'),
  ];
  for (const workflowPath of workflowPaths) {
    const content = fs.readFileSync(workflowPath, 'utf8');
    assert.match(content, /copilot\/\$\{issue\.number\}-\{slug\}/);
    assert.doesNotMatch(content, /copilot\/\*/);
  }
});

test('Copilot instruction sources use the Squad v0.13.0 branch convention', () => {
  const instructionPaths = [
    path.join(__dirname, '..', 'copilot-instructions.md'),
    path.join(
      __dirname,
      '..',
      '..',
      '.squad',
      'templates',
      'copilot-instructions.md'),
  ];

  for (const instructionPath of instructionPaths) {
    const content = fs.readFileSync(instructionPath, 'utf8');
    assert.match(
      content,
      /Use the squad branch convention:\s*```\s*squad\/\{issue-number\}-\{kebab-case-slug\}\s*```/);
    assert.doesNotMatch(content, /Use the Copilot coding-agent branch convention:/i);
  }
});

test('squad issue assignment matches slugged multi-word member labels', () => {
  const workflowPaths = [
    path.join(__dirname, '..', 'workflows', 'squad-issue-assign.yml'),
    path.join(
      __dirname,
      '..',
      '..',
      '.squad',
      'templates',
      'workflows',
      'squad-issue-assign.yml'),
  ];

  for (const workflowPath of workflowPaths) {
    const content = fs.readFileSync(workflowPath, 'utf8');
    const slugifySource = content.match(
      /function slugify\(value\) \{\s+return [^;]+;\s+\}/);
    assert.ok(slugifySource, `${workflowPath} must define slugify`);
    const slugify = Function(`return (${slugifySource[0]})`)();
    assert.equal(slugify('Deanna Troi'), 'deanna-troi');
    assert.equal(slugify('Jett Reno'), 'jett-reno');
    assert.match(
      content,
      /slugify\(cells\[0\]\) === memberName/,
      `${workflowPath} must match slugged labels for multi-word roster names`);
  }
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

test('sidebar-linked PR pagination reconciles the 101st PR', async () => {
  let calls = 0;
  const github = {
    graphql: async (_query, variables) => {
      calls += 1;
      const firstPage = variables.cursor === null;
      return {
        repository: {
          issue: {
            closedByPullRequestsReferences: {
              nodes: firstPage
                ? Array.from({ length: 100 }, (_, index) => ({
                  number: index + 1,
                  repository: { nameWithOwner: 'Jamula/Andreja' },
                }))
                : [{
                  number: 101,
                  repository: { nameWithOwner: 'Jamula/Andreja' },
                }],
              pageInfo: firstPage
                ? { hasNextPage: true, endCursor: 'page-100' }
                : { hasNextPage: false, endCursor: 'page-101' },
            },
          },
        },
      };
    },
  };
  const context = {
    repo: { owner: 'Jamula', repo: 'Andreja' },
    payload: { repository: { full_name: 'Jamula/Andreja' } },
  };
  const connectedNumbers = await linkedPullRequestNumbers({
    github,
    context,
    issueNumber: 70,
  });
  const pullRequests = selectPullRequestsForIssue({
    pullRequests: [{
      number: 101,
      state: 'open',
      isDraft: true,
      merged: false,
      baseRef: 'main',
      headRef: 'feature/page-101',
      headRepository: 'Jamula/Andreja',
      body: '',
    }],
    issueNumber: 70,
    repository: context.repo,
    repositoryFullName: 'Jamula/Andreja',
    defaultBranch: 'main',
    connectedNumbers,
  });

  assert.equal(calls, 2);
  assert.ok(connectedNumbers.has(101));
  assert.equal(deriveStatus({
    issueState: 'open',
    pullRequests,
    defaultBranch: 'main',
  }), STATUS.PR_DRAFT);
});

test('closing issue pagination includes the 101st issue', async () => {
  let calls = 0;
  const github = {
    graphql: async (_query, variables) => {
      calls += 1;
      const firstPage = variables.cursor === null;
      return {
        repository: {
          pullRequest: {
            closingIssuesReferences: {
              nodes: firstPage
                ? Array.from({ length: 100 }, (_, index) => ({
                  number: index + 1,
                  repository: { nameWithOwner: 'Jamula/Andreja' },
                }))
                : [{
                  number: 101,
                  repository: { nameWithOwner: 'Jamula/Andreja' },
                }],
              pageInfo: firstPage
                ? { hasNextPage: true, endCursor: 'page-100' }
                : { hasNextPage: false, endCursor: 'page-101' },
            },
          },
        },
      };
    },
  };
  const numbers = await closingIssueNumbers({
    github,
    context: { repo: { owner: 'Jamula', repo: 'Andreja' } },
    pullRequest: { number: 10, body: '' },
  });

  assert.equal(calls, 2);
  assert.equal(numbers.size, 101);
  assert.ok(numbers.has(101));
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
    mergeCommitSha: 'layer',
  };
  const promotion = {
    number: 11,
    state: 'closed',
    merged: true,
    mergedAt: '2026-08-24T21:00:00Z',
    baseRef: 'main',
    headRef: 'stack/integration',
    headRepository: 'Jamula/Andreja',
    promotionSourceShas: new Set(['layer']),
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
    mergeCommitSha: 'layer',
  };
  const stalePromotion = {
    number: 11,
    state: 'closed',
    merged: true,
    mergedAt: '2026-08-24T20:00:00Z',
    baseRef: 'main',
    headRef: 'stack/integration',
    headRepository: 'Jamula/Andreja',
    promotionSourceShas: new Set(['layer']),
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
    mergeCommitSha: 'layer',
  };
  const promotion = {
    number: 11,
    state: 'closed',
    merged: true,
    mergedAt: '2026-08-24T21:00:00Z',
    baseRef: 'main',
    headRef: 'stack/integration',
    headRepository: 'Jamula/Andreja',
    promotionSourceShas: new Set(['layer']),
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

test('reopen at the exact merge second remains authoritative', () => {
  const mergedPullRequest = {
    number: 10,
    state: 'closed',
    merged: true,
    mergedAt: '2026-08-24T21:00:00Z',
    baseRef: 'main',
    headRef: 'feature/merged',
    headRepository: 'Jamula/Andreja',
  };
  const expanded = expandPullRequestPromotions(
    [mergedPullRequest],
    [mergedPullRequest],
    'main',
    'Jamula/Andreja',
    '2026-08-24T21:00:00Z');

  assert.equal(expanded[0].supersededByReopen, true);
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

test('stacked PR merged into a non-default base counts only when its merge commit reached the default branch', async () => {
  const statuses = { landed: 'ahead', same: 'identical', stray: 'diverged', behind: 'behind' };
  const compared = [];
  const github = {
    rest: { repos: { compareCommitsWithBasehead: async ({ basehead }) => {
      compared.push(basehead);
      const sha = basehead.split('...')[0];
      return { data: { status: statuses[sha] } };
    } } },
  };
  const make = (number, sha, extra = {}) => ({
    number,
    state: 'closed',
    draft: false,
    merged_at: '2026-08-24T20:00:00Z',
    base: { ref: 'squad/stack' },
    head: { ref: `squad/layer-${number}`, repo: { full_name: 'o/r' } },
    body: 'Closes #70',
    merge_commit_sha: sha,
    ...extra,
  });
  const rawPullRequests = [
    make(1, 'landed'), make(2, 'same'), make(3, 'stray'), make(4, 'behind'),
    make(5, undefined), make(6, 'landed', { merged_at: null }),
    make(7, 'landed', { base: { ref: 'main' } }),
  ];
  const pullRequests = rawPullRequests.map(normalizePullRequest);
  assert.equal(pullRequests[0].mergeCommitSha, rawPullRequests[0].merge_commit_sha);
  assert.equal(pullRequests[0].merged, true);
  assert.equal(pullRequests[5].merged, false);
  const result = await markLandedOnDefaultBranch({
    github,
    context: { repo: { owner: 'o', repo: 'r' } },
    defaultBranch: 'main',
    pullRequests,
  });

  assert.deepEqual(result.map((pr) => pr.baseRef),
    ['main', 'main', 'squad/stack', 'squad/stack', 'squad/stack', 'squad/stack', 'main']);
  assert.equal(result[0].originalBaseRef, 'squad/stack');
  assert.deepEqual(compared, [
    'landed...main', 'same...main', 'stray...main', 'behind...main',
  ]);
  assert.equal(deriveStatus({ pullRequests: [result[0]] }), STATUS.MERGED);
  assert.equal(deriveStatus({ pullRequests: [result[2]] }), STATUS.BRANCH_ONLY);
});

test('merge commit comparisons bound deferred requests and deduplicate SHAs', async () => {
  const pending = [];
  const compared = [];
  let active = 0;
  let peak = 0;
  const github = {
    rest: { repos: { compareCommitsWithBasehead: ({ basehead }) => {
      compared.push(basehead);
      active += 1;
      peak = Math.max(peak, active);
      return new Promise((resolve) => pending.push(() => {
        active -= 1;
        resolve({ data: { status: 'ahead' } });
      }));
    } } },
  };
  const shas = ['one', 'one', 'two', 'three', 'two', 'four'];
  const resultPromise = markLandedOnDefaultBranch({
    github,
    context: { repo: { owner: 'o', repo: 'r' } },
    defaultBranch: 'main',
    pullRequests: shas.map((sha, index) => ({
      number: index + 1, merged: true, baseRef: 'stack/base', mergeCommitSha: sha,
    })),
  });

  for (const sha of ['one', 'two', 'three', 'four']) {
    // Allow continuations to run while the current compare remains deferred.
    await new Promise((resolve) => setImmediate(resolve));
    assert.equal(active, 1);
    assert.equal(pending.length, 1);
    assert.equal(compared.at(-1), `${sha}...main`);
    pending.shift()();
  }
  const result = await resultPromise;
  assert.equal(peak, 1);
  assert.equal(active, 0);
  assert.deepEqual(compared, ['one...main', 'two...main', 'three...main', 'four...main']);
  assert.deepEqual(result.map((pr) => pr.baseRef), shas.map(() => 'main'));
});

test('promotion ancestry requires a recorded head and accepts only ahead or identical', async () => {
  const compared = [];
  const layer = {
    number: 1, merged: true, baseRef: 'stack/base', mergeCommitSha: 'layer',
  };
  const candidates = ['ahead', 'identical', 'behind', 'diverged', undefined]
    .map((headSha, index) => ({
      number: index + 2, state: 'open', headRef: 'stack/base', headSha,
      headRepository: 'o/r',
    }));
  const result = await markVerifiedPromotions({
    github: { rest: { repos: { compareCommitsWithBasehead: async ({ basehead }) => {
      compared.push(basehead);
      return { data: { status: basehead.split('...')[1] } };
    } } } },
    context: { repo: { owner: 'o', repo: 'r' } },
    defaultBranch: 'main',
    repositoryFullName: 'o/r',
    pullRequests: [layer, ...candidates,
      { ...layer, number: 9, mergeCommitSha: undefined },
      { ...candidates[0], number: 10, headRepository: 'other/fork' }],
  });
  assert.deepEqual(compared,
    ['layer...ahead', 'layer...identical', 'layer...behind', 'layer...diverged']);
  assert.deepEqual(result.map((pr) => [...pr.promotionSourceShas]),
    [[], ['layer'], ['layer'], [], [], [], [], []]);
  const expanded = expandPullRequestPromotions([result[0]], result, 'main', 'o/r');
  assert.deepEqual(expanded.map((pr) => pr.number), [1, 2, 3]);
  assert.equal(deriveStatus({ pullRequests: expanded }), STATUS.READY);
});

test('promotion comparisons are sequential and deduplicate commit pairs', async () => {
  const pending = [];
  const compared = [];
  let active = 0;
  let peak = 0;
  const operation = markVerifiedPromotions({
    github: { rest: { repos: { compareCommitsWithBasehead: ({ basehead }) => {
      compared.push(basehead);
      active += 1;
      peak = Math.max(peak, active);
      return new Promise((resolve) => pending.push(() => {
        active -= 1;
        resolve({ data: { status: 'ahead' } });
      }));
    } } } },
    context: { repo: { owner: 'o', repo: 'r' } },
    defaultBranch: 'main',
    repositoryFullName: 'o/r',
    pullRequests: [
      { number: 1, merged: true, baseRef: 'stack/base', mergeCommitSha: 'layer' },
      ...['one', 'one', 'two'].map((headSha, index) => ({
        number: index + 2, headRef: 'stack/base', headSha, headRepository: 'o/r',
      })),
    ],
  });
  for (const head of ['one', 'two']) {
    await new Promise((resolve) => setImmediate(resolve));
    assert.equal(active, 1);
    assert.equal(pending.length, 1);
    assert.equal(compared.at(-1), `layer...${head}`);
    pending.shift()();
  }
  const result = await operation;
  assert.equal(peak, 1);
  assert.equal(active, 0);
  assert.deepEqual(compared, ['layer...one', 'layer...two']);
  assert.deepEqual(result.map((pr) => [...pr.promotionSourceShas]),
    [[], ['layer'], ['layer'], ['layer']]);
});

test('only the earliest proven default landing promotes a layer across branch reuse', async () => {
  const pullRequests = [
    {
      number: 10, merged: true, mergedAt: '2026-08-24T20:00:00Z',
      baseRef: 'stack/integration', mergeCommitSha: 'layer',
    },
    {
      number: 13, merged: true, mergedAt: '2026-08-24T22:00:00Z',
      baseRef: 'stack/integration', mergeCommitSha: 'new-layer',
    },
    {
      number: 11, merged: true, mergedAt: '2026-08-24T21:00:00Z',
      baseRef: 'main', headRef: 'stack/integration', headSha: 'old-head',
      headRepository: 'o/r',
    },
    {
      number: 12, merged: true, mergedAt: '2026-08-24T23:00:00Z',
      baseRef: 'main', headRef: 'stack/integration', headSha: 'later-head',
      headRepository: 'o/r',
    },
  ];
  const github = {
    rest: { repos: { compareCommitsWithBasehead: async () =>
      ({ data: { status: 'ahead' } }) } },
  };
  const options = {
    github,
    context: { repo: { owner: 'o', repo: 'r' } },
    defaultBranch: 'main',
    repositoryFullName: 'o/r',
  };
  const forward = await markVerifiedPromotions({
    ...options, pullRequests,
  });
  const reverse = await markVerifiedPromotions({
    ...options, pullRequests: [...pullRequests].reverse(),
  });

  for (const result of [forward, reverse]) {
    assert.deepEqual(
      Object.fromEntries(result.map((pr) => [
        pr.number, [...pr.promotionSourceShas],
      ])),
      { 10: [], 11: ['layer'], 12: ['new-layer'], 13: [] });
  }
});

for (const intermediary of [false, true]) {
  for (const reuseState of ['open', 'draft', 'closed', 'merged']) {
    test(`landed stack rejects ${reuseState} ${intermediary ? 'intermediary' : 'default'} branch reuse`, async () => {
      const pullRequests = [
        {
          number: 10, state: 'closed', merged: true,
          mergedAt: '2026-08-24T19:00:00Z',
          baseRef: 'stack/integration', headRef: 'stack/layer',
          headRepository: 'Jamula/Andreja', mergeCommitSha: 'layer',
          body: 'Closes #70',
        },
        {
          number: 11, state: 'closed', merged: true,
          mergedAt: '2026-08-24T20:00:00Z',
          baseRef: 'stack/root', headRef: 'stack/integration',
          headSha: 'old-middle-head', headRepository: 'Jamula/Andreja',
          mergeCommitSha: 'old-middle',
        },
        {
          number: 12, state: 'closed', merged: true,
          mergedAt: '2026-08-24T21:00:00Z',
          baseRef: 'main', headRef: 'stack/root',
          headSha: 'old-root-head', headRepository: 'Jamula/Andreja',
          mergeCommitSha: 'old-root',
        },
        {
          number: 13, state: reuseState === 'open' || reuseState === 'draft'
            ? 'open' : 'closed',
          isDraft: reuseState === 'draft', merged: reuseState === 'merged',
          mergedAt: reuseState === 'merged' ? '2026-08-24T23:00:00Z' : null,
          baseRef: intermediary ? 'stack/reused-root' : 'main',
          headRef: 'stack/integration', headSha: 'reused-middle-head',
          headRepository: 'Jamula/Andreja', mergeCommitSha: 'reused-middle',
        },
        ...(intermediary && reuseState === 'merged' ? [{
          number: 14, state: 'closed', merged: true,
          mergedAt: '2026-08-25T00:00:00Z',
          baseRef: 'main', headRef: 'stack/reused-root',
          headSha: 'reused-root-head', headRepository: 'Jamula/Andreja',
          mergeCommitSha: 'reused-root',
        }] : []),
      ];
      const ancestors = {
        main: ['layer', 'old-middle', 'reused-middle'],
        'old-middle-head': ['layer'],
        'old-root-head': ['layer', 'old-middle'],
        'reused-middle-head': ['layer', 'old-middle'],
        'reused-root-head': ['layer', 'old-middle', 'reused-middle'],
      };
      for (const ordered of [pullRequests, [...pullRequests].reverse()]) {
        const options = {
          github: { rest: { repos: { compareCommitsWithBasehead: async ({ basehead }) => {
            const [source, head] = basehead.split('...');
            return { data: { status: ancestors[head]?.includes(source) ? 'ahead' : 'diverged' } };
          } } } },
          context: { repo: { owner: 'Jamula', repo: 'Andreja' } },
          defaultBranch: 'main',
          repositoryFullName: 'Jamula/Andreja',
        };
        const verified = await markVerifiedPromotions({
          ...options,
          pullRequests: await markLandedOnDefaultBranch({ ...options, pullRequests: ordered }),
        });
        const expanded = selectPullRequestsForIssue({
          pullRequests: verified, issueNumber: 70,
          repository: options.context.repo,
          repositoryFullName: options.repositoryFullName,
          defaultBranch: 'main', latestReopenedAt: '2026-08-24T22:00:00Z',
        });
        assert.equal(deriveStatus({ pullRequests: expanded }), STATUS.BACKLOG);
        assert.deepEqual(expanded.map((pr) => pr.number), [10, 11, 12]);
        const original = selectPullRequestsForIssue({
          pullRequests: verified, issueNumber: 70,
          repository: options.context.repo,
          repositoryFullName: options.repositoryFullName, defaultBranch: 'main',
        });
        assert.equal(deriveStatus({ pullRequests: original }), STATUS.MERGED);
      }
    });
  }
}

for (const intermediary of [false, true]) {
  for (const state of ['open', 'draft', 'merged']) {
    test(`first ${intermediary ? 'intermediary' : 'default'} ${state} promotion after reopen remains visible`, async () => {
      const pullRequests = [
        {
          number: 10, state: 'closed', merged: true,
          mergedAt: '2026-08-24T19:00:00Z',
          baseRef: 'stack/integration', mergeCommitSha: 'layer',
          headRepository: 'Jamula/Andreja', body: 'Closes #70',
        },
        {
          number: 11, state: 'closed', merged: true,
          mergedAt: '2026-08-24T20:00:00Z',
          baseRef: 'stack/root', headRef: 'stack/integration',
          headSha: 'middle-head', mergeCommitSha: 'middle',
          headRepository: 'Jamula/Andreja',
        },
        {
          number: 12, state: state === 'merged' ? 'closed' : 'open',
          merged: state === 'merged', isDraft: state === 'draft',
          mergedAt: state === 'merged' ? '2026-08-24T23:00:00Z' : null,
          baseRef: intermediary ? 'stack/final' : 'main',
          headRef: 'stack/root', headSha: 'root-head',
          mergeCommitSha: 'root',
          headRepository: 'Jamula/Andreja',
        },
        ...(intermediary && state === 'merged' ? [{
          number: 13, state: 'closed', merged: true,
          mergedAt: '2026-08-25T00:00:00Z',
          baseRef: 'main', headRef: 'stack/final', headSha: 'final-head',
          headRepository: 'Jamula/Andreja',
        }] : []),
      ];
      const options = {
        github: { rest: { repos: { compareCommitsWithBasehead: async ({ basehead }) =>
          ({ data: { status: basehead.endsWith('...main') && state !== 'merged'
            ? 'diverged' : 'ahead' } }) } } },
        context: { repo: { owner: 'Jamula', repo: 'Andreja' } },
        defaultBranch: 'main', repositoryFullName: 'Jamula/Andreja',
      };
      const verified = await markVerifiedPromotions({
        ...options,
        pullRequests: await markLandedOnDefaultBranch({ ...options, pullRequests }),
      });
      const expanded = selectPullRequestsForIssue({
        pullRequests: verified, issueNumber: 70, repository: options.context.repo,
        repositoryFullName: options.repositoryFullName, defaultBranch: 'main',
        latestReopenedAt: '2026-08-24T22:00:00Z',
      });
      assert.deepEqual(expanded.map((pr) => pr.number),
        intermediary && state === 'merged' ? [10, 11, 12, 13] : [10, 11, 12]);
      assert.equal(deriveStatus({ pullRequests: expanded }),
        state === 'merged' ? STATUS.MERGED : state === 'draft' ? STATUS.PR_DRAFT : STATUS.READY);
    });
  }
}

for (const missing of ['headSha', 'mergeCommitSha', 'mergedAt']) {
  test(`missing ${missing} cannot prove a first transitive landing after reopen`, async () => {
    const pullRequests = [
      {
        number: 10, state: 'closed', merged: true,
        mergedAt: '2026-08-24T19:00:00Z',
        baseRef: 'stack/integration', mergeCommitSha: 'layer',
        headRepository: 'Jamula/Andreja', body: 'Closes #70',
      },
      {
        number: 11, state: 'closed', merged: true,
        mergedAt: '2026-08-24T20:00:00Z',
        baseRef: 'stack/root', headRef: 'stack/integration',
        headSha: 'middle-head', mergeCommitSha: 'middle',
        headRepository: 'Jamula/Andreja',
      },
      {
        number: 12, state: 'closed', merged: true,
        mergedAt: '2026-08-24T23:00:00Z',
        baseRef: 'main', headRef: 'stack/root', headSha: 'root-head',
        headRepository: 'Jamula/Andreja',
      },
    ];
    delete pullRequests[missing === 'mergeCommitSha' ? 1 : 2][missing];
    const options = {
      github: { rest: { repos: { compareCommitsWithBasehead: async () =>
        ({ data: { status: 'ahead' } }) } } },
      context: { repo: { owner: 'Jamula', repo: 'Andreja' } },
      defaultBranch: 'main', repositoryFullName: 'Jamula/Andreja',
    };
    const verified = await markVerifiedPromotions({
      ...options,
      pullRequests: await markLandedOnDefaultBranch({ ...options, pullRequests }),
    });
    const expanded = selectPullRequestsForIssue({
      pullRequests: verified, issueNumber: 70, repository: options.context.repo,
      repositoryFullName: options.repositoryFullName, defaultBranch: 'main',
      latestReopenedAt: '2026-08-24T22:00:00Z',
    });
    assert.equal(deriveStatus({ pullRequests: expanded }), STATUS.BACKLOG);
  });
}

for (const status of [401, 403, 404, 429, 500, 503, undefined]) {
  test(`merge commit comparison failure ${status ?? 'network'} aborts reconciliation`, async () => {
    const error = Object.assign(new Error('comparison unavailable'), { status });
    let calls = 0;
    const github = {
      rest: { repos: { compareCommitsWithBasehead: async () => {
        calls += 1;
        throw error;
      } } },
    };
    await assert.rejects(markLandedOnDefaultBranch({
      github,
      context: { repo: { owner: 'o', repo: 'r' } },
      defaultBranch: 'main',
      pullRequests: ['first', 'second'].map((sha, index) => ({
        number: index + 1, merged: true, baseRef: 'stack/base', mergeCommitSha: sha,
      })),
    }), (actual) => actual === error);
    assert.equal(calls, 1);
  });
  test(`promotion comparison failure ${status ?? 'network'} propagates`, async () => {
    const error = Object.assign(new Error('comparison unavailable'), { status });
    let calls = 0;
    await assert.rejects(markVerifiedPromotions({
      github: { rest: { repos: { compareCommitsWithBasehead: async () => {
        calls += 1;
        throw error;
      } } } },
      context: { repo: { owner: 'o', repo: 'r' } },
      defaultBranch: 'main',
      repositoryFullName: 'o/r',
      pullRequests: [
        { number: 1, merged: true, baseRef: 'stack/base', mergeCommitSha: 'layer' },
        ...['one', 'two'].map((headSha, index) => ({
          number: index + 2, headRef: 'stack/base', headSha, headRepository: 'o/r',
        })),
      ],
    }), (actual) => actual === error);
    assert.equal(calls, 1);
  });
}

for (const scenario of ['landed', 'error', 'reused', 'genuine', 'promotion-error']) {
  const description = {
    landed: 'maps merge_commit_sha before deriving labels',
    error: 'aborts label writes on reachability errors',
    reused: 'old landing plus unrelated post-reopen branch reuse remains backlog',
    genuine: 'ancestry-proven post-reopen promotion remains merged',
    'promotion-error': 'aborts label writes on promotion errors',
  }[scenario];
  test(`production REST evidence loader: ${description}`, async () => {
    const comparisonFails = scenario === 'error' || scenario === 'promotion-error';
    const hasPromotion = ['reused', 'genuine', 'promotion-error'].includes(scenario);
    const listBranches = Symbol('listBranches');
    const listPulls = Symbol('listPulls');
    const listIssues = Symbol('listIssues');
    const listEvents = Symbol('listEvents');
    const writes = [];
    const compared = [];
    const error = Object.assign(new Error('rate limited'), { status: 403 });
    const github = {
      rest: {
        repos: {
          listBranches,
          compareCommitsWithBasehead: async ({ basehead }) => {
            compared.push(basehead);
            if (scenario === 'error' ||
                scenario === 'promotion-error' && basehead.endsWith('...later-head')) {
              throw error;
            }
            return { data: { status:
              scenario === 'genuine' && basehead.endsWith('...old-head')
                ? 'diverged' : 'ahead' } };
          },
        },
        pulls: { list: listPulls },
        issues: {
          listForRepo: listIssues,
          listEventsForTimeline: listEvents,
          getLabel: async ({ name }) => ({
            data: { color: LABELS[name][0], description: LABELS[name][1] },
          }),
          get: async () => ({ data: { number: 70, state: 'open', labels: [] } }),
          addLabels: async (parameters) => writes.push(parameters),
          removeLabel: async (parameters) => writes.push(parameters),
        },
      },
      paginate: async (endpoint) => {
        if (endpoint === listIssues) return [{ number: 70 }];
        if (endpoint === listPulls) return [{
          number: 10,
          state: 'closed',
          draft: false,
          merged_at: '2026-08-24T20:00:00Z',
          base: { ref: 'stack/integration' },
          head: { ref: 'stack/layer', repo: { full_name: 'Jamula/Andreja' } },
          body: 'Closes #70',
          merge_commit_sha: 'raw-rest-merge',
        }, ...(hasPromotion ? [
          {
            number: 11,
            state: 'closed',
            merged_at: '2026-08-24T21:00:00Z',
            base: { ref: 'main' },
            head: {
              ref: 'stack/integration', sha: 'old-head',
              repo: { full_name: 'Jamula/Andreja' },
            },
            merge_commit_sha: 'old-landing',
          },
          {
            number: 12,
            state: 'closed',
            merged_at: '2026-08-24T23:00:00Z',
            base: { ref: 'main' },
            head: {
              ref: 'stack/integration', sha: 'later-head',
              repo: { full_name: 'Jamula/Andreja' },
            },
            merge_commit_sha: 'later-landing',
          },
        ] : [])];
        if (endpoint === listEvents && hasPromotion) {
          return [{ event: 'reopened', created_at: '2026-08-24T22:00:00Z' }];
        }
        if (endpoint === listBranches || endpoint === listEvents ||
            typeof endpoint === 'string' && endpoint.endsWith('/blocked_by')) {
          return [];
        }
        assert.fail(`Unexpected pagination endpoint: ${String(endpoint)}`);
      },
      graphql: async () => ({
        repository: { issue: { closedByPullRequestsReferences: { nodes: [] } } },
      }),
    };
    const operation = run({
      github,
      context: {
        eventName: 'schedule',
        repo: { owner: 'Jamula', repo: 'Andreja' },
        payload: { repository: {
          default_branch: 'main',
          full_name: 'Jamula/Andreja',
          url: 'https://api.github.com/repos/Jamula/Andreja',
        } },
      },
      core: { info: () => {} },
    });
    if (comparisonFails) {
      await assert.rejects(operation, (actual) => actual === error);
      assert.deepEqual(writes, []);
    } else {
      await operation;
      assert.equal(writes.length, 1);
      assert.equal(writes[0].issue_number, 70);
      assert.deepEqual(writes[0].labels,
        [scenario === 'reused' ? STATUS.BACKLOG : STATUS.MERGED]);
    }
    assert.deepEqual(compared, [
      'raw-rest-merge...main',
      ...(hasPromotion ? ['raw-rest-merge...old-head', 'raw-rest-merge...later-head'] : []),
    ]);
  });
}

for (const intermediary of [false, true]) {
  for (const parentMergedAt of [
    '2026-08-24T21:00:00Z',
    '2026-08-24T22:00:00Z',
    '2026-08-24T23:00:00Z',
  ]) {
    test(`reachable ${intermediary ? 'intermediary' : 'direct'} stack parent at ${parentMergedAt} respects reopen`, async () => {
      const layer = normalizePullRequest({
        number: 10,
        state: 'closed',
        merged_at: '2026-08-24T19:00:00Z',
        base: { ref: 'stack/integration' },
        head: { ref: 'stack/layer', repo: { full_name: 'Jamula/Andreja' } },
        body: 'Closes #70',
        merge_commit_sha: 'layer',
      });
      const middle = {
        number: 11,
        state: 'closed',
        merged: true,
        mergedAt: '2026-08-24T20:00:00Z',
        baseRef: 'stack/root',
        headRef: 'stack/integration',
        headSha: 'middle-head',
        headRepository: 'Jamula/Andreja',
        mergeCommitSha: 'middle',
      };
      const parent = {
        number: 12,
        state: 'closed',
        merged: true,
        mergedAt: parentMergedAt,
        baseRef: 'main',
        headRef: intermediary ? 'stack/root' : 'stack/integration',
        headSha: 'parent-head',
        headRepository: 'Jamula/Andreja',
      };
      const github = {
        rest: { repos: { compareCommitsWithBasehead: async () =>
          ({ data: { status: 'ahead' } }) } },
      };
      const landedPullRequests = await markLandedOnDefaultBranch({
        github,
        context: { repo: { owner: 'Jamula', repo: 'Andreja' } },
        defaultBranch: 'main',
        pullRequests: intermediary ? [layer, middle, parent] : [layer, parent],
      });
      const pullRequests = await markVerifiedPromotions({
        github,
        context: { repo: { owner: 'Jamula', repo: 'Andreja' } },
        defaultBranch: 'main',
        repositoryFullName: 'Jamula/Andreja',
        pullRequests: landedPullRequests,
      });
      assert.equal(pullRequests[0].baseRef, 'main');
      assert.equal(pullRequests[0].originalBaseRef, 'stack/integration');
      const expanded = selectPullRequestsForIssue({
        pullRequests,
        issueNumber: 70,
        repository: { owner: 'Jamula', repo: 'Andreja' },
        repositoryFullName: 'Jamula/Andreja',
        defaultBranch: 'main',
        latestReopenedAt: '2026-08-24T22:00:00Z',
      });

      assert.deepEqual(expanded.map((pr) => pr.number),
        intermediary ? [10, 11, 12] : [10, 12]);
      assert.equal(expanded[0].supersededByReopen, true);
      if (intermediary) {
        assert.equal(expanded[1].originalBaseRef, 'stack/root');
        assert.equal(expanded[1].supersededByReopen, true);
      }
      assert.equal(expanded.at(-1).supersededByReopen,
        parentMergedAt <= '2026-08-24T22:00:00Z');
      assert.equal(deriveStatus({ pullRequests: expanded }),
        parentMergedAt > '2026-08-24T22:00:00Z' ? STATUS.MERGED : STATUS.BACKLOG);
    });
  }
}
