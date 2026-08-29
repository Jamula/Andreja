'use strict';

const MAX_BATCH_SIZE = 5;
const MIN_SPAWN_SPACING_MILLISECONDS = 10_000;
const ACK_TIMEOUT_MILLISECONDS = 5 * 60 * 1000;
const QUEUE_OPERATIONS = new Set([
  'enumerate',
  'status',
  'classify',
  'admit',
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
  const validUntil = validDate(sectionZero?.authorization?.validUntil);
  return Boolean(
    sectionZero?.allowed === true
    && sectionZero.mode === 'writer'
    && current !== null
    && checkedAt === current
    && validUntil !== null
    && current < validUntil
    && (batchId === null || sectionZero.authorization.batchId === batchId),
  );
}

function assessAtomicOwnership(capability, {
  now,
  repository,
  ownerId,
} = {}) {
  if (!capability || typeof capability !== 'object' || Array.isArray(capability)) {
    return { available: false, mode: 'read-only', reason: 'atomic-ownership-unavailable' };
  }

  const common =
    capability.verified === true
    && capability.repositoryScoped === true
    && capability.repository === repository
    && capability.ownerId === ownerId
    && typeof capability.grantId === 'string'
    && capability.grantId.length > 0
    && typeof capability.tool === 'string'
    && capability.tool.length > 0;
  const lease =
    capability.kind === 'lease'
    && capability.acquire === true
    && capability.renew === true
    && capability.release === true;
  const conditionalCreate =
    capability.kind === 'conditional-create'
    && capability.createIfAbsent === true
    && capability.conflictIsFailure === true;

  const current = validDate(now);
  const verifiedAt = validDate(capability.verifiedAt);
  const validUntil = validDate(capability.validUntil);
  const currentGrant = current !== null
    && verifiedAt !== null
    && validUntil !== null
    && verifiedAt <= current
    && current < validUntil;

  if (!common || !currentGrant || (!lease && !conditionalCreate)) {
    return { available: false, mode: 'read-only', reason: 'atomic-ownership-unavailable' };
  }

  return {
    available: true,
    mode: 'writer',
    reason: 'atomic-ownership-verified',
    kind: capability.kind,
    tool: capability.tool,
    repository,
    ownerId,
    grantId: capability.grantId,
    validUntil: new Date(validUntil).toISOString(),
  };
}

function assessSectionZero({
  operation,
  stateAvailable = false,
  enumerationComplete = false,
  retrospectiveAllowed = false,
  ownershipCapability,
  now,
  repository,
  ownerId,
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

  const ownership = assessAtomicOwnership(ownershipCapability, {
    now,
    repository,
    ownerId,
  });
  if (!ownership.available) {
    if (operation === 'admit') {
      return { allowed: false, mode: 'read-only', reason: ownership.reason };
    }
    return { allowed: true, mode: 'read-only', reason: ownership.reason };
  }

  if (operation === 'admit' && (typeof batchId !== 'string' || !batchId.trim())) {
    return { allowed: false, mode: 'blocked', reason: 'batch-id-required' };
  }
  return {
    allowed: true,
    mode: 'writer',
    reason: 'section-zero-passed',
    checkedAt: new Date(validDate(now)).toISOString(),
    authorization: {
      repository: ownership.repository,
      ownerId: ownership.ownerId,
      grantId: ownership.grantId,
      validUntil: ownership.validUntil,
      batchId: operation === 'admit' ? batchId : null,
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

  const reservedAt = new Date(validDate(now)).toISOString();
  return {
    ready: true,
    reason: 'wave-reserved',
    batch: {
      id: batchId,
      limit,
      authorization: { ...sectionZero.authorization },
      launchState: 'launching',
      reservedAt,
      launchClosedAt: null,
      stopReason: null,
      ackInspectionAt: null,
      admissions: selected.map((candidate) => ({
        ...candidate,
        batchId,
        reservationId: candidate.admissionToken,
        state: 'reserved',
        reservedAt,
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
  if (!admission
      || admission.batchId !== batchId
      || admission.state !== 'reserved'
      || admission.reservationId !== admission.admissionToken
      || typeof admission.admissionToken !== 'string'
      || !admission.admissionToken) {
    return { allowed: false, reason: 'issue-reservation-invalid' };
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
    return { recorded: false, reason: 'reservation-not-found', batch: null };
  }

  const current = batch.admissions[index];
  const stateAllowed = current.state === 'reserved'
    || (fallback === true && current.state === 'failed');
  if (!stateAllowed) {
    return { recorded: false, reason: 'reservation-already-resolved', batch: null };
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
  const reserved = validDate(current.reservedAt);
  if (reserved === null || attempt < reserved) {
    return { recorded: false, reason: 'attempt-precedes-reservation', batch: null };
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
        && next.admissions.every((item) => item.state !== 'reserved')) {
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
    if (next.admissions[remaining].state === 'reserved') {
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
    && validDate(authorization?.validUntil) !== null
    && current < validDate(authorization.validUntil)
    && authorization?.batchId === batch.id
    && batchAuthorization?.batchId === batch.id
    && authorization?.repository === batchAuthorization?.repository
    && authorization?.ownerId === batchAuthorization?.ownerId
    && authorization?.grantId === batchAuthorization?.grantId;
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
  assessAtomicOwnership,
  assessBatchAdvance,
  assessLocalFallback,
  assessSectionZero,
  assessSpawnAttempt,
  createBatch,
  mergeDisposition,
  recordAckInspection,
  recordCreationOutcome,
  validateAck,
};
