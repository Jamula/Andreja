# Review-gate GitHub App

Issue [#104](https://github.com/Jamula/Andreja/issues/104) owns this
design. It is repository scaffolding, not a deployed merge gate.

## Current rollout status

**Blocked. Keep PRs draft or under a manual merge hold, keep auto-merge
disabled, and do not add the ruleset check.**

At the 2026-08-25 revision:

- repository Actions variables: **0**;
- repository Actions secrets: **0**;
- the installed `Copilot Pull Request Reviewer` App is App ID `946600`,
  but its observed installation permissions include `checks:read`, not
  `checks:write`;
- existing repository checks are published as the generic GitHub Actions App,
  App ID `15368`;
- repository-installation enumeration was unavailable to the current
  credential, so no other dedicated checks-writer identity could be proven;
- no successful check from the proposed review-gate App exists; and
- active branch ruleset `21199927` remains unchanged, with no bypass actors,
  zero required approvals, required thread resolution, strict up-to-date
  checking, and its five existing GitHub Actions checks.

The earlier required-workflow design was removed. Ordinary review, thread,
issue, and dispatch workflow runs cannot supersede a same-head ruleset-workflow
result. `cancel-in-progress` could also cancel the only ruleset-created
generation. A generic GitHub Actions check is not an acceptable replacement.

## Required trust boundary

Provisioning requires an independently owned GitHub App installed only on
`Jamula/Andreja`. A PR-authored workflow must have no way to mint its
installation token.

Required App repository permissions:

| Permission | Access | Purpose |
| --- | --- | --- |
| Checks | Read/write | Start pending generations and publish the exact required check |
| Issues | Read/write | Read labels and append authenticated policy-ledger events |
| Pull requests | Read | Read exact PR/head/base/reviews |
| Administration | Read | Revalidate reviewer and policy-admin permission |
| Metadata | Read | Resolve repository and installation identity |
| Contents | Read | Read only trusted default-branch automation |

Subscribe to pull request, pull request review, review comment/thread, issue,
issue-comment deletion, and installation events. Select only this repository.
Do not grant contents write, actions write, workflows write, secrets, or broad
organization access.

The default-branch workflow bridge expects only these names:

- variable `REVIEW_GATE_APP_CLIENT_ID`;
- variable `REVIEW_GATE_APP_ID` (exact numeric App ID); and
- secret `REVIEW_GATE_APP_PRIVATE_KEY`.

The pinned `actions/create-github-app-token` action narrows the token to this
repository. Both privileged workflows check out `github.workflow_sha`, disable
credential persistence, execute no PR code, and use the App token for all
policy/check writes.

Phase 0 currently authorizes no remote provisioning. Creating or installing
the App and storing credentials therefore requires the applicable explicit
human authorization before this runbook can proceed.

## Check and generation model

The required context is:

```text
Andreja review policy + exact review-gate App ID
```

The publisher identity, not the text alone, is the security boundary.
`.github/workflows/review-gate-app.yml` handles trusted default-branch metadata
events through that App. Each direct PR event:

1. creates a new `in_progress` App check on the event's PR head before policy
   evaluation;
2. records the event/run generation in `external_id`;
3. evaluates every open PR sharing that head SHA;
4. requires two identical full snapshots before success; and
5. publishes a terminal result only if its check-run ID is still the newest
   exact-App generation for that SHA.

An older writer never updates after a newer generation exists. Workflows have
no cancellation concurrency. Two PRs sharing a head are intentionally
aggregated: the commit-scoped check succeeds only if **every** open PR sharing
the SHA passes its own PR/base/policy evaluation. This prevents one PR's result
from replaying onto a different base, at the cost of conservatively blocking
both when either is incomplete.

`merge_group` and default-branch runs publish explicit `not_applicable`
success: PR-head policy was already enforced. Drafts and unresolved threads
remain native and App-enforced hard failures.

## Authenticated monotonic policy

Closing references and PR labels are author-controlled inputs. They do not
initialize policy.

A `maintain` or `admin` human must first run **Review gate App
administration** with operation `bind-issue`, the complete live
`PR + head + base repository/ref/SHA` identity, the current policy digest,
reason, and repository-local audit URL. The App appends an immutable hidden
policy event to the PR conversation. Evaluation accepts it only when
`performed_via_github_app.id` equals the configured App ID and the event
integrity digest, repository ID/name, and PR number all match.

After binding:

- every observed PR review-requirement label is persisted;
- every observed requirement label on a bound issue is persisted;
- removing a PR label, issue label, or `Closes #...` reference does not remove
  an observation;
- issue metadata events reevaluate every PR in the authenticated association
  ledger; and
- policy digest changes invalidate evidence created for an earlier policy.

Requirements can decrease only through the same admin workflow's
`reduce-policy` operation. It requires the exact current diff, current policy
digest, an explicit JSON list of active event IDs, a human with
`maintain`/`admin`, a bounded reason, and an audit URL. The App record targets
only those event IDs; a concurrently added observation remains required.
Stale digests or stale event IDs fail.

## Review evidence

Every ready PR requires:

1. the newest native review from user ID `175728472`,
   `copilot-pull-request-reviewer[bot]`, type `Bot`, on the current head, plus
   an App-authored attestation captured at review submission that binds that
   review ID to the exact PR/head/base identity;
2. zero unresolved review threads with full GraphQL pagination; and
3. independent evidence for every persisted domain: either a native human
   review or a gate-App event produced only after the App service authenticates
   an allowlisted specialist integration run.

Valid domains are architecture, security, privacy, and quality. Only the newest
review containing that domain marker is considered. The reviewer must be a
human with current `write`, `maintain`, or `admin`, must differ from the PR
author, and must approve. The marker binds:

```text
schema v3 + domain + PR number + head SHA +
base repository ID/name + base ref + base SHA +
current authenticated policy digest + evidence URL + summary
```

A copied, malformed, stale, dismissed, or rejected newest candidate blocks;
the evaluator never falls back. A push, retarget, base advance, or policy
increase invalidates prior evidence.

An automation attestation binds the same exact diff and policy plus domain,
outcome, upstream App ID/slug/run ID, evidence URL, and summary. The admin
workflow cannot create one. The external App worker must verify the upstream
installation/run against its reviewed allowlist before publishing. No such
specialist integration is currently proven or configured, so this path is also
a rollout prerequisite rather than deployed evidence.

Deleting an App-authored policy comment emits a trusted `issue_comment`
metadata event. The bridge starts a new generation and restores the exact
integrity-checked event before evaluation. A copied marker from any other App
or user is ignored.

This does not require a second human for every PR. Authenticated current-diff
App automation evidence is valid where policy permits it. Author self-approval
never counts as independent domain evidence.

## Break-glass

Break-glass is an App-authored policy event, not a bypass actor, approval-count
change, generic status, or workflow artifact. A `maintain`/`admin` human uses
the admin workflow with operation `break-glass`, complete current diff
identity, current policy digest, bounded reason, and durable audit URL.

The exact App provenance and content are revalidated. It may substitute for
unavailable Copilot/domain evidence for that exact diff and policy. It cannot
permit a draft or unresolved thread. Any head, base, or policy change
invalidates it.

Reasons, summaries, and audit links become repository metadata. Never include
prompts, connector content, tokens, personal data, or private diagnostics.

## Remaining platform blocker

The checked-in Actions bridge is deliberately insufficient evidence for
rollout by itself.

For direct PR metadata, it can create the pending App check before its first
policy API read, but only after the workflow is scheduled, starts, mints a
token, and checks out trusted code. For an `issues` event it must additionally
resolve authenticated issue-to-PR mappings. Queue delay or failure before
check creation would leave an older success visible. GitHub checks are
commit-scoped and do not expire automatically.

Before activation, either:

1. deploy an authorized App webhook worker with durable
   `issue -> PR -> current head` state that starts the new generation before
   metadata evaluation and demonstrably fails closed on delivery/API failure;
   or
2. empirically prove a GitHub-native mechanism that closes this exact startup
   gap.

If neither is available, the ruleset rollout remains blocked. A successful
happy-path Actions canary does not waive this condition.

## Mandatory live canary

Run only after the reviewed code is on `main`, the dedicated App is authorized
and installed, credentials are configured, and the startup blocker above has
an enforceable implementation.

1. Record live `main`, the full ruleset JSON, ETag, and digest.
2. Open two canary PRs sharing one head SHA but using distinct PR/base
   identities. Keep both draft and auto-merge disabled.
3. Authentically bind each PR to different issue policies.
4. Confirm every check run named `Andreja review policy` has the exact expected
   App ID and `andreja-review-gate:v3:` external identity.
5. Exercise draft/ready, Copilot late completion, current-head push, retarget,
   base advance, PR labels, bound-issue labels, author removing closing
   references, unresolved/resolved threads, newer review rejection, policy
   increase, audited reduction, break-glass, two shared-head PRs, and manual
   rerequest.
6. Inject 403/429 and failures before/after policy reads, stale concurrent
   writers, wrong-App checks, missing credentials, webhook redelivery, dropped
   delivery, and worker restart. At no transition may an older success make
   either PR merge-ready.
7. Record event delivery IDs, check-run IDs/App IDs/external IDs, expected and
   actual mergeability, timestamps, and rollback evidence on #104.

Any merge-ready interval or ambiguous publisher fails the canary.

## Latency, cost, and selective CI

The observed Copilot-review latency that motivated #104 was 237 seconds on
#100, 489 seconds on #101, and 267 seconds on #105 (median 267, maximum 489).
The evaluator retains a 12-minute bounded wait and adds no model request; it
waits for the already configured Copilot review. Each metadata transition does
consume one trusted workflow/App-check evaluation plus bounded GitHub API
calls, which must be measured during canary.

Issue [#102](https://github.com/Jamula/Andreja/issues/102) may reduce unrelated
C# work for docs-only PRs. It must not path-filter, skip, or synthesize this
review policy. This revision coordinates that boundary but does not implement
#102.

## Guarded ruleset rollout

Plan mode may use a reviewed file snapshot for offline inspection. Apply mode
rejects `--input`, refetches live state immediately, requires the plan's exact
digest and ETag, sends `If-Match`, and verifies the complete post-state.
Every current required check and rule is preserved; bypass actors must remain
empty.

```powershell
node .github\scripts\review-gate-ruleset.js plan-rollout `
  --repo Jamula/Andreja `
  --ruleset-id 21199927 `
  --app-id <exact-numeric-app-id>
```

Use the live plan's `beforeDigest` and `etag`:

```powershell
node .github\scripts\review-gate-ruleset.js apply-rollout `
  --repo Jamula/Andreja `
  --ruleset-id 21199927 `
  --app-id <exact-numeric-app-id> `
  --expected-main-sha <live-main-sha> `
  --expected-ruleset-digest <plan-beforeDigest> `
  --expected-etag '<plan-etag>' `
  --pr-canary-head-sha <canary-pr-head-sha> `
  --pr-canary-check-run-id <approved-exact-app-check-run-id> `
  --merge-group-canary-head-sha <merge-group-head-sha> `
  --merge-group-canary-check-run-id <not-applicable-exact-app-check-run-id> `
  --main-check-run-id <not-applicable-exact-app-main-check-run-id> `
  --evidence-url https://github.com/Jamula/Andreja/issues/104#issuecomment-... `
  --confirm "APPLY EXACT APP CHECK"
```

The script also verifies every trusted workflow/script exists at that exact
live `main`, plus PR, real `merge_group`, and current-main check names, heads,
App IDs, statuses, conclusions, external identities, and expected
approved/not-applicable outputs. `412 Precondition Failed` or any post-state
difference aborts without retrying a stale payload.

## Rollback

Rollback removes only the exact
`Andreja review policy + App ID` requirement from freshly fetched live state.
It preserves every other rule/check, uses the same ETag/`If-Match` guard, and
requires an audit URL.

```powershell
node .github\scripts\review-gate-ruleset.js plan-rollback `
  --repo Jamula/Andreja `
  --ruleset-id 21199927 `
  --app-id <exact-numeric-app-id>

node .github\scripts\review-gate-ruleset.js apply-rollback `
  --repo Jamula/Andreja `
  --ruleset-id 21199927 `
  --app-id <exact-numeric-app-id> `
  --expected-main-sha <live-main-sha> `
  --expected-ruleset-digest <plan-beforeDigest> `
  --expected-etag '<plan-etag>' `
  --evidence-url https://github.com/Jamula/Andreja/issues/104#issuecomment-... `
  --confirm "ROLLBACK EXACT APP CHECK"
```

Preserve plan/apply output, ruleset before/after JSON, ETags, check provenance,
canary transitions, actor, reason, and timestamps. Rollback never enables
auto-merge or changes any other protection.
