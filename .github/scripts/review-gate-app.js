'use strict';

const {
  CHECK_EXTERNAL_PREFIX,
  CHECK_NAME,
  COPILOT_REVIEWER,
  REVIEW_DOMAINS,
  domainAttestationEvidence,
  evaluateReviewCompletion,
  foldPolicyEvents,
  latestDomainReview,
  makePolicyEvent,
  parsePolicyEventComment,
  policyEventComment,
  pullIdentity,
  requiredDomains,
  reviewMarkers,
  samePullIdentity,
  securityDigest,
  summarizeResult,
  trustedPolicyEvents,
  validateEvidenceBinding,
} = require('./review-gate-policy');

const AUTHORIZED_REVIEW_PERMISSIONS = new Set(['write', 'maintain', 'admin']);
const DEFAULT_POLL_SECONDS = 30;
const DEFAULT_MAX_WAIT_SECONDS = 12 * 60;
const DEFAULT_STABILITY_SECONDS = 5;

function sanitizedApiFailure(error) {
  if (error?.status === 403 || error?.status === 429) {
    return 'GitHub metadata API was rate-limited or unavailable; the App check failed closed.';
  }
  return 'GitHub metadata evaluation failed; the App check failed closed.';
}

function eventDeliveryId(context, suffix = '') {
  const base = [
    context.runId || 'no-run',
    context.runAttempt || 1,
    context.eventName || 'unknown',
    suffix,
  ].join(':');
  return base.slice(0, 240);
}

function assertTrustedDispatchRef(context) {
  if (context.eventName !== 'workflow_dispatch') {
    return;
  }
  const expected = `refs/heads/${context.payload.repository.default_branch}`;
  if (context.ref !== expected) {
    throw new Error('Privileged workflow_dispatch must run from the default branch.');
  }
}

async function listReviewThreads({ github, context, pullNumber }) {
  const threads = [];
  const seenCursors = new Set();
  let cursor = null;
  do {
    const result = await github.graphql(
      `query($owner: String!, $repo: String!, $number: Int!, $cursor: String) {
        repository(owner: $owner, name: $repo) {
          pullRequest(number: $number) {
            reviewThreads(first: 100, after: $cursor) {
              nodes {
                id
                isResolved
                comments(last: 1) {
                  nodes { id updatedAt }
                }
              }
              pageInfo { hasNextPage endCursor }
            }
          }
        }
      }`,
      {
        owner: context.repo.owner,
        repo: context.repo.repo,
        number: pullNumber,
        cursor,
      });
    const connection = result.repository?.pullRequest?.reviewThreads;
    if (!connection) {
      throw new Error('Review-thread metadata was unavailable.');
    }
    threads.push(...connection.nodes);
    if (!connection.pageInfo.hasNextPage) {
      break;
    }
    cursor = connection.pageInfo.endCursor;
    if (!cursor || seenCursors.has(cursor)) {
      throw new Error('Review-thread pagination did not advance.');
    }
    seenCursors.add(cursor);
  } while (true);
  return threads;
}

async function listPolicyComments({ github, context, pullNumber }) {
  return github.paginate(github.rest.issues.listComments, {
    ...context.repo,
    issue_number: pullNumber,
    per_page: 100,
  });
}

async function loadPolicyLedger({
  github,
  context,
  pullRequest,
  expectedAppId,
}) {
  const comments = await listPolicyComments({
    github,
    context,
    pullNumber: pullRequest.number,
  });
  const repositoryId = Number(context.payload.repository.id);
  const repository = context.payload.repository.full_name;
  const trusted = trustedPolicyEvents(comments, {
    appId: expectedAppId,
    repositoryId,
    repository,
    pullNumber: pullRequest.number,
  });
  return {
    ...trusted,
    comments,
    snapshot: foldPolicyEvents(trusted.events, trusted.errors),
  };
}

async function appendPolicyEvent({
  github,
  context,
  event,
  expectedAppId,
}) {
  const response = await github.rest.issues.createComment({
    ...context.repo,
    issue_number: event.pullNumber,
    body: policyEventComment(event),
  });
  if (Number(response.data?.performed_via_github_app?.id) !==
      Number(expectedAppId)) {
    throw new Error('Policy event publisher does not match the configured GitHub App.');
  }
  return response.data;
}

async function recordCopilotAttestationFromEvent({
  github,
  context,
  expectedAppId,
}) {
  if (context.eventName !== 'pull_request_review' ||
      context.payload.action !== 'submitted') {
    return null;
  }
  const review = context.payload.review;
  const pullRequest = context.payload.pull_request;
  if (Number(review?.user?.id) !== COPILOT_REVIEWER.id ||
      review?.user?.login !== COPILOT_REVIEWER.login ||
      review?.user?.type !== COPILOT_REVIEWER.type) {
    return null;
  }
  const identity = pullIdentity(pullRequest);
  if (review.commit_id !== identity.headSha) {
    throw new Error('The Copilot review event is not attached to the current head.');
  }
  const event = makePolicyEvent({
    kind: 'copilot-attestation',
    repositoryId: Number(context.payload.repository.id),
    repository: context.payload.repository.full_name,
    pullNumber: pullRequest.number,
    deliveryId: eventDeliveryId(context, `copilot-review:${review.id}`),
    actor: COPILOT_REVIEWER.login,
    data: {
      identity,
      reviewId: Number(review.id),
      reviewerId: COPILOT_REVIEWER.id,
      reviewerLogin: COPILOT_REVIEWER.login,
      reviewSubmittedAt: review.submitted_at,
    },
  });
  await appendPolicyEvent({ github, context, event, expectedAppId });
  return event;
}

async function restoreDeletedPolicyEventFromEvent({
  github,
  context,
  expectedAppId,
}) {
  if (context.eventName !== 'issue_comment' ||
      context.payload.action !== 'deleted' ||
      !context.payload.issue?.pull_request ||
      Number(context.payload.comment?.performed_via_github_app?.id) !==
        Number(expectedAppId)) {
    return null;
  }
  const parsed = parsePolicyEventComment(context.payload.comment.body);
  if (parsed.error ||
      Number(parsed.event.pullNumber) !== Number(context.payload.issue.number)) {
    throw new Error('A deleted App policy event could not be restored safely.');
  }
  await appendPolicyEvent({
    github,
    context,
    event: parsed.event,
    expectedAppId,
  });
  return parsed.event;
}

async function labelsForIssue({ github, context, issueNumber }) {
  return github.paginate(github.rest.issues.listLabelsOnIssue, {
    ...context.repo,
    issue_number: issueNumber,
    per_page: 100,
  });
}

function observationEvent({
  context,
  pullRequest,
  kind,
  sourceKey,
  data,
}) {
  return makePolicyEvent({
    kind,
    repositoryId: Number(context.payload.repository.id),
    repository: context.payload.repository.full_name,
    pullNumber: pullRequest.number,
    deliveryId: eventDeliveryId(context, sourceKey),
    actor: context.actor || 'github',
    data: { sourceKey, ...data },
  });
}

async function observeCurrentRequirements({
  github,
  context,
  pullRequest,
  expectedAppId,
}) {
  let ledger = await loadPolicyLedger({
    github,
    context,
    pullRequest,
    expectedAppId,
  });
  if (ledger.snapshot.errors.length > 0) {
    return ledger;
  }
  const desired = [];
  const pullLabels = await labelsForIssue({
    github,
    context,
    issueNumber: pullRequest.number,
  });
  for (const domain of requiredDomains([pullLabels])) {
    const sourceKey = `pull:${pullRequest.number}:domain:${domain}`;
    if (!ledger.snapshot.activeSources[sourceKey]) {
      desired.push(observationEvent({
        context,
        pullRequest,
        kind: 'require-domain',
        sourceKey,
        data: {
          domain,
          sourceKind: 'pull-label',
          sourceNumber: pullRequest.number,
        },
      }));
    }
  }
  for (const issueNumber of ledger.snapshot.associations) {
    const labels = await labelsForIssue({ github, context, issueNumber });
    for (const domain of requiredDomains([labels])) {
      const sourceKey = `issue:${issueNumber}:domain:${domain}`;
      if (!ledger.snapshot.activeSources[sourceKey]) {
        desired.push(observationEvent({
          context,
          pullRequest,
          kind: 'require-domain',
          sourceKey,
          data: {
            domain,
            sourceKind: 'issue-label',
            sourceNumber: issueNumber,
          },
        }));
      }
    }
  }
  for (const event of desired) {
    await appendPolicyEvent({ github, context, event, expectedAppId });
  }
  if (desired.length > 0) {
    ledger = await loadPolicyLedger({
      github,
      context,
      pullRequest,
      expectedAppId,
    });
  }
  return ledger;
}

async function reviewerPermission({ github, context, login, cache }) {
  if (!cache.has(login)) {
    const response = await github.rest.repos.getCollaboratorPermissionLevel({
      ...context.repo,
      username: login,
    });
    cache.set(login, response.data.permission);
  }
  return cache.get(login);
}

async function domainReviewEvidence({
  github,
  context,
  reviews,
  domain,
  identity,
  policyDigest,
  author,
  permissionCache,
}) {
  const review = latestDomainReview(reviews, domain);
  if (!review) {
    return { outcome: 'missing', candidateId: null };
  }
  const markers = reviewMarkers(review.body)
    .filter((marker) => marker.domain === domain);
  if (markers.length !== 1 || markers[0].error) {
    return {
      outcome: 'failure',
      candidateId: review.id,
      reason: markers[0]?.error ||
        `The newest ${domain} review contains ambiguous evidence markers.`,
    };
  }
  const reviewer = String(review.user?.login || '');
  if (!reviewer || review.user?.type !== 'User') {
    return {
      outcome: 'failure',
      candidateId: review.id,
      reason: `The newest ${domain} review is not from an authenticated human.`,
    };
  }
  if (reviewer.toLowerCase() === String(author || '').toLowerCase()) {
    return {
      outcome: 'failure',
      candidateId: review.id,
      reason: `The PR author cannot provide independent ${domain} evidence.`,
    };
  }
  const permission = await reviewerPermission({
    github,
    context,
    login: reviewer,
    cache: permissionCache,
  });
  if (!AUTHORIZED_REVIEW_PERMISSIONS.has(permission)) {
    return {
      outcome: 'failure',
      candidateId: review.id,
      reason: `The newest ${domain} reviewer is not authorized for this repository.`,
    };
  }
  const bindingError = validateEvidenceBinding(markers[0].binding, {
    domain,
    identity,
    policyDigest,
    repository: identity.baseRepository,
  });
  if (bindingError) {
    return {
      outcome: 'failure',
      candidateId: review.id,
      reason: bindingError,
    };
  }
  if (review.commit_id !== identity.headSha) {
    return {
      outcome: 'failure',
      candidateId: review.id,
      reason: `The newest ${domain} review is not attached to the current head.`,
    };
  }
  const state = String(review.state || '').toUpperCase();
  if (state === 'APPROVED') {
    return {
      outcome: 'success',
      candidateId: review.id,
      reviewer,
      binding: markers[0].binding,
    };
  }
  if (state === 'CHANGES_REQUESTED' || state === 'DISMISSED') {
    return {
      outcome: 'failure',
      candidateId: review.id,
      reviewer,
      reason: `The newest independent ${domain} review is ${state.toLowerCase()}.`,
    };
  }
  return {
    outcome: 'pending',
    candidateId: review.id,
    reviewer,
    reason: `The newest independent ${domain} review has not approved the current policy.`,
  };
}

function newestDomainCandidate(human, automated) {
  const candidates = [human, automated]
    .filter((candidate) => candidate && candidate.outcome !== 'missing')
    .sort((left, right) =>
      Number(right.observedAt || 0) - Number(left.observedAt || 0) ||
      (right.outcome === 'failure' ? 1 : 0) -
        (left.outcome === 'failure' ? 1 : 0) ||
      String(right.candidateId || '').localeCompare(
        String(left.candidateId || '')));
  return candidates[0] || { outcome: 'missing', candidateId: null, observedAt: 0 };
}

function reviewSecurityState(reviews) {
  return reviews.map((review) => ({
    id: Number(review.id),
    login: review.user?.login || '',
    userId: Number(review.user?.id || 0),
    type: review.user?.type || '',
    state: review.state || '',
    commitId: review.commit_id || '',
    submittedAt: review.submitted_at || '',
    bodyDigest: securityDigest(String(review.body || '')),
  })).sort((left, right) => left.id - right.id);
}

function threadSecurityState(threads) {
  return threads.map((thread) => ({
    id: thread.id,
    isResolved: Boolean(thread.isResolved),
    comments: thread.comments?.nodes || [],
  })).sort((left, right) => String(left.id).localeCompare(String(right.id)));
}

async function evaluatePullSnapshot({
  github,
  context,
  pullNumber,
  expectedHeadSha,
  expectedAppId,
}) {
  const pullResponse = await github.rest.pulls.get({
    ...context.repo,
    pull_number: pullNumber,
  });
  const pullRequest = pullResponse.data;
  const identity = pullIdentity(pullRequest);
  if (identity.headSha !== expectedHeadSha) {
    return {
      stale: true,
      result: {
        state: 'rejected',
        reasons: ['The PR head changed during this App generation.'],
      },
      fingerprint: null,
    };
  }
  const ledger = await observeCurrentRequirements({
    github,
    context,
    pullRequest,
    expectedAppId,
  });
  const [reviews, threads] = await Promise.all([
    github.paginate(github.rest.pulls.listReviews, {
      ...context.repo,
      pull_number: pullNumber,
      per_page: 100,
    }),
    listReviewThreads({ github, context, pullNumber }),
  ]);
  const permissionCache = new Map();
  const entries = await Promise.all(ledger.snapshot.domains.map(async (domain) => {
    const human = await domainReviewEvidence({
      github,
      context,
      reviews,
      domain,
      identity,
      policyDigest: ledger.snapshot.digest,
      author: pullRequest.user?.login,
      permissionCache,
    });
    const humanReview = latestDomainReview(reviews, domain);
    human.observedAt = Date.parse(
      humanReview?.submitted_at || humanReview?.updated_at || 0) || 0;
    const automated = domainAttestationEvidence(
      ledger.events,
      domain,
      identity,
      ledger.snapshot.digest,
      identity.baseRepository);
    return [domain, newestDomainCandidate(human, automated)];
  }));
  const domainEvidence = Object.fromEntries(entries);
  const unresolvedThreads = threads.filter((thread) => !thread.isResolved).length;
  const result = evaluateReviewCompletion({
    pullRequest,
    policy: ledger.snapshot,
    reviews,
    unresolvedThreads,
    domainEvidence,
    policyEvents: ledger.events,
  });
  const securityState = {
    identity,
    state: pullRequest.state,
    draft: Boolean(pullRequest.draft),
    author: pullRequest.user?.login || '',
    policy: ledger.snapshot,
    reviews: reviewSecurityState(reviews),
    threads: threadSecurityState(threads),
    domainEvidence,
    result,
  };
  return {
    stale: false,
    pullNumber,
    identity,
    policy: ledger.snapshot,
    result,
    fingerprint: securityDigest(securityState),
    securityState,
  };
}

async function openPullsSharingHead({ github, context, headSha }) {
  const pulls = await github.paginate(github.rest.pulls.list, {
    ...context.repo,
    state: 'open',
    per_page: 100,
  });
  return pulls
    .filter((pullRequest) => pullRequest.head?.sha === headSha)
    .sort((left, right) => left.number - right.number);
}

function aggregateSnapshots(snapshots, headSha) {
  const rejected = snapshots.filter((snapshot) =>
    snapshot.stale || snapshot.result.state === 'rejected');
  const pending = snapshots.filter((snapshot) =>
    !snapshot.stale && snapshot.result.state === 'pending');
  let result;
  if (snapshots.length === 0) {
    result = {
      state: 'rejected',
      reasons: [`No open PR is bound to head ${headSha}.`],
    };
  } else if (rejected.length > 0) {
    result = {
      state: 'rejected',
      reasons: rejected.flatMap((snapshot) =>
        snapshot.result.reasons.map((reason) =>
          `PR #${snapshot.pullNumber || 'unknown'}: ${reason}`)),
    };
  } else if (pending.length > 0) {
    result = {
      state: 'pending',
      reasons: pending.flatMap((snapshot) =>
        snapshot.result.reasons.map((reason) =>
          `PR #${snapshot.pullNumber}: ${reason}`)),
    };
  } else {
    result = {
      state: 'approved',
      reasons: [
        `Every open PR sharing head ${headSha} passed its exact PR/base/policy review.`,
        `Evaluated PRs: ${snapshots.map((snapshot) =>
          `#${snapshot.pullNumber}`).join(', ')}.`,
      ],
    };
  }
  return {
    result,
    snapshots,
    fingerprint: securityDigest({
      headSha,
      pulls: snapshots.map((snapshot) => ({
        pullNumber: snapshot.pullNumber,
        fingerprint: snapshot.fingerprint,
        result: snapshot.result,
      })),
    }),
  };
}

async function evaluateHeadSnapshot({
  github,
  context,
  headSha,
  expectedAppId,
}) {
  const pulls = await openPullsSharingHead({ github, context, headSha });
  const snapshots = [];
  for (const pullRequest of pulls) {
    snapshots.push(await evaluatePullSnapshot({
      github,
      context,
      pullNumber: pullRequest.number,
      expectedHeadSha: headSha,
      expectedAppId,
    }));
  }
  return aggregateSnapshots(snapshots, headSha);
}

async function sleep(milliseconds) {
  await new Promise((resolve) => setTimeout(resolve, milliseconds));
}

async function evaluateHeadUntilStable({
  github,
  context,
  headSha,
  expectedAppId,
  poll = true,
  pollSeconds = DEFAULT_POLL_SECONDS,
  maxWaitSeconds = DEFAULT_MAX_WAIT_SECONDS,
  stabilitySeconds = DEFAULT_STABILITY_SECONDS,
  sleepFunction = sleep,
  now = () => Date.now(),
}) {
  const deadline = now() + maxWaitSeconds * 1000;
  let snapshot;
  while (true) {
    snapshot = await evaluateHeadSnapshot({
      github,
      context,
      headSha,
      expectedAppId,
    });
    if (snapshot.result.state === 'rejected') {
      return snapshot;
    }
    if (snapshot.result.state === 'approved') {
      await sleepFunction(stabilitySeconds * 1000);
      const confirmation = await evaluateHeadSnapshot({
        github,
        context,
        headSha,
        expectedAppId,
      });
      if (confirmation.result.state === 'rejected') {
        return confirmation;
      }
      if (confirmation.result.state === 'approved' &&
          confirmation.fingerprint === snapshot.fingerprint) {
        return confirmation;
      }
      snapshot = confirmation;
    }
    if (!poll) {
      return snapshot;
    }
    if (now() >= deadline) {
      return {
        ...snapshot,
        result: {
          state: 'rejected',
          reasons: [
            ...snapshot.result.reasons,
            `Review automation did not complete within ${maxWaitSeconds} seconds.`,
          ],
        },
      };
    }
    await sleepFunction(pollSeconds * 1000);
  }
}

async function createGeneration({
  github,
  context,
  headSha,
  expectedAppId,
  trigger,
}) {
  const externalId = [
    CHECK_EXTERNAL_PREFIX,
    `sha=${headSha}`,
    `delivery=${eventDeliveryId(context, trigger)}`,
  ].join(':');
  const response = await github.rest.checks.create({
    ...context.repo,
    name: CHECK_NAME,
    head_sha: headSha,
    status: 'in_progress',
    started_at: new Date().toISOString(),
    external_id: externalId,
    output: {
      title: 'Pending — trusted metadata evaluation started',
      summary: [
        'The dedicated review-gate App started a new generation before policy evaluation.',
        `Trigger: ${trigger}.`,
      ].join('\n'),
    },
  });
  const checkRun = response.data;
  if (Number(checkRun.app?.id) !== Number(expectedAppId)) {
    throw new Error('The check publisher does not match REVIEW_GATE_APP_ID.');
  }
  return checkRun;
}

async function listAppGenerations({
  github,
  context,
  headSha,
  expectedAppId,
}) {
  const runs = await github.paginate(github.rest.checks.listForRef, {
    ...context.repo,
    ref: headSha,
    check_name: CHECK_NAME,
    per_page: 100,
  });
  return runs.filter((run) =>
    run.name === CHECK_NAME &&
    Number(run.app?.id) === Number(expectedAppId) &&
    String(run.external_id || '').startsWith(`${CHECK_EXTERNAL_PREFIX}:`))
    .sort((left, right) => Number(left.id) - Number(right.id));
}

async function completeGeneration({
  github,
  context,
  checkRun,
  expectedAppId,
  result,
}) {
  const before = await listAppGenerations({
    github,
    context,
    headSha: checkRun.head_sha,
    expectedAppId,
  });
  if (Number(before.at(-1)?.id) !== Number(checkRun.id)) {
    return false;
  }
  const conclusion = result.state === 'approved' ||
    result.state === 'not_applicable'
    ? 'success'
    : 'failure';
  await github.rest.checks.update({
    ...context.repo,
    check_run_id: checkRun.id,
    status: 'completed',
    conclusion,
    completed_at: new Date().toISOString(),
    output: {
      title: result.state === 'approved'
        ? 'Approved — exact policy complete'
        : result.state === 'not_applicable'
          ? 'Not applicable — PR heads enforce review'
          : 'Rejected — review policy incomplete',
      summary: summarizeResult(result),
    },
  });
  const after = await listAppGenerations({
    github,
    context,
    headSha: checkRun.head_sha,
    expectedAppId,
  });
  return Number(after.at(-1)?.id) === Number(checkRun.id);
}

async function runHead({
  github,
  context,
  core,
  headSha,
  expectedAppId,
  trigger,
  evaluatorOptions = {},
}) {
  const generation = await createGeneration({
    github,
    context,
    headSha,
    expectedAppId,
    trigger,
  });
  let aggregate;
  try {
    await restoreDeletedPolicyEventFromEvent({
      github,
      context,
      expectedAppId,
    });
    await recordCopilotAttestationFromEvent({
      github,
      context,
      expectedAppId,
    });
    aggregate = await evaluateHeadUntilStable({
      github,
      context,
      headSha,
      expectedAppId,
      ...evaluatorOptions,
    });
  } catch (error) {
    aggregate = {
      result: {
        state: 'rejected',
        reasons: [sanitizedApiFailure(error)],
      },
    };
  }
  const completed = await completeGeneration({
    github,
    context,
    checkRun: generation,
    expectedAppId,
    result: aggregate.result,
  });
  if (!completed) {
    core.info(
      `Generation ${generation.id} was superseded; it did not publish a terminal result.`);
    return { ...aggregate, superseded: true, generation };
  }
  if (aggregate.result.state !== 'approved') {
    core.setFailed(aggregate.result.reasons.join(' '));
  }
  return { ...aggregate, superseded: false, generation };
}

async function runNotApplicable({
  github,
  context,
  core,
  headSha,
  expectedAppId,
  path,
}) {
  const generation = await createGeneration({
    github,
    context,
    headSha,
    expectedAppId,
    trigger: path,
  });
  const result = evaluateReviewCompletion({
    path,
    pullRequest: {
      number: 1,
      state: 'open',
      draft: false,
      head: { sha: headSha },
      base: {
        sha: headSha,
        ref: context.payload.repository.default_branch,
        repo: {
          id: context.payload.repository.id,
          full_name: context.payload.repository.full_name,
        },
      },
    },
    policy: { errors: [] },
  });
  await completeGeneration({
    github,
    context,
    checkRun: generation,
    expectedAppId,
    result,
  });
  core.info(summarizeResult(result));
  return { result, generation };
}

async function associatedPullNumbersForIssue({
  github,
  context,
  issueNumber,
  expectedAppId,
}) {
  const pulls = await github.paginate(github.rest.pulls.list, {
    ...context.repo,
    state: 'open',
    per_page: 100,
  });
  const numbers = [];
  for (const pullRequest of pulls) {
    const ledger = await loadPolicyLedger({
      github,
      context,
      pullRequest,
      expectedAppId,
    });
    if (ledger.snapshot.associations.includes(Number(issueNumber))) {
      numbers.push(pullRequest.number);
    }
  }
  return numbers;
}

async function run({
  github,
  context,
  core,
  expectedAppId = Number(process.env.REVIEW_GATE_APP_ID),
  evaluatorOptions = {},
}) {
  if (!Number.isInteger(Number(expectedAppId)) || Number(expectedAppId) <= 0) {
    throw new Error('REVIEW_GATE_APP_ID must be the exact numeric GitHub App ID.');
  }
  assertTrustedDispatchRef(context);
  if (context.eventName === 'merge_group') {
    return runNotApplicable({
      github,
      context,
      core,
      headSha: context.payload.merge_group.head_sha,
      expectedAppId,
      path: 'merge_group',
    });
  }
  if (context.eventName === 'push') {
    return runNotApplicable({
      github,
      context,
      core,
      headSha: context.payload.after || context.sha,
      expectedAppId,
      path: 'default_branch',
    });
  }

  let pullNumbers = [];
  if (context.eventName === 'issues') {
    pullNumbers = await associatedPullNumbersForIssue({
      github,
      context,
      issueNumber: context.payload.issue.number,
      expectedAppId,
    });
  } else {
    const number = Number(
      context.payload.pull_request?.number ||
      (context.payload.issue?.pull_request
        ? context.payload.issue.number
        : 0) ||
      context.payload.inputs?.pr_number ||
      0);
    if (number > 0) {
      pullNumbers = [number];
    }
  }
  if (pullNumbers.length === 0) {
    throw new Error('No authenticated PR policy association matched this event.');
  }

  const heads = new Set();
  for (const pullNumber of pullNumbers) {
    const payloadPull = context.payload.pull_request;
    if (payloadPull?.number === pullNumber && payloadPull.head?.sha) {
      heads.add(payloadPull.head.sha);
      continue;
    }
    const response = await github.rest.pulls.get({
      ...context.repo,
      pull_number: pullNumber,
    });
    heads.add(pullIdentity(response.data).headSha);
  }
  const results = [];
  for (const headSha of [...heads].sort()) {
    results.push(await runHead({
      github,
      context,
      core,
      headSha,
      expectedAppId,
      trigger: `${context.eventName}:${pullNumbers.join(',')}`,
      evaluatorOptions,
    }));
  }
  return results;
}

module.exports = {
  AUTHORIZED_REVIEW_PERMISSIONS,
  DEFAULT_MAX_WAIT_SECONDS,
  DEFAULT_POLL_SECONDS,
  DEFAULT_STABILITY_SECONDS,
  aggregateSnapshots,
  assertTrustedDispatchRef,
  appendPolicyEvent,
  associatedPullNumbersForIssue,
  completeGeneration,
  createGeneration,
  domainReviewEvidence,
  evaluateHeadSnapshot,
  evaluateHeadUntilStable,
  evaluatePullSnapshot,
  eventDeliveryId,
  listAppGenerations,
  listPolicyComments,
  listReviewThreads,
  loadPolicyLedger,
  newestDomainCandidate,
  observeCurrentRequirements,
  openPullsSharingHead,
  recordCopilotAttestationFromEvent,
  restoreDeletedPolicyEventFromEvent,
  run,
  runHead,
  runNotApplicable,
  sanitizedApiFailure,
};
