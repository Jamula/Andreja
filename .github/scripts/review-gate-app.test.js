'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');

const {
  CHECK_EXTERNAL_PREFIX,
  CHECK_NAME,
  COPILOT_REVIEWER,
  evaluateReviewCompletion,
  foldPolicyEvents,
  makePolicyEvent,
  policyEventComment,
  pullIdentity,
  reviewMarker,
  trustedPolicyEvents,
} = require('./review-gate-policy');
const {
  assertTrustedDispatchRef,
  completeGeneration,
  createGeneration,
  evaluateHeadSnapshot,
  evaluateHeadUntilStable,
  listReviewThreads,
  run,
  runHead,
  sanitizedApiFailure,
} = require('./review-gate-app');
const {
  applyPlanWithClient,
  main: rulesetMain,
  planRollback,
  planRollout,
  verifyCanaryRun,
} = require('./review-gate-ruleset');
const {
  reductionTargets,
} = require('./record-review-gate-policy');

const APP_ID = 314159;
const OTHER_APP_ID = 271828;
const REPOSITORY_ID = 1342901808;
const HEAD = 'a'.repeat(40);
const NEXT_HEAD = 'b'.repeat(40);
const BASE = 'c'.repeat(40);
const OTHER_BASE = 'd'.repeat(40);
const fixtures = JSON.parse(fs.readFileSync(
  path.join(__dirname, 'fixtures', 'review-gate-app-scenarios.json'),
  'utf8'));

function endpoint(kind) {
  const marker = function endpointMarker() {};
  marker.kind = kind;
  return marker;
}

function pull(number = 115, overrides = {}) {
  return {
    number,
    state: 'open',
    draft: false,
    body: 'Closes #104',
    user: { login: 'pr-author', id: 10, type: 'User' },
    labels: [],
    head: {
      sha: HEAD,
      ref: `feature-${number}`,
      repo: { full_name: 'Jamula/Andreja' },
    },
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

function context(eventName = 'pull_request_target', pullRequest = pull()) {
  return {
    actor: 'maintainer',
    eventName,
    runId: 9001,
    runAttempt: 1,
    sha: pullRequest.head.sha,
    repo: { owner: 'Jamula', repo: 'Andreja' },
    payload: {
      repository: {
        id: REPOSITORY_ID,
        default_branch: 'main',
        full_name: 'Jamula/Andreja',
        name: 'Andreja',
      },
      pull_request: pullRequest,
    },
  };
}

function policyEvent(pullRequest, kind, data, suffix = kind) {
  return makePolicyEvent({
    kind,
    repositoryId: REPOSITORY_ID,
    repository: 'Jamula/Andreja',
    pullNumber: pullRequest.number,
    deliveryId: `fixture:${pullRequest.number}:${suffix}`,
    createdAt: `2026-08-25T10:00:0${suffix.length % 10}Z`,
    actor: 'maintainer',
    data,
  });
}

function trustedComment(event, appId = APP_ID, id = null) {
  return {
    id: id || Number.parseInt(event.eventId.slice(0, 10), 16),
    body: policyEventComment(event),
    html_url: `https://github.com/Jamula/Andreja/pull/${event.pullNumber}` +
      `#issuecomment-${id || 1}`,
    performed_via_github_app: { id: appId, slug: 'andreja-review-gate' },
    user: { login: 'andreja-review-gate[bot]', type: 'Bot' },
  };
}

function bindEvent(pullRequest, issueNumber = 104) {
  return policyEvent(pullRequest, 'bind-issue', {
    sourceKey: `trusted-issue:${issueNumber}`,
    issueNumber,
    identity: pullIdentity(pullRequest),
    reason: 'Bind the implementation PR to its approved issue policy.',
    auditUrl: `https://github.com/Jamula/Andreja/issues/${issueNumber}`,
  }, `bind-${issueNumber}`);
}

function requirementEvent(pullRequest, domain, issueNumber = 104) {
  return policyEvent(pullRequest, 'require-domain', {
    sourceKey: `issue:${issueNumber}:domain:${domain}`,
    domain,
    sourceKind: 'issue-label',
    sourceNumber: issueNumber,
  }, `require-${domain}`);
}

function copilotAttestationEvent(pullRequest, review = copilotReview(
  pullRequest.head.sha)) {
  return policyEvent(pullRequest, 'copilot-attestation', {
    identity: pullIdentity(pullRequest),
    reviewId: Number(review.id),
    reviewerId: COPILOT_REVIEWER.id,
    reviewerLogin: COPILOT_REVIEWER.login,
    reviewSubmittedAt: review.submitted_at,
  }, `copilot-${review.id}`);
}

function domainAttestationEvent(
  pullRequest,
  domain,
  policyDigest,
  overrides = {},
) {
  return policyEvent(pullRequest, 'domain-attestation', {
    domain,
    identity: pullIdentity(pullRequest),
    policyDigest,
    outcome: 'approved',
    attester: {
      appId: 424242,
      slug: 'seven-reviewer',
      runId: 12345,
    },
    evidenceUrl: `https://github.com/Jamula/Andreja/pull/` +
      `${pullRequest.number}#issuecomment-automation`,
    summary: `Authenticated automated ${domain} review completed.`,
    ...overrides,
  }, `domain-attestation-${domain}`);
}

function snapshotForComments(comments, pullRequest = pull()) {
  const trusted = trustedPolicyEvents(comments, {
    appId: APP_ID,
    repositoryId: REPOSITORY_ID,
    repository: 'Jamula/Andreja',
    pullNumber: pullRequest.number,
  });
  return foldPolicyEvents(trusted.events, trusted.errors);
}

function copilotReview(headSha = HEAD, overrides = {}) {
  return {
    id: 100,
    state: 'COMMENTED',
    commit_id: headSha,
    submitted_at: '2026-08-25T10:05:00Z',
    body: 'Authenticated Copilot review completed.',
    user: { ...COPILOT_REVIEWER },
    ...overrides,
  };
}

function domainReview(pullRequest, domain, policyDigest, overrides = {}) {
  const binding = {
    schemaVersion: 3,
    kind: 'independent-review',
    domain,
    ...pullIdentity(pullRequest),
    policyDigest,
    evidenceUrl: `https://github.com/Jamula/Andreja/pull/${pullRequest.number}` +
      '#pullrequestreview-1',
    summary: `Independent ${domain} review completed for this exact policy.`,
  };
  return {
    id: 200,
    state: 'APPROVED',
    commit_id: pullRequest.head.sha,
    submitted_at: '2026-08-25T10:06:00Z',
    body: reviewMarker(domain, binding),
    user: { id: 22, login: 'reviewer', type: 'User' },
    ...overrides,
  };
}

class FakeGitHub {
  constructor({
    pulls = [pull()],
    comments = {},
    labels = {},
    reviews = {},
    threads = {},
    permissions = { reviewer: 'write', maintainer: 'admin' },
    appId = APP_ID,
  } = {}) {
    this.state = {
      pulls: structuredClone(pulls),
      comments: structuredClone(comments),
      labels: structuredClone(labels),
      reviews: structuredClone(reviews),
      threads: structuredClone(threads),
      permissions: structuredClone(permissions),
      checkRuns: [],
      nextCheckId: 1000,
      nextCommentId: 5000,
      appId,
      issues: {},
    };
    this.callLog = [];
    this.failOn = null;
    this.rest = {
      checks: {
        create: async (parameters) => {
          this.#maybeFail('checks.create');
          this.callLog.push('checks.create');
          const run = {
            id: this.state.nextCheckId++,
            ...structuredClone(parameters),
            head_sha: parameters.head_sha,
            app: { id: this.state.appId, slug: 'andreja-review-gate' },
          };
          this.state.checkRuns.push(run);
          return { data: structuredClone(run) };
        },
        update: async (parameters) => {
          this.#maybeFail('checks.update');
          this.callLog.push(`checks.update:${parameters.check_run_id}`);
          const run = this.state.checkRuns.find((candidate) =>
            candidate.id === parameters.check_run_id);
          Object.assign(run, structuredClone(parameters));
          return { data: structuredClone(run) };
        },
        listForRef: endpoint('checkRuns'),
      },
      issues: {
        createComment: async (parameters) => {
          this.#maybeFail('issues.createComment');
          this.callLog.push(`issues.createComment:${parameters.issue_number}`);
          const comment = {
            id: this.state.nextCommentId++,
            body: parameters.body,
            html_url: `https://github.com/Jamula/Andreja/pull/` +
              `${parameters.issue_number}#issuecomment-${this.state.nextCommentId}`,
            performed_via_github_app: {
              id: this.state.appId,
              slug: 'andreja-review-gate',
            },
            user: { login: 'andreja-review-gate[bot]', type: 'Bot' },
          };
          this.state.comments[parameters.issue_number] ||= [];
          this.state.comments[parameters.issue_number].push(comment);
          return { data: structuredClone(comment) };
        },
        get: async ({ issue_number }) => ({
          data: this.state.issues[issue_number] || {
            number: issue_number,
            pull_request: null,
          },
        }),
        listComments: endpoint('comments'),
        listLabelsOnIssue: endpoint('labels'),
      },
      pulls: {
        get: async ({ pull_number }) => {
          this.#maybeFail('pulls.get');
          this.callLog.push(`pulls.get:${pull_number}`);
          return {
            data: structuredClone(this.state.pulls.find((candidate) =>
              candidate.number === pull_number)),
          };
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

  #maybeFail(name) {
    if (this.failOn?.name === name) {
      const error = this.failOn.error;
      this.failOn = null;
      throw error;
    }
  }

  async paginate(operation, parameters) {
    this.#maybeFail(operation.kind);
    this.callLog.push(`paginate:${operation.kind}`);
    switch (operation.kind) {
      case 'checkRuns':
        return structuredClone(this.state.checkRuns.filter((run) =>
          run.head_sha === parameters.ref &&
          (!parameters.check_name || run.name === parameters.check_name)));
      case 'comments':
        return structuredClone(this.state.comments[parameters.issue_number] || []);
      case 'labels':
        return (this.state.labels[parameters.issue_number] || [])
          .map((name) => ({ name }));
      case 'pulls':
        return structuredClone(this.state.pulls.filter((candidate) =>
          candidate.state === (parameters.state || candidate.state)));
      case 'reviews':
        return structuredClone(this.state.reviews[parameters.pull_number] || []);
      default:
        throw new Error(`Unexpected pagination endpoint ${operation.kind}.`);
    }
  }

  async graphql(_query, variables) {
    this.#maybeFail('graphql');
    this.callLog.push(`graphql:${variables.number}:${variables.cursor || 'first'}`);
    return {
      repository: {
        pullRequest: {
          reviewThreads: {
            nodes: structuredClone(this.state.threads[variables.number] || []),
            pageInfo: { hasNextPage: false, endCursor: null },
          },
        },
      },
    };
  }
}

function seededFake({
  pullRequest = pull(),
  issueNumber = 104,
  domains = [],
  labels = {},
  reviews = null,
  extraPulls = [],
} = {}) {
  const reviewList = reviews || [copilotReview(pullRequest.head.sha)];
  const events = [
    bindEvent(pullRequest, issueNumber),
    ...domains.map((domain) =>
      requirementEvent(pullRequest, domain, issueNumber)),
    ...reviewList
      .filter((review) => Number(review.user?.id) === COPILOT_REVIEWER.id)
      .map((review) => copilotAttestationEvent(pullRequest, review)),
  ];
  return new FakeGitHub({
    pulls: [pullRequest, ...extraPulls],
    comments: {
      [pullRequest.number]: events.map((event, index) =>
        trustedComment(event, APP_ID, index + 1)),
    },
    labels,
    reviews: {
      [pullRequest.number]: reviewList,
    },
  });
}

function addCopilotCompletion(fake, pullRequest = fake.state.pulls[0]) {
  const review = copilotReview(pullRequest.head.sha);
  fake.state.reviews[pullRequest.number] ||= [];
  fake.state.reviews[pullRequest.number].push(review);
  fake.state.comments[pullRequest.number] ||= [];
  fake.state.comments[pullRequest.number].push(
    trustedComment(copilotAttestationEvent(pullRequest, review), APP_ID));
}

const quietCore = {
  info() {},
  setFailed() {},
};

test('draft, unresolved thread, missing policy, and stale Copilot fail closed', () => {
  const current = pull();
  const policy = snapshotForComments([trustedComment(bindEvent(current))], current);
  assert.equal(evaluateReviewCompletion({
    pullRequest: { ...current, draft: true },
    policy,
  }).state, 'rejected');
  assert.equal(evaluateReviewCompletion({
    pullRequest: current,
    policy,
    unresolvedThreads: 1,
  }).state, 'rejected');
  assert.equal(evaluateReviewCompletion({
    pullRequest: current,
    policy: foldPolicyEvents([]),
  }).state, 'rejected');
  assert.equal(evaluateReviewCompletion({
    pullRequest: current,
    policy,
    reviews: [copilotReview(NEXT_HEAD)],
  }).state, 'pending');
});

test('copied policy marker from the wrong App identity is ignored', () => {
  const current = pull();
  const copied = trustedComment(bindEvent(current), OTHER_APP_ID);
  const snapshot = snapshotForComments([copied], current);
  assert.equal(snapshot.initialized, false);
  assert.equal(snapshot.errors.length, 0);
});

test('same-head metadata revocation publishes pending before failure', async () => {
  const fake = seededFake();
  await runHead({
    github: fake,
    context: context(),
    core: quietCore,
    headSha: HEAD,
    expectedAppId: APP_ID,
    trigger: 'initial',
    evaluatorOptions: { poll: false, stabilitySeconds: 0 },
  });
  assert.equal(fake.state.checkRuns.at(-1).conclusion, 'success');

  fake.state.threads[115] = [{
    id: 'thread-1',
    isResolved: false,
    comments: { nodes: [{ id: 'comment-1', updatedAt: '2026-08-25T10:07:00Z' }] },
  }];
  const before = fake.callLog.length;
  await runHead({
    github: fake,
    context: context('pull_request_review_thread'),
    core: quietCore,
    headSha: HEAD,
    expectedAppId: APP_ID,
    trigger: 'thread-unresolved',
    evaluatorOptions: { poll: false, stabilitySeconds: 0 },
  });
  assert.equal(fake.callLog[before], 'checks.create');
  assert.equal(fake.state.checkRuns.at(-1).conclusion, 'failure');
  assert.match(fake.state.checkRuns.at(-1).output.summary, /unresolved/);
});

test('late metadata recovery creates a newer successful App generation', async () => {
  const fake = seededFake();
  fake.state.threads[115] = [{
    id: 'thread-1',
    isResolved: false,
    comments: { nodes: [] },
  }];
  await runHead({
    github: fake,
    context: context('pull_request_review_thread'),
    core: quietCore,
    headSha: HEAD,
    expectedAppId: APP_ID,
    trigger: 'unresolved',
    evaluatorOptions: { poll: false, stabilitySeconds: 0 },
  });
  assert.equal(fake.state.checkRuns.at(-1).conclusion, 'failure');
  fake.state.threads[115][0].isResolved = true;
  await runHead({
    github: fake,
    context: context('pull_request_review_thread'),
    core: quietCore,
    headSha: HEAD,
    expectedAppId: APP_ID,
    trigger: 'resolved',
    evaluatorOptions: { poll: false, stabilitySeconds: 0 },
  });
  assert.equal(fake.state.checkRuns.at(-1).conclusion, 'success');
});

test('Copilot review webhook is attested by the App before approval', async () => {
  const current = pull();
  const review = copilotReview();
  const fake = new FakeGitHub({
    pulls: [current],
    comments: { 115: [trustedComment(bindEvent(current), APP_ID, 1)] },
    reviews: { 115: [review] },
  });
  const reviewContext = context('pull_request_review', current);
  reviewContext.payload.action = 'submitted';
  reviewContext.payload.review = review;
  await runHead({
    github: fake,
    context: reviewContext,
    core: quietCore,
    headSha: HEAD,
    expectedAppId: APP_ID,
    trigger: 'copilot-submitted',
    evaluatorOptions: { poll: false, stabilitySeconds: 0 },
  });
  assert.equal(fake.state.checkRuns.at(-1).conclusion, 'success');
  assert.ok(fake.state.comments[115].some((comment) =>
    comment.body.includes('Exact-diff Copilot review attested')));
});

test('bound-issue label event revokes same-head success through a new generation', async () => {
  const fake = seededFake();
  await runHead({
    github: fake,
    context: context(),
    core: quietCore,
    headSha: HEAD,
    expectedAppId: APP_ID,
    trigger: 'initial',
    evaluatorOptions: { poll: false, stabilitySeconds: 0 },
  });
  assert.equal(fake.state.checkRuns.at(-1).conclusion, 'success');
  fake.state.labels[104] = ['area:architecture'];
  const issueContext = context('issues');
  delete issueContext.payload.pull_request;
  issueContext.payload.issue = { number: 104 };
  await run({
    github: fake,
    context: issueContext,
    core: quietCore,
    expectedAppId: APP_ID,
    evaluatorOptions: { poll: false, stabilitySeconds: 0 },
  });
  assert.equal(fake.state.checkRuns.at(-1).conclusion, 'failure');
  assert.match(fake.state.checkRuns.at(-1).output.summary, /architecture/);
});

test('retarget or base advance invalidates exact-diff evidence', async () => {
  const current = pull();
  const fake = seededFake({ pullRequest: current });
  assert.equal(
    (await evaluateHeadSnapshot({
      github: fake,
      context: context(),
      headSha: HEAD,
      expectedAppId: APP_ID,
    })).result.state,
    'approved');

  fake.state.pulls[0].base = {
    ...fake.state.pulls[0].base,
    ref: 'release',
    sha: OTHER_BASE,
  };
  const changed = await evaluateHeadSnapshot({
    github: fake,
    context: context(),
    headSha: HEAD,
    expectedAppId: APP_ID,
  });
  assert.equal(changed.result.state, 'pending');
  assert.match(changed.result.reasons.join(' '), /exact PR\/head\/base/);
});

test('push invalidates Copilot App attestation until the new diff is reviewed', async () => {
  const fake = seededFake();
  fake.state.pulls[0].head.sha = NEXT_HEAD;
  const result = await evaluateHeadSnapshot({
    github: fake,
    context: context(),
    headSha: NEXT_HEAD,
    expectedAppId: APP_ID,
  });
  assert.equal(result.result.state, 'pending');
  assert.match(result.result.reasons.join(' '), /newest Copilot review/);
});

test('author removing closing references cannot reduce authenticated policy', async () => {
  const current = pull();
  const fake = seededFake({
    pullRequest: current,
    labels: { 104: ['area:architecture'] },
  });
  fake.state.pulls[0].body = 'No closing references remain.';
  const first = await evaluateHeadSnapshot({
    github: fake,
    context: context(),
    headSha: HEAD,
    expectedAppId: APP_ID,
  });
  assert.equal(first.snapshots[0].policy.associations[0], 104);
  assert.deepEqual(first.snapshots[0].policy.domains, ['architecture']);
  assert.equal(first.result.state, 'pending');

  fake.state.reviews[115].push(domainReview(
    fake.state.pulls[0],
    'architecture',
    first.snapshots[0].policy.digest));
  const recovered = await evaluateHeadSnapshot({
    github: fake,
    context: context(),
    headSha: HEAD,
    expectedAppId: APP_ID,
  });
  assert.equal(recovered.result.state, 'approved');
});

test('deleting an App policy comment restores it before evaluation', async () => {
  const current = pull();
  const fake = seededFake({
    pullRequest: current,
    domains: ['architecture'],
  });
  const policy = snapshotForComments(fake.state.comments[115], current);
  fake.state.reviews[115].push(
    domainReview(current, 'architecture', policy.digest));
  const requirementComment = fake.state.comments[115].find((comment) =>
    comment.body.includes('architecture review requirement'));
  fake.state.comments[115] = fake.state.comments[115].filter((comment) =>
    comment.id !== requirementComment.id);
  const deletedContext = context('issue_comment');
  delete deletedContext.payload.pull_request;
  deletedContext.payload.action = 'deleted';
  deletedContext.payload.issue = {
    number: 115,
    pull_request: { url: 'https://api.github.com/repos/Jamula/Andreja/pulls/115' },
  };
  deletedContext.payload.comment = requirementComment;
  await run({
    github: fake,
    context: deletedContext,
    core: quietCore,
    expectedAppId: APP_ID,
    evaluatorOptions: { poll: false, stabilitySeconds: 0 },
  });
  assert.equal(fake.state.checkRuns.at(-1).conclusion, 'success');
  assert.equal(fake.state.comments[115].filter((comment) =>
    comment.body.includes('architecture review requirement')).length, 1);
});

test('newest independent rejection masks an older approval', async () => {
  const current = pull();
  const fake = seededFake({
    pullRequest: current,
    domains: ['architecture'],
  });
  const policy = snapshotForComments(fake.state.comments[115], current);
  const approved = domainReview(current, 'architecture', policy.digest);
  fake.state.reviews[115].push(approved);
  assert.equal((await evaluateHeadSnapshot({
    github: fake,
    context: context(),
    headSha: HEAD,
    expectedAppId: APP_ID,
  })).result.state, 'approved');
  fake.state.reviews[115].push({
    ...approved,
    id: 201,
    state: 'CHANGES_REQUESTED',
    submitted_at: '2026-08-25T10:08:00Z',
  });
  assert.equal((await evaluateHeadSnapshot({
    github: fake,
    context: context(),
    headSha: HEAD,
    expectedAppId: APP_ID,
  })).result.state, 'rejected');
});

test('PR author self-approval never counts as independent evidence', async () => {
  const current = pull();
  const fake = seededFake({
    pullRequest: current,
    domains: ['quality'],
  });
  const policy = snapshotForComments(fake.state.comments[115], current);
  fake.state.reviews[115].push(domainReview(
    current,
    'quality',
    policy.digest,
    { user: { id: 10, login: 'pr-author', type: 'User' } }));
  const result = await evaluateHeadSnapshot({
    github: fake,
    context: context(),
    headSha: HEAD,
    expectedAppId: APP_ID,
  });
  assert.equal(result.result.state, 'rejected');
  assert.match(result.result.reasons.join(' '), /author cannot/);
});

test('current-diff App automation evidence supports the one-human model', async () => {
  const current = pull();
  const fake = seededFake({
    pullRequest: current,
    domains: ['architecture'],
  });
  const policy = snapshotForComments(fake.state.comments[115], current);
  fake.state.comments[115].push(trustedComment(
    domainAttestationEvent(current, 'architecture', policy.digest),
    APP_ID,
    99));
  const result = await evaluateHeadSnapshot({
    github: fake,
    context: context(),
    headSha: HEAD,
    expectedAppId: APP_ID,
  });
  assert.equal(result.result.state, 'approved');

  fake.state.pulls[0].base.sha = OTHER_BASE;
  const stale = await evaluateHeadSnapshot({
    github: fake,
    context: context(),
    headSha: HEAD,
    expectedAppId: APP_ID,
  });
  assert.notEqual(stale.result.state, 'approved');
});

test('a changed second full snapshot cannot publish success', async () => {
  const fake = seededFake();
  let sleepCount = 0;
  const result = await evaluateHeadUntilStable({
    github: fake,
    context: context(),
    headSha: HEAD,
    expectedAppId: APP_ID,
    poll: false,
    stabilitySeconds: 0,
    sleepFunction: async () => {
      sleepCount += 1;
      fake.state.threads[115] = [{
        id: 'late-thread',
        isResolved: false,
        comments: { nodes: [] },
      }];
    },
  });
  assert.equal(sleepCount, 1);
  assert.equal(result.result.state, 'rejected');
});

test('concurrent stale writer cannot update after a newer generation exists', async () => {
  const fake = seededFake();
  const ctx = context();
  const oldRun = await createGeneration({
    github: fake,
    context: ctx,
    headSha: HEAD,
    expectedAppId: APP_ID,
    trigger: 'old',
  });
  const newRun = await createGeneration({
    github: fake,
    context: ctx,
    headSha: HEAD,
    expectedAppId: APP_ID,
    trigger: 'new',
  });
  const approved = { state: 'approved', reasons: ['complete'] };
  assert.equal(await completeGeneration({
    github: fake,
    context: ctx,
    checkRun: oldRun,
    expectedAppId: APP_ID,
    result: approved,
  }), false);
  assert.equal(fake.state.checkRuns.find((run) => run.id === oldRun.id).status,
    'in_progress');
  assert.equal(await completeGeneration({
    github: fake,
    context: ctx,
    checkRun: newRun,
    expectedAppId: APP_ID,
    result: approved,
  }), true);
  assert.equal(fake.state.checkRuns.at(-1).conclusion, 'success');
});

test('rate limit after startup leaves the newest App generation failed', async () => {
  const fake = seededFake();
  fake.failOn = {
    name: 'pulls',
    error: Object.assign(new Error('private rate-limit detail'), { status: 429 }),
  };
  await runHead({
    github: fake,
    context: context(),
    core: quietCore,
    headSha: HEAD,
    expectedAppId: APP_ID,
    trigger: 'rate-limit',
    evaluatorOptions: { poll: false, stabilitySeconds: 0 },
  });
  const latest = fake.state.checkRuns.at(-1);
  assert.equal(latest.conclusion, 'failure');
  assert.match(latest.output.summary, /failed closed/);
  assert.doesNotMatch(latest.output.summary, /private rate-limit/);
  assert.match(sanitizedApiFailure({ status: 429 }), /failed closed/);
});

test('check creation rejects an App identity mismatch', async () => {
  const fake = seededFake();
  fake.state.appId = OTHER_APP_ID;
  await assert.rejects(() => createGeneration({
    github: fake,
    context: context(),
    headSha: HEAD,
    expectedAppId: APP_ID,
    trigger: 'mismatch',
  }), /publisher/);
});

test('two PRs sharing a head are aggregated so one approval cannot replay', async () => {
  const first = pull(115);
  const second = pull(205, {
    body: 'Closes #205',
    user: { login: 'other-author', id: 11, type: 'User' },
  });
  const firstBind = trustedComment(bindEvent(first, 104), APP_ID, 1);
  const firstCopilot = trustedComment(
    copilotAttestationEvent(first),
    APP_ID,
    4);
  const secondBind = trustedComment(bindEvent(second, 205), APP_ID, 2);
  const secondRequirement = trustedComment(
    requirementEvent(second, 'quality', 205),
    APP_ID,
    3);
  const secondCopilot = trustedComment(
    copilotAttestationEvent(second),
    APP_ID,
    5);
  const fake = new FakeGitHub({
    pulls: [first, second],
    comments: {
      115: [firstBind, firstCopilot],
      205: [secondBind, secondRequirement, secondCopilot],
    },
    reviews: {
      115: [copilotReview()],
      205: [copilotReview()],
    },
  });
  const blocked = await evaluateHeadSnapshot({
    github: fake,
    context: context(),
    headSha: HEAD,
    expectedAppId: APP_ID,
  });
  assert.equal(blocked.result.state, 'pending');
  assert.match(blocked.result.reasons.join(' '), /PR #205/);

  const secondPolicy = blocked.snapshots.find((snapshot) =>
    snapshot.pullNumber === 205).policy;
  fake.state.reviews[205].push(
    domainReview(second, 'quality', secondPolicy.digest));
  const approved = await evaluateHeadSnapshot({
    github: fake,
    context: context(),
    headSha: HEAD,
    expectedAppId: APP_ID,
  });
  assert.equal(approved.result.state, 'approved');
  assert.match(approved.result.reasons.join(' '), /#115, #205/);
});

test('exact App-authored break-glass is current-policy bound and keeps hard blocks', async () => {
  const current = pull();
  const bind = trustedComment(bindEvent(current), APP_ID, 1);
  const policy = snapshotForComments([bind], current);
  const breakGlass = policyEvent(current, 'break-glass', {
    identity: pullIdentity(current),
    policyDigest: policy.digest,
    reason: 'Reviewer automation outage with accepted and recorded residual risk.',
    auditUrl: 'https://github.com/Jamula/Andreja/issues/104',
  }, 'break-glass');
  const fake = new FakeGitHub({
    pulls: [current],
    comments: { 115: [bind, trustedComment(breakGlass, APP_ID, 2)] },
    reviews: { 115: [] },
  });
  assert.equal((await evaluateHeadSnapshot({
    github: fake,
    context: context(),
    headSha: HEAD,
    expectedAppId: APP_ID,
  })).result.state, 'approved');
  fake.state.pulls[0].draft = true;
  assert.equal((await evaluateHeadSnapshot({
    github: fake,
    context: context(),
    headSha: HEAD,
    expectedAppId: APP_ID,
  })).result.state, 'rejected');
});

test('wrong-App break-glass cannot substitute for missing Copilot evidence', async () => {
  const current = pull();
  const bind = trustedComment(bindEvent(current), APP_ID, 1);
  const policy = snapshotForComments([bind], current);
  const breakGlass = policyEvent(current, 'break-glass', {
    identity: pullIdentity(current),
    policyDigest: policy.digest,
    reason: 'Reviewer automation outage with accepted and recorded residual risk.',
    auditUrl: 'https://github.com/Jamula/Andreja/issues/104',
  }, 'wrong-break-glass');
  const fake = new FakeGitHub({
    pulls: [current],
    comments: {
      115: [bind, trustedComment(breakGlass, OTHER_APP_ID, 2)],
    },
    reviews: { 115: [] },
  });
  assert.equal((await evaluateHeadSnapshot({
    github: fake,
    context: context(),
    headSha: HEAD,
    expectedAppId: APP_ID,
  })).result.state, 'pending');
});

test('stateful #100, #101, and #105 fixtures have no merge-ready interval', async () => {
  for (const fixture of fixtures) {
    const fake = seededFake({ reviews: [] });
    for (const event of fixture.events) {
      if (event.kind === 'copilot-review') {
        addCopilotCompletion(fake);
      }
      const state = (await evaluateHeadSnapshot({
        github: fake,
        context: context(),
        headSha: HEAD,
        expectedAppId: APP_ID,
      })).result.state;
      assert.equal(state, event.expected, `${fixture.incident} at ${event.atSeconds}s`);
      if (event.atSeconds < fixture.firstReviewAtSeconds) {
        assert.notEqual(state, 'approved', fixture.incident);
      }
    }
    assert.ok(fixture.mergedAtSeconds < fixture.firstReviewAtSeconds);
  }
});

test('review-thread GraphQL pagination reads every page', async () => {
  const cursors = [];
  const github = {
    graphql: async (_query, variables) => {
      cursors.push(variables.cursor);
      return {
        repository: {
          pullRequest: {
            reviewThreads: variables.cursor === null
              ? {
                  nodes: [{ id: 'one', isResolved: true, comments: { nodes: [] } }],
                  pageInfo: { hasNextPage: true, endCursor: 'next' },
                }
              : {
                  nodes: [{ id: 'two', isResolved: false, comments: { nodes: [] } }],
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
    pullNumber: 115,
  });
  assert.deepEqual(cursors, [null, 'next']);
  assert.deepEqual(threads.map((thread) => thread.isResolved), [true, false]);
});

test('audited policy reductions target active event IDs and reject stale IDs', () => {
  const current = pull();
  const bind = bindEvent(current);
  const requirement = requirementEvent(current, 'architecture');
  const ledger = {
    snapshot: snapshotForComments([
      trustedComment(bind, APP_ID, 1),
      trustedComment(requirement, APP_ID, 2),
    ], current),
  };
  assert.deepEqual(
    reductionTargets(JSON.stringify([requirement.eventId]), ledger),
    [requirement.eventId]);
  assert.throws(
    () => reductionTargets(JSON.stringify(['f'.repeat(64)]), ledger),
    /stale/);
});

function liveRuleset(extraChecks = []) {
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
          ].map((name) => ({ context: name, integration_id: 15368 }))
            .concat(extraChecks),
        },
      },
    ],
  };
}

test('ruleset rollout preserves every existing check and binds exact App ID', () => {
  const extra = { context: 'Future protected check', integration_id: 999 };
  const rollout = planRollout(liveRuleset([extra]), { appId: APP_ID });
  const checks = rollout.payload.rules.find((rule) =>
    rule.type === 'required_status_checks').parameters.required_status_checks;
  assert.equal(checks.length, 7);
  assert.ok(checks.some((check) =>
    check.context === extra.context && check.integration_id === extra.integration_id));
  assert.deepEqual(checks.at(-1), {
    context: CHECK_NAME,
    integration_id: APP_ID,
  });
  const rollback = planRollback({
    ...liveRuleset([extra]),
    rules: rollout.payload.rules,
  }, { appId: APP_ID });
  const remaining = rollback.payload.rules.find((rule) =>
    rule.type === 'required_status_checks').parameters.required_status_checks;
  assert.equal(remaining.length, 6);
  assert.ok(remaining.some((check) => check.context === extra.context));
});

test('ruleset stale concurrent writer is rejected by ETag without overwrite', () => {
  const beforeRuleset = liveRuleset();
  const plan = planRollout(beforeRuleset, { appId: APP_ID });
  const client = {
    update(_payload, etag) {
      assert.equal(etag, 'W/"old"');
      const error = Object.assign(new Error('Precondition Failed'), { status: 412 });
      throw error;
    },
    load() {
      throw new Error('load must not follow a rejected conditional update');
    },
  };
  assert.throws(() => applyPlanWithClient({
    etag: 'W/"old"',
    plan,
    client,
  }), /Precondition Failed/);
});

test('ruleset apply rejects a file snapshot before any GitHub mutation', () => {
  assert.throws(() => rulesetMain([
    'apply-rollout',
    '--input',
    'stale-ruleset.json',
  ]), /forbidden/);
});

test('ruleset canaries bind PR, merge-group, and main outputs to exact App ID', () => {
  const baseRun = {
    id: 700,
    name: CHECK_NAME,
    head_sha: HEAD,
    app: { id: APP_ID },
    status: 'completed',
    conclusion: 'success',
    external_id: `${CHECK_EXTERNAL_PREFIX}:sha=${HEAD}:delivery=canary`,
    output: { title: 'Approved — exact policy complete' },
  };
  assert.equal(verifyCanaryRun(baseRun, {
    checkRunId: 700,
    headSha: HEAD,
    appId: APP_ID,
    checkName: CHECK_NAME,
    titlePrefix: 'Approved',
  }).appId, APP_ID);
  assert.equal(verifyCanaryRun({
    ...baseRun,
    output: { title: 'Not applicable — PR heads enforce review' },
  }, {
    checkRunId: 700,
    headSha: HEAD,
    appId: APP_ID,
    checkName: CHECK_NAME,
    titlePrefix: 'Not applicable',
  }).headSha, HEAD);
  assert.throws(() => verifyCanaryRun({
    ...baseRun,
    app: { id: OTHER_APP_ID },
  }, {
    checkRunId: 700,
    headSha: HEAD,
    appId: APP_ID,
    checkName: CHECK_NAME,
    titlePrefix: 'Approved',
  }), /provenance/);
});

test('privileged dispatch rejects a non-default-branch workflow ref', () => {
  const dispatch = context('workflow_dispatch');
  dispatch.ref = 'refs/heads/untrusted-branch';
  assert.throws(
    () => assertTrustedDispatchRef(dispatch),
    /default branch/);
  dispatch.ref = 'refs/heads/main';
  assert.doesNotThrow(() => assertTrustedDispatchRef(dispatch));
});

test('workflows use exact App identity, trusted code, no cancellation, and pinned actions', () => {
  const workflowDirectory = path.join(__dirname, '..', 'workflows');
  const gate = fs.readFileSync(
    path.join(workflowDirectory, 'review-gate-app.yml'),
    'utf8');
  const admin = fs.readFileSync(
    path.join(workflowDirectory, 'review-gate-app-admin.yml'),
    'utf8');
  for (const source of [gate, admin]) {
    assert.match(source, /REVIEW_GATE_APP_CLIENT_ID/);
    assert.match(source, /REVIEW_GATE_APP_PRIVATE_KEY/);
    assert.match(source, /REVIEW_GATE_APP_ID/);
    assert.match(source, /ref: \$\{\{ github\.workflow_sha \}\}/);
    assert.match(source, /refs\/heads\/\{0\}/);
    assert.doesNotMatch(source, /cancel-in-progress/);
    for (const match of source.matchAll(/uses:\s*[^@\s]+@([^\s]+)/g)) {
      assert.match(match[1], /^[0-9a-f]{40}$/);
    }
  }
  assert.match(gate, /^\s{2}pull_request_review:\s*$/m);
  assert.match(gate, /^\s{2}pull_request_review_thread:\s*$/m);
  assert.match(gate, /^\s{2}issues:\s*$/m);
  assert.match(gate, /^\s{2}merge_group:\s*$/m);
  assert.doesNotMatch(gate, /github\.event\.pull_request\.head\.ref/);
  assert.equal(
    fs.existsSync(path.join(workflowDirectory, 'review-completion.yml')),
    false);
  assert.equal(
    fs.existsSync(path.join(workflowDirectory, 'record-review-break-glass.yml')),
    false);
});

test('App check external identity is generation scoped', async () => {
  const fake = seededFake();
  const generation = await createGeneration({
    github: fake,
    context: context(),
    headSha: HEAD,
    expectedAppId: APP_ID,
    trigger: 'fixture',
  });
  assert.equal(generation.name, CHECK_NAME);
  assert.equal(generation.app.id, APP_ID);
  assert.match(generation.external_id, new RegExp(`^${CHECK_EXTERNAL_PREFIX}:`));
  assert.equal(generation.status, 'in_progress');
});
