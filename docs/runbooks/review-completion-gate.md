# Review-completion merge gate

Issue [#104](https://github.com/Jamula/Andreja/issues/104) owns this gate. Its
stable required-check context is **`Review completion gate`**. Keep auto-merge
disabled until the staged ruleset rollout below is proved complete.

## Policy

For every ready pull-request head, the trusted gate requires all of:

1. GitHub's `copilot-pull-request-reviewer` check completed successfully on the
   current head SHA.
2. A `copilot-pull-request-reviewer[bot]` review whose `commit_id` is that exact
   head SHA. A historical review or the author's approval is not evidence.
3. Zero unresolved review threads, including threads on outdated diffs.
4. Current-head independent evidence for every required domain.

Drafts and failed, cancelled, missing, stale, late, rate-limited, or unavailable
automation fail closed. A push creates a new head with no matching review
evidence, so the new gate starts pending. The gate polls a single metadata event
for at most 12 minutes; a late review event starts a fresh evaluation. After
resolving a thread without another GitHub event, comment exactly
`/review-gate` on the PR.

The gate reads domain requirements from the PR and its closing issues:

| Domain | Requirement labels |
| --- | --- |
| Architecture | `area:architecture`, `review:architecture-required` |
| Security | `area:security`, `review:security-required` |
| Privacy | `area:privacy`, `review:privacy-required` |
| Quality | `review:quality-required` |

The `review:*` labels are deliberate overrides for work whose area label is
broader or absent. Creating a label does not record completion.

## Recording independent evidence

An authorized operator dispatches **Record review evidence** from `main` with
the open PR number, exact current head SHA, domain, verdict, independent
reviewer, repository-local evidence URL, and bounded summary. The workflow
rejects a reviewer identity equal to the PR author.
The sole human may be the workflow actor recording a named Squad specialist's
artifact; that action is an auditable record, not an author approval or a claim
that a second human reviewed the PR.

The workflow creates a domain check on the supplied SHA and a 90-day binding
artifact. The gate accepts it only when the check's Actions run:

- is a successful `workflow_dispatch` of the exact trusted workflow on `main`;
- is in this repository;
- has an unexpired artifact binding domain, verdict, and head SHA; and
- matches the check's run ID, conclusion, and repository-local run URL.

The evidence URL is the durable human-readable artifact after the binding
artifact expires. Expired machine binding fails closed and must be re-recorded
against the still-current head.

## Break-glass

Break-glass is not a ruleset bypass and does not alter approval counts or other
required checks. An `admin` or `maintain` human may dispatch **Record review
break-glass** with:

- the exact current head SHA;
- the exact phrase `BREAK GLASS REVIEW GATE`;
- a specific emergency reason and residual risk; and
- a durable incident or decision URL in this repository.

The current-head audited check may replace unavailable Copilot/domain evidence.
It never permits a draft or unresolved thread, and a new push invalidates it.
Bots and lower-permission actors cannot record it.

## Trust boundary and availability

The privileged workflows use metadata-only permissions: repository content,
issues, PRs, Actions, and reviews are read; only Checks metadata and a targeted
Actions workflow dispatch are written.
Every action is SHA-pinned. They check out and execute automation from the
repository default branch with persisted credentials disabled. They never
check out or execute PR code. REST calls use `per_page: 100` with Octokit
pagination; review-thread and linked-issue GraphQL connections advance every
cursor and fail if pagination stalls.

`pull_request_target`, review, comment, issue-label, evidence-workflow, and
manual events converge PR checks. `merge_group` and pushes to `main` create the
same stable context with an explicit `not applicable` explanation because
review metadata was enforced on the component PR head. Concurrency cancels
superseded same-PR runs; every successful final update re-fetches the head and
draft state.

## Measured latency and cost

The incidents establish the current review latency baseline:

| PR | Ready | Copilot review complete | Latency | Merged before review by |
| --- | --- | --- | ---: | ---: |
| #100 | 04:41:58Z | 04:45:55Z | 237 s | 89 s |
| #101 | 04:37:33Z | 04:45:42Z | 489 s | 315 s |
| #105 | 05:21:51Z | 05:26:18Z | 267 s | 189 s |

Observed median latency is 267 seconds and maximum is 489 seconds. The
12-minute bound leaves 231 seconds above that maximum. The gate adds one short
Linux runner job per metadata event and, while review is pending, roughly
4–9 observed runner-minutes per ready head (12-minute maximum). It adds no new
AI review request: the existing Copilot ruleset owns that cost. Record actual
run duration after rollout before changing the bound.

Selective CI issue [#102](https://github.com/Jamula/Andreja/issues/102) may
later skip unrelated heavy jobs. It must keep this stable metadata gate on every
path and may not classify review as not applicable. This change implements no
selective-CI path classification.

## Staged ruleset rollout and rollback

The active repository ruleset at implementation start was
`Default-Ruleset` (`21199927`, updated `2026-08-24T21:39:50.526-07:00`). It had
no bypass actor, required strict up-to-date checks and thread resolution, and
required these five GitHub Actions contexts:

- `Build and test (Debug)`
- `Build and test (Release)`
- `Format verification`
- `NuGet vulnerability audit`
- `C# SAST (DevSkim)`

Do not edit the ruleset from this implementation branch. The safe rollout is:

1. Merge the trusted workflow code under the existing policy without enabling
   auto-merge.
2. On a separate canary PR, prove the exact `Review completion gate` context is
   created immediately, remains pending after all five existing checks pass,
   rejects drafts/unresolved threads/stale evidence, passes only after
   current-head review, and emits `not applicable` on `merge_group` and `main`.
3. Capture the complete live ruleset and verify it still matches the snapshot
   above. Stop if the live tip or policy changed.
4. Replace the ruleset once, adding only `Review completion gate` with GitHub
   Actions integration ID `15368`. Preserve every existing rule, context,
   strictness setting, thread-resolution requirement, zero-approval
   one-human model, and empty bypass list.
5. Re-read the ruleset and exercise a second canary. Never enable auto-merge as
   part of rollout.

Rollback is one full ruleset replacement restoring the five-context snapshot
above, not a partial edit. Use it only if the new context is proven
unavailable after rollout; preserve the failed canary run, before/after
ruleset JSON, actor, reason, and rollback timestamp in issue #104. Rollback
does not weaken any pre-existing check or thread requirement.
