### 2026-10-04T19-36-20: Branch cleanup scope and archival safety gates
**By:** Squad (Coordinator)
**What:** Branch cleanup scope and archival safety gates
**References:** PR #159, PR #176, PR #177, PR #178, PR #179
**Why:** ### Decision
**By:** Squad (Coordinator), following the Retrospective facilitated by Picard
**What:** Before deleting or detaching branches, refresh remote refs and verify each candidate's tip, unique commits, upstream, live PR state, and worktree status. The user-approved worktree list is a strict allowlist; stop if observed state differs. Preserve branches with active PRs or necessary unique work. For decision archival, require a tracked destination, append and verify the destination before removing source entries; if no tracked destination exists, retain the source and report the blocker.
**Why:** The retrospective identified stale branch classification and a worktree detachment outside the approved list. The archival pass exceeded its threshold but had no tracked destination, so no entries were safely archived.