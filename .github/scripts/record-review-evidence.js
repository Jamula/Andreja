'use strict';

const fs = require('node:fs');
const path = require('node:path');

const {
  BREAK_GLASS_PREFIX,
  CHECK_NAMES,
  EVIDENCE_PREFIX,
  REVIEW_DOMAINS,
  breakGlassArtifactName,
  evidenceArtifactName,
  evidenceCheckName,
} = require('./review-completion');

const BREAK_GLASS_CONFIRMATION = 'BREAK GLASS REVIEW GATE';
const DEFAULT_ARTIFACT_DIRECTORY = path.join(
  process.cwd(),
  'artifacts',
  'review-evidence');

function repositoryEvidenceUrl(value, context) {
  let url;
  try {
    url = new URL(String(value || ''));
  } catch {
    throw new Error('Evidence URL must be a valid URL.');
  }
  const expectedPrefix = `/${context.repo.owner}/${context.repo.repo}/`;
  if (url.protocol !== 'https:' || url.hostname !== 'github.com' ||
      !url.pathname.toLowerCase().startsWith(expectedPrefix.toLowerCase())) {
    throw new Error('Evidence URL must point to this GitHub repository.');
  }
  return url.toString();
}

function normalizedInputs(context) {
  const inputs = context.payload.inputs || {};
  const pullNumber = Number(inputs.pr_number);
  const headSha = String(inputs.head_sha || '').trim();
  if (!Number.isInteger(pullNumber) || pullNumber <= 0) {
    throw new Error('A positive pull-request number is required.');
  }
  if (!/^[0-9a-f]{40}$/.test(headSha)) {
    throw new Error('The exact 40-character lowercase pull-request head SHA is required.');
  }
  return { inputs, pullNumber, headSha };
}

async function currentPullRequest({ github, context, pullNumber, headSha }) {
  const response = await github.rest.pulls.get({
    ...context.repo,
    pull_number: pullNumber,
  });
  const pullRequest = response.data;
  if (pullRequest.state !== 'open') {
    throw new Error('Review evidence can be recorded only for an open pull request.');
  }
  if (pullRequest.head.sha !== headSha) {
    throw new Error('The supplied SHA is stale; record evidence for the current head.');
  }
  return pullRequest;
}

async function createEvidenceCheck({
  github,
  context,
  name,
  headSha,
  conclusion,
  externalId,
  title,
  summary,
}) {
  const serverUrl = context.serverUrl ||
    process.env.GITHUB_SERVER_URL ||
    'https://github.com';
  const detailsUrl = `${serverUrl}/${context.repo.owner}/${context.repo.repo}` +
    `/actions/runs/${context.runId}`;
  return github.rest.checks.create({
    ...context.repo,
    name,
    head_sha: headSha,
    status: 'completed',
    conclusion,
    completed_at: new Date().toISOString(),
    external_id: externalId,
    details_url: detailsUrl,
    output: { title, summary },
  });
}

function writeBindingArtifact({
  artifactDirectory = DEFAULT_ARTIFACT_DIRECTORY,
  artifactName,
  binding,
}) {
  fs.mkdirSync(artifactDirectory, { recursive: true });
  const artifactPath = path.join(artifactDirectory, `${artifactName}.json`);
  fs.writeFileSync(artifactPath, `${JSON.stringify(binding, null, 2)}\n`, {
    encoding: 'utf8',
    flag: 'wx',
  });
  return artifactPath;
}

async function recordReviewEvidence({
  github,
  context,
  core,
  artifactDirectory,
}) {
  const { inputs, pullNumber, headSha } = normalizedInputs(context);
  const domain = String(inputs.domain || '').trim().toLowerCase();
  const verdict = String(inputs.verdict || '').trim().toLowerCase();
  const reviewer = String(inputs.reviewer || '').trim();
  const summary = String(inputs.summary || '').trim();

  if (!REVIEW_DOMAINS.includes(domain)) {
    throw new Error('Unknown independent-review domain.');
  }
  if (!new Set(['approved', 'rejected']).has(verdict)) {
    throw new Error('Review verdict must be approved or rejected.');
  }
  if (!reviewer || reviewer.length > 100) {
    throw new Error('A bounded independent reviewer identity is required.');
  }
  if (!summary || summary.length > 2000) {
    throw new Error('A bounded review summary is required.');
  }
  const evidenceUrl = repositoryEvidenceUrl(inputs.evidence_url, context);
  const pullRequest = await currentPullRequest({
    github,
    context,
    pullNumber,
    headSha,
  });
  if (reviewer.toLowerCase() === pullRequest.user.login.toLowerCase()) {
    throw new Error('The pull-request author cannot be the independent reviewer.');
  }

  const conclusion = verdict === 'approved' ? 'success' : 'failure';
  const artifactName = evidenceArtifactName(domain, headSha, verdict);
  const checkSummary = [
    `- Pull request: #${pullNumber}`,
    `- Head SHA: \`${headSha}\``,
    `- Domain: ${domain}`,
    `- Verdict: ${verdict}`,
    `- Independent reviewer: ${reviewer}`,
    `- Recorded by workflow actor: ${context.actor}`,
    `- Evidence: ${evidenceUrl}`,
    '',
    summary,
  ].join('\n');
  await createEvidenceCheck({
    github,
    context,
    name: evidenceCheckName(domain),
    headSha,
    conclusion,
    externalId: `${EVIDENCE_PREFIX}${domain}:${headSha}:${context.runId}`,
    title: `${domain} review ${verdict}`,
    summary: checkSummary,
  });
  writeBindingArtifact({
    artifactDirectory,
    artifactName,
    binding: {
      schemaVersion: 1,
      kind: 'independent-review',
      pullNumber,
      headSha,
      domain,
      verdict,
      reviewer,
      recordedBy: context.actor,
      evidenceUrl,
      workflowRunId: context.runId,
    },
  });
  core.setOutput('artifact-name', artifactName);
  core.setOutput('artifact-path', path.join(
    artifactDirectory || DEFAULT_ARTIFACT_DIRECTORY,
    `${artifactName}.json`));
  core.info(`Recorded ${domain} review evidence for PR #${pullNumber} at ${headSha}.`);
}

async function recordBreakGlass({
  github,
  context,
  core,
  artifactDirectory,
}) {
  const { inputs, pullNumber, headSha } = normalizedInputs(context);
  const confirmation = String(inputs.confirmation || '');
  const reason = String(inputs.reason || '').trim();
  if (confirmation !== BREAK_GLASS_CONFIRMATION) {
    throw new Error('The exact break-glass confirmation phrase is required.');
  }
  if (reason.length < 20 || reason.length > 2000) {
    throw new Error('A specific bounded break-glass reason is required.');
  }
  if (String(context.actor).endsWith('[bot]')) {
    throw new Error('Break-glass requires an explicit human workflow dispatch.');
  }
  const incidentUrl = repositoryEvidenceUrl(inputs.incident_url, context);
  await currentPullRequest({
    github,
    context,
    pullNumber,
    headSha,
  });
  const permission = await github.rest.repos.getCollaboratorPermissionLevel({
    ...context.repo,
    username: context.actor,
  });
  if (!new Set(['admin', 'maintain']).has(permission.data.permission)) {
    throw new Error('Break-glass requires maintain or admin repository permission.');
  }

  const checkSummary = [
    `- Pull request: #${pullNumber}`,
    `- Head SHA: \`${headSha}\``,
    `- Human actor: ${context.actor}`,
    `- Durable incident/decision record: ${incidentUrl}`,
    '',
    reason,
    '',
    'This record does not bypass draft state, unresolved review threads, or any other required check.',
  ].join('\n');
  await createEvidenceCheck({
    github,
    context,
    name: CHECK_NAMES.breakGlass,
    headSha,
    conclusion: 'success',
    externalId: `${BREAK_GLASS_PREFIX}${headSha}:${context.runId}`,
    title: 'Current-head break-glass decision recorded',
    summary: checkSummary,
  });
  const artifactName = breakGlassArtifactName(headSha);
  writeBindingArtifact({
    artifactDirectory,
    artifactName,
    binding: {
      schemaVersion: 1,
      kind: 'review-break-glass',
      pullNumber,
      headSha,
      actor: context.actor,
      incidentUrl,
      workflowRunId: context.runId,
    },
  });
  core.setOutput('artifact-name', artifactName);
  core.setOutput('artifact-path', path.join(
    artifactDirectory || DEFAULT_ARTIFACT_DIRECTORY,
    `${artifactName}.json`));
  core.warning(`Break-glass recorded for PR #${pullNumber} at ${headSha}.`);
}

module.exports = {
  BREAK_GLASS_CONFIRMATION,
  createEvidenceCheck,
  currentPullRequest,
  normalizedInputs,
  recordBreakGlass,
  recordReviewEvidence,
  repositoryEvidenceUrl,
  writeBindingArtifact,
};
