'use strict';

const fs = require('node:fs');
const path = require('node:path');
const { execFileSync } = require('node:child_process');
const { createHash } = require('node:crypto');
const { ALL_DOMAINS, classifyFiles, loadPolicy } = require('./change-classifier');

function argument(name, fallback) {
  const index = process.argv.indexOf(`--${name}`);
  return index >= 0 ? process.argv[index + 1] : fallback;
}

function ghJson(endpoint) {
  return JSON.parse(
    execFileSync('gh', ['api', endpoint], {
      encoding: 'utf8',
      maxBuffer: 16 * 1024 * 1024,
    }),
  );
}

function baseContent(repository, sha, filename) {
  try {
    return execFileSync('git', ['show', `${sha}:${filename}`], {
      encoding: 'utf8',
      maxBuffer: 16 * 1024 * 1024,
      stdio: ['ignore', 'pipe', 'ignore'],
    });
  } catch {
    try {
      const encodedPath = filename.split('/').map(encodeURIComponent).join('/');
      const body = ghJson(
        `repos/${repository}/contents/${encodedPath}?ref=${encodeURIComponent(sha)}`,
      );
      if (body.type !== 'file' || body.encoding !== 'base64') return undefined;
      return Buffer.from(body.content.replace(/\s/g, ''), 'base64').toString('utf8');
    } catch {
      return undefined;
    }
  }
}

function pullFiles(repository, pull, mergeBaseSha) {
  const files = [];
  for (let page = 1; page <= 30; page += 1) {
    const response = ghJson(
      `repos/${repository}/pulls/${pull.number}/files?per_page=100&page=${page}`,
    );
    files.push(...response);
    if (response.length < 100) break;
  }
  return files.map((file) => {
    if (file.status === 'added' || !/\.md$/i.test(file.filename)) return file;
    const content = baseContent(
      repository,
      mergeBaseSha,
      file.previous_filename ?? file.filename,
    );
    return content === undefined ? file : { ...file, baseContent: content };
  });
}

function replayForcedFullReasons(pull, files, mergeBaseSha) {
  const reasons =
    Number.isInteger(pull.changed_files) && pull.changed_files === files.length
      ? []
      : ['replay-pull-request-file-count-mismatch'];
  if (files.length >= 3000) reasons.push('pull-request-file-limit');
  if (!mergeBaseSha) reasons.push('replay-merge-base-unavailable');
  return reasons;
}

function classificationName(decision) {
  if (decision.fullSuite) return 'full';
  const selected = ALL_DOMAINS.filter((domain) => decision.domains[domain].selected);
  if (selected.length === 1 && selected[0] === 'docs') return 'docs-only';
  return 'partial';
}

function estimatedMinutes(decision) {
  const roundedDomainMinutes = {
    docs: 1,
    dotnet: 3,
    postgres: 3,
    powershell: 1,
    javascript: 1,
    oci: 5,
  };
  if (decision.fullSuite) return 16;
  return (
    2 +
    ALL_DOMAINS.filter((domain) => decision.domains[domain].selected).reduce(
      (sum, domain) => sum + roundedDomainMinutes[domain],
      0,
    )
  );
}

function main() {
  const repository = argument('repository', 'Jamula/Andreja');
  const limit = Number(argument('limit', '20'));
  const output = argument(
    'output',
    path.join(__dirname, 'evidence', 'recent-merged-pr-replay.json'),
  );
  if (!Number.isSafeInteger(limit) || limit < 1 || limit > 100) {
    throw new Error('limit must be an integer from 1 through 100.');
  }

  const policyPath = path.join(__dirname, 'change-policy.v1.json');
  const policyBytes = fs.readFileSync(policyPath, 'utf8').replace(/\r\n/g, '\n');
  const classifierBytes = fs
    .readFileSync(path.join(__dirname, 'change-classifier.js'), 'utf8')
    .replace(/\r\n/g, '\n');
  const policy = loadPolicy(policyPath);
  const closed = ghJson(
    `repos/${repository}/pulls?state=closed&sort=updated&direction=desc&per_page=100`,
  );
  const candidates = closed.filter((pull) => pull.merged_at).slice(0, limit);
  if (candidates.length !== limit) {
    throw new Error(`Only ${candidates.length} merged pull requests found.`);
  }
  const pulls = candidates.map((pull) =>
    ghJson(`repos/${repository}/pulls/${pull.number}`),
  );

  const rows = pulls.map((pull) => {
    const compare = ghJson(
      `repos/${repository}/compare/${pull.base.sha}...${pull.head.sha}?per_page=1`,
    );
    const mergeBaseSha = compare.merge_base_commit?.sha;
    const files = pullFiles(repository, pull, mergeBaseSha);
    const forcedFullReasons = replayForcedFullReasons(pull, files, mergeBaseSha);
    const decision = classifyFiles(files, policy, {
      eventName: 'historical-replay',
      trustedPolicySha: 'policy-at-generation-commit',
      forcedFullReasons,
    });
    const selectedDomains = ALL_DOMAINS.filter((domain) => decision.domains[domain].selected);
    return {
      number: pull.number,
      mergedAt: pull.merged_at,
      baseSha: pull.base.sha,
      headSha: pull.head.sha,
      mergeBaseSha: mergeBaseSha ?? null,
      changedFileCount: files.length,
      classification: classificationName(decision),
      fullSuite: decision.fullSuite,
      selectedDomains,
      fullReasons: decision.fullReasons,
      estimatedSelectiveRoundedMinutes: estimatedMinutes(decision),
    };
  });

  const counts = Object.fromEntries(
    ['docs-only', 'partial', 'full'].map((name) => [
      name,
      rows.filter((row) => row.classification === name).length,
    ]),
  );
  const baselineMinutes = rows.length * 16;
  const estimatedMinutesTotal = rows.reduce(
    (sum, row) => sum + row.estimatedSelectiveRoundedMinutes,
    0,
  );
  const evidence = {
    schemaVersion: 1,
    generatedAt: new Date().toISOString(),
    repository,
    policyVersion: policy.schemaVersion,
    policySha256: createHash('sha256').update(policyBytes).digest('hex'),
    classifierSha256: createHash('sha256').update(classifierBytes).digest('hex'),
    sample: {
      method: 'most recently updated merged pull requests returned by GitHub REST',
      count: rows.length,
      newestMergedAt: rows.map((row) => row.mergedAt).sort().at(-1),
      oldestMergedAt: rows.map((row) => row.mergedAt).sort()[0],
    },
    assumptions: {
      normalizedCurrentFullSuiteRoundedMinutesPerPullRequest: 16,
      normalizedCurrentFullSuiteUsdPerPullRequest: 0.128,
      selectiveFixedClassificationAndAggregateRoundedMinutes: 2,
      selectedDomainRoundedMinutes: {
        docs: 1,
        dotnet: 3,
        postgres: 3,
        powershell: 1,
        javascript: 1,
        oci: 5,
      },
      note: 'Planning estimate from baseline and live bootstrap run; not an invoice or live selective result.',
    },
    portfolio: {
      counts,
      shares: Object.fromEntries(
        Object.entries(counts).map(([name, count]) => [name, Number((count / rows.length).toFixed(4))]),
      ),
      normalizedBaselineRoundedMinutes: baselineMinutes,
      estimatedSelectiveRoundedMinutes: estimatedMinutesTotal,
      expectedRoundedMinutesSaved: baselineMinutes - estimatedMinutesTotal,
      expectedSavingsShare: Number(
        ((baselineMinutes - estimatedMinutesTotal) / baselineMinutes).toFixed(4),
      ),
      expectedListRateUsdSaved: Number(
        ((baselineMinutes - estimatedMinutesTotal) * 0.008).toFixed(3),
      ),
    },
    pullRequests: rows,
  };

  fs.mkdirSync(path.dirname(output), { recursive: true });
  fs.writeFileSync(output, `${JSON.stringify(evidence, null, 2)}\n`);
  process.stdout.write(`${JSON.stringify(evidence.portfolio)}\n`);
}

if (require.main === module) {
  main();
}

module.exports = { classificationName, estimatedMinutes, replayForcedFullReasons };
