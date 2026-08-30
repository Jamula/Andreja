# Weekly retrospective admission runbook

## Ownership and invariant

**Operational owner:** Jett Reno owns the admission mechanism and this runbook.
Ralph enforces the check before queue work, Picard facilitates the ceremony, and
the Squad coordinator requests governed state operations. Scribe alone completes
the canonical `log/` record through a single `squad_state_write` followed by
exact read-back under the single-coordinator process guard. The guard uses
best-effort repository
reconciliation; it is not distributed mutual exclusion or cross-process
exclusivity. GitHub issues remain the action source of truth.

Queue admission requires one valid completed retrospective no more than seven
elapsed days old. Each UTC Monday-through-Sunday cycle has at most one durable
record:

`log/weekly-retrospective-YYYY-MM-DD.md`

The date is the cycle's UTC Monday. The fixed key makes retries idempotent.
Legacy detailed `*-retrospective-with-enforcement.md` records remain readable
during rollout, but new ceremonies use only the canonical key.

## Universal queue check

Run this check before every queue enumeration, status, classification, or
admission path, including Ralph status, recovery, restart, heartbeat, and
post-batch rescan.

1. Confirm the runtime state bridge is healthy.
2. List `log` with `squad_state_list`; read every canonical completion record
   with `squad_state_read`. Preserve candidate legacy records for audit, but
   never accept them as completion.
3. Apply `.squad/skills/squad-issue-drain/weekly-retrospective.js` using the
   coordinator-provided current timestamp. Filesystem timestamps are not
   evidence. Current, completion, and both evidence-window endpoints must be
   timezone-qualified RFC 3339 values with no more than three fractional-second
   digits; malformed, timezone-less, over-precise, or reversed endpoints fail
   closed.
4. Fail closed on an overdue record, unavailable/incomplete state read,
   malformed canonical record, future completion, or multiple completed records
   in one cycle.
5. Reconfirm the check immediately before each queue admission.

The optional `retro-enforcement` skill is not part of this path. Its absence
does not disable or relax admission.

## Completion order

1. Establish and review the GitHub evidence window.
2. Record shipped and open counts and link blockers.
3. Record every required decision through governed decision tools.
4. Search all open and closed issues for each concrete action. Link an existing
   issue when it already owns the work. Create an issue labeled `retro-action`
   only for a genuinely new, non-duplicate action.
5. Confirm the record contains no personal data, prompts, connector content,
   credentials, or private diagnostics.
6. Run completion validation with state availability, complete enumeration,
   structured GitHub URL/count evidence, blocker references, governed decision
   references, open-and-closed duplicate-search results, action issue URLs, and
   privacy review evidence. GitHub evidence must be observed within five minutes
   of completion, and shipped/open identities must be disjoint even when issue
   and pull-request URL forms differ. Missing, malformed, stale, or inconsistent
   evidence fails closed; caller self-assertion is not evidence.
7. Prove that generic Scribe work is quiescent. If it cannot be proven finished,
   stop. Do not start generic Scribe until completion is reconciled.
8. Run the single-coordinator process guard and complete best-effort repository
   reconciliation of sessions, branches, worktrees, PRs, reservations/ledger,
   and issue readiness. Incomplete, stale, duplicate, or conflicting evidence
   blocks completion.
   - A repository artifact is issue-owned when it carries a positive `issue`.
     That ownership cannot be suppressed by another marker.
   - A session, branch, worktree, or PR without issue ownership is excluded only
     with the complete explicit shape `issue: null`, `writing: false`, and
     `ownership: non-issue|out-of-scope`. These records remain surfaced in
     `excludedRecords`; they are not silently discarded.
   - Every active worktree record also reports boolean `dirty`. Dirty
     issue-owned worktrees block. Dirty worktrees explicitly proven non-writing
     and non-issue or out-of-scope remain visible but do not block. Missing or
     contradictory ownership or dirty-state evidence fails closed.
   - A PR body reference such as `Refs #3` is evidence of relationship, not
     issue ownership. A verified closing issue link is ownership. Duplicate
     positive issue ownership across active sessions, branches, worktrees, and
     PRs blocks completion.
9. Hand the returned key and content to dedicated Scribe. After a complete
   listing proves the key absent, Scribe makes one `squad_state_write`. The
   orchestrator never writes `log/` directly.
10. Re-list and re-read the record. Exact valid content confirms completion. An
    exact valid existing record is reused idempotently without another write.
    Missing or different content is a conflict and remains blocked.

The completion record contains only the evidence window, shipped/open counts,
blocker references, decision summaries/references, and action issue links. Do
not write an additional ceremony or session summary log for the same cycle.
Canonical section entries use these exact shapes:

- **Blockers:** `- #123`, `- owner/repository#123`, or a canonical GitHub issue
  or pull-request URL; use only `- No blockers.` when none exist.
- **Decisions:** `- <summary> — Reference: <durable reference>`; use only
  `- No new decision required.` when none are required.
- **Retro actions:** `- created|existing: <summary> — https://github.com/<owner>/<repository>/issues/<number>`;
  use only `- No actions after complete duplicate search.` when the completed
  search found no actions.

Arbitrary bullets, malformed links, empty sections, or a no-item sentinel mixed
with other entries invalidate the record and keep admission closed.

## Recovery

- **State bridge or enumeration unavailable:** keep admission blocked. Restore
  the configured runtime state bridge, then rerun the round-start check.
- **Optional enforcement component unavailable:** continue with the built-in
  issue-drain protocol; do not bypass the ceremony.
- **Ceremony interrupted before the final write:** rerun it. The absence of a
  valid record intentionally keeps admission blocked.
- **Interruption after the write:** re-read and reuse the valid canonical
  record. Never create a second log.
- **Existing malformed canonical key or duplicate cycle records:** stop
  admission and escalate to the Squad coordinator. Preserve the records for
  audit; any correction or tombstone must use the runtime state backend and its
  approved recovery policy.
- **GitHub evidence or duplicate search incomplete:** keep the ceremony open and
  admission blocked. Do not infer completion or create speculative actions.
- **Atomic CAS, lease, or conditional-create unavailable:** continue under the
  single-coordinator process guard. Capability absence alone does not block
  completion. This is best-effort repository reconciliation, not a cross-process
  safety claim.
- **Guard conflict or incomplete reconciliation:** keep admission blocked.
  Preserve state, re-enumerate every source, and escalate persistent conflicts.
- **Uncertain write outcome:** re-list and re-read. Treat exact valid content as
  completed; treat missing or different content as blocked. Never overwrite.

Never write, edit, delete, or copy runtime-owned Squad state directly, and never
use git-notes choreography. Recovery uses `squad_state_*` or governed memory
tools only.
