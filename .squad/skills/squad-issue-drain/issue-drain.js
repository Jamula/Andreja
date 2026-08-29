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
  const timestamp = typeof value === 'string' ? Date.parse(value) : NaN;
  return Number.isFinite(timestamp) ? timestamp : null;
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

  return {
    ready: true,
    reason: 'batch-ready',
    batch: {
      id: batchId,
      limit,
      authorization: { ...sectionZero.authorization },
      admissions: selected.map((candidate) => ({ ...candidate, batchId })),
    },
  };
}

function assessSpawnAttempt({
  now,
  previousAttemptAt = null,
  sectionZero,
  batchId,
  duplicateCheck = false,
  collisionCheck = false,
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
      retryAt: new Date(previous + MIN_SPAWN_SPACING_MILLISECONDS).toISOString(),
    };
  }
  return { allowed: true, reason: 'spawn-attempt-authorized' };
}

function validateAck(expected, ack) {
  if (!expected || !ack || typeof ack !== 'object' || Array.isArray(ack)) {
    return { valid: false, reason: 'ack-missing' };
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
    return { valid: false, reason: `ack-correlation-mismatch:${mismatch}` };
  }
  if (ack.duplicate_check !== 'clear') {
    return { valid: false, reason: 'ack-duplicate-check-not-clear' };
  }
  if (ack.collision_check !== 'clear') {
    return { valid: false, reason: 'ack-collision-check-not-clear' };
  }
  if (ack.ready !== true) {
    return { valid: false, reason: 'ack-not-ready' };
  }
  if (ack.blocker !== 'none') {
    return { valid: false, reason: 'ack-blocked' };
  }
  return { valid: true, reason: 'ack-valid' };
}

function assessBatchAdvance({
  batch,
  acknowledgements = [],
  now,
  ackDeadline,
  sectionZero,
  restarted = false,
  reconciliationComplete = false,
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
  const deadline = validDate(ackDeadline);
  if (current === null || deadline === null) {
    return { allowed: false, releaseChildren: false, reason: 'ack-deadline-invalid' };
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
  if ((restarted === true || current >= deadline) && reconciliationComplete !== true) {
    return {
      allowed: false,
      releaseChildren: false,
      reason: current >= deadline
        ? 'ack-timeout-reconcile-required'
        : 'restart-reconcile-required',
    };
  }

  const ackByToken = new Map();
  for (const ack of acknowledgements) {
    if (!ack || typeof ack.admissionToken !== 'string'
        || ackByToken.has(ack.admissionToken)) {
      return { allowed: false, releaseChildren: false, reason: 'ack-set-ambiguous' };
    }
    ackByToken.set(ack.admissionToken, ack);
  }

  const missing = [];
  for (const admission of batch.admissions) {
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
        reason: 'batch-ack-invalid',
        token: admission.admissionToken,
        detail: validation.reason,
      };
    }
  }

  if (missing.length > 0) {
    return {
      allowed: false,
      releaseChildren: false,
      reason: 'batch-acks-pending',
      missing,
    };
  }
  if (ackByToken.size !== batch.admissions.length) {
    return { allowed: false, releaseChildren: false, reason: 'ack-set-ambiguous' };
  }

  return { allowed: true, releaseChildren: true, reason: 'all-batch-acks-valid' };
}

function assessLocalFallback({
  admissionToken,
  originalAdmissionToken,
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
  validateAck,
};
