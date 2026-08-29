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
  validateAck,
} = require('./issue-drain');

const now = '2026-08-29T19:00:00.000Z';
const writerCapability = {
  verified: true,
  repositoryScoped: true,
  repository: 'Jamula/Andreja',
  ownerId: 'coordinator-1',
  grantId: 'grant-1',
  verifiedAt: now,
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
  assert.equal(full.batch.admissions.length, 5);

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
  const base = {
    now,
    previousAttemptAt: '2026-08-29T18:59:50.001Z',
    sectionZero: writerSection(),
    batchId: 'batch-1',
    duplicateCheck: true,
    collisionCheck: true,
  };
  assert.equal(MIN_SPAWN_SPACING_MILLISECONDS, 10_000);
  assert.equal(ACK_TIMEOUT_MILLISECONDS, 300_000);
  assert.equal(assessSpawnAttempt(base).reason, 'spawn-spacing-not-elapsed');
  assert.equal(assessSpawnAttempt({
    ...base,
    previousAttemptAt: '2026-08-29T18:59:50.000Z',
  }).allowed, true);
  assert.equal(assessSpawnAttempt({ ...base, duplicateCheck: false }).allowed, false);
  assert.equal(assessSpawnAttempt({ ...base, collisionCheck: false }).allowed, false);
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
    { duplicate_check: 'unknown' },
    { collision_check: 'unknown' },
    { ready: 'true' },
    { blocker: 'waiting' },
  ]) {
    assert.equal(validateAck(expected, ackFor(expected, override)).valid, false);
  }
});

test('every child ACK is required before child release or batch advancement', () => {
  const batch = {
    id: 'batch-1',
    authorization: writerSection().authorization,
    admissions: [admission(1), admission(2), admission(3)],
  };
  const pending = assessBatchAdvance({
    batch,
    acknowledgements: batch.admissions.slice(0, 2).map(ackFor),
    now,
    ackDeadline: '2026-08-29T19:05:00.000Z',
    sectionZero: writerSection(),
  });
  assert.equal(pending.allowed, false);
  assert.equal(pending.releaseChildren, false);
  assert.equal(pending.reason, 'batch-acks-pending');

  const complete = assessBatchAdvance({
    batch,
    acknowledgements: batch.admissions.map(ackFor),
    now,
    ackDeadline: '2026-08-29T19:05:00.000Z',
    sectionZero: writerSection(),
  });
  assert.equal(complete.allowed, true);
  assert.equal(complete.releaseChildren, true);
});

test('restart and ACK timeout require reconciliation rather than replacement', () => {
  const batch = {
    id: 'batch-1',
    authorization: writerSection().authorization,
    admissions: [admission(1), admission(2)],
  };
  const restarted = JSON.parse(JSON.stringify(batch));
  const result = assessBatchAdvance({
    batch: restarted,
    acknowledgements: [ackFor(restarted.admissions[0])],
    now: '2026-08-29T19:05:00.000Z',
    ackDeadline: '2026-08-29T19:05:00.000Z',
    sectionZero: writerSection('admit', '2026-08-29T19:05:00.000Z'),
    restarted: true,
  });
  assert.equal(result.allowed, false);
  assert.equal(result.releaseChildren, false);
  assert.equal(result.reason, 'ack-timeout-reconcile-required');

  const allAcks = restarted.admissions.map(ackFor);
  assert.equal(assessBatchAdvance({
    batch: restarted,
    acknowledgements: allAcks,
    now: '2026-08-29T19:05:00.000Z',
    ackDeadline: '2026-08-29T19:05:00.000Z',
    sectionZero: writerSection('admit', '2026-08-29T19:05:00.000Z'),
  }).allowed, false);
  assert.equal(assessBatchAdvance({
    batch: restarted,
    acknowledgements: allAcks,
    now: '2026-08-29T19:05:00.000Z',
    ackDeadline: '2026-08-29T19:05:00.000Z',
    sectionZero: writerSection('admit', '2026-08-29T19:05:00.000Z'),
    reconciliationComplete: true,
  }).allowed, true);
});

test('batch release requires current writer authorization', () => {
  const batch = {
    id: 'batch-1',
    authorization: writerSection().authorization,
    admissions: [admission(1)],
  };
  const result = assessBatchAdvance({
    batch,
    acknowledgements: batch.admissions.map(ackFor),
    now,
    ackDeadline: '2026-08-29T19:05:00.000Z',
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
  for (const batch of [
    {
      id: 'batch-1',
      authorization: writerSection().authorization,
      admissions: [{ ...admission(1), batchId: 'other-batch' }],
    },
    {
      id: 'batch-1',
      authorization: {
        ...writerSection().authorization,
        batchId: 'other-batch',
      },
      admissions: [admission(1)],
    },
  ]) {
    const result = assessBatchAdvance({
      batch,
      acknowledgements: batch.admissions.map(ackFor),
      now,
      ackDeadline: '2026-08-29T19:05:00.000Z',
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
  assert.equal(assessSpawnAttempt({
    now,
    previousAttemptAt: null,
    sectionZero: statusAuthorization,
    batchId: 'batch-1',
    duplicateCheck: true,
    collisionCheck: true,
  }).allowed, false);
});

test('ambiguous creation blocks fallback and definitive non-creation permits one same-token fallback', () => {
  const spawnAttempt = assessSpawnAttempt({
    now,
    previousAttemptAt: '2026-08-29T18:59:50.000Z',
    sectionZero: writerSection(),
    duplicateCheck: true,
    collisionCheck: true,
    batchId: 'batch-1',
    creationOutcome: 'definitive-non-creation',
  });
  assert.equal(assessLocalFallback({
    admissionToken: 'token',
    originalAdmissionToken: 'token',
    cloudOutcome: 'uncertain',
    spawnAttempt,
  }).allowed, false);
  assert.equal(assessLocalFallback({
    admissionToken: 'token',
    originalAdmissionToken: 'token',
    cloudOutcome: 'definitive-non-creation',
    spawnAttempt,
  }).allowed, true);
  assert.equal(assessLocalFallback({
    admissionToken: 'replacement',
    originalAdmissionToken: 'token',
    cloudOutcome: 'definitive-non-creation',
    spawnAttempt,
  }).allowed, false);
});

test('approved work is handed off and never directly merged', () => {
  assert.deepEqual(mergeDisposition({ approved: true, checksPassing: true }), {
    action: 'READY_FOR_AGENT_MERGE',
    reason: 'app-landing-required',
  });
});

test('prompt and templates carry universal Section 0, batch ACK, and no-merge contracts', () => {
  const root = path.resolve(__dirname, '../../..');
  const read = (relative) => fs.readFileSync(path.join(root, relative), 'utf8');
  const prompt = read('.squad/skills/squad-issue-drain/PROMPT.md');
  const coordinator = read('.github/agents/squad.agent.md');
  const coordinatorTemplate = read('.squad/templates/squad.agent.md.template');
  const ralph = read('.squad/templates/ralph-reference.md');
  const lifecycle = read('.squad/templates/issue-lifecycle.md');
  const spawn = read('.squad/templates/spawn-reference.md');
  const scribe = read('.squad/templates/after-agent-reference.md');
  const workflowGuide = read('.squad/templates/workflow-wiring-guide.md');
  const workflowAppendix = read(
    '.squad/templates/workflow-wiring-appendix-a-code-reviewer.md',
  );

  for (const content of [prompt, coordinator, coordinatorTemplate, ralph]) {
    assert.match(content, /enumerat(?:e|ion).*status.*classif.*admi/is);
    assert.match(content, /Section 0/i);
  }
  assert.match(prompt, /up to five/i);
  assert.match(prompt, /at least 10 seconds/i);
  assert.match(prompt, /every child.*ACK/is);
  assert.match(prompt, /release.*all.*ACK/is);
  assert.match(prompt, /read-only/i);
  assert.match(prompt, /atomic.*capabilit/i);
  assert.match(prompt, /generic Scribe/i);
  assert.match(spawn, /five simultaneous/i);
  assert.match(spawn, /ambiguous.*creation/is);
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
