'use strict';

const zlib = require('node:zlib');

const {
  BREAK_GLASS_KIND,
  BREAK_GLASS_SCHEMA_VERSION,
  COPILOT_LOGIN,
  REVIEW_DOMAINS,
  breakGlassArtifactName,
  breakGlassRunTitle,
  evaluateReviewCompletion,
  latestCopilotReview,
  latestDomainReview,
  pullIdentity,
  requiredDomains,
  reviewMarkers,
  samePullIdentity,
  securityFingerprint,
  summarizeResult,
} = require('./review-completion');
const { closingIssueNumbers } = require('./run-issue-status');

const BREAK_GLASS_WORKFLOW = '.github/workflows/record-review-break-glass.yml';
const DEFAULT_POLL_SECONDS = 30;
const DEFAULT_MAX_WAIT_SECONDS = 12 * 60;
const DEFAULT_STABILITY_SECONDS = 5;
const MAX_BINDING_BYTES = 64 * 1024;
const AUTHORIZED_REVIEW_PERMISSIONS = new Set(['write', 'maintain', 'admin']);

function sanitizedApiFailure(error) {
  if (error?.status === 403 || error?.status === 429) {
    return 'GitHub metadata API was rate-limited or unavailable; the required workflow failed closed.';
  }
  return 'GitHub metadata evaluation failed; the required workflow failed closed.';
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

async function loadReviewPolicy({ github, context, pullRequest }) {
  const linkedIssues = [...await closingIssueNumbers({
    github,
    context,
    pullRequest,
  })].sort((left, right) => left - right);
  const labeledItems = [pullRequest.number, ...linkedIssues];
  const labelSets = await Promise.all(labeledItems.map(async (issueNumber) => ({
    issueNumber,
    labels: (await listLabels({ github, context, issueNumber }))
      .map((label) => label.name)
      .filter(Boolean)
      .sort(),
  })));
  return {
    domains: requiredDomains(labelSets.map((item) => item.labels)),
    labelSets,
    linkedIssues,
  };
}

function repositoryEvidenceUrl(value, context) {
  let url;
  try {
    url = new URL(String(value || ''));
  } catch {
    return null;
  }
  const expectedPrefix = `/${context.repo.owner}/${context.repo.repo}/`;
  return url.protocol === 'https:' &&
    url.hostname === 'github.com' &&
    url.pathname.toLowerCase().startsWith(expectedPrefix.toLowerCase())
    ? url.toString()
    : null;
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

function validateReviewBinding({ binding, domain, identity, context }) {
  if (!binding || binding.schemaVersion !== 2 ||
      binding.kind !== 'independent-review' ||
      binding.domain !== domain ||
      Number(binding.pullNumber) !== identity.pullNumber ||
      binding.headSha !== identity.headSha ||
      Number(binding.baseRepositoryId) !== identity.baseRepositoryId ||
      String(binding.baseRepository || '').toLowerCase() !==
        identity.baseRepository.toLowerCase() ||
      binding.baseRef !== identity.baseRef ||
      binding.baseSha !== identity.baseSha) {
    return 'The newest review marker is not bound to this exact pull-request diff.';
  }
  if (!repositoryEvidenceUrl(binding.evidenceUrl, context)) {
    return 'The newest review marker does not contain a repository-local evidence URL.';
  }
  const summary = String(binding.summary || '').trim();
  if (!summary || summary.length > 2000) {
    return 'The newest review marker does not contain a bounded evidence summary.';
  }
  return null;
}

async function domainEvidence({
  github,
  context,
  reviews,
  domain,
  identity,
  author,
  permissionCache,
}) {
  const review = latestDomainReview(reviews, domain);
  if (!review) {
    return { outcome: 'missing', candidateId: null };
  }
  const markers = reviewMarkers(review.body)
    .filter((marker) => marker.domain === domain);
  if (markers.length !== 1) {
    return {
      outcome: 'failure',
      candidateId: review.id,
      reason: `The newest ${domain} review contains ambiguous evidence markers.`,
    };
  }
  const marker = markers[0];
  if (marker.error) {
    return {
      outcome: 'failure',
      candidateId: review.id,
      reason: marker.error,
    };
  }
  const reviewer = String(review.user?.login || '');
  if (!reviewer || reviewer.endsWith('[bot]')) {
    return {
      outcome: 'failure',
      candidateId: review.id,
      reason: `The newest ${domain} review is not from an authenticated human identity.`,
    };
  }
  if (reviewer.toLowerCase() === String(author || '').toLowerCase()) {
    return {
      outcome: 'failure',
      candidateId: review.id,
      reason: `The pull-request author cannot provide independent ${domain} evidence.`,
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
  const bindingError = validateReviewBinding({
    binding: marker.binding,
    domain,
    identity,
    context,
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
      binding: marker.binding,
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
    reason: `The newest independent ${domain} review has not approved the current diff.`,
  };
}

let crcTable;
function crc32(buffer) {
  if (!crcTable) {
    crcTable = Array.from({ length: 256 }, (_, index) => {
      let value = index;
      for (let bit = 0; bit < 8; bit += 1) {
        value = (value & 1) ? (0xedb88320 ^ (value >>> 1)) : (value >>> 1);
      }
      return value >>> 0;
    });
  }
  let crc = 0xffffffff;
  for (const byte of buffer) {
    crc = crcTable[(crc ^ byte) & 0xff] ^ (crc >>> 8);
  }
  return (crc ^ 0xffffffff) >>> 0;
}

function archiveBuffer(data) {
  if (Buffer.isBuffer(data)) {
    return data;
  }
  if (data instanceof ArrayBuffer) {
    return Buffer.from(data);
  }
  if (ArrayBuffer.isView(data)) {
    return Buffer.from(data.buffer, data.byteOffset, data.byteLength);
  }
  throw new Error('The downloaded evidence artifact was not a binary archive.');
}

function readZipJson(data, expectedFileName) {
  const archive = archiveBuffer(data);
  const minimumEocd = 22;
  let eocd = -1;
  for (let offset = archive.length - minimumEocd;
    offset >= Math.max(0, archive.length - 65_557);
    offset -= 1) {
    if (archive.readUInt32LE(offset) === 0x06054b50) {
      eocd = offset;
      break;
    }
  }
  if (eocd < 0) {
    throw new Error('The evidence artifact is not a valid ZIP archive.');
  }
  const entryCount = archive.readUInt16LE(eocd + 10);
  let cursor = archive.readUInt32LE(eocd + 16);
  const files = [];
  for (let index = 0; index < entryCount; index += 1) {
    if (archive.readUInt32LE(cursor) !== 0x02014b50) {
      throw new Error('The evidence artifact central directory is invalid.');
    }
    const flags = archive.readUInt16LE(cursor + 8);
    const method = archive.readUInt16LE(cursor + 10);
    const expectedCrc = archive.readUInt32LE(cursor + 16);
    const compressedSize = archive.readUInt32LE(cursor + 20);
    const uncompressedSize = archive.readUInt32LE(cursor + 24);
    const nameLength = archive.readUInt16LE(cursor + 28);
    const extraLength = archive.readUInt16LE(cursor + 30);
    const commentLength = archive.readUInt16LE(cursor + 32);
    const localOffset = archive.readUInt32LE(cursor + 42);
    const name = archive.subarray(cursor + 46, cursor + 46 + nameLength)
      .toString('utf8');
    if ((flags & 1) !== 0 || name.includes('\\') ||
        name.split('/').includes('..')) {
      throw new Error('The evidence artifact contains an unsafe ZIP entry.');
    }
    if (!name.endsWith('/') && (name === expectedFileName ||
        name.endsWith(`/${expectedFileName}`))) {
      files.push({
        compressedSize,
        expectedCrc,
        localOffset,
        method,
        name,
        uncompressedSize,
      });
    }
    cursor += 46 + nameLength + extraLength + commentLength;
  }
  if (files.length !== 1) {
    throw new Error('The evidence artifact must contain exactly one expected binding file.');
  }
  const file = files[0];
  if (file.uncompressedSize > MAX_BINDING_BYTES ||
      archive.readUInt32LE(file.localOffset) !== 0x04034b50) {
    throw new Error('The evidence binding is missing, oversized, or invalid.');
  }
  const localNameLength = archive.readUInt16LE(file.localOffset + 26);
  const localExtraLength = archive.readUInt16LE(file.localOffset + 28);
  const dataOffset = file.localOffset + 30 + localNameLength + localExtraLength;
  const compressed = archive.subarray(
    dataOffset,
    dataOffset + file.compressedSize);
  let content;
  if (file.method === 0) {
    content = compressed;
  } else if (file.method === 8) {
    content = zlib.inflateRawSync(compressed, {
      maxOutputLength: MAX_BINDING_BYTES,
    });
  } else {
    throw new Error('The evidence artifact uses an unsupported compression method.');
  }
  if (content.length !== file.uncompressedSize ||
      crc32(content) !== file.expectedCrc) {
    throw new Error('The evidence artifact content failed integrity validation.');
  }
  try {
    return JSON.parse(content.toString('utf8'));
  } catch {
    throw new Error('The evidence artifact binding is not valid JSON.');
  }
}

function validateBreakGlassBinding({
  binding,
  identity,
  context,
  run,
}) {
  if (!binding ||
      binding.schemaVersion !== BREAK_GLASS_SCHEMA_VERSION ||
      binding.kind !== BREAK_GLASS_KIND ||
      !samePullIdentity(binding, identity) ||
      binding.actor !== run.actor?.login ||
      Number(binding.workflowRunId) !== Number(run.id) ||
      !repositoryEvidenceUrl(binding.incidentUrl, context) ||
      String(binding.reason || '').trim().length < 20 ||
      String(binding.reason || '').length > 2000) {
    throw new Error(
      'The break-glass artifact content is not bound to this exact diff, run, and actor.');
  }
  return binding;
}

async function breakGlassEvidence({
  github,
  context,
  identity,
  permissionCache,
}) {
  const runs = await github.paginate(github.rest.actions.listWorkflowRuns, {
    ...context.repo,
    workflow_id: BREAK_GLASS_WORKFLOW,
    event: 'workflow_dispatch',
    per_page: 100,
  });
  const title = breakGlassRunTitle(identity);
  const candidates = runs
    .filter((run) => run.display_title === title)
    .sort((left, right) => Number(right.id || 0) - Number(left.id || 0));
  const run = candidates[0];
  if (!run) {
    return { outcome: 'missing', candidateRunId: null };
  }
  if (run.status !== 'completed') {
    return { outcome: 'pending', candidateRunId: run.id };
  }
  const workflowPath = String(run.path || '').split('@')[0];
  const defaultBranch = context.payload.repository.default_branch;
  const repositoryName = context.payload.repository.full_name;
  if (run.conclusion !== 'success' ||
      run.event !== 'workflow_dispatch' ||
      workflowPath !== BREAK_GLASS_WORKFLOW ||
      run.head_branch !== defaultBranch ||
      run.head_sha !== identity.baseSha ||
      run.repository?.full_name !== repositoryName ||
      !run.actor?.login ||
      String(run.actor.login).endsWith('[bot]')) {
    return {
      outcome: 'failure',
      candidateRunId: run.id,
      reason: 'The newest current-diff break-glass workflow run is failed or untrusted.',
    };
  }
  const permission = await reviewerPermission({
    github,
    context,
    login: run.actor.login,
    cache: permissionCache,
  });
  if (!new Set(['maintain', 'admin']).has(permission)) {
    return {
      outcome: 'failure',
      candidateRunId: run.id,
      reason: 'The newest break-glass actor is no longer an authorized maintainer.',
    };
  }
  const artifacts = await github.paginate(
    github.rest.actions.listWorkflowRunArtifacts,
    {
      ...context.repo,
      run_id: run.id,
      per_page: 100,
    });
  const artifactName = breakGlassArtifactName(identity);
  const matches = artifacts.filter((artifact) =>
    artifact.name === artifactName && artifact.expired === false);
  if (matches.length !== 1) {
    return {
      outcome: 'failure',
      candidateRunId: run.id,
      reason: 'The newest break-glass run lacks one unexpired exact-identity artifact.',
    };
  }
  try {
    const download = await github.rest.actions.downloadArtifact({
      ...context.repo,
      artifact_id: matches[0].id,
      archive_format: 'zip',
    });
    const binding = validateBreakGlassBinding({
      binding: readZipJson(
        download.data,
        `${artifactName}.json`),
      identity,
      context,
      run,
    });
    return {
      outcome: 'success',
      candidateRunId: run.id,
      actor: run.actor.login,
      binding,
    };
  } catch {
    return {
      outcome: 'failure',
      candidateRunId: run.id,
      reason: 'The newest break-glass artifact content failed exact binding validation.',
    };
  }
}

function reviewSecurityState(reviews) {
  return reviews.map((review) => ({
    id: Number(review.id),
    login: review.user?.login || '',
    state: review.state || '',
    commitId: review.commit_id || '',
    submittedAt: review.submitted_at || '',
    bodyDigest: securityFingerprint(String(review.body || '')),
  })).sort((left, right) => left.id - right.id);
}

function threadSecurityState(threads) {
  return threads.map((thread) => ({
    id: thread.id,
    isResolved: thread.isResolved,
    comments: thread.comments?.nodes || [],
  })).sort((left, right) => String(left.id).localeCompare(String(right.id)));
}

async function evaluateSnapshot({
  github,
  context,
  pullNumber,
  expectedIdentity,
}) {
  const pullResponse = await github.rest.pulls.get({
    ...context.repo,
    pull_number: pullNumber,
  });
  const pullRequest = pullResponse.data;
  const identity = pullIdentity(pullRequest);
  if (expectedIdentity && !samePullIdentity(identity, expectedIdentity)) {
    return {
      stale: true,
      identity,
      result: {
        state: 'rejected',
        reasons: ['The pull-request head or reviewed base changed during evaluation.'],
      },
      fingerprint: null,
    };
  }

  const [reviews, threads, policy] = await Promise.all([
    github.paginate(github.rest.pulls.listReviews, {
      ...context.repo,
      pull_number: pullNumber,
      per_page: 100,
    }),
    listReviewThreads({ github, context, pullNumber }),
    loadReviewPolicy({ github, context, pullRequest }),
  ]);
  const permissionCache = new Map();
  const evidenceEntries = await Promise.all(policy.domains.map(async (domain) => [
    domain,
    await domainEvidence({
      github,
      context,
      reviews,
      domain,
      identity,
      author: pullRequest.user?.login,
      permissionCache,
    }),
  ]));
  const evidence = Object.fromEntries(evidenceEntries);
  const breakGlass = await breakGlassEvidence({
    github,
    context,
    identity,
    permissionCache,
  });
  const copilotReview = latestCopilotReview(reviews);
  const unresolvedThreads = threads.filter((thread) => !thread.isResolved).length;
  const result = evaluateReviewCompletion({
    draft: pullRequest.draft,
    open: pullRequest.state === 'open',
    identity,
    copilotReview,
    unresolvedThreads,
    domains: policy.domains,
    evidence,
    breakGlass,
  });
  const securityState = {
    identity,
    open: pullRequest.state === 'open',
    draft: Boolean(pullRequest.draft),
    author: pullRequest.user?.login || '',
    policy,
    reviews: reviewSecurityState(reviews),
    threads: threadSecurityState(threads),
    evidence,
    breakGlass,
    result,
  };
  return {
    stale: false,
    identity,
    result,
    fingerprint: securityFingerprint(securityState),
    securityState,
  };
}

async function sleep(milliseconds) {
  await new Promise((resolve) => setTimeout(resolve, milliseconds));
}

async function evaluatePullRequest({
  github,
  context,
  pullNumber,
  poll = true,
  pollSeconds = DEFAULT_POLL_SECONDS,
  maxWaitSeconds = DEFAULT_MAX_WAIT_SECONDS,
  stabilitySeconds = DEFAULT_STABILITY_SECONDS,
  sleepFunction = sleep,
  now = () => Date.now(),
}) {
  const initialResponse = await github.rest.pulls.get({
    ...context.repo,
    pull_number: pullNumber,
  });
  const expectedIdentity = pullIdentity(initialResponse.data);
  const deadline = now() + maxWaitSeconds * 1000;
  let snapshot = null;

  while (true) {
    snapshot = await evaluateSnapshot({
      github,
      context,
      pullNumber,
      expectedIdentity,
    });
    if (snapshot.stale || snapshot.result.state === 'rejected') {
      return snapshot.result;
    }
    if (snapshot.result.state === 'approved') {
      await sleepFunction(stabilitySeconds * 1000);
      const confirmation = await evaluateSnapshot({
        github,
        context,
        pullNumber,
        expectedIdentity,
      });
      if (confirmation.stale || confirmation.result.state === 'rejected') {
        return confirmation.result;
      }
      if (confirmation.result.state === 'approved' &&
          confirmation.fingerprint === snapshot.fingerprint) {
        return confirmation.result;
      }
      snapshot = confirmation;
    }
    if (!poll) {
      return snapshot.result;
    }
    if (now() >= deadline) {
      return {
        state: 'rejected',
        reasons: [
          ...snapshot.result.reasons,
          `Review automation did not complete within ${maxWaitSeconds} seconds.`,
        ],
      };
    }
    await sleepFunction(pollSeconds * 1000);
  }
}

async function pullRequestNumbers({ context }) {
  const payloadNumber =
    context.payload.pull_request?.number ||
    (context.payload.issue?.pull_request ? context.payload.issue.number : null);
  if (payloadNumber) {
    return [Number(payloadNumber)];
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
  core.info(
    `Dispatched ${dispatched} linked pull-request evaluation(s); ` +
    'ruleset activation remains blocked until canary proves PR association.');
}

async function writeSummary(core, pullNumber, result) {
  if (core.summary?.addHeading) {
    await core.summary
      .addHeading(`Review completion — PR #${pullNumber}`)
      .addRaw(summarizeResult(result))
      .write();
  }
}

async function run({ github, context, core }) {
  if (context.eventName === 'merge_group') {
    core.info(summarizeResult(evaluateReviewCompletion({ path: 'merge_group' })));
    return;
  }
  if (context.eventName === 'push') {
    core.info(summarizeResult(evaluateReviewCompletion({ path: 'default_branch' })));
    return;
  }
  if (context.eventName === 'issues') {
    await dispatchLinkedIssue({ github, context, core });
    return;
  }

  const numbers = await pullRequestNumbers({ context });
  if (numbers.length === 0) {
    core.setFailed('No open pull request was bound to this required workflow run.');
    return;
  }
  for (const pullNumber of numbers) {
    let result;
    try {
      result = await evaluatePullRequest({
        github,
        context,
        pullNumber,
      });
    } catch (error) {
      result = {
        state: 'rejected',
        reasons: [sanitizedApiFailure(error)],
      };
    }
    core.info(`PR #${pullNumber}: ${result.state}`);
    await writeSummary(core, pullNumber, result);
    if (result.state !== 'approved') {
      core.setFailed(`PR #${pullNumber}: ${result.reasons.join(' ')}`);
    }
  }
}

module.exports = {
  AUTHORIZED_REVIEW_PERMISSIONS,
  BREAK_GLASS_WORKFLOW,
  DEFAULT_MAX_WAIT_SECONDS,
  DEFAULT_POLL_SECONDS,
  DEFAULT_STABILITY_SECONDS,
  archiveBuffer,
  breakGlassEvidence,
  crc32,
  dispatchLinkedIssue,
  domainEvidence,
  evaluatePullRequest,
  evaluateSnapshot,
  listReviewThreads,
  loadReviewPolicy,
  pullRequestNumbers,
  readZipJson,
  repositoryEvidenceUrl,
  run,
  sanitizedApiFailure,
  validateBreakGlassBinding,
  validateReviewBinding,
};
