# Work Routing

How to decide who handles what.

## Routing Table

| Work Type | Route To | Examples |
|-----------|----------|----------|
| Executive strategy and priorities | Picard | Mission, portfolio, launch gates, company decisions |
| Architecture and ADRs | Spock | Boundaries, portability, scale, federation coherence |
| Security and identity | Tuvok | Threats, auth, authorization, cryptography, abuse |
| Privacy and consent | Deanna Troi | Data minimization, inference, sharing, non-user subjects |
| Platform, channels and operations | Jett Reno | Hosting, OpenTofu, OTel, CI/CD, channel topology |
| Quality and release evidence | Data | Tests, performance, accessibility, conformance, release gates |
| FinOps and sustainability | Quark | Burn, budgets, AI usage, unit economics, sponsorship |
| Domain and application | T'Pol | .NET use cases, APIs, persistence, tenant data boundaries |
| Web and public experience | Jadzia Dax | Blazor, accessibility, public/help, control planes |
| Assistant, skills and semantic graph | Seven of Nine | Providers, tools, manifests, ontology, federation |
| Feedback and support | Guinan | Intake, triage, user status, verification, help gaps |
| Legal and regulatory | Sarek | IP, contracts, federal/state applicability, counsel questions |
| Marketing and community | Neelix | Positioning, Personal Brand, community, partnerships |
| Mobile | Hoshi Sato | iOS/Android, offline, device security, app stores |
| Health and wellbeing | Beverly Crusher | Health artifacts, safety, medications/labs/imaging boundaries |
| Code review | Reviewer selected by Coordinator | Independent specialist review; rejected author is locked out |
| Session logging | Scribe | Automatic memory, decisions and session logs |
| Work monitoring | Ralph | Work queue, blocked/stale issues and keep-alive |
| RAI review | Rai | AI safety, bias, harmful content and credential awareness |
| Claim verification | Fact Checker | Source verification and Devil's Advocate review |
| Session logging | Scribe | Automatic — never needs routing |
| RAI review | Rai | Content safety, bias checks, credential detection, ethical review |

## Issue Routing

| Label | Action | Who |
|-------|--------|-----|
| `squad` | Triage: analyze issue, assign `squad:{member}` label | Picard |
| `squad:{name}` | Pick up issue and complete the work | Named member |

### How Issue Assignment Works

1. When a GitHub issue gets the `squad` label, **Picard** triages it, assigns the right `squad:{member}` label, and comments with rationale.
2. When a `squad:{member}` label is applied, that member picks up the issue in their next session.
3. Members can reassign by removing their label and adding another member's label.
4. The `squad` label is the "inbox" — untriaged issues waiting for Lead review.

## Rules

1. **Eager by default** — spawn all agents who could usefully start work, including anticipatory downstream work.
2. **Scribe always runs** after substantial work, always as `mode: "background"`. Never blocks.
3. **Quick facts → coordinator answers directly.** Don't spawn an agent for "what port does the server run on?"
4. **When two agents could handle it**, pick the one whose domain is the primary concern.
5. **"Team, ..." → fan-out.** Spawn all relevant agents in parallel as `mode: "background"`.
6. **Anticipate downstream work.** If a feature is being built, spawn the tester to write test cases from requirements simultaneously.
7. **Issue-labeled work** — when a `squad:{member}` label is applied to an issue, route to that member. The Lead handles all `squad` (base label) triage.
8. **Worktrees by default for parallel work** — each independent issue gets one branch, worktree, owner and draft PR.
9. **Main is the integration branch** — issue PRs target `main`; use native protections/merge queue when entitled and documented procedural fallback otherwise.
10. **Dependent slices may stack** — create layers bottom-to-top and use GitHub native stack metadata when available; ordinary dependent PRs are the fallback.
11. **Artifact gates, not votes** — specialists produce/challenge the required ADR, threat/privacy, cost, test or runbook evidence; Cyrus approves.
12. **Critical path protection** — Phase 0 decisions and Phase 1A self-host MVP outrank later channels, skills and marketplace work.
