'use strict';

const fs = require('node:fs');
const path = require('node:path');

const ALL_DOMAINS = ['docs', 'dotnet', 'postgres', 'powershell', 'javascript', 'oci'];
const AMBIGUOUS_STATUSES = new Set(['copied', 'removed', 'renamed']);
const MARKDOWN_FENCE = /^\s*(`{3,}|~{3,})(.*)$/;
const MARKDOWN_INDENTED_CODE = /^(?: {4}|\t).*\S/;
const SHADOW_SAMPLE_LABEL = 'ci:selective-shadow-sample';
const API_REQUEST_LIMIT = 132;
const MARKDOWN_FETCH_CONCURRENCY = 8;
const EXACT_SHA = /^[0-9a-f]{40}$/;

function loadPolicy(policyPath) {
  const policy = JSON.parse(fs.readFileSync(policyPath, 'utf8'));
  if (policy.schemaVersion !== 1 || !Array.isArray(policy.rules)) {
    throw new Error('Unsupported or invalid change-classification policy.');
  }

  return {
    ...policy,
    rules: policy.rules.map((rule) => ({
      ...rule,
      expression: new RegExp(rule.pattern, rule.caseSensitive ? '' : 'i'),
    })),
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

function markdownStructure(line) {
  let content = String(line);
  let quoteDepth = 0;
  while (true) {
    const prefix = content.match(/^ {0,3}>[ \t]?/);
    if (!prefix) break;
    quoteDepth += 1;
    content = content.slice(prefix[0].length);
  }
  return { content, quoteDepth };
}

function markdownStructuralContent(line) {
  return markdownStructure(line).content;
}

function fenceDelimiter(line) {
  const { content, quoteDepth } = markdownStructure(line);
  const match = content.match(MARKDOWN_FENCE);
  if (!match) return null;
  return {
    marker: match[1][0],
    length: match[1].length,
    suffix: match[2].trim(),
    quoteDepth,
  };
}

function transitionFence(state, line) {
  const delimiter = fenceDelimiter(line);
  if (!delimiter) return state;
  if (!state) {
    return {
      marker: delimiter.marker,
      length: delimiter.length,
      quoteDepth: delimiter.quoteDepth,
    };
  }
  if (
    delimiter.marker === state.marker &&
    delimiter.length >= state.length &&
    delimiter.quoteDepth === state.quoteDepth &&
    delimiter.suffix === ''
  ) {
    return null;
  }
  return state;
}

function transitionIndentedCode(state, line) {
  const value = markdownStructuralContent(line);
  if (MARKDOWN_INDENTED_CODE.test(value)) return true;
  if (/^\s*$/.test(value)) return state;
  return false;
}

function transitionInlineCode(state, line) {
  const value = String(line);
  let delimiterLength = state;
  let sawDelimiter = false;
  for (let index = 0; index < value.length;) {
    if (value[index] !== '`') {
      index += 1;
      continue;
    }
    let end = index + 1;
    while (value[end] === '`') end += 1;
    let precedingBackslashes = 0;
    for (let cursor = index - 1; cursor >= 0 && value[cursor] === '\\'; cursor -= 1) {
      precedingBackslashes += 1;
    }
    const escaped = delimiterLength === null && precedingBackslashes % 2 === 1;
    if (!escaped) {
      const runLength = end - index;
      sawDelimiter = true;
      if (delimiterLength === null) {
        delimiterLength = runLength;
      } else if (runLength === delimiterLength) {
        delimiterLength = null;
      }
    }
    index = end;
  }
  return { state: delimiterLength, sawDelimiter };
}

function markdownFenceStates(baseContent) {
  const lines = String(baseContent).split('\n');
  const before = new Array(lines.length + 2).fill(false);
  const after = new Array(lines.length + 2).fill(false);
  const indentedBefore = new Array(lines.length + 2).fill(false);
  const indentedAfter = new Array(lines.length + 2).fill(false);
  const inlineBefore = new Array(lines.length + 2).fill(null);
  const inlineAfter = new Array(lines.length + 2).fill(null);
  let fenceState = null;
  let indentedState = false;
  let inlineState = null;
  for (let index = 0; index < lines.length; index += 1) {
    const lineNumber = index + 1;
    const line = lines[index].replace(/\r$/, '');
    before[lineNumber] = fenceState !== null;
    indentedBefore[lineNumber] = indentedState;
    inlineBefore[lineNumber] = inlineState;
    const delimiter = fenceDelimiter(line);
    const scanInline =
      inlineState !== null ||
      (fenceState === null &&
        delimiter === null &&
        !MARKDOWN_INDENTED_CODE.test(markdownStructuralContent(line)));
    if (scanInline) inlineState = transitionInlineCode(inlineState, line).state;
    fenceState = transitionFence(fenceState, line);
    indentedState = fenceState === null && transitionIndentedCode(indentedState, line);
    after[lineNumber] = fenceState !== null;
    indentedAfter[lineNumber] = indentedState;
    inlineAfter[lineNumber] = inlineState;
  }
  before[lines.length + 1] = fenceState !== null;
  after[lines.length + 1] = fenceState !== null;
  indentedBefore[lines.length + 1] = indentedState;
  indentedAfter[lines.length + 1] = indentedState;
  inlineBefore[lines.length + 1] = inlineState;
  inlineAfter[lines.length + 1] = inlineState;
  return {
    before,
    after,
    indentedBefore,
    indentedAfter,
    inlineBefore,
    inlineAfter,
    lines,
    lineCount: lines.length,
  };
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
  if (
    patch
      .split('\n')
      .some((line) => (line.startsWith('+') || line.startsWith('-')) && line.slice(1).includes('\t'))
  ) {
    return { executable: true, changedLineCount };
  }
  if (!isAdded && typeof file.baseContent !== 'string') {
    return { uncertain: 'markdown-base-unavailable', changedLineCount };
  }
  if (!isAdded && file.baseContent.includes('\t')) {
    return { uncertain: 'markdown-tab-indentation-ambiguous', changedLineCount };
  }

  const base = markdownFenceStates(file.baseContent ?? '');
  let oldLine = 0;
  let fenceState = null;
  let indentedState = false;
  let inlineState = null;
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
      fenceState =
        oldLine === 0 ? null : base.before[oldLine] ? { marker: '?', length: 0 } : null;
      indentedState = oldLine === 0 ? false : base.indentedBefore[oldLine];
      inlineState = oldLine === 0 ? null : base.inlineBefore[oldLine];
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
      fenceState = !isAdded && base.after[oldLine] ? { marker: '?', length: 0 } : null;
      indentedState = !isAdded && base.indentedAfter[oldLine];
      inlineState = !isAdded ? base.inlineAfter[oldLine] : null;
      oldLine += 1;
      continue;
    }
    if (prefix === '-') {
      if (!isAdded && base.lines[oldLine - 1]?.replace(/\r$/, '') !== content) {
        return { uncertain: 'markdown-base-patch-mismatch', changedLineCount };
      }
      hunkAligned = true;
      if (
        fenceDelimiter(content) ||
        MARKDOWN_INDENTED_CODE.test(markdownStructuralContent(content)) ||
        transitionInlineCode(null, content).sawDelimiter ||
        (!isAdded &&
          (base.before[oldLine] ||
            base.indentedBefore[oldLine] ||
            base.indentedAfter[oldLine] ||
            base.inlineBefore[oldLine] !== null ||
            base.inlineAfter[oldLine] !== null))
      ) {
        executable = true;
      }
      fenceState =
        !isAdded && base.before[oldLine] ? { marker: '?', length: 0 } : fenceState;
      indentedState = !isAdded && base.indentedBefore[oldLine];
      inlineState = !isAdded ? base.inlineBefore[oldLine] : inlineState;
      oldLine += 1;
      continue;
    }
    if (prefix === '+') {
      const inlineTransition = transitionInlineCode(inlineState, content);
      if (
        fenceState ||
        indentedState ||
        inlineState !== null ||
        fenceDelimiter(content) ||
        MARKDOWN_INDENTED_CODE.test(markdownStructuralContent(content)) ||
        inlineTransition.sawDelimiter
      ) {
        executable = true;
      }
      fenceState = transitionFence(fenceState, content);
      indentedState =
        fenceState === null && transitionIndentedCode(indentedState, content);
      inlineState = inlineTransition.state;
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
  concurrency = MARKDOWN_FETCH_CONCURRENCY,
) {
  if (!Number.isSafeInteger(concurrency) || concurrency < 1) {
    throw new Error('Markdown fetch concurrency must be a positive integer.');
  }
  const results = new Array(files.length);
  let cursor = 0;
  let terminalError = null;
  async function worker() {
    while (cursor < files.length && terminalError === null) {
      const index = cursor;
      cursor += 1;
      const rawFile = files[index];
      const file = normalizeFile(rawFile);
      if (
        file.invalid ||
        !/\.md$/i.test(file.filename) ||
        file.status === 'added' ||
        typeof file.baseContent === 'string' ||
        !trustedContentSha
      ) {
        results[index] = rawFile;
        continue;
      }

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
          throw new Error('GitHub Contents metadata response was not a base64 file.');
        }
        results[index] = {
          ...rawFile,
          baseContent: Buffer.from(
            body.content.replace(/\s/g, ''),
            'base64',
          ).toString('utf8'),
        };
      } catch (error) {
        terminalError ??= error;
        break;
      }
    }
  }
  await Promise.all(
    Array.from(
      { length: Math.min(files.length, concurrency) },
      () => worker(),
    ),
  );
  if (terminalError) throw terminalError;
  return results;
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
    trustedClassifierAvailable: options.trustedClassifierAvailable ?? true,
    apiRequestBudget: options.apiRequestBudget ?? null,
    classificationFailure: options.classificationFailure ?? null,
    mergeCommitProof: options.mergeCommitProof ?? null,
    shadowSample: {
      label: SHADOW_SAMPLE_LABEL,
      sampled: options.shadowSampled ?? true,
      reason: options.shadowSampleReason ?? 'fixture-or-non-pull-request',
      observedActor: options.shadowSampleObservedActor ?? null,
      expectedOperator: options.shadowSampleExpectedOperator ?? null,
    },
    fullSuite,
    fullReasons: [...new Set(fullReasons)].sort(),
    changedFileCount: classified.length,
    files: classified,
    domains: domainDecisions,
  };
}

function shadowSample(eventName, event, expectedOperator) {
  const observedActor =
    typeof event.sender?.login === 'string' && event.sender.login.length > 0
      ? event.sender.login
      : null;
  const configuredOperator =
    typeof expectedOperator === 'string' && expectedOperator.length > 0
      ? expectedOperator
      : null;
  if (eventName !== 'pull_request' && eventName !== 'pull_request_target') {
    return {
      sampled: true,
      reason: 'non-pull-request-full-safety',
      observedActor,
      expectedOperator: configuredOperator,
    };
  }
  const labels = Array.isArray(event.pull_request?.labels) ? event.pull_request.labels : [];
  const labelPresent = labels.some((label) => label?.name === SHADOW_SAMPLE_LABEL);
  const sampleLabelEvent =
    event.action === 'labeled' &&
    event.label?.name === SHADOW_SAMPLE_LABEL &&
    labelPresent;
  const operatorMatches =
    observedActor !== null &&
    configuredOperator !== null &&
    observedActor === configuredOperator;
  const sampled = sampleLabelEvent && operatorMatches;
  const reason = sampled
    ? 'authorized-sample-label-event'
    : sampleLabelEvent && configuredOperator === null
      ? 'sample-operator-unconfigured'
      : sampleLabelEvent
        ? 'sample-operator-mismatch'
        : 'shadow-not-sampled';
  return {
    sampled,
    reason,
    observedActor,
    expectedOperator: configuredOperator,
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

function createBudgetedFetch(fetchImpl = fetch, limit = API_REQUEST_LIMIT) {
  let used = 0;
  let exhausted = false;
  let rateLimitResponseObserved = false;
  let minimumRemaining = null;
  let resetEpoch = null;
  const budgetedFetch = async (url, options) => {
    if (used >= limit) {
      exhausted = true;
      throw new Error(`GitHub metadata request budget exceeded ${limit} requests.`);
    }
    used += 1;
    const response = await fetchImpl(url, options);
    const rawRemaining = response.headers.get('x-ratelimit-remaining');
    const rawReset = response.headers.get('x-ratelimit-reset');
    const remaining = rawRemaining === null ? null : Number(rawRemaining);
    const reset = rawReset === null ? null : Number(rawReset);
    if (Number.isSafeInteger(remaining) && remaining >= 0) {
      minimumRemaining =
        minimumRemaining === null ? remaining : Math.min(minimumRemaining, remaining);
    }
    if (Number.isSafeInteger(reset) && reset > 0) resetEpoch = reset;
    if (response.status === 429 || (response.status === 403 && rawRemaining === '0')) {
      rateLimitResponseObserved = true;
    }
    return response;
  };
  budgetedFetch.observation = () => ({
    limit,
    used,
    exhausted,
    rateLimitResponseObserved,
    minimumRateLimitRemaining: minimumRemaining,
    rateLimitResetEpoch: resetEpoch,
  });
  return budgetedFetch;
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
    const remaining = response.headers.get('x-ratelimit-remaining');
    if (response.status === 429 || (response.status === 403 && remaining === '0')) {
      throw new Error(
        `GitHub metadata rate limit exhausted: ${response.status} ${response.statusText}`,
      );
    }
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

function isSampleLabelEvent(eventName, event) {
  if (eventName !== 'pull_request_target') return false;
  const labels = Array.isArray(event.pull_request?.labels) ? event.pull_request.labels : [];
  return (
    event.action === 'labeled' &&
    event.label?.name === SHADOW_SAMPLE_LABEL &&
    labels.some((label) => label?.name === SHADOW_SAMPLE_LABEL)
  );
}

function mergeIntegritySnapshot(eventPull, livePull, commit) {
  const eventBase = eventPull?.base?.sha;
  const eventHead = eventPull?.head?.sha;
  const eventMerge = eventPull?.merge_commit_sha;
  const liveBase = livePull?.base?.sha;
  const liveHead = livePull?.head?.sha;
  const liveMerge = livePull?.merge_commit_sha;
  const parentShas = Array.isArray(commit?.parents)
    ? commit.parents.map((parent) => parent?.sha ?? null)
    : [];
  const proof = {
    verified: false,
    eventBaseSha: eventBase ?? null,
    eventHeadSha: eventHead ?? null,
    eventMergeCommitSha: eventMerge ?? null,
    liveBaseSha: liveBase ?? null,
    liveHeadSha: liveHead ?? null,
    liveMergeCommitSha: liveMerge ?? null,
    mergeable: livePull?.mergeable ?? null,
    commitSha: commit?.sha ?? null,
    parentShas,
    reason: null,
  };
  const requiredShas = [eventBase, eventHead, eventMerge, liveBase, liveHead, liveMerge];
  if (requiredShas.some((sha) => !EXACT_SHA.test(String(sha ?? '')))) {
    proof.reason = 'pull-request-merge-identity-invalid';
  } else if (livePull.mergeable !== true) {
    proof.reason = 'pull-request-test-merge-unavailable';
  } else if (
    eventBase !== liveBase ||
    eventHead !== liveHead ||
    eventMerge !== liveMerge
  ) {
    proof.reason = 'pull-request-test-merge-stale';
  } else if (
    !EXACT_SHA.test(String(commit?.sha ?? '')) ||
    commit.sha !== liveMerge ||
    parentShas.length < 2
  ) {
    proof.reason = 'pull-request-test-merge-commit-invalid';
  } else if (parentShas[0] !== liveBase || parentShas[1] !== liveHead) {
    proof.reason = 'pull-request-test-merge-parent-mismatch';
  } else {
    proof.verified = true;
  }
  return proof;
}

async function acquireChanges(eventName, event, repository, token, fetchImpl = fetch) {
  const api = process.env.GITHUB_API_URL || 'https://api.github.com';
  if (eventName === 'pull_request' || eventName === 'pull_request_target') {
    const number = event.pull_request?.number ?? event.number;
    if (!number) throw new Error('Pull request event omitted its number.');
    let mergeCommitProof = null;
    let mergeCommit = null;
    if (isSampleLabelEvent(eventName, event)) {
      const { body: livePull } = await getJson(
        `${api}/repos/${repository}/pulls/${number}`,
        token,
        fetchImpl,
      );
      const liveMerge = livePull?.merge_commit_sha;
      if (EXACT_SHA.test(String(liveMerge ?? ''))) {
        const { body } = await getJson(
          `${api}/repos/${repository}/git/commits/${liveMerge}`,
          token,
          fetchImpl,
        );
        mergeCommit = body;
      }
      mergeCommitProof = mergeIntegritySnapshot(event.pull_request, livePull, mergeCommit);
      if (!mergeCommitProof.verified) {
        return {
          files: [],
          forcedFullReasons: [mergeCommitProof.reason],
          trustedContentSha: null,
          classificationFailure: 'pull-request-merge-integrity-unavailable',
          mergeCommitProof,
        };
      }
    }
    const files = await paginatedFiles(
      `${api}/repos/${repository}/pulls/${number}/files?per_page=100`,
      token,
      fetchImpl,
    );
    const expectedCount = event.pull_request?.changed_files;
    const reasons = [];
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
    if (eventName === 'pull_request_target' && !event.pull_request?.merge_commit_sha) {
      reasons.push('pull-request-merge-commit-sha-unavailable');
    }
    let trustedContentSha = null;
    let snapshotFiles = files;
    if (base && head) {
      const { body } = await getJson(
        `${api}/repos/${repository}/compare/${encodeURIComponent(base)}...${encodeURIComponent(head)}?per_page=1`,
        token,
        fetchImpl,
      );
      trustedContentSha = body.merge_base_commit?.sha ?? null;
      if (!trustedContentSha) reasons.push('pull-request-merge-base-unavailable');
      if (!Array.isArray(body.files)) {
        reasons.push('pull-request-compare-files-unavailable');
      } else {
        snapshotFiles = body.files;
        if (snapshotFiles.length >= 300) reasons.push('pull-request-compare-file-limit');
        const identity = (file) =>
          JSON.stringify([
            file.filename,
            file.previous_filename ?? null,
            file.status,
            file.additions,
            file.deletions,
            file.changes,
            file.patch ?? null,
          ]);
        if (
          files.length !== snapshotFiles.length ||
          files.some((file, index) => identity(file) !== identity(snapshotFiles[index]))
        ) {
          reasons.push('pull-request-file-snapshot-mismatch');
        }
      }
    }
    if (!Number.isInteger(expectedCount) || expectedCount !== snapshotFiles.length) {
      reasons.push('pull-request-file-count-mismatch');
    }
    if (mergeCommitProof) {
      const { body: finalPull } = await getJson(
        `${api}/repos/${repository}/pulls/${number}`,
        token,
        fetchImpl,
      );
      const finalProof = mergeIntegritySnapshot(event.pull_request, finalPull, mergeCommit);
      if (!finalProof.verified) {
        reasons.push(finalProof.reason);
        return {
          files: snapshotFiles,
          forcedFullReasons: reasons,
          trustedContentSha,
          classificationFailure: 'pull-request-merge-integrity-unavailable',
          mergeCommitProof: finalProof,
        };
      }
      mergeCommitProof = finalProof;
    }
    return {
      files: snapshotFiles,
      forcedFullReasons: reasons,
      trustedContentSha,
      classificationFailure: null,
      mergeCommitProof,
    };
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
    `shadow_sampled=${decision.shadowSample.sampled}`,
    `trusted_classifier=${decision.trustedClassifierAvailable}`,
    ...ALL_DOMAINS.map((domain) => `${domain}=${decision.domains[domain].selected}`),
  ];
  fs.appendFileSync(outputPath, `${lines.join('\n')}\n`);
}

async function main(fetchImpl = fetch) {
  const eventName = process.env.GITHUB_EVENT_NAME;
  const event = JSON.parse(fs.readFileSync(process.env.GITHUB_EVENT_PATH, 'utf8'));
  const policyPath = process.env.CHANGE_POLICY_PATH || path.join(__dirname, 'change-policy.v1.json');
  const outputFile = process.env.CHANGE_DECISION_PATH || 'artifacts/selective-ci/classification.json';
  const policy = loadPolicy(policyPath);
  const sample = shadowSample(
    eventName,
    event,
    process.env.SELECTIVE_CI_SAMPLE_OPERATOR,
  );
  const budgetedFetch = createBudgetedFetch(fetchImpl);

  let changes;
  let classificationFailure = null;
  try {
    changes = await acquireChanges(
      eventName,
      event,
      process.env.GITHUB_REPOSITORY,
      process.env.GITHUB_TOKEN,
      budgetedFetch,
    );
    classificationFailure = changes.classificationFailure ?? null;
  } catch (error) {
    const mergeProofRequired = isSampleLabelEvent(eventName, event);
    changes = {
      files: [],
      forcedFullReasons: [`metadata-error:${error.message}`],
      mergeCommitProof: mergeProofRequired
        ? {
            ...mergeIntegritySnapshot(event.pull_request, {}, null),
            reason: 'pull-request-test-merge-metadata-unavailable',
            error: error.message,
          }
        : null,
    };
    if (/rate limit exhausted|request budget exceeded/i.test(error.message)) {
      classificationFailure = 'github-metadata-rate-limit-or-budget';
    } else if (mergeProofRequired) {
      classificationFailure = 'pull-request-merge-integrity-unavailable';
    }
  }

  let files = changes.files;
  if (changes.forcedFullReasons.length === 0) {
    try {
      files = await attachTrustedMarkdownBase(
        changes.files,
        process.env.GITHUB_REPOSITORY,
        changes.trustedContentSha,
        process.env.GITHUB_TOKEN,
        budgetedFetch,
      );
    } catch (error) {
      changes.forcedFullReasons.push(`trusted-markdown-base-error:${error.message}`);
      if (/rate limit exhausted|request budget exceeded/i.test(error.message)) {
        classificationFailure = 'github-metadata-rate-limit-or-budget';
      } else {
        classificationFailure = 'github-metadata-unavailable';
      }
      if (changes.mergeCommitProof) {
        changes.mergeCommitProof = {
          ...changes.mergeCommitProof,
          verified: false,
          reason: 'pull-request-test-merge-recheck-unavailable',
          error: error.message,
        };
      }
    }
  }
  if (changes.mergeCommitProof?.verified) {
    try {
      const number = event.pull_request?.number ?? event.number;
      const api = process.env.GITHUB_API_URL || 'https://api.github.com';
      const { body: finalPull } = await getJson(
        `${api}/repos/${process.env.GITHUB_REPOSITORY}/pulls/${number}`,
        process.env.GITHUB_TOKEN,
        budgetedFetch,
      );
      const finalProof = mergeIntegritySnapshot(
        event.pull_request,
        finalPull,
        {
          sha: changes.mergeCommitProof.commitSha,
          parents: changes.mergeCommitProof.parentShas.map((sha) => ({ sha })),
        },
      );
      changes.mergeCommitProof = finalProof;
      if (!finalProof.verified) {
        changes.forcedFullReasons.push(finalProof.reason);
        classificationFailure = 'pull-request-merge-integrity-unavailable';
      }
    } catch (error) {
      changes.forcedFullReasons.push(`merge-integrity-recheck-error:${error.message}`);
      classificationFailure = /rate limit exhausted|request budget exceeded/i.test(error.message)
        ? 'github-metadata-rate-limit-or-budget'
        : 'pull-request-merge-integrity-unavailable';
      changes.mergeCommitProof = {
        ...changes.mergeCommitProof,
        verified: false,
        reason: 'pull-request-test-merge-recheck-unavailable',
        error: error.message,
      };
    }
  }
  const apiRequestBudget = budgetedFetch.observation();
  if (apiRequestBudget.exhausted || apiRequestBudget.rateLimitResponseObserved) {
    classificationFailure = 'github-metadata-rate-limit-or-budget';
  }
  const forcedFullReasons = [
    ...changes.forcedFullReasons,
    ...(apiRequestBudget.exhausted ? ['api-request-budget-exhausted'] : []),
  ];
  const decision = classifyFiles(files, policy, {
    eventName,
    trustedPolicySha: process.env.TRUSTED_POLICY_SHA,
    forcedFullReasons,
    shadowSampled: sample.sampled,
    shadowSampleReason: sample.reason,
    shadowSampleObservedActor: sample.observedActor,
    shadowSampleExpectedOperator: sample.expectedOperator,
    trustedClassifierAvailable:
      classificationFailure === null &&
      (!isSampleLabelEvent(eventName, event) || changes.mergeCommitProof?.verified === true),
    apiRequestBudget,
    classificationFailure,
    mergeCommitProof: changes.mergeCommitProof ?? null,
  });
  fs.mkdirSync(path.dirname(outputFile), { recursive: true });
  fs.writeFileSync(outputFile, `${JSON.stringify(decision, null, 2)}\n`);
  if (process.env.GITHUB_OUTPUT) writeOutputs(decision, process.env.GITHUB_OUTPUT);
  process.stdout.write(`${JSON.stringify(decision)}\n`);
  if (classificationFailure) process.exitCode = 1;
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
  createBudgetedFetch,
  inspectMarkdownPatch,
  loadPolicy,
  main,
  paginatedFiles,
  parseNext,
  shadowSample,
};
