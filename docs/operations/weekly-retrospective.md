# Weekly retrospective admission runbook

## Ownership and invariant

**Operational owner:** Jett Reno owns the admission mechanism and this runbook.
Ralph enforces the check before queue work, Picard facilitates the ceremony, and
the Squad coordinator requests governed state operations. Scribe alone completes the canonical `log/` record through a verified,
repository-scoped atomic conditional create supplied by the existing runtime.
Plain state writes do not establish exclusivity. GitHub issues remain the
action source of truth.

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
8. Capability-detect an existing verified repository-scoped atomic
   conditional-create operation. Without it, remain read-only and blocked; do
   not substitute `squad_state_write`.
9. Hand the returned key and content to exclusive Scribe. Scribe makes one
   create-if-absent attempt. Conflict or uncertainty is failure, not overwrite
   or success. The orchestrator never writes `log/` directly.
10. Re-list and re-read the record, then resume queue work.

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
- **Ceremony interrupted before the final conditional create:** rerun it. The absence of a
  valid record intentionally keeps admission blocked.
- **Interruption after the conditional create:** re-read and reuse the valid canonical
  record. Never create a second log.
- **Existing malformed canonical key or duplicate cycle records:** stop
  admission and escalate to the Squad coordinator. Preserve the records for
  audit; any correction or tombstone must use the runtime state backend and its
  approved recovery policy.
- **GitHub evidence or duplicate search incomplete:** keep the ceremony open and
  admission blocked. Do not infer completion or create speculative actions.
- **Atomic conditional-create unavailable:** remain read-only. Do not claim a
  lease, write completion, admit children, or replace atomicity with convention.

Never write, edit, delete, or copy runtime-owned Squad state directly, and never
use git-notes choreography. Recovery uses `squad_state_*` or governed memory
tools only.
