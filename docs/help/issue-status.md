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

Use GitHub's **Blocked by** relationship. An unresolved ordinary dependency adds
`blocked:dependency` to the dependent issue. Add `blocks:evidence` to a blocker
that exists to produce external or milestone-exit proof; dependents then show
`blocked:evidence`. Add `blocks:human` to a blocker that requires a recorded
human decision or approval; dependents then show `blocked:human`.

These labels never replace `status:*`. An issue can correctly be both
`status:merged` and `blocked:evidence`.

## Automatic and manual repair

The Issue Status workflow reacts to issue lifecycle, configured branch names,
and trusted same-repository pull requests that close an issue. Tracked branch
forms are `squad/{issue-number}-{slug}`,
`copilot/{issue-number}-{slug}`, and
`u/{account}/{issue-number}-{slug}`; arbitrary number-leading names are ignored.
Branch existence is queried from GitHub and never inferred from a lifecycle
label. Pull requests connected through the issue's Development sidebar are
queried from GitHub's `closedByPullRequestsReferences`, so they drive draft,
ready, and merged state even without a closing keyword in the PR body. Open fork
PRs remain excluded from the write-capable status path.

GitHub Actions does not expose native issue-dependency changes as a workflow
trigger. A trusted hourly full reconciliation repairs dependency labels, PR-body
reference edits, deleted branches, and stack promotions. For a stacked change,
the reconciler follows a merged layer's base branch into the PR whose head
promotes that branch. Repeating the closing reference on the final PR remains
recommended because it gives GitHub and reviewers an explicit closure record.
The workflow is idempotent and does not post comments.

From **Actions > Issue Status > Run workflow**, leave the issue number blank to
reconcile all issues, or enter one issue number. `auto` derives lifecycle and
blockers. A selected lifecycle or blocker value is a one-run override; the next
relevant event derives state again. Adding a recognized `status:*` label directly
is also a one-event manual override. Unknown `status:*` labels are removed.
