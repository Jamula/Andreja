# Weekly retrospective admission runbook

## Ownership and invariant

**Operational owner:** Jett Reno owns the admission mechanism and this runbook.
Ralph enforces the check before queue work, Picard facilitates the ceremony, and
the Squad coordinator performs governed state operations. GitHub issues remain
the action source of truth.

Queue admission requires one valid completed retrospective no more than seven
elapsed days old. Each UTC Monday-through-Sunday cycle has at most one durable
record:

`log/weekly-retrospective-YYYY-MM-DD.md`

The date is the cycle's UTC Monday. The fixed key makes retries idempotent.
Legacy detailed `*-retrospective-with-enforcement.md` records remain readable
during rollout, but new ceremonies use only the canonical key.

## Round-start check

1. Confirm the runtime state bridge is healthy.
2. List `log` with `squad_state_list`; read canonical and candidate legacy
   completion records with `squad_state_read`.
3. Apply `.squad/skills/squad-issue-drain/weekly-retrospective.js` using the
   coordinator-provided current timestamp. Filesystem timestamps are not
   evidence.
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
6. Run the completion validation. Only after every gate passes, persist the
   returned key and content with exactly one `squad_state_write`.
7. Re-list and re-read the record, then resume queue work.

The completion record contains only the evidence window, shipped/open counts,
blocker references, decision summaries/references, and action issue links. Do
not write an additional ceremony or session summary log for the same cycle.

## Recovery

- **State bridge or enumeration unavailable:** keep admission blocked. Restore
  the configured runtime state bridge, then rerun the round-start check.
- **Optional enforcement component unavailable:** continue with the built-in
  issue-drain protocol; do not bypass the ceremony.
- **Ceremony interrupted before the final write:** rerun it. The absence of a
  valid record intentionally keeps admission blocked.
- **Interruption after the final write:** re-read and reuse the valid canonical
  record. Never create a second log.
- **Existing malformed canonical key or duplicate cycle records:** stop
  admission and escalate to the Squad coordinator. Preserve the records for
  audit; any correction or tombstone must use the runtime state backend and its
  approved recovery policy.
- **GitHub evidence or duplicate search incomplete:** keep the ceremony open and
  admission blocked. Do not infer completion or create speculative actions.

Never write, edit, delete, or copy runtime-owned Squad state directly, and never
use git-notes choreography. Recovery uses `squad_state_*` or governed memory
tools only.
