'use strict';

const fs = require('node:fs');
const { parseRoster } = require('../../.squad/templates/ralph-triage.js');

function checkReadiness(team) {
  const roster = parseRoster(team);
  const hasFallback = roster.some(({ role }) =>
    (role.toLowerCase().match(/[a-z0-9]+/g) || [])
      .some(token => token === 'lead' || token === 'architect'));
  if (!hasFallback) {
    throw new Error(
      'Squad triage is not ready: .squad/team.md needs a configured member with a Lead or Architect role. Complete the approved roster and routing before retrying.'
    );
  }
}

if (require.main === module) {
  try {
    checkReadiness(fs.readFileSync('.squad/team.md', 'utf8'));
    console.log('Squad roster has a triage fallback.');
  } catch (error) {
    console.error(error.message);
    process.exitCode = 1;
  }
}

module.exports = { checkReadiness };
