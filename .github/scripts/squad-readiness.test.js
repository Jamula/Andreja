'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const test = require('node:test');
const { checkReadiness } = require('./squad-readiness.js');

const team = role => `## Members
| Name | Role |
|---|---|
| Seven | ${role} |
`;

test('requires a real Lead or Architect roster member', () => {
  for (const roster of ['', team('Coordinator'), team('Leadership')]) {
    assert.throws(() => checkReadiness(roster), /needs a configured member with a Lead or Architect role/);
  }
  assert.doesNotThrow(() => checkReadiness(team('Platform Lead')));
  assert.doesNotThrow(() => checkReadiness(team('Security Architect')));
});

test('both triage entry points fail closed before applying decisions', () => {
  for (const workflow of ['squad-triage', 'squad-heartbeat']) {
    const source = fs.readFileSync(`.github/workflows/${workflow}.yml`, 'utf8');
    const check = source.indexOf('run: node .github/scripts/squad-readiness.js');
    assert.ok(check >= 0, `${workflow}: missing readiness check`);
    const effect = source.indexOf(workflow === 'squad-triage'
      ? 'await github.rest.issues.addLabels'
      : 'node .squad/templates/ralph-triage.js');
    assert.ok(effect > check, `${workflow}: check must precede triage effects`);
  }
  const heartbeat = fs.readFileSync('.github/workflows/squad-heartbeat.yml', 'utf8');
  assert.match(heartbeat, /ralph-triage\.js not found[^\n]*\n\s+exit 1/);
});

function workflowScript(name) {
  const lines = fs.readFileSync(`.github/workflows/${name}.yml`, 'utf8').replace(/\r\n/g, '\n').split('\n');
  const start = lines.findIndex(line => line.trim() === 'script: |');
  assert.notEqual(start, -1, `${name}: missing github-script`);
  const script = [];
  for (const line of lines.slice(start + 1)) {
    if (line.trim() && !line.startsWith('            ')) break;
    script.push(line.slice(12));
  }
  return new (Object.getPrototypeOf(async function () {}).constructor)(
    'require', 'context', 'github', 'core', script.join('\n')
  );
}

for (const name of ['squad-triage', 'squad-heartbeat']) {
  for (const mode of ['member', 'manual-copilot', 'auto-copilot']) {
    test(`${name}: ${mode} produces a truthful actionable handoff`, async () => {
      const auto = mode === 'auto-copilot';
      const copilot = mode !== 'member';
      const roster = team('Lead') + (copilot ? '\n🤖 Coding Agent\n🟢 Good fit: bug fix\n' : '') +
        (auto ? '<!-- copilot-auto-assign: true -->' : '');
      const comments = [];
      const dispatches = [];
      const mockFs = {
        existsSync: () => true,
        readFileSync: path => {
          if (path === '.squad/team.md') return roster;
          if (path === '.squad/routing.md') return '';
          if (path === 'triage-results.json') return JSON.stringify([{
            issueNumber: 12, assignTo: copilot ? '@copilot' : 'Seven',
            label: copilot ? 'squad:copilot' : 'squad:seven',
            reason: 'fixture', source: 'fixture',
          }]);
          throw new Error(`Unexpected read: ${path}`);
        },
      };
      const context = {
        repo: { owner: 'owner', repo: 'repo' },
        payload: {
          issue: { number: 12, title: 'bug fix', body: '', labels: [] },
          repository: { default_branch: 'main' },
        },
      };
      const github = { rest: {
        issues: {
          addLabels: async () => {},
          createComment: async ({ body }) => comments.push(body),
        },
        actions: { createWorkflowDispatch: async args => dispatches.push(args) },
      } };
      const core = { info: () => {}, warning: message => { throw new Error(message); },
        setFailed: message => { throw new Error(message); } };
      await workflowScript(name)(module => {
        assert.equal(module, 'fs');
        return mockFs;
      }, context, github, core);
      assert.equal(comments.length, 1);
      assert.match(comments[0], /routing hint, not a GitHub assignee or a running session/);
      if (copilot) {
        assert.match(comments[0], /verify (?:a |the )linked (?:Copilot )?session or draft PR/i);
        assert.match(comments[0], auto ? /assignment.*(?:dispatched|requested)/i : /manually dispatch Squad Issue Assign/);
      } else {
        assert.match(comments[0], /Start an authorized session for Seven/);
      }
      assert.equal(dispatches.length, auto ? 1 : 0);
      if (auto) assert.deepEqual(dispatches[0].inputs, { issue_number: '12' });
    });
  }
}
