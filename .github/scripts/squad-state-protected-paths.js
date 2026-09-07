'use strict';

const path = require('node:path');

const PROTECTED_PATHS = Object.freeze([
  Object.freeze(['.squad/orchestration-log/', '.squad/orchestration-log/.state-bridge-validation', ':(glob).squad/orchestration-log/**']),
  Object.freeze(['.squad/log/', '.squad/log/.state-bridge-validation', ':(glob).squad/log/**']),
  Object.freeze(['.squad/decisions/inbox/', '.squad/decisions/inbox/.state-bridge-validation', ':(glob).squad/decisions/inbox/**']),
  Object.freeze(['.squad/sessions/', '.squad/sessions/.state-bridge-validation', ':(glob).squad/sessions/**']),
  Object.freeze(['.squad/.scratch/', '.squad/.scratch/.state-bridge-validation', ':(glob).squad/.scratch/**']),
  Object.freeze(['.squad/.cache/', '.squad/.cache/.state-bridge-validation', ':(glob).squad/.cache/**']),
  Object.freeze(['.squad/decisions.md', '.squad/decisions.md', '.squad/decisions.md']),
  Object.freeze(['.squad/agents/*/history.md', '.squad/agents/state-bridge-validation/history.md', ':(glob).squad/agents/*/history.md']),
  Object.freeze(['.squad/agents/*/history-archive.md', '.squad/agents/state-bridge-validation/history-archive.md', ':(glob).squad/agents/*/history-archive.md']),
  Object.freeze(['.squad/casting/history.json', '.squad/casting/history.json', '.squad/casting/history.json']),
  Object.freeze(['.squad/identity/', '.squad/identity/.state-bridge-validation', ':(glob).squad/identity/**']),
  Object.freeze(['.squad/memory/', '.squad/memory/.state-bridge-validation', ':(glob).squad/memory/**']),
  Object.freeze(['.squad/rai/audit-trail.md', '.squad/rai/audit-trail.md', '.squad/rai/audit-trail.md']),
  Object.freeze(['.squad/fact-checker/audit-trail.md', '.squad/fact-checker/audit-trail.md', '.squad/fact-checker/audit-trail.md']),
]);

const PROTECTED_DIRECTORY_PATHS = Object.freeze(
  PROTECTED_PATHS
    .map(([ignorePattern]) => ignorePattern)
    .filter(ignorePattern => ignorePattern.endsWith('/'))
    .map(ignorePattern => ignorePattern.slice(0, -1)),
);
const PROTECTED_AGENT_FILES = Object.freeze(
  PROTECTED_PATHS
    .map(([ignorePattern]) => ignorePattern)
    .filter(ignorePattern => ignorePattern.startsWith('.squad/agents/*/'))
    .map(ignorePattern => path.posix.basename(ignorePattern)),
);
const PROTECTED_LITERAL_FILES = Object.freeze(
  PROTECTED_PATHS
    .map(([ignorePattern]) => ignorePattern)
    .filter(ignorePattern => !ignorePattern.endsWith('/') && !ignorePattern.includes('*')),
);

function isProtectedPathIdentity(identity) {
  if (PROTECTED_LITERAL_FILES.includes(identity)) {
    return true;
  }
  if (PROTECTED_DIRECTORY_PATHS.some(directory =>
    identity === directory || identity.startsWith(`${directory}/`))) {
    return true;
  }
  return PROTECTED_AGENT_FILES.some(file =>
    new RegExp(`^\\.squad/agents/[^/]+/${file.replace('.', '\\.')}$`, 'u').test(identity));
}

module.exports = {
  PROTECTED_AGENT_FILES,
  PROTECTED_DIRECTORY_PATHS,
  PROTECTED_LITERAL_FILES,
  PROTECTED_PATHS,
  isProtectedPathIdentity,
};
