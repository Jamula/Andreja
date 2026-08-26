'use strict';

const assert = require('node:assert/strict');
const { spawnSync } = require('node:child_process');
const crypto = require('node:crypto');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');

const {
  CHECK_EXTERNAL_PREFIX,
  CHECK_NAME,
  CONTRACT_REVISION,
  COPILOT_REVIEWER,
  POLICY_EVENT_MARKER_PREFIX,
  evaluateReviewCompletion,
  foldPolicyEvents,
  makeObservationEpoch,
  makePolicyEvent,
  parsePolicyEventComment,
  policyEventComment,
  pullIdentity,
  reviewMarker,
  securityDigest,
  stableValue,
  trustedPolicyEvents,
} = require('./review-gate-policy');
const {
  basicPullMapping,
  completeGeneration,
  createGeneration,
  evaluateHeadSnapshot,
  evaluateHeadUntilStable,
  exactMappingIdentity,
  externalIdForProvenance,
  handleEvent,
  listAllOpenPulls,
  listReviewThreads,
  sanitizedApiFailure,
} = require('./review-gate-app');
const {
  ROLLOUT_BLOCKER_CODE,
  main: rulesetMain,
  planRollback,
  planRollout,
  verifyCanaryRun,
} = require('./review-gate-ruleset');
const {
  inputIdentity,
  recordDecision,
  reductionTargets,
} = require('./record-review-gate-policy');

const APP_ID = 314159;
const OTHER_APP_ID = 271828;
const REPOSITORY_ID = 1342901808;
const HEAD = 'a'.repeat(40);
const NEXT_HEAD = 'b'.repeat(40);
const BASE = 'c'.repeat(40);
const NEXT_BASE = 'd'.repeat(40);
const MERGE_HEAD = 'f'.repeat(40);
const WORKER_REVISION = 'e'.repeat(64);
const fixtures = JSON.parse(fs.readFileSync(
  path.join(__dirname, 'fixtures', 'review-gate-app-scenarios.json'),
  'utf8'));
let sequence = 0;

function endpoint(kind, implementation = async () => undefined) {
  const marker = implementation;
  marker.kind = kind;
  return marker;
}

function pull(number = 115, overrides = {}) {
  return {
    number,
    state: 'open',
    draft: false,
    body: 'Closes #104',
    user: { login: `pr-author-${number}`, id: number, type: 'User' },
    labels: [],
    head: {
      sha: HEAD,
      ref: `feature-${number}`,
      repo: { id: REPOSITORY_ID, full_name: 'Jamula/Andreja' },
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

function envelope(eventPath, payload = {}, overrides = {}) {
  sequence += 1;
  const source = eventPath === 'reconciliation'
    ? 'trusted-scheduler'
    : eventPath === 'trusted_dispatch'
      ? 'trusted-admin'
      : eventPath === 'specialist_attestation'
        ? 'trusted-specialist-broker'
      : 'github-app-webhook';
  return {
    actor: 'maintainer',
    repository: {
      id: REPOSITORY_ID,
      fullName: 'Jamula/Andreja',
      owner: 'Jamula',
      name: 'Andreja',
      defaultBranch: 'main',
    },
    delivery: {
      id: `delivery-${sequence}`,
      runId: `worker-run-${sequence}`,
      eventPath,
      source,
      authenticated: true,
    },
    worker: {
      hostKind: 'independent-app-worker',
      revision: WORKER_REVISION,
      instanceId: 'worker-a',
    },
    payload,
    ...overrides,
  };
}

function eventTimestamp() {
  sequence += 1;
  return new Date(Date.UTC(2026, 7, 25, 17, 0, sequence)).toISOString();
}

function observation(pullRequest, kind, data, sourceKey, suffix = kind) {
  const identity = pullIdentity(pullRequest);
  const deliveryId = `fixture:${pullRequest.number}:${suffix}:${sequence + 1}`;
  return makePolicyEvent({
    kind,
    repositoryId: REPOSITORY_ID,
    repository: 'Jamula/Andreja',
    pullNumber: pullRequest.number,
    deliveryId,
    createdAt: eventTimestamp(),
    actor: 'maintainer',
    data: {
      sourceKey,
      observationEpoch: makeObservationEpoch(identity, {
        deliveryId,
        eventPath: 'trusted_dispatch',
        workerRevision: WORKER_REVISION,
      }),
      ...data,
    },
  });
}

function policyEvent(pullRequest, kind, data, suffix = kind) {
  return makePolicyEvent({
    kind,
    repositoryId: REPOSITORY_ID,
    repository: 'Jamula/Andreja',
    pullNumber: pullRequest.number,
    deliveryId: `fixture:${pullRequest.number}:${suffix}:${sequence + 1}`,
    createdAt: eventTimestamp(),
    actor: 'maintainer',
    data,
  });
}

function bindEvent(pullRequest, issueNumber = 104, suffix = 'bind') {
  return observation(
    pullRequest,
    'bind-issue',
    {
      issueNumber,
      identity: pullIdentity(pullRequest),
      reason: 'Bind the implementation PR to its approved issue policy.',
      auditUrl: `https://github.com/Jamula/Andreja/issues/${issueNumber}`,
    },
    `trusted-issue:${issueNumber}`,
    `${suffix}-${issueNumber}`);
}

function requirementEvent(pullRequest, domain, issueNumber = 104, suffix = '') {
  return observation(
    pullRequest,
    'require-domain',
    {
      domain,
      sourceKind: 'issue-label',
      sourceNumber: issueNumber,
    },
    `issue:${issueNumber}:domain:${domain}`,
    `require-${domain}-${suffix}`);
}

function copilotReview(headSha = HEAD, overrides = {}) {
  return {
    id: 100 + sequence,
    state: 'COMMENTED',
    commit_id: headSha,
    submitted_at: eventTimestamp(),
    body: 'Authenticated Copilot review completed.',
    user: { ...COPILOT_REVIEWER },
    ...overrides,
  };
}

function copilotAttestationEvent(pullRequest, review) {
  return policyEvent(pullRequest, 'copilot-attestation', {
    identity: pullIdentity(pullRequest),
    reviewId: Number(review.id),
    reviewerId: COPILOT_REVIEWER.id,
    reviewerLogin: COPILOT_REVIEWER.login,
    reviewSubmittedAt: review.submitted_at,
  }, `copilot-${review.id}`);
}

function domainReview(pullRequest, domain, policyDigest, overrides = {}) {
  const binding = {
    schemaVersion: 5,
    kind: 'independent-review',
    domain,
    ...pullIdentity(pullRequest),
    policyDigest,
    evidenceUrl: `https://github.com/Jamula/Andreja/pull/${pullRequest.number}` +
      '#pullrequestreview-1',
    summary: `Independent ${domain} review completed for this exact policy.`,
  };
  return {
    id: 500 + sequence,
    state: 'APPROVED',
    commit_id: pullRequest.head.sha,
    submitted_at: eventTimestamp(),
    body: reviewMarker(domain, binding),
    user: { id: 22, login: 'reviewer', type: 'User' },
    ...overrides,
  };
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
      slug: 'independent-specialist',
      runId: 12345,
      runAttempt: 1,
      workflowRevision: '1'.repeat(40),
    },
    artifact: {
      id: 99,
      name: 'review-evidence.json',
      sha256: '2'.repeat(64),
      manifestDigest: '3'.repeat(64),
      downloadedAt: '2026-08-25T17:00:00Z',
    },
    evidenceUrl: `https://github.com/Jamula/Andreja/pull/` +
      `${pullRequest.number}#issuecomment-automation`,
    summary: `Authenticated automated ${domain} review completed.`,
    ...overrides,
  }, `domain-attestation-${domain}`);
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

function snapshotForEvents(events, pullRequest) {
  return foldPolicyEvents(events, [], { identity: pullIdentity(pullRequest) });
}

function mapping(pullRequest, {
  issues = [104],
  reviewers = [],
} = {}) {
  const identity = pullIdentity(pullRequest);
  const value = {
    repositoryId: REPOSITORY_ID,
    repository: 'Jamula/Andreja',
    pullNumber: identity.pullNumber,
    headRepositoryId: Number(pullRequest.head.repo.id),
    headRepository: pullRequest.head.repo.full_name,
    headRef: pullRequest.head.ref,
    headSha: identity.headSha,
    baseRepositoryId: identity.baseRepositoryId,
    baseRepository: identity.baseRepository,
    baseRef: identity.baseRef,
    baseSha: identity.baseSha,
    issueNumbers: issues,
    reviewerLogins: reviewers,
    open: true,
    version: 1,
  };
  value.diffIdentity = securityDigest({
    pullNumber: value.pullNumber,
    headRepositoryId: value.headRepositoryId,
    headRepository: value.headRepository.toLowerCase(),
    headRef: value.headRef,
    headSha: value.headSha,
    baseRepositoryId: value.baseRepositoryId,
    baseRepository: value.baseRepository.toLowerCase(),
    baseRef: value.baseRef,
    baseSha: value.baseSha,
  });
  return value;
}

function canonicalPolicyState(events, comments) {
  return {
    policyEvents: structuredClone(events),
    projections: events.map((event) => {
      const comment = (comments[event.pullNumber] || []).find((candidate) =>
        String(candidate.body || '') === policyEventComment(event));
      if (!comment) {
        throw new Error(`Test fixture lacks projection for ${event.eventId}.`);
      }
      return {
        repositoryId: REPOSITORY_ID,
        repository: 'Jamula/Andreja',
        pullNumber: event.pullNumber,
        eventId: event.eventId,
        commentId: Number(comment.id),
        bodyDigest: securityDigest(comment.body),
      };
    }),
  };
}

function canonicalPolicyStateFromComments(comments) {
  const events = Object.values(comments).flat().map((comment) => {
    const parsed = parsePolicyEventComment(comment.body);
    if (parsed.error) {
      throw new Error(parsed.error);
    }
    return parsed.event;
  });
  return canonicalPolicyState(events, comments);
}

async function appendCanonicalFixture(store, event, comment) {
  await store.appendPolicyLedgerEvent({
    repositoryId: REPOSITORY_ID,
    repository: 'Jamula/Andreja',
    pullNumber: event.pullNumber,
    event,
  });
  await store.upsertPolicyProjection({
    repositoryId: REPOSITORY_ID,
    repository: 'Jamula/Andreja',
    pullNumber: event.pullNumber,
    eventId: event.eventId,
    commentId: Number(comment.id),
    bodyDigest: securityDigest(comment.body),
  });
}

class FakeStore {
  constructor({
    mappings = [],
    mergeGroups = {},
    policyEvents = [],
    projections = [],
  } = {}) {
    this.mappings = new Map(mappings.map((item) =>
      [item.pullNumber, structuredClone({
        ...item,
        version: Number(item.version || 1),
      })]));
    this.mergeGroups = structuredClone(mergeGroups);
    this.generations = [];
    this.sequences = new Map();
    this.deliveries = new Map();
    this.policyEvents = new Map();
    for (const event of policyEvents) {
      const values = this.policyEvents.get(Number(event.pullNumber)) || [];
      values.push(structuredClone(event));
      this.policyEvents.set(Number(event.pullNumber), values);
    }
    this.projections = new Map(projections.map((projection) =>
      [projection.eventId, structuredClone(projection)]));
  }

  async compareAndSwapPullMapping({
    pullNumber,
    expectedVersion,
    mapping: value,
  }) {
    const current = this.mappings.get(Number(pullNumber));
    const currentVersion = Number(current?.version || 0);
    if (currentVersion !== Number(expectedVersion)) {
      return {
        applied: false,
        current: current ? structuredClone(current) : null,
      };
    }
    if (Number(value.version) !== Number(expectedVersion) + 1) {
      throw new Error('Fake CAS received a non-monotonic mapping version.');
    }
    this.mappings.set(Number(pullNumber), structuredClone(value));
    return {
      applied: true,
      mapping: structuredClone(value),
    };
  }

  async getPullMapping(pullNumber) {
    const value = this.mappings.get(Number(pullNumber));
    return value ? structuredClone(value) : null;
  }

  async listPullMappingsByIssue(issueNumber) {
    return [...this.mappings.values()]
      .filter((value) => value.open &&
        value.issueNumbers.includes(Number(issueNumber)))
      .map((value) => structuredClone(value));
  }

  async listPullMappingsByReviewer(login) {
    return [...this.mappings.values()]
      .filter((value) => value.open &&
        value.reviewerLogins.some((candidate) =>
          candidate.toLowerCase() === String(login).toLowerCase()))
      .map((value) => structuredClone(value));
  }

  async listPullMappingsByBase({ repositoryId, ref }) {
    return [...this.mappings.values()]
      .filter((value) => value.open &&
        Number(value.baseRepositoryId) === Number(repositoryId) &&
        value.baseRef === ref)
      .map((value) => structuredClone(value));
  }

  async listOpenPullMappings() {
    return [...this.mappings.values()]
      .filter((value) => value.open)
      .map((value) => structuredClone(value));
  }

  async reserveGeneration({ repositoryId, headSha }) {
    const key = `${repositoryId}:${headSha}`;
    const next = (this.sequences.get(key) || 0) + 1;
    this.sequences.set(key, next);
    const generation = {
      repositoryId,
      headSha,
      sequence: next,
      generationId: `${key}:${next}`,
      active: false,
    };
    this.generations.push(generation);
    return structuredClone(generation);
  }

  async activateGeneration({ generationId, checkRunId, externalId, provenance }) {
    const generation = this.generations.find((candidate) =>
      candidate.generationId === generationId);
    Object.assign(generation, {
      active: true,
      checkRunId,
      externalId,
      provenance: structuredClone(provenance),
    });
  }

  async getNewestGeneration({ repositoryId, headSha }) {
    const values = this.generations.filter((candidate) =>
      Number(candidate.repositoryId) === Number(repositoryId) &&
      candidate.headSha === headSha &&
      candidate.active);
    return values.length > 0 ? structuredClone(values.at(-1)) : null;
  }

  async claimDelivery({ repositoryId, deliveryId, eventPath, workerRevision }) {
    const key = `${repositoryId}:${deliveryId}:${eventPath}:${workerRevision}`;
    if (this.deliveries.has(key)) {
      return false;
    }
    this.deliveries.set(key, { state: 'claimed' });
    return true;
  }

  async completeDelivery({ repositoryId, deliveryId, eventPath, workerRevision }) {
    const key = `${repositoryId}:${deliveryId}:${eventPath}:${workerRevision}`;
    this.deliveries.set(key, { state: 'completed' });
  }

  async failDelivery({
    repositoryId,
    deliveryId,
    eventPath,
    workerRevision,
    reason,
  }) {
    const key = `${repositoryId}:${deliveryId}:${eventPath}:${workerRevision}`;
    this.deliveries.set(key, { state: 'failed', reason });
  }

  async resolveMergeGroupConstituents(group) {
    const numbers = this.mergeGroups[group.id] || [];
    return numbers.map((number) => structuredClone(this.mappings.get(number)))
      .filter(Boolean);
  }

  async appendPolicyLedgerEvent(record) {
    const values = this.policyEvents.get(Number(record.pullNumber)) || [];
    const existing = values.find((event) => event.eventId === record.event.eventId);
    if (existing &&
        securityDigest(existing) !== securityDigest(record.event)) {
      throw new Error('Conflicting canonical policy event.');
    }
    if (!existing) {
      values.push(structuredClone(record.event));
      this.policyEvents.set(Number(record.pullNumber), values);
    }
  }

  async listPolicyLedgerEvents({ pullNumber }) {
    return structuredClone(this.policyEvents.get(Number(pullNumber)) || []);
  }

  async upsertPolicyProjection(projection) {
    this.projections.set(projection.eventId, structuredClone(projection));
  }

  async listPolicyProjections({ pullNumber }) {
    return [...this.projections.values()]
      .filter((projection) =>
        Number(projection.pullNumber) === Number(pullNumber))
      .map((projection) => structuredClone(projection));
  }
}

class FakeClient {
  constructor({
    pulls = [pull()],
    comments = {},
    labels = {},
    reviews = {},
    threads = {},
    permissions = { reviewer: 'write', maintainer: 'admin' },
    appId = APP_ID,
    branches = { main: BASE },
    protectedBranches = ['main'],
    mergeGroups = {},
    specialistRuns = {},
    specialistArtifacts = {},
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
      branches: structuredClone(branches),
      protectedBranches: new Set(protectedBranches),
      mergeGroups: structuredClone(mergeGroups),
      specialistRuns: structuredClone(specialistRuns),
      specialistArtifacts: structuredClone(specialistArtifacts),
    };
    this.callLog = [];
    this.failure = null;
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
        updateComment: async (parameters) => {
          this.#maybeFail('issues.updateComment');
          this.callLog.push(`issues.updateComment:${parameters.comment_id}`);
          for (const commentsForPull of Object.values(this.state.comments)) {
            const comment = commentsForPull.find((candidate) =>
              Number(candidate.id) === Number(parameters.comment_id));
            if (comment) {
              comment.body = parameters.body;
              return { data: structuredClone(comment) };
            }
          }
          throw Object.assign(new Error('Comment not found.'), { status: 404 });
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
        getReview: async ({ pull_number, review_id }) => {
          this.#maybeFail('pulls.getReview');
          this.callLog.push(`pulls.getReview:${pull_number}:${review_id}`);
          return {
            data: structuredClone((this.state.reviews[pull_number] || [])
              .find((candidate) => Number(candidate.id) === Number(review_id))),
          };
        },
        list: endpoint('pulls', async (parameters) => {
          this.#maybeFail('pulls.list');
          const page = Number(parameters.page || 1);
          const perPage = Number(parameters.per_page || 30);
          this.callLog.push(`pulls.list:${page}`);
          const pulls = this.state.pulls.filter((candidate) =>
            candidate.state === (parameters.state || candidate.state));
          const start = (page - 1) * perPage;
          return {
            data: structuredClone(pulls.slice(start, start + perPage)),
          };
        }),
        listReviews: endpoint('reviews'),
      },
      repos: {
        getCollaboratorPermissionLevel: async ({ username }) => {
          this.#maybeFail('repos.permission');
          this.callLog.push(`repos.permission:${username}`);
          return {
            data: { permission: this.state.permissions[username] || 'read' },
          };
        },
        getBranch: async ({ branch }) => {
          this.#maybeFail('repos.getBranch');
          this.callLog.push(`repos.getBranch:${branch}`);
          return {
            data: {
              name: branch,
              protected: this.state.protectedBranches.has(branch),
              commit: { sha: this.state.branches[branch] },
            },
          };
        },
      },
    };
  }

  failNext(name, error) {
    this.failure = { name, error };
  }

  #maybeFail(name) {
    if (this.failure?.name === name) {
      const error = this.failure.error;
      this.failure = null;
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

  async resolveMergeGroupConstituents({ mergeGroupId }) {
    this.#maybeFail('mergeGroup.resolve');
    this.callLog.push(`mergeGroup.resolve:${mergeGroupId}`);
    return structuredClone(this.state.mergeGroups[mergeGroupId] || []);
  }

  async getSpecialistRun({ runId }) {
    this.#maybeFail('specialist.run');
    this.callLog.push(`specialist.run:${runId}`);
    return structuredClone(this.state.specialistRuns[runId]);
  }

  async downloadSpecialistArtifact({ artifactId }) {
    this.#maybeFail('specialist.artifact');
    this.callLog.push(`specialist.artifact:${artifactId}`);
    const artifact = this.state.specialistArtifacts[artifactId];
    return {
      ...structuredClone(artifact),
      bytes: Buffer.from(artifact?.bytes || ''),
    };
  }
}

function seededState({
  pullRequest = pull(),
  issueNumber = 104,
  domains = [],
  labels = {},
  includeCopilot = true,
  permissions,
} = {}) {
  const bind = bindEvent(pullRequest, issueNumber);
  const requirements = domains.map((domain) =>
    requirementEvent(pullRequest, domain, issueNumber));
  const events = [bind, ...requirements];
  const policy = snapshotForEvents(events, pullRequest);
  const reviews = [];
  if (includeCopilot) {
    const review = copilotReview(pullRequest.head.sha);
    reviews.push(review);
    events.push(copilotAttestationEvent(pullRequest, review));
  }
  for (const domain of domains) {
    reviews.push(domainReview(pullRequest, domain, policy.digest));
  }
  const comments = {
    [pullRequest.number]: events.map((event, index) =>
      trustedComment(event, APP_ID, index + 1)),
  };
  const client = new FakeClient({
    pulls: [pullRequest],
    comments,
    labels,
    reviews: { [pullRequest.number]: reviews },
    permissions,
  });
  const store = new FakeStore({
    mappings: [mapping(pullRequest)],
    ...canonicalPolicyState(events, comments),
  });
  return { client, store, policy, events, reviews };
}

function reducedState(domain = 'security') {
  const current = pull();
  const bind = bindEvent(current);
  const requirement = requirementEvent(current, domain);
  const before = snapshotForEvents([bind, requirement], current);
  const reduction = policyEvent(current, 'reduce-policy', {
    identity: pullIdentity(current),
    expectedPolicyDigest: before.digest,
    targets: [{
      eventId: requirement.eventId,
      epochId: requirement.observationEpoch.id,
    }],
    reason: `Authorized ${domain} reduction for this exact diff identity only.`,
    auditUrl: 'https://github.com/Jamula/Andreja/issues/104',
  });
  const review = copilotReview(current.head.sha);
  const events = [
    bind,
    requirement,
    reduction,
    copilotAttestationEvent(current, review),
  ];
  const comments = {
    [current.number]: events.map((event, index) =>
      trustedComment(event, APP_ID, index + 1)),
  };
  const client = new FakeClient({
    pulls: [current],
    comments,
    labels: { 104: [], 115: [] },
    reviews: { [current.number]: [review] },
  });
  const store = new FakeStore({
    mappings: [mapping(current)],
    ...canonicalPolicyState(events, comments),
  });
  return { client, store, events, requirement, reduction };
}

async function runPullEvent(state, pullRequest = state.client.state.pulls[0]) {
  return handleEvent({
    client: state.client,
    store: state.store,
    envelope: envelope('pull_request', {
      action: 'synchronize',
      pull_request: structuredClone(pullRequest),
    }),
    publisherAppId: APP_ID,
    evaluatorOptions: { poll: false, stabilitySeconds: 0 },
  });
}

async function runReconciliation(state, overrides = {}) {
  return handleEvent({
    client: state.client,
    store: state.store,
    envelope: envelope('reconciliation', {
      reason: 'periodic-full-reconciliation',
    }),
    publisherAppId: APP_ID,
    evaluatorOptions: { poll: false, stabilitySeconds: 0 },
    ...overrides,
  });
}

function latestCheck(client, headSha = null) {
  const values = headSha
    ? client.state.checkRuns.filter((run) => run.head_sha === headSha)
    : client.state.checkRuns;
  return values.at(-1);
}

test('drafts, unresolved threads, missing policy, and stale Copilot fail closed', () => {
  const current = pull();
  const bind = bindEvent(current);
  const policy = snapshotForEvents([bind], current);
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
    policy: foldPolicyEvents([], [], { identity: pullIdentity(current) }),
  }).state, 'rejected');
  assert.equal(evaluateReviewCompletion({
    pullRequest: current,
    policy,
    reviews: [copilotReview(NEXT_HEAD)],
  }).state, 'pending');
  assert.equal(evaluateReviewCompletion({
    path: 'merge_group',
    pullRequest: current,
    policy,
  }).state, 'rejected');
});

test('copied policy marker from the wrong App identity is ignored', () => {
  const current = pull();
  const copied = trustedComment(bindEvent(current), OTHER_APP_ID);
  const trusted = trustedPolicyEvents([copied], {
    appId: APP_ID,
    repositoryId: REPOSITORY_ID,
    repository: 'Jamula/Andreja',
    pullNumber: current.number,
  });
  assert.equal(foldPolicyEvents(
    trusted.events,
    trusted.errors,
    { identity: pullIdentity(current) }).initialized, false);
});

test('policy reductions validate historical digest and exact observation epoch', () => {
  const current = pull();
  const bind = bindEvent(current);
  const requirement = requirementEvent(current, 'architecture');
  const before = snapshotForEvents([bind, requirement], current);
  const reduction = policyEvent(current, 'reduce-policy', {
    identity: pullIdentity(current),
    expectedPolicyDigest: before.digest,
    targets: [{
      eventId: requirement.eventId,
      epochId: requirement.observationEpoch.id,
    }],
    reason: 'Authorized removal for the exact current observation epoch.',
    auditUrl: 'https://github.com/Jamula/Andreja/issues/104',
  });
  const reduced = snapshotForEvents([bind, requirement, reduction], current);
  assert.deepEqual(reduced.domains, []);
  assert.equal(reduced.errors.length, 0);

  const reobserved = requirementEvent(
    current,
    'architecture',
    104,
    'new-observation');
  const restored = snapshotForEvents(
    [bind, requirement, reduction, reobserved],
    current);
  assert.deepEqual(restored.domains, ['architecture']);
  assert.equal(restored.activeSources[
    'issue:104:domain:architecture'], reobserved.eventId);

  const staleReduction = policyEvent(current, 'reduce-policy', {
    identity: pullIdentity(current),
    expectedPolicyDigest: before.digest,
    targets: [{
      eventId: requirement.eventId,
      epochId: requirement.observationEpoch.id,
    }],
    reason: 'This stale historical reduction must not affect re-observation.',
    auditUrl: 'https://github.com/Jamula/Andreja/issues/104',
  }, 'stale-reduction');
  const stale = snapshotForEvents(
    [bind, requirement, reduction, reobserved, staleReduction],
    current);
  assert.deepEqual(stale.domains, ['architecture']);
  assert.match(stale.errors.join(' '), /historical digest|observation-epoch/);
});

test('a policy reduction never carries across push and re-observation', () => {
  const current = pull();
  const bind = bindEvent(current);
  const requirement = requirementEvent(current, 'security');
  const before = snapshotForEvents([bind, requirement], current);
  const reduction = policyEvent(current, 'reduce-policy', {
    identity: pullIdentity(current),
    expectedPolicyDigest: before.digest,
    targets: [{
      eventId: requirement.eventId,
      epochId: requirement.observationEpoch.id,
    }],
    reason: 'Exact-diff security requirement reduction for recorded reasons.',
    auditUrl: 'https://github.com/Jamula/Andreja/issues/104',
  });
  const next = pull(115, {
    head: { ...current.head, sha: NEXT_HEAD },
  });
  const stale = foldPolicyEvents(
    [bind, requirement, reduction],
    [],
    { identity: pullIdentity(next) });
  assert.equal(stale.currentEpochComplete, false);
  assert.equal(stale.latestSources[
    'issue:104:domain:security'].reduced, true);
  const carriedBind = bindEvent(next, 104, 'carry');
  const nextRequirement = requirementEvent(next, 'security', 104, 'next-head');
  const snapshot = foldPolicyEvents(
    [bind, requirement, reduction, carriedBind, nextRequirement],
    [],
    { identity: pullIdentity(next) });
  assert.deepEqual(snapshot.domains, ['security']);
  assert.equal(snapshot.currentEpochComplete, true);
});

test('handler re-observes every reduced source after a PR head push', async () => {
  const state = reducedState('security');
  await runPullEvent(state);
  assert.equal(latestCheck(state.client).conclusion, 'success');

  state.client.state.pulls[0].head.sha = NEXT_HEAD;
  await runPullEvent(state);

  const events = state.store.policyEvents.get(115);
  const latestRequirement = events.filter((event) =>
    event.kind === 'require-domain' &&
    event.sourceKey === 'issue:104:domain:security').at(-1);
  assert.equal(latestRequirement.observationEpoch.identity.headSha, NEXT_HEAD);
  assert.notEqual(latestRequirement.eventId, state.requirement.eventId);
  assert.equal(latestCheck(state.client, NEXT_HEAD).conclusion, 'failure');
  assert.match(latestCheck(state.client, NEXT_HEAD).output.summary, /security/);
});

test('handler re-observes every reduced source after live retarget', async () => {
  const state = reducedState('privacy');
  await runPullEvent(state);
  const stalePayload = structuredClone(state.client.state.pulls[0]);
  state.client.state.pulls[0].base = {
    sha: NEXT_BASE,
    ref: 'release',
    repo: {
      id: REPOSITORY_ID,
      full_name: 'Jamula/Andreja',
    },
  };
  await runPullEvent(state, stalePayload);

  const latestRequirement = state.store.policyEvents.get(115)
    .filter((event) => event.kind === 'require-domain' &&
      event.sourceKey === 'issue:104:domain:privacy')
    .at(-1);
  assert.equal(latestRequirement.observationEpoch.identity.baseRef, 'release');
  assert.equal(latestRequirement.observationEpoch.identity.baseSha, NEXT_BASE);
  assert.equal((await state.store.getPullMapping(115)).baseRef, 'release');
  assert.equal(latestCheck(state.client).conclusion, 'failure');
  assert.match(latestCheck(state.client).output.summary, /privacy/);
});

test('base-push handler re-observes every reduced source after base advance', async () => {
  const state = reducedState('architecture');
  await runPullEvent(state);
  state.client.state.pulls[0].base.sha = NEXT_BASE;
  state.client.state.branches.main = NEXT_BASE;

  await handleEvent({
    client: state.client,
    store: state.store,
    envelope: envelope('push', {
      ref: 'refs/heads/main',
      before: BASE,
      after: NEXT_BASE,
    }),
    publisherAppId: APP_ID,
    evaluatorOptions: { poll: false, stabilitySeconds: 0 },
  });

  const latestRequirement = state.store.policyEvents.get(115)
    .filter((event) => event.kind === 'require-domain' &&
      event.sourceKey === 'issue:104:domain:architecture')
    .at(-1);
  assert.equal(latestRequirement.observationEpoch.identity.baseSha, NEXT_BASE);
  assert.notEqual(latestRequirement.eventId, state.requirement.eventId);
  assert.equal(latestCheck(state.client, HEAD).conclusion, 'failure');
  assert.match(latestCheck(state.client, HEAD).output.summary, /architecture/);
});

test('reduction tooling emits exact event and epoch targets only', () => {
  const current = pull();
  const bind = bindEvent(current);
  const requirement = requirementEvent(current, 'privacy');
  const snapshot = snapshotForEvents([bind, requirement], current);
  const targets = reductionTargets(
    JSON.stringify([requirement.eventId]),
    { snapshot },
    pullIdentity(current));
  assert.deepEqual(targets, [{
    eventId: requirement.eventId,
    epochId: requirement.observationEpoch.id,
  }]);
  assert.throws(() => reductionTargets(
    JSON.stringify(['f'.repeat(64)]),
    { snapshot },
    pullIdentity(current)), /stale|epoch/);
});

test('the PR author cannot reduce their own review policy', async () => {
  const current = pull(115, {
    user: { login: 'maintainer', id: 115, type: 'User' },
  });
  const state = seededState({
    pullRequest: current,
    domains: ['security'],
  });
  const identity = pullIdentity(current);
  const requirement = state.events.find((event) =>
    event.kind === 'require-domain' && event.domain === 'security');
  await assert.rejects(() => recordDecision({
    client: state.client,
    store: state.store,
    envelope: envelope('trusted_dispatch', {}),
    publisherAppId: APP_ID,
    command: {
      operation: 'reduce-policy',
      pull_number: identity.pullNumber,
      head_repository_id: identity.headRepositoryId,
      head_repository: identity.headRepository,
      head_ref: identity.headRef,
      head_sha: identity.headSha,
      base_repository_id: identity.baseRepositoryId,
      base_repository: identity.baseRepository,
      base_ref: identity.baseRef,
      base_sha: identity.baseSha,
      diff_identity: identity.diffIdentity,
      expected_policy_digest: state.policy.digest,
      target_event_ids: JSON.stringify([requirement.eventId]),
      reason: 'The PR author must not reduce their own required review.',
      audit_url: 'https://github.com/Jamula/Andreja/issues/104',
    },
  }), /author cannot reduce/);
  assert.equal(
    state.store.policyEvents.get(115).some((event) =>
      event.kind === 'reduce-policy'),
    false);
});

test('trusted policy commands require the full exact diff identity', () => {
  const identity = pullIdentity(pull());
  const inputs = {
    pull_number: identity.pullNumber,
    head_repository_id: identity.headRepositoryId,
    head_repository: identity.headRepository,
    head_ref: identity.headRef,
    head_sha: identity.headSha,
    base_repository_id: identity.baseRepositoryId,
    base_repository: identity.baseRepository,
    base_ref: identity.baseRef,
    base_sha: identity.baseSha,
    diff_identity: identity.diffIdentity,
  };
  assert.deepEqual(inputIdentity(inputs), identity);
  assert.throws(
    () => inputIdentity({ ...inputs, head_ref: '' }),
    /complete diff identity/);
  assert.throws(
    () => inputIdentity({ ...inputs, diff_identity: '0'.repeat(64) }),
    /diff identity digest/);
});

test('repository Actions cannot authenticate or host the publisher contract', async () => {
  const state = seededState();
  const unsafe = envelope('pull_request', {
    action: 'synchronize',
    pull_request: pull(),
  });
  unsafe.delivery.source = 'github-actions';
  unsafe.worker.hostKind = 'github-actions';
  await assert.rejects(() => handleEvent({
    client: state.client,
    store: state.store,
    envelope: unsafe,
    publisherAppId: APP_ID,
  }), /independent-worker|App-webhook|Actions/);
  assert.equal(state.client.state.checkRuns.length, 0);
});

test('direct PR handling creates pending before mutable metadata reads', async () => {
  const state = seededState();
  const result = await runPullEvent(state);
  assert.equal(state.client.callLog[0], 'checks.create');
  assert.equal(result[0].result.state, 'approved');
  assert.equal(latestCheck(state.client).conclusion, 'success');
  assert.equal(latestCheck(state.client).app.id, APP_ID);
});

test('authenticated webhook redelivery cannot create a second writer', async () => {
  const state = seededState();
  const delivered = envelope('pull_request', {
    action: 'synchronize',
    pull_request: structuredClone(state.client.state.pulls[0]),
  });
  const options = {
    client: state.client,
    store: state.store,
    envelope: delivered,
    publisherAppId: APP_ID,
    evaluatorOptions: { poll: false, stabilitySeconds: 0 },
  };
  await handleEvent(options);
  const count = state.client.state.checkRuns.length;
  const duplicate = await handleEvent(options);
  assert.equal(duplicate.duplicate, true);
  assert.equal(state.client.state.checkRuns.length, count);
});

test('delayed H1 webhook leaves the live H2 mapping authoritative', async () => {
  const state = seededState();
  const staleH1 = structuredClone(state.client.state.pulls[0]);
  await runPullEvent(state, staleH1);
  state.client.state.pulls[0].head.sha = NEXT_HEAD;
  await runPullEvent(state);
  const beforeDelayed = await state.store.getPullMapping(115);
  const start = state.client.callLog.length;

  await runPullEvent(state, staleH1);

  const afterDelayed = await state.store.getPullMapping(115);
  assert.equal(afterDelayed.headSha, NEXT_HEAD);
  assert.ok(afterDelayed.version > beforeDelayed.version);
  const log = state.client.callLog.slice(start);
  assert.equal(log[0], 'checks.create');
  assert.ok(log.indexOf('pulls.get:115') > 1);
  const delayedRuns = state.client.state.checkRuns.slice(-2);
  assert.deepEqual(
    delayedRuns.map((run) => run.head_sha),
    [HEAD, NEXT_HEAD]);
  assert.equal(latestCheck(state.client, HEAD).conclusion, 'failure');
  assert.equal(latestCheck(state.client, NEXT_HEAD).conclusion, 'failure');
});

test('delayed review and comment hints cannot regress live H2', async () => {
  const currentH2 = pull(115, {
    head: {
      ...pull().head,
      sha: NEXT_HEAD,
    },
  });
  const state = seededState({ pullRequest: currentH2 });
  await runPullEvent(state);
  const staleH1 = pull();
  const staleReview = copilotReview(HEAD);

  await handleEvent({
    client: state.client,
    store: state.store,
    envelope: envelope('pull_request_review', {
      action: 'submitted',
      pull_request: staleH1,
      review: staleReview,
    }),
    publisherAppId: APP_ID,
    evaluatorOptions: { poll: false, stabilitySeconds: 0 },
  });
  assert.equal((await state.store.getPullMapping(115)).headSha, NEXT_HEAD);
  assert.equal(latestCheck(state.client, NEXT_HEAD).conclusion, 'failure');
  assert.equal(
    state.store.policyEvents.get(115).some((event) =>
      Number(event.reviewId) === Number(staleReview.id)),
    false);

  await handleEvent({
    client: state.client,
    store: state.store,
    envelope: envelope('pull_request_review_comment', {
      action: 'edited',
      pull_request: staleH1,
      comment: { id: 9001 },
    }),
    publisherAppId: APP_ID,
    evaluatorOptions: { poll: false, stabilitySeconds: 0 },
  });
  assert.equal((await state.store.getPullMapping(115)).headSha, NEXT_HEAD);
  assert.equal(latestCheck(state.client, HEAD).conclusion, 'failure');
});

test('delayed pre-retarget Copilot review cannot attest the new base', async () => {
  const state = seededState();
  const stalePull = structuredClone(state.client.state.pulls[0]);
  const staleReview = state.client.state.reviews[115][0];
  state.client.state.pulls[0].base = {
    sha: NEXT_BASE,
    ref: 'release',
    repo: {
      id: REPOSITORY_ID,
      full_name: 'Jamula/Andreja',
    },
  };

  await handleEvent({
    client: state.client,
    store: state.store,
    envelope: envelope('pull_request_review', {
      action: 'submitted',
      pull_request: stalePull,
      review: staleReview,
    }),
    publisherAppId: APP_ID,
    evaluatorOptions: { poll: false, stabilitySeconds: 0 },
  });

  const current = await state.store.getPullMapping(115);
  assert.equal(current.baseRef, 'release');
  assert.equal(current.baseSha, NEXT_BASE);
  assert.equal(
    state.store.policyEvents.get(115).filter((event) =>
      event.kind === 'copilot-attestation').length,
    1);
  assert.equal(latestCheck(state.client, HEAD).conclusion, 'failure');
});

test('delayed close after reopen cannot close a live-open mapping', async () => {
  const state = seededState();
  state.client.state.pulls[0].state = 'closed';
  const closedPayload = structuredClone(state.client.state.pulls[0]);
  await handleEvent({
    client: state.client,
    store: state.store,
    envelope: envelope('pull_request', {
      action: 'closed',
      pull_request: closedPayload,
    }),
    publisherAppId: APP_ID,
    evaluatorOptions: { poll: false, stabilitySeconds: 0 },
  });
  assert.equal((await state.store.getPullMapping(115)).open, false);

  state.client.state.pulls[0].state = 'open';
  await handleEvent({
    client: state.client,
    store: state.store,
    envelope: envelope('pull_request', {
      action: 'reopened',
      pull_request: structuredClone(state.client.state.pulls[0]),
    }),
    publisherAppId: APP_ID,
    evaluatorOptions: { poll: false, stabilitySeconds: 0 },
  });
  const reopened = await state.store.getPullMapping(115);
  assert.equal(reopened.open, true);

  await handleEvent({
    client: state.client,
    store: state.store,
    envelope: envelope('pull_request', {
      action: 'closed',
      pull_request: closedPayload,
    }),
    publisherAppId: APP_ID,
    evaluatorOptions: { poll: false, stabilitySeconds: 0 },
  });
  const afterDelayedClose = await state.store.getPullMapping(115);
  assert.equal(afterDelayedClose.open, true);
  assert.ok(afterDelayedClose.version > reopened.version);
});

test('live PR API failure invalidates durable H2 before failing closed', async () => {
  const currentH2 = pull(115, {
    head: {
      ...pull().head,
      sha: NEXT_HEAD,
    },
  });
  const state = seededState({ pullRequest: currentH2 });
  await runPullEvent(state);
  assert.equal(latestCheck(state.client, NEXT_HEAD).conclusion, 'success');
  const staleH1 = pull();
  state.client.failNext(
    'pulls.get',
    Object.assign(new Error('private live API detail'), { status: 429 }));

  await runPullEvent(state, staleH1);

  assert.equal((await state.store.getPullMapping(115)).headSha, NEXT_HEAD);
  assert.equal(latestCheck(state.client, NEXT_HEAD).conclusion, 'failure');
  assert.match(
    latestCheck(state.client, NEXT_HEAD).output.summary,
    /rate-limited|failed closed/);
  assert.doesNotMatch(
    latestCheck(state.client, NEXT_HEAD).output.summary,
    /private live API detail/);
});

test('head drift during evaluation publishes H2 before mapping mutation', async () => {
  const state = seededState();
  const originalGet = state.client.rest.pulls.get;
  let liveReads = 0;
  state.client.rest.pulls.get = async (parameters) => {
    liveReads += 1;
    if (liveReads === 2) {
      state.client.state.pulls[0].head.sha = NEXT_HEAD;
    }
    return originalGet(parameters);
  };

  await runPullEvent(state);

  assert.equal(liveReads, 2);
  assert.equal((await state.store.getPullMapping(115)).headSha, NEXT_HEAD);
  assert.equal(latestCheck(state.client, NEXT_HEAD).conclusion, 'failure');
  assert.match(
    latestCheck(state.client, NEXT_HEAD).output.summary,
    /head\/base\/diff identity changed/);
});

test('base retarget during evaluation supersedes same-head success', async () => {
  const state = seededState();
  const originalGet = state.client.rest.pulls.get;
  let liveReads = 0;
  state.client.rest.pulls.get = async (parameters) => {
    liveReads += 1;
    if (liveReads === 2) {
      state.client.state.pulls[0].base = {
        sha: NEXT_BASE,
        ref: 'release',
        repo: {
          id: REPOSITORY_ID,
          full_name: 'Jamula/Andreja',
        },
      };
    }
    return originalGet(parameters);
  };

  await runPullEvent(state);

  const current = await state.store.getPullMapping(115);
  assert.equal(current.baseRef, 'release');
  assert.equal(current.baseSha, NEXT_BASE);
  assert.equal(latestCheck(state.client, HEAD).conclusion, 'failure');
  assert.match(
    latestCheck(state.client, HEAD).output.summary,
    /head\/base\/diff identity changed/);
});

test('Copilot webhook attestation binds exact PR, head, and base', async () => {
  const current = pull();
  const bind = bindEvent(current);
  const review = copilotReview();
  const client = new FakeClient({
    pulls: [current],
    comments: { 115: [trustedComment(bind, APP_ID, 1)] },
    reviews: { 115: [review] },
  });
  const store = new FakeStore({
    mappings: [mapping(current)],
    ...canonicalPolicyStateFromComments(client.state.comments),
  });
  await handleEvent({
    client,
    store,
    envelope: envelope('pull_request_review', {
      action: 'submitted',
      pull_request: current,
      review,
    }),
    publisherAppId: APP_ID,
    evaluatorOptions: { poll: false, stabilitySeconds: 0 },
  });
  assert.equal(client.callLog[0], 'checks.create');
  assert.ok(client.state.comments[115].some((comment) =>
    comment.body.includes('Exact-diff Copilot review attested')));
  assert.equal(latestCheck(client).conclusion, 'success');
});

test('issue policy increases are monotonic despite label and reference removal', async () => {
  const state = seededState();
  await runPullEvent(state);
  state.client.state.labels[104] = ['area:architecture'];
  await handleEvent({
    client: state.client,
    store: state.store,
    envelope: envelope('issues', {
      action: 'labeled',
      issue: { number: 104 },
    }),
    publisherAppId: APP_ID,
    evaluatorOptions: { poll: false, stabilitySeconds: 0 },
  });
  assert.equal(latestCheck(state.client).conclusion, 'failure');
  assert.match(latestCheck(state.client).output.summary, /architecture/);

  state.client.state.labels[104] = [];
  state.client.state.pulls[0].body = 'No closing reference remains.';
  await handleEvent({
    client: state.client,
    store: state.store,
    envelope: envelope('issues', {
      action: 'unlabeled',
      issue: { number: 104 },
    }),
    publisherAppId: APP_ID,
    evaluatorOptions: { poll: false, stabilitySeconds: 0 },
  });
  assert.equal(latestCheck(state.client).conclusion, 'failure');
  assert.match(latestCheck(state.client).output.summary, /architecture/);
});

test('deleted App policy projection is rejected and restored from the ledger', async () => {
  const state = seededState();
  await runPullEvent(state);
  const removed = state.client.state.comments[115].find((comment) =>
    comment.body.includes('Trusted issue'));
  state.client.state.comments[115] = state.client.state.comments[115]
    .filter((comment) => comment.id !== removed.id);
  const before = state.client.callLog.length;
  await handleEvent({
    client: state.client,
    store: state.store,
    envelope: envelope('issue_comment', {
      action: 'deleted',
      issue: { number: 115, pull_request: { url: 'pull' } },
      comment: removed,
    }),
    publisherAppId: APP_ID,
    evaluatorOptions: { poll: false, stabilitySeconds: 0 },
  });
  assert.equal(state.client.callLog[before], 'checks.create');
  assert.equal(latestCheck(state.client).conclusion, 'failure');
  assert.match(
    latestCheck(state.client).output.summary,
    /malformed/);
  assert.ok(state.client.state.comments[115].some((comment) =>
    comment.body.includes('Trusted issue')));
  await handleEvent({
    client: state.client,
    store: state.store,
    envelope: envelope('reconciliation', {}),
    publisherAppId: APP_ID,
    evaluatorOptions: { poll: false, stabilitySeconds: 0 },
  });
  assert.equal(latestCheck(state.client).conclusion, 'success');
});

test('edited App policy projection cannot become policy authority', async () => {
  const state = seededState({ domains: ['security'] });
  await runPullEvent(state);
  const projected = state.client.state.comments[115].find((comment) =>
    comment.body.includes('security review requirement'));
  const canonicalBody = projected.body;
  projected.body = projected.body.replace(
    'This record was published by the dedicated review-gate GitHub App.',
    'Edited presentation text must not affect canonical policy.');
  const before = state.client.callLog.length;
  await handleEvent({
    client: state.client,
    store: state.store,
    envelope: envelope('issue_comment', {
      action: 'edited',
      issue: { number: 115, pull_request: { url: 'pull' } },
      comment: structuredClone(projected),
    }),
    publisherAppId: APP_ID,
    evaluatorOptions: { poll: false, stabilitySeconds: 0 },
  });
  assert.equal(state.client.callLog[before], 'checks.create');
  assert.equal(latestCheck(state.client).conclusion, 'failure');
  assert.equal(projected.body, canonicalBody);
  assert.ok(state.store.policyEvents.get(115).some((event) =>
    event.kind === 'require-domain' && event.domain === 'security'));
});

test('marker removal is rejected and canonical projection is restored', async () => {
  const state = seededState({ domains: ['architecture'] });
  await runPullEvent(state);
  const projected = state.client.state.comments[115].find((comment) =>
    comment.body.includes('architecture review requirement'));
  const canonicalBody = projected.body;
  projected.body = projected.body.replace(
    /\n<!-- andreja-review-policy-event:v5:[A-Za-z0-9_-]+ -->/,
    '');
  await handleEvent({
    client: state.client,
    store: state.store,
    envelope: envelope('issue_comment', {
      action: 'edited',
      issue: { number: 115, pull_request: { url: 'pull' } },
      comment: structuredClone(projected),
    }),
    publisherAppId: APP_ID,
    evaluatorOptions: { poll: false, stabilitySeconds: 0 },
  });
  assert.equal(latestCheck(state.client).conclusion, 'failure');
  assert.equal(projected.body, canonicalBody);
  assert.ok(projected.body.includes(POLICY_EVENT_MARKER_PREFIX));
});

test('digest-mismatched policy projection is rejected without reducing policy', async () => {
  const state = seededState({ domains: ['privacy'] });
  await runPullEvent(state);
  const projected = state.client.state.comments[115].find((comment) =>
    comment.body.includes('privacy review requirement'));
  const canonicalBody = projected.body;
  const parsed = parsePolicyEventComment(canonicalBody);
  const altered = {
    ...parsed.event,
    integrityDigest: '0'.repeat(64),
  };
  const encoded = Buffer.from(
    JSON.stringify(stableValue(altered)),
    'utf8').toString('base64url');
  projected.body = canonicalBody.replace(
    new RegExp(`${POLICY_EVENT_MARKER_PREFIX}[A-Za-z0-9_-]+`),
    `${POLICY_EVENT_MARKER_PREFIX}${encoded}`);
  await handleEvent({
    client: state.client,
    store: state.store,
    envelope: envelope('issue_comment', {
      action: 'edited',
      issue: { number: 115, pull_request: { url: 'pull' } },
      comment: structuredClone(projected),
    }),
    publisherAppId: APP_ID,
    evaluatorOptions: { poll: false, stabilitySeconds: 0 },
  });
  assert.equal(latestCheck(state.client).conclusion, 'failure');
  assert.equal(projected.body, canonicalBody);
  assert.ok(state.store.policyEvents.get(115).some((event) =>
    event.kind === 'require-domain' && event.domain === 'privacy'));
});

test('full second snapshot prevents a late thread from publishing success', async () => {
  const state = seededState();
  let slept = false;
  const result = await evaluateHeadUntilStable({
    client: state.client,
    store: state.store,
    envelope: envelope('reconciliation', {}),
    headSha: HEAD,
    publisherAppId: APP_ID,
    poll: false,
    stabilitySeconds: 0,
    sleepFunction: async () => {
      slept = true;
      state.client.state.threads[115] = [{
        id: 'late-thread',
        isResolved: false,
        comments: { nodes: [] },
      }];
    },
  });
  assert.equal(slept, true);
  assert.equal(result.result.state, 'rejected');
});

test('base push paginates durable mappings and publishes live heads before CAS', async () => {
  const first = pull(115);
  const second = pull(116, {
    head: {
      sha: NEXT_HEAD,
      ref: 'feature-116',
      repo: { id: REPOSITORY_ID, full_name: 'Jamula/Andreja' },
    },
    user: { login: 'other-author', id: 116, type: 'User' },
  });
  const firstReview = copilotReview(first.head.sha);
  const secondReview = copilotReview(second.head.sha);
  const client = new FakeClient({
    pulls: [first, second],
    comments: {
      115: [
        trustedComment(bindEvent(first), APP_ID, 1),
        trustedComment(copilotAttestationEvent(first, firstReview), APP_ID, 2),
      ],
      116: [
        trustedComment(bindEvent(second), APP_ID, 3),
        trustedComment(copilotAttestationEvent(second, secondReview), APP_ID, 4),
      ],
    },
    reviews: { 115: [firstReview], 116: [secondReview] },
  });
  const firstMapping = mapping(first);
  const secondMapping = mapping(second);
  const store = new FakeStore({
    mappings: [firstMapping, secondMapping],
    ...canonicalPolicyStateFromComments(client.state.comments),
  });
  const mappingPageCursors = [];
  store.listPullMappingsByBase = async ({ cursor, perPage }) => {
    mappingPageCursors.push(cursor);
    assert.equal(perPage, 100);
    if (cursor === null) {
      return {
        mappings: [structuredClone(firstMapping)],
        pageInfo: { hasNextPage: true, endCursor: 'base-page-2' },
      };
    }
    assert.equal(cursor, 'base-page-2');
    return {
      mappings: [structuredClone(secondMapping)],
      pageInfo: { hasNextPage: false, endCursor: null },
    };
  };
  const originalCas = store.compareAndSwapPullMapping.bind(store);
  const preEvaluationCas = [];
  store.compareAndSwapPullMapping = async (request) => {
    if (request.expectedVersion === 1) {
      preEvaluationCas.push(request.pullNumber);
      assert.equal(
        latestCheck(client, request.mapping.headSha).status,
        'in_progress');
      assert.equal(client.callLog.includes('paginate:pulls'), false);
    }
    return originalCas(request);
  };
  client.state.pulls.forEach((candidate) => {
    candidate.base.sha = NEXT_BASE;
  });
  client.state.branches.main = NEXT_BASE;
  const start = client.callLog.length;
  const result = await handleEvent({
    client,
    store,
    envelope: envelope('push', {
      ref: 'refs/heads/main',
      before: BASE,
      after: NEXT_BASE,
    }),
    publisherAppId: APP_ID,
    evaluatorOptions: { poll: false, stabilitySeconds: 0 },
  });
  const log = client.callLog.slice(start);
  assert.deepEqual(mappingPageCursors, [null, 'base-page-2']);
  assert.deepEqual(preEvaluationCas, [115, 116]);
  assert.ok(log.indexOf('pulls.get:115') < log.indexOf('paginate:pulls'));
  assert.ok(log.indexOf('pulls.get:116') < log.indexOf('paginate:pulls'));
  assert.equal(latestCheck(client, HEAD).conclusion, 'failure');
  assert.equal(latestCheck(client, NEXT_HEAD).conclusion, 'failure');
  assert.equal(latestCheck(client, NEXT_BASE).conclusion, 'success');
  assert.equal(result.results.length, 2);
  assert.match(
    latestCheck(client, NEXT_BASE).output.summary,
    /not merge-group evidence/i);
});

test('base push invalidates reused live H2 before mapping CAS and evaluation', async () => {
  const state = seededState();
  const reusedPull = pull(999, {
    head: {
      sha: NEXT_HEAD,
      ref: 'previously-reviewed',
      repo: { id: REPOSITORY_ID, full_name: 'Jamula/Andreja' },
    },
  });
  const priorGeneration = await createGeneration({
    client: state.client,
    store: state.store,
    envelope: envelope('reconciliation', {}),
    headSha: NEXT_HEAD,
    publisherAppId: APP_ID,
    mappings: [mapping(reusedPull, { issues: [999] })],
    association: { kind: 'reconciliation', pullNumbers: [999] },
  });
  await completeGeneration({
    client: state.client,
    store: state.store,
    envelope: envelope('reconciliation', {}),
    checkRun: priorGeneration,
    publisherAppId: APP_ID,
    result: { state: 'approved', reasons: ['A prior use of H2 completed.'] },
  });
  assert.equal(latestCheck(state.client, NEXT_HEAD).conclusion, 'success');

  state.client.state.pulls[0].head.sha = NEXT_HEAD;
  state.client.state.pulls[0].head.ref = 'feature-115-reused';
  state.client.state.pulls[0].base.sha = NEXT_BASE;
  state.client.state.branches.main = NEXT_BASE;
  const handlerStart = state.client.callLog.length;
  const originalCreate = state.client.rest.checks.create;
  let sawFetchBeforePending = false;
  state.client.rest.checks.create = async (parameters) => {
    if (parameters.head_sha === NEXT_HEAD) {
      assert.ok(state.client.callLog
        .slice(handlerStart)
        .includes('pulls.get:115'));
      sawFetchBeforePending = true;
    }
    return originalCreate(parameters);
  };
  const originalCas =
    state.store.compareAndSwapPullMapping.bind(state.store);
  let sawPendingBeforeMapping = false;
  state.store.compareAndSwapPullMapping = async (request) => {
    if (request.pullNumber === 115 && request.expectedVersion === 1) {
      const runs = state.client.state.checkRuns.filter((run) =>
        run.head_sha === NEXT_HEAD);
      assert.equal(runs.at(-2).conclusion, 'success');
      assert.equal(runs.at(-1).status, 'in_progress');
      sawPendingBeforeMapping = true;
    }
    return originalCas(request);
  };
  const originalPaginate = state.client.paginate.bind(state.client);
  let sawPendingBeforeEvaluation = false;
  state.client.paginate = async (operation, parameters) => {
    if (operation.kind === 'pulls') {
      assert.equal(latestCheck(state.client, NEXT_HEAD).status, 'in_progress');
      sawPendingBeforeEvaluation = true;
    }
    return originalPaginate(operation, parameters);
  };

  await handleEvent({
    client: state.client,
    store: state.store,
    envelope: envelope('push', {
      ref: 'refs/heads/main',
      before: BASE,
      after: NEXT_BASE,
    }),
    publisherAppId: APP_ID,
    evaluatorOptions: { poll: false, stabilitySeconds: 0 },
  });

  assert.equal(sawFetchBeforePending, true);
  assert.equal(sawPendingBeforeMapping, true);
  assert.equal(sawPendingBeforeEvaluation, true);
  const current = await state.store.getPullMapping(115);
  assert.equal(current.headSha, NEXT_HEAD);
  assert.equal(current.headRef, 'feature-115-reused');
  assert.equal(current.baseSha, NEXT_BASE);
  assert.equal(latestCheck(state.client, NEXT_HEAD).conclusion, 'failure');
  assert.ok(state.store.policyEvents.get(115).some((event) =>
    event.kind === 'bind-issue' &&
    event.observationEpoch.identity.headSha === NEXT_HEAD &&
    event.observationEpoch.identity.baseSha === NEXT_BASE));
});

test('base push API rate limit fails mapped and base generations closed', async () => {
  const state = seededState();
  state.client.state.pulls[0].base.sha = NEXT_BASE;
  state.client.state.branches.main = NEXT_BASE;
  state.client.failNext(
    'pulls.get',
    Object.assign(new Error('private base-push detail'), { status: 429 }));

  const result = await handleEvent({
    client: state.client,
    store: state.store,
    envelope: envelope('push', {
      ref: 'refs/heads/main',
      before: BASE,
      after: NEXT_BASE,
    }),
    publisherAppId: APP_ID,
    evaluatorOptions: { poll: false, stabilitySeconds: 0 },
  });

  assert.equal((await state.store.getPullMapping(115)).headSha, HEAD);
  assert.equal(latestCheck(state.client, HEAD).conclusion, 'failure');
  assert.equal(latestCheck(state.client, NEXT_BASE).conclusion, 'failure');
  assert.ok(result.results.every((item) => item.result.state === 'rejected'));
  assert.match(
    latestCheck(state.client, NEXT_BASE).output.summary,
    /rate-limited|failed closed/);
  assert.doesNotMatch(
    latestCheck(state.client, NEXT_BASE).output.summary,
    /private base-push detail/);
});

test('base mapping pagination rate limit fails partial pages closed', async () => {
  const state = seededState();
  state.client.state.pulls[0].base.sha = NEXT_BASE;
  state.client.state.branches.main = NEXT_BASE;
  const durable = await state.store.getPullMapping(115);
  const cursors = [];
  state.store.listPullMappingsByBase = async ({ cursor }) => {
    cursors.push(cursor);
    if (cursor === null) {
      return {
        mappings: [durable],
        pageInfo: { hasNextPage: true, endCursor: 'next-base-page' },
      };
    }
    throw Object.assign(
      new Error('private durable-page detail'),
      { status: 429 });
  };

  await handleEvent({
    client: state.client,
    store: state.store,
    envelope: envelope('push', {
      ref: 'refs/heads/main',
      before: BASE,
      after: NEXT_BASE,
    }),
    publisherAppId: APP_ID,
    evaluatorOptions: { poll: false, stabilitySeconds: 0 },
  });

  assert.deepEqual(cursors, [null, 'next-base-page']);
  assert.equal(latestCheck(state.client, HEAD).conclusion, 'failure');
  assert.equal(latestCheck(state.client, NEXT_BASE).conclusion, 'failure');
  assert.match(
    latestCheck(state.client, NEXT_BASE).output.summary,
    /rate-limited|failed closed/);
  assert.doesNotMatch(
    latestCheck(state.client, NEXT_BASE).output.summary,
    /private durable-page detail/);
});

test('push handler rejects tags, feature refs, and deletions without a check', async (t) => {
  const cases = [
    {
      name: 'tag',
      payload: { ref: 'refs/tags/v1.0.0', after: NEXT_BASE },
    },
    {
      name: 'feature branch',
      payload: { ref: 'refs/heads/feature-attacker', after: NEXT_BASE },
    },
    {
      name: 'deletion',
      payload: {
        ref: 'refs/heads/main',
        after: '0'.repeat(40),
        deleted: true,
      },
    },
  ];
  for (const candidate of cases) {
    await t.test(candidate.name, async () => {
      const client = new FakeClient();
      const store = new FakeStore();
      await assert.rejects(() => handleEvent({
        client,
        store,
        envelope: envelope('push', candidate.payload),
        publisherAppId: APP_ID,
        evaluatorOptions: { poll: false, stabilitySeconds: 0 },
      }), /rejects|allowlisted protected/);
      assert.equal(client.state.checkRuns.length, 0);
    });
  }
});

test('push handler rejects a stale supplied protected-branch tip', async () => {
  const client = new FakeClient({ branches: { main: BASE } });
  const store = new FakeStore();
  const result = await handleEvent({
    client,
    store,
    envelope: envelope('push', {
      ref: 'refs/heads/main',
      before: '9'.repeat(40),
      after: NEXT_BASE,
      deleted: false,
    }),
    publisherAppId: APP_ID,
    evaluatorOptions: { poll: false, stabilitySeconds: 0 },
  });
  assert.equal(result.results.length, 0);
  assert.equal(latestCheck(client, NEXT_BASE).conclusion, 'failure');
  assert.doesNotMatch(
    latestCheck(client, NEXT_BASE).output.title,
    /Not applicable/);
  assert.equal(
    client.state.checkRuns.some((run) => run.conclusion === 'success'),
    false);
});

test('merge group revalidates all constituent PRs against the current base', async () => {
  const first = pull(115);
  const second = pull(116, {
    head: {
      sha: NEXT_HEAD,
      ref: 'feature-116',
      repo: { id: REPOSITORY_ID, full_name: 'Jamula/Andreja' },
    },
  });
  const firstReview = copilotReview(first.head.sha);
  const secondReview = copilotReview(second.head.sha);
  const client = new FakeClient({
    pulls: [first, second],
    comments: {
      115: [
        trustedComment(bindEvent(first), APP_ID, 1),
        trustedComment(copilotAttestationEvent(first, firstReview), APP_ID, 2),
      ],
      116: [
        trustedComment(bindEvent(second), APP_ID, 3),
        trustedComment(copilotAttestationEvent(second, secondReview), APP_ID, 4),
      ],
    },
    reviews: { 115: [firstReview], 116: [secondReview] },
    branches: { main: BASE },
    mergeGroups: { 'merge-group-1': [115, 116] },
  });
  const store = new FakeStore({
    mappings: [mapping(first), mapping(second)],
    mergeGroups: { 'merge-group-1': [115, 116] },
    ...canonicalPolicyStateFromComments(client.state.comments),
  });
  const event = envelope('merge_group', {
    action: 'checks_requested',
    merge_group: {
      id: 'merge-group-1',
      head_sha: MERGE_HEAD,
      head_ref: 'gh-readonly-queue/main/pr-115',
      base_sha: BASE,
      base_ref: 'refs/heads/main',
    },
  });
  const result = await handleEvent({
    client,
    store,
    envelope: event,
    publisherAppId: APP_ID,
    evaluatorOptions: { stabilitySeconds: 0 },
  });
  assert.equal(result.result.state, 'approved');
  assert.equal(latestCheck(client, MERGE_HEAD).conclusion, 'success');
  assert.match(latestCheck(client, MERGE_HEAD).output.summary, /constituent PR/);

  client.state.pulls[1].draft = true;
  const blocked = await handleEvent({
    client,
    store,
    envelope: envelope('merge_group', event.payload),
    publisherAppId: APP_ID,
    evaluatorOptions: { stabilitySeconds: 0 },
  });
  assert.equal(blocked.result.state, 'rejected');
  assert.equal(latestCheck(client, MERGE_HEAD).conclusion, 'failure');
  assert.match(latestCheck(client, MERGE_HEAD).output.summary, /PR #116/);
});

test('merge group fails closed on constituent or live-base disagreement', async () => {
  const current = pull();
  const review = copilotReview();
  const client = new FakeClient({
    pulls: [current],
    comments: {
      115: [
        trustedComment(bindEvent(current), APP_ID, 1),
        trustedComment(copilotAttestationEvent(current, review), APP_ID, 2),
      ],
    },
    reviews: { 115: [review] },
    branches: { main: NEXT_BASE },
    mergeGroups: { group: [115] },
  });
  const store = new FakeStore({
    mappings: [mapping(current)],
    mergeGroups: { group: [115] },
    ...canonicalPolicyStateFromComments(client.state.comments),
  });
  const result = await handleEvent({
    client,
    store,
    envelope: envelope('merge_group', {
      merge_group: {
        id: 'group',
        head_sha: MERGE_HEAD,
        base_sha: BASE,
        base_ref: 'refs/heads/main',
      },
    }),
    publisherAppId: APP_ID,
    evaluatorOptions: { stabilitySeconds: 0 },
  });
  assert.equal(result.result.state, 'rejected');
  assert.match(result.result.reasons.join(' '), /current base/);
});

test('reviewer authorization revocation supersedes a successful head', async () => {
  const state = seededState({ domains: ['quality'] });
  await runPullEvent(state);
  assert.equal(latestCheck(state.client).conclusion, 'success');
  const stored = await state.store.getPullMapping(115);
  assert.deepEqual(stored.reviewerLogins, ['reviewer']);

  state.client.state.permissions.reviewer = 'read';
  const before = state.client.callLog.length;
  await handleEvent({
    client: state.client,
    store: state.store,
    envelope: envelope('membership', {
      action: 'removed',
      membership: { user: { login: 'reviewer' } },
    }),
    publisherAppId: APP_ID,
    evaluatorOptions: { poll: false, stabilitySeconds: 0 },
  });
  assert.equal(state.client.callLog[before], 'checks.create');
  assert.equal(latestCheck(state.client).conclusion, 'failure');
  assert.match(latestCheck(state.client).output.summary, /not authorized/);
});

test('issue and reviewer invalidations target current H2 after delayed H1', async () => {
  const state = seededState({ domains: ['quality'] });
  const staleH1 = structuredClone(state.client.state.pulls[0]);
  await runPullEvent(state, staleH1);
  state.client.state.pulls[0].head.sha = NEXT_HEAD;
  await runPullEvent(state, staleH1);
  const current = await state.store.getPullMapping(115);
  assert.equal(current.headSha, NEXT_HEAD);
  assert.deepEqual(current.issueNumbers, [104]);
  assert.deepEqual(current.reviewerLogins, ['reviewer']);
  const h1Count = state.client.state.checkRuns.filter((run) =>
    run.head_sha === HEAD).length;

  state.client.state.labels[104] = ['area:architecture'];
  await handleEvent({
    client: state.client,
    store: state.store,
    envelope: envelope('issues', {
      action: 'labeled',
      issue: { number: 104 },
    }),
    publisherAppId: APP_ID,
    evaluatorOptions: { poll: false, stabilitySeconds: 0 },
  });
  assert.equal(latestCheck(state.client).head_sha, NEXT_HEAD);

  state.client.state.permissions.reviewer = 'read';
  await handleEvent({
    client: state.client,
    store: state.store,
    envelope: envelope('membership', {
      action: 'removed',
      membership: { user: { login: 'reviewer' } },
    }),
    publisherAppId: APP_ID,
    evaluatorOptions: { poll: false, stabilitySeconds: 0 },
  });
  assert.equal(latestCheck(state.client).head_sha, NEXT_HEAD);
  assert.equal(
    state.client.state.checkRuns.filter((run) => run.head_sha === HEAD).length,
    h1Count);
  assert.equal((await state.store.getPullMapping(115)).headSha, NEXT_HEAD);
});

test('periodic reconciliation catches dropped thread and revocation deliveries', async () => {
  const state = seededState({ domains: ['architecture'] });
  await runPullEvent(state);
  state.client.state.permissions.reviewer = 'read';
  state.client.state.threads[115] = [{
    id: 'dropped-thread-event',
    isResolved: false,
    comments: { nodes: [] },
  }];
  const before = state.client.callLog.length;
  await handleEvent({
    client: state.client,
    store: state.store,
    envelope: envelope('reconciliation', {
      reason: 'periodic-full-reconciliation',
    }),
    publisherAppId: APP_ID,
    evaluatorOptions: { poll: false, stabilitySeconds: 0 },
  });
  const log = state.client.callLog.slice(before);
  assert.equal(log[0], 'checks.create');
  assert.ok(log.indexOf('paginate:pulls') > 0);
  assert.equal(latestCheck(state.client).conclusion, 'failure');
  assert.match(latestCheck(state.client).output.summary, /unresolved|not authorized/);
});

test('reconciliation recovers a dropped reopen from closed v5 idempotently', async () => {
  const state = seededState();
  const closed = await state.store.getPullMapping(115);
  closed.open = false;
  closed.version = 5;
  state.store.mappings.set(115, structuredClone(closed));
  const originalCas =
    state.store.compareAndSwapPullMapping.bind(state.store);
  const expectedVersions = [];
  state.store.compareAndSwapPullMapping = async (request) => {
    expectedVersions.push(request.expectedVersion);
    if (request.expectedVersion === 5) {
      assert.equal(latestCheck(state.client, HEAD).status, 'in_progress');
    }
    return originalCas(request);
  };

  await runReconciliation(state);

  const first = await state.store.getPullMapping(115);
  const firstIdentity = exactMappingIdentity(first);
  const eventCount = state.store.policyEvents.get(115).length;
  assert.equal(first.open, true);
  assert.equal(expectedVersions[0], 5);
  assert.equal(latestCheck(state.client, HEAD).conclusion, 'success');

  await runReconciliation(state);

  const second = await state.store.getPullMapping(115);
  assert.equal(second.open, true);
  assert.deepEqual(exactMappingIdentity(second), firstIdentity);
  assert.equal(state.store.policyEvents.get(115).length, eventCount);
  assert.ok(second.version > first.version);
  assert.equal(latestCheck(state.client, HEAD).conclusion, 'success');
  assert.equal(
    state.client.state.checkRuns.filter((run) =>
      run.head_sha === HEAD && run.conclusion === 'failure').length,
    0);
});

test('dropped-reopen CAS conflict fails closed and the next sweep retries', async () => {
  const state = seededState();
  const closed = await state.store.getPullMapping(115);
  closed.open = false;
  closed.version = 5;
  state.store.mappings.set(115, structuredClone(closed));
  const originalCas =
    state.store.compareAndSwapPullMapping.bind(state.store);
  const expectedVersions = [];
  let conflict = true;
  state.store.compareAndSwapPullMapping = async (request) => {
    expectedVersions.push(request.expectedVersion);
    if (conflict && request.expectedVersion === 5) {
      conflict = false;
      const concurrent = { ...structuredClone(closed), version: 6 };
      state.store.mappings.set(115, concurrent);
      return { applied: false, current: structuredClone(concurrent) };
    }
    return originalCas(request);
  };

  const failed = await runReconciliation(state);

  assert.equal((await state.store.getPullMapping(115)).open, false);
  assert.equal(latestCheck(state.client, HEAD).conclusion, 'failure');
  assert.match(
    latestCheck(state.client, HEAD).output.summary,
    /mapping changed concurrently|failed closed/);
  assert.ok(failed.some((item) => item.result.state === 'rejected'));

  const retried = await runReconciliation(state);

  const recovered = await state.store.getPullMapping(115);
  assert.equal(recovered.open, true);
  assert.deepEqual(expectedVersions.slice(0, 3), [5, 6, 7]);
  assert.equal(latestCheck(state.client, HEAD).conclusion, 'success');
  assert.ok(retried.some((item) => item.result.state === 'approved'));
});

test('reconciliation detects a dropped synchronize on the current head', async () => {
  const state = seededState();
  await runPullEvent(state);
  assert.equal(latestCheck(state.client).conclusion, 'success');
  state.client.state.pulls[0].head.sha = NEXT_HEAD;
  const originalCas =
    state.store.compareAndSwapPullMapping.bind(state.store);
  state.store.compareAndSwapPullMapping = async (request) => {
    if (request.mapping.headSha === NEXT_HEAD) {
      assert.equal(latestCheck(state.client, NEXT_HEAD)?.status, 'in_progress');
    }
    return originalCas(request);
  };
  const before = state.client.callLog.length;
  await runReconciliation(state);
  const log = state.client.callLog.slice(before);
  assert.equal(log[0], 'checks.create');
  assert.ok(log.indexOf('pulls.list:1') > 0);
  assert.ok(log.lastIndexOf('checks.create') > log.indexOf('pulls.list:1'));
  assert.equal(latestCheck(state.client, NEXT_HEAD).conclusion, 'failure');
  assert.equal((await state.store.getPullMapping(115)).headSha, NEXT_HEAD);
  assert.ok(state.store.policyEvents.get(115).some((event) =>
    event.kind === 'bind-issue' &&
    event.observationEpoch.identity.headSha === NEXT_HEAD));
});

test('reconciliation supersedes success on a reused commit', async () => {
  const state = seededState();
  await runPullEvent(state);
  const reusedPull = pull(999, {
    head: {
      id: 999,
      sha: NEXT_HEAD,
      ref: 'previously-reviewed',
      repo: { id: REPOSITORY_ID, full_name: 'Jamula/Andreja' },
    },
  });
  const priorGeneration = await createGeneration({
    client: state.client,
    store: state.store,
    envelope: envelope('reconciliation', {}),
    headSha: NEXT_HEAD,
    publisherAppId: APP_ID,
    mappings: [mapping(reusedPull, { issues: [999] })],
    association: { kind: 'reconciliation', pullNumbers: [999] },
  });
  await completeGeneration({
    client: state.client,
    store: state.store,
    envelope: envelope('reconciliation', {}),
    checkRun: priorGeneration,
    publisherAppId: APP_ID,
    result: { state: 'approved', reasons: ['Prior exact diff was complete.'] },
  });
  assert.equal(latestCheck(state.client, NEXT_HEAD).conclusion, 'success');

  state.client.state.pulls[0].head.sha = NEXT_HEAD;
  state.client.state.pulls[0].head.ref = 'feature-115-rebased';
  await runReconciliation(state);
  const runs = state.client.state.checkRuns.filter((run) =>
    run.head_sha === NEXT_HEAD);
  assert.ok(runs.length >= 2);
  assert.equal(runs.at(-1).conclusion, 'failure');
  assert.ok(runs.at(-1).id > priorGeneration.id);
  assert.match(runs.at(-1).output.summary, /Copilot|policy/);
});

test('reconciliation detects dropped retarget and base identity drift', async () => {
  const state = seededState();
  await runPullEvent(state);
  const beforeMapping = await state.store.getPullMapping(115);
  state.client.state.pulls[0].base = {
    sha: NEXT_BASE,
    ref: 'release',
    repo: {
      id: REPOSITORY_ID,
      full_name: 'Jamula/Andreja',
    },
  };
  await runReconciliation(state);
  const refreshed = await state.store.getPullMapping(115);
  assert.equal(refreshed.baseRef, 'release');
  assert.equal(refreshed.baseSha, NEXT_BASE);
  assert.notEqual(refreshed.diffIdentity, beforeMapping.diffIdentity);
  assert.equal(latestCheck(state.client, HEAD).conclusion, 'failure');
  assert.ok(state.store.policyEvents.get(115).some((event) =>
    event.kind === 'bind-issue' &&
    event.observationEpoch.identity.baseRef === 'release' &&
    event.observationEpoch.identity.baseSha === NEXT_BASE));
});

test('concurrent webhook wins a reconciliation mapping CAS conflict', async () => {
  const state = seededState();
  await runPullEvent(state);
  state.client.state.pulls[0].head.sha = NEXT_HEAD;
  const originalCas =
    state.store.compareAndSwapPullMapping.bind(state.store);
  let webhookInterleaved = false;
  state.store.compareAndSwapPullMapping = async (request) => {
    if (!webhookInterleaved &&
        request.mapping.headSha === NEXT_HEAD) {
      webhookInterleaved = true;
      await runPullEvent(state);
    }
    return originalCas(request);
  };

  const result = await runReconciliation(state);

  assert.equal(webhookInterleaved, true);
  assert.equal((await state.store.getPullMapping(115)).headSha, NEXT_HEAD);
  assert.equal(latestCheck(state.client, NEXT_HEAD).conclusion, 'failure');
  assert.match(
    latestCheck(state.client, NEXT_HEAD).output.summary,
    /mapping changed concurrently|failed closed/);
  assert.ok(result.some((item) => item.result.state === 'rejected'));
});

test('evaluation CAS conflict invalidates a newer live-head success', async () => {
  const state = seededState();
  const originalCas =
    state.store.compareAndSwapPullMapping.bind(state.store);
  let interleaved = false;
  state.store.compareAndSwapPullMapping = async (request) => {
    if (!interleaved &&
        request.mapping.headSha === HEAD &&
        request.expectedVersion === 2) {
      interleaved = true;
      state.client.state.pulls[0].head.sha = NEXT_HEAD;
      const next = mapping(state.client.state.pulls[0], {
        issues: request.mapping.issueNumbers,
        reviewers: request.mapping.reviewerLogins,
      });
      next.version = request.expectedVersion + 1;
      const write = await originalCas({
        ...request,
        mapping: next,
      });
      assert.equal(write.applied, true);
      const event = envelope('reconciliation', {});
      const generation = await createGeneration({
        client: state.client,
        store: state.store,
        envelope: event,
        headSha: NEXT_HEAD,
        publisherAppId: APP_ID,
        mappings: [next],
        association: {
          kind: 'concurrent-live-writer',
          pullNumbers: [115],
        },
      });
      await completeGeneration({
        client: state.client,
        store: state.store,
        envelope: event,
        checkRun: generation,
        publisherAppId: APP_ID,
        result: {
          state: 'approved',
          reasons: ['Concurrent writer briefly completed live H2.'],
        },
      });
    }
    return originalCas(request);
  };

  await runPullEvent(state);

  assert.equal(interleaved, true);
  assert.equal((await state.store.getPullMapping(115)).headSha, NEXT_HEAD);
  const h2Runs = state.client.state.checkRuns.filter((run) =>
    run.head_sha === NEXT_HEAD);
  assert.equal(h2Runs.at(-2).conclusion, 'success');
  assert.equal(h2Runs.at(-1).conclusion, 'failure');
  assert.match(
    h2Runs.at(-1).output.summary,
    /mapping changed concurrently/);
});

test('open PR reconciliation discovery uses complete REST pagination', async () => {
  const pulls = Array.from({ length: 205 }, (_, index) => pull(index + 1));
  const client = new FakeClient({ pulls });
  const result = await listAllOpenPulls({
    client,
    envelope: envelope('reconciliation', {}),
  });
  assert.equal(result.length, 205);
  assert.deepEqual(
    client.callLog.filter((entry) => entry.startsWith('pulls.list:')),
    ['pulls.list:1', 'pulls.list:2', 'pulls.list:3']);
  assert.equal(result[0].number, 1);
  assert.equal(result.at(-1).number, 205);
});

test('reconciliation API rate limit fails the newest known heads closed', async () => {
  const state = seededState();
  await runPullEvent(state);
  state.client.failNext(
    'pulls.list',
    Object.assign(new Error('private reconciliation detail'), { status: 429 }));
  const before = state.client.callLog.length;
  await runReconciliation(state);
  assert.equal(state.client.callLog[before], 'checks.create');
  assert.equal(latestCheck(state.client).conclusion, 'failure');
  assert.match(latestCheck(state.client).output.summary, /rate-limited|failed closed/);
  assert.doesNotMatch(
    latestCheck(state.client).output.summary,
    /private reconciliation detail/);
});

test('rate limit after pending publication leaves newest generation failed', async () => {
  const state = seededState();
  state.client.failNext(
    'pulls',
    Object.assign(new Error('private rate-limit detail'), { status: 429 }));
  await runPullEvent(state);
  const latest = latestCheck(state.client);
  assert.equal(state.client.callLog[0], 'checks.create');
  assert.equal(latest.conclusion, 'failure');
  assert.match(latest.output.summary, /failed closed/);
  assert.doesNotMatch(latest.output.summary, /private rate-limit/);
  assert.match(sanitizedApiFailure({ status: 429 }), /failed closed/);
});

test('check creation rejects a spoofed publisher App identity', async () => {
  const state = seededState();
  state.client.state.appId = OTHER_APP_ID;
  await assert.rejects(() => createGeneration({
    client: state.client,
    store: state.store,
    envelope: envelope('pull_request', {
      action: 'synchronize',
      pull_request: pull(),
    }),
    headSha: HEAD,
    publisherAppId: APP_ID,
    mappings: [mapping(pull())],
    association: { kind: 'pull_request', pullNumbers: [115] },
  }), /publisher/);
  assert.equal(
    (await state.store.getNewestGeneration({
      repositoryId: REPOSITORY_ID,
      headSha: HEAD,
    })),
    null);
});

test('startup and missing durable-state failures do not claim readiness', async () => {
  const client = new FakeClient();
  await assert.rejects(() => handleEvent({
    client,
    store: {},
    envelope: envelope('pull_request', {
      action: 'opened',
      pull_request: pull(),
    }),
    publisherAppId: APP_ID,
  }), /durable store/);
  assert.equal(client.state.checkRuns.length, 0);
  assert.throws(() => rulesetMain(['apply-rollout']), new RegExp(
    ROLLOUT_BLOCKER_CODE));
});

test('concurrent stale writer cannot complete after a newer generation', async () => {
  const state = seededState();
  const event = envelope('pull_request', {
    action: 'synchronize',
    pull_request: pull(),
  });
  const map = mapping(pull());
  const oldRun = await createGeneration({
    client: state.client,
    store: state.store,
    envelope: event,
    headSha: HEAD,
    publisherAppId: APP_ID,
    mappings: [map],
    association: { kind: 'pull_request', pullNumbers: [115] },
  });
  const newRun = await createGeneration({
    client: state.client,
    store: state.store,
    envelope: event,
    headSha: HEAD,
    publisherAppId: APP_ID,
    mappings: [map],
    association: { kind: 'pull_request', pullNumbers: [115] },
  });
  const approved = { state: 'approved', reasons: ['complete'] };
  assert.equal(await completeGeneration({
    client: state.client,
    store: state.store,
    envelope: event,
    checkRun: oldRun,
    publisherAppId: APP_ID,
    result: approved,
  }), false);
  assert.equal(await completeGeneration({
    client: state.client,
    store: state.store,
    envelope: event,
    checkRun: newRun,
    publisherAppId: APP_ID,
    result: approved,
  }), true);
  assert.equal(latestCheck(state.client).conclusion, 'success');
});

test('two PRs sharing a head aggregate every exact PR policy', async () => {
  const first = pull(115);
  const second = pull(205, {
    body: 'Closes #205',
    user: { login: 'other-author', id: 205, type: 'User' },
  });
  const firstReview = copilotReview();
  const secondReview = copilotReview();
  const secondBind = bindEvent(second, 205);
  const secondRequirement = requirementEvent(second, 'quality', 205);
  const client = new FakeClient({
    pulls: [first, second],
    comments: {
      115: [
        trustedComment(bindEvent(first), APP_ID, 1),
        trustedComment(copilotAttestationEvent(first, firstReview), APP_ID, 2),
      ],
      205: [
        trustedComment(secondBind, APP_ID, 3),
        trustedComment(secondRequirement, APP_ID, 4),
        trustedComment(copilotAttestationEvent(second, secondReview), APP_ID, 5),
      ],
    },
    reviews: { 115: [firstReview], 205: [secondReview] },
  });
  const store = new FakeStore({
    mappings: [mapping(first), mapping(second, { issues: [205] })],
    ...canonicalPolicyStateFromComments(client.state.comments),
  });
  const blocked = await evaluateHeadSnapshot({
    client,
    store,
    envelope: envelope('reconciliation', {}),
    headSha: HEAD,
    publisherAppId: APP_ID,
  });
  assert.equal(blocked.result.state, 'pending');
  assert.match(blocked.result.reasons.join(' '), /PR #205/);
});

test('newest domain rejection and author self-review never fall back', async () => {
  const state = seededState({ domains: ['security'] });
  const approved = state.client.state.reviews[115]
    .find((review) => review.user?.login === 'reviewer');
  state.client.state.reviews[115].push({
    ...approved,
    id: approved.id + 1000,
    state: 'CHANGES_REQUESTED',
    submitted_at: eventTimestamp(),
  });
  let result = await evaluateHeadSnapshot({
    client: state.client,
    store: state.store,
    envelope: envelope('reconciliation', {}),
    headSha: HEAD,
    publisherAppId: APP_ID,
  });
  assert.equal(result.result.state, 'rejected');

  const self = seededState({ domains: ['security'] });
  const policy = self.policy;
  self.client.state.reviews[115] = self.client.state.reviews[115]
    .filter((review) => review.user?.login !== 'reviewer');
  self.client.state.reviews[115].push(domainReview(
    self.client.state.pulls[0],
    'security',
    policy.digest,
    { user: { login: 'pr-author-115', id: 115, type: 'User' } }));
  result = await evaluateHeadSnapshot({
    client: self.client,
    store: self.store,
    envelope: envelope('reconciliation', {}),
    headSha: HEAD,
    publisherAppId: APP_ID,
  });
  assert.equal(result.result.state, 'rejected');
  assert.match(result.result.reasons.join(' '), /author cannot/);
});

test('authenticated current-diff specialist App evidence supports one-human model', async () => {
  const current = pull();
  const bind = bindEvent(current);
  const requirement = requirementEvent(current, 'privacy');
  const policy = snapshotForEvents([bind, requirement], current);
  const review = copilotReview();
  const events = [
    bind,
    requirement,
    copilotAttestationEvent(current, review),
    domainAttestationEvent(current, 'privacy', policy.digest),
  ];
  const client = new FakeClient({
    pulls: [current],
    comments: {
      115: events.map((event, index) =>
        trustedComment(event, APP_ID, index + 1)),
    },
    reviews: { 115: [review] },
  });
  const store = new FakeStore({
    mappings: [mapping(current)],
    ...canonicalPolicyStateFromComments(client.state.comments),
  });
  const result = await evaluateHeadSnapshot({
    client,
    store,
    envelope: envelope('reconciliation', {}),
    headSha: HEAD,
    publisherAppId: APP_ID,
  });
  assert.equal(result.result.state, 'approved');
  client.state.pulls[0].base.sha = NEXT_BASE;
  const stale = await evaluateHeadSnapshot({
    client,
    store,
    envelope: envelope('reconciliation', {}),
    headSha: HEAD,
    publisherAppId: APP_ID,
  });
  assert.notEqual(stale.result.state, 'approved');
});

test('specialist automation downloads and validates an exact artifact before evidence', async () => {
  const current = pull();
  const bind = bindEvent(current);
  const requirement = requirementEvent(current, 'architecture');
  const policy = snapshotForEvents([bind, requirement], current);
  const review = copilotReview();
  const manifest = {
    schemaVersion: 1,
    kind: 'andreja-review-evidence',
    domain: 'architecture',
    outcome: 'approved',
    identity: pullIdentity(current),
    policyDigest: policy.digest,
    evidenceUrl: 'https://github.com/Jamula/Andreja/pull/115#issuecomment-7000',
    summary: 'Downloaded architecture evidence validates the current diff.',
  };
  const bytes = Buffer.from(JSON.stringify(manifest));
  const artifactSha = crypto.createHash('sha256').update(bytes).digest('hex');
  const attestation = {
    domain: manifest.domain,
    outcome: manifest.outcome,
    identity: manifest.identity,
    policyDigest: manifest.policyDigest,
    evidenceUrl: manifest.evidenceUrl,
    summary: manifest.summary,
    attester: {
      appId: 424242,
      slug: 'independent-specialist',
      runId: 12345,
      runAttempt: 2,
      workflowRevision: '1'.repeat(40),
    },
    artifact: {
      id: 99,
      name: 'review-evidence.json',
      sha256: artifactSha,
    },
  };
  const client = new FakeClient({
    pulls: [current],
    comments: {
      115: [
        trustedComment(bind, APP_ID, 1),
        trustedComment(requirement, APP_ID, 2),
        trustedComment(copilotAttestationEvent(current, review), APP_ID, 3),
      ],
    },
    reviews: { 115: [review] },
    specialistRuns: {
      12345: {
        appId: 424242,
        slug: 'independent-specialist',
        runId: 12345,
        runAttempt: 2,
        workflowRevision: '1'.repeat(40),
        repository: 'Jamula/Andreja',
        headSha: HEAD,
        status: 'completed',
        conclusion: 'success',
        artifactIds: [99],
      },
    },
    specialistArtifacts: {
      99: {
        id: 99,
        name: 'review-evidence.json',
        expired: false,
        bytes,
      },
    },
  });
  const store = new FakeStore({
    mappings: [mapping(current)],
    ...canonicalPolicyStateFromComments(client.state.comments),
  });
  const options = {
    client,
    store,
    envelope: envelope('specialist_attestation', {
      pull_number: 115,
      attestation,
    }),
    publisherAppId: APP_ID,
    specialistAllowlist: [{
      appId: 424242,
      slug: 'independent-specialist',
      workflowRevision: '1'.repeat(40),
    }],
    evaluatorOptions: { poll: false, stabilitySeconds: 0 },
  };
  const accepted = await handleEvent(options);
  assert.equal(client.callLog[0], 'checks.create');
  assert.ok(client.callLog.includes('specialist.artifact:99'));
  assert.equal(accepted[0].result.state, 'approved');
  assert.equal(latestCheck(client).conclusion, 'success');

  client.state.specialistArtifacts[99].bytes = Buffer.from('tampered');
  const rejected = await handleEvent({
    ...options,
    envelope: envelope('specialist_attestation', {
      pull_number: 115,
      attestation,
    }),
  });
  assert.equal(rejected[0].result.state, 'rejected');
  assert.equal(latestCheck(client).conclusion, 'failure');
  assert.match(latestCheck(client).output.summary, /failed closed validation/);
});

test('break-glass is exact-policy audited evidence, never a draft/thread bypass', () => {
  const current = pull();
  const bind = bindEvent(current);
  const policy = snapshotForEvents([bind], current);
  const emergency = policyEvent(current, 'break-glass', {
    identity: pullIdentity(current),
    policyDigest: policy.digest,
    reason: 'Reviewer outage accepted by a human with recorded residual risk.',
    auditUrl: 'https://github.com/Jamula/Andreja/issues/104',
  });
  assert.equal(evaluateReviewCompletion({
    pullRequest: current,
    policy,
    policyEvents: [bind, emergency],
  }).state, 'approved');
  assert.equal(evaluateReviewCompletion({
    pullRequest: { ...current, draft: true },
    policy,
    policyEvents: [bind, emergency],
  }).state, 'rejected');
  assert.equal(evaluateReviewCompletion({
    pullRequest: current,
    policy,
    policyEvents: [bind, emergency],
    unresolvedThreads: 1,
  }).state, 'rejected');
});

test('review-thread GraphQL pagination reads every page', async () => {
  const cursors = [];
  const client = {
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
    client,
    envelope: envelope('reconciliation', {}),
    pullNumber: 115,
  });
  assert.deepEqual(cursors, [null, 'next']);
  assert.deepEqual(threads.map((thread) => thread.isResolved), [true, false]);
});

test('stateful #100, #101, and #105 fixtures have no ready interval', async () => {
  for (const fixture of fixtures.filter((item) => item.incident)) {
    const current = pull();
    const bind = bindEvent(current);
    const client = new FakeClient({
      pulls: [current],
      comments: { 115: [trustedComment(bind, APP_ID, 1)] },
      reviews: { 115: [] },
    });
    const store = new FakeStore({
      mappings: [mapping(current)],
      ...canonicalPolicyStateFromComments(client.state.comments),
    });
    for (const event of fixture.events) {
      if (event.kind === 'copilot-review') {
        const review = copilotReview();
        client.state.reviews[115].push(review);
        const attestation = copilotAttestationEvent(current, review);
        const comment = trustedComment(attestation, APP_ID, 2);
        client.state.comments[115].push(comment);
        await appendCanonicalFixture(store, attestation, comment);
      }
      const state = (await evaluateHeadSnapshot({
        client,
        store,
        envelope: envelope('reconciliation', {}),
        headSha: HEAD,
        publisherAppId: APP_ID,
      })).result.state;
      assert.equal(state, event.expected, `${fixture.incident} at ${event.atSeconds}s`);
      if (event.atSeconds < fixture.firstReviewAtSeconds) {
        assert.notEqual(state, 'approved', fixture.incident);
      }
    }
    assert.ok(fixture.mergedAtSeconds < fixture.firstReviewAtSeconds);
  }
});

test('fixture catalog includes every external-worker regression class', () => {
  const scenarios = new Set(fixtures.map((item) => item.scenario).filter(Boolean));
  assert.deepEqual(scenarios, new Set([
    'base-advance-enumeration',
    'base-push-live-authority',
    'merge-group-constituents',
    'reviewer-revocation',
    'dropped-delivery-reconciliation',
    'dropped-reopen-reconciliation',
    'event-specific-canary',
    'canonical-policy-projection',
    'protected-push-identity',
    'reconciliation-identity-drift',
  ]));
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

test('ruleset rollout is plan-only with exact blocker and rollback snapshot', () => {
  const extra = { context: 'Future protected check', integration_id: 999 };
  const rollout = planRollout(liveRuleset([extra]), { appId: APP_ID });
  const checks = rollout.proposedPayload.rules.find((rule) =>
    rule.type === 'required_status_checks').parameters.required_status_checks;
  assert.equal(rollout.activation.state, 'BLOCKED');
  assert.equal(rollout.activation.code, ROLLOUT_BLOCKER_CODE);
  assert.ok(rollout.activation.prerequisites.some((item) =>
    item.includes('real merge-queue')));
  assert.equal(checks.length, 7);
  assert.ok(checks.some((check) => check.context === extra.context));
  assert.deepEqual(checks.at(-1), {
    context: CHECK_NAME,
    integration_id: APP_ID,
  });
  assert.equal(
    rollout.rollbackSnapshot.payload.rules.length,
    liveRuleset([extra]).rules.length);

  const rollback = planRollback({
    ...liveRuleset([extra]),
    rules: rollout.proposedPayload.rules,
  }, { appId: APP_ID });
  assert.equal(rollback.activation.state, 'BLOCKED');
  assert.equal(rollback.proposedRemoval.integration_id, APP_ID);
});

test('ruleset script has no apply or mutation path and exits nonzero on apply', () => {
  assert.throws(
    () => rulesetMain(['apply-rollout']),
    new RegExp(ROLLOUT_BLOCKER_CODE));
  const scriptPath = path.join(__dirname, 'review-gate-ruleset.js');
  const source = fs.readFileSync(scriptPath, 'utf8');
  assert.doesNotMatch(source, /--method['"]?\s*,?\s*['"]PUT/i);
  assert.doesNotMatch(source, /client\.update|rulesets\.update|rest\.rulesets/);
  const result = spawnSync(process.execPath, [scriptPath, 'apply-rollout'], {
    encoding: 'utf8',
  });
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, new RegExp(ROLLOUT_BLOCKER_CODE));
});

function canaryProvenance({
  eventPath,
  association,
  headSha = HEAD,
  targets = [exactMappingIdentity(mapping(pull()))],
  baseSha = BASE,
}) {
  return {
    schemaVersion: 5,
    contractRevision: CONTRACT_REVISION,
    eventPath,
    workerRevision: WORKER_REVISION,
    workerInstanceId: 'worker-a',
    deliveryId: `canary-${eventPath}`,
    runId: `run-${eventPath}`,
    association,
    targets,
    headSha,
    base: {
      repositoryId: REPOSITORY_ID,
      repository: 'Jamula/Andreja',
      ref: 'main',
      sha: baseSha,
    },
    sequence: 1,
    generationId: `generation-${eventPath}`,
  };
}

test('canary provenance distinguishes PR, base push, and real merge group', () => {
  const merge = canaryProvenance({
    eventPath: 'merge_group',
    headSha: MERGE_HEAD,
    association: {
      kind: 'merge_group',
      mergeGroupId: 'group',
      pullNumbers: [115],
    },
  });
  const run = {
    id: 700,
    name: CHECK_NAME,
    head_sha: MERGE_HEAD,
    app: { id: APP_ID },
    status: 'completed',
    conclusion: 'success',
    external_id: externalIdForProvenance(merge),
    output: { title: 'Approved — exact policy complete' },
  };
  const verified = verifyCanaryRun(run, {
    checkRunId: 700,
    headSha: MERGE_HEAD,
    appId: APP_ID,
    expectedProvenance: merge,
    expectedEventPath: 'merge_group',
    expectedAssociationKind: 'merge_group',
    titlePrefix: 'Approved',
  });
  assert.equal(verified.eventPath, 'merge_group');
  assert.equal(verified.workerRevision, WORKER_REVISION);

  const push = canaryProvenance({
    eventPath: 'push',
    association: { kind: 'default_branch', ref: 'main' },
    targets: [],
  });
  const pushRun = {
    ...run,
    head_sha: HEAD,
    external_id: externalIdForProvenance(push),
    output: { title: 'Not applicable — base path reconciled' },
  };
  assert.throws(() => verifyCanaryRun(pushRun, {
    checkRunId: 700,
    headSha: HEAD,
    appId: APP_ID,
    expectedProvenance: push,
    expectedEventPath: 'merge_group',
    expectedAssociationKind: 'merge_group',
    titlePrefix: 'Approved',
  }), /event-path|merge group/);
});

test('publisher credentials and unsupported Actions trigger are absent', () => {
  const workflowDirectory = path.join(__dirname, '..', 'workflows');
  assert.equal(
    fs.existsSync(path.join(workflowDirectory, 'review-gate-app.yml')),
    false);
  assert.equal(
    fs.existsSync(path.join(workflowDirectory, 'review-gate-app-admin.yml')),
    false);
  const workflowSources = fs.readdirSync(workflowDirectory)
    .filter((name) => /\.ya?ml$/i.test(name))
    .map((name) => fs.readFileSync(path.join(workflowDirectory, name), 'utf8'))
    .join('\n');
  assert.doesNotMatch(workflowSources, /REVIEW_GATE_APP_PRIVATE_KEY/);
  assert.doesNotMatch(workflowSources, /create-github-app-token/);
  assert.doesNotMatch(
    workflowSources,
    /^\s{2}pull_request_review_thread:\s*$/m);
  const workerSource = fs.readFileSync(
    path.join(__dirname, 'review-gate-app.js'),
    'utf8');
  assert.doesNotMatch(workerSource, /process\.env|actions\/checkout|child_process/);
});
