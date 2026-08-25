'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');

const {
  KNOWN_STATUSES,
  normalizeStatus,
  resolveEvent,
} = require('./issue-status');
const {
  issueNumberFromBranch,
  issueNumbersFromBody,
} = require('./run-issue-status');

const fixtures = JSON.parse(fs.readFileSync(
  path.join(__dirname, 'fixtures', 'issue-status-events.json'),
  'utf8'));

for (const fixture of fixtures) {
  test(fixture.name, () => {
    const result = resolveEvent(fixture);
    assert.equal(result.desiredStatus, fixture.expectedStatus);
    assert.deepEqual(result.blockerLabels, fixture.expectedBlockers || []);

    const resultingStatuses = new Set([
      ...fixture.currentLabels
        .map((label) => typeof label === 'string' ? label : label.name)
        .filter((label) => label.startsWith('status:') &&
          !result.plan.remove.includes(label)),
      ...result.plan.add.filter((label) => label.startsWith('status:')),
    ]);
    assert.deepEqual([...resultingStatuses], [fixture.expectedStatus]);

    for (const label of fixture.expectedRemoved || []) {
      assert.ok(result.plan.remove.includes(label));
    }
    for (const label of fixture.expectedPreserved || []) {
      assert.ok(!result.plan.remove.includes(label));
    }
  });
}

test('all known statuses normalize and unknown statuses fail closed', () => {
  for (const status of KNOWN_STATUSES) {
    assert.equal(normalizeStatus(status), status);
  }
  assert.throws(() => normalizeStatus('mystery'), /Unknown lifecycle status/);
});

test('branch parser accepts the documented convention only', () => {
  assert.equal(issueNumberFromBranch('squad/70-fix-status'), 70);
  assert.equal(issueNumberFromBranch('70-fix-status'), 70);
  assert.equal(issueNumberFromBranch('feature/no-issue'), null);
  assert.equal(issueNumberFromBranch('feature/v2-status'), null);
});

test('closing keyword parser is bounded to explicit local issue references', () => {
  assert.deepEqual(
    [...issueNumbersFromBody('Closes #70\nFixes #71\nRelated to #72')],
    [70, 71]);
  assert.deepEqual([...issueNumbersFromBody('Closes owner/repo#70')], []);
});

test('write workflow executes only trusted pinned automation with least privilege', () => {
  const workflow = fs.readFileSync(
    path.join(__dirname, '..', 'workflows', 'issue-status.yml'),
    'utf8');

  assert.match(workflow, /ref: \$\{\{ github\.event\.repository\.default_branch \}\}/);
  assert.match(workflow, /persist-credentials: false/);
  assert.match(
    workflow,
    /github\.event\.pull_request\.head\.repo\.full_name == github\.repository/);
  assert.match(workflow, /contents: read/);
  assert.match(workflow, /issues: write/);
  assert.match(workflow, /pull-requests: read/);
  assert.doesNotMatch(workflow, /contents: write/);
  assert.doesNotMatch(workflow, /createComment/);

  const actionReferences = [...workflow.matchAll(/uses:\s+\S+@(\S+)/g)];
  assert.ok(actionReferences.length > 0);
  for (const [, reference] of actionReferences) {
    assert.match(reference, /^[0-9a-f]{40}$/);
  }
});
