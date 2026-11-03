# Issue status and blockers

GitHub issues use one `status:*` label for implementation and pull-request
lifecycle:

| Label | Meaning |
|---|---|
| `status:backlog` | Open; no tracked branch or pull request |
| `status:branch-only` | Implementation branch or closed unmerged PR exists |
| `status:pr-draft` | A linked pull request is draft |
| `status:ready` | A linked pull request is ready for review |
| `status:merged` | A linked pull request merged to the default branch |
| `status:closed` | Closed without a merge to the default branch |

`status:ready` does not mean approved, checks-passing, or mergeable. Read the
pull request's required checks and reviews before merging. A stack layer merged
only into another feature branch remains `status:branch-only` until its work
reaches the default branch.

## Dependencies and evidence

Use GitHub's **Blocked by** relationship. For an unresolved ordinary dependency,
the issue owner adds `blocked:dependency` to the dependent issue. Add
`blocks:evidence` to a blocker that exists to produce external or milestone-exit
proof and `blocked:evidence` to its dependents. Add `blocks:human` to a blocker
that requires a recorded human decision or approval and `blocked:human` to its
dependents.

These labels never replace `status:*`. An issue can correctly be both
`status:merged` and `blocked:evidence`.

## Manual maintenance

Issue owners maintain lifecycle and blocker labels manually. There is no
scheduled reconciler, branch-name parser, automatic label synchronization, or
workflow-dispatch override.

Use the issue's Development links and explicit closing references to identify
related pull requests. Confirm the actual branch, draft, review, and merge state
before changing a label; label text alone is not evidence. Keep exactly one
`status:*` label and remove resolved blocker labels after checking the linked
dependency's outcome.

For stacks, verify that the implementation reached the default branch before
using `status:merged`. Reopening an issue requires a fresh assessment of its
remaining outcome; a historical merge does not prove that new work is complete.
