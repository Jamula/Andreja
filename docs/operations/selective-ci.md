# Fail-closed selective CI

Status: **shadow only**. The existing required workflows and repository ruleset
remain unchanged. Auto-merge remains disabled.

## Trust and classification

`.github/ci/change-policy.v1.json` is the versioned path policy and
`.github/ci/change-classifier.js` is its repository-owned implementation. The
always-triggered `Selective CI (Shadow)` workflow has no path filter and listens
for `pull_request`, `merge_group`, a weekly `schedule` (Tuesday 03:17 UTC), and
`workflow_dispatch`. Automatic `push` execution is intentionally absent during
the bounded collection window; the existing required workflows remain the
`main` push safety net.

For a pull request, the classification job checks out only
`pull_request.base.sha` into `trusted-ci` and gets changed-file metadata from the
read-only, paginated REST endpoint. It never checks out or executes pull-request
code to classify it. Merge-group classification similarly executes
`merge_group.base_sha` code and compares the base and group head. Compare API
`Link` headers paginate commits rather than file slices, so the classifier uses
only the first compare response and selects the full suite on any link, 300
files, or other truncation uncertainty. The initial
rollout, metadata errors, unexpected events, missing policy, empty changes,
invalid paths, unclassified files, unknown statuses, renames, deletions, and the
3,000-file PR limit, 100-file merge-base Markdown fetch budget, or 300-file
compare limit all select every domain.

Changed workflow, classifier, repository automation script, ruleset/security
policy, dependency-update configuration (including `.github/dependabot.yml`),
repository actions, CODEOWNERS, Copilot instructions, agent/skill governance,
Squad governance, central package/build/project, SDK, generated-schema, or
executable documentation inputs select the full suite. Only the enumerated
Funding file, current issue/PR templates, `.gitignore`, `.gitattributes`,
`.env.example`, and `.mcp.json` are inert repository metadata. Any future or
unknown `.github/**` path is unclassified and therefore fails closed. GitHub Markdown patches
contain hunk bodies without `+++`/`---` file headers, so every `+` or `-` body
line counts, including `+- bullet` and `-- bullet`. The count must exactly equal
GitHub's `changes` value. The classifier reads only the trusted base Markdown to
establish fence state at each hunk. For pull requests and merge groups it
requires the REST compare `merge_base_commit` and reads the old file through the
read-only Contents API at that exact SHA; it does not assume the moving
base-branch tip matches the patch. Context and deleted lines must also match the
merge-base blob.
The scan treats additions, deletions, fence starts/ends, and changed lines inside
unchanged backtick or tilde fences as executable. Labeled, unlabeled, and
unknown-language fences are all treated conservatively. Markdown is docs-only
only when its complete API patch changes prose outside every fence. Missing or
misaligned merge-base content, patch, or hunk state fails closed. A deployed
JavaScript or C# change also selects OCI because it changes image content.

The only permissions are `contents: read` and `pull-requests: read`. Checkout
does not persist credentials. External actions and service images are immutable
SHA/digest pins. The classification job uploads the decision, including each
file's rule and reasons. The stable `Aggregate gate` always runs. Its JSON and
summary enumerate `passed`, `not-applicable`, `unavailable`, and `failed`
dispositions and fail if classification or any selected domain did not pass.
If classification fails, every domain is `unavailable` rather than
`not-applicable`. Concurrency keys include the event name, cancellation is
enabled only for superseded pull-request runs, and weekly/manual safety runs
never cancel each other.

The classifier also emits `trustedClassifierAvailable`. If a labeled PR's base
does not yet contain `.github/ci/change-classifier.js`, bootstrap evidence
contains `trusted-classifier-unavailable-on-base`, every aggregate domain is
`unavailable`, `samplePreconditionFailed=true`, no domain job runs, and the
aggregate fails. This is a precondition failure, not a sample. An ordinary
unlabelled bootstrap still succeeds with `shadow-not-sampled`/`not-applicable`
topology evidence because it never attempts domain sampling.

Pull requests always emit `Change classification` and `Aggregate gate`, but
domain jobs run only for the single `labeled` event that applies
`ci:selective-shadow-sample`. Merely retaining the label across a synchronize
event does not sample again. An unsampled PR records `shadow-not-sampled`;
affected domains remain visible in classification evidence while aggregate
evidence marks every unscheduled domain `not-applicable`. This is a non-required
shadow signal: the unchanged existing required workflows remain authoritative.
GitHub permits label application only to users with triage/write-equivalent
repository permission, so a fork author cannot self-sample.

## Domains

| Domain | Selected work |
| --- | --- |
| Docs | Consistency fixtures/check, deterministic architecture render, PNG policy |
| .NET | Restore, Debug/Release build and unit/architecture tests, format, direct/transitive vulnerability scan, pinned DevSkim |
| PostgreSQL | Compile, format, PostgreSQL-project vulnerability scan, and runtime tests against a disposable pinned PostgreSQL service |
| PowerShell | Pinned PSScriptAnalyzer with findings as failure |
| JavaScript/browser harness | Syntax checks and repository-owned Node fixtures; the existing separately controlled evidence Compose profile remains the source of real-browser evidence |
| Docker/OCI | Pin policy and negative cases, two-build reproducibility, SBOM/IaC/vulnerability scan, hosted unsigned provenance verification, then destruction of private layers/reports |

Schedules and manual dispatches always select all domains. The existing required
workflows remain the `main` push drift/safety net during collection.

The selective .NET domain deliberately invokes the advisory scan with
`-SkipPostgreSql`; PostgreSQL dependencies are scanned by the separately selected
PostgreSQL domain. A partial .NET-only decision therefore has less immediate
PostgreSQL advisory coverage than the existing `NuGet vulnerability audit`
context. Weekly/manual full-safety runs cover dependency drift, but this is a
recorded residual risk, not parity. The current vulnerability context cannot be
retired until live selective evidence demonstrates equivalent coverage and the
security/risk owner explicitly approves that residual.

## Measured baseline and target

Baseline sampled 2026-08-26 from successful pull-request runs for PR #117 at
head `e0c6c2843b2c19b9de84f0c2e89417d58801a23d`. This sample is a current
full-suite cost baseline, not a docs-only eligibility claim: PR #117 touched
threat-model and docs-consistency code and therefore correctly classifies full.
Runner time is the sum of GitHub job `started_at`/`completed_at`; estimated
billable minutes round each Linux job up independently. The cost estimate uses
the public hosted-Linux list rate of USD 0.008/minute and is not an invoice.

| Workflow | Run | Jobs | Runner seconds | Estimated billable minutes |
| --- | ---: | ---: | ---: | ---: |
| .NET Validation | 32920956382 | 5 | 330 | 8 |
| OCI Supply Chain | 32920956506 | 2 | 292 | 6 |
| PSScriptAnalyzer | 32920956479 | 1 | 31 | 1 |
| Docs Consistency | 32920956444 | 1 | 5 | 1 |
| **Total** | | **9** | **658** | **16** |

The six baseline artifacts total 136,641 bytes. Although their workflows
requested 14 days, the API reports expiration after 10 days, demonstrating a
repository/organization retention cap. The baseline list rate is approximately
USD 0.128 per run. A genuinely prose-only change should
run classification, docs, and aggregate only. The provisional target is at most
3 rounded runner-minutes (USD 0.024, at least 81.25% below this baseline) and
two bounded JSON artifacts requesting 14 days but expected to retain no longer
than the observed 10-day cap. This docs-only target is not a prediction for a
full shadow run, which adds new live PostgreSQL and JavaScript evidence absent
from the historical baseline. This target and a zero-tolerance false-negative
risk require owner approval. Post-change duration/storage/cost remains
**unavailable until a live shadow docs-only PR exists**; it must not be inferred
from local fixtures.

The fixed classifier was replayed against the 20 most recently updated merged
pull requests (merged 2026-08-24 through 2026-08-26). Machine-readable inputs,
per-PR outcomes, policy and classifier digests, and assumptions are in
`.github/ci/evidence/recent-merged-pr-replay.json`.

| Replay classification | Count | Share |
| --- | ---: | ---: |
| Eligible docs-only | 2 | 10% |
| Partial domain selection | 1 | 5% |
| Fail-closed full suite | 17 | 85% |

The normalized planning model uses the measured 16-minute full baseline,
a provisional 2 fixed minutes for classification/aggregate, and measured rounded
domain minutes from bootstrap run 32924008713. It estimates 292 rather than 320 rounded
minutes across the sample: 28 minutes and USD 0.224 list rate saved, or **8.75%
portfolio savings**. This sits next to, and is deliberately much lower than,
the **81.25% per-run** target for an eligible docs-only PR. It is a historical
planning replay, not live selective billing evidence. The cost baseline is
**n=1**, only 3 of 20 replayed PRs (15%) were selectively eligible, and one PR is
five percentage points of the sample. Reclassifying one docs-only observation
as full lowers modeled savings from 8.75% to 4.69%; one additional full PR
becoming docs-only raises it to 12.81%. Treat 8.75% as sensitivity-prone, not a
forecast.

Shadow operation is additive because the existing five heavy required contexts
remain. The modeled **8.75% steady-state saving exists only if a later,
independently reviewed ruleset change retires those five contexts after parity
is proven**. Until then, both this shadow and any additive promotion increase
runner cost.

Bootstrap run 32927691826 measured fixed shadow overhead at 14 seconds for classification
plus 5 seconds for aggregation. Independent per-job rounding makes that 2 Linux
minutes / USD 0.016 for every pull-request event run. Its classification and
aggregate archives were 337 and 597 bytes (934 bytes total), and the API applied
the observed 10-day retention cap. **This is bootstrap-only pricing, not trusted
classifier pricing.** The first valid labeled run must use a PR base containing
the merged classifier. Immediately after that run, use the completed Jobs API to
reprice classification and aggregate; do not start any of the remaining nine
samples until FinOps approves the new fixed rounded overhead and every formula,
ceiling, and headroom number below is refreshed if it differs from 2 minutes.

Provisional sampled pricing includes that fixed charge:
a modeled docs sample is 3 minutes / USD 0.024 and a full sample is 16 minutes /
USD 0.128. Exactly five eligible docs and five full samples therefore plan at
95 minutes / USD 0.760; if all intended docs samples fail closed to full, their
conservative ceiling is 160 minutes / USD 1.280.

The weekly Tuesday 03:17 UTC full-safety schedule costs provisionally 16 minutes
/ USD 0.128 per run. A 14-day window can intersect at most three weekly
occurrences. Let `S <= 3` cover scheduled and manual full-safety runs combined:
a manual dispatch consumes the same cap, and collection must pause before a
later weekly run would exceed it. Before a merge-queue rollout, budget at most three real
`merge_group` canaries. Let `F` be the trusted fixed rounded overhead (provisionally
2), `N <= 100` all PR event runs, and `M <= 3`
merge-group canaries. With current domain estimates, the planned window is
`F*N + 75 + (F+14)*(S+M)` and the fail-closed sampled ceiling is
`F*N + 140 + (F+14)*(S+M)`. At provisional `F=2`, `N=100`, `S=3`, and `M=3`,
planned use is 371 minutes / USD 2.968 and the ceiling is 436 minutes /
USD 3.488. Do not start the window without 25% headroom: 545 rounded minutes /
USD 4.360. `N <= 100` is expected to bind before 14 days; the time limit is a
secondary backstop, not the primary bound.

Jett Reno owns run-count and headroom checkpoints on collection day 3 and day 6.
Using the recorded UTC window bounds, run this exact paginated Actions query:

```powershell
gh api --paginate --slurp `
  "repos/Jamula/Andreja/actions/workflows/selective-ci-shadow.yml/runs?event=pull_request&created=<START>..<END>&per_page=100" `
  --jq '[.[].workflow_runs[]] | length'
```

Jett records the result with Jobs API spend, recomputes remaining headroom using
the approved `F`, and pauses the workflow immediately at `N=100`, exhausted
headroom, ten valid samples, `S=3`, or `M=3`.

Live PR run
[32924008713](https://github.com/Jamula/Andreja/actions/runs/32924008713)
proved the workflow topology, complete job graph, stable aggregate context, and
artifact upload: all six selected domains passed in 635 completed-job seconds
and 16 rounded minutes, matching the baseline's 16-minute full-suite cost class.
It used `trusted-classifier-unavailable-on-base` bootstrap behavior. It is
**bootstrap topology/job-graph/artifact evidence only**, not trusted-classifier,
docs-only, merge-group, or post-fix behavioral evidence. The five-docs plus
five-full collection starts only after the shadow workflow merges to `main`.

Every aggregate artifact records diagnostic step-wall-clock seconds for
classification, selected domains, and aggregate evaluation, plus rounded-minute
and cost lower bounds and aggregate JSON bytes. Step timers exclude queue,
runner provisioning, and PostgreSQL service startup. Depending on step order,
individual timers can include pre-timing cleanup/upload, but always exclude work
after their timing step and the aggregate job's final upload. Therefore
authoritative post-change comparisons must use completed-run Jobs API
`started_at`/`completed_at`, the same method as the baseline. Verify actual
artifact `expires_at` rather than assuming requested retention. Apply the label
for exactly ten **valid** runs: five eligible docs-only candidates and five full
candidates. A bootstrap precondition failure is invalid, consumes only its
ordinary `N` overhead, and does not consume a valid sample slot. Update the PR
branch so its base includes the merged trusted classifier, run unlabelled first,
and verify `trustedClassifierAvailable=true` and the
`trusted-classifier-unavailable-on-base` reason is absent before reapplying the
label or incrementing sample counts. Any later failed/canceled trusted sample
consumes its valid slot and cost but is not evidence; incomplete evidence remains
a promotion blocker. Stop at 14 calendar days, ten valid slots, 100 PR event
runs, or exhausted headroom, whichever happens first; if blockers remain, keep
the ruleset unchanged and pause the shadow workflow.

## Shadow rollout and promotion hold

1. Merge only this shadow workflow while existing required checks continue.
2. A maintainer provisions the sample label once:
   `gh label create ci:selective-shadow-sample --repo Jamula/Andreja --color 5319e7 --description "Maintainer-authorized bounded selective-CI sample"`.
   Confirm the repository role policy still prevents fork authors from applying
   labels.
3. Select exactly five eligible prose-only and five full-suite PRs. On a
   quiescent head, first update the branch so `pull_request.base.sha` contains
   the merged classifier. Run the PR unlabelled and inspect classification JSON:
   require `trustedClassifierAvailable=true` and no
   `trusted-classifier-unavailable-on-base` reason. Only then may a maintainer
   apply the label with
   `gh pr edit <number> --add-label ci:selective-shadow-sample`, waits for that
   `labeled` run to finish, records Jobs/artifact API evidence, then immediately
   removes it with
   `gh pr edit <number> --remove-label ci:selective-shadow-sample`.
   A synchronize event never qualifies merely because a label remains.
4. Treat a labeled bootstrap aggregate failure as a precondition failure: remove
   the label, update the branch, repeat the unlabelled trust check, and do not
   increment the ten-slot counter. After the first valid trusted sample, stop and
   reprice `F` from the Jobs API; FinOps approval of refreshed formulas/headroom
   is required before any of the remaining nine.
5. Confirm unsampled PRs emit `shadow-not-sampled` and skipped domain jobs,
   sampled docs/full runs have correct dispositions, no fork gets write
   permission or secrets, and classification failure reports every domain
   `unavailable`.
6. Collect up to three real `merge_group` canaries with the same stable aggregate
   context, charge each as `F+14`, and stop at `M=3`.
7. Obtain independent workflow correctness, security, and FinOps approvals and
   resolve every finding.
8. Only then prepare, review, and execute an atomic ruleset update. At collection
   end, remove the label from every PR and delete it only after audit evidence
   confirms no active sample:
   `gh label delete ci:selective-shadow-sample --repo Jamula/Andreja --yes`.

Repository merge queue is not configured/available as of 2026-08-26: the
effective main rules have no merge-queue rule and the Actions API reports zero
`merge_group` runs. This is a promotion blocker. No merge-group evidence may be
fabricated.

Ruleset `21199927` was read but not changed. Its observed weak ETag was
`W/"027443a7de0846f3da4da7e0ec926a795f7ce378ebd57df4f5ca6a0e646c3514"`;
it has no bypass actors, requires thread resolution, strict status checks, and
the five existing .NET contexts. Before any later write:

1. GET the live ruleset with response headers and save the complete body, ETag,
   required contexts, pull-request parameters, and bypass actors as rollback
   evidence.
2. Abort if the body or ETag differs from the independently reviewed candidate.
3. Preserve every existing property: active enforcement; default-branch
   conditions; deletion, non-fast-forward, and linear-history rules; zero
   approving reviews; `dismiss_stale_reviews_on_push=false`; empty
   `required_reviewers`; `require_code_owner_review=false`; disabled/empty
   dismissal restrictions; `require_last_push_approval=false`;
   `required_review_thread_resolution=true`;
   `require_extra_approval_for_unattributed_changes=true`;
   `allowed_merge_methods=[squash]`; code-quality severity `notes`; Copilot
   `review_on_push=true` and `review_draft_pull_requests=false`; strict required
   checks; `do_not_enforce_on_create=false`; all five existing required contexts
   with integration ID 15368; and empty bypass actors. During the first additive
   change, add the live aggregate context with its observed Actions integration
   ID; never guess it.
4. Send the complete update with `If-Match: <live-etag>`. Re-GET and compare
   every preserved field. On mismatch, restore the saved body with the new live
   ETag and verify again.
5. Retiring old always-heavy required contexts is a separate reviewed change
   only after the selective workflow itself becomes trusted default-branch code
   and equivalent live evidence is complete.

De-shadowing is also separate and atomic. Do not rename
`Selective CI (Shadow)`, remove the sample gate, restore `push`, or replace any
ruleset context in the evidence-collection PR. First demonstrate the exact live
PR and merge-group check-run names and Actions integration IDs from
default-branch code. In an ETag-guarded update, add the proven stable aggregate
context while preserving all old contexts; re-GET and verify before any later
change renames the workflow or retires a context. Because a workflow display
name and a job/check context can be represented differently by GitHub, never
infer one from the YAML name. A missing or renamed aggregate remains fail
closed. Roll back the ruleset body before rolling back/renaming workflow code.

Restoring `push` is a separately reviewed promotion change. It must retain the
all-zero `before` branch-creation fallback to `github.sha`, force every domain,
and keep `github.event_name` in the concurrency key so push cannot collide with
schedule/manual runs. `cancel-in-progress` must remain false for schedule and
manual events. Until that change is proven, the existing .NET, OCI, PowerShell,
docs, and related workflows remain the `main` push safety net.

A future sole-aggregate requirement intentionally concentrates trust in one
stable context. Its residual-risk controls are explicit: every workflow or
classifier edit forces the full suite, and deleting or renaming the workflow,
workflow name, or `Aggregate gate` job prevents the exact required context from
appearing, so strict protection blocks rather than silently passing. This does
not replace independent workflow review, default-branch trust, or the live
merge-group canary.

Issue #115's blocked external review-gate contract is not a dependency or a
completed control here. Issue #104's independent review-completion protections
must not be weakened; this rollout does not alter them.
