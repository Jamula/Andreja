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
| **When** | weekly |
| **Condition** | No *retrospective* log in .squad/log/ within the last 7 days |
| **Facilitator** | lead |
| **Participants** | all |
| **Time budget** | focused |
| **Enabled** | yes |
| **Enforcement skill** | retro-enforcement |

**Agenda:**
1. What shipped this week? (closed issues, merged PRs)
2. What did not ship? (open issues, blockers)
3. Root cause on any failures
4. Action items -- each MUST become a GitHub Issue labeled retro-action

**Coordinator integration:**
At round start, call Test-RetroOverdue (see skill retro-enforcement). If overdue, run this ceremony before the work queue.

**Why GitHub Issues, not markdown:**
Production data: 0% completion across 6 retros using markdown checklists, 100% after switching to GitHub Issues.

---

## Session Close Efficiency Review

| Field | Value |
|-------|-------|
| **Trigger** | auto |
| **When** | after |
| **Condition** | Any agent or factory work consumed AI credits or incurred external cost |
| **Facilitator** | Quark |
| **Participants** | work owners |
| **Time budget** | concise |
| **Enabled** | yes |

**Agenda:**
1. Record provider/model, AI credits or available usage units, duration, retries, tools and outcome.
2. Keep prompts, responses and personal data out of the usage ledger.
3. Identify failed/repeated work and one efficiency improvement.
4. Update the relevant FinOps or retrospective issue when action is required.

---

## Phase Gate Review

| Field | Value |
|-------|-------|
| **Trigger** | manual |
| **When** | before milestone exit |
| **Condition** | A phase exit-gate issue is ready for decision |
| **Facilitator** | Picard |
| **Participants** | artifact owners and Cyrus |
| **Time budget** | focused |
| **Enabled** | yes |

**Agenda:**
1. Review required evidence, unresolved risks, cost and user outcome.
2. Confirm dependencies and rollback/stop criteria.
3. Record dissent and specialist challenges.
4. Cyrus decides proceed, extend learning, de-scope or stop.
