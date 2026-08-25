'use strict';

const {
  STATUS,
  classifyDependencies,
  deriveStatus,
  manualBlockers,
  planLabelUpdate,
  statusOverrideForEvent,
} = require('./issue-status');

const LABELS = Object.freeze({
  [STATUS.BACKLOG]: ['D4E5F7', 'Open with no tracked implementation branch or pull request'],
  [STATUS.BRANCH_ONLY]: ['5319E7', 'Implementation branch exists without an open pull request'],
  [STATUS.PR_DRAFT]: ['BFD4F2', 'A linked pull request is still draft'],
  [STATUS.READY]: ['0E8A16', 'A linked pull request is ready for review'],
  [STATUS.MERGED]: ['6F42C1', 'A linked pull request merged to the default branch'],
  [STATUS.CLOSED]: ['6A737D', 'Issue closed without a merge to the default branch'],
  'blocked:dependency': ['B60205', 'Unresolved issue dependency; lifecycle status remains independent'],
  'blocked:evidence': ['D93F0B', 'Unresolved dependency classified as external or exit evidence'],
  'blocked:human': ['FBCA04', 'Unresolved dependency classified as a human decision or approval'],
  'blocks:evidence': ['D93F0B', 'Classifies this issue as an evidence dependency for blocked issues'],
  'blocks:human': ['FBCA04', 'Classifies this issue as a human decision dependency for blocked issues'],
});

function issueNumberFromBranch(branch) {
  const match = String(branch || '').match(/^(?:squad\/)?(\d+)(?:-|$)/);
  return match ? Number(match[1]) : null;
}

function issueNumbersFromBody(body) {
  const numbers = new Set();
  const pattern = /\b(?:close[sd]?|fix(?:e[sd])?|resolve[sd]?)\s+#(\d+)\b/gi;
  for (const match of String(body || '').matchAll(pattern)) {
    numbers.add(Number(match[1]));
  }
  return numbers;
}

async function ensureLabel({ github, context, name }) {
  const [color, description] = LABELS[name];
  try {
    const { data } = await github.rest.issues.getLabel({ ...context.repo, name });
    if (data.color.toLowerCase() !== color.toLowerCase() ||
        data.description !== description) {
      await github.rest.issues.updateLabel({
        ...context.repo,
        name,
        color,
        description,
      });
    }
  } catch (error) {
    if (error.status !== 404) {
      throw error;
    }
    await github.rest.issues.createLabel({
      ...context.repo,
      name,
      color,
      description,
    });
  }
}

async function listDependencies({ github, context, issueNumber, direction }) {
  const endpoint = direction === 'blocking' ? 'blocking' : 'blocked_by';
  return github.paginate(
    `GET /repos/{owner}/{repo}/issues/{issue_number}/dependencies/${endpoint}`,
    {
      ...context.repo,
      issue_number: issueNumber,
      per_page: 100,
      headers: {
        'X-GitHub-Api-Version': '2022-11-28',
      },
    });
}

async function linkedPullRequests({ github, context, issueNumber }) {
  const events = await github.paginate(github.rest.issues.listEventsForTimeline, {
    ...context.repo,
    issue_number: issueNumber,
    per_page: 100,
  });
  const pullRequestNumbers = new Set();

  for (const event of events) {
    const sourceIssue = event.source?.issue;
    if (event.event === 'cross-referenced' && sourceIssue?.pull_request) {
      pullRequestNumbers.add(sourceIssue.number);
    }
  }

  const pullRequests = [];
  for (const pullNumber of [...pullRequestNumbers].slice(0, 100)) {
    const { data } = await github.rest.pulls.get({
      ...context.repo,
      pull_number: pullNumber,
    });
    if (!issueNumbersFromBody(data.body).has(issueNumber)) {
      continue;
    }
    pullRequests.push({
      state: data.state,
      isDraft: data.draft,
      merged: data.merged,
      baseRef: data.base.ref,
    });
  }
  return pullRequests;
}

async function closingIssueNumbers({ github, context, pullRequest }) {
  const numbers = issueNumbersFromBody(pullRequest.body);
  const result = await github.graphql(
    `query($owner: String!, $repo: String!, $number: Int!) {
      repository(owner: $owner, name: $repo) {
        pullRequest(number: $number) {
          closingIssuesReferences(first: 100) {
            nodes { number }
          }
        }
      }
    }`,
    {
      owner: context.repo.owner,
      repo: context.repo.repo,
      number: pullRequest.number,
    });
  for (const issue of result.repository.pullRequest.closingIssuesReferences.nodes) {
    numbers.add(issue.number);
  }
  return numbers;
}

async function expandBlockingIssues({ github, context, issueNumbers }) {
  const expanded = new Set(issueNumbers);
  for (const issueNumber of issueNumbers) {
    const blockedIssues = await listDependencies({
      github,
      context,
      issueNumber,
      direction: 'blocking',
    });
    for (const issue of blockedIssues) {
      expanded.add(issue.number);
    }
  }
  return expanded;
}

async function targetIssueNumbers({ github, context }) {
  const payload = context.payload;
  let issueNumbers = new Set();

  if (context.eventName === 'workflow_dispatch') {
    const requested = String(payload.inputs?.issue_number || '').trim();
    if (requested) {
      issueNumbers.add(Number(requested));
    } else {
      const issues = await github.paginate(github.rest.issues.listForRepo, {
        ...context.repo,
        state: 'all',
        per_page: 100,
      });
      issueNumbers = new Set(
        issues.filter((issue) => !issue.pull_request).map((issue) => issue.number));
    }
  } else if (context.eventName === 'issues') {
    issueNumbers.add(payload.issue.number);
  } else if (context.eventName === 'pull_request_target') {
    issueNumbers = await closingIssueNumbers({
      github,
      context,
      pullRequest: payload.pull_request,
    });
  } else if (context.eventName === 'create' || context.eventName === 'delete') {
    const issueNumber = issueNumberFromBranch(payload.ref);
    if (payload.ref_type === 'branch' && issueNumber) {
      issueNumbers.add(issueNumber);
    }
  }

  return expandBlockingIssues({ github, context, issueNumbers });
}

async function applyPlan({ github, context, core, issueNumber, plan }) {
  for (const label of plan.add) {
    await ensureLabel({ github, context, name: label });
  }
  if (plan.add.length > 0) {
    await github.rest.issues.addLabels({
      ...context.repo,
      issue_number: issueNumber,
      labels: plan.add,
    });
  }

  for (const label of plan.remove) {
    try {
      await github.rest.issues.removeLabel({
        ...context.repo,
        issue_number: issueNumber,
        name: label,
      });
    } catch (error) {
      if (error.status !== 404) {
        throw error;
      }
    }
  }

  if (plan.add.length === 0 && plan.remove.length === 0) {
    core.info(`Issue #${issueNumber}: status labels already reconciled`);
  } else {
    core.info(
      `Issue #${issueNumber}: added [${plan.add.join(', ')}], ` +
      `removed [${plan.remove.join(', ')}]`);
  }
}

async function reconcileIssue({ github, context, core, issueNumber, primaryIssue }) {
  const { data: issue } = await github.rest.issues.get({
    ...context.repo,
    issue_number: issueNumber,
  });
  if (issue.pull_request) {
    return;
  }

  const pullRequests = await linkedPullRequests({
    github,
    context,
    issueNumber,
  });
  const dependencies = await listDependencies({
    github,
    context,
    issueNumber,
    direction: 'blocked_by',
  });
  const appliedLabel = context.payload.label?.name;
  const eventOverride = primaryIssue
    ? statusOverrideForEvent({
      eventName: context.eventName,
      action: context.payload.action,
      appliedLabel,
      inputs: context.payload.inputs,
    })
    : null;
  const desiredStatus = eventOverride || deriveStatus({
    issueState: issue.state,
    pullRequests,
    branchLinked:
      (context.eventName === 'create' && primaryIssue) ||
      (context.eventName !== 'delete' &&
        issue.labels.some((label) => label.name === STATUS.BRANCH_ONLY)),
    defaultBranch: context.payload.repository.default_branch,
    mergeEvent:
      context.eventName === 'pull_request_target' &&
      context.payload.action === 'closed' &&
      context.payload.pull_request.merged === true,
  });
  const blockerOverride = context.eventName === 'workflow_dispatch' && primaryIssue
    ? manualBlockers(context.payload.inputs?.blockers)
    : null;
  const blockerLabels = blockerOverride ?? classifyDependencies(dependencies);
  const plan = planLabelUpdate({
    currentLabels: issue.labels,
    desiredStatus,
    blockerLabels,
  });

  await applyPlan({ github, context, core, issueNumber, plan });
}

async function run({ github, context, core }) {
  if (context.eventName === 'pull_request_target' &&
      context.payload.pull_request.head.repo.full_name !==
      context.payload.repository.full_name) {
    core.info('Ignoring fork pull request: write-capable status tracking only trusts repository branches');
    return;
  }

  const issueNumbers = await targetIssueNumbers({ github, context });
  if (issueNumbers.size === 0) {
    core.info('No linked issue to reconcile');
    return;
  }

  for (const name of Object.keys(LABELS)) {
    await ensureLabel({ github, context, name });
  }

  const primaryIssueNumber = context.eventName === 'issues'
    ? context.payload.issue.number
    : context.eventName === 'workflow_dispatch' &&
      String(context.payload.inputs?.issue_number || '').trim()
      ? Number(context.payload.inputs.issue_number)
      : context.eventName === 'create' || context.eventName === 'delete'
        ? issueNumberFromBranch(context.payload.ref)
        : null;

  for (const issueNumber of issueNumbers) {
    await reconcileIssue({
      github,
      context,
      core,
      issueNumber,
      primaryIssue: issueNumber === primaryIssueNumber,
    });
  }
}

module.exports = {
  LABELS,
  closingIssueNumbers,
  issueNumberFromBranch,
  issueNumbersFromBody,
  run,
};
