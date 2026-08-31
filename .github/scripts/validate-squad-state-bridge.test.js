'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { execFileSync, spawnSync } = require('node:child_process');
const test = require('node:test');

const {
  EOF_CANARY,
  HEAD_CANARY,
  HOST_NEUTRAL_RECOVERY_SENTENCE,
  NON_LOCAL_RAW_FILE_PROHIBITION_HEADING,
  PROBE_FAILURE_RECOVERY_PARAGRAPH,
  REQUIRED_IGNORES,
  REQUIRED_MCP_TOOLS,
  REQUIRED_NON_LOCAL_RAW_FILE_PROHIBITIONS,
  SQUAD_VERSION,
  parseBoundedBulletList,
  parseBoundedNumberedStep,
  validateRepository,
} = require('./validate-squad-state-bridge');

let sequence = 0;

const CANONICAL_FRESH_CHILD_PROBE_STEP = [
  '5. With a unique, non-sensitive',
  '.scratch/state-bridge-probe-<session-id>.md key in the explicitly',
  'ephemeral governed .scratch/ namespace, use squad_state_write, read back',
  'the exact value, delete it with squad_state_delete, and confirm a final',
  'read reports key-not-found.',
].join(' ');

function normalizeMarkdownAndWhitespace(markdown) {
  return markdown
    .replace(/`([^`\r\n]+)`/gu, '$1')
    .replace(/\*\*([^*\r\n]+)\*\*/gu, '$1')
    .replace(/__([^_\r\n]+)__/gu, '$1')
    .replace(/~~([^~\r\n]+)~~/gu, '$1')
    .replace(/(?<!\*)\*([^*\r\n]+)\*(?!\*)/gu, '$1')
    .replace(/\s+/gu, ' ')
    .trim();
}

function freshChildProbeStepErrors(runbook) {
  const step = parseBoundedNumberedStep(runbook, 5, 6);
  if (step === null) {
    return ['fresh-child acceptance must contain one step 5 bounded by step 6'];
  }

  const errors = [];
  if (/~~/u.test(step)) {
    errors.push(
      'fresh-child acceptance step 5 must not contain Markdown strikethrough markup',
    );
  }
  const normalizedStep = normalizeMarkdownAndWhitespace(step);
  if (/(?:^|[^A-Za-z0-9_.-])log\//iu.test(normalizedStep)) {
    errors.push('fresh-child acceptance step 5 must not use a log/ probe path');
  }
  if (normalizedStep !== CANONICAL_FRESH_CHILD_PROBE_STEP) {
    errors.push(
      'fresh-child acceptance step 5 must retain the canonical affirmative probe sequence',
    );
  }
  return errors;
}

function fixture(t) {
  sequence += 1;
  const root = path.join(
    path.resolve(__dirname, '..', '..'),
    `.squad-state-bridge-test-${process.pid}-${sequence}`,
  );
  fs.mkdirSync(path.join(root, '.squad'), { recursive: true });
  fs.mkdirSync(path.join(root, '.github', 'agents'), { recursive: true });
  fs.writeFileSync(path.join(root, '.squad', 'config.json'), JSON.stringify({
    version: 1,
    stateBackend: 'two-layer',
  }));
  fs.writeFileSync(path.join(root, '.mcp.json'), JSON.stringify({
    mcpServers: {
      squad_state: {
        command: 'npx',
        args: ['-y', `@bradygaster/squad-cli@${SQUAD_VERSION}`, 'state-mcp'],
        env: {},
        tools: REQUIRED_MCP_TOOLS,
      },
    },
  }));
  fs.writeFileSync(
    path.join(root, '.github', 'agents', 'squad.agent.md'),
    [
      HEAD_CANARY,
      `<!-- version: ${SQUAD_VERSION} -->`,
      `Report \`Squad v${SQUAD_VERSION}\`.`,
      'Static config (charters, team.md, routing.md) always lives on disk.',
      'The runtime owns persistence and you MUST NOT touch mutable files.',
      NON_LOCAL_RAW_FILE_PROHIBITION_HEADING,
      '',
      '- `.squad/agents/*/history.md`',
      '- `.squad/agents/*/history-archive.md`',
      '',
      'These are runtime-managed paths under non-local backends.',
      PROBE_FAILURE_RECOVERY_PARAGRAPH,
      `<!-- ${EOF_CANARY} -->`,
    ].join('\n'),
  );
  fs.writeFileSync(path.join(root, '.gitignore'), REQUIRED_IGNORES.join('\n'));
  execFileSync('git', ['init', '--quiet'], { cwd: root });
  t.after(() => fs.rmSync(root, { recursive: true, force: true }));
  return root;
}

function writeJson(root, relativePath, value) {
  fs.writeFileSync(path.join(root, relativePath), JSON.stringify(value));
}

test('accepts the approved two-layer static contract', (t) => {
  assert.deepEqual(validateRepository(fixture(t)), []);
});

test('rejects a local-backend fallback', (t) => {
  const root = fixture(t);
  writeJson(root, path.join('.squad', 'config.json'), {
    version: 1,
    stateBackend: 'local',
  });
  assert.match(validateRepository(root).join('\n'), /must be "two-layer"/);
});

test('rejects an absent MCP bridge', (t) => {
  const root = fixture(t);
  fs.rmSync(path.join(root, '.mcp.json'));
  assert.match(validateRepository(root).join('\n'), /\.mcp\.json is unreadable:/);
});

test('preserves controlled malformed-JSON errors for static bridge configuration', (t) => {
  const files = [
    [path.join('.squad', 'config.json'), '.squad/config.json'],
    ['.mcp.json', '.mcp.json'],
  ];

  for (const [relativePath, description] of files) {
    const root = fixture(t);
    fs.writeFileSync(path.join(root, relativePath), '{');
    const errors = validateRepository(root);
    assert.equal(errors.length, 1);
    assert.equal(errors[0].startsWith(`${description} is not valid JSON:`), true);
  }
});

test('rejects non-object JSON roots for static bridge configuration', (t) => {
  const cases = [
    ['null', null],
    ['false', false],
    ['zero', 0],
    ['empty string', ''],
    ['array', []],
    ['non-empty array', ['value']],
  ];
  const files = [
    [path.join('.squad', 'config.json'), '.squad/config.json'],
    ['.mcp.json', '.mcp.json'],
  ];

  for (const [relativePath, description] of files) {
    for (const [name, value] of cases) {
      const root = fixture(t);
      writeJson(root, relativePath, value);
      assert.deepEqual(
        validateRepository(root),
        [`${description} top-level value must be a non-null, non-array JSON object`],
        `${description} should reject a ${name} root`,
      );
    }
  }
});

test('rejects non-object mcpServers containers without throwing', (t) => {
  const cases = [
    ['null', null],
    ['empty array', []],
    ['non-empty array', ['value']],
    ['string', 'value'],
    ['number', 1],
    ['boolean', true],
  ];

  for (const [name, value] of cases) {
    const root = fixture(t);
    writeJson(root, '.mcp.json', { mcpServers: value });
    let errors;
    assert.doesNotThrow(
      () => {
        errors = validateRepository(root);
      },
      `.mcp.json should handle a ${name} mcpServers container`,
    );
    assert.deepEqual(
      errors,
      ['.mcp.json mcpServers must be a non-null, non-array JSON object'],
    );
  }
});

test('rejects non-object squad_state server values without throwing', (t) => {
  const cases = [
    ['null', null],
    ['empty array', []],
    ['non-empty array', ['value']],
    ['string', 'value'],
    ['number', 1],
    ['boolean', true],
  ];

  for (const [name, value] of cases) {
    const root = fixture(t);
    writeJson(root, '.mcp.json', {
      mcpServers: {
        squad_state: value,
      },
    });
    let errors;
    assert.doesNotThrow(
      () => {
        errors = validateRepository(root);
      },
      `.mcp.json should handle a ${name} squad_state server`,
    );
    assert.deepEqual(
      errors,
      ['squad_state MCP server must be a non-null, non-array JSON object'],
    );
  }
});

test('rejects a broadened MCP bridge', (t) => {
  const root = fixture(t);
  writeJson(root, '.mcp.json', {
    mcpServers: {
      squad_state: {
        command: 'npx',
        args: ['-y', `@bradygaster/squad-cli@${SQUAD_VERSION}`, 'state-mcp'],
        env: {},
        tools: REQUIRED_MCP_TOOLS,
      },
      unrelated: { command: 'other' },
    },
  });
  assert.match(validateRepository(root).join('\n'), /only the squad_state MCP server/);
});

test('rejects extra squad_state MCP server fields', (t) => {
  const cases = [
    ['type and url', { type: 'http', url: 'https://example.invalid/mcp' }],
    ['cwd', { cwd: 'alternate-directory' }],
    ['timeout', { timeout: 30_000 }],
  ];

  for (const [name, extraFields] of cases) {
    const root = fixture(t);
    writeJson(root, '.mcp.json', {
      mcpServers: {
        squad_state: {
          command: 'npx',
          args: ['-y', `@bradygaster/squad-cli@${SQUAD_VERSION}`, 'state-mcp'],
          env: {},
          tools: REQUIRED_MCP_TOOLS,
          ...extraFields,
        },
      },
    });
    assert.deepEqual(
      validateRepository(root),
      ['squad_state MCP server keys must exactly match: command, args, env, tools'],
      `should reject extra ${name} fields`,
    );
  }
});

test('rejects a mismatched bridge package version', (t) => {
  const root = fixture(t);
  writeJson(root, '.mcp.json', {
    mcpServers: {
      squad_state: {
        command: 'npx',
        args: ['-y', '@bradygaster/squad-cli@0.11.0', 'state-mcp'],
        env: {},
        tools: REQUIRED_MCP_TOOLS,
      },
    },
  });
  assert.match(validateRepository(root).join('\n'), /squad-cli@0\.12\.0/);
});

test('rejects a wildcard MCP tool grant', (t) => {
  const root = fixture(t);
  writeJson(root, '.mcp.json', {
    mcpServers: {
      squad_state: {
        command: 'npx',
        args: ['-y', `@bradygaster/squad-cli@${SQUAD_VERSION}`, 'state-mcp'],
        env: {},
        tools: ['*'],
      },
    },
  });
  assert.match(validateRepository(root).join('\n'), /tools must exactly match/);
});

test('rejects an incomplete MCP tool grant', (t) => {
  const root = fixture(t);
  writeJson(root, '.mcp.json', {
    mcpServers: {
      squad_state: {
        command: 'npx',
        args: ['-y', `@bradygaster/squad-cli@${SQUAD_VERSION}`, 'state-mcp'],
        env: {},
        tools: REQUIRED_MCP_TOOLS.slice(0, -1),
      },
    },
  });
  assert.match(validateRepository(root).join('\n'), /tools must exactly match/);
});

test('rejects truncated coordinator instructions', (t) => {
  const root = fixture(t);
  const coordinator = path.join(root, '.github', 'agents', 'squad.agent.md');
  fs.writeFileSync(
    coordinator,
    fs.readFileSync(coordinator, 'utf8').replace(EOF_CANARY, ''),
  );
  assert.match(validateRepository(root).join('\n'), /HEAD and EOF canary/);
});

test('rejects coordinator content after the EOF canary', (t) => {
  const root = fixture(t);
  const coordinator = path.join(root, '.github', 'agents', 'squad.agent.md');
  fs.appendFileSync(coordinator, '\ntruncated-at-runtime content\n');
  assert.match(validateRepository(root).join('\n'), /must end with the EOF canary marker/);
});

test('rejects an empty readable coordinator file', (t) => {
  const root = fixture(t);
  fs.writeFileSync(path.join(root, '.github', 'agents', 'squad.agent.md'), '');
  assert.match(validateRepository(root).join('\n'), /squad\.agent\.md must not be empty/);
});

test('checked-in coordinator and template retain synchronized host-neutral recovery wording', () => {
  const repositoryRoot = path.resolve(__dirname, '..', '..');
  const sources = [
    path.join(repositoryRoot, '.github', 'agents', 'squad.agent.md'),
    path.join(repositoryRoot, '.squad', 'templates', 'squad.agent.md.template'),
  ];
  const recoveryParagraphs = sources.map(source => fs.readFileSync(source, 'utf8')
    .split(/\r?\n/u)
    .filter(line => line.startsWith('3. **If the probe fails**')));

  for (const paragraphs of recoveryParagraphs) {
    assert.deepEqual(paragraphs, [PROBE_FAILURE_RECOVERY_PARAGRAPH]);
    assert.equal(paragraphs[0].includes(HOST_NEUTRAL_RECOVERY_SENTENCE), true);
  }
  assert.equal(recoveryParagraphs[0][0], recoveryParagraphs[1][0]);
});

test('checked-in coordinator and template prohibit raw archived-history writes', () => {
  const repositoryRoot = path.resolve(__dirname, '..', '..');
  const sources = [
    path.join(repositoryRoot, '.github', 'agents', 'squad.agent.md'),
    path.join(repositoryRoot, '.squad', 'templates', 'squad.agent.md.template'),
  ];
  const prohibitionLists = sources.map(source => parseBoundedBulletList(
    fs.readFileSync(source, 'utf8'),
    NON_LOCAL_RAW_FILE_PROHIBITION_HEADING,
  ));
  const requiredLines = REQUIRED_NON_LOCAL_RAW_FILE_PROHIBITIONS.map(
    protectedPath => `- \`${protectedPath}\``,
  );

  for (const prohibitionList of prohibitionLists) {
    assert.notEqual(prohibitionList, null);
    for (const requiredLine of requiredLines) {
      assert.equal(prohibitionList.filter(line => line === requiredLine).length, 1);
    }
  }
  assert.deepEqual(prohibitionLists[0], prohibitionLists[1]);
});

test('rejects loss of the fail-closed ownership contract', (t) => {
  const root = fixture(t);
  const coordinator = path.join(root, '.github', 'agents', 'squad.agent.md');
  fs.writeFileSync(
    coordinator,
    fs.readFileSync(coordinator, 'utf8')
      .replace('Do not silently fall back to raw file ops.', ''),
  );
  assert.match(validateRepository(root).join('\n'), /ownership and fail-closed rules/);
});

test('rejects an archived-history prohibition moved outside its bounded list', (t) => {
  const root = fixture(t);
  const coordinator = path.join(root, '.github', 'agents', 'squad.agent.md');
  const archiveLine = '- `.squad/agents/*/history-archive.md`';
  fs.writeFileSync(
    coordinator,
    fs.readFileSync(coordinator, 'utf8')
      .replace(`${archiveLine}\n\n`, `\n${archiveLine}\n`),
  );
  assert.match(
    validateRepository(root).join('\n'),
    /prohibition list must contain exactly once.*\.squad\/agents\/\*\/history-archive\.md/,
  );
});

test('fresh-child acceptance uses only the ephemeral governed probe namespace', () => {
  const runbook = fs.readFileSync(
    path.resolve(__dirname, '..', '..', 'docs', 'operations', 'squad-state-bridge.md'),
    'utf8',
  );
  assert.deepEqual(freshChildProbeStepErrors(runbook), []);
});

test('fresh-child step rejects negated governed probe operations', () => {
  const runbook = fs.readFileSync(
    path.resolve(__dirname, '..', '..', 'docs', 'operations', 'squad-state-bridge.md'),
    'utf8',
  );
  const negatedInstructions = [
    [/use `squad_state_write`/u, 'do not use `squad_state_write`'],
    [/delete it with `squad_state_delete`/u, 'do not delete it with `squad_state_delete`'],
    [
      /confirm a final\r?\n   read reports key-not-found/u,
      'do not confirm a final\n   read reports key-not-found',
    ],
  ];

  for (const [affirmativePattern, negated] of negatedInstructions) {
    const variant = runbook.replace(affirmativePattern, negated);
    assert.notEqual(variant, runbook);
    assert.deepEqual(
      freshChildProbeStepErrors(variant),
      ['fresh-child acceptance step 5 must retain the canonical affirmative probe sequence'],
    );
  }
});

test('fresh-child step rejects struck-through governed probe operations', () => {
  const runbook = fs.readFileSync(
    path.resolve(__dirname, '..', '..', 'docs', 'operations', 'squad-state-bridge.md'),
    'utf8',
  );
  const struckInstructions = [
    [/use `squad_state_write`/u, '~~use `squad_state_write`~~'],
    [
      /delete it with `squad_state_delete`/u,
      '~~delete it with `squad_state_delete`~~',
    ],
  ];

  for (const [affirmativePattern, struck] of struckInstructions) {
    const variant = runbook.replace(affirmativePattern, struck);
    assert.notEqual(variant, runbook);
    assert.deepEqual(
      freshChildProbeStepErrors(variant),
      ['fresh-child acceptance step 5 must not contain Markdown strikethrough markup'],
    );
  }
});

test('fresh-child step rejects a rendered Markdown-obscured log probe path', () => {
  const runbook = fs.readFileSync(
    path.resolve(__dirname, '..', '..', 'docs', 'operations', 'squad-state-bridge.md'),
    'utf8',
  );
  const variant = runbook.replace(
    '\n6. Attempt a non-sensitive write',
    '\n   Also probe **log**/probe.md.\n6. Attempt a non-sensitive write',
  );

  assert.deepEqual(
    freshChildProbeStepErrors(variant),
    [
      'fresh-child acceptance step 5 must not use a log/ probe path',
      'fresh-child acceptance step 5 must retain the canonical affirmative probe sequence',
    ],
  );
});

test('rejects CLI-only probe-failure recovery advice', (t) => {
  const root = fixture(t);
  const coordinator = path.join(root, '.github', 'agents', 'squad.agent.md');
  fs.writeFileSync(
    coordinator,
    fs.readFileSync(coordinator, 'utf8').replace(
      HOST_NEUTRAL_RECOVERY_SENTENCE,
      'Restart Copilot CLI so `.mcp.json` is loaded, then start a fresh session.',
    ),
  );
  assert.match(validateRepository(root).join('\n'), /host-neutral probe-failure recovery paragraph/);
});

test('rejects a state-backend downgrade paraphrase as probe-failure recovery', (t) => {
  const root = fixture(t);
  const coordinator = path.join(root, '.github', 'agents', 'squad.agent.md');
  fs.writeFileSync(
    coordinator,
    fs.readFileSync(coordinator, 'utf8').replace(
      PROBE_FAILURE_RECOVERY_PARAGRAPH,
      '3. **If the probe fails**: HALT before any state write. Set stateBackend to local and retry.',
    ),
  );
  assert.match(validateRepository(root).join('\n'), /host-neutral probe-failure recovery paragraph/);
});

test('rejects duplicate or conflicting coordinator version markers', (t) => {
  for (const additionalVersion of [SQUAD_VERSION, '0.13.0']) {
    const root = fixture(t);
    const coordinator = path.join(root, '.github', 'agents', 'squad.agent.md');
    fs.writeFileSync(
      coordinator,
      fs.readFileSync(coordinator, 'utf8').replace(
        `<!-- version: ${SQUAD_VERSION} -->`,
        `<!-- version: ${SQUAD_VERSION} -->\n<!-- version: ${additionalVersion} -->`,
      ),
    );
    assert.match(validateRepository(root).join('\n'), /exactly one version marker/);
  }
});

test('rejects a conflicting reported Squad version', (t) => {
  const root = fixture(t);
  const coordinator = path.join(root, '.github', 'agents', 'squad.agent.md');
  fs.writeFileSync(
    coordinator,
    fs.readFileSync(coordinator, 'utf8').replace(
      `Report \`Squad v${SQUAD_VERSION}\`.`,
      `Report \`Squad v${SQUAD_VERSION}\`, then report \`Squad v0.13.0\`.`,
    ),
  );
  assert.match(validateRepository(root).join('\n'), /must report only Squad v0\.12\.0/);
});

test('rejects prefixed conflicting numeric Squad versions', (t) => {
  const root = fixture(t);
  const coordinator = path.join(root, '.github', 'agents', 'squad.agent.md');
  fs.writeFileSync(
    coordinator,
    fs.readFileSync(coordinator, 'utf8').replace(
      `Report \`Squad v${SQUAD_VERSION}\`.`,
      `Report \`Squad v${SQUAD_VERSION}\`, \`_Squad v0.13.0\`, and \`xSquad v0.13.0\`.`,
    ),
  );
  assert.match(validateRepository(root).join('\n'), /must report only Squad v0\.12\.0/);
});

test('rejects canonical plus malformed reported Squad version tokens', (t) => {
  const malformedVersions = [
    `${SQUAD_VERSION}.1`,
    `${SQUAD_VERSION}+`,
    `${SQUAD_VERSION}-`,
  ];

  for (const malformedVersion of malformedVersions) {
    const root = fixture(t);
    const coordinator = path.join(root, '.github', 'agents', 'squad.agent.md');
    fs.writeFileSync(
      coordinator,
      fs.readFileSync(coordinator, 'utf8').replace(
        `Report \`Squad v${SQUAD_VERSION}\`.`,
        `Report \`Squad v${SQUAD_VERSION}\`, then report \`Squad v${malformedVersion}\`.`,
      ),
    );
    assert.match(
      validateRepository(root).join('\n'),
      /must report only Squad v0\.12\.0/,
      `should reject Squad v${malformedVersion}`,
    );
  }
});

test('rejects a later gitignore negation of runtime-owned state', (t) => {
  const root = fixture(t);
  fs.appendFileSync(path.join(root, '.gitignore'), '\n!.squad/decisions.md\n');
  assert.match(
    validateRepository(root).join('\n'),
    /effectively exclude runtime-owned state: \.squad\/decisions\.md/,
  );
});

test('rejects a sentinel-only ignore that leaves the agent history namespace exposed', (t) => {
  const root = fixture(t);
  const gitignore = path.join(root, '.gitignore');
  fs.writeFileSync(
    gitignore,
    fs.readFileSync(gitignore, 'utf8').replace(
      '.squad/agents/*/history.md',
      '.squad/agents/state-bridge-validation/history.md',
    ),
  );
  const exposed = spawnSync(
    'git',
    ['check-ignore', '--quiet', '--no-index', '.squad/agents/data/history.md'],
    { cwd: root },
  );
  assert.equal(exposed.status, 1);
  assert.match(
    validateRepository(root).join('\n'),
    /must contain canonical protected ignore rule: \.squad\/agents\/\*\/history\.md/,
  );
});

test('rejects a targeted agent history negation', (t) => {
  const root = fixture(t);
  fs.appendFileSync(path.join(root, '.gitignore'), '\n!.squad/agents/data/history.md\n');
  assert.match(
    validateRepository(root).join('\n'),
    /must not contain negation rules \(line \d+: !\.squad\/agents\/data\/history\.md\)/,
  );
});

test('rejects a nested tracked gitignore negation of agent history', (t) => {
  const root = fixture(t);
  const agentDirectory = path.join(root, '.squad', 'agents', 'data');
  fs.mkdirSync(agentDirectory, { recursive: true });
  fs.writeFileSync(path.join(agentDirectory, '.gitignore'), '!history.md\n');
  execFileSync('git', ['add', '.squad/agents/data/.gitignore'], { cwd: root });
  assert.match(
    validateRepository(root).join('\n'),
    /\.squad\/agents\/data\/\.gitignore must not contain negation rules \(line 1: !history\.md\)/,
  );
});

test('rejects a BOM-prefixed nested tracked gitignore negation of agent history', (t) => {
  const root = fixture(t);
  const agentDirectory = path.join(root, '.squad', 'agents', 'data');
  fs.mkdirSync(agentDirectory, { recursive: true });
  fs.writeFileSync(path.join(agentDirectory, '.gitignore'), '\uFEFF!history.md\n');
  execFileSync('git', ['add', '.squad/agents/data/.gitignore'], { cwd: root });
  assert.match(
    validateRepository(root).join('\n'),
    /\.squad\/agents\/data\/\.gitignore must not contain negation rules \(line 1: !history\.md\)/,
  );
});

test('rejects a protected-directory exception', (t) => {
  const root = fixture(t);
  fs.appendFileSync(path.join(root, '.gitignore'), '\n!/.squad/decisions/inbox/\n');
  assert.match(
    validateRepository(root).join('\n'),
    /must not contain negation rules \(line \d+: !\/\.squad\/decisions\/inbox\/\)/,
  );
});

test('rejects a character-class spelling of a private history path', (t) => {
  const root = fixture(t);
  fs.appendFileSync(path.join(root, '.gitignore'), '\n!.[s]quad/agents/data/history.md\n');
  assert.match(
    validateRepository(root).join('\n'),
    /must not contain negation rules \(line \d+: !\.\[s\]quad\/agents\/data\/history\.md\)/,
  );
});

test('rejects a wildcard spelling of a private history path', (t) => {
  const root = fixture(t);
  fs.appendFileSync(path.join(root, '.gitignore'), '\n!?squad/agents/data/history.md\n');
  assert.match(
    validateRepository(root).join('\n'),
    /must not contain negation rules \(line \d+: !\?squad\/agents\/data\/history\.md\)/,
  );
});

test('rejects every active gitignore negation rule', (t) => {
  const root = fixture(t);
  fs.appendFileSync(path.join(root, '.gitignore'), '\n!docs/example.md\n');
  assert.match(
    validateRepository(root).join('\n'),
    /must not contain negation rules \(line \d+: !docs\/example\.md\)/,
  );
});

test('rejects an info exclude as the only protected-state ignore source', (t) => {
  const root = fixture(t);
  fs.writeFileSync(
    path.join(root, '.gitignore'),
    REQUIRED_IGNORES.filter(rule => rule !== '.squad/decisions.md').join('\n'),
  );
  fs.appendFileSync(path.join(root, '.git', 'info', 'exclude'), '\n.squad/decisions.md\n');
  assert.match(
    validateRepository(root).join('\n'),
    /matched by non-repository rule \.git\/info\/exclude/,
  );
});

test('rejects a configured global exclude as the only protected-state ignore source', (t) => {
  const root = fixture(t);
  const globalExclude = path.join(root, 'global-ignore');
  fs.writeFileSync(
    path.join(root, '.gitignore'),
    REQUIRED_IGNORES.filter(rule => rule !== '.squad/decisions.md').join('\n'),
  );
  fs.writeFileSync(globalExclude, '.squad/decisions.md\n');
  execFileSync('git', ['config', 'core.excludesFile', globalExclude], { cwd: root });
  assert.match(
    validateRepository(root).join('\n'),
    /matched by non-repository rule .*global-ignore/,
  );
});

test('rejects force-tracked runtime-owned state', (t) => {
  const root = fixture(t);
  const decisions = path.join(root, '.squad', 'decisions.md');
  fs.writeFileSync(decisions, 'runtime-owned\n');
  execFileSync('git', ['add', '--force', '.squad/decisions.md'], { cwd: root });
  assert.match(
    validateRepository(root).join('\n'),
    /runtime-owned state must not be tracked: \.squad\/decisions\.md/,
  );
});

test('rejects force-tracked archived agent history', (t) => {
  const root = fixture(t);
  const archive = path.join(root, '.squad', 'agents', 'data', 'history-archive.md');
  fs.mkdirSync(path.dirname(archive), { recursive: true });
  fs.writeFileSync(archive, 'runtime-owned\n');
  execFileSync('git', ['add', '--force', '.squad/agents/data/history-archive.md'], { cwd: root });
  assert.match(
    validateRepository(root).join('\n'),
    /runtime-owned state must not be tracked: \.squad\/agents\/data\/history-archive\.md/,
  );
});
