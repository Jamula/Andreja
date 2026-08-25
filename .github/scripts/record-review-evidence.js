'use strict';

const fs = require('node:fs');
const path = require('node:path');

const {
  BREAK_GLASS_KIND,
  BREAK_GLASS_SCHEMA_VERSION,
  breakGlassArtifactName,
  pullIdentity,
  samePullIdentity,
} = require('./review-completion');

const BREAK_GLASS_CONFIRMATION = 'BREAK GLASS REVIEW GATE';
const DEFAULT_ARTIFACT_DIRECTORY = path.join(
  process.cwd(),
  'artifacts',
  'review-break-glass');

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

function normalizedBreakGlassInputs(context) {
  const inputs = context.payload.inputs || {};
  const identity = {
    pullNumber: Number(inputs.pr_number),
    headSha: String(inputs.head_sha || '').trim(),
    baseRepositoryId: Number(inputs.base_repository_id),
    baseRepository: String(inputs.base_repository || '').trim(),
    baseRef: String(inputs.base_ref || '').trim(),
    baseSha: String(inputs.base_sha || '').trim(),
  };
  if (!Number.isInteger(identity.pullNumber) || identity.pullNumber <= 0 ||
      !/^[0-9a-f]{40}$/.test(identity.headSha) ||
      !Number.isInteger(identity.baseRepositoryId) ||
      identity.baseRepositoryId <= 0 ||
      !identity.baseRepository ||
      identity.baseRepository.length > 200 ||
      !identity.baseRef ||
      identity.baseRef.length > 255 ||
      !/^[0-9a-f]{40}$/.test(identity.baseSha)) {
    throw new Error('A complete exact pull-request diff identity is required.');
  }
  return { inputs, identity };
}

async function currentPullRequest({ github, context, identity }) {
  const response = await github.rest.pulls.get({
    ...context.repo,
    pull_number: identity.pullNumber,
  });
  const pullRequest = response.data;
  if (pullRequest.state !== 'open') {
    throw new Error('Break-glass can be recorded only for an open pull request.');
  }
  if (!samePullIdentity(pullIdentity(pullRequest), identity)) {
    throw new Error('The supplied pull-request diff identity is stale or incorrect.');
  }
  return pullRequest;
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

async function recordBreakGlass({
  github,
  context,
  core,
  artifactDirectory,
}) {
  const { inputs, identity } = normalizedBreakGlassInputs(context);
  const confirmation = String(inputs.confirmation || '');
  const reason = String(inputs.reason || '').trim();
  if (confirmation !== BREAK_GLASS_CONFIRMATION) {
    throw new Error('The exact break-glass confirmation phrase is required.');
  }
  if (reason.length < 20 || reason.length > 2000) {
    throw new Error('A specific bounded break-glass reason is required.');
  }
  if (!context.actor || String(context.actor).endsWith('[bot]')) {
    throw new Error('Break-glass requires an explicit authenticated human dispatch.');
  }
  const incidentUrl = repositoryEvidenceUrl(inputs.incident_url, context);
  await currentPullRequest({ github, context, identity });
  const permission = await github.rest.repos.getCollaboratorPermissionLevel({
    ...context.repo,
    username: context.actor,
  });
  if (!new Set(['admin', 'maintain']).has(permission.data.permission)) {
    throw new Error('Break-glass requires maintain or admin repository permission.');
  }

  const artifactName = breakGlassArtifactName(identity);
  const artifactPath = writeBindingArtifact({
    artifactDirectory,
    artifactName,
    binding: {
      schemaVersion: BREAK_GLASS_SCHEMA_VERSION,
      kind: BREAK_GLASS_KIND,
      ...identity,
      actor: context.actor,
      incidentUrl,
      reason,
      workflowRunId: Number(context.runId),
    },
  });
  core.setOutput('artifact-name', artifactName);
  core.setOutput('artifact-path', artifactPath);
  core.warning(
    `Break-glass recorded for PR #${identity.pullNumber} at ${identity.headSha} ` +
    `against ${identity.baseRepository}:${identity.baseRef}@${identity.baseSha}.`);
}

module.exports = {
  BREAK_GLASS_CONFIRMATION,
  currentPullRequest,
  normalizedBreakGlassInputs,
  recordBreakGlass,
  repositoryEvidenceUrl,
  writeBindingArtifact,
};
