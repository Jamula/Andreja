'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const {
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
} = require('./issue-drain');

const now = '2026-08-29T19:00:00.000Z';
const writerCapability = {
  verified: true,
  repositoryScoped: true,
  repository: 'Jamula/Andreja',
  ownerId: 'coordinator-1',
  grantId: 'grant-1',
  verifiedAt: '2026-08-29T18:00:00.000Z',
  validUntil: '2026-08-29T20:00:00.000Z',
  kind: 'conditional-create',
  tool: 'runtime.conditionalCreate',
  createIfAbsent: true,
  conflictIsFailure: true,
};

function writerSection(
  operation = 'admit',
  checkedAt = now,
  batchId = 'batch-1',
) {
  return assessSectionZero({
    operation,
    stateAvailable: true,
    enumerationComplete: true,
    retrospectiveAllowed: true,
    ownershipCapability: writerCapability,
    now: checkedAt,
    repository: 'Jamula/Andreja',
    ownerId: 'coordinator-1',
    batchId,
  });
}

function admission(issue) {
  return {
    issue,
    batchId: 'batch-1',
    admissionToken: `batch-1:${issue}`,
    session: `session-${issue}`,
    location: 'local',
    branch: `squad/${issue}-work`,
    workspace: `C:\\worktrees\\${issue}`,
    base: 'origin/main@abc123',
  };
}

function ackFor(expected, override = {}) {
  return {
    ...expected,
    duplicate_check: 'clear',
    collision_check: 'clear',
    ready: true,
    blocker: 'none',
    ...override,
  };
}

function reserveBatch(
  issues = [1, 2],
  batchId = 'batch-1',
  verifiedCapacity = 5,
  reservedAt = '2026-08-29T18:59:00.000Z',
) {
  const result = createBatch({
    batchId,
    candidates: issues.map(admission),
    verifiedCapacity,
    sectionZero: writerSection('admit', reservedAt, batchId),
    now: reservedAt,
  });
  assert.equal(result.ready, true);
  return result.batch;
}

function recordCreated(batch, admissionToken, attemptedAt = now, fallback = false) {
  const expected = batch.admissions.find(
    (item) => item.admissionToken === admissionToken,
  );
  const result = recordCreationOutcome({
    batch,
    admissionToken,
    attemptedAt,
    fallback,
    outcome: 'created',
    child: {
      session: expected.session,
      location: expected.location,
      branch: expected.branch,
      workspace: expected.workspace,
      base: expected.base,
    },
  });
  assert.equal(result.recorded, true);
  return result.batch;
}

function closeCreatedWave(batch, attemptedAt = now) {
  const finalAttempt = Date.parse(attemptedAt);
  return batch.admissions.reduce((current, item, index) => {
    const milliseconds = finalAttempt
      - ((batch.admissions.length - index - 1) * MIN_SPAWN_SPACING_MILLISECONDS);
    return recordCreated(
      current,
      item.admissionToken,
      new Date(milliseconds).toISOString(),
    );
  }, batch);
}

test('atomic ownership must be explicit, scoped, current, and verified', () => {
  assert.deepEqual(assessAtomicOwnership(), {
    available: false,
    mode: 'read-only',
    reason: 'atomic-ownership-unavailable',
  });
  for (const override of [
    { verified: false },
    { repositoryScoped: false },
    { createIfAbsent: false },
    { conflictIsFailure: false },
    { repository: 'other/repository' },
    { ownerId: 'other-owner' },
    { grantId: '' },
    { validUntil: now },
  ]) {
    assert.equal(
      assessAtomicOwnership(
        { ...writerCapability, ...override },
        { now, repository: 'Jamula/Andreja', ownerId: 'coordinator-1' },
      ).available,
      false,
    );
  }
  assert.equal(assessAtomicOwnership(
    writerCapability,
    { now, repository: 'Jamula/Andreja', ownerId: 'coordinator-1' },
  ).available, true);
});

test('the installed runtime exposes no atomic ownership tool and therefore stays read-only', () => {
  const root = path.resolve(__dirname, '../../..');
  const config = JSON.parse(fs.readFileSync(path.join(root, '.mcp.json'), 'utf8'));
  const tools = config.mcpServers.squad_state.tools;
  assert.equal(
    tools.some((name) => /lease|compare|conditional|create.?if.?absent/i.test(name)),
    false,
  );
  assert.deepEqual(assessSectionZero({
    operation: 'admit',
    stateAvailable: true,
    enumerationComplete: true,
    retrospectiveAllowed: true,
  }), {
    allowed: false,
    mode: 'read-only',
    reason: 'atomic-ownership-unavailable',
  });
});

test('Section 0 is mandatory for every queue operation', () => {
  for (const operation of ['enumerate', 'status', 'classify', 'admit']) {
    assert.equal(assessSectionZero({ operation }).allowed, false);
    assert.equal(writerSection(operation).allowed, true);
  }
  assert.equal(assessSectionZero({
    operation: 'status',
    stateAvailable: true,
    enumerationComplete: true,
    retrospectiveAllowed: true,
  }).mode, 'read-only');
});

test('batch size is five and lower verified capacity overrides it', () => {
  const candidates = Array.from({ length: 7 }, (_, index) => admission(index + 1));
  const full = createBatch({
    batchId: 'batch-1',
    candidates,
    verifiedCapacity: 9,
    sectionZero: writerSection(),
    now,
  });
  assert.equal(MAX_BATCH_SIZE, 5);
  assert.equal(full.reason, 'wave-reserved');
  assert.equal(full.batch.admissions.length, 5);
  assert.equal(full.batch.launchState, 'launching');
  assert.ok(full.batch.admissions.every(
    (item) => item.state === 'reserved'
      && item.reservationId === item.admissionToken,
  ));

  const lower = createBatch({
    batchId: 'batch-2',
    candidates,
    verifiedCapacity: 3,
    sectionZero: writerSection('admit', now, 'batch-2'),
    now,
  });
  assert.equal(lower.batch.limit, 3);
  assert.equal(lower.batch.admissions.length, 3);
});

test('spawn attempts require ten elapsed seconds and all safety checks', () => {
  const batch = reserveBatch([1]);
  const base = {
    now,
    previousAttemptAt: '2026-08-29T18:59:50.001Z',
    sectionZero: writerSection(),
    batchId: 'batch-1',
    admission: batch.admissions[0],
    duplicateCheck: true,
    collisionCheck: true,
    issueReady: true,
    capacityAvailable: true,
  };
  assert.equal(MIN_SPAWN_SPACING_MILLISECONDS, 10_000);
  assert.equal(ACK_TIMEOUT_MILLISECONDS, 300_000);
  const waiting = assessSpawnAttempt(base);
  assert.equal(waiting.reason, 'spawn-spacing-not-elapsed');
  assert.equal(waiting.wakeAt, '2026-08-29T19:00:00.001Z');
  assert.equal(waiting.nextAction, 'ONE_TIME_WAKE_OR_NEXT_TICK_REQUIRED');
  const allowed = assessSpawnAttempt({
    ...base,
    previousAttemptAt: '2026-08-29T18:59:50.000Z',
  });
  assert.equal(allowed.allowed, true);
  assert.equal(allowed.admissionToken, batch.admissions[0].admissionToken);
  assert.equal(allowed.attemptedAt, now);
  assert.equal(assessSpawnAttempt({ ...base, duplicateCheck: false }).allowed, false);
  assert.equal(assessSpawnAttempt({ ...base, collisionCheck: false }).allowed, false);
  assert.equal(
    assessSpawnAttempt({ ...base, issueReady: false }).reason,
    'issue-eligibility-changed',
  );
  assert.equal(
    assessSpawnAttempt({ ...base, capacityAvailable: false }).reason,
    'verified-capacity-unavailable',
  );
  assert.equal(
    assessSpawnAttempt({
      ...base,
      admission: { ...base.admission, state: 'created' },
    }).reason,
    'issue-reservation-invalid',
  );
});

test('ACKs are correlated to issue, admission token, ownership, and base', () => {
  const expected = admission(132);
  assert.equal(validateAck(expected, ackFor(expected)).valid, true);
  for (const override of [
    { issue: 133 },
    { batchId: 'other-batch' },
    { admissionToken: 'other' },
    { session: 'other' },
    { branch: 'other' },
    { base: 'origin/main@changed' },
    { ready: 'true' },
  ]) {
    const result = validateAck(expected, ackFor(expected, override));
    assert.equal(result.valid, false);
    assert.equal(result.kind, 'invalid');
  }
  for (const override of [
    { duplicate_check: 'blocked' },
    { collision_check: 'blocked' },
    { ready: false },
    { blocker: 'waiting' },
  ]) {
    const result = validateAck(expected, ackFor(expected, override));
    assert.equal(result.valid, false);
    assert.equal(result.kind, 'negative');
  }
});

test('same-wave spawning continues without waiting for individual ACKs', () => {
  let batch = reserveBatch([1, 2]);
  batch = recordCreated(
    batch,
    batch.admissions[0].admissionToken,
    '2026-08-29T18:59:50.000Z',
  );
  const firstAck = ackFor(batch.admissions[0]);
  const advancement = assessBatchAdvance({
    batch,
    acknowledgements: [firstAck],
    now,
    sectionZero: writerSection(),
  });
  assert.equal(advancement.reason, 'wave-launch-incomplete');
  assert.equal(advancement.releaseChildren, false);
  assert.equal(assessSpawnAttempt({
    now,
    previousAttemptAt: '2026-08-29T18:59:50.000Z',
    sectionZero: writerSection(),
    batchId: batch.id,
    admission: batch.admissions[1],
    duplicateCheck: true,
    collisionCheck: true,
    issueReady: true,
    capacityAvailable: true,
  }).allowed, true);
});

test('every successfully created child ACK is required before wave advancement', () => {
  const batch = closeCreatedWave(reserveBatch([1, 2, 3]));
  const pending = assessBatchAdvance({
    batch,
    acknowledgements: batch.admissions.slice(0, 2).map(ackFor),
    now,
    sectionZero: writerSection(),
  });
  assert.equal(pending.allowed, false);
  assert.equal(pending.releaseChildren, false);
  assert.equal(pending.beginNextWave, false);
  assert.equal(pending.reason, 'wave-acks-pending');

  const complete = assessBatchAdvance({
    batch,
    acknowledgements: batch.admissions.map(ackFor),
    now,
    sectionZero: writerSection(),
  });
  assert.equal(complete.allowed, true);
  assert.equal(complete.releaseChildren, true);
  assert.equal(complete.beginNextWave, true);
  assert.equal(complete.reason, 'all-created-child-acks-valid');
});

test('failed, ambiguous, or ineligible creation stops the remainder of a wave', () => {
  for (const outcome of [
    'definitive-non-creation',
    'uncertain',
    'eligibility-changed',
    'capacity-lost',
    'safety-gate-failed',
  ]) {
    let batch = reserveBatch([1, 2, 3]);
    batch = recordCreated(
      batch,
      batch.admissions[0].admissionToken,
      '2026-08-29T18:59:50.000Z',
    );
    const stopped = recordCreationOutcome({
      batch,
      admissionToken: batch.admissions[1].admissionToken,
      outcome,
      attemptedAt: now,
    });
    assert.equal(stopped.recorded, true);
    assert.equal(stopped.reason, 'wave-stopped');
    assert.equal(stopped.batch.launchState, 'stopped');
    assert.equal(stopped.batch.stopReason, outcome);
    assert.equal(stopped.batch.admissions[0].state, 'created');
    assert.equal(stopped.batch.admissions[2].state, 'unlaunched');

    const barrier = assessBatchAdvance({
      batch: stopped.batch,
      acknowledgements: [ackFor(stopped.batch.admissions[0])],
      now,
      sectionZero: writerSection(),
    });
    assert.equal(barrier.allowed, false);
    assert.equal(barrier.releaseChildren, true);
    assert.equal(barrier.beginNextWave, false);
    assert.equal(barrier.reason, 'stopped-partial-wave-acks-valid');
  }
});

test('stopped partial waves remain distinct from negative and corrupt ACKs', () => {
  let batch = reserveBatch([1, 2, 3]);
  batch = recordCreated(
    batch,
    batch.admissions[0].admissionToken,
    '2026-08-29T18:59:50.000Z',
  );
  batch = recordCreationOutcome({
    batch,
    admissionToken: batch.admissions[1].admissionToken,
    outcome: 'uncertain',
    attemptedAt: now,
  }).batch;

  const negative = assessBatchAdvance({
    batch,
    acknowledgements: [ackFor(batch.admissions[0], {
      ready: false,
      blocker: 'base changed',
    })],
    now,
    sectionZero: writerSection(),
  });
  assert.equal(negative.reason, 'wave-ack-negative');
  assert.equal(negative.releaseChildren, false);

  const corrupt = assessBatchAdvance({
    batch,
    acknowledgements: [ackFor(batch.admissions[0], { session: 'wrong' })],
    now,
    sectionZero: writerSection(),
  });
  assert.equal(corrupt.reason, 'wave-ack-invalid');
  assert.equal(corrupt.releaseChildren, false);
});

test('a stopped wave with no created child cannot advance vacuously', () => {
  let batch = reserveBatch([1, 2]);
  batch = recordCreationOutcome({
    batch,
    admissionToken: batch.admissions[0].admissionToken,
    outcome: 'definitive-non-creation',
    attemptedAt: now,
  }).batch;
  const result = assessBatchAdvance({
    batch,
    acknowledgements: [],
    now,
    sectionZero: writerSection(),
  });
  assert.equal(result.reason, 'stopped-wave-no-created-children');
  assert.equal(result.releaseChildren, false);
  assert.equal(result.beginNextWave, false);
});

test('ACK timeout is derived from wave closure and inspected exactly once', () => {
  let batch = closeCreatedWave(reserveBatch([1, 2]));
  const acknowledgements = [ackFor(batch.admissions[0])];
  assert.equal(assessBatchAdvance({
    batch,
    acknowledgements,
    now: '2026-08-29T19:04:59.999Z',
    ackDeadline: '2099-01-01T00:00:00.000Z',
    sectionZero: writerSection('admit', '2026-08-29T19:04:59.999Z'),
  }).reason, 'wave-acks-pending');

  const timedOut = assessBatchAdvance({
    batch,
    acknowledgements,
    now: '2026-08-29T19:05:00.000Z',
    sectionZero: writerSection('admit', '2026-08-29T19:05:00.000Z'),
  });
  assert.equal(timedOut.reason, 'ack-timeout-inspect-required');
  assert.equal(timedOut.inspectOnce, true);
  assert.equal(timedOut.replaceChildren, false);

  const inspected = recordAckInspection({
    batch,
    inspectedAt: '2026-08-29T19:05:00.000Z',
  });
  assert.equal(inspected.recorded, true);
  batch = inspected.batch;
  assert.equal(recordAckInspection({
    batch,
    inspectedAt: '2026-08-29T19:06:00.000Z',
  }).reason, 'ack-inspection-already-recorded');
  assert.equal(assessBatchAdvance({
    batch,
    acknowledgements,
    now: '2026-08-29T19:05:00.000Z',
    sectionZero: writerSection('admit', '2026-08-29T19:05:00.000Z'),
  }).reason, 'wave-acks-pending-after-inspection');
  assert.equal(assessBatchAdvance({
    batch,
    acknowledgements: batch.admissions.map(ackFor),
    now: '2026-08-29T19:05:00.000Z',
    sectionZero: writerSection('admit', '2026-08-29T19:05:00.000Z'),
  }).allowed, true);
});

test('restart requires the same inspect-once path and never replacement', () => {
  const batch = closeCreatedWave(reserveBatch([1, 2]));
  const result = assessBatchAdvance({
    batch,
    acknowledgements: [ackFor(batch.admissions[0])],
    now,
    sectionZero: writerSection(),
    restarted: true,
  });
  assert.equal(result.reason, 'restart-inspect-required');
  assert.equal(result.inspectOnce, true);
  assert.equal(result.replaceChildren, false);
});

test('batch release requires current writer authorization', () => {
  const batch = closeCreatedWave(reserveBatch([1]));
  const result = assessBatchAdvance({
    batch,
    acknowledgements: batch.admissions.map(ackFor),
    now,
    sectionZero: assessSectionZero({
      operation: 'admit',
      stateAvailable: true,
      enumerationComplete: true,
      retrospectiveAllowed: true,
      now,
      repository: 'Jamula/Andreja',
      ownerId: 'coordinator-1',
      batchId: 'batch-1',
    }),
  });
  assert.equal(result.allowed, false);
  assert.equal(result.releaseChildren, false);
  assert.equal(result.reason, 'writer-admission-not-authorized');
});

test('batch release rejects mismatched embedded batch identity', () => {
  const valid = closeCreatedWave(reserveBatch([1]));
  for (const batch of [
    {
      ...valid,
      admissions: [{ ...valid.admissions[0], batchId: 'other-batch' }],
    },
    {
      ...valid,
      authorization: {
        ...valid.authorization,
        batchId: 'other-batch',
      },
    },
  ]) {
    const result = assessBatchAdvance({
      batch,
      acknowledgements: batch.admissions.map(ackFor),
      now,
      sectionZero: writerSection(),
    });
    assert.equal(result.allowed, false);
    assert.match(result.reason, /^batch-(?:correlation|authorization)/);
  }
});

test('batch creation and spawning reject stale or unbound writer authorization', () => {
  const stale = writerSection();
  assert.equal(createBatch({
    batchId: 'batch-1',
    candidates: [admission(1)],
    verifiedCapacity: 1,
    sectionZero: stale,
    now: '2026-08-29T20:00:00.000Z',
  }).ready, false);

  const statusAuthorization = writerSection('status');
  const batch = reserveBatch([1]);
  assert.equal(assessSpawnAttempt({
    now,
    previousAttemptAt: null,
    sectionZero: statusAuthorization,
    batchId: 'batch-1',
    admission: batch.admissions[0],
    duplicateCheck: true,
    collisionCheck: true,
    issueReady: true,
    capacityAvailable: true,
  }).allowed, false);
});

test('ambiguous creation blocks fallback and definitive non-creation permits one same-token fallback', () => {
  const batch = reserveBatch([1]);
  const cloudAttemptAt = '2026-08-29T18:59:50.000Z';
  const spawnAttempt = assessSpawnAttempt({
    now,
    previousAttemptAt: cloudAttemptAt,
    sectionZero: writerSection(),
    admission: batch.admissions[0],
    duplicateCheck: true,
    collisionCheck: true,
    issueReady: true,
    capacityAvailable: true,
    batchId: 'batch-1',
    creationOutcome: 'definitive-non-creation',
  });
  assert.equal(assessLocalFallback({
    admissionToken: batch.admissions[0].admissionToken,
    originalAdmissionToken: batch.admissions[0].admissionToken,
    batchId: batch.id,
    cloudAttemptAt,
    cloudOutcome: 'uncertain',
    spawnAttempt,
  }).allowed, false);
  assert.equal(assessLocalFallback({
    admissionToken: batch.admissions[0].admissionToken,
    originalAdmissionToken: batch.admissions[0].admissionToken,
    batchId: batch.id,
    cloudAttemptAt,
    cloudOutcome: 'definitive-non-creation',
    spawnAttempt,
  }).allowed, true);
  assert.equal(assessLocalFallback({
    admissionToken: 'replacement',
    originalAdmissionToken: batch.admissions[0].admissionToken,
    batchId: batch.id,
    cloudAttemptAt,
    cloudOutcome: 'definitive-non-creation',
    spawnAttempt,
  }).allowed, false);
  assert.equal(assessLocalFallback({
    admissionToken: batch.admissions[0].admissionToken,
    originalAdmissionToken: batch.admissions[0].admissionToken,
    batchId: 'other-batch',
    cloudAttemptAt,
    cloudOutcome: 'definitive-non-creation',
    spawnAttempt,
  }).reason, 'fallback-attempt-correlation-invalid');
});

test('writer timestamps require timezone-qualified RFC 3339 values', () => {
  for (const malformed of [
    '2026-08-29',
    '2026-08-29T19:00:00',
    '2026-02-30T19:00:00.000Z',
    '2026-08-29T25:00:00.000Z',
    '2026-08-29T19:00:00.0000Z',
  ]) {
    assert.equal(assessAtomicOwnership(
      { ...writerCapability, verifiedAt: malformed },
      { now, repository: 'Jamula/Andreja', ownerId: 'coordinator-1' },
    ).available, false);
  }
});

test('approved work is handed off and never directly merged', () => {
  assert.deepEqual(mergeDisposition({ approved: true, checksPassing: true }), {
    action: 'READY_FOR_AGENT_MERGE',
    reason: 'app-landing-required',
  });
});

test('prompt and every orchestration contract carry the five-child wave rules', () => {
  const root = path.resolve(__dirname, '../../..');
  const read = (relative) => fs.readFileSync(path.join(root, relative), 'utf8');
  const prompt = read('.squad/skills/squad-issue-drain/PROMPT.md');
  const coordinator = read('.github/agents/squad.agent.md');
  const coordinatorTemplate = read('.squad/templates/squad.agent.md.template');
  const ralph = read('.squad/templates/ralph-reference.md');
  const ralphInstructions = read('.squad/ralph-instructions.md');
  const ralphInstructionsTemplate = read('.squad/templates/ralph-instructions.md');
  const lifecycle = read('.squad/templates/issue-lifecycle.md');
  const spawn = read('.squad/templates/spawn-reference.md');
  const client = read('.squad/templates/client-compatibility-reference.md');
  const orchestrationLog = read('.squad/templates/orchestration-log.md');
  const scribe = read('.squad/templates/after-agent-reference.md');
  const scribeCharter = read('.squad/agents/scribe/charter.md');
  const scribeCharterTemplate = read('.squad/templates/scribe-charter.md');
  const workflowGuide = read('.squad/templates/workflow-wiring-guide.md');
  const workflowAppendix = read(
    '.squad/templates/workflow-wiring-appendix-a-code-reviewer.md',
  );

  for (const content of [prompt, coordinator, coordinatorTemplate, ralph]) {
    assert.match(content, /enumerat(?:e|ion).*status.*classif.*admi/is);
    assert.match(content, /Section 0/i);
  }
  assert.match(prompt, /up to five/i);
  assert.match(prompt, /exact 10-second boundary/i);
  assert.match(prompt, /every successfully created child/i);
  assert.match(prompt, /stopped\s+partial wave/i);
  assert.match(prompt, /inspect.*exactly once/is);
  assert.match(prompt, /NEXT_TICK_REQUIRED/);
  assert.match(prompt, /useful N\/5 target/);
  assert.match(prompt, /read-only/i);
  assert.match(prompt, /atomic.*capabilit/i);
  assert.match(prompt, /generic Scribe/i);
  assert.match(spawn, /Reserve five child issues/i);
  assert.match(spawn, /ambiguous.*creation/is);
  assert.match(client, /never\s+manufacture concurrency/i);
  assert.match(lifecycle, /every successfully created child returns/i);
  assert.match(orchestrationLog, /stopped partial wave/i);
  assert.match(scribe, /five-minute inspect-once/i);
  assert.match(scribeCharter, /all and only.*successfully created children/is);
  assert.equal(scribeCharter, scribeCharterTemplate);
  assert.equal(ralphInstructions, ralphInstructionsTemplate);
  for (const content of [
    prompt,
    coordinator,
    coordinatorTemplate,
    ralph,
    ralphInstructions,
    lifecycle,
    spawn,
    client,
  ]) {
    assert.doesNotMatch(content, /all admitted children/i);
    assert.doesNotMatch(content, /4-5 simultaneous/i);
    assert.doesNotMatch(content, /all actionable issues simultaneously/i);
  }
  for (const content of [
    coordinator,
    coordinatorTemplate,
    ralph,
    lifecycle,
    workflowGuide,
    workflowAppendix,
  ]) {
    assert.doesNotMatch(
      content,
      /gh pr merge|az repos pr update.*status completed|automatically merges approved/i,
    );
    assert.match(content, /READY_FOR_AGENT_MERGE/);
  }
  assert.match(scribe, /exclusive retrospective completion/i);
});
