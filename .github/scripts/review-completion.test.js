'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');

const {
  COPILOT_LOGIN,
  breakGlassArtifactName,
  breakGlassRunTitle,
  evaluateReviewCompletion,
  latestCopilotReview,
  pullIdentity,
  requiredDomains,
  reviewMarker,
  reviewMarkers,
} = require('./review-completion');
const {
  crc32,
  domainEvidence,
  evaluatePullRequest,
  evaluateSnapshot,
  listReviewThreads,
  readZipJson,
  sanitizedApiFailure,
} = require('./run-review-completion');
const {
  BREAK_GLASS_CONFIRMATION,
  recordBreakGlass,
} = require('./record-review-evidence');
const {
  REQUIRED_WORKFLOW_PATH,
  planRollback,
  planRollout,
} = require('./review-gate-ruleset');

const fixtures = JSON.parse(fs.readFileSync(
  path.join(__dirname, 'fixtures', 'review-completion-scenarios.json'),
  'utf8'));
const HEAD = 'a'.repeat(40);
const OLD_HEAD = 'b'.repeat(40);
const BASE = 'c'.repeat(40);
const OTHER_BASE = 'd'.repeat(40);
const REPOSITORY_ID = 1342901808;

function context(eventName = 'pull_request_target') {
  return {
    actor: 'reviewer',
    eventName,
    payload: {
      repository: {
        default_branch: 'main',
        full_name: 'Jamula/Andreja',
      },
    },
    repo: { owner: 'Jamula', repo: 'Andreja' },
    runId: 9001,
  };
}

function pull(number = 104, overrides = {}) {
  return {
    number,
    state: 'open',
    draft: false,
    body: 'Closes #104',
    user: { login: 'pr-author' },
    head: { sha: HEAD },
    base: {
      sha: BASE,
      ref: 'main',
      repo: {
        id: REPOSITORY_ID,
        full_name: 'Jamula/Andreja',
      },
    },
    ...overrides,
  };
}

function identity(number = 104, overrides = {}) {
  return {
    pullNumber: number,
    headSha: HEAD,
    baseRepositoryId: REPOSITORY_ID,
    baseRepository: 'Jamula/Andreja',
    baseRef: 'main',
    baseSha: BASE,
    ...overrides,
  };
}

function copilotReview(commitId = HEAD, overrides = {}) {
  return {
    id: 100,
    body: 'Copilot review complete.',
    state: 'COMMENTED',
    commit_id: commitId,
    submitted_at: '2026-08-25T05:00:00Z',
    user: { login: COPILOT_LOGIN },
    ...overrides,
  };
}

function reviewBinding(domain, number = 104, overrides = {}) {
  return {
    schemaVersion: 2,
    kind: 'independent-review',
    domain,
    ...identity(number),
    evidenceUrl: `https://github.com/Jamula/Andreja/pull/${number}#pullrequestreview-1`,
    summary: `Independent ${domain} review completed.`,
    ...overrides,
  };
}

function domainReview(domain, {
  number = 104,
  state = 'APPROVED',
  login = 'reviewer',
  submittedAt = '2026-08-25T05:01:00Z',
  id = 200,
  binding = reviewBinding(domain, number),
  commitId = HEAD,
} = {}) {
  return {
    id,
    body: reviewMarker(domain, binding),
    state,
    commit_id: commitId,
    submitted_at: submittedAt,
    user: { login },
  };
}

function endpoint(kind) {
  const callable = function endpointMarker() {};
  callable.kind = kind;
  return callable;
}

class FakeGitHub {
  constructor({
    pullRequest = pull(),
    reviews = [],
    threads = [],
    labels = { 104: ['area:architecture'] },
    permissions = { reviewer: 'write' },
    workflowRuns = [],
    artifacts = {},
    downloads = {},
  } = {}) {
    this.state = {
      pullRequest,
      reviews,
      threads,
      labels,
      permissions,
      workflowRuns,
      artifacts,
      downloads,
    };
    this.failNext = null;
    this.rest = {
      actions: {
        listWorkflowRuns: endpoint('workflowRuns'),
        listWorkflowRunArtifacts: endpoint('artifacts'),
        downloadArtifact: async ({ artifact_id }) => {
          if (this.failNext) {
            const error = this.failNext;
            this.failNext = null;
            throw error;
          }
          return { data: this.state.downloads[artifact_id] };
        },
        createWorkflowDispatch: async () => {},
      },
      issues: {
        listLabelsOnIssue: endpoint('labels'),
      },
      pulls: {
        get: async () => {
          if (this.failNext) {
            const error = this.failNext;
            this.failNext = null;
            throw error;
          }
          return { data: structuredClone(this.state.pullRequest) };
        },
        list: endpoint('pulls'),
        listReviews: endpoint('reviews'),
      },
      repos: {
        getCollaboratorPermissionLevel: async ({ username }) => ({
          data: { permission: this.state.permissions[username] || 'read' },
        }),
      },
    };
  }

  async paginate(operation, parameters) {
    switch (operation.kind) {
      case 'reviews':
        return structuredClone(this.state.reviews);
      case 'labels':
        return (this.state.labels[parameters.issue_number] || [])
          .map((name) => ({ name }));
      case 'workflowRuns':
        return structuredClone(this.state.workflowRuns);
      case 'artifacts':
        return structuredClone(this.state.artifacts[parameters.run_id] || []);
      case 'pulls':
        return [structuredClone(this.state.pullRequest)];
      default:
        throw new Error('Unexpected pagination endpoint.');
    }
  }

  async graphql(query) {
    if (query.includes('reviewThreads')) {
      return {
        repository: {
          pullRequest: {
            reviewThreads: {
              nodes: structuredClone(this.state.threads),
              pageInfo: { hasNextPage: false, endCursor: null },
            },
          },
        },
      };
    }
    if (query.includes('closingIssuesReferences')) {
      return {
        repository: {
          pullRequest: {
            closingIssuesReferences: {
              nodes: [],
              pageInfo: { hasNextPage: false, endCursor: null },
            },
          },
        },
      };
    }
    throw new Error('Unexpected GraphQL query.');
  }
}

function lifecycleState(result) {
  return {
    approved: 'success',
    pending: 'in_progress',
    rejected: 'failure',
  }[result.state];
}

async function snapshotState(fake, number = 104) {
  const snapshot = await evaluateSnapshot({
    github: fake,
    context: context(),
    pullNumber: number,
    expectedIdentity: identity(number),
  });
  return lifecycleState(snapshot.result);
}

test('drafts, unresolved threads, and stale Copilot reviews fail closed', () => {
  const exactIdentity = identity();
  assert.equal(evaluateReviewCompletion({
    identity: exactIdentity,
    draft: true,
  }).state, 'rejected');
  assert.equal(evaluateReviewCompletion({
    identity: exactIdentity,
    unresolvedThreads: 1,
  }).state, 'rejected');
  assert.equal(evaluateReviewCompletion({
    identity: exactIdentity,
    copilotReview: copilotReview(OLD_HEAD),
  }).state, 'pending');
  assert.equal(latestCopilotReview([
    copilotReview(HEAD, { id: 1 }),
    copilotReview(OLD_HEAD, {
      id: 2,
      submitted_at: '2026-08-25T05:02:00Z',
    }),
  ]).commit_id, OLD_HEAD);
});

test('issue and PR labels select independent review domains', () => {
  assert.deepEqual(requiredDomains([
    [{ name: 'area:architecture' }, { name: 'area:security' }],
    ['review:privacy-required', 'review:quality-required'],
  ]), ['architecture', 'security', 'privacy', 'quality']);
});

test('review evidence marker round trips an exact PR and base identity', () => {
  const binding = reviewBinding('architecture');
  assert.deepEqual(
    reviewMarkers(reviewMarker('architecture', binding)),
    [{ domain: 'architecture', binding, error: null }]);
});

test('authenticated reviewer identity is bound to the native GitHub review actor', async () => {
  const fake = new FakeGitHub();
  const approved = await domainEvidence({
    github: fake,
    context: context(),
    reviews: [domainReview('architecture')],
    domain: 'architecture',
    identity: identity(),
    author: 'pr-author',
    permissionCache: new Map(),
  });
  assert.equal(approved.outcome, 'success');
  assert.equal(approved.reviewer, 'reviewer');

  const self = await domainEvidence({
    github: fake,
    context: context(),
    reviews: [domainReview('architecture', { login: 'pr-author' })],
    domain: 'architecture',
    identity: identity(),
    author: 'pr-author',
    permissionCache: new Map(),
  });
  assert.equal(self.outcome, 'failure');
  assert.match(self.reason, /author cannot/);

  fake.state.permissions.reviewer = 'read';
  const unauthorized = await domainEvidence({
    github: fake,
    context: context(),
    reviews: [domainReview('architecture')],
    domain: 'architecture',
    identity: identity(),
    author: 'pr-author',
    permissionCache: new Map(),
  });
  assert.equal(unauthorized.outcome, 'failure');
  assert.match(unauthorized.reason, /not authorized/);
});

test('newest expected-identity evidence candidate never falls back', async () => {
  const fake = new FakeGitHub();
  const oldApproval = domainReview('architecture', {
    id: 1,
    submittedAt: '2026-08-25T05:00:00Z',
  });
  const newestRejection = domainReview('architecture', {
    id: 2,
    state: 'CHANGES_REQUESTED',
    submittedAt: '2026-08-25T05:02:00Z',
  });
  const rejected = await domainEvidence({
    github: fake,
    context: context(),
    reviews: [oldApproval, newestRejection],
    domain: 'architecture',
    identity: identity(),
    author: 'pr-author',
    permissionCache: new Map(),
  });
  assert.equal(rejected.outcome, 'failure');
  assert.equal(rejected.candidateId, 2);

  const newestStale = domainReview('architecture', {
    id: 3,
    submittedAt: '2026-08-25T05:03:00Z',
    binding: reviewBinding('architecture', 104, { baseSha: OTHER_BASE }),
  });
  const stale = await domainEvidence({
    github: fake,
    context: context(),
    reviews: [oldApproval, newestStale],
    domain: 'architecture',
    identity: identity(),
    author: 'pr-author',
    permissionCache: new Map(),
  });
  assert.equal(stale.outcome, 'failure');
  assert.equal(stale.candidateId, 3);

  const newestIncomplete = {
    ...domainReview('architecture', {
      id: 4,
      submittedAt: '2026-08-25T05:04:00Z',
    }),
    body: '<!-- andreja-review-evidence:v2:architecture {"schemaVersion":2',
  };
  const incomplete = await domainEvidence({
    github: fake,
    context: context(),
    reviews: [oldApproval, newestIncomplete],
    domain: 'architecture',
    identity: identity(),
    author: 'pr-author',
    permissionCache: new Map(),
  });
  assert.equal(incomplete.outcome, 'failure');
  assert.equal(incomplete.candidateId, 4);
});

test('same head SHA cannot replay evidence across two PRs', async () => {
  const evidenceForFirst = domainReview('architecture', { number: 104 });
  const secondFake = new FakeGitHub({
    pullRequest: pull(205, { body: 'Closes #205' }),
    reviews: [copilotReview(), evidenceForFirst],
    labels: { 205: ['area:architecture'] },
  });
  const second = await domainEvidence({
    github: secondFake,
    context: context(),
    reviews: secondFake.state.reviews,
    domain: 'architecture',
    identity: identity(205),
    author: 'pr-author',
    permissionCache: new Map(),
  });
  assert.equal(second.outcome, 'failure');
  assert.match(second.reason, /exact pull-request diff/);
  assert.equal(await snapshotState(secondFake, 205), 'failure');
});

test('same PR and head cannot replay evidence after base identity changes', async () => {
  const fake = new FakeGitHub();
  const stale = await domainEvidence({
    github: fake,
    context: context(),
    reviews: [domainReview('architecture')],
    domain: 'architecture',
    identity: identity(104, { baseSha: OTHER_BASE }),
    author: 'pr-author',
    permissionCache: new Map(),
  });
  assert.equal(stale.outcome, 'failure');
});

test('stateful #100, #101, and #105 fixtures execute the real evaluator', async () => {
  for (const fixture of fixtures) {
    const fake = new FakeGitHub({
      reviews: [],
      labels: { 104: [] },
    });
    for (const event of fixture.events) {
      if (event.kind === 'copilot-review') {
        fake.state.reviews.push(copilotReview());
      }
      const actual = await snapshotState(fake);
      assert.equal(actual, event.expected, `${fixture.incident} at ${event.atSeconds}s`);
      if (event.atSeconds < fixture.firstReadyAtSeconds) {
        assert.notEqual(actual, 'success', fixture.incident);
      }
    }
    assert.ok(fixture.mergedAtSeconds < fixture.firstReadyAtSeconds);
  }
});

test('metadata changes invalidate same-head success through the real evaluator', async () => {
  const fake = new FakeGitHub({
    reviews: [copilotReview()],
    labels: { 104: [] },
  });
  assert.equal(await snapshotState(fake), 'success');

  fake.state.threads.push({
    id: 'thread-1',
    isResolved: false,
    comments: { nodes: [{ id: 'comment-1', updatedAt: '2026-08-25T05:03:00Z' }] },
  });
  assert.equal(await snapshotState(fake), 'failure');

  fake.state.threads[0].isResolved = true;
  fake.state.labels[104] = ['area:architecture'];
  assert.equal(await snapshotState(fake), 'in_progress');

  fake.state.reviews.push(domainReview('architecture'));
  assert.equal(await snapshotState(fake), 'success');

  fake.state.reviews.push(domainReview('architecture', {
    id: 201,
    state: 'CHANGES_REQUESTED',
    submittedAt: '2026-08-25T05:04:00Z',
  }));
  assert.equal(await snapshotState(fake), 'failure');
});

test('a new push immediately starts a non-success generation with no stale review', async () => {
  const fake = new FakeGitHub({
    reviews: [copilotReview()],
    labels: { 104: [] },
  });
  assert.equal(await snapshotState(fake), 'success');

  const nextHead = 'e'.repeat(40);
  fake.state.pullRequest.head.sha = nextHead;
  const nextIdentity = identity(104, { headSha: nextHead });
  const snapshot = await evaluateSnapshot({
    github: fake,
    context: context(),
    pullNumber: 104,
    expectedIdentity: nextIdentity,
  });
  assert.equal(lifecycleState(snapshot.result), 'in_progress');
  assert.match(snapshot.result.reasons.join(' '), /newest Copilot review/);
});

test('a stable second full snapshot is required before success', async () => {
  const fake = new FakeGitHub({
    reviews: [copilotReview()],
    labels: { 104: [] },
  });
  let sleeps = 0;
  const result = await evaluatePullRequest({
    github: fake,
    context: context(),
    pullNumber: 104,
    pollSeconds: 0,
    maxWaitSeconds: 10,
    stabilitySeconds: 0,
    sleepFunction: async () => {
      sleeps += 1;
      if (sleeps === 1) {
        fake.state.labels[104] = ['area:architecture'];
      } else if (sleeps === 2) {
        fake.state.reviews.push(domainReview('architecture'));
      }
    },
    now: (() => {
      let value = 0;
      return () => value++;
    })(),
  });
  assert.equal(result.state, 'approved');
  assert.ok(sleeps >= 3);
});

test('GitHub-managed generations reject stale completion writers', async () => {
  const fake = new FakeGitHub({
    reviews: [copilotReview()],
    labels: { 104: [] },
  });
  let generation = 1;
  let latest = { generation, state: 'in_progress' };
  const oldSnapshot = await evaluateSnapshot({
    github: fake,
    context: context(),
    pullNumber: 104,
    expectedIdentity: identity(),
  });

  generation += 1;
  latest = { generation, state: 'in_progress' };
  fake.state.labels[104] = ['area:architecture'];
  const newSnapshot = await evaluateSnapshot({
    github: fake,
    context: context(),
    pullNumber: 104,
    expectedIdentity: identity(),
  });
  latest = { generation, state: lifecycleState(newSnapshot.result) };

  const staleGeneration = generation - 1;
  if (staleGeneration === latest.generation) {
    latest.state = lifecycleState(oldSnapshot.result);
  }
  assert.equal(oldSnapshot.result.state, 'approved');
  assert.equal(latest.state, 'in_progress');
});

test('403 and 429 startup failures replace old success with a failed generation', async () => {
  for (const status of [403, 429]) {
    const fake = new FakeGitHub({
      reviews: [copilotReview()],
      labels: { 104: [] },
    });
    let latest = { generation: 1, state: 'success' };
    latest = { generation: 2, state: 'in_progress' };
    fake.failNext = Object.assign(new Error('private diagnostic'), { status });
    try {
      await evaluateSnapshot({
        github: fake,
        context: context(),
        pullNumber: 104,
        expectedIdentity: identity(),
      });
    } catch (error) {
      latest.state = 'failure';
      assert.match(sanitizedApiFailure(error), /failed closed/);
      assert.doesNotMatch(sanitizedApiFailure(error), /private diagnostic/);
    }
    assert.equal(latest.state, 'failure');
  }
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
                  nodes: [{
                    id: 'one',
                    isResolved: true,
                    comments: { nodes: [] },
                  }],
                  pageInfo: { hasNextPage: true, endCursor: 'next' },
                }
              : {
                  nodes: [{
                    id: 'two',
                    isResolved: false,
                    comments: { nodes: [] },
                  }],
                  pageInfo: { hasNextPage: false, endCursor: null },
                },
          },
        },
      };
    },
  };
  const threads = await listReviewThreads({
    github,
    context: context(),
    pullNumber: 104,
  });
  assert.deepEqual(cursors, [null, 'next']);
  assert.deepEqual(threads.map((thread) => thread.isResolved), [true, false]);
});

function storedZip(fileName, content) {
  const name = Buffer.from(fileName);
  const body = Buffer.from(content);
  const checksum = crc32(body);
  const local = Buffer.alloc(30 + name.length);
  local.writeUInt32LE(0x04034b50, 0);
  local.writeUInt16LE(20, 4);
  local.writeUInt32LE(checksum, 14);
  local.writeUInt32LE(body.length, 18);
  local.writeUInt32LE(body.length, 22);
  local.writeUInt16LE(name.length, 26);
  name.copy(local, 30);

  const central = Buffer.alloc(46 + name.length);
  central.writeUInt32LE(0x02014b50, 0);
  central.writeUInt16LE(20, 4);
  central.writeUInt16LE(20, 6);
  central.writeUInt32LE(checksum, 16);
  central.writeUInt32LE(body.length, 20);
  central.writeUInt32LE(body.length, 24);
  central.writeUInt16LE(name.length, 28);
  name.copy(central, 46);

  const end = Buffer.alloc(22);
  end.writeUInt32LE(0x06054b50, 0);
  end.writeUInt16LE(1, 8);
  end.writeUInt16LE(1, 10);
  end.writeUInt32LE(central.length, 12);
  end.writeUInt32LE(local.length + body.length, 16);
  return Buffer.concat([local, body, central, end]);
}

test('downloaded break-glass artifact content is parsed and integrity checked', () => {
  const name = `${breakGlassArtifactName(identity())}.json`;
  const binding = { schemaVersion: 2, value: 'expected' };
  const archive = storedZip(name, JSON.stringify(binding));
  assert.deepEqual(readZipJson(archive, name), binding);

  const tampered = Buffer.from(archive);
  tampered[30 + Buffer.byteLength(name)] ^= 1;
  assert.throws(() => readZipJson(tampered, name), /integrity/);
});

test('real evaluator downloads and validates newest exact break-glass content', async () => {
  const exactIdentity = identity();
  const artifactName = breakGlassArtifactName(exactIdentity);
  const run = {
    id: 700,
    display_title: breakGlassRunTitle(exactIdentity),
    status: 'completed',
    conclusion: 'success',
    event: 'workflow_dispatch',
    path: '.github/workflows/record-review-break-glass.yml',
    head_branch: 'main',
    head_sha: BASE,
    repository: { full_name: 'Jamula/Andreja' },
    actor: { login: 'maintainer' },
  };
  const binding = {
    schemaVersion: 2,
    kind: 'review-break-glass',
    ...exactIdentity,
    actor: 'maintainer',
    incidentUrl: 'https://github.com/Jamula/Andreja/issues/104',
    reason: 'Emergency reviewer outage with recorded residual risk.',
    workflowRunId: 700,
  };
  const fake = new FakeGitHub({
    reviews: [],
    workflowRuns: [run],
    permissions: { maintainer: 'maintain' },
    artifacts: {
      700: [{ id: 701, name: artifactName, expired: false }],
    },
    downloads: {
      701: storedZip(`${artifactName}.json`, JSON.stringify(binding)),
    },
  });
  assert.equal(await snapshotState(fake), 'success');

  const tamperedBinding = { ...binding, pullNumber: 205 };
  fake.state.downloads[701] = storedZip(
    `${artifactName}.json`,
    JSON.stringify(tamperedBinding));
  assert.equal(await snapshotState(fake), 'failure');
});

test('newest failed break-glass run never falls back to older valid evidence', async () => {
  const exactIdentity = identity();
  const artifactName = breakGlassArtifactName(exactIdentity);
  const validRun = {
    id: 700,
    display_title: breakGlassRunTitle(exactIdentity),
    status: 'completed',
    conclusion: 'success',
    event: 'workflow_dispatch',
    path: '.github/workflows/record-review-break-glass.yml',
    head_branch: 'main',
    head_sha: BASE,
    repository: { full_name: 'Jamula/Andreja' },
    actor: { login: 'maintainer' },
  };
  const failedRun = {
    ...validRun,
    id: 702,
    conclusion: 'failure',
  };
  const binding = {
    schemaVersion: 2,
    kind: 'review-break-glass',
    ...exactIdentity,
    actor: 'maintainer',
    incidentUrl: 'https://github.com/Jamula/Andreja/issues/104',
    reason: 'Emergency reviewer outage with recorded residual risk.',
    workflowRunId: 700,
  };
  const fake = new FakeGitHub({
    workflowRuns: [validRun, failedRun],
    permissions: { maintainer: 'maintain' },
    artifacts: {
      700: [{ id: 701, name: artifactName, expired: false }],
    },
    downloads: {
      701: storedZip(`${artifactName}.json`, JSON.stringify(binding)),
    },
  });
  assert.equal(await snapshotState(fake), 'failure');
});

test('break-glass recorder binds PR, head, base, actor, and run', async () => {
  const exactIdentity = identity();
  const outputs = {};
  const artifactDirectory = path.join(__dirname, 'test-break-glass-artifact-v2');
  fs.rmSync(artifactDirectory, { recursive: true, force: true });
  const github = {
    rest: {
      pulls: {
        get: async () => ({ data: pull() }),
      },
      repos: {
        getCollaboratorPermissionLevel: async () => ({
          data: { permission: 'maintain' },
        }),
      },
    },
  };
  try {
    await recordBreakGlass({
      github,
      context: {
        ...context('workflow_dispatch'),
        actor: 'maintainer',
        payload: {
          inputs: {
            pr_number: String(exactIdentity.pullNumber),
            head_sha: exactIdentity.headSha,
            base_repository_id: String(exactIdentity.baseRepositoryId),
            base_repository: exactIdentity.baseRepository,
            base_ref: exactIdentity.baseRef,
            base_sha: exactIdentity.baseSha,
            confirmation: BREAK_GLASS_CONFIRMATION,
            reason: 'Emergency reviewer outage with recorded residual risk.',
            incident_url: 'https://github.com/Jamula/Andreja/issues/104',
          },
        },
      },
      core: {
        warning() {},
        setOutput(name, value) { outputs[name] = value; },
      },
      artifactDirectory,
    });
    const binding = JSON.parse(fs.readFileSync(outputs['artifact-path'], 'utf8'));
    assert.equal(binding.pullNumber, 104);
    assert.equal(binding.headSha, HEAD);
    assert.equal(binding.baseSha, BASE);
    assert.equal(binding.actor, 'maintainer');
    assert.equal(binding.workflowRunId, 9001);
    assert.equal(
      breakGlassRunTitle(exactIdentity),
      `review-break-glass:v2:pr=104:head=${HEAD}:baseRepo=${REPOSITORY_ID}:baseRef=main:base=${BASE}`);
  } finally {
    fs.rmSync(artifactDirectory, { recursive: true, force: true });
  }
});

function liveRuleset(workflowRules = []) {
  return {
    id: 21199927,
    name: 'Default-Ruleset',
    target: 'branch',
    enforcement: 'active',
    bypass_actors: [],
    conditions: {
      ref_name: { include: ['~DEFAULT_BRANCH'], exclude: [] },
    },
    rules: [
      { type: 'deletion' },
      { type: 'non_fast_forward' },
      { type: 'required_linear_history' },
      {
        type: 'pull_request',
        parameters: {
          required_approving_review_count: 0,
          required_review_thread_resolution: true,
        },
      },
      {
        type: 'required_status_checks',
        parameters: {
          strict_required_status_checks_policy: true,
          required_status_checks: [
            'Build and test (Debug)',
            'Build and test (Release)',
            'Format verification',
            'NuGet vulnerability audit',
            'C# SAST (DevSkim)',
          ].map((name) => ({ context: name, integration_id: 15368 })),
        },
      },
      ...workflowRules,
    ],
  };
}

test('rollout adds an exact required-workflow rule and rollback removes only it', () => {
  const parameters = {
    repositoryId: REPOSITORY_ID,
    workflowSha: HEAD,
  };
  const rollout = planRollout(liveRuleset(), parameters);
  const rule = rollout.payload.rules.at(-1);
  assert.deepEqual(rule, {
    type: 'workflows',
    parameters: {
      do_not_enforce_on_create: false,
      workflows: [{
        path: REQUIRED_WORKFLOW_PATH,
        repository_id: REPOSITORY_ID,
        sha: HEAD,
      }],
    },
  });
  const rollback = planRollback(liveRuleset([rule]), parameters);
  assert.equal(
    rollback.payload.rules.some((candidate) => candidate.type === 'workflows'),
    false);
  assert.equal(
    rollback.payload.rules.find((candidate) =>
      candidate.type === 'required_status_checks')
      .parameters.required_status_checks.length,
    5);
});

test('privileged workflow executes trusted code and emits no spoofable gate check', () => {
  const workflowPath = path.join(
    __dirname,
    '..',
    'workflows',
    'review-completion.yml');
  const workflow = fs.readFileSync(workflowPath, 'utf8');
  const runner = fs.readFileSync(
    path.join(__dirname, 'run-review-completion.js'),
    'utf8');
  assert.match(workflow, /name: Required trusted review policy/);
  assert.match(workflow, /ref: \$\{\{ github\.workflow_sha \}\}/);
  assert.match(workflow, /^\s{2}pull_request_target:\s*$/m);
  assert.match(workflow, /^\s{2}pull_request_review:\s*$/m);
  assert.match(workflow, /^\s{2}pull_request_review_thread:\s*$/m);
  assert.match(workflow, /^\s{2}merge_group:\s*$/m);
  assert.doesNotMatch(workflow, /^\s+checks:\s+write\s*$/m);
  assert.doesNotMatch(runner, /checks\.(?:create|update)/);
  assert.equal(fs.existsSync(path.join(
    __dirname,
    '..',
    'workflows',
    'record-review-evidence.yml')), false);
  for (const match of workflow.matchAll(/uses:\s*[^@\s]+@([^\s]+)/g)) {
    assert.match(match[1], /^[0-9a-f]{40}$/);
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
