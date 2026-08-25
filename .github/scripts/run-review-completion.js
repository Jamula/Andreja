'use strict';

const {
  BREAK_GLASS_PREFIX,
  CHECK_NAMES,
  COPILOT_LOGIN,
  EVIDENCE_PREFIX,
  REVIEW_DOMAINS,
  TRUSTED_ACTIONS_APP,
  breakGlassArtifactName,
  currentCopilotReview,
  evaluateReviewCompletion,
  evidenceArtifactName,
  evidenceCheckName,
  latestCheck,
  requiredDomains,
  summarizeResult,
} = require('./review-completion');
const { closingIssueNumbers } = require('./run-issue-status');

const REVIEW_WORKFLOW = '.github/workflows/record-review-evidence.yml';
const BREAK_GLASS_WORKFLOW = '.github/workflows/record-review-break-glass.yml';
const GATE_PREFIX = 'review-completion-gate:v1:';
const DEFAULT_POLL_SECONDS = 30;
const DEFAULT_MAX_WAIT_SECONDS = 12 * 60;

function runUrl(context) {
  const serverUrl = context.serverUrl ||
    process.env.GITHUB_SERVER_URL ||
    'https://github.com';
  return `${serverUrl}/${context.repo.owner}/${context.repo.repo}` +
    `/actions/runs/${context.runId}`;
}

function sanitizedApiFailure(error) {
  if (error?.status === 403 || error?.status === 429) {
    return 'GitHub metadata API was rate-limited or unavailable; the gate failed closed.';
  }
  return 'GitHub metadata evaluation failed; the gate failed closed.';
}

async function listCheckRuns({ github, context, ref }) {
  return github.paginate(github.rest.checks.listForRef, {
    ...context.repo,
    ref,
    per_page: 100,
    filter: 'all',
  });
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
              nodes { isResolved }
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
    const connection = result.repository.pullRequest.reviewThreads;
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

async function listLabels({ github, context, issueNumber }) {
  return github.paginate(github.rest.issues.listLabelsOnIssue, {
    ...context.repo,
    issue_number: issueNumber,
    per_page: 100,
  });
}

async function loadRequiredDomains({ github, context, pullRequest }) {
  const linkedIssues = await closingIssueNumbers({
    github,
    context,
    pullRequest,
  });
  const labelSets = [
    await listLabels({
      github,
      context,
      issueNumber: pullRequest.number,
    }),
  ];
  for (const issueNumber of linkedIssues) {
    labelSets.push(await listLabels({ github, context, issueNumber }));
  }
  return requiredDomains(labelSets);
}

function workflowRunId(check, prefix) {
  if (check.app?.slug !== TRUSTED_ACTIONS_APP ||
      !String(check.external_id || '').startsWith(prefix)) {
    return null;
  }
  const id = String(check.external_id).split(':').at(-1);
  if (!/^\d+$/.test(id)) {
    return null;
  }
  let detailsUrl;
  try {
    detailsUrl = new URL(String(check.details_url || ''));
  } catch {
    return null;
  }
  const expectedPath = new RegExp(`/actions/runs/${id}(?:$|/)`);
  return detailsUrl.protocol === 'https:' &&
    detailsUrl.hostname === 'github.com' &&
    expectedPath.test(detailsUrl.pathname)
    ? Number(id)
    : null;
}

async function isTrustedWorkflowCheck({
  github,
  context,
  check,
  prefix,
  workflowPath,
  artifactName,
  cache,
}) {
  const workflowRun = workflowRunId(check, prefix);
  if (workflowRun === null) {
    return false;
  }
  let cached = cache.get(workflowRun);
  if (!cached || cached.run.status !== 'completed') {
    const [response, artifacts] = await Promise.all([
      github.rest.actions.getWorkflowRun({
        ...context.repo,
        run_id: workflowRun,
      }),
      github.paginate(github.rest.actions.listWorkflowRunArtifacts, {
        ...context.repo,
        run_id: workflowRun,
        per_page: 100,
      }),
    ]);
    cached = { run: response.data, artifacts };
    if (response.data.status === 'completed') {
      cache.set(workflowRun, cached);
    }
  }
  const { run, artifacts } = cached;
  const path = String(run.path || '').split('@')[0];
  return run.event === 'workflow_dispatch' &&
    run.status === 'completed' &&
    run.conclusion === 'success' &&
    run.head_branch === context.payload.repository.default_branch &&
    path === workflowPath &&
    run.repository?.full_name === context.payload.repository.full_name &&
    artifacts.some((artifact) =>
      artifact.name === artifactName && artifact.expired === false);
}

async function latestTrustedCheck({
  github,
  context,
  checks,
  name,
  prefix,
  workflowPath,
  artifactName,
  cache,
}) {
  const candidates = checks
    .filter((check) => check.name === name)
    .sort((left, right) =>
      Number(right.id || 0) - Number(left.id || 0));
  for (const check of candidates) {
    if (await isTrustedWorkflowCheck({
      github,
      context,
      check,
      prefix,
      workflowPath,
      artifactName: artifactName(check),
      cache,
    })) {
      return check;
    }
  }
  return null;
}

async function findGateCheck({ github, context, headSha, externalId }) {
  const checks = await listCheckRuns({ github, context, ref: headSha });
  return latestCheck(checks, (check) =>
    check.name === CHECK_NAMES.gate &&
    check.app?.slug === TRUSTED_ACTIONS_APP &&
    check.external_id === externalId);
}

async function ensureGateCheck({
  github,
  context,
  headSha,
  identity,
  title = 'Review metadata evaluation started',
}) {
  const externalId = `${GATE_PREFIX}${identity}:${headSha}`;
  let check = await findGateCheck({
    github,
    context,
    headSha,
    externalId,
  });
  const output = {
    title,
    summary: 'The trusted default-branch gate is evaluating review metadata.',
  };
  if (!check || check.status === 'completed') {
    const response = await github.rest.checks.create({
      ...context.repo,
      name: CHECK_NAMES.gate,
      head_sha: headSha,
      status: 'in_progress',
      started_at: new Date().toISOString(),
      external_id: externalId,
      details_url: runUrl(context),
      output,
    });
    check = response.data;
  } else {
    await github.rest.checks.update({
      ...context.repo,
      check_run_id: check.id,
      status: 'in_progress',
      details_url: runUrl(context),
      output,
    });
  }
  return check;
}

async function updateGateCheck({
  github,
  context,
  check,
  result,
  complete = false,
}) {
  const request = {
    ...context.repo,
    check_run_id: check.id,
    details_url: runUrl(context),
    output: {
      title: {
        approved: 'Review policy complete',
        pending: 'Review policy pending',
        rejected: 'Review policy rejected',
        not_applicable: 'Review policy not applicable',
      }[result.state],
      summary: summarizeResult(result),
    },
  };
  if (complete) {
    request.status = 'completed';
    request.conclusion =
      result.state === 'approved' || result.state === 'not_applicable'
        ? 'success'
        : 'failure';
    request.completed_at = new Date().toISOString();
  } else {
    request.status = 'in_progress';
  }
  await github.rest.checks.update(request);
}

async function evaluateSnapshot({
  github,
  context,
  pullNumber,
  expectedHeadSha,
  workflowCache,
}) {
  const response = await github.rest.pulls.get({
    ...context.repo,
    pull_number: pullNumber,
  });
  const pullRequest = response.data;
  const headSha = pullRequest.head.sha;
  if (expectedHeadSha && headSha !== expectedHeadSha) {
    return {
      stale: true,
      headSha,
      result: {
        state: 'rejected',
        reasons: ['The pull-request head changed during evaluation.'],
      },
    };
  }

  const [reviews, checks, threads, domains] = await Promise.all([
    github.paginate(github.rest.pulls.listReviews, {
      ...context.repo,
      pull_number: pullNumber,
      per_page: 100,
    }),
    listCheckRuns({ github, context, ref: headSha }),
    listReviewThreads({ github, context, pullNumber }),
    loadRequiredDomains({ github, context, pullRequest }),
  ]);

  const copilotCheck = latestCheck(checks, (check) =>
    check.name === CHECK_NAMES.copilot &&
    check.app?.slug === TRUSTED_ACTIONS_APP);
  const evidence = {};
  for (const domain of domains) {
    evidence[domain] = await latestTrustedCheck({
      github,
      context,
      checks,
      name: evidenceCheckName(domain),
      prefix: `${EVIDENCE_PREFIX}${domain}:${headSha}:`,
      workflowPath: REVIEW_WORKFLOW,
      artifactName: (check) => evidenceArtifactName(
        domain,
        headSha,
        check.conclusion === 'success' ? 'approved' : 'rejected'),
      cache: workflowCache,
    });
  }
  const breakGlass = await latestTrustedCheck({
    github,
    context,
    checks,
    name: CHECK_NAMES.breakGlass,
    prefix: `${BREAK_GLASS_PREFIX}${headSha}:`,
    workflowPath: BREAK_GLASS_WORKFLOW,
    artifactName: () => breakGlassArtifactName(headSha),
    cache: workflowCache,
  });

  return {
    stale: false,
    headSha,
    result: evaluateReviewCompletion({
      draft: pullRequest.draft,
      headSha,
      copilotCheck,
      copilotReview: currentCopilotReview(reviews, headSha),
      unresolvedThreads: threads.filter((thread) => !thread.isResolved).length,
      domains,
      evidence,
      breakGlass,
    }),
  };
}

function shouldPoll(eventName, targetCount) {
  return targetCount === 1 && new Set([
    'pull_request_target',
    'pull_request_review',
    'pull_request_review_comment',
    'issue_comment',
    'workflow_dispatch',
  ]).has(eventName);
}

async function sleep(milliseconds) {
  await new Promise((resolve) => setTimeout(resolve, milliseconds));
}

async function evaluatePullRequest({
  github,
  context,
  core,
  pullNumber,
  poll,
  pollSeconds = DEFAULT_POLL_SECONDS,
  maxWaitSeconds = DEFAULT_MAX_WAIT_SECONDS,
  sleepFunction = sleep,
}) {
  const initial = await github.rest.pulls.get({
    ...context.repo,
    pull_number: pullNumber,
  });
  const expectedHeadSha = initial.data.head.sha;
  let gateCheck = await ensureGateCheck({
    github,
    context,
    headSha: expectedHeadSha,
    identity: `pr-${pullNumber}`,
  });
  const deadline = Date.now() + maxWaitSeconds * 1000;
  const workflowCache = new Map();

  try {
    while (true) {
      const snapshot = await evaluateSnapshot({
        github,
        context,
        pullNumber,
        expectedHeadSha,
        workflowCache,
      });
      if (snapshot.stale) {
        await updateGateCheck({
          github,
          context,
          check: gateCheck,
          result: snapshot.result,
          complete: true,
        });
        return snapshot.result;
      }

      const result = snapshot.result;
      if (result.state !== 'pending') {
        const finalPull = await github.rest.pulls.get({
          ...context.repo,
          pull_number: pullNumber,
        });
        if (finalPull.data.head.sha !== expectedHeadSha ||
            finalPull.data.draft !== initial.data.draft) {
          const stale = {
            state: 'rejected',
            reasons: ['The pull-request head or draft state changed before completion.'],
          };
          await updateGateCheck({
            github,
            context,
            check: gateCheck,
            result: stale,
            complete: true,
          });
          return stale;
        }
        await updateGateCheck({
          github,
          context,
          check: gateCheck,
          result,
          complete: true,
        });
        return result;
      }

      if (!poll) {
        await updateGateCheck({
          github,
          context,
          check: gateCheck,
          result,
          complete: false,
        });
        return result;
      }
      if (Date.now() >= deadline) {
        const timeout = {
          state: 'rejected',
          reasons: [
            ...result.reasons,
            `Review automation did not complete within ${maxWaitSeconds} seconds.`,
          ],
        };
        await updateGateCheck({
          github,
          context,
          check: gateCheck,
          result: timeout,
          complete: true,
        });
        return timeout;
      }
      await updateGateCheck({
        github,
        context,
        check: gateCheck,
        result,
        complete: false,
      });
      await sleepFunction(pollSeconds * 1000);
    }
  } catch (error) {
    const failure = {
      state: 'rejected',
      reasons: [sanitizedApiFailure(error)],
    };
    try {
      gateCheck = gateCheck || await ensureGateCheck({
        github,
        context,
        headSha: expectedHeadSha,
        identity: `pr-${pullNumber}`,
      });
      await updateGateCheck({
        github,
        context,
        check: gateCheck,
        result: failure,
        complete: true,
      });
    } catch {
      core.warning('Unable to update the explicit gate check; the missing context remains blocking.');
    }
    throw error;
  }
}

async function listOpenPullRequestNumbers({ github, context }) {
  const pulls = await github.paginate(github.rest.pulls.list, {
    ...context.repo,
    state: 'open',
    per_page: 100,
  });
  return pulls.map((pull) => pull.number);
}

async function pullRequestNumbers({ github, context }) {
  const payloadNumber =
    context.payload.pull_request?.number ||
    (context.payload.issue?.pull_request ? context.payload.issue.number : null);
  if (payloadNumber) {
    return [payloadNumber];
  }
  if (context.eventName === 'workflow_dispatch') {
    const number = Number(context.payload.inputs?.pr_number || 0);
    return number > 0 ? [number] : [];
  }
  return [];
}

async function dispatchLinkedIssue({ github, context, core }) {
  const issueNumber = context.payload.issue?.number;
  if (!issueNumber) {
    return;
  }
  const pulls = await github.paginate(github.rest.pulls.list, {
    ...context.repo,
    state: 'open',
    per_page: 100,
  });
  let dispatched = 0;
  for (const pullRequest of pulls) {
    const issues = await closingIssueNumbers({
      github,
      context,
      pullRequest,
    });
    if (!issues.has(issueNumber)) {
      continue;
    }
    await github.rest.actions.createWorkflowDispatch({
      ...context.repo,
      workflow_id: 'review-completion.yml',
      ref: context.payload.repository.default_branch,
      inputs: { pr_number: String(pullRequest.number) },
    });
    dispatched += 1;
  }
  core.info(`Dispatched ${dispatched} linked pull-request evaluation(s).`);
}

async function completeNonPullRequestPath({ github, context, path, headSha }) {
  const check = await ensureGateCheck({
    github,
    context,
    headSha,
    identity: path,
    title: `Review policy ${path} path`,
  });
  const result = evaluateReviewCompletion({ path });
  await updateGateCheck({
    github,
    context,
    check,
    result,
    complete: true,
  });
}

async function run({ github, context, core }) {
  if (context.eventName === 'merge_group') {
    await completeNonPullRequestPath({
      github,
      context,
      path: 'merge_group',
      headSha: context.payload.merge_group.head_sha,
    });
    return;
  }
  if (context.eventName === 'push') {
    await completeNonPullRequestPath({
      github,
      context,
      path: 'default_branch',
      headSha: context.sha,
    });
    return;
  }

  const numbers = await pullRequestNumbers({ github, context });
  if (numbers.length === 0) {
    core.info('No open pull request requires review-gate evaluation.');
    return;
  }
  let failed = false;
  const poll = shouldPoll(context.eventName, numbers.length);
  for (const pullNumber of numbers) {
    try {
      const result = await evaluatePullRequest({
        github,
        context,
        core,
        pullNumber,
        poll,
      });
      core.info(`PR #${pullNumber}: ${result.state}`);
      failed ||= result.state === 'rejected';
    } catch {
      failed = true;
    }
  }
  if (failed) {
    core.setFailed('One or more review-completion gates rejected or could not evaluate.');
  }
}

module.exports = {
  BREAK_GLASS_WORKFLOW,
  DEFAULT_MAX_WAIT_SECONDS,
  DEFAULT_POLL_SECONDS,
  GATE_PREFIX,
  REVIEW_WORKFLOW,
  completeNonPullRequestPath,
  dispatchLinkedIssue,
  ensureGateCheck,
  evaluatePullRequest,
  evaluateSnapshot,
  isTrustedWorkflowCheck,
  listCheckRuns,
  listOpenPullRequestNumbers,
  listReviewThreads,
  pullRequestNumbers,
  run,
  sanitizedApiFailure,
  shouldPoll,
  workflowRunId,
};
