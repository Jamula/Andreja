'use strict';

const WINDOW_MILLISECONDS = 7 * 24 * 60 * 60 * 1000;
const CANONICAL_PREFIX = 'log/weekly-retrospective-';
const SCHEMA_MARKER = '<!-- weekly-retrospective:v1 -->';
const NO_BLOCKERS = 'No blockers.';
const NO_DECISIONS = 'No new decision required.';
const NO_RETRO_ACTIONS = 'No actions after complete duplicate search.';
const GITHUB_ISSUE_URL =
  /^https:\/\/github\.com\/[^/\s]+\/[^/\s]+\/issues\/\d+$/;
const BLOCKER_REFERENCE =
  /^(?:#\d+|[^/\s]+\/[^/#\s]+#\d+|https:\/\/github\.com\/[^/\s]+\/[^/\s]+\/(?:issues|pull)\/\d+)$/;
const RFC3339_TIMESTAMP =
  /^(\d{4})-(\d{2})-(\d{2})T([01]\d|2[0-3]):([0-5]\d):([0-5]\d)(?:\.(\d{1,3}))?(Z|[+-](?:(?:0\d|1[0-3]):[0-5]\d|14:00))$/;

function requiredDate(value, name) {
  const match = typeof value === 'string' ? value.match(RFC3339_TIMESTAMP) : null;
  if (!match) {
    throw new Error(`${name} must be a timezone-qualified RFC 3339 timestamp`);
  }

  const year = Number(match[1]);
  const month = Number(match[2]);
  const day = Number(match[3]);
  const leapYear = year % 4 === 0 && (year % 100 !== 0 || year % 400 === 0);
  const daysInMonth = [
    31, leapYear ? 29 : 28, 31, 30, 31, 30,
    31, 31, 30, 31, 30, 31,
  ];
  if (month < 1 || month > 12 || day < 1 || day > daysInMonth[month - 1]) {
    throw new Error(`${name} must be a timezone-qualified RFC 3339 timestamp`);
  }

  const timestamp = Date.parse(value);
  if (!Number.isFinite(timestamp)) {
    throw new Error(`${name} must be a timezone-qualified RFC 3339 timestamp`);
  }
  return new Date(timestamp);
}

function cycleStart(dateValue) {
  const date = dateValue instanceof Date
    ? new Date(dateValue.getTime())
    : requiredDate(dateValue, 'date');
  if (!Number.isFinite(date.getTime())) {
    throw new Error('date must be a valid Date or timezone-qualified RFC 3339 timestamp');
  }
  const dayFromMonday = (date.getUTCDay() + 6) % 7;
  date.setUTCHours(0, 0, 0, 0);
  date.setUTCDate(date.getUTCDate() - dayFromMonday);
  return date.toISOString().slice(0, 10);
}

function completionKey(dateValue) {
  return `${CANONICAL_PREFIX}${cycleStart(dateValue)}.md`;
}

function parseStateTimestamp(key) {
  const match = key.match(
    /^log\/(\d{4}-\d{2}-\d{2})T(\d{2})-(\d{2})-(\d{2}(?:\.\d+)?)(Z|[+-]\d{2}-\d{2})-/,
  );
  if (!match) return null;
  const zone = match[5] === 'Z' ? 'Z' : `${match[5].slice(0, 3)}:${match[5].slice(4)}`;
  try {
    return requiredDate(
      `${match[1]}T${match[2]}:${match[3]}:${match[4]}${zone}`,
      'legacy completion timestamp',
    );
  } catch {
    return null;
  }
}

function field(content, name) {
  const match = content.match(new RegExp(`^- ${name}:\\s*(.+)$`, 'im'));
  return match ? match[1].trim() : null;
}

function parseEvidenceWindow(value) {
  const match = typeof value === 'string' ? value.match(/^(.+) through (.+)$/) : null;
  if (!match) {
    throw new Error('evidence window must contain two timestamps');
  }

  const start = requiredDate(match[1], 'evidence window start');
  const end = requiredDate(match[2], 'evidence window end');
  if (start >= end) {
    throw new Error('evidence window start must precede its end');
  }
  return { start, end };
}

function sectionHasListEntry(content, name) {
  const lines = content.split(/\r?\n/);
  const sectionStart = lines.indexOf(`## ${name}`);
  if (sectionStart === -1) return false;

  const nextSection = lines.findIndex(
    (line, index) => index > sectionStart && /^##\s+\S/.test(line),
  );
  const sectionEnd = nextSection === -1 ? lines.length : nextSection;
  return lines
    .slice(sectionStart + 1, sectionEnd)
    .some((line) => /^[-*+]\s+\S/.test(line.trim()));
}

function canonicalSectionEntries(content, name) {
  const lines = content.split(/\r?\n/);
  const sectionStart = lines.indexOf(`## ${name}`);
  if (sectionStart === -1) {
    return { valid: false, reason: 'required-sections-missing' };
  }
  const nextSection = lines.findIndex(
    (line, index) => index > sectionStart && /^##\s+\S/.test(line),
  );
  const sectionEnd = nextSection === -1 ? lines.length : nextSection;
  const body = lines
    .slice(sectionStart + 1, sectionEnd)
    .map((line) => line.trim())
    .filter(Boolean);

  if (body.length === 0) return { valid: false, reason: 'required-section-entries-missing' };
  if (body.some((line) => !/^- \S/.test(line))) {
    return { valid: false, reason: 'required-section-entries-invalid' };
  }
  return { valid: true, entries: body.map((line) => line.slice(2)) };
}

function isSentinelOnly(entries, sentinel) {
  return entries.length === 1 && entries[0] === sentinel;
}

function validateCanonicalSections(content) {
  const validators = {
    Blockers: (entries) =>
      isSentinelOnly(entries, NO_BLOCKERS)
      || entries.every((entry) => BLOCKER_REFERENCE.test(entry)),
    Decisions: (entries) =>
      isSentinelOnly(entries, NO_DECISIONS)
      || entries.every((entry) =>
        /^\S(?:.*\S)? — Reference: \S(?:.*\S)?$/.test(entry)),
    'Retro actions': (entries) =>
      isSentinelOnly(entries, NO_RETRO_ACTIONS)
      || entries.every((entry) => {
        const match = entry.match(/^(created|existing): (\S(?:.*\S)?) — (\S+)$/);
        return Boolean(match && GITHUB_ISSUE_URL.test(match[3]));
      }),
  };

  for (const [name, validator] of Object.entries(validators)) {
    const parsed = canonicalSectionEntries(content, name);
    if (!parsed.valid) return parsed;
    if (!validator(parsed.entries)) {
      return { valid: false, reason: 'required-section-entries-invalid' };
    }
  }
  return { valid: true };
}

function validateCompletedLog(log) {
  if (!log || typeof log.key !== 'string' || typeof log.content !== 'string') {
    return { valid: false, reason: 'missing-log-data' };
  }

  if (log.key.startsWith(CANONICAL_PREFIX)) {
    if (!log.content.includes(SCHEMA_MARKER)) {
      return { valid: false, reason: 'missing-schema-marker' };
    }
    if (field(log.content, 'Status')?.toLowerCase() !== 'complete') {
      return { valid: false, reason: 'completion-status-missing' };
    }

    const completedAt = field(log.content, 'Completed at');
    const evidenceWindow = field(log.content, 'Evidence window');
    const shippedCount = field(log.content, 'Shipped count');
    const openCount = field(log.content, 'Open count');
    let completedDate;
    let parsedEvidenceWindow;
    try {
      completedDate = requiredDate(completedAt, 'Completed at');
    } catch {
      return { valid: false, reason: 'invalid-completion-timestamp' };
    }

    if (completionKey(completedDate) !== log.key) {
      return { valid: false, reason: 'completion-key-cycle-mismatch' };
    }
    if (!evidenceWindow || !/ through /.test(evidenceWindow)) {
      return { valid: false, reason: 'evidence-window-missing' };
    }
    try {
      parsedEvidenceWindow = parseEvidenceWindow(evidenceWindow);
    } catch {
      return { valid: false, reason: 'invalid-evidence-window' };
    }
    if (parsedEvidenceWindow.end > completedDate) {
      return { valid: false, reason: 'invalid-evidence-window' };
    }
    if (!/^\d+$/.test(shippedCount || '') || !/^\d+$/.test(openCount || '')) {
      return { valid: false, reason: 'counts-missing' };
    }
    const requiredSections = ['Blockers', 'Decisions', 'Retro actions'];
    if (requiredSections.some((name) => !new RegExp(`^## ${name}$`, 'm').test(log.content))) {
      return { valid: false, reason: 'required-sections-missing' };
    }
    const sectionValidation = validateCanonicalSections(log.content);
    if (!sectionValidation.valid) return sectionValidation;
    return { valid: true, completedAt: completedDate };
  }

  if (!/-retrospective-with-enforcement\.md$/.test(log.key)) {
    return { valid: false, reason: 'not-a-completion-record' };
  }
  const completedAt = parseStateTimestamp(log.key);
  const legacyEvidence = log.content.match(/^Evidence window:\s*(.+)\.$/m);
  let evidenceWindowValid = false;
  if (legacyEvidence) {
    try {
      const parsedEvidenceWindow = parseEvidenceWindow(legacyEvidence[1]);
      evidenceWindowValid = Boolean(completedAt && parsedEvidenceWindow.end <= completedAt);
    } catch {
      evidenceWindowValid = false;
    }
  }
  const legacyComplete =
    completedAt
    && /^# Retrospective with Enforcement\b/m.test(log.content)
    && evidenceWindowValid
    && /^## Evidence$/m.test(log.content)
    && /^## Decisions$/m.test(log.content)
    && /^## Actions$/m.test(log.content)
    && sectionHasListEntry(log.content, 'Evidence')
    && sectionHasListEntry(log.content, 'Decisions')
    && sectionHasListEntry(log.content, 'Actions');
  return legacyComplete
    ? { valid: true, completedAt, legacy: true }
    : { valid: false, reason: 'not-a-completed-legacy-record' };
}

function assessAdmission({
  now,
  logs = [],
  stateAvailable = false,
  enumerationComplete = false,
  configuredEnforcementAvailable = true,
}) {
  const current = requiredDate(now, 'now');
  if (stateAvailable !== true) {
    return { allowed: false, ceremonyRequired: false, reason: 'state-backend-unavailable' };
  }
  if (enumerationComplete !== true) {
    return { allowed: false, ceremonyRequired: false, reason: 'log-enumeration-incomplete' };
  }

  const invalidCanonical = logs
    .filter((log) => log.key?.startsWith(CANONICAL_PREFIX))
    .map((log) => ({ log, validation: validateCompletedLog(log) }))
    .find(({ validation }) => !validation.valid);
  if (invalidCanonical) {
    return {
      allowed: false,
      ceremonyRequired: false,
      reason: 'invalid-completion-record',
      key: invalidCanonical.log.key,
      detail: invalidCanonical.validation.reason,
    };
  }

  const completed = logs
    .map((log) => ({ log, validation: validateCompletedLog(log) }))
    .filter(({ validation }) => validation.valid);
  const future = completed.find(({ validation }) => validation.completedAt > current);
  if (future) {
    return {
      allowed: false,
      ceremonyRequired: false,
      reason: 'future-completion-record',
      key: future.log.key,
    };
  }

  const cycles = new Map();
  for (const item of completed) {
    const cycle = cycleStart(item.validation.completedAt);
    cycles.set(cycle, [...(cycles.get(cycle) || []), item.log.key]);
  }
  const duplicate = [...cycles.entries()].find(([, keys]) => new Set(keys).size > 1);
  if (duplicate) {
    return {
      allowed: false,
      ceremonyRequired: false,
      reason: 'duplicate-completion-records',
      cycle: duplicate[0],
      keys: duplicate[1],
    };
  }

  completed.sort((left, right) =>
    right.validation.completedAt.getTime() - left.validation.completedAt.getTime());
  const latest = completed[0];
  if (latest && current.getTime() - latest.validation.completedAt.getTime() <= WINDOW_MILLISECONDS) {
    return {
      allowed: true,
      ceremonyRequired: false,
      reason: 'retrospective-current',
      completionKey: latest.log.key,
      configuredEnforcementAvailable,
    };
  }

  return {
    allowed: false,
    ceremonyRequired: true,
    reason: 'retrospective-overdue',
    mechanism: 'built-in',
    configuredEnforcementAvailable,
  };
}

function issueUrl(issue) {
  return issue?.html_url || issue?.url || null;
}

function resolveActionCandidates(candidates, searches) {
  const searchByCandidate = new Map(
    (searches || []).map((search) => [String(search.candidateId), search]),
  );
  const actions = [];
  const pending = [];

  for (const candidate of candidates || []) {
    const id = String(candidate.id);
    const search = searchByCandidate.get(id);
    if (search?.complete !== true) {
      pending.push({ id, reason: 'duplicate-search-incomplete' });
      continue;
    }

    const matches = (search.matches || [])
      .filter((issue) => issueUrl(issue))
      .sort((left, right) => (left.number || 0) - (right.number || 0));
    if (matches.length > 0) {
      actions.push({
        summary: candidate.summary,
        disposition: 'existing',
        issueUrl: issueUrl(matches[0]),
      });
      continue;
    }

    const created = search.createdIssue;
    const labels = (created?.labels || []).map((label) =>
      String(typeof label === 'string' ? label : label.name).toLowerCase());
    if (issueUrl(created) && labels.includes('retro-action')) {
      actions.push({
        summary: candidate.summary,
        disposition: 'created',
        issueUrl: issueUrl(created),
      });
      continue;
    }

    pending.push({ id, reason: 'new-action-issue-required' });
  }

  return { complete: pending.length === 0, actions, pending };
}

function isNonNegativeInteger(value) {
  return Number.isInteger(value) && value >= 0;
}

function oneLine(value, name) {
  if (typeof value !== 'string') {
    throw new Error(`${name} must be a non-empty single line`);
  }
  const text = value.trim();
  if (!text || /[\r\n]/.test(text)) {
    throw new Error(`${name} must be a non-empty single line`);
  }
  return text;
}

function referenceList(values, emptyText) {
  if (!Array.isArray(values)) {
    throw new Error('blockers must be an array');
  }
  if (values.length === 0) return [`- ${emptyText}`];
  return values.map((value) => {
    const reference = oneLine(value, 'blocker reference');
    if (!BLOCKER_REFERENCE.test(reference)) {
      throw new Error('blocker reference must identify a GitHub issue or pull request');
    }
    return `- ${reference}`;
  });
}

function prepareCompletion({
  now,
  logs = [],
  stateAvailable = false,
  enumerationComplete = false,
  evidence,
  blockers = [],
  decisions = [],
  actions = [],
  gates = {},
}) {
  const admission = assessAdmission({
    now,
    logs,
    stateAvailable,
    enumerationComplete,
  });
  if (admission.allowed) {
    return { ready: false, reason: 'cycle-already-complete', write: null };
  }
  if (!admission.ceremonyRequired) {
    return { ready: false, reason: admission.reason, write: null };
  }

  const requiredGates = [
    'evidenceReviewComplete',
    'decisionReviewComplete',
    'decisionsRecorded',
    'duplicateSearchComplete',
    'actionIssuesComplete',
    'privacyReviewComplete',
  ];
  const missingGate = requiredGates.find((gate) => gates[gate] !== true);
  if (missingGate) {
    return { ready: false, reason: `gate-incomplete:${missingGate}`, write: null };
  }

  if (!evidence
    || !isNonNegativeInteger(evidence.shippedCount)
    || !isNonNegativeInteger(evidence.openCount)) {
    return { ready: false, reason: 'evidence-counts-invalid', write: null };
  }

  let current;
  let windowStart;
  let windowEnd;
  try {
    current = requiredDate(now, 'now');
    windowStart = requiredDate(evidence.windowStart, 'evidence.windowStart');
    windowEnd = requiredDate(evidence.windowEnd, 'evidence.windowEnd');
  } catch {
    return { ready: false, reason: 'evidence-window-invalid', write: null };
  }
  if (windowStart >= windowEnd || windowEnd > current) {
    return { ready: false, reason: 'evidence-window-invalid', write: null };
  }

  const key = completionKey(current);
  if (logs.some((log) => log.key === key)) {
    return { ready: false, reason: 'completion-key-conflict', write: null };
  }

  let blockerLines;
  let decisionLines;
  let actionLines;
  try {
    blockerLines = referenceList(blockers, NO_BLOCKERS);
    if (!Array.isArray(decisions)) {
      throw new Error('decisions must be an array');
    }
    decisionLines = decisions.length === 0
      ? [`- ${NO_DECISIONS}`]
      : decisions.map((decision) => {
        if (!decision || typeof decision !== 'object' || Array.isArray(decision)) {
          throw new Error('decision must be an object');
        }
        return `- ${oneLine(decision.summary, 'decision summary')} — Reference: ${oneLine(decision.reference, 'decision reference')}`;
      });
    if (!Array.isArray(actions)) {
      throw new Error('actions must be an array');
    }
    actionLines = actions.length === 0
      ? [`- ${NO_RETRO_ACTIONS}`]
      : actions.map((action) => {
        if (!action || typeof action !== 'object' || Array.isArray(action)) {
          throw new Error('action must be an object');
        }
        if (!['existing', 'created'].includes(action.disposition)) {
          throw new Error('action disposition must be existing or created');
        }
        const url = oneLine(action.issueUrl, 'action issue URL');
        if (!GITHUB_ISSUE_URL.test(url)) {
          throw new Error('action issue URL must be a GitHub issue URL');
        }
        return `- ${action.disposition}: ${oneLine(action.summary, 'action summary')} — ${url}`;
      });
  } catch (error) {
    return { ready: false, reason: `record-invalid:${error.message}`, write: null };
  }

  const content = [
    SCHEMA_MARKER,
    `# Weekly Retrospective — ${cycleStart(current)}`,
    '',
    '- Status: complete',
    `- Completed at: ${current.toISOString()}`,
    `- Evidence window: ${windowStart.toISOString()} through ${windowEnd.toISOString()}`,
    `- Shipped count: ${evidence.shippedCount}`,
    `- Open count: ${evidence.openCount}`,
    '',
    '## Blockers',
    ...blockerLines,
    '',
    '## Decisions',
    ...decisionLines,
    '',
    '## Retro actions',
    ...actionLines,
    '',
  ].join('\n');

  const validation = validateCompletedLog({ key, content });
  if (!validation.valid) {
    return {
      ready: false,
      reason: `record-invalid:generated completion failed validation (${validation.reason})`,
      write: null,
    };
  }

  return { ready: true, reason: 'completion-ready', write: { key, content } };
}

module.exports = {
  CANONICAL_PREFIX,
  SCHEMA_MARKER,
  assessAdmission,
  completionKey,
  cycleStart,
  prepareCompletion,
  resolveActionCandidates,
  validateCompletedLog,
};
