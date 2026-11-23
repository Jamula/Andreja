# Ceremonies

> Team meetings that happen before or after work. Each squad configures their own.

## Design Review

| Field | Value |
|-------|-------|
| **Trigger** | auto |
| **When** | before |
| **Condition** | multi-agent task involving 2+ agents modifying shared systems |
| **Facilitator** | lead |
| **Participants** | all-relevant |
| **Time budget** | focused |
| **Enabled** | ✅ yes |

**Agenda:**
1. Review the task and requirements
2. Agree on interfaces and contracts between components
3. Identify risks and edge cases
4. Assign action items

---

## Retrospective

| Field | Value |
|-------|-------|
| **Trigger** | auto |
| **When** | after |
| **Condition** | build failure, test failure, or reviewer rejection |
| **Facilitator** | lead |
| **Participants** | all-involved |
| **Time budget** | focused |
| **Enabled** | ✅ yes |

**Agenda:**
1. What happened? (facts only)
2. Root cause analysis
3. What should change?
4. Action items for next iteration


---

## Retrospective with Enforcement

| Field | Value |
|-------|-------|
| **Trigger** | auto |
| **When** | before |
| **Condition** | No validated canonical weekly completion record in .squad/log/ within the last 7 days; a failure-triggered retrospective counts only if it meets every weekly requirement |
| **Facilitator** | lead |
| **Participants** | all |
| **Time budget** | focused |
| **Enabled** | yes |
| **Enforcement** | manual pending [#297](https://github.com/Jamula/Andreja/issues/297) |

**Agenda:**
1. What shipped this week? (closed issues, merged PRs)
2. What did not ship? (open issues, blockers)
3. Root cause on any failures
4. Action items -- each MUST become a GitHub Issue labeled retro-action

**Coordinator integration:**
At round start, enumerate retrospective logs through the configured Squad state
backend. Before admitting work, require a canonical weekly completion record
from the previous seven days and validate it against the weekly agenda and
evidence requirements, including the required `retro-action` issues. A
failure-triggered retrospective does not count unless it satisfies every weekly
requirement. If state is unavailable or the log inventory or record cannot be
validated, stop rather than treating the check as passed. The facilitator
creates the action issues; verify the canonical record and issue labels before
resuming.

The current configuration uses worktree-local state. A successful local inventory
does not establish the team's retrospective history across other worktrees or
sessions. Do not treat it as a team-wide admission check or exactly-once proof.

The former issue-drain admission implementation was removed in PR #250 and was
not restored by PR #252. The `retro-enforcement` example under `.squad/templates/`
is not an installed skill or a scheduler. Squad CLI 1.1.0 offers
`squad triage --retro`, but its housekeeping check runs after triage/execution,
can report completion without verifying a record, and can trigger repeatedly
on Friday. Do not use it as the admission or exactly-once gate. This manual
procedure does not prove automatic admission or exactly-once operation; #297
remains open for those operational checks and two consecutive on-time cycles.

**Why GitHub Issues, not markdown:**
Production data: 0% completion across 6 retros using markdown checklists, 100% after switching to GitHub Issues.
