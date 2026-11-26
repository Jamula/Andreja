'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const test = require('node:test');
const { checkReadiness } = require('./squad-readiness.js');
const { parseRoster } = require('../../.squad/templates/ralph-triage.js');

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
  for (const workflowPath of [
    '.github/workflows',
    '.squad/templates/workflows',
  ]) {
    for (const workflow of ['squad-triage', 'squad-heartbeat']) {
      const source = fs.readFileSync(`${workflowPath}/${workflow}.yml`, 'utf8');
      const check = source.indexOf('run: node .github/scripts/squad-readiness.js');
      assert.ok(check >= 0, `${workflowPath}/${workflow}: missing readiness check`);
      const effect = source.indexOf(workflow === 'squad-triage'
        ? 'await github.rest.issues.addLabels'
        : 'node .squad/templates/ralph-triage.js');
      assert.ok(effect > check, `${workflowPath}/${workflow}: check must precede triage effects`);
    }
  }
  for (const workflowPath of ['.github/workflows', '.squad/templates/workflows']) {
    const heartbeat = fs.readFileSync(`${workflowPath}/squad-heartbeat.yml`, 'utf8');
    assert.match(heartbeat, /ralph-triage\.js not found[^\n]*\n\s+exit 1/);
  }
});

function workflowScript(name, stepName, workflowPath = `.github/workflows/${name}.yml`) {
  let lines = fs.readFileSync(workflowPath, 'utf8').replace(/\r\n/g, '\n').split('\n');
  if (stepName) {
    const step = lines.findIndex(line => line.trim() === `- name: ${stepName}`);
    assert.ok(step >= 0, `${name}: missing step "${stepName}"`);
    lines = lines.slice(step);
  }
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

const supportNames = ['Scribe', 'Ralph', 'Rai', 'Fact Checker'];
const supportSlugs = ['scribe', 'ralph', 'rai', 'fact-checker'];

test('support identities stay on the roster', () => {
  const roster = fs.readFileSync('.squad/team.md', 'utf8');
  for (const name of supportNames) {
    assert.ok(roster.split('\n').some(line => line.startsWith(`| ${name} |`)), `${name} is missing from the roster`);
  }
});

for (const workflowPath of [
  '.github/workflows/squad-triage.yml',
  '.squad/templates/workflows/squad-triage.yml',
]) {
  test(`${workflowPath}: support identities do not appear as routing destinations`, async () => {
    const roster = fs.readFileSync('.squad/team.md', 'utf8');
    const comments = [];
    const mockFs = {
      existsSync: () => true,
      readFileSync: path => path === '.squad/team.md' ? roster : '',
    };
    const context = {
      repo: { owner: 'owner', repo: 'repo' },
      payload: {
        issue: { number: 12, title: 'unmatched issue', body: '', labels: [] },
        repository: { default_branch: 'main' },
      },
    };
    const github = { rest: { issues: {
      addLabels: async () => {},
      createComment: async ({ body }) => comments.push(body),
    } } };
    await workflowScript('squad-triage', undefined, workflowPath)(
      module => module === 'fs' ? mockFs : { parseRoster },
      context,
      github,
      { info: () => {}, warning: message => { throw new Error(message); } }
    );

    assert.equal(comments.length, 1);
    for (const slug of supportSlugs) {
      assert.ok(!comments[0].includes(`label: \`squad:${slug}\``), `triage advertised squad:${slug}`);
    }
    assert.ok(comments[0].includes('label: `squad:lead`'));
  });
}

for (const workflowPath of [
  '.github/workflows/sync-squad-labels.yml',
  '.squad/templates/workflows/sync-squad-labels.yml',
]) {
  test(`${workflowPath}: support identities do not generate routing labels`, async () => {
    const roster = fs.readFileSync('.squad/team.md', 'utf8');
    const syncedLabels = [];
    const mockFs = { existsSync: () => true, readFileSync: () => roster };
    const context = { repo: { owner: 'owner', repo: 'repo' } };
    const github = { rest: { issues: {
      getLabel: async () => {},
      updateLabel: async ({ name }) => syncedLabels.push(name),
      createLabel: async ({ name }) => syncedLabels.push(name),
    } } };
    await workflowScript('sync-squad-labels', undefined, workflowPath)(module => {
      assert.equal(module, 'fs');
      return mockFs;
    }, context, github, { info: () => {} });

    for (const slug of supportSlugs) {
      assert.ok(!syncedLabels.includes(`squad:${slug}`), `support identity created squad:${slug}`);
    }
    assert.ok(syncedLabels.includes('squad:lead'));
  });
}

for (const workflowPath of [
  '.github/workflows/squad-issue-assign.yml',
  '.squad/templates/workflows/squad-issue-assign.yml',
]) {
  test(`${workflowPath}: support identity labels do not resolve or post assignments`, async () => {
    const supportLabels = [...supportSlugs.map(slug => `squad:${slug}`), 'squad:Fact Checker'];
    const comments = [];
    const logs = [];
    const mockFs = {
      existsSync: () => { throw new Error('Support labels must be rejected before roster lookup'); },
      readFileSync: () => { throw new Error('Support labels must be rejected before roster lookup'); },
    };
    const context = {
      repo: { owner: 'owner', repo: 'repo' },
      payload: { label: { name: '' }, issue: { number: 12, title: 'test issue' } },
    };
    const github = { rest: { issues: {
      createComment: async args => comments.push(args),
    } } };
    for (const label of supportLabels) {
      context.payload.label.name = label;
      await workflowScript('squad-issue-assign', 'Identify assigned member and trigger work', workflowPath)(
        module => {
          assert.equal(module, 'fs');
          return mockFs;
        },
        context,
        github,
        { info: message => logs.push(message), warning: message => logs.push(message) }
      );
    }
    assert.deepEqual(comments, []);
    assert.equal(logs.length, supportLabels.length);
  });
}

for (const workflowPath of ['.github/workflows', '.squad/templates/workflows']) {
  for (const name of ['squad-triage', 'squad-heartbeat']) {
    for (const mode of ['member', 'manual-copilot', 'auto-copilot']) {
      test(`${workflowPath}/${name}: ${mode} produces a truthful actionable handoff`, async () => {
        const auto = mode === 'auto-copilot';
        const copilot = mode !== 'member';
        const roster = team('Lead') + (copilot ? '\n🤖 Coding Agent\n🟢 Good fit: bug fix\n' : '') +
          (auto ? '<!-- copilot-auto-assign: true -->' : '');
        const comments = [];
        const dispatches = [];
        const events = [];
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
            createComment: async ({ body }) => { comments.push(body); events.push('comment'); },
          },
          actions: { createWorkflowDispatch: async args => { dispatches.push(args); events.push('dispatch'); } },
        } };
        const core = { info: () => {}, warning: message => { throw new Error(message); },
          setFailed: message => { throw new Error(message); } };
        await workflowScript(name, name === 'squad-heartbeat' ? 'Ralph — Apply triage decisions' : undefined,
          `${workflowPath}/${name}.yml`)(module => {
          if (module === 'fs') return mockFs;
          assert.ok(module.replace(/\\/g, '/').endsWith('/.squad/templates/ralph-triage.js'));
          return { parseRoster };
        }, context, github, core);
        assert.equal(comments.length, 1);
        assert.match(comments[0], /routing hint, not a GitHub assignee or a running session/);
        assert.doesNotMatch(comments[0], /\*\*Assigned to:\*\*/);
        if (copilot) {
          assert.match(comments[0], /verify (?:a |the )linked (?:Copilot )?session or draft PR/i);
          assert.match(comments[0], auto ? /assignment.*(?:dispatched|requested)/i : /manually dispatch Squad Issue Assign/);
        } else {
          assert.match(comments[0], /start an authorized session for Seven/i);
        }
        assert.equal(dispatches.length, auto ? 1 : 0);
        assert.deepEqual(events, auto ? ['dispatch', 'comment'] : ['comment']);
        if (auto) assert.deepEqual(dispatches[0].inputs, { issue_number: '12' });
      });
    }
  }
}

for (const workflowPath of ['.github/workflows', '.squad/templates/workflows']) {
  test(`${workflowPath}/squad-triage routes a roster with reordered columns`, async () => {
    const roster = `## Members
| Name | Charter | Role |
|---|---|---|
| Seven | .squad/agents/seven/charter.md | Platform Lead |
`;
    assert.doesNotThrow(() => checkReadiness(roster));
    const labels = [];
    const mockFs = {
      existsSync: () => true,
      readFileSync: path => path === '.squad/team.md' ? roster : '',
    };
    const context = { repo: { owner: 'owner', repo: 'repo' }, payload: {
      issue: { number: 12, title: 'unmatched', body: '', labels: [] },
      repository: { default_branch: 'main' },
    } };
    const github = { rest: { issues: {
      addLabels: async ({ labels: added }) => labels.push(...added),
      createComment: async () => {},
    } } };
    await workflowScript('squad-triage', undefined, `${workflowPath}/squad-triage.yml`)(
      module => module === 'fs' ? mockFs : { parseRoster },
      context, github, { info: () => {}, warning: () => {} });
    assert.deepEqual(labels, ['squad:seven', 'go:needs-research']);
  });

  test(`${workflowPath}/squad-triage fails instead of silently succeeding without a fallback`, async () => {
    const mockFs = { existsSync: () => true, readFileSync: () => team('Backend') };
    const context = { payload: { issue: { number: 12, title: 'unmatched', body: '', labels: [] } } };
    await assert.rejects(
      workflowScript('squad-triage', undefined, `${workflowPath}/squad-triage.yml`)(
        module => module === 'fs' ? mockFs : { parseRoster },
        context, {}, { info: () => {}, warning: () => {} }),
      /No Lead or Architect role found/
    );
  });
}
