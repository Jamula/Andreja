'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const {
  assessAdmission,
  prepareCompletion,
  resolveActionCandidates,
  validateCompletedLog,
} = require('./weekly-retrospective');

const now = '2026-08-28T22:03:38.453-07:00';

function completedLog(completedAt = '2026-08-29T04:17:54.580Z') {
  return {
    key: 'log/weekly-retrospective-2026-08-24.md',
    content: [
      '<!-- weekly-retrospective:v1 -->',
      '# Weekly Retrospective — 2026-08-24',
      '',
      '- Status: complete',
      `- Completed at: ${completedAt}`,
      '- Evidence window: 2026-08-22T04:17:54.580Z through 2026-08-29T04:17:54.580Z',
      '- Shipped count: 56',
      '- Open count: 27',
      '',
      '## Blockers',
      '- #44',
      '',
      '## Decisions',
      '- Queue admission fails closed — Reference: governed decision a029d433',
      '',
      '## Retro actions',
      '- created: Automate retrospective enforcement — https://github.com/Jamula/Andreja/issues/122',
      '',
    ].join('\n'),
  };
}

test('overdue admission fails closed and requires the built-in ceremony', () => {
  assert.deepEqual(assessAdmission({
    now,
    logs: [],
    stateAvailable: true,
    enumerationComplete: true,
  }), {
    allowed: false,
    ceremonyRequired: true,
    reason: 'retrospective-overdue',
    mechanism: 'built-in',
    configuredEnforcementAvailable: true,
  });
});

test('a completed retrospective within seven days allows admission', () => {
  const result = assessAdmission({
    now,
    logs: [completedLog()],
    stateAvailable: true,
    enumerationComplete: true,
  });
  assert.equal(result.allowed, true);
  assert.equal(result.reason, 'retrospective-current');

  assert.equal(
    assessAdmission({
      now: '2026-09-05T04:17:54.580Z',
      logs: [completedLog()],
      stateAvailable: true,
      enumerationComplete: true,
    }).allowed,
    true,
  );
  assert.equal(
    assessAdmission({
      now: '2026-09-05T04:17:54.581Z',
      logs: [completedLog()],
      stateAvailable: true,
      enumerationComplete: true,
    }).reason,
    'retrospective-overdue',
  );
});

test('the existing detailed runtime log is accepted during canonical-key rollout', () => {
  const legacy = {
    key: 'log/2026-08-28T21-17-54.580-07-00-retrospective-with-enforcement.md',
    content: [
      '# Retrospective with Enforcement — 2026-08-28',
      '',
      'Evidence window: 2026-08-22T04:17:54.580Z through 2026-08-29T04:17:54.580Z.',
      '',
      '## Evidence',
      '- GitHub counts reviewed.',
      '',
      '## Decisions',
      '- Queue admission fails closed.',
      '',
      '## Actions',
      '- Existing issues reused.',
    ].join('\n'),
  };

  assert.equal(assessAdmission({
    now,
    logs: [legacy],
    stateAvailable: true,
    enumerationComplete: true,
  }).allowed, true);
});

test('legacy rollout records require a substantive list entry in every section', () => {
  const entries = [
    ['Evidence', '- GitHub counts reviewed.'],
    ['Decisions', '- Queue admission fails closed.'],
    ['Actions', '- Existing issues reused.'],
  ];

  for (const [name, entry] of entries) {
    for (const body of ['', ' \t', '-   ']) {
      const legacy = {
        key: 'log/2026-08-28T21-17-54.580-07-00-retrospective-with-enforcement.md',
        content: [
          '# Retrospective with Enforcement — 2026-08-28',
          '',
          'Evidence window: 2026-08-22T04:17:54.580Z through 2026-08-29T04:17:54.580Z.',
          '',
          '## Evidence',
          '- GitHub counts reviewed.',
          '',
          '## Decisions',
          '- Queue admission fails closed.',
          '',
          '## Actions',
          '- Existing issues reused.',
        ].join('\n').replace(`## ${name}\n${entry}`, `## ${name}\n${body}`),
      };

      assert.equal(
        validateCompletedLog(legacy).reason,
        'not-a-completed-legacy-record',
        `${name} without a substantive list entry must fail validation`,
      );
      assert.equal(
        assessAdmission({
          now,
          logs: [legacy],
          stateAvailable: true,
          enumerationComplete: true,
        }).allowed,
        false,
        `${name} without a substantive list entry must not allow admission`,
      );
    }
  }
});

test('an unavailable configured enforcement component does not bypass built-in enforcement', () => {
  const overdue = assessAdmission({
    now,
    logs: [],
    stateAvailable: true,
    enumerationComplete: true,
    configuredEnforcementAvailable: false,
  });
  assert.equal(overdue.allowed, false);
  assert.equal(overdue.ceremonyRequired, true);
  assert.equal(overdue.mechanism, 'built-in');

  const current = assessAdmission({
    now,
    logs: [completedLog()],
    stateAvailable: true,
    enumerationComplete: true,
    configuredEnforcementAvailable: false,
  });
  assert.equal(current.allowed, true);
});

test('duplicate action search reuses an existing issue instead of creating another', () => {
  const resolution = resolveActionCandidates(
    [{ id: 'admission', summary: 'Automate retrospective enforcement' }],
    [{
      candidateId: 'admission',
      complete: true,
      matches: [{
        url: 'https://api.github.com/repos/Jamula/Andreja/issues/122',
        repository_url: 'https://api.github.com/repos/Jamula/Andreja',
        html_url: 'https://github.com/Jamula/Andreja/issues/122',
        number: 122,
        state: 'OPEN',
        title: '[Feedback]: Automate weekly retrospective enforcement and durable logging',
      }],
      createdIssue: null,
    }],
  );

  assert.deepEqual(resolution, {
    complete: true,
    actions: [{
      summary: 'Automate retrospective enforcement',
      disposition: 'existing',
      issueUrl: 'https://github.com/Jamula/Andreja/issues/122',
    }],
    pending: [],
  });
});

test('duplicate action search requires an exact boolean completion confirmation', () => {
  for (const complete of ['true', 'false', 1, -1, {}, []]) {
    const resolution = resolveActionCandidates(
      [{ id: 'admission', summary: 'Automate retrospective enforcement' }],
      [{
        candidateId: 'admission',
        complete,
        matches: [{
          number: 122,
          html_url: 'https://github.com/Jamula/Andreja/issues/122',
          state: 'OPEN',
        }],
      }],
    );

    assert.deepEqual(resolution, {
      complete: false,
      actions: [],
      pending: [{ id: 'admission', reason: 'duplicate-search-incomplete' }],
    });
  }
});

test('an interrupted ceremony cannot produce a completion write', () => {
  const result = prepareCompletion({
    now,
    logs: [],
    stateAvailable: true,
    enumerationComplete: true,
    evidence: {
      windowStart: '2026-08-22T04:17:54.580Z',
      windowEnd: '2026-08-29T04:17:54.580Z',
      shippedCount: 56,
      openCount: 27,
    },
    blockers: ['#44'],
    decisions: [{ summary: 'Fail closed', reference: 'governed decision a029d433' }],
    actions: [],
    gates: {
      evidenceReviewComplete: true,
      decisionReviewComplete: true,
      decisionsRecorded: true,
      duplicateSearchComplete: false,
      actionIssuesComplete: false,
      privacyReviewComplete: true,
    },
  });

  assert.equal(result.ready, false);
  assert.equal(result.write, null);
  assert.match(result.reason, /^gate-incomplete:/);
});

test('completion is a single deterministic runtime-state write per UTC weekly cycle', () => {
  const input = {
    now,
    logs: [],
    stateAvailable: true,
    enumerationComplete: true,
    evidence: {
      windowStart: '2026-08-22T04:17:54.580Z',
      windowEnd: '2026-08-29T04:17:54.580Z',
      shippedCount: 56,
      openCount: 27,
    },
    blockers: ['#44', '#62'],
    decisions: [{ summary: 'Queue admission fails closed', reference: 'governed decision a029d433' }],
    actions: [{
      summary: 'Automate retrospective enforcement',
      disposition: 'created',
      issueUrl: 'https://github.com/Jamula/Andreja/issues/122',
    }],
    gates: {
      evidenceReviewComplete: true,
      decisionReviewComplete: true,
      decisionsRecorded: true,
      duplicateSearchComplete: true,
      actionIssuesComplete: true,
      privacyReviewComplete: true,
    },
  };
  const first = prepareCompletion(input);
  const second = prepareCompletion(input);

  assert.equal(first.ready, true);
  assert.equal(first.write.key, 'log/weekly-retrospective-2026-08-24.md');
  assert.deepEqual(second, first);
  assert.match(first.write.content, /Evidence window:/);
  assert.match(first.write.content, /Shipped count: 56/);
  assert.match(first.write.content, /Open count: 27/);
  assert.match(first.write.content, /## Blockers/);
  assert.match(first.write.content, /## Decisions/);
  assert.match(first.write.content, /## Retro actions/);
  assert.equal(validateCompletedLog(first.write).valid, true);

  const afterWrite = prepareCompletion({
    ...input,
    logs: [first.write],
  });
  assert.equal(afterWrite.ready, false);
  assert.equal(afterWrite.reason, 'cycle-already-complete');
  assert.equal(afterWrite.write, null);
});

test('state failures and malformed canonical records fail closed', () => {
  assert.equal(assessAdmission({ now }).reason, 'state-backend-unavailable');
  assert.equal(
    assessAdmission({ now, stateAvailable: true }).reason,
    'log-enumeration-incomplete',
  );
  assert.equal(
    assessAdmission({ now, stateAvailable: 'true', enumerationComplete: true }).reason,
    'state-backend-unavailable',
  );
  assert.equal(
    assessAdmission({ now, stateAvailable: true, enumerationComplete: 1 }).reason,
    'log-enumeration-incomplete',
  );
  assert.equal(
    assessAdmission({
      now,
      logs: [{ key: 'log/weekly-retrospective-2026-08-24.md', content: 'partial' }],
      stateAvailable: true,
      enumerationComplete: true,
    }).reason,
    'invalid-completion-record',
  );
  assert.equal(
    assessAdmission({
      now,
      logs: [completedLog('2026-08-30T04:17:54.580Z')],
      stateAvailable: true,
      enumerationComplete: true,
    }).reason,
    'future-completion-record',
  );
});

test('canonical completion validation rejects empty required section bodies', () => {
  const sections = [
    ['Blockers', '- #44'],
    ['Decisions', '- Queue admission fails closed — Reference: governed decision a029d433'],
    [
      'Retro actions',
      '- created: Automate retrospective enforcement — https://github.com/Jamula/Andreja/issues/122',
    ],
  ];

  for (const [name, entry] of sections) {
    for (const body of ['', ' \t\n']) {
      const log = completedLog();
      log.content = log.content.replace(`## ${name}\n${entry}\n`, `## ${name}\n${body}`);
      assert.equal(
        validateCompletedLog(log).reason,
        'required-section-entries-missing',
        `${name} with an empty or blank body must fail validation`,
      );
    }
  }
});

test('admission rejects a canonical completion with a blank required section', () => {
  const log = completedLog();
  log.content = log.content.replace(
    '## Decisions\n- Queue admission fails closed — Reference: governed decision a029d433\n',
    '## Decisions\n \t\n',
  );

  assert.deepEqual(assessAdmission({
    now,
    logs: [log],
    stateAvailable: true,
    enumerationComplete: true,
  }), {
    allowed: false,
    ceremonyRequired: false,
    reason: 'invalid-completion-record',
    key: log.key,
    detail: 'required-section-entries-missing',
  });
});

test('canonical completion validation enforces each section entry shape', () => {
  const malformedEntries = [
    ['Blockers', '- #44', '- arbitrary text'],
    [
      'Decisions',
      '- Queue admission fails closed — Reference: governed decision a029d433',
      '- Queue admission fails closed — governed decision a029d433',
    ],
    [
      'Retro actions',
      '- created: Automate retrospective enforcement — https://github.com/Jamula/Andreja/issues/122',
      '- created: Automate retrospective enforcement — https://api.github.com/repos/Jamula/Andreja/issues/122',
    ],
  ];

  for (const [name, validEntry, malformedEntry] of malformedEntries) {
    const log = completedLog();
    log.content = log.content.replace(
      `## ${name}\n${validEntry}`,
      `## ${name}\n${malformedEntry}`,
    );
    assert.equal(
      validateCompletedLog(log).reason,
      'required-section-entries-invalid',
      `${name} must reject malformed entries`,
    );
    assert.equal(
      assessAdmission({
        now,
        logs: [log],
        stateAvailable: true,
        enumerationComplete: true,
      }).allowed,
      false,
      `${name} must not allow admission with malformed entries`,
    );
  }

  const mixedSentinel = completedLog();
  mixedSentinel.content = mixedSentinel.content.replace(
    '## Blockers\n- #44',
    '## Blockers\n- No blockers.\n- #44',
  );
  assert.equal(
    validateCompletedLog(mixedSentinel).reason,
    'required-section-entries-invalid',
  );
});

test('completion preserves explicit no-item sentinels accepted by validation', () => {
  const completion = prepareCompletion({
    now,
    logs: [],
    stateAvailable: true,
    enumerationComplete: true,
    evidence: {
      windowStart: '2026-08-22T04:17:54.580Z',
      windowEnd: '2026-08-29T04:17:54.580Z',
      shippedCount: 56,
      openCount: 27,
    },
    blockers: [],
    decisions: [],
    actions: [],
    gates: {
      evidenceReviewComplete: true,
      decisionReviewComplete: true,
      decisionsRecorded: true,
      duplicateSearchComplete: true,
      actionIssuesComplete: true,
      privacyReviewComplete: true,
    },
  });

  assert.equal(completion.ready, true);
  assert.match(completion.write.content, /## Blockers\n- No blockers\./);
  assert.match(completion.write.content, /## Decisions\n- No new decision required\./);
  assert.match(
    completion.write.content,
    /## Retro actions\n- No actions after complete duplicate search\./,
  );
  assert.equal(validateCompletedLog(completion.write).valid, true);
});

test('completion fails closed on malformed caller entries for every section', () => {
  const input = {
    now,
    logs: [],
    stateAvailable: true,
    enumerationComplete: true,
    evidence: {
      windowStart: '2026-08-22T04:17:54.580Z',
      windowEnd: '2026-08-29T04:17:54.580Z',
      shippedCount: 56,
      openCount: 27,
    },
    blockers: ['#44'],
    decisions: [{ summary: 'Fail closed', reference: 'governed decision a029d433' }],
    actions: [{
      summary: 'Automate retrospective enforcement',
      disposition: 'existing',
      issueUrl: 'https://github.com/Jamula/Andreja/issues/122',
    }],
    gates: {
      evidenceReviewComplete: true,
      decisionReviewComplete: true,
      decisionsRecorded: true,
      duplicateSearchComplete: true,
      actionIssuesComplete: true,
      privacyReviewComplete: true,
    },
  };
  const malformed = [
    { blockers: ['arbitrary text'] },
    { decisions: [{ summary: 'Fail closed', reference: '' }] },
    { decisions: [{ summary: 'Fail closed', reference: { key: 'decision' } }] },
    {
      actions: [{
        summary: 'Automate retrospective enforcement',
        disposition: 'existing',
        issueUrl: 'https://api.github.com/repos/Jamula/Andreja/issues/122',
      }],
    },
  ];

  for (const override of malformed) {
    const result = prepareCompletion({ ...input, ...override });
    assert.equal(result.ready, false);
    assert.equal(result.write, null);
    assert.match(result.reason, /^record-invalid:/);
  }
});

test('timestamps and both evidence-window endpoints require strict timezone-qualified RFC 3339', () => {
  assert.throws(
    () => assessAdmission({
      now: '2026-08-29 04:17:54',
      logs: [],
      stateAvailable: true,
      enumerationComplete: true,
    }),
    /timezone-qualified RFC 3339/,
  );
  assert.equal(
    validateCompletedLog(completedLog('2026-08-29T04:17:54.580')).reason,
    'invalid-completion-timestamp',
  );
  assert.equal(
    validateCompletedLog(completedLog('2026-08-29T04:17:54.5801Z')).reason,
    'invalid-completion-timestamp',
  );

  const invalidWindows = [
    '2026-08-22T04:17:54.580 through 2026-08-29T04:17:54.580Z',
    '2026-08-22T04:17:54.580Z through 2026-08-29T04:17:54.580',
    '2026-08-30T04:17:54.580Z through 2026-08-29T04:17:54.580Z',
    '2026-02-30T04:17:54.580Z through 2026-08-29T04:17:54.580Z',
    '2026-08-22T04:17:54.5801Z through 2026-08-29T04:17:54.580Z',
    '2026-08-22T04:17:54.580Z through 2026-08-29T04:17:54.5801Z',
  ];
  for (const evidenceWindow of invalidWindows) {
    const log = completedLog();
    log.content = log.content.replace(
      '2026-08-22T04:17:54.580Z through 2026-08-29T04:17:54.580Z',
      evidenceWindow,
    );
    assert.equal(validateCompletedLog(log).reason, 'invalid-evidence-window');
  }

  const legacy = {
    key: 'log/2026-08-28T21-17-54.580-07-00-retrospective-with-enforcement.md',
    content: [
      '# Retrospective with Enforcement — 2026-08-28',
      'Evidence window: 2026-08-22T04:17:54.580 through 2026-08-29T04:17:54.580Z.',
      '## Evidence',
      '## Decisions',
      '## Actions',
    ].join('\n'),
  };
  assert.equal(validateCompletedLog(legacy).valid, false);
});

test('completion requires confirmed state availability and complete enumeration', () => {
  const input = {
    now,
    logs: [],
    evidence: {
      windowStart: '2026-08-22T04:17:54.580Z',
      windowEnd: '2026-08-29T04:17:54.580Z',
      shippedCount: 56,
      openCount: 27,
    },
    gates: {
      evidenceReviewComplete: true,
      decisionReviewComplete: true,
      decisionsRecorded: true,
      duplicateSearchComplete: true,
      actionIssuesComplete: true,
      privacyReviewComplete: true,
    },
  };

  assert.deepEqual(prepareCompletion(input), {
    ready: false,
    reason: 'state-backend-unavailable',
    write: null,
  });
  assert.equal(
    prepareCompletion({ ...input, stateAvailable: true }).reason,
    'log-enumeration-incomplete',
  );
  assert.equal(
    prepareCompletion({
      ...input,
      stateAvailable: 'true',
      enumerationComplete: true,
    }).reason,
    'state-backend-unavailable',
  );
  assert.equal(
    prepareCompletion({
      ...input,
      stateAvailable: true,
      enumerationComplete: 1,
    }).reason,
    'log-enumeration-incomplete',
  );
  for (const [endpoint, value] of [
    ['windowStart', '2026-08-22T04:17:54.5801Z'],
    ['windowEnd', '2026-08-29T04:17:54.5801Z'],
  ]) {
    assert.equal(
      prepareCompletion({
        ...input,
        stateAvailable: true,
        enumerationComplete: true,
        evidence: { ...input.evidence, [endpoint]: value },
      }).reason,
      'evidence-window-invalid',
    );
  }
  const completion = prepareCompletion({
    ...input,
    stateAvailable: true,
    enumerationComplete: true,
  });
  assert.equal(completion.ready, true);
  assert.equal(validateCompletedLog(completion.write).valid, true);
});

test('the issue-drain contract and runbook require governed fail-closed recovery', () => {
  const root = path.resolve(__dirname, '../../..');
  const prompt = fs.readFileSync(path.join(__dirname, 'PROMPT.md'), 'utf8');
  const runbook = fs.readFileSync(
    path.join(root, 'docs/operations/weekly-retrospective.md'),
    'utf8',
  );

  assert.match(prompt, /before enumerating or admitting queue work/i);
  assert.match(prompt, /squad_state_list.*`log`/s);
  assert.match(prompt, /must not depend on the configured\s+`retro-enforcement` skill/i);
  assert.match(prompt, /orchestrator must not write `log\/` directly/i);
  assert.match(prompt, /Scribe alone\s+makes exactly one `squad_state_write`/i);
  assert.match(prompt, /limited to millisecond precision/i);
  assert.match(prompt, /literal boolean `true`/i);
  assert.match(runbook, /Operational owner.*Jett Reno/i);
  assert.match(runbook, /Scribe alone writes\s+`log\/`/i);
  assert.match(runbook, /interrupted before the final write/i);
  assert.match(runbook, /no more than three fractional-second\s+digits/i);
  assert.match(runbook, /Omitted or\s+non-boolean confirmations fail closed/i);
  assert.match(runbook, /Never write.*runtime-owned.*directly/i);
});
