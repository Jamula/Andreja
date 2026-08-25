'use strict';

const STATUS = Object.freeze({
  BACKLOG: 'status:backlog',
  BRANCH_ONLY: 'status:branch-only',
  PR_DRAFT: 'status:pr-draft',
  READY: 'status:ready',
  MERGED: 'status:merged',
  CLOSED: 'status:closed',
});

const KNOWN_STATUSES = new Set(Object.values(STATUS));
const MANAGED_BLOCKERS = new Set([
  'blocked:dependency',
  'blocked:evidence',
  'blocked:human',
]);

function labelNames(labels = []) {
  return labels.map((label) => typeof label === 'string' ? label : label.name);
}

function normalizeStatus(value) {
  if (!value || value === 'auto') {
    return null;
  }

  const candidate = value.startsWith('status:') ? value : `status:${value}`;
  if (!KNOWN_STATUSES.has(candidate)) {
    throw new Error(`Unknown lifecycle status: ${value}`);
  }

  return candidate;
}

function deriveStatus({
  issueState = 'open',
  pullRequests = [],
  branchLinked = false,
  defaultBranch = 'main',
  mergeEvent = false,
} = {}) {
  const normalizedPullRequests = pullRequests.map((pullRequest) => ({
    state: String(pullRequest.state || '').toLowerCase(),
    isDraft: Boolean(pullRequest.isDraft ?? pullRequest.draft),
    merged: Boolean(pullRequest.merged || pullRequest.merged_at),
    baseRef: pullRequest.baseRef || pullRequest.base?.ref,
    promotesIssue: Boolean(pullRequest.promotesIssue),
    supersededByReopen: Boolean(pullRequest.supersededByReopen),
  }));

  if (String(issueState).toLowerCase() === 'closed') {
    return normalizedPullRequests.some((pullRequest) =>
      !pullRequest.supersededByReopen && pullRequest.merged &&
      pullRequest.baseRef === defaultBranch)
      ? STATUS.MERGED
      : STATUS.CLOSED;
  }

  if (normalizedPullRequests.some((pullRequest) =>
    !pullRequest.supersededByReopen && pullRequest.promotesIssue &&
    pullRequest.merged &&
    pullRequest.baseRef === defaultBranch)) {
    return STATUS.MERGED;
  }

  if (mergeEvent && normalizedPullRequests.some((pullRequest) =>
    pullRequest.merged && pullRequest.baseRef === defaultBranch)) {
    return STATUS.MERGED;
  }

  const openPullRequests = normalizedPullRequests.filter(
    (pullRequest) => pullRequest.state === 'open');
  if (openPullRequests.some((pullRequest) => !pullRequest.isDraft)) {
    return STATUS.READY;
  }

  if (openPullRequests.some((pullRequest) => pullRequest.isDraft)) {
    return STATUS.PR_DRAFT;
  }

  if (branchLinked || normalizedPullRequests.some((pullRequest) =>
    !pullRequest.supersededByReopen &&
    (!pullRequest.merged || pullRequest.baseRef !== defaultBranch))) {
    return STATUS.BRANCH_ONLY;
  }

  return STATUS.BACKLOG;
}

function classifyDependencies(dependencies = []) {
  const blockers = new Set();

  for (const dependency of dependencies) {
    if (String(dependency.state).toLowerCase() === 'closed') {
      continue;
    }

    const labels = new Set(labelNames(dependency.labels));
    let classified = false;
    if (labels.has('blocks:evidence')) {
      blockers.add('blocked:evidence');
      classified = true;
    }
    if (labels.has('blocks:human')) {
      blockers.add('blocked:human');
      classified = true;
    }
    if (!classified) {
      blockers.add('blocked:dependency');
    }
  }

  return [...blockers].sort();
}

function manualBlockers(value) {
  switch (value) {
    case undefined:
    case null:
    case '':
    case 'auto':
      return null;
    case 'none':
      return [];
    case 'dependency':
      return ['blocked:dependency'];
    case 'evidence':
      return ['blocked:evidence'];
    case 'human':
      return ['blocked:human'];
    case 'evidence-and-human':
      return ['blocked:evidence', 'blocked:human'];
    default:
      throw new Error(`Unknown blocker override: ${value}`);
  }
}

function statusOverrideForEvent({ eventName, action, appliedLabel, inputs = {} }) {
  if (eventName === 'workflow_dispatch') {
    return normalizeStatus(inputs.lifecycle);
  }

  if (eventName === 'issues' && action === 'labeled' &&
      KNOWN_STATUSES.has(appliedLabel)) {
    return appliedLabel;
  }

  return null;
}

function planLabelUpdate({
  currentLabels = [],
  desiredStatus,
  blockerLabels = [],
}) {
  if (!KNOWN_STATUSES.has(desiredStatus)) {
    throw new Error(`Cannot apply unknown lifecycle status: ${desiredStatus}`);
  }

  const current = new Set(labelNames(currentLabels));
  const desired = new Set([desiredStatus, ...blockerLabels]);
  const remove = [];

  for (const label of current) {
    if (label.startsWith('status:') && label !== desiredStatus) {
      remove.push(label);
    } else if (MANAGED_BLOCKERS.has(label) && !desired.has(label)) {
      remove.push(label);
    }
  }

  const add = [...desired].filter((label) => !current.has(label));
  return {
    add: add.sort(),
    remove: remove.sort(),
  };
}

function resolveEvent(fixture) {
  const statusOverride = statusOverrideForEvent(fixture);
  const desiredStatus = statusOverride || deriveStatus(fixture);
  const blockerOverride = fixture.eventName === 'workflow_dispatch'
    ? manualBlockers(fixture.inputs?.blockers)
    : null;
  const blockerLabels = blockerOverride ?? classifyDependencies(fixture.dependencies);

  return {
    desiredStatus,
    blockerLabels,
    plan: planLabelUpdate({
      currentLabels: fixture.currentLabels,
      desiredStatus,
      blockerLabels,
    }),
  };
}

module.exports = {
  KNOWN_STATUSES,
  MANAGED_BLOCKERS,
  STATUS,
  classifyDependencies,
  deriveStatus,
  labelNames,
  manualBlockers,
  normalizeStatus,
  planLabelUpdate,
  resolveEvent,
  statusOverrideForEvent,
};
