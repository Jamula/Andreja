'use strict';

const MAX_BATCH_SIZE = 5;
const MIN_SPAWN_SPACING_MILLISECONDS = 10_000;
const ACK_TIMEOUT_MILLISECONDS = 5 * 60 * 1000;
const QUEUE_OPERATIONS = new Set([
  'enumerate',
  'status',
  'classify',
  'admit',
  'mutate',
  'release',
]);
const WRITER_OPERATIONS = new Set(['admit', 'mutate', 'release']);
const RECONCILIATION_COLLECTIONS = [
  'sessions',
  'branches',
  'worktrees',
  'pullRequests',
  'reservations',
  'ledger',
  'issueReadiness',
];
const REPOSITORY_ARTIFACT_COLLECTIONS = new Set([
  'sessions',
  'branches',
  'worktrees',
  'pullRequests',
]);
const EXCLUDED_OWNERSHIP = new Set(['non-issue', 'out-of-scope']);
const INACTIVE_STATES = new Set([
  'archived',
  'closed',
  'failed',
  'ineligible',
  'merged',
  'released',
  'unlaunched',
]);

function validDate(value) {
  if (typeof value !== 'string') return null;
  const match = value.match(
    /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})(?:\.(\d{1,3}))?(Z|[+-]\d{2}:\d{2})$/,
  );
  if (!match) return null;

  const [, year, month, day, hour, minute, second, fraction = '', zone] = match;
  const wall = `${year}-${month}-${day}T${hour}:${minute}:${second}.${fraction.padEnd(3, '0')}Z`;
  const wallTimestamp = Date.parse(wall);
  if (!Number.isFinite(wallTimestamp)
      || new Date(wallTimestamp).toISOString() !== wall) {
    return null;
  }
  if (zone === 'Z') return wallTimestamp;

  const sign = zone[0] === '+' ? 1 : -1;
  const offsetHour = Number(zone.slice(1, 3));
  const offsetMinute = Number(zone.slice(4, 6));
  if (offsetHour > 23 || offsetMinute > 59) return null;
  return wallTimestamp - sign * ((offsetHour * 60) + offsetMinute) * 60_000;
}

function positiveInteger(value) {
  return Number.isInteger(value) && value > 0;
}

function writerAuthorizationCurrent(sectionZero, now, batchId = null) {
  const current = validDate(now);
  const checkedAt = validDate(sectionZero?.checkedAt);
  return Boolean(
    sectionZero?.allowed === true
    && sectionZero.mode === 'writer'
    && sectionZero.authorization?.mechanism === 'single-coordinator-process-guard'
    && current !== null
    && checkedAt === current
    && (batchId === null || sectionZero.authorization.batchId === batchId),
  );
}

function activeRecord(record) {
  return record?.active !== false
    && !INACTIVE_STATES.has(String(record?.state || '').toLowerCase());
}

function ownershipEvidenceIssues(source, record) {
  const issues = positiveInteger(record.issue) ? [record.issue] : [];
  if (source !== 'pullRequests'
      || !Object.prototype.hasOwnProperty.call(record, 'closingIssues')) {
    return { valid: true, issues };
  }
  if (!Array.isArray(record.closingIssues)
      || record.closingIssues.some((issue) => !positiveInteger(issue))
      || new Set(record.closingIssues).size !== record.closingIssues.length) {
    return { valid: false, reason: 'repository-reconciliation-ambiguous' };
  }
  return {
    valid: true,
    issues: [...new Set([...issues, ...record.closingIssues])],
  };
}

function classifyReconciliationRecord(source, record) {
  if (!record || typeof record !== 'object' || Array.isArray(record)) {
    return { valid: false, reason: 'repository-reconciliation-invalid' };
  }

  const evidence = ownershipEvidenceIssues(source, record);
  if (!evidence.valid) return evidence;
  if (evidence.issues.length > 0) {
    if (source === 'worktrees'
        && activeRecord(record)
        && typeof record.dirty !== 'boolean') {
      return { valid: false, reason: 'repository-reconciliation-ambiguous' };
    }
    return { valid: true, ownership: 'issue', issues: evidence.issues };
  }

  const explicitlyExcluded = REPOSITORY_ARTIFACT_COLLECTIONS.has(source)
    && record.issue === null
    && record.writing === false
    && EXCLUDED_OWNERSHIP.has(record.ownership);
  if (!explicitlyExcluded) {
    return { valid: false, reason: 'repository-reconciliation-ambiguous' };
  }
  if (source === 'worktrees'
      && activeRecord(record)
      && typeof record.dirty !== 'boolean') {
    return { valid: false, reason: 'repository-reconciliation-ambiguous' };
  }
  return { valid: true, ownership: record.ownership, excluded: true };
}

function assessSingleCoordinatorProcessGuard(reconciliation, {
  now,
  repository,
  coordinatorId,
  batchId = null,
  issue = null,
  admissionToken = null,
} = {}) {
  const current = validDate(now);
  const checkedAt = validDate(reconciliation?.checkedAt);
  const shapeValid = reconciliation
    && typeof reconciliation === 'object'
    && !Array.isArray(reconciliation)
    && reconciliation.complete === true
    && reconciliation.repository === repository
    && /^[^/\s]+\/[^/\s]+$/.test(repository || '')
    && typeof coordinatorId === 'string'
    && coordinatorId.trim()
    && current !== null
    && checkedAt === current
    && RECONCILIATION_COLLECTIONS.every(
      (name) => Array.isArray(reconciliation[name]),
    );
  if (!shapeValid) {
    return {
      available: false,
      mode: 'blocked',
      reason: 'repository-reconciliation-incomplete',
    };
  }

  const records = RECONCILIATION_COLLECTIONS.flatMap((name) =>
    reconciliation[name].map((record) => ({
      name,
      record,
      classification: classifyReconciliationRecord(name, record),
    })));
  const invalid = records.find(({ classification }) => !classification.valid);
  if (invalid) {
    return {
      available: false,
      mode: 'blocked',
      reason: invalid.classification.reason,
      source: invalid.name,
    };
  }

  const activeArtifacts = records.filter(({ name, record, classification }) =>
    REPOSITORY_ARTIFACT_COLLECTIONS.has(name)
      && classification.ownership === 'issue'
      && activeRecord(record));
  const activeOwnershipClaims = activeArtifacts.flatMap(
    ({ name, classification }) =>
      classification.issues.map((ownedIssue) => ({ name, issue: ownedIssue })),
  );
  const seenIssues = new Set();
  const duplicateArtifact = activeOwnershipClaims.find(({ issue: ownedIssue }) => {
    if (seenIssues.has(ownedIssue)) return true;
    seenIssues.add(ownedIssue);
    return false;
  });
  if (duplicateArtifact) {
    return {
      available: false,
      mode: 'blocked',
      reason: 'duplicate-reconciliation-conflict',
      issue: duplicateArtifact.issue,
      sources: activeOwnershipClaims
        .filter(({ issue: ownedIssue }) => ownedIssue === duplicateArtifact.issue)
        .map(({ name }) => name),
    };
  }

  const dirtyIssueWorktree = records.find(({ name, record, classification }) =>
    name === 'worktrees'
      && classification.ownership === 'issue'
      && activeRecord(record)
      && record.dirty === true);
  if (dirtyIssueWorktree) {
    return {
      available: false,
      mode: 'blocked',
      reason: 'dirty-issue-worktree-conflict',
      issue: dirtyIssueWorktree.record.issue,
    };
  }

  const activeCoordination = [
    ...reconciliation.reservations,
    ...reconciliation.ledger,
  ].filter(activeRecord);
  const foreign = activeCoordination.find(
    (record) => record.coordinatorId !== coordinatorId,
  );
  if (foreign) {
    return {
      available: false,
      mode: 'blocked',
      reason: 'coordinator-reconciliation-conflict',
      issue: foreign.issue,
    };
  }

  const readiness = new Map();
  for (const record of reconciliation.issueReadiness) {
    if (readiness.has(record.issue)) {
      return {
        available: false,
        mode: 'blocked',
        reason: 'issue-readiness-duplicate',
        issue: record.issue,
      };
    }
    readiness.set(record.issue, record.ready === true);
  }
  const occupiedIssues = new Set(
    activeOwnershipClaims.map(({ issue: ownedIssue }) => ownedIssue),
  );
  const excludedRecords = records
    .filter(({ classification }) => classification.excluded)
    .map(({ name, record, classification }) => ({
      source: name,
      ownership: classification.ownership,
      active: activeRecord(record),
      ...(name === 'worktrees' ? { dirty: record.dirty } : {}),
    }));

  if (issue !== null) {
    if (!positiveInteger(issue) || readiness.get(issue) !== true) {
      return {
        available: false,
        mode: 'blocked',
        reason: 'issue-eligibility-changed',
        issue,
      };
    }
    if (occupiedIssues.has(issue)) {
      return {
        available: false,
        mode: 'blocked',
        reason: 'issue-already-active',
        issue,
      };
    }
    for (const name of ['reservations', 'ledger']) {
      const matching = reconciliation[name]
        .filter(activeRecord)
        .filter((record) => record.issue === issue);
      if (matching.length > 1 || matching.some((record) =>
        record.coordinatorId !== coordinatorId
        || (batchId !== null && record.batchId !== batchId)
        || (admissionToken !== null && record.admissionToken !== admissionToken))) {
        return {
          available: false,
          mode: 'blocked',
          reason: 'admission-reconciliation-conflict',
          source: name,
          issue,
        };
      }
    }
  }

  return {
    available: true,
    mode: 'writer',
    reason: 'single-coordinator-process-guard-clear',
    mechanism: 'single-coordinator-process-guard',
    checkedAt: new Date(current).toISOString(),
    guardId: `${repository}:${coordinatorId}:${new Date(current).toISOString()}`,
    repository,
    coordinatorId,
    readyIssues: [...readiness.entries()]
      .filter(([, ready]) => ready)
      .map(([readyIssue]) => readyIssue)
      .sort((left, right) => left - right),
    occupiedIssues: [...occupiedIssues].sort((left, right) => left - right),
    excludedRecords,
  };
}

function assessSectionZero({
  operation,
  stateAvailable = false,
  enumerationComplete = false,
  retrospectiveAllowed = false,
  reconciliation,
  now,
  repository,
  coordinatorId,
  batchId,
}) {
  if (!QUEUE_OPERATIONS.has(operation)) {
    return { allowed: false, mode: 'blocked', reason: 'unknown-queue-operation' };
  }
  if (stateAvailable !== true) {
    return { allowed: false, mode: 'blocked', reason: 'state-backend-unavailable' };
  }
  if (enumerationComplete !== true) {
    return { allowed: false, mode: 'blocked', reason: 'state-enumeration-incomplete' };
  }
  if (retrospectiveAllowed !== true) {
    return { allowed: false, mode: 'blocked', reason: 'retrospective-not-current' };
  }

  if (!WRITER_OPERATIONS.has(operation)) {
    return { allowed: true, mode: 'read-only', reason: 'section-zero-passed' };
  }

  if (typeof batchId !== 'string' || !batchId.trim()) {
    return { allowed: false, mode: 'blocked', reason: 'batch-id-required' };
  }
  const guard = assessSingleCoordinatorProcessGuard(reconciliation, {
    now,
    repository,
    coordinatorId,
    batchId,
  });
  if (!guard.available) {
    return { allowed: false, mode: 'blocked', reason: guard.reason };
  }
  return {
    allowed: true,
    mode: 'writer',
    reason: 'section-zero-passed',
    checkedAt: guard.checkedAt,
    authorization: {
      mechanism: guard.mechanism,
      guardId: guard.guardId,
      repository: guard.repository,
      coordinatorId: guard.coordinatorId,
      readyIssues: guard.readyIssues,
      occupiedIssues: guard.occupiedIssues,
      excludedRecords: guard.excludedRecords,
      batchId,
    },
  };
}

function createBatch({
  batchId,
  candidates = [],
  verifiedCapacity,
  sectionZero,
  now,
}) {
  if (!writerAuthorizationCurrent(sectionZero, now, batchId)) {
    return { ready: false, reason: 'writer-admission-not-authorized', batch: null };
  }
  if (typeof batchId !== 'string' || !batchId.trim()) {
    return { ready: false, reason: 'batch-id-required', batch: null };
  }
  if (!sectionZero.authorization
      || sectionZero.authorization.batchId !== batchId) {
    return { ready: false, reason: 'batch-authorization-invalid', batch: null };
  }
  if (!positiveInteger(verifiedCapacity)) {
    return { ready: false, reason: 'verified-capacity-required', batch: null };
  }

  const limit = Math.min(MAX_BATCH_SIZE, verifiedCapacity);
  const selected = candidates.slice(0, limit);
  if (selected.length === 0) {
    return { ready: false, reason: 'no-ready-candidates', batch: null };
  }
  if (selected.some((candidate) =>
    !candidate
    || !positiveInteger(candidate.issue)
    || typeof candidate.admissionToken !== 'string'
    || !candidate.admissionToken.trim())) {
    return { ready: false, reason: 'candidate-correlation-invalid', batch: null };
  }
  if (new Set(selected.map((candidate) => candidate.issue)).size !== selected.length
      || new Set(selected.map((candidate) => candidate.admissionToken)).size !== selected.length) {
    return { ready: false, reason: 'candidate-correlation-duplicate', batch: null };
  }
  if (selected.some((candidate) =>
    !sectionZero.authorization.readyIssues.includes(candidate.issue)
    || sectionZero.authorization.occupiedIssues.includes(candidate.issue))) {
    return { ready: false, reason: 'candidate-reconciliation-conflict', batch: null };
  }

  const preparedAt = new Date(validDate(now)).toISOString();
  return {
    ready: true,
    reason: 'wave-prepared',
    batch: {
      id: batchId,
      limit,
      authorization: { ...sectionZero.authorization },
      launchState: 'launching',
      preparedAt,
      launchClosedAt: null,
      stopReason: null,
      ackInspectionAt: null,
      admissions: selected.map((candidate) => ({
        ...candidate,
        batchId,
        guardToken: candidate.admissionToken,
        state: 'prepared',
        preparedAt,
        attemptedAt: null,
        createdAt: null,
      })),
    },
  };
}

function assessSpawnAttempt({
  now,
  previousAttemptAt = null,
  sectionZero,
  batchId,
  admission,
  duplicateCheck = false,
  collisionCheck = false,
  issueReady = false,
  capacityAvailable = false,
  creationOutcome = 'not-started',
  reconciliation,
}) {
  if (!writerAuthorizationCurrent(sectionZero, now, batchId)) {
    return { allowed: false, reason: 'writer-admission-not-authorized' };
  }
  if (duplicateCheck !== true) {
    return { allowed: false, reason: 'duplicate-check-incomplete' };
  }
  if (collisionCheck !== true) {
    return { allowed: false, reason: 'collision-check-incomplete' };
  }
  if (issueReady !== true) {
    return { allowed: false, reason: 'issue-eligibility-changed' };
  }
  if (capacityAvailable !== true) {
    return { allowed: false, reason: 'verified-capacity-unavailable' };
  }
  const guard = assessSingleCoordinatorProcessGuard(reconciliation, {
    now,
    repository: sectionZero.authorization.repository,
    coordinatorId: sectionZero.authorization.coordinatorId,
    batchId,
    issue: admission?.issue,
    admissionToken: admission?.admissionToken,
  });
  if (!guard.available) {
    return { allowed: false, reason: guard.reason };
  }
  if (!admission
      || admission.batchId !== batchId
      || admission.state !== 'prepared'
      || admission.guardToken !== admission.admissionToken
      || typeof admission.admissionToken !== 'string'
      || !admission.admissionToken) {
    return { allowed: false, reason: 'issue-guard-token-invalid' };
  }
  if (!['not-started', 'definitive-non-creation'].includes(creationOutcome)) {
    return { allowed: false, reason: 'creation-outcome-ambiguous' };
  }

  const current = validDate(now);
  const previous = previousAttemptAt === null ? null : validDate(previousAttemptAt);
  if (current === null || (previousAttemptAt !== null && previous === null)) {
    return { allowed: false, reason: 'spawn-timestamp-invalid' };
  }
  if (previous !== null && current - previous < MIN_SPAWN_SPACING_MILLISECONDS) {
    return {
      allowed: false,
      reason: 'spawn-spacing-not-elapsed',
      wakeAt: new Date(previous + MIN_SPAWN_SPACING_MILLISECONDS).toISOString(),
      nextAction: 'ONE_TIME_WAKE_OR_NEXT_TICK_REQUIRED',
    };
  }
  return {
    allowed: true,
    reason: 'spawn-attempt-authorized',
    batchId,
    admissionToken: admission.admissionToken,
    attemptedAt: new Date(current).toISOString(),
    previousAttemptAt: previous === null ? null : new Date(previous).toISOString(),
  };
}

function recordCreationOutcome({
  batch,
  admissionToken,
  outcome,
  attemptedAt,
  child = {},
  fallback = false,
}) {
  const attempt = validDate(attemptedAt);
  if (!batch || !Array.isArray(batch.admissions) || attempt === null) {
    return { recorded: false, reason: 'wave-or-attempt-invalid', batch: null };
  }
  const index = batch.admissions.findIndex(
    (admission) => admission.admissionToken === admissionToken,
  );
  if (index < 0) {
    return { recorded: false, reason: 'admission-not-found', batch: null };
  }

  const current = batch.admissions[index];
  const stateAllowed = current.state === 'prepared'
    || (fallback === true && current.state === 'failed');
  if (!stateAllowed) {
    return { recorded: false, reason: 'admission-already-resolved', batch: null };
  }
  const supported = new Set([
    'created',
    'definitive-non-creation',
    'uncertain',
    'eligibility-changed',
    'capacity-lost',
    'safety-gate-failed',
  ]);
  if (!supported.has(outcome)) {
    return { recorded: false, reason: 'creation-outcome-invalid', batch: null };
  }
  const prepared = validDate(current.preparedAt);
  if (prepared === null || attempt < prepared) {
    return { recorded: false, reason: 'attempt-precedes-admission', batch: null };
  }
  if (['created', 'definitive-non-creation', 'uncertain'].includes(outcome)) {
    const previousAttempts = batch.admissions
      .map((item) => validDate(item.attemptedAt))
      .filter((timestamp) => timestamp !== null);
    const previousAttempt = previousAttempts.length === 0
      ? null
      : Math.max(...previousAttempts);
    if (previousAttempt !== null
        && attempt - previousAttempt < MIN_SPAWN_SPACING_MILLISECONDS) {
      return { recorded: false, reason: 'attempt-spacing-invalid', batch: null };
    }
  }

  const next = {
    ...batch,
    admissions: batch.admissions.map((admission) => ({ ...admission })),
  };
  const admission = next.admissions[index];
  admission.attemptedAt = new Date(attempt).toISOString();

  if (outcome === 'created') {
    const required = ['session', 'location', 'branch', 'workspace', 'base'];
    if (required.some((field) => typeof child[field] !== 'string' || !child[field])) {
      return { recorded: false, reason: 'created-child-identity-invalid', batch: null };
    }
    Object.assign(admission, child, {
      state: 'created',
      createdAt: admission.attemptedAt,
    });
    if (next.launchState !== 'stopped'
        && next.admissions.every((item) => item.state !== 'prepared')) {
      next.launchState = 'complete';
      next.launchClosedAt = admission.attemptedAt;
    }
    return { recorded: true, reason: 'child-created', batch: next };
  }

  const stateByOutcome = {
    'definitive-non-creation': 'failed',
    uncertain: 'ambiguous',
    'eligibility-changed': 'ineligible',
    'capacity-lost': 'unlaunched',
    'safety-gate-failed': 'unlaunched',
  };
  admission.state = stateByOutcome[outcome];
  for (let remaining = index + 1; remaining < next.admissions.length; remaining += 1) {
    if (next.admissions[remaining].state === 'prepared') {
      next.admissions[remaining].state = 'unlaunched';
    }
  }
  next.launchState = 'stopped';
  next.launchClosedAt = admission.attemptedAt;
  next.stopReason = outcome;
  return { recorded: true, reason: 'wave-stopped', batch: next };
}

function recordAckInspection({ batch, inspectedAt }) {
  const inspected = validDate(inspectedAt);
  const launchClosed = validDate(batch?.launchClosedAt);
  if (!batch || inspected === null || launchClosed === null || inspected < launchClosed) {
    return { recorded: false, reason: 'ack-inspection-invalid', batch: null };
  }
  if (batch.ackInspectionAt !== null) {
    return { recorded: false, reason: 'ack-inspection-already-recorded', batch: null };
  }
  return {
    recorded: true,
    reason: 'ack-inspection-recorded',
    batch: {
      ...batch,
      admissions: batch.admissions.map((admission) => ({ ...admission })),
      ackInspectionAt: new Date(inspected).toISOString(),
    },
  };
}

function validateAck(expected, ack) {
  if (!expected || !ack || typeof ack !== 'object' || Array.isArray(ack)) {
    return { valid: false, kind: 'invalid', reason: 'ack-missing' };
  }
  const exactFields = [
    'issue',
    'batchId',
    'admissionToken',
    'session',
    'location',
    'branch',
    'workspace',
    'base',
  ];
  const mismatch = exactFields.find((field) =>
    expected[field] === undefined || ack[field] !== expected[field]);
  if (mismatch) {
    return {
      valid: false,
      kind: 'invalid',
      reason: `ack-correlation-mismatch:${mismatch}`,
    };
  }
  if (ack.duplicate_check !== 'clear') {
    return { valid: false, kind: 'negative', reason: 'ack-duplicate-check-not-clear' };
  }
  if (ack.collision_check !== 'clear') {
    return { valid: false, kind: 'negative', reason: 'ack-collision-check-not-clear' };
  }
  if (typeof ack.ready !== 'boolean') {
    return { valid: false, kind: 'invalid', reason: 'ack-ready-invalid' };
  }
  if (ack.ready !== true) {
    return { valid: false, kind: 'negative', reason: 'ack-not-ready' };
  }
  if (ack.blocker !== 'none') {
    return { valid: false, kind: 'negative', reason: 'ack-blocked' };
  }
  return { valid: true, kind: 'valid', reason: 'ack-valid' };
}

function assessBatchAdvance({
  batch,
  acknowledgements = [],
  now,
  sectionZero,
  restarted = false,
}) {
  if (!writerAuthorizationCurrent(sectionZero, now, batch?.id || null)) {
    return {
      allowed: false,
      releaseChildren: false,
      reason: 'writer-admission-not-authorized',
    };
  }
  if (!batch || !Array.isArray(batch.admissions) || batch.admissions.length === 0) {
    return { allowed: false, releaseChildren: false, reason: 'batch-invalid' };
  }
  const current = validDate(now);
  const launchClosedAt = validDate(batch.launchClosedAt);
  if (current === null) {
    return { allowed: false, releaseChildren: false, reason: 'ack-timestamp-invalid' };
  }
  const batchIdValid =
    typeof batch.id === 'string'
    && batch.id.length > 0
    && batch.admissions.every((admission) => admission?.batchId === batch.id);
  if (!batchIdValid) {
    return { allowed: false, releaseChildren: false, reason: 'batch-correlation-invalid' };
  }
  const authorization = sectionZero.authorization;
  const batchAuthorization = batch.authorization;
  const authorizationCurrent =
    validDate(sectionZero.checkedAt) === current
    && authorization?.mechanism === 'single-coordinator-process-guard'
    && authorization?.batchId === batch.id
    && batchAuthorization?.batchId === batch.id
    && authorization?.repository === batchAuthorization?.repository
    && authorization?.coordinatorId === batchAuthorization?.coordinatorId;
  if (!authorizationCurrent) {
    return {
      allowed: false,
      releaseChildren: false,
      reason: 'batch-authorization-stale-or-mismatched',
    };
  }
  const created = batch.admissions.filter((admission) => admission.state === 'created');
  const ackByToken = new Map();
  for (const ack of acknowledgements) {
    if (!ack || typeof ack.admissionToken !== 'string'
        || ackByToken.has(ack.admissionToken)) {
      return { allowed: false, releaseChildren: false, reason: 'wave-ack-invalid' };
    }
    ackByToken.set(ack.admissionToken, ack);
  }

  const missing = [];
  for (const admission of created) {
    const ack = ackByToken.get(admission.admissionToken);
    if (!ack) {
      missing.push(admission.admissionToken);
      continue;
    }
    const validation = validateAck(admission, ack);
    if (!validation.valid) {
      return {
        allowed: false,
        releaseChildren: false,
        beginNextWave: false,
        reason: validation.kind === 'negative'
          ? 'wave-ack-negative'
          : 'wave-ack-invalid',
        token: admission.admissionToken,
        detail: validation.reason,
      };
    }
  }

  const createdTokens = new Set(created.map((admission) => admission.admissionToken));
  if ([...ackByToken.keys()].some((token) => !createdTokens.has(token))) {
    return {
      allowed: false,
      releaseChildren: false,
      beginNextWave: false,
      reason: 'wave-ack-invalid',
    };
  }
  if (!['complete', 'stopped'].includes(batch.launchState)
      || launchClosedAt === null) {
    return {
      allowed: false,
      releaseChildren: false,
      beginNextWave: false,
      reason: 'wave-launch-incomplete',
    };
  }

  const inspection = validDate(batch.ackInspectionAt);
  if (batch.ackInspectionAt !== null
      && (inspection === null || inspection < launchClosedAt || inspection > current)) {
    return {
      allowed: false,
      releaseChildren: false,
      beginNextWave: false,
      reason: 'ack-inspection-invalid',
    };
  }
  const inspectionRequired = restarted === true
    || current >= launchClosedAt + ACK_TIMEOUT_MILLISECONDS;
  if (inspectionRequired && inspection === null) {
    return {
      allowed: false,
      releaseChildren: false,
      beginNextWave: false,
      replaceChildren: false,
      inspectOnce: true,
      reason: restarted === true
        ? 'restart-inspect-required'
        : 'ack-timeout-inspect-required',
    };
  }

  if (missing.length > 0) {
    return {
      allowed: false,
      releaseChildren: false,
      beginNextWave: false,
      replaceChildren: false,
      inspectOnce: false,
      reason: inspection === null
        ? 'wave-acks-pending'
        : 'wave-acks-pending-after-inspection',
      missing,
    };
  }

  if (batch.launchState === 'stopped') {
    return {
      allowed: false,
      releaseChildren: created.length > 0,
      beginNextWave: false,
      replaceChildren: false,
      reason: created.length > 0
        ? 'stopped-partial-wave-acks-valid'
        : 'stopped-wave-no-created-children',
      stopReason: batch.stopReason,
    };
  }

  return {
    allowed: true,
    releaseChildren: true,
    beginNextWave: true,
    reason: 'all-created-child-acks-valid',
  };
}

function assessLocalFallback({
  admissionToken,
  originalAdmissionToken,
  batchId,
  cloudAttemptAt,
  cloudOutcome,
  localFallbackAttempts = 0,
  spawnAttempt,
}) {
  if (cloudOutcome === 'uncertain') {
    return { allowed: false, reason: 'ambiguous-creation-reconcile-required' };
  }
  if (cloudOutcome !== 'definitive-non-creation') {
    return { allowed: false, reason: 'cloud-non-creation-not-proven' };
  }
  if (admissionToken !== originalAdmissionToken) {
    return { allowed: false, reason: 'fallback-token-mismatch' };
  }
  if (localFallbackAttempts !== 0) {
    return { allowed: false, reason: 'fallback-already-attempted' };
  }
  if (!spawnAttempt || spawnAttempt.allowed !== true) {
    return { allowed: false, reason: spawnAttempt?.reason || 'spawn-attempt-not-authorized' };
  }
  const cloudAttempt = validDate(cloudAttemptAt);
  if (typeof batchId !== 'string'
      || !batchId
      || cloudAttempt === null
      || spawnAttempt.batchId !== batchId
      || spawnAttempt.admissionToken !== admissionToken
      || spawnAttempt.previousAttemptAt !== new Date(cloudAttempt).toISOString()) {
    return { allowed: false, reason: 'fallback-attempt-correlation-invalid' };
  }
  return { allowed: true, reason: 'single-local-fallback-authorized' };
}

function mergeDisposition({ approved = false, checksPassing = false } = {}) {
  if (approved !== true || checksPassing !== true) {
    return { action: 'wait', reason: 'review-or-checks-incomplete' };
  }
  return { action: 'READY_FOR_AGENT_MERGE', reason: 'app-landing-required' };
}

module.exports = {
  ACK_TIMEOUT_MILLISECONDS,
  MAX_BATCH_SIZE,
  MIN_SPAWN_SPACING_MILLISECONDS,
  assessBatchAdvance,
  assessLocalFallback,
  assessSectionZero,
  assessSingleCoordinatorProcessGuard,
  assessSpawnAttempt,
  createBatch,
  mergeDisposition,
  recordAckInspection,
  recordCreationOutcome,
  validateAck,
};
