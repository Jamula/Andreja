'use strict';

const fs = require('node:fs');
const path = require('node:path');
const { spawnSync } = require('node:child_process');

const SQUAD_VERSION = '0.12.0';
const HEAD_CANARY = 'SQUAD_COORDINATOR_CANARY_HEAD_b7d2';
const EOF_CANARY = 'SQUAD_COORDINATOR_CANARY_a8f3';
const PROTECTED_PATHS = [
  ['.squad/orchestration-log/', '.squad/orchestration-log/.state-bridge-validation', ':(glob).squad/orchestration-log/**'],
  ['.squad/log/', '.squad/log/.state-bridge-validation', ':(glob).squad/log/**'],
  ['.squad/decisions/inbox/', '.squad/decisions/inbox/.state-bridge-validation', ':(glob).squad/decisions/inbox/**'],
  ['.squad/sessions/', '.squad/sessions/.state-bridge-validation', ':(glob).squad/sessions/**'],
  ['.squad/.scratch/', '.squad/.scratch/.state-bridge-validation', ':(glob).squad/.scratch/**'],
  ['.squad/.cache/', '.squad/.cache/.state-bridge-validation', ':(glob).squad/.cache/**'],
  ['.squad/decisions.md', '.squad/decisions.md', '.squad/decisions.md'],
  ['.squad/agents/*/history.md', '.squad/agents/state-bridge-validation/history.md', ':(glob).squad/agents/*/history.md'],
  ['.squad/casting/history.json', '.squad/casting/history.json', '.squad/casting/history.json'],
  ['.squad/identity/', '.squad/identity/.state-bridge-validation', ':(glob).squad/identity/**'],
  ['.squad/memory/', '.squad/memory/.state-bridge-validation', ':(glob).squad/memory/**'],
  ['.squad/rai/audit-trail.md', '.squad/rai/audit-trail.md', '.squad/rai/audit-trail.md'],
  ['.squad/fact-checker/audit-trail.md', '.squad/fact-checker/audit-trail.md', '.squad/fact-checker/audit-trail.md'],
];
const REQUIRED_IGNORES = PROTECTED_PATHS.map(([ignorePattern]) => ignorePattern);

function isPlainObject(value) {
  return value !== null &&
    !Array.isArray(value) &&
    typeof value === 'object' &&
    Object.getPrototypeOf(value) === Object.prototype;
}

function readJson(file, errors, description) {
  try {
    const value = JSON.parse(fs.readFileSync(file, 'utf8'));
    if (!isPlainObject(value)) {
      errors.push(`${description} top-level value must be a non-null, non-array JSON object`);
      return null;
    }
    return value;
  } catch (error) {
    errors.push(`${path.relative(path.dirname(file), file) || path.basename(file)} is not valid JSON: ${error.message}`);
    return null;
  }
}

function count(text, value) {
  return text.split(value).length - 1;
}

function git(root, args) {
  return spawnSync('git', args, {
    cwd: root,
    encoding: 'utf8',
    windowsHide: true,
  });
}

function gitignoreNegationRules(gitignore) {
  const normalized = gitignore.startsWith('\uFEFF') ? gitignore.slice(1) : gitignore;
  return normalized.split(/\r?\n/u).flatMap((line, index) => {
    if (!line.startsWith('!')) {
      return [];
    }

    return [{ line: index + 1, rule: line }];
  });
}

function repositoryGitignorePaths(root, errors) {
  const paths = new Set(['.gitignore']);
  const result = git(root, [
    'ls-files',
    '--cached',
    '--others',
    '--exclude-standard',
    '-z',
    '--',
    '.gitignore',
    ':(glob)**/.gitignore',
  ]);

  if (result.error) {
    errors.push(`git is required to enumerate repository .gitignore files: ${result.error.message}`);
  } else if (result.status !== 0) {
    errors.push(`git ls-files failed while enumerating repository .gitignore files: ${(result.stderr || '').trim() || `exit ${result.status}`}`);
  } else {
    for (const gitignorePath of result.stdout.split('\0').filter(Boolean)) {
      paths.add(gitignorePath);
    }
  }

  return [...paths].sort();
}

function validateRepository(root) {
  const errors = [];
  const configPath = path.join(root, '.squad', 'config.json');
  const mcpPath = path.join(root, '.mcp.json');
  const coordinatorPath = path.join(root, '.github', 'agents', 'squad.agent.md');

  const config = readJson(configPath, errors, '.squad/config.json');
  if (config) {
    if (config.version !== 1) {
      errors.push('.squad/config.json must keep version 1');
    }
    if (config.stateBackend !== 'two-layer') {
      errors.push('.squad/config.json stateBackend must be "two-layer"; do not use a local fallback');
    }
  }

  const mcp = readJson(mcpPath, errors, '.mcp.json');
  if (mcp) {
    if (!isPlainObject(mcp.mcpServers)) {
      errors.push('.mcp.json mcpServers must be a non-null, non-array JSON object');
    } else {
      const serverNames = Object.keys(mcp.mcpServers);
      if (serverNames.length !== 1 || serverNames[0] !== 'squad_state') {
        errors.push('.mcp.json must declare only the squad_state MCP server');
      } else {
        const server = mcp.mcpServers.squad_state;
        if (!isPlainObject(server)) {
          errors.push('squad_state MCP server must be a non-null, non-array JSON object');
        } else {
          const expectedArgs = ['-y', `@bradygaster/squad-cli@${SQUAD_VERSION}`, 'state-mcp'];
          if (server.command !== 'npx' ||
              JSON.stringify(server.args) !== JSON.stringify(expectedArgs)) {
            errors.push(`squad_state must run npx ${expectedArgs.join(' ')}`);
          }
          if (!isPlainObject(server.env) || Object.keys(server.env).length !== 0) {
            errors.push('squad_state must not inject environment values');
          }
          if (JSON.stringify(server.tools) !== JSON.stringify(['*'])) {
            errors.push('squad_state must expose its governed state and memory tool surface');
          }
        }
      }
    }
  }

  let coordinator = null;
  try {
    coordinator = fs.readFileSync(coordinatorPath, 'utf8');
  } catch (error) {
    errors.push(`.github/agents/squad.agent.md is unreadable: ${error.message}`);
  }
  if (coordinator !== null && coordinator.trim().length === 0) {
    errors.push('.github/agents/squad.agent.md must not be empty');
  } else if (coordinator !== null) {
    if (count(coordinator, HEAD_CANARY) !== 1 ||
        count(coordinator, EOF_CANARY) !== 1 ||
        coordinator.indexOf(HEAD_CANARY) > coordinator.indexOf(EOF_CANARY)) {
      errors.push('squad.agent.md must contain one ordered HEAD and EOF canary');
    }
    if (!coordinator.includes(`<!-- version: ${SQUAD_VERSION} -->`) ||
        !coordinator.includes(`Squad v${SQUAD_VERSION}`)) {
      errors.push(`squad.agent.md must retain the ${SQUAD_VERSION} version stamp`);
    }
    if (!coordinator.includes('Static config (charters, team.md, routing.md) always lives on disk') ||
        !coordinator.includes('runtime owns persistence and you MUST NOT touch') ||
        !coordinator.includes('Do not silently fall back to raw file ops.')) {
      errors.push('squad.agent.md must retain static/mutable ownership and fail-closed rules');
    }
  }

  for (const gitignorePath of repositoryGitignorePaths(root, errors)) {
    const absolutePath = path.resolve(root, gitignorePath);
    const relativePath = path.relative(root, absolutePath);
    if (relativePath === '' ||
        relativePath === '..' ||
        relativePath.startsWith(`..${path.sep}`) ||
        path.isAbsolute(relativePath)) {
      errors.push(`refusing to inspect .gitignore outside the repository: ${gitignorePath}`);
      continue;
    }

    let gitignore = null;
    try {
      const stat = fs.lstatSync(absolutePath);
      if (!stat.isFile()) {
        errors.push(`${gitignorePath} must be a regular file`);
        continue;
      }
      gitignore = fs.readFileSync(absolutePath, 'utf8');
    } catch (error) {
      errors.push(`${gitignorePath} is unreadable: ${error.message}`);
    }
    if (gitignore !== null) {
      for (const negation of gitignoreNegationRules(gitignore)) {
        errors.push(
          `${gitignorePath} must not contain negation rules (line ${negation.line}: ${negation.rule})`,
        );
      }
    }
  }

  for (const [ignorePattern, probePath] of PROTECTED_PATHS) {
    const result = git(root, ['check-ignore', '--quiet', '--no-index', '--', probePath]);
    if (result.error) {
      errors.push(`git is required to validate runtime-owned state: ${result.error.message}`);
      break;
    }
    if (result.status === 1) {
      errors.push(`.gitignore must effectively exclude runtime-owned state: ${ignorePattern}`);
    } else if (result.status !== 0) {
      errors.push(`git check-ignore failed for ${probePath}: ${(result.stderr || '').trim() || `exit ${result.status}`}`);
    }
  }

  const tracked = git(root, [
    'ls-files',
    '--cached',
    '-z',
    '--',
    ...PROTECTED_PATHS.map(([, , pathspec]) => pathspec),
  ]);
  if (tracked.error) {
    errors.push(`git is required to inspect tracked runtime-owned state: ${tracked.error.message}`);
  } else if (tracked.status !== 0) {
    errors.push(`git ls-files failed while inspecting runtime-owned state: ${(tracked.stderr || '').trim() || `exit ${tracked.status}`}`);
  } else {
    const trackedPaths = tracked.stdout.split('\0').filter(Boolean);
    if (trackedPaths.length > 0) {
      errors.push(`runtime-owned state must not be tracked: ${trackedPaths.join(', ')}`);
    }
  }

  return errors;
}

function main() {
  const root = path.resolve(__dirname, '..', '..');
  const errors = validateRepository(root);
  if (errors.length > 0) {
    for (const error of errors) {
      console.error(`ERROR: ${error}`);
    }
    console.error('Squad state bridge static contract validation failed.');
    process.exitCode = 1;
    return;
  }
  console.log(`Squad ${SQUAD_VERSION} two-layer static contract is valid.`);
}

if (require.main === module) {
  main();
}

module.exports = {
  EOF_CANARY,
  HEAD_CANARY,
  REQUIRED_IGNORES,
  SQUAD_VERSION,
  validateRepository,
};
