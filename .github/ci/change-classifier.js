'use strict';

const fs = require('node:fs');
const path = require('node:path');

const ALL_DOMAINS = ['docs', 'dotnet', 'postgres', 'powershell', 'javascript', 'oci'];
const AMBIGUOUS_STATUSES = new Set(['removed', 'renamed']);
const EXECUTABLE_FENCE = /^\s*[+-]\s*```(?:bash|console|csharp|cs|dockerfile|javascript|js|powershell|pwsh|sh|shell|sql|typescript|ts|yaml|yml)?\s*$/im;

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
      if (typeof file.patch !== 'string') {
        reasons.push('markdown-patch-unavailable');
      } else if (
        Number.isInteger(file.changes) &&
        file.changes >
          file.patch.split('\n').filter((line) => /^[+-](?![+-])/.test(line)).length
      ) {
        reasons.push('markdown-patch-truncated');
      } else if (EXECUTABLE_FENCE.test(file.patch)) {
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
  return { body: await response.json(), next: parseNext(response.headers.get('link')) };
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
    return { files, forcedFullReasons: reasons };
  }

  if (eventName === 'merge_group') {
    const base = event.merge_group?.base_sha;
    const head = event.merge_group?.head_sha;
    if (!base || !head) throw new Error('Merge group event omitted base or head SHA.');
    const files = await paginatedFiles(
      `${api}/repos/${repository}/compare/${encodeURIComponent(base)}...${encodeURIComponent(head)}?per_page=100`,
      token,
      fetchImpl,
    );
    const reasons = files.length >= 300 ? ['merge-group-compare-file-limit'] : [];
    return { files, forcedFullReasons: reasons };
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

  const decision = classifyFiles(changes.files, policy, {
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
  classifyFiles,
  loadPolicy,
  paginatedFiles,
  parseNext,
};
