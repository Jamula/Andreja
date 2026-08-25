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

const BRANCH_PATTERNS = Object.freeze([
  /^squad\/(?<issue>\d+)-[a-z0-9][a-z0-9-]*$/,
  /^copilot\/(?<issue>\d+)-[a-z0-9][a-z0-9-]*$/,
  /^u\/[a-z0-9_.-]+\/(?<issue>\d+)-[a-z0-9][a-z0-9-]*$/,
]);

function issueNumberFromBranch(branch, patterns = BRANCH_PATTERNS) {
  for (const pattern of patterns) {
    const match = String(branch || '').match(pattern);
    if (match) {
      return Number(match.groups.issue);
    }
  }
  return null;
}

function issueNumbersFromBody(body, repository = {}) {
  const numbers = new Set();
  const pattern = /\b(?:close[sd]?|fix(?:e[sd])?|resolve[sd]?)\s*:?\s+(?:([a-z0-9_.-]+)\/([a-z0-9_.-]+))?#(\d+)\b/gi;
  for (const match of String(body || '').matchAll(pattern)) {
    const [, owner, repo, number] = match;
    if ((!owner && !repo) ||
        (owner.toLowerCase() === String(repository.owner).toLowerCase() &&
          repo.toLowerCase() === String(repository.repo).toLowerCase())) {
      numbers.add(Number(number));
    }
  }
  return numbers;
}

function normalizeRepositoryUrl(url) {
  return String(url || '').replace(/\/+$/, '').toLowerCase();
}

function isCurrentRepositoryIssue(issue, repositoryUrl) {
  return normalizeRepositoryUrl(issue?.repository_url) ===
    normalizeRepositoryUrl(repositoryUrl);
}

function pullRequestNumbersFromTimeline(events, repositoryUrl) {
  const numbers = new Set();
  for (const event of events) {
    const sourceIssue = event.source?.issue;
    if (event.event === 'cross-referenced' && sourceIssue?.pull_request &&
        isCurrentRepositoryIssue(sourceIssue, repositoryUrl)) {
      numbers.add(sourceIssue.number);
    }
  }
  return numbers;
}

function localIssueNumbers(issues, repositoryUrl) {
  return issues
    .filter((issue) => isCurrentRepositoryIssue(issue, repositoryUrl))
    .map((issue) => issue.number);
}

function pullRequestReferencesIssue(pullRequest, issueNumber, repository) {
  return issueNumbersFromBody(pullRequest.body, repository).has(issueNumber);
}

function isTrustedPullRequestReference(
  pullRequest,
  timelineNumbers,
  repositoryFullName,
) {
  return pullRequest.headRepository === repositoryFullName ||
    (pullRequest.merged && timelineNumbers.has(pullRequest.number));
}

function expandPullRequestPromotions(
  directPullRequests,
  allPullRequests,
  defaultBranch,
  repositoryFullName,
  latestReopenedAt = null,
) {
  const isSuperseded = (pullRequest) =>
    Boolean(latestReopenedAt && pullRequest.mergedAt &&
      Date.parse(pullRequest.mergedAt) < Date.parse(latestReopenedAt));
  const expanded = new Map(directPullRequests.map((pullRequest) => [
    pullRequest.number,
    {
      ...pullRequest,
      supersededByReopen: isSuperseded(pullRequest),
    },
  ]));
  const branches = [];

  for (const pullRequest of directPullRequests) {
    if (pullRequest.merged && pullRequest.baseRef !== defaultBranch &&
        !isSuperseded(pullRequest)) {
      branches.push({
        name: pullRequest.baseRef,
        after: pullRequest.mergedAt,
      });
    }
  }

  while (branches.length > 0) {
    const promotedBranch = branches.shift();
    for (const candidate of allPullRequests) {
      if (candidate.headRepository !== repositoryFullName ||
          candidate.headRef !== promotedBranch.name ||
          expanded.has(candidate.number)) {
        continue;
      }
      if (candidate.merged &&
          (!promotedBranch.after || !candidate.mergedAt ||
            Date.parse(candidate.mergedAt) < Date.parse(promotedBranch.after))) {
        continue;
      }

      expanded.set(candidate.number, {
        ...candidate,
        promotesIssue: true,
        supersededByReopen: isSuperseded(candidate),
      });
      if (candidate.merged && candidate.baseRef !== defaultBranch) {
        branches.push({
          name: candidate.baseRef,
          after: candidate.mergedAt,
        });
      }
    }
  }

  return [...expanded.values()];
}

function pullRequestIssueNumbers(payload, repository) {
  const numbers = issueNumbersFromBody(payload.pull_request?.body, repository);
  const previousBody = payload.action === 'edited'
    ? payload.changes?.body?.from
    : null;
  for (const number of issueNumbersFromBody(previousBody, repository)) {
    numbers.add(number);
  }
  return numbers;
}

function latestIssueEventAt(events, eventName) {
  let latest = null;
  for (const event of events) {
    if (event.event !== eventName || !event.created_at) {
      continue;
    }
    if (!latest || Date.parse(event.created_at) > Date.parse(latest)) {
      latest = event.created_at;
    }
  }
  return latest;
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

function normalizePullRequest(pullRequest) {
  return {
    number: pullRequest.number,
    state: pullRequest.state,
    isDraft: pullRequest.draft,
    merged: Boolean(pullRequest.merged ?? pullRequest.merged_at),
    mergedAt: pullRequest.merged_at,
    baseRef: pullRequest.base?.ref,
    headRef: pullRequest.head?.ref,
    headRepository: pullRequest.head?.repo?.full_name,
    body: pullRequest.body,
  };
}

async function loadRepositoryEvidence({ github, context }) {
  const [branches, pullRequests] = await Promise.all([
    github.paginate(github.rest.repos.listBranches, {
      ...context.repo,
      per_page: 100,
    }),
    github.paginate(github.rest.pulls.list, {
      ...context.repo,
      state: 'all',
      sort: 'updated',
      direction: 'desc',
      per_page: 100,
    }),
  ]);

  return {
    branchNamesByIssue: branchNamesByIssue(branches.map((branch) => branch.name)),
    pullRequests: pullRequests.map(normalizePullRequest),
  };
}

function branchNamesByIssue(branchNames) {
  const branches = new Map();
  for (const branchName of branchNames) {
    const issueNumber = issueNumberFromBranch(branchName);
    if (issueNumber === null) {
      continue;
    }
    const names = branches.get(issueNumber) || new Set();
    names.add(branchName);
    branches.set(issueNumber, names);
  }
  return branches;
}

function updateBranchEvidence(evidence, eventName, branchName) {
  const issueNumber = issueNumberFromBranch(branchName);
  if (issueNumber === null) {
    return null;
  }

  const names = evidence.branchNamesByIssue.get(issueNumber) || new Set();
  if (eventName === 'create') {
    names.add(branchName);
  } else if (eventName === 'delete') {
    names.delete(branchName);
  }

  if (names.size > 0) {
    evidence.branchNamesByIssue.set(issueNumber, names);
  } else {
    evidence.branchNamesByIssue.delete(issueNumber);
  }
  return issueNumber;
}

async function linkedPullRequests({ github, context, issueNumber, evidence }) {
  const events = await github.paginate(github.rest.issues.listEventsForTimeline, {
    ...context.repo,
    issue_number: issueNumber,
    per_page: 100,
  });
  const repositoryUrl = context.payload.repository.url;
  const timelineNumbers = pullRequestNumbersFromTimeline(events, repositoryUrl);
  const directPullRequests = evidence.pullRequests.filter((pullRequest) =>
    pullRequestReferencesIssue(pullRequest, issueNumber, context.repo) &&
    isTrustedPullRequestReference(
      pullRequest,
      timelineNumbers,
      context.payload.repository.full_name));

  return expandPullRequestPromotions(
    directPullRequests,
    evidence.pullRequests,
    context.payload.repository.default_branch,
    context.payload.repository.full_name,
    latestIssueEventAt(events, 'reopened'),
  );
}

async function closingIssueNumbers({ github, context, pullRequest }) {
  const numbers = issueNumbersFromBody(pullRequest.body, context.repo);
  const result = await github.graphql(
    `query($owner: String!, $repo: String!, $number: Int!) {
      repository(owner: $owner, name: $repo) {
        pullRequest(number: $number) {
          closingIssuesReferences(first: 100) {
            nodes {
              number
              repository { nameWithOwner }
            }
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
    if (issue.repository.nameWithOwner.toLowerCase() ===
        `${context.repo.owner}/${context.repo.repo}`.toLowerCase()) {
      numbers.add(issue.number);
    }
  }
  return numbers;
}

async function expandBlockingIssues({ github, context, issueNumbers }) {
  const expanded = new Set(issueNumbers);
  const repositoryUrl = context.payload.repository.url;
  for (const issueNumber of issueNumbers) {
    const blockedIssues = await listDependencies({
      github,
      context,
      issueNumber,
      direction: 'blocking',
    });
    for (const number of localIssueNumbers(blockedIssues, repositoryUrl)) {
      expanded.add(number);
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
  } else if (context.eventName === 'schedule') {
    const issues = await github.paginate(github.rest.issues.listForRepo, {
      ...context.repo,
      state: 'all',
      per_page: 100,
    });
    issueNumbers = new Set(
      issues.filter((issue) => !issue.pull_request).map((issue) => issue.number));
  } else if (context.eventName === 'issues') {
    issueNumbers.add(payload.issue.number);
  } else if (context.eventName === 'pull_request_target') {
    const currentNumbers = await closingIssueNumbers({
      github,
      context,
      pullRequest: payload.pull_request,
    });
    issueNumbers = pullRequestIssueNumbers(payload, context.repo);
    for (const number of currentNumbers) {
      issueNumbers.add(number);
    }
  } else if (context.eventName === 'create' || context.eventName === 'delete') {
    const issueNumber = issueNumberFromBranch(payload.ref);
    if (payload.ref_type === 'branch' && issueNumber) {
      issueNumbers.add(issueNumber);
    }
  }

  const isFullReconciliation = context.eventName === 'schedule' ||
    (context.eventName === 'workflow_dispatch' &&
      !String(payload.inputs?.issue_number || '').trim());
  return isFullReconciliation
    ? issueNumbers
    : expandBlockingIssues({ github, context, issueNumbers });
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

async function reconcileIssue({
  github,
  context,
  core,
  issueNumber,
  primaryIssue,
  evidence,
}) {
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
    evidence,
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
    branchLinked: evidence.branchNamesByIssue.has(issueNumber),
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
      context.payload.pull_request.head.repo?.full_name !==
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

  const evidence = await loadRepositoryEvidence({ github, context });
  if (context.eventName === 'create' || context.eventName === 'delete') {
    updateBranchEvidence(evidence, context.eventName, context.payload.ref);
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
      evidence,
    });
  }
}

module.exports = {
  BRANCH_PATTERNS,
  LABELS,
  branchNamesByIssue,
  closingIssueNumbers,
  expandPullRequestPromotions,
  isCurrentRepositoryIssue,
  isTrustedPullRequestReference,
  issueNumberFromBranch,
  issueNumbersFromBody,
  localIssueNumbers,
  latestIssueEventAt,
  normalizePullRequest,
  pullRequestIssueNumbers,
  pullRequestNumbersFromTimeline,
  run,
  targetIssueNumbers,
  updateBranchEvidence,
};
