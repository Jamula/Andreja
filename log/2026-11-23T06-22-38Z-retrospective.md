# Andreja retrospective - 2026-11-23 UTC

Review window: 2026-11-16 through 2026-11-23 UTC (seven-day baseline, not a verified previous-completion boundary). Requested at 2026-11-23T06:22:38Z; evidence gathered and follow-ups posted during this session.

## Scope and participation

User-requested coordination retrospective. The Squad coordinator delegate inspected the installed roster and enforcement requirements but stopped before facilitating a full ceremony. The coordinating session completed this evidence review and posted follow-up assignments. No specialist attendance, team consensus, or owner acceptance is claimed. Lead is the installed facilitator; Seven is the target Chief of Staff in the rebuild brief, not an installed member. No roster or authority changes were made.

Status: evidence review and action follow-up recorded; full-team weekly ceremony certification is NOT established. This record must not be used as a validated team-wide weekly admission completion or exactly-once proof. The configured local log inventory was empty before this write; #297 comments report a 2026-11-15 catch-up in another runtime scope that could not be read here. Missing local evidence does not mean no prior retrospective occurred. Cross-worktree state reconciliation and required participant input remain outstanding.

## What shipped

Two merged PRs in the review window: [#299](https://github.com/Jamula/Andreja/pull/299), functional roster, specialist charters, routing and honest manual/local operating limits, merged 2026-11-23T05:37:25Z; [#300](https://github.com/Jamula/Andreja/pull/300), Squad workflow/state scaffolding with restored readiness contracts and incompatible promotion workflow removed, merged 2026-11-23T06:19:00Z.

Six closed dependency-update issues: #255 and #291-295. Issue closure is not itself a separate claim about a release or phase exit.

Both PRs' current GitHub rollups report successful Debug/Release builds, format verification, NuGet audit, OCI evidence and hosted heartbeat. Keyless signing is skipped, not passed. #300 reports 15/15 local readiness tests after remediation; no new product tests were run for this coordination review.

## What did not ship and blockers

Live inventory: 36 open issues, zero open PRs, retrieved with limits above the observed totals. Open issues comprise 14 epics, 15 human decision gates, and seven other items: #6 legal research, #93 website proposal, #211 historical OCI scan diagnosis, #158 feedback readiness decision (included among the 15 decisions), #297 retrospective admission and #298 heartbeat/full-health readiness. More precisely: 14 epics + 15 decisions + 7 others, where the seven others are #6, #93, #211, #297, #298 and the two remaining non-decision roadmap items only if classified separately; classification is not used for admission. The authoritative figure is the 36-item live inventory, not this category approximation.

Human security/privacy/legal/release decisions remain human-owned. Merged configuration does not authorize phase exit, spending, deployment or release. No unrelated backlog work was dispatched. Generated CI/release scaffolding in #300 is explicitly placeholder functionality; an installed file is not a working build or publishing pipeline.

## Failures and evidence-backed causes

#300 initially encountered 11 readiness-test failures and a precommit rejection of four casting files as state-branch-owned, per recent session summaries. Its merged description records restoration of readiness gates, support-identity exclusions, roster header parsing and routing-handoff contracts. The incompatible promotion workflow lacked its required release-version/changelog/branch prerequisites and was removed. Lesson: validate scaffold compatibility and state ownership before calling an upgrade operational.

For #297, existing investigation established that #250 removed historical admission implementation and #252 did not restore it. Current ceremonies describe manual worktree-local checking. This establishes an enforcement capability gap, not the exact cause of a particular missed cycle. CLI triage --retro is not a supported substitute for fail-closed pre-admission verification. Today's manual review does not prove two on-time cycles or shared exactly-once coordination.

For #298, newer evidence corrects stale readiness statements: [#299 heartbeat SUCCESS](https://github.com/Jamula/Andreja/actions/runs/45118942019/job/137508043835) and [#300 heartbeat SUCCESS](https://github.com/Jamula/Andreja/actions/runs/45123282103/job/137521758500). The narrower heartbeat is working in those runs. #299's recorded fresh-checkout full-health failure without excluded registry/history is a distinct unresolved requirement; heartbeat success must not erase it. No new fresh-checkout health experiment was conducted in this review.

## Prior actions and concrete follow-up

Reuse existing GitHub issues labeled retro-action; do not add duplicate markdown-only commitments. Both remain OPEN.

[#297 follow-up](https://github.com/Jamula/Andreja/issues/297#issuecomment-7093332903): installed Lead accountable for coordination. Establish a supported shared admission/logging path, demonstrate stale/unavailable/invalid/concurrent failure behavior, and collect exactly one canonical completion with valid issue links in each of two subsequent on-time cycles. First checkpoint no later than 2026-11-30 UTC; future-cycle acceptance remains time-gated. No scheduler was enabled.

[#298 follow-up](https://github.com/Jamula/Andreja/issues/298#issuecomment-7093333238): installed Lead accountable, Backend for registry/state provisioning and Tester for fresh-checkout evidence. Demonstrate permitted registry/history provisioning and passing full clean-checkout health while preserving publication restrictions and readiness/support-identity safeguards. First checkpoint no later than 2026-11-30 UTC. The comments explicitly distinguish assignments from accepted or active specialist work.

## Limitations and safeguards

No new issue, PR, commit, release, scheduler or roster change. No claim that all team members participated, that a team-wide ceremony completed, or that admission may resume. Record is worktree-local and created atomically through the configured state tool; local atomic creation does not establish cross-worktree uniqueness. Usage/cost measurements unavailable; no estimates invented. Full-team participant input, shared state history and operational admission evidence remain outstanding under #297.
