# Meet the Squad

Andreja uses a repository-backed [`.squad/`](.squad) multi-agent team
coordinated through [`.github/agents/squad.agent.md`](.github/agents/squad.agent.md),
[`.squad/team.md`](.squad/team.md), and [`.squad/routing.md`](.squad/routing.md)
under human authority from repository owner Cyrus Jamula.

## Coordinator

| Identity | Role | Responsibility |
| --- | --- | --- |
| `Squad` | Coordinator | Triages work, assigns one accountable specialist per task in an isolated worktree, enforces independent reviewer lockouts ([`.github/skills/reviewer-protocol/SKILL.md`](.github/skills/reviewer-protocol/SKILL.md)), and escalates human-gated spending, legal, and release decisions to Cyrus Jamula. |

## Active domain specialists

These specialists own implementation, review, and verification domains and are
the only identities eligible for `squad:*` GitHub issue routing labels:

| Identity | Label | Domain | Charter |
| --- | --- | --- | --- |
| `Lead` | `squad:lead` | Architecture, roadmap slicing, ADR alignment, and cross-cutting code review. | [`.squad/agents/lead/charter.md`](.squad/agents/lead/charter.md) |
| `Backend` | `squad:backend` | `.NET 10` modules, typed HTTP contracts, PostgreSQL/EF Core persistence, and portability CLI. | [`.squad/agents/backend/charter.md`](.squad/agents/backend/charter.md) |
| `Frontend` | `squad:frontend` | Responsive Blazor UI, account/passkey surfaces, keyboard navigation, and viewport accessibility. | [`.squad/agents/frontend/charter.md`](.squad/agents/frontend/charter.md) |
| `Tester` | `squad:tester` | `MSTest.Sdk` / Microsoft Testing Platform suites, conformance fixtures, and parity gates. | [`.squad/agents/tester/charter.md`](.squad/agents/tester/charter.md) |
| `Security` | `squad:security` | Passkey identity, composite tenant isolation, privacy/BYOK boundaries, OCI supply chain, and governance claim accuracy. | [`.squad/agents/security/charter.md`](.squad/agents/security/charter.md) |

## Built-in support identities

These always-on support identities assist coordination, durable memory, and
independent review. Per [`.squad/routing.md`](.squad/routing.md) and
[`docs/help/issue-status.md`](docs/help/issue-status.md), they are **not**
issue-routing destinations and must never be assigned `squad:*` issue labels:

| Identity | Role | Charter |
| --- | --- | --- |
| `Scribe` | Durable decision and session-history curator (enforces secret-free `.squad/` writes). | [`.squad/agents/scribe/charter.md`](.squad/agents/scribe/charter.md) |
| `Ralph` | Backlog and active work-queue monitor. | [`.squad/agents/ralph/charter.md`](.squad/agents/ralph/charter.md) |
| `Rai` | Responsible-AI, fairness, and accessibility reviewer. | [`.squad/agents/Rai/charter.md`](.squad/agents/Rai/charter.md) |
| `Fact Checker` | Evidence verification and devil's-advocate reviewer against unsubstantiated claims. | [`.squad/agents/fact-checker/charter.md`](.squad/agents/fact-checker/charter.md) |

## Proposed roster rebuild (not yet ratified)

[`docs/operations/squad-rebuild-prompt.md`](docs/operations/squad-rebuild-prompt.md)
contains a proposed Star Trek crew rebuild brief (introducing `Seven` as Chief
of Staff alongside named Star Trek domain specialists while preserving `Scribe`,
`Ralph`, `Rai`, and `Fact Checker` unchanged). That brief remains **pending
interactive Phase 2 ratification and explicit implementation approval** from
Cyrus Jamula; until ratified and applied to [`.squad/team.md`](.squad/team.md),
the active roster above is authoritative.
