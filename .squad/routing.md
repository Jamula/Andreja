# Work Routing

How to decide who handles what.

## Routing Table

| Work Type | Route To | Examples |
|-----------|----------|----------|
| Architecture, roadmap, delivery coordination | Lead | Cross-cutting design, milestone planning, dependency ordering |
| .NET domain, APIs, persistence | Backend | Handlers, EF Core, schemas, migrations |
| Blazor and user experience | Frontend | Pages, components, accessibility, responsive behavior |
| Testing and quality | Tester | Test plans, regression, integration and failure-path evidence |
| Security and privacy controls | Security | Identity, threat controls, data handling, security findings |

Routes refer to the approved specialist roster. The casting registry remains
local and is not included in the repository; a fresh checkout cannot yet
validate these routes with `squad health`. Scribe, Ralph, Rai, and Fact Checker
are built-in support identities, not issue-routing destinations.

## Issue Routing

| Label | Action | Who |
|-------|--------|-----|
| `squad` | Deterministic triage adds a `squad:{member}` routing label | Triage workflow |
| `squad:{name}` | Routing hint for a named member | Coordinator |

### How Issue Assignment Works

1. When a GitHub issue gets the `squad` label, the workflow uses deterministic keyword routing and comments on the result; this is not Lead-agent analysis.
2. A `squad:{member}` label is a routing hint, not a running session or GitHub assignment. Start an authorized session to begin work.
3. Members can reassign by removing their label and adding another member's label.
4. The `squad` label is the inbox for automated triage; ambiguous routing falls
   back to the configured Lead.

## Rules

1. **Eager by default** — spawn all agents who could usefully start work, including anticipatory downstream work.
2. **Scribe runs** in the background when accepted durable decisions need merging.
3. **Quick facts → coordinator answers directly.** Don't spawn an agent for "what port does the server run on?"
4. **When two agents could handle it**, pick the one whose domain is the primary concern.
5. **"Team, ..." → fan-out.** Spawn all relevant agents in parallel as `mode: "background"`.
6. **Anticipate downstream work.** If a feature is being built, spawn the tester to write test cases from requirements simultaneously.
7. **Issue-labeled work** — when a `squad:{member}` label is applied to an issue, route to that member. The Lead handles all `squad` (base label) triage.
