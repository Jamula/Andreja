# Review-completion merge gate

Issue [#104](https://github.com/Jamula/Andreja/issues/104) owns this gate.
Auto-merge must remain disabled until the exact-workflow rollout below passes
both canaries.

## Enforcement identity

The merge gate is the GitHub ruleset **required workflow**, not a status-check
name. The rule must identify all of:

- repository ID `1342901808` (`Jamula/Andreja`);
- `.github/workflows/review-completion.yml`; and
- the exact trusted `main` SHA containing the reviewed workflow revision.

The workflow's required job is `Required trusted review policy`. It does not
create or update a Checks API context. A pull-request workflow therefore cannot
gain the required workflow's identity by copying a job or check name.

The active ruleset is intentionally unchanged while this PR is open. A required
workflow cannot safely reference this revision until it is on `main`, and the
metadata-event behavior described under [Rollout blocker](#rollout-blocker)
requires a live canary. Draft state and disabled auto-merge are the fail-closed
hold until then.

## Policy

For every ready pull-request diff, the trusted evaluator requires:

1. the newest review from `copilot-pull-request-reviewer[bot]` to be attached
   to the exact current head SHA and not dismissed;
2. zero unresolved review threads, including outdated-diff threads;
3. authenticated independent evidence for each domain selected by PR or closing
   issue labels; and
4. two identical, complete policy snapshots separated by a stabilization delay.

The reviewed diff identity is:

`PR number + head SHA + base repository ID/name + base branch + base SHA`.

A change to any component invalidates Copilot, independent-review, and
break-glass evidence. The evaluator re-fetches PR state, the reviewed base,
closing issues and labels, all reviews, all review threads, reviewer
authorization, and break-glass state for the second snapshot. Drafts, closed
PRs, unresolved threads, stale evidence, failed review attempts, timeouts,
403/429 responses, and unavailable APIs fail closed.

The newest Copilot review is considered first; the evaluator never falls back
to an older current-head review. Likewise, for each independent domain, only
the newest review containing that domain's evidence marker is considered.
Malformed, stale, unauthorized, incomplete, dismissed, or rejected newest
evidence blocks the PR rather than exposing an older approval.

## Independent review evidence

The former workflow-dispatch recorder and generic GitHub Actions checks were
removed. They allowed an input string to impersonate a reviewer and required a
spoofable Actions check identity.

Evidence is now a native GitHub pull-request review. GitHub supplies the
authenticated `review.user.login`, review state, PR, and reviewed commit. The
reviewer must:

- be a human with current `write`, `maintain`, or `admin` permission;
- differ from the PR author; and
- submit `APPROVED` for acceptance or `CHANGES_REQUESTED` for rejection.

The review body contains exactly one marker for its domain:

```text
<!-- andreja-review-evidence:v2:architecture {"schemaVersion":2,"kind":"independent-review","domain":"architecture","pullNumber":115,"headSha":"<40 hex>","baseRepositoryId":1342901808,"baseRepository":"Jamula/Andreja","baseRef":"main","baseSha":"<40 hex>","evidenceUrl":"https://github.com/Jamula/Andreja/pull/115#pullrequestreview-...","summary":"Bounded finding and disposition summary."} -->
```

Valid domains are `architecture`, `security`, `privacy`, and `quality`. The
marker is durable review content, and the evaluator validates its content
against the authenticated review and live diff. A copied review body cannot be
replayed on another PR sharing the same head SHA or after the base changes.

The requirement labels are:

| Domain | Requirement labels |
| --- | --- |
| Architecture | `area:architecture`, `review:architecture-required` |
| Security | `area:security`, `review:security-required` |
| Privacy | `area:privacy`, `review:privacy-required` |
| Quality | `review:quality-required` |

Creating a label does not record completion. The one-human model remains
possible when automation authored the PR and the human submits the review. A
human PR author cannot turn self-review into independent evidence. If no second
authorized identity exists, the normal independent path remains blocked; only
the separately audited emergency path below can substitute.

## Break-glass

Break-glass is not a bypass actor, approval-count change, or generic success
check. An authenticated `maintain` or `admin` human dispatches **Record review
break-glass** from `main` with the complete diff identity, exact confirmation
`BREAK GLASS REVIEW GATE`, a bounded reason, and a durable repository-local
incident/decision URL.

The run name and artifact name include the PR, head, base repository ID, and
base SHA. The JSON artifact additionally binds the base repository name/branch,
actor, reason, incident URL, and workflow run ID. The evaluator:

1. selects only the newest run whose trusted run name matches the live diff;
2. rejects pending, failed, wrong-path, wrong-repository, wrong-branch, bot, or
   unauthorized runs without falling back;
3. downloads the exact unexpired artifact;
4. validates ZIP structure, size, CRC, JSON, every diff field, actor, and run ID.

Break-glass may replace unavailable Copilot/domain evidence. It never permits a
draft or unresolved thread. A push or base change invalidates it.

## Race and failure behavior

GitHub creates the required workflow run in queued/in-progress state before the
evaluator's first API call. Therefore an initial 403/429 cannot leave an older
generic success as the gate result. The workflow uses per-PR
`cancel-in-progress` concurrency. It never writes a shared check, so an older
cancelled run has no API surface with which to overwrite a newer generation.

Approval requires two identical full fingerprints. Any security-relevant change
between snapshots restarts evaluation. PR labels, reviews, review edits or
dismissals, review-thread resolution, review comments, draft state, pushes, and
base changes have workflow event coverage. Missing/late reviewer automation
keeps the job running up to 12 minutes and then fails it.

All REST lists use Octokit pagination. Review threads and linked closing issues
advance GraphQL cursors and reject a non-advancing cursor. Privileged workflows
use metadata-only permissions, SHA-pinned actions, disabled checkout
credentials, and trusted workflow/default-branch source. They never execute PR
code or receive repository secrets.

## Measured latency and cost

| PR | Ready | Copilot review complete | Latency | Merged before review by |
| --- | --- | --- | ---: | ---: |
| #100 | 04:41:58Z | 04:45:55Z | 237 s | 89 s |
| #101 | 04:37:33Z | 04:45:42Z | 489 s | 315 s |
| #105 | 05:21:51Z | 05:26:18Z | 267 s | 189 s |

Observed median latency is 267 seconds and maximum is 489 seconds. The
12-minute bound leaves 231 seconds above that maximum. The gate adds no AI
request; it waits for the existing Copilot ruleset review. Record actual
required-workflow duration after rollout before changing the bound.

Issue [#102](https://github.com/Jamula/Andreja/issues/102) may later avoid
unrelated heavy CI. It must not skip or path-filter this review policy. This
change does not implement selective CI.

## Rollout blocker

GitHub documents that ruleset workflows natively trigger only the default
`pull_request`, `pull_request_target`, and `merge_group` activity types; event
filters are ignored by the ruleset runner. Ordinary runs of the same trusted
workflow also listen for label, review, dismissal, thread, and comment events.
Linked issue changes can request a default-branch `workflow_dispatch`.

Before enforcement, a live canary must prove that GitHub associates those later
ordinary/dispatch executions with the same PR and that a newer queued, failed,
or cancelled execution supersedes a prior successful required-workflow run.
This is not established by local mocks or by matching job names. In particular,
the canary must prove linked-issue label changes and `pull_request_review`
rejections close a same-head merge-ready state immediately.

If any metadata event leaves the prior success usable, **do not add the
workflow rule**. Keep auto-merge disabled and the PR draft/manual hold in place.
The safe follow-up is a dedicated GitHub App identity/event bridge unavailable
to PR workflows, or a redesign using only GitHub-managed PR-scoped state. A
generic GitHub Actions required status context is not an acceptable fallback.

## Staged rollout

At revision start, active `Default-Ruleset` `21199927` had no bypass actors,
zero required approvals, thread resolution, strict status checks, and these
five GitHub Actions contexts:

1. `Build and test (Debug)`
2. `Build and test (Release)`
3. `Format verification`
4. `NuGet vulnerability audit`
5. `C# SAST (DevSkim)`

After this revision merges manually under that unchanged policy:

1. Capture the live `main` SHA, full ruleset JSON, and `updated_at`. Stop if
   either differs from the reviewed baseline.
2. Open two canary PRs that intentionally share one head SHA but have different
   PR/base identities.
3. Exercise draft/ready, all-five-CI-before-review, Copilot current-head,
   approval then newer rejection, malformed/newer evidence, PR and linked-issue
   labels, thread unresolved/resolved, base advance, push, concurrent event
   cancellation, manual rerun, and simulated/unavailable API paths. At every
   transition inspect the ruleset result, not only job names.
4. Keep auto-merge disabled. Record run URLs and exact expected/actual states in
   issue #104.
5. Only if every transition passes, run the tool's plan mode, inspect the one
   added `workflows` rule, then use apply mode with the exact live guards and
   canary evidence URL:

```powershell
node .github\scripts\review-gate-ruleset.js plan-rollout `
  --repo Jamula/Andreja `
  --ruleset-id 21199927 `
  --repository-id 1342901808 `
  --expected-main-sha <live-main-sha>

node .github\scripts\review-gate-ruleset.js apply-rollout `
  --repo Jamula/Andreja `
  --ruleset-id 21199927 `
  --repository-id 1342901808 `
  --expected-main-sha <live-main-sha> `
  --expected-updated-at <exact-ruleset-updated-at> `
  --evidence-url https://github.com/Jamula/Andreja/issues/104#issuecomment-... `
  --confirm "APPLY EXACT REQUIRED WORKFLOW"
```

The tool verifies the full baseline, live `main`, live ruleset timestamp, empty
bypass list, five exact existing checks, zero approvals, and thread resolution.
It performs one full ruleset replacement, adds only the exact SHA-pinned
`workflows` rule, re-reads the ruleset, and fails if the complete result differs.

## Rollback

Rollback is one full replacement that removes only the exact reviewed workflow
rule and retains every baseline rule/context. It requires the same live guards,
an audit URL, and a different exact confirmation:

```powershell
node .github\scripts\review-gate-ruleset.js apply-rollback `
  --repo Jamula/Andreja `
  --ruleset-id 21199927 `
  --repository-id 1342901808 `
  --workflow-sha <workflow-sha-used-at-rollout> `
  --expected-main-sha <current-live-main-sha> `
  --expected-updated-at <exact-ruleset-updated-at> `
  --evidence-url https://github.com/Jamula/Andreja/issues/104#issuecomment-... `
  --confirm "ROLLBACK EXACT REQUIRED WORKFLOW"
```

The tool refuses rollback if the workflow rule or any protected baseline field
has changed. Preserve canary runs, before/after ruleset JSON, actor, reason, and
timestamps on #104. Rollback does not enable auto-merge or weaken any
pre-existing protection.
