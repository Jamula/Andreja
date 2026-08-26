'use strict';

const fs = require('node:fs');
const path = require('node:path');

const ALL_DOMAINS = ['docs', 'dotnet', 'postgres', 'powershell', 'javascript', 'oci'];
const AMBIGUOUS_STATUSES = new Set(['removed', 'renamed']);
const MARKDOWN_FENCE = /^\s*(`{3,}|~{3,})(.*)$/;

function loadPolicy(policyPath) {
  const policy = JSON.parse(fs.readFileSync(policyPath, 'utf8'));
  if (policy.schemaVersion !== 1 || !Array.isArray(policy.rules)) {
    throw new Error('Unsupported or invalid change-classification policy.');
  }

  return {
    ...policy,
    rules: policy.rules.map((rule) => ({ ...rule, expression: new RegExp(rule.pattern, 'i') })),
  };
}

function normalizeFile(file) {
  const filename = String(file.filename ?? '');
  if (
    !filename ||
    filename.includes('\\') ||
    filename.includes('\0') ||
    filename.startsWith('/') ||
    filename.split('/').includes('..')
  ) {
    return { ...file, filename, invalid: true };
  }
  return { ...file, filename, status: String(file.status ?? 'unknown').toLowerCase() };
}

function fenceDelimiter(line) {
  const match = String(line).match(MARKDOWN_FENCE);
  if (!match) return null;
  return { marker: match[1][0], length: match[1].length, suffix: match[2].trim() };
}

function transitionFence(state, line) {
  const delimiter = fenceDelimiter(line);
  if (!delimiter) return state;
  if (!state) return { marker: delimiter.marker, length: delimiter.length };
  if (
    delimiter.marker === state.marker &&
    delimiter.length >= state.length &&
    delimiter.suffix === ''
  ) {
    return null;
  }
  return state;
}

function markdownFenceStates(baseContent) {
  const lines = String(baseContent).split('\n');
  const before = new Array(lines.length + 2).fill(false);
  const after = new Array(lines.length + 2).fill(false);
  let state = null;
  for (let index = 0; index < lines.length; index += 1) {
    const lineNumber = index + 1;
    before[lineNumber] = state !== null;
    state = transitionFence(state, lines[index].replace(/\r$/, ''));
    after[lineNumber] = state !== null;
  }
  before[lines.length + 1] = state !== null;
  after[lines.length + 1] = state !== null;
  return { before, after, lines, lineCount: lines.length };
}

function inspectMarkdownPatch(file) {
  const patch = file.patch;
  if (typeof patch !== 'string') return { uncertain: 'markdown-patch-unavailable' };

  const changedLineCount = patch
    .split('\n')
    .filter((line) => line.startsWith('+') || line.startsWith('-')).length;
  if (Number.isInteger(file.changes) && file.changes !== changedLineCount) {
    return { uncertain: 'markdown-patch-change-count-mismatch', changedLineCount };
  }

  const isAdded = file.status === 'added';
  if (!isAdded && typeof file.baseContent !== 'string') {
    return { uncertain: 'markdown-base-unavailable', changedLineCount };
  }

  const base = markdownFenceStates(file.baseContent ?? '');
  let oldLine = 0;
  let state = null;
  let sawHunk = false;
  let hunkAligned = isAdded;
  let executable = false;

  for (const patchLine of patch.split('\n')) {
    const hunk = patchLine.match(/^@@ -(\d+)(?:,\d+)? \+\d+(?:,\d+)? @@/);
    if (hunk) {
      if (sawHunk && !hunkAligned) {
        return { uncertain: 'markdown-hunk-base-unaligned', changedLineCount };
      }
      oldLine = Number(hunk[1]);
      if (!Number.isSafeInteger(oldLine) || oldLine < 0) {
        return { uncertain: 'markdown-hunk-invalid', changedLineCount };
      }
      state = oldLine === 0 ? null : base.before[oldLine] ? { marker: '?', length: 0 } : null;
      sawHunk = true;
      hunkAligned = isAdded;
      continue;
    }
    if (!sawHunk || patchLine === '\\ No newline at end of file') continue;

    const prefix = patchLine[0];
    const content = patchLine.slice(1).replace(/\r$/, '');
    if (prefix === ' ') {
      if (!isAdded && (oldLine < 1 || oldLine > base.lineCount + 1)) {
        return { uncertain: 'markdown-hunk-out-of-range', changedLineCount };
      }
      if (!isAdded && base.lines[oldLine - 1]?.replace(/\r$/, '') !== content) {
        return { uncertain: 'markdown-base-patch-mismatch', changedLineCount };
      }
      hunkAligned = true;
      state = !isAdded && base.after[oldLine] ? { marker: '?', length: 0 } : null;
      oldLine += 1;
      continue;
    }
    if (prefix === '-') {
      if (!isAdded && base.lines[oldLine - 1]?.replace(/\r$/, '') !== content) {
        return { uncertain: 'markdown-base-patch-mismatch', changedLineCount };
      }
      hunkAligned = true;
      if (fenceDelimiter(content) || (!isAdded && base.before[oldLine])) {
        executable = true;
      }
      oldLine += 1;
      continue;
    }
    if (prefix === '+') {
      if (state || fenceDelimiter(content)) {
        executable = true;
      }
      state = transitionFence(state, content);
      continue;
    }
    return { uncertain: 'markdown-hunk-line-invalid', changedLineCount };
  }

  if (!sawHunk) return { uncertain: 'markdown-hunk-unavailable', changedLineCount };
  if (!hunkAligned) return { uncertain: 'markdown-hunk-base-unaligned', changedLineCount };
  return { executable, changedLineCount };
}

async function attachTrustedMarkdownBase(
  files,
  repository,
  trustedContentSha,
  token,
  fetchImpl = fetch,
) {
  return Promise.all(files.map(async (rawFile) => {
    const file = normalizeFile(rawFile);
    if (
      file.invalid ||
      !/\.md$/i.test(file.filename) ||
      file.status === 'added' ||
      typeof file.baseContent === 'string'
    ) {
      return rawFile;
    }

    if (!trustedContentSha) return rawFile;
    const sourcePath = file.previous_filename ?? file.filename;
    const encodedPath = sourcePath.split('/').map(encodeURIComponent).join('/');
    try {
      const { body } = await getJson(
        `${process.env.GITHUB_API_URL || 'https://api.github.com'}/repos/${repository}/contents/${encodedPath}?ref=${encodeURIComponent(trustedContentSha)}`,
        token,
        fetchImpl,
      );
      if (
        body.type !== 'file' ||
        body.encoding !== 'base64' ||
        typeof body.content !== 'string'
      ) {
        return rawFile;
      }
      return {
        ...rawFile,
        baseContent: Buffer.from(body.content.replace(/\s/g, ''), 'base64').toString('utf8'),
      };
    } catch {
      return rawFile;
    }
  }));
}

function classifyFiles(files, policy, options = {}) {
  const fullReasons = [...(options.forcedFullReasons ?? [])];
  const classified = [];
  const selected = new Set();

  for (const rawFile of files) {
    const file = normalizeFile(rawFile);
    const match = file.invalid ? undefined : policy.rules.find((rule) => rule.expression.test(file.filename));
    const reasons = [];
    let domains = [];

    if (file.invalid) {
      reasons.push('invalid-path');
    } else if (!match) {
      reasons.push('unclassified-path');
    } else if (match.fullSuite) {
      reasons.push(match.id);
    } else {
      domains = [...match.domains];
    }

    if (AMBIGUOUS_STATUSES.has(file.status)) {
      reasons.push(`ambiguous-${file.status}`);
    } else if (!['added', 'modified', 'changed', 'copied', 'unchanged'].includes(file.status)) {
      reasons.push(`unknown-status-${file.status}`);
    }

    if (match?.inspectMarkdownPatch && /\.md$/i.test(file.filename)) {
      const inspection = inspectMarkdownPatch(file);
      if (inspection.uncertain) {
        reasons.push(inspection.uncertain);
      } else if (inspection.executable) {
        reasons.push('executable-documentation');
      }
    }

    if (reasons.length > 0) {
      fullReasons.push(...reasons.map((reason) => `${file.filename}:${reason}`));
      domains = [...ALL_DOMAINS];
    }

    for (const domain of domains) {
      selected.add(domain);
    }

    classified.push({
      path: file.filename,
      status: file.status,
      rule: match?.id ?? 'unclassified',
      domains,
      reasons,
    });
  }

  if (files.length === 0 && fullReasons.length === 0) {
    fullReasons.push('empty-change-set');
  }

  const fullSuite = fullReasons.length > 0;
  if (fullSuite) {
    ALL_DOMAINS.forEach((domain) => selected.add(domain));
  }

  const domainDecisions = Object.fromEntries(
    ALL_DOMAINS.map((domain) => [
      domain,
      {
        selected: selected.has(domain),
        disposition: selected.has(domain) ? 'selected' : 'not-applicable',
        reason: fullSuite
          ? 'fail-closed-full-suite'
          : classified.some((file) => file.domains.includes(domain))
            ? 'affected-input'
            : 'no-affected-input',
      },
    ]),
  );

  return {
    schemaVersion: 1,
    policyVersion: policy.schemaVersion,
    eventName: options.eventName ?? 'fixture',
    trustedPolicySha: options.trustedPolicySha ?? null,
    fullSuite,
    fullReasons: [...new Set(fullReasons)].sort(),
    changedFileCount: classified.length,
    files: classified,
    domains: domainDecisions,
  };
}

function parseNext(link) {
  if (!link) return null;
  for (const item of link.split(',')) {
    const match = item.match(/<([^>]+)>;\s*rel="next"/);
    if (match) return match[1];
  }
  return null;
}

async function getJson(url, token, fetchImpl) {
  const response = await fetchImpl(url, {
    headers: {
      Accept: 'application/vnd.github+json',
      Authorization: `Bearer ${token}`,
      'X-GitHub-Api-Version': '2022-11-28',
      'User-Agent': 'andreja-trusted-change-classifier',
    },
  });
  if (!response.ok) {
    throw new Error(`GitHub metadata request failed: ${response.status} ${response.statusText}`);
  }
  const link = response.headers.get('link');
  return { body: await response.json(), next: parseNext(link), link };
}

async function paginatedFiles(initialUrl, token, fetchImpl = fetch) {
  const files = [];
  let url = initialUrl;
  let pageCount = 0;
  while (url) {
    pageCount += 1;
    if (pageCount > 100) throw new Error('GitHub metadata pagination exceeded 100 pages.');
    const { body, next } = await getJson(url, token, fetchImpl);
    const pageFiles = Array.isArray(body) ? body : body.files;
    if (!Array.isArray(pageFiles)) throw new Error('GitHub metadata response omitted files.');
    files.push(...pageFiles);
    url = next;
  }
  return files;
}

async function acquireChanges(eventName, event, repository, token, fetchImpl = fetch) {
  const api = process.env.GITHUB_API_URL || 'https://api.github.com';
  if (eventName === 'pull_request') {
    const number = event.pull_request?.number ?? event.number;
    if (!number) throw new Error('Pull request event omitted its number.');
    const files = await paginatedFiles(
      `${api}/repos/${repository}/pulls/${number}/files?per_page=100`,
      token,
      fetchImpl,
    );
    const expectedCount = event.pull_request?.changed_files;
    const reasons =
      Number.isInteger(expectedCount) && expectedCount === files.length
        ? []
        : ['pull-request-file-count-mismatch'];
    if (files.length >= 3000) reasons.push('pull-request-file-limit');
    if (
      files.filter(
        (file) => file.status !== 'added' && /\.md$/i.test(String(file.filename ?? '')),
      ).length > 100
    ) {
      reasons.push('markdown-base-fetch-limit');
    }
    const base = event.pull_request?.base?.sha;
    const head = event.pull_request?.head?.sha;
    if (!base || !head) reasons.push('pull-request-compare-sha-unavailable');
    let trustedContentSha = null;
    if (base && head) {
      const { body } = await getJson(
        `${api}/repos/${repository}/compare/${encodeURIComponent(base)}...${encodeURIComponent(head)}?per_page=1`,
        token,
        fetchImpl,
      );
      trustedContentSha = body.merge_base_commit?.sha ?? null;
      if (!trustedContentSha) reasons.push('pull-request-merge-base-unavailable');
    }
    return { files, forcedFullReasons: reasons, trustedContentSha };
  }

  if (eventName === 'merge_group') {
    const base = event.merge_group?.base_sha;
    const head = event.merge_group?.head_sha;
    if (!base || !head) throw new Error('Merge group event omitted base or head SHA.');
    const { body, next, link } = await getJson(
      `${api}/repos/${repository}/compare/${encodeURIComponent(base)}...${encodeURIComponent(head)}?per_page=100`,
      token,
      fetchImpl,
    );
    if (!Array.isArray(body.files)) throw new Error('GitHub compare response omitted files.');
    const files = body.files;
    const reasons = [];
    if (files.length >= 300) reasons.push('merge-group-compare-file-limit');
    if (next || link) reasons.push('merge-group-compare-link-uncertainty');
    const trustedContentSha = body.merge_base_commit?.sha ?? null;
    if (!trustedContentSha) reasons.push('merge-group-merge-base-unavailable');
    return { files, forcedFullReasons: reasons, trustedContentSha };
  }

  if (eventName === 'push') {
    return { files: [], forcedFullReasons: ['push-main-full-safety-suite'] };
  }
  if (eventName === 'schedule' || eventName === 'workflow_dispatch') {
    return { files: [], forcedFullReasons: [`${eventName}-full-safety-suite`] };
  }
  return { files: [], forcedFullReasons: [`unsupported-event-${eventName}`] };
}

function writeOutputs(decision, outputPath) {
  const lines = [
    `full_suite=${decision.fullSuite}`,
    ...ALL_DOMAINS.map((domain) => `${domain}=${decision.domains[domain].selected}`),
  ];
  fs.appendFileSync(outputPath, `${lines.join('\n')}\n`);
}

async function main() {
  const eventName = process.env.GITHUB_EVENT_NAME;
  const event = JSON.parse(fs.readFileSync(process.env.GITHUB_EVENT_PATH, 'utf8'));
  const policyPath = process.env.CHANGE_POLICY_PATH || path.join(__dirname, 'change-policy.v1.json');
  const outputFile = process.env.CHANGE_DECISION_PATH || 'artifacts/selective-ci/classification.json';
  const policy = loadPolicy(policyPath);

  let changes;
  try {
    changes = await acquireChanges(
      eventName,
      event,
      process.env.GITHUB_REPOSITORY,
      process.env.GITHUB_TOKEN,
    );
  } catch (error) {
    changes = { files: [], forcedFullReasons: [`metadata-error:${error.message}`] };
  }

  const files =
    changes.forcedFullReasons.length > 0
      ? changes.files
      : await attachTrustedMarkdownBase(
          changes.files,
          process.env.GITHUB_REPOSITORY,
          changes.trustedContentSha,
          process.env.GITHUB_TOKEN,
        );
  const decision = classifyFiles(files, policy, {
    eventName,
    trustedPolicySha: process.env.TRUSTED_POLICY_SHA,
    forcedFullReasons: changes.forcedFullReasons,
  });
  fs.mkdirSync(path.dirname(outputFile), { recursive: true });
  fs.writeFileSync(outputFile, `${JSON.stringify(decision, null, 2)}\n`);
  if (process.env.GITHUB_OUTPUT) writeOutputs(decision, process.env.GITHUB_OUTPUT);
  process.stdout.write(`${JSON.stringify(decision)}\n`);
}

if (require.main === module) {
  main().catch((error) => {
    console.error(error);
    process.exitCode = 1;
  });
}

module.exports = {
  ALL_DOMAINS,
  acquireChanges,
  attachTrustedMarkdownBase,
  classifyFiles,
  inspectMarkdownPatch,
  loadPolicy,
  paginatedFiles,
  parseNext,
};
