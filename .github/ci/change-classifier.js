'use strict';

const fs = require('node:fs');
const path = require('node:path');
const { createHash } = require('node:crypto');

const ALL_DOMAINS = ['docs', 'dotnet', 'postgres', 'powershell', 'javascript', 'oci'];
const AMBIGUOUS_STATUSES = new Set(['copied', 'removed', 'renamed']);
const MARKDOWN_FENCE = /^\s*(`{3,}|~{3,})(.*)$/;
const MARKDOWN_INDENTED_CODE = /^(?: {4}|\t).*\S/;
const SHADOW_SAMPLE_LABEL = 'ci:selective-shadow-sample';
const API_REQUEST_LIMIT = 132;
const MARKDOWN_FETCH_CONCURRENCY = 8;
const EXACT_SHA = /^[0-9a-f]{40}$/;
const ASCII_CONTROL = /[\x00-\x1f\x7f]/;
const SMOKE_EVENT_TYPE = 'selective-ci-smoke';
const SMOKE_WORKFLOW_FILE = 'selective-ci-shadow.yml';
const LABELED_RUN_LIMIT = 2;
const LABEL_EVENT_RUN_MAX_LAG_SECONDS = 60;

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

function invalidRepositoryPath(filename) {
  return (
    !filename ||
    ASCII_CONTROL.test(filename) ||
    filename.includes('\\') ||
    filename.startsWith('/') ||
    filename.split('/').includes('..')
  );
}

function normalizeFile(file) {
  const filename = String(file.filename ?? '');
  const previousFilename =
    file.previous_filename === undefined || file.previous_filename === null
      ? undefined
      : String(file.previous_filename);
  const invalid =
    invalidRepositoryPath(filename) ||
    (previousFilename !== undefined && invalidRepositoryPath(previousFilename));
  return {
    ...file,
    filename,
    ...(previousFilename === undefined ? {} : { previous_filename: previousFilename }),
    ...(invalid ? { invalid: true } : {}),
    status: String(file.status ?? 'unknown').toLowerCase(),
  };
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
  if (!Number.isInteger(file.changes) || file.changes <= 0) {
    return { uncertain: 'markdown-patch-change-count-invalid', changedLineCount };
  }
  if (file.changes !== changedLineCount) {
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
    labelRunAuthorization: options.labelRunAuthorization ?? null,
    smokeAuthorization: options.smokeAuthorization ?? null,
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

function shadowSample(
  eventName,
  event,
  expectedOperator,
  smokeAuthorization = null,
  labelRunAuthorization = null,
) {
  const observedActor =
    typeof event.sender?.login === 'string' && event.sender.login.length > 0
      ? event.sender.login
      : null;
  const configuredOperator =
    typeof expectedOperator === 'string' && expectedOperator.length > 0
      ? expectedOperator
      : null;
  if (eventName === 'repository_dispatch') {
    return {
      sampled: smokeAuthorization?.authorized === true,
      reason: smokeAuthorization?.reason ?? 'repository-dispatch-smoke-unavailable',
      observedActor: smokeAuthorization?.sender ?? observedActor,
      expectedOperator: smokeAuthorization?.expectedOperator ?? configuredOperator,
    };
  }
  if (eventName !== 'pull_request' && eventName !== 'pull_request_target') {
    return {
      sampled: false,
      reason: 'unsupported-shadow-event',
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
  const sampled =
    sampleLabelEvent &&
    operatorMatches &&
    labelRunAuthorization?.authorized === true;
  const reason = sampled
    ? labelRunAuthorization.reason
    : sampleLabelEvent && labelRunAuthorization?.reason
      ? labelRunAuthorization.reason
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

function parseWindowStart(windowStart, now = null) {
  const windowStartMilliseconds = Date.parse(windowStart);
  const nowMilliseconds = now ? Date.parse(now) : Date.now();
  if (
    !/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$/.test(windowStart) ||
    !Number.isFinite(windowStartMilliseconds) ||
    new Date(windowStartMilliseconds).toISOString().replace('.000Z', 'Z') !== windowStart ||
    !Number.isFinite(nowMilliseconds) ||
    windowStartMilliseconds > nowMilliseconds
  ) {
    return null;
  }
  return windowStartMilliseconds;
}

async function readCompleteWorkflowRunHistory({
  repository,
  token,
  fetchImpl,
  eventName,
  windowStart,
  windowStartMilliseconds,
}) {
  const api = process.env.GITHUB_API_URL || 'https://api.github.com';
  const runs = [];
  const ids = new Set();
  let pageCount = 0;
  let totalCount = null;
  let url =
    `${api}/repos/${repository}/actions/workflows/${SMOKE_WORKFLOW_FILE}/runs` +
    `?event=${encodeURIComponent(eventName)}` +
    `&created=${encodeURIComponent(`>=${windowStart}`)}&per_page=100`;
  while (url) {
    pageCount += 1;
    if (pageCount > 100) throw new Error('Workflow run pagination exceeded 100 pages.');
    const { body, next } = await getJson(url, token, fetchImpl);
    if (
      !Number.isSafeInteger(body.total_count) ||
      body.total_count < 0 ||
      body.total_count >= 1000
    ) {
      throw new Error('Workflow run total_count was invalid or reached the 1,000-run API cap.');
    }
    if (totalCount === null) totalCount = body.total_count;
    if (body.total_count !== totalCount) {
      throw new Error('Workflow run total_count changed during pagination.');
    }
    if (!Array.isArray(body.workflow_runs)) {
      throw new Error('Workflow run metadata omitted workflow_runs.');
    }
    for (const run of body.workflow_runs) {
      const id = String(run.id ?? '');
      if (!/^[1-9]\d*$/.test(id) || ids.has(id)) {
        throw new Error('Workflow run metadata contained an invalid or duplicate run ID.');
      }
      if (
        typeof run.created_at !== 'string' ||
        !Number.isFinite(Date.parse(run.created_at)) ||
        Date.parse(run.created_at) < windowStartMilliseconds
      ) {
        throw new Error('Workflow run metadata fell outside the authorized window.');
      }
      ids.add(id);
      runs.push(run);
    }
    url = next;
  }
  if (runs.length !== totalCount) {
    throw new Error(
      `Workflow run pagination was incomplete: expected ${totalCount}, observed ${runs.length}.`,
    );
  }
  return {
    runs,
    totalCount,
    pageCount,
    runIds: runs.map((run) => String(run.id)).sort(),
  };
}

async function readCompleteIssueEventHistory({
  repository,
  pullRequestNumber,
  token,
  fetchImpl,
  windowStartMilliseconds,
}) {
  const api = process.env.GITHUB_API_URL || 'https://api.github.com';
  const events = [];
  const ids = new Set();
  let pageCount = 0;
  let url =
    `${api}/repos/${repository}/issues/${pullRequestNumber}/events?per_page=100`;
  while (url) {
    pageCount += 1;
    if (pageCount > 100) throw new Error('Issue event pagination exceeded 100 pages.');
    const { body, next } = await getJson(url, token, fetchImpl);
    if (!Array.isArray(body)) throw new Error('Issue event metadata was not an array.');
    for (const event of body) {
      const id = String(event.id ?? '');
      const createdAtMilliseconds = Date.parse(event.created_at);
      if (
        !/^[1-9]\d*$/.test(id) ||
        ids.has(id) ||
        typeof event.created_at !== 'string' ||
        !Number.isFinite(createdAtMilliseconds)
      ) {
        throw new Error('Issue event metadata contained an invalid or duplicate identity.');
      }
      ids.add(id);
      if (createdAtMilliseconds >= windowStartMilliseconds) events.push(event);
    }
    url = next;
  }
  return {
    events,
    pageCount,
    eventIds: events.map((event) => String(event.id)).sort(),
  };
}

async function authorizeLabeledRun(
  event,
  repository,
  token,
  fetchImpl = fetch,
  runtime = {},
) {
  const sender =
    typeof event.sender?.login === 'string' && event.sender.login.length > 0
      ? event.sender.login
      : null;
  const actor =
    typeof runtime.actor === 'string' && runtime.actor.length > 0
      ? runtime.actor
      : null;
  const expectedOperator =
    typeof runtime.expectedOperator === 'string' && runtime.expectedOperator.length > 0
      ? runtime.expectedOperator
      : null;
  const expectedControllerSha =
    typeof runtime.configuredControllerSha === 'string' &&
    runtime.configuredControllerSha.length > 0
      ? runtime.configuredControllerSha
      : null;
  const windowStart =
    typeof runtime.windowStart === 'string' && runtime.windowStart.length > 0
      ? runtime.windowStart
      : null;
  const evidence = {
    authorized: false,
    reason: null,
    eventAction: event.action ?? null,
    label: event.label?.name ?? null,
    sender,
    actor,
    expectedOperator,
    workflowRef: runtime.workflowRef ?? null,
    workflowSha: runtime.workflowSha ?? null,
    expectedControllerSha,
    windowStart,
    currentRunId: runtime.runId ?? null,
    currentRunAttempt: runtime.runAttempt ?? null,
    pullRequestNumber: event.pull_request?.number ?? null,
    labelRunCount: null,
    authorizedRunCount: null,
    authorizedSlot: null,
    historyFingerprint: null,
    historySnapshots: [],
    requestBudgetAtAuthorization: null,
    error: null,
  };
  const requestObservation = () =>
    typeof runtime.requestObservation === 'function'
      ? runtime.requestObservation()
      : null;
  const fail = (reason, error = null) => {
    evidence.requestBudgetAtAuthorization = requestObservation();
    return {
      ...evidence,
      reason,
      error: error === null ? null : String(error),
    };
  };
  if (event.action !== 'labeled' || typeof event.label?.name !== 'string') {
    return fail('label-event-invalid');
  }
  if (!Number.isSafeInteger(event.pull_request?.number) || event.pull_request.number < 1) {
    return fail('label-pull-request-number-invalid');
  }
  if (expectedOperator === null) return fail('label-operator-unconfigured');
  if (!EXACT_SHA.test(String(expectedControllerSha ?? ''))) {
    return fail('label-controller-sha-unconfigured-or-invalid');
  }
  if (windowStart === null) return fail('label-window-start-unconfigured');
  const windowStartMilliseconds = parseWindowStart(windowStart, runtime.now);
  if (windowStartMilliseconds === null) return fail('label-window-start-invalid');
  if (!/^[1-9]\d*$/.test(String(runtime.runId ?? ''))) {
    return fail('label-run-id-invalid');
  }

  const delay = runtime.delay ?? ((milliseconds) =>
    new Promise((resolve) => setTimeout(resolve, milliseconds)));
  const readHistory = async (label) => {
    const history = await readCompleteWorkflowRunHistory({
      repository,
      token,
      fetchImpl,
      eventName: 'pull_request_target',
      windowStart,
      windowStartMilliseconds,
    });
    evidence.labelRunCount = history.totalCount;
    const pullRequestNumbers = history.runs.map((run) => {
      if (
        !Array.isArray(run.pull_requests) ||
        run.pull_requests.length !== 1 ||
        !Number.isSafeInteger(run.pull_requests[0]?.number) ||
        run.pull_requests[0].number < 1
      ) {
        return null;
      }
      return run.pull_requests[0].number;
    });
    const currentRun = history.runs.find(
      (run) => String(run.id) === String(runtime.runId),
    );
    const earlyFailure = (reason) => {
      evidence.historySnapshots.push({
        label: `${runtime.snapshotLabelPrefix ?? ''}${label}`,
        totalCount: history.totalCount,
        pageCount: history.pageCount,
        runIds: history.runIds,
        pullRequestNumbers,
        currentRunVisible: currentRun !== undefined,
      });
      return reason;
    };
    if (!currentRun) return earlyFailure('label-current-run-not-visible');
    if (history.totalCount > LABELED_RUN_LIMIT) {
      return earlyFailure('label-run-limit-exceeded');
    }
    if (pullRequestNumbers.some((number) => number === null)) {
      throw new Error('Workflow run metadata omitted an exact pull request identity.');
    }
    if (new Set(pullRequestNumbers).size !== pullRequestNumbers.length) {
      return earlyFailure('label-run-pull-request-repeated');
    }
    const issueHistories = new Map();
    for (const pullRequestNumber of new Set(pullRequestNumbers)) {
      issueHistories.set(
        pullRequestNumber,
        await readCompleteIssueEventHistory({
          repository,
          pullRequestNumber,
          token,
          fetchImpl,
          windowStartMilliseconds,
        }),
      );
    }
    const usedIssueEventIds = new Set();
    const runRecords = [];
    const sortedRuns = [...history.runs].sort(
      (left, right) =>
        Date.parse(left.created_at) - Date.parse(right.created_at) ||
        Number(left.id) - Number(right.id),
    );
    for (const run of sortedRuns) {
      const pullRequestNumber = run.pull_requests[0].number;
      const runCreatedAtMilliseconds = Date.parse(run.created_at);
      const candidates = issueHistories
        .get(pullRequestNumber)
        .events.filter((issueEvent) => {
          const lagMilliseconds =
            runCreatedAtMilliseconds - Date.parse(issueEvent.created_at);
          return (
            issueEvent.event === 'labeled' &&
            lagMilliseconds >= 0 &&
            lagMilliseconds <= LABEL_EVENT_RUN_MAX_LAG_SECONDS * 1000 &&
            !usedIssueEventIds.has(String(issueEvent.id))
          );
        });
      if (candidates.length !== 1) {
        throw new Error(
          `Workflow run ${run.id} had ${candidates.length} unambiguous labeled issue-event bindings.`,
        );
      }
      const issueEvent = candidates[0];
      usedIssueEventIds.add(String(issueEvent.id));
      const controllerOwned =
        run.event === 'pull_request_target' &&
        run.head_sha === expectedControllerSha &&
        run.head_branch === 'main' &&
        run.path === `.github/workflows/${SMOKE_WORKFLOW_FILE}` &&
        run.head_repository?.full_name === repository &&
        run.actor?.login === expectedOperator;
      const exactSampleEvent =
        issueEvent.event === 'labeled' &&
        issueEvent.label?.name === SHADOW_SAMPLE_LABEL &&
        issueEvent.actor?.login === expectedOperator;
      const authorized = controllerOwned && exactSampleEvent;
      runRecords.push({
        runId: String(run.id),
        runAttempt: run.run_attempt ?? null,
        runCreatedAt: run.created_at ?? null,
        eventId: String(issueEvent.id),
        eventAction: issueEvent.event ?? null,
        eventCreatedAt: issueEvent.created_at ?? null,
        eventRunLagSeconds:
          (runCreatedAtMilliseconds - Date.parse(issueEvent.created_at)) / 1000,
        label: issueEvent.label?.name ?? null,
        operator: issueEvent.actor?.login ?? null,
        pullRequestNumber,
        workflowEvent: run.event ?? null,
        workflowPath: run.path ?? null,
        workflowSha: run.head_sha ?? null,
        workflowBranch: run.head_branch ?? null,
        workflowActor: run.actor?.login ?? null,
        repository: run.head_repository?.full_name ?? null,
        controllerOwned,
        authorized,
      });
    }
    const authorizedRecords = runRecords.filter((record) => record.authorized);
    const currentRecord = runRecords.find(
      (record) => record.runId === String(runtime.runId),
    );
    const issueHistoryEvidence = [...issueHistories.entries()]
      .sort(([left], [right]) => left - right)
      .map(([pullRequestNumber, issueHistory]) => ({
        pullRequestNumber,
        pageCount: issueHistory.pageCount,
        eventCountSinceWindowStart: issueHistory.events.length,
        eventIdsSinceWindowStart: issueHistory.eventIds,
      }));
    const fingerprintInput = {
      runRecords,
      issueHistoryEvidence,
      totalCount: history.totalCount,
    };
    const historyFingerprint = createHash('sha256')
      .update(JSON.stringify(fingerprintInput))
      .digest('hex');
    evidence.authorizedRunCount = authorizedRecords.length;
    evidence.authorizedSlot =
      currentRecord?.authorized === true
        ? authorizedRecords.findIndex((record) => record.runId === currentRecord.runId) + 1
        : null;
    evidence.historyFingerprint = historyFingerprint;
    evidence.historySnapshots.push({
      label: `${runtime.snapshotLabelPrefix ?? ''}${label}`,
      totalCount: history.totalCount,
      pageCount: history.pageCount,
      runIds: history.runIds,
      pullRequestNumbers,
      authorizedRunCount: authorizedRecords.length,
      authorizedRunIds: authorizedRecords.map((record) => record.runId),
      authorizedEventIds: authorizedRecords.map((record) => record.eventId),
      authorizedSlot: evidence.authorizedSlot,
      unrelatedRunCount: runRecords.length - authorizedRecords.length,
      issueHistories: issueHistoryEvidence,
      runRecords,
      historyFingerprint,
      currentRunVisible: currentRun !== undefined,
    });
    return null;
  };

  try {
    const initialFailure = await readHistory('initial');
    if (initialFailure) return fail(initialFailure);
    await delay(5000);
    const recheckFailure = await readHistory('race-recheck');
    if (recheckFailure) return fail(recheckFailure);
    const [initial, recheck] = evidence.historySnapshots;
    if (initial.historyFingerprint !== recheck.historyFingerprint) {
      return fail('label-run-history-unstable');
    }
    const currentRecord = recheck.runRecords.find(
      (record) => record.runId === String(runtime.runId),
    );
    if (
      event.label.name !== SHADOW_SAMPLE_LABEL ||
      !Array.isArray(event.pull_request?.labels) ||
      !event.pull_request.labels.some((label) => label?.name === SHADOW_SAMPLE_LABEL)
    ) {
      return fail('label-name-or-state-mismatch');
    }
    if (sender === null || actor === null) return fail('label-operator-missing');
    if (sender !== expectedOperator || actor !== expectedOperator) {
      return fail('label-operator-mismatch');
    }
    if (runtime.workflowRef !== 'refs/heads/main') {
      return fail('label-workflow-ref-mismatch');
    }
    if (!EXACT_SHA.test(String(runtime.workflowSha ?? ''))) {
      return fail('label-workflow-sha-invalid');
    }
    if (runtime.workflowSha !== expectedControllerSha) {
      return fail('label-controller-sha-stale');
    }
    if (String(runtime.runAttempt ?? '') !== '1') return fail('label-rerun-forbidden');
    if (
      currentRecord?.workflowEvent !== 'pull_request_target' ||
      currentRecord.workflowSha !== expectedControllerSha ||
      currentRecord.workflowBranch !== 'main' ||
      currentRecord.workflowPath !== `.github/workflows/${SMOKE_WORKFLOW_FILE}` ||
      currentRecord.repository !== repository ||
      currentRecord.workflowActor !== expectedOperator ||
      currentRecord.runAttempt !== 1
    ) {
      return fail('label-current-run-identity-mismatch');
    }
    if (
      currentRecord.pullRequestNumber !== event.pull_request.number ||
      currentRecord.authorized !== true ||
      currentRecord.label !== event.label.name ||
      currentRecord.operator !== sender
    ) {
      return fail('label-run-pull-request-identity-unavailable');
    }
    if (recheck.runRecords.some((record) => record.authorized && record.runAttempt !== 1)) {
      return fail('label-authorized-run-rerun-forbidden');
    }
    const requestBudget = requestObservation();
    evidence.requestBudgetAtAuthorization = requestBudget;
    if (
      requestBudget === null ||
      requestBudget.exhausted !== false ||
      requestBudget.rateLimitResponseObserved !== false ||
      !Number.isSafeInteger(requestBudget.minimumRateLimitRemaining) ||
      requestBudget.minimumRateLimitRemaining < 1
    ) {
      return fail('label-rate-limit-observation-unavailable');
    }
    return {
      ...evidence,
      authorized: true,
      reason: `authorized-labeled-run-slot-${evidence.authorizedSlot}-of-${LABELED_RUN_LIMIT}`,
    };
  } catch (error) {
    return fail('label-metadata-unavailable', error.message);
  }
}

async function authorizeRepositoryDispatchSmoke(
  event,
  repository,
  token,
  fetchImpl = fetch,
  runtime = {},
) {
  const sender =
    typeof event.sender?.login === 'string' && event.sender.login.length > 0
      ? event.sender.login
      : null;
  const actor =
    typeof runtime.actor === 'string' && runtime.actor.length > 0
      ? runtime.actor
      : null;
  const expectedOperator =
    typeof runtime.expectedOperator === 'string' && runtime.expectedOperator.length > 0
      ? runtime.expectedOperator
      : null;
  const expectedControllerSha =
    typeof runtime.configuredControllerSha === 'string' &&
    runtime.configuredControllerSha.length > 0
      ? runtime.configuredControllerSha
      : null;
  const windowStart =
    typeof runtime.windowStart === 'string' && runtime.windowStart.length > 0
      ? runtime.windowStart
      : null;
  const evidence = {
    authorized: false,
    reason: null,
    eventType: event.action ?? null,
    sender,
    actor,
    expectedOperator,
    workflowRef: runtime.workflowRef ?? null,
    workflowSha: runtime.workflowSha ?? null,
    expectedControllerSha,
    windowStart,
    defaultBranch: null,
    liveDefaultBranchSha: null,
    currentRunId: runtime.runId ?? null,
    currentRunAttempt: runtime.runAttempt ?? null,
    historySnapshots: [],
    requestBudgetAtAuthorization: null,
    error: null,
  };
  const fail = (reason, error = null) => ({
    ...evidence,
    reason,
    error: error === null ? null : String(error),
  });
  if (event.action !== SMOKE_EVENT_TYPE) return fail('smoke-event-type-mismatch');
  if (expectedOperator === null) return fail('smoke-operator-unconfigured');
  if (windowStart === null) return fail('smoke-window-start-unconfigured');
  if (sender === null || actor === null) return fail('smoke-operator-missing');
  if (sender !== expectedOperator || actor !== expectedOperator) {
    return fail('smoke-operator-mismatch');
  }
  if (!EXACT_SHA.test(String(runtime.workflowSha ?? ''))) {
    return fail('smoke-workflow-sha-invalid');
  }
  if (!EXACT_SHA.test(String(expectedControllerSha ?? ''))) {
    return fail('smoke-controller-sha-unconfigured-or-invalid');
  }
  const windowStartMilliseconds = parseWindowStart(windowStart, runtime.now);
  if (windowStartMilliseconds === null) return fail('smoke-window-start-invalid');
  if (!/^[1-9]\d*$/.test(String(runtime.runId ?? ''))) {
    return fail('smoke-run-id-invalid');
  }
  if (String(runtime.runAttempt ?? '') !== '1') return fail('smoke-rerun-forbidden');

  const api = process.env.GITHUB_API_URL || 'https://api.github.com';
  const delay = runtime.delay ?? ((milliseconds) =>
    new Promise((resolve) => setTimeout(resolve, milliseconds)));

  const readHistory = async (label, defaultBranch, liveSha) => {
    const history = await readCompleteWorkflowRunHistory({
      repository,
      token,
      fetchImpl,
      eventName: 'repository_dispatch',
      windowStart,
      windowStartMilliseconds,
    });
    const { runs, totalCount, pageCount } = history;
    const revisionRuns = runs.filter((run) => run.head_sha === liveSha);
    const currentRun = revisionRuns.find(
      (run) => String(run.id) === String(runtime.runId),
    );
    evidence.historySnapshots.push({
      label,
      totalCount,
      pageCount,
      currentRevisionCount: revisionRuns.length,
      currentRevisionRunIds: revisionRuns.map((run) => String(run.id)),
      currentRunVisible: currentRun !== undefined,
    });
    if (!currentRun) return 'smoke-current-run-not-visible';
    if (
      currentRun.event !== 'repository_dispatch' ||
      currentRun.head_sha !== liveSha ||
      currentRun.head_branch !== defaultBranch ||
      currentRun.path !== `.github/workflows/${SMOKE_WORKFLOW_FILE}` ||
      currentRun.head_repository?.full_name !== repository ||
      currentRun.actor?.login !== expectedOperator ||
      currentRun.run_attempt !== 1
    ) {
      return 'smoke-current-run-identity-mismatch';
    }
    if (runs.length !== 1 || revisionRuns.length !== 1) {
      return 'smoke-run-limit-exceeded';
    }
    return null;
  };

  const readLiveState = async (label, previous = null) => {
    const { body: repositoryMetadata } = await getJson(
      `${api}/repos/${repository}`,
      token,
      fetchImpl,
    );
    const defaultBranch = repositoryMetadata.default_branch ?? null;
    if (defaultBranch !== 'main') return { failure: 'smoke-default-branch-mismatch' };
    if (runtime.workflowRef !== `refs/heads/${defaultBranch}`) {
      return { failure: 'smoke-workflow-ref-mismatch' };
    }
    const { body: branchMetadata } = await getJson(
      `${api}/repos/${repository}/branches/${encodeURIComponent(defaultBranch)}`,
      token,
      fetchImpl,
    );
    const liveSha = branchMetadata.commit?.sha ?? null;
    if (!EXACT_SHA.test(String(liveSha ?? ''))) {
      return { failure: 'smoke-live-default-sha-invalid' };
    }
    if (previous && (previous.defaultBranch !== defaultBranch || previous.liveSha !== liveSha)) {
      return { failure: 'smoke-live-default-raced' };
    }
    if (runtime.workflowSha !== liveSha) return { failure: 'smoke-workflow-sha-stale' };
    if (expectedControllerSha !== liveSha) {
      return { failure: 'smoke-expected-controller-sha-stale' };
    }
    const historyFailure = await readHistory(label, defaultBranch, liveSha);
    if (historyFailure) return { failure: historyFailure };
    return { defaultBranch, liveSha };
  };

  try {
    const first = await readLiveState('initial');
    if (first.failure) return fail(first.failure);
    evidence.defaultBranch = first.defaultBranch;
    evidence.liveDefaultBranchSha = first.liveSha;
    await delay(5000);
    const recheck = await readLiveState('race-recheck', first);
    if (recheck.failure) return fail(recheck.failure);
    const requestBudget =
      typeof runtime.requestObservation === 'function'
        ? runtime.requestObservation()
        : null;
    evidence.requestBudgetAtAuthorization = requestBudget;
    if (
      requestBudget === null ||
      requestBudget.exhausted !== false ||
      requestBudget.rateLimitResponseObserved !== false ||
      !Number.isSafeInteger(requestBudget.minimumRateLimitRemaining) ||
      requestBudget.minimumRateLimitRemaining < 1
    ) {
      return fail('smoke-rate-limit-observation-unavailable');
    }

    return {
      ...evidence,
      authorized: true,
      reason: 'authorized-first-repository-dispatch-smoke',
    };
  } catch (error) {
    return fail('smoke-metadata-unavailable', error.message);
  }
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
    parentShas.length !== 2
  ) {
    proof.reason = 'pull-request-test-merge-commit-invalid';
  } else if (parentShas[0] !== liveBase || parentShas[1] !== liveHead) {
    proof.reason = 'pull-request-test-merge-parent-mismatch';
  } else {
    proof.verified = true;
  }
  return proof;
}

async function acquireChanges(eventName, event, repository, token, fetchImpl = fetch, runtime = {}) {
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

  if (eventName === 'repository_dispatch') {
    const smokeAuthorization = await authorizeRepositoryDispatchSmoke(
      event,
      repository,
      token,
      fetchImpl,
      runtime,
    );
    const rateLimitOrBudgetFailure =
      smokeAuthorization.reason.includes('rate-limit') ||
      /rate limit exhausted|request budget exceeded/i.test(
        smokeAuthorization.error ?? '',
      );
    return {
      files: [],
      forcedFullReasons: [
        smokeAuthorization.authorized
          ? 'repository-dispatch-full-safety-suite'
          : smokeAuthorization.reason,
      ],
      trustedContentSha: smokeAuthorization.authorized
        ? smokeAuthorization.liveDefaultBranchSha
        : null,
      classificationFailure: smokeAuthorization.authorized
        ? null
        : rateLimitOrBudgetFailure
          ? 'github-metadata-rate-limit-or-budget'
          : 'repository-dispatch-smoke-unavailable',
      mergeCommitProof: null,
      smokeAuthorization,
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
  if (eventName === 'schedule') {
    return { files: [], forcedFullReasons: [`${eventName}-full-safety-suite`] };
  }
  return { files: [], forcedFullReasons: [`unsupported-event-${eventName}`] };
}

function writeOutputs(decision, outputPath) {
  const lines = [
    `full_suite=${decision.fullSuite}`,
    `shadow_sampled=${decision.shadowSample.sampled}`,
    `trusted_classifier=${decision.trustedClassifierAvailable}`,
    `label_run_authorization=${JSON.stringify(decision.labelRunAuthorization)}`,
    `smoke_authorization=${JSON.stringify(decision.smokeAuthorization)}`,
    `api_request_budget=${JSON.stringify(decision.apiRequestBudget)}`,
    ...ALL_DOMAINS.map((domain) => `${domain}=${decision.domains[domain].selected}`),
  ];
  fs.appendFileSync(outputPath, `${lines.join('\n')}\n`);
}

async function main(fetchImpl = fetch, runtimeOverrides = {}) {
  const eventName = process.env.GITHUB_EVENT_NAME;
  const event = JSON.parse(fs.readFileSync(process.env.GITHUB_EVENT_PATH, 'utf8'));
  const policyPath = process.env.CHANGE_POLICY_PATH || path.join(__dirname, 'change-policy.v1.json');
  const outputFile = process.env.CHANGE_DECISION_PATH || 'artifacts/selective-ci/classification.json';
  const policy = loadPolicy(policyPath);
  const budgetedFetch = createBudgetedFetch(fetchImpl);

  let changes;
  let classificationFailure = null;
  let labelRunAuthorization = null;
  let runtime;
  try {
    runtime = {
      actor: process.env.GITHUB_ACTOR,
      expectedOperator: process.env.SELECTIVE_CI_SAMPLE_OPERATOR,
      configuredControllerSha: process.env.SELECTIVE_CI_CONTROLLER_SHA,
      windowStart: process.env.SELECTIVE_CI_WINDOW_START,
      workflowRef: process.env.GITHUB_REF,
      workflowSha: process.env.GITHUB_SHA,
      runId: process.env.GITHUB_RUN_ID,
      runAttempt: process.env.GITHUB_RUN_ATTEMPT,
      requestObservation: budgetedFetch.observation,
      ...runtimeOverrides,
    };
    if (eventName === 'pull_request_target' && event.action === 'labeled') {
      labelRunAuthorization = await authorizeLabeledRun(
        event,
        process.env.GITHUB_REPOSITORY,
        process.env.GITHUB_TOKEN,
        budgetedFetch,
        runtime,
      );
      if (!labelRunAuthorization.authorized) {
        const rateLimitOrBudgetFailure =
          labelRunAuthorization.reason.includes('rate-limit') ||
          /rate limit exhausted|request budget exceeded/i.test(
            labelRunAuthorization.error ?? '',
          );
        changes = {
          files: [],
          forcedFullReasons: [labelRunAuthorization.reason],
          trustedContentSha: null,
          classificationFailure: rateLimitOrBudgetFailure
            ? 'github-metadata-rate-limit-or-budget'
            : 'labeled-run-window-unavailable',
          mergeCommitProof: null,
          labelRunAuthorization,
          smokeAuthorization: null,
        };
      }
    }
    if (!changes) {
      changes = await acquireChanges(
        eventName,
        event,
        process.env.GITHUB_REPOSITORY,
        process.env.GITHUB_TOKEN,
        budgetedFetch,
        runtime,
      );
      changes.labelRunAuthorization = labelRunAuthorization;
    }
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
      smokeAuthorization: eventName === 'repository_dispatch'
        ? {
            authorized: false,
            reason: 'smoke-metadata-unavailable',
            sender: event.sender?.login ?? null,
            actor: process.env.GITHUB_ACTOR || null,
            expectedOperator: process.env.SELECTIVE_CI_SAMPLE_OPERATOR || null,
            workflowRef: process.env.GITHUB_REF || null,
            workflowSha: process.env.GITHUB_SHA || null,
            expectedControllerSha: process.env.SELECTIVE_CI_CONTROLLER_SHA || null,
            windowStart: process.env.SELECTIVE_CI_WINDOW_START || null,
            defaultBranch: null,
            liveDefaultBranchSha: null,
            currentRunId: process.env.GITHUB_RUN_ID || null,
            currentRunAttempt: process.env.GITHUB_RUN_ATTEMPT || null,
            historySnapshots: [],
            requestBudgetAtAuthorization: budgetedFetch.observation(),
            error: error.message,
          }
        : null,
      labelRunAuthorization:
        labelRunAuthorization ??
        (eventName === 'pull_request_target' && event.action === 'labeled'
          ? {
              authorized: false,
              reason: 'label-metadata-unavailable',
              eventAction: event.action ?? null,
              label: event.label?.name ?? null,
              sender: event.sender?.login ?? null,
              actor: process.env.GITHUB_ACTOR || null,
              expectedOperator: process.env.SELECTIVE_CI_SAMPLE_OPERATOR || null,
              workflowRef: process.env.GITHUB_REF || null,
              workflowSha: process.env.GITHUB_SHA || null,
              expectedControllerSha: process.env.SELECTIVE_CI_CONTROLLER_SHA || null,
              windowStart: process.env.SELECTIVE_CI_WINDOW_START || null,
              currentRunId: process.env.GITHUB_RUN_ID || null,
              currentRunAttempt: process.env.GITHUB_RUN_ATTEMPT || null,
              pullRequestNumber: event.pull_request?.number ?? null,
              labelRunCount: null,
              historySnapshots: [],
              requestBudgetAtAuthorization: budgetedFetch.observation(),
              error: error.message,
            }
          : null),
    };
    if (/rate limit exhausted|request budget exceeded/i.test(error.message)) {
      classificationFailure = 'github-metadata-rate-limit-or-budget';
    } else if (
      eventName === 'pull_request_target' &&
      event.action === 'labeled' &&
      labelRunAuthorization?.authorized !== true
    ) {
      classificationFailure = 'labeled-run-window-unavailable';
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
  if (
    isSampleLabelEvent(eventName, event) &&
    labelRunAuthorization?.authorized === true &&
    classificationFailure === null
  ) {
    const initialAuthorization = labelRunAuthorization;
    const finalAuthorization = await authorizeLabeledRun(
      event,
      process.env.GITHUB_REPOSITORY,
      process.env.GITHUB_TOKEN,
      budgetedFetch,
      {
        ...runtime,
        snapshotLabelPrefix: 'final-',
      },
    );
    const historyChanged =
      finalAuthorization.authorized === true &&
      initialAuthorization.historyFingerprint !== finalAuthorization.historyFingerprint;
    labelRunAuthorization = {
      ...finalAuthorization,
      authorized: finalAuthorization.authorized && !historyChanged,
      reason: historyChanged
        ? 'label-run-history-changed-before-domain-scheduling'
        : finalAuthorization.reason,
      authorizationPasses: 2,
      initialRequestBudgetAtAuthorization:
        initialAuthorization.requestBudgetAtAuthorization,
      historySnapshots: [
        ...initialAuthorization.historySnapshots,
        ...finalAuthorization.historySnapshots,
      ],
    };
    changes.labelRunAuthorization = labelRunAuthorization;
    if (!labelRunAuthorization.authorized) {
      changes.forcedFullReasons.push(labelRunAuthorization.reason);
      classificationFailure =
        labelRunAuthorization.reason.includes('rate-limit') ||
        /rate limit exhausted|request budget exceeded/i.test(
          labelRunAuthorization.error ?? '',
        )
          ? 'github-metadata-rate-limit-or-budget'
          : 'labeled-run-window-unavailable';
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
  const sample = shadowSample(
    eventName,
    event,
    process.env.SELECTIVE_CI_SAMPLE_OPERATOR,
    changes.smokeAuthorization ?? null,
    changes.labelRunAuthorization ?? null,
  );
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
    labelRunAuthorization: changes.labelRunAuthorization ?? null,
    smokeAuthorization: changes.smokeAuthorization ?? null,
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
  authorizeLabeledRun,
  authorizeRepositoryDispatchSmoke,
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
