# Fail-closed selective CI

Status: **shadow only**. The existing required workflows and repository ruleset
remain unchanged. Auto-merge remains disabled.

## Trust and classification

`.github/ci/change-policy.v1.json` is the versioned path policy and
`.github/ci/change-classifier.js` is its repository-owned implementation. The
`Selective CI (Shadow)` workflow has no path filter and listens only for
read-only `pull_request_target` `opened`/`labeled`, dormant future `merge_group`, a weekly
`schedule` (Tuesday 03:17 UTC), and `workflow_dispatch`. `synchronize`,
`reopened`, and automatic `push` execution are intentionally absent during the
bounded collection window; the existing required workflows remain the `main`
push safety net.

`pull_request_target` is deliberate: GitHub loads the controller YAML from the
default branch rather than from the pull request. For a pull request, the
classification job checks out only `pull_request.base.sha` into `trusted-ci` and
gets changed-file metadata from the read-only, paginated REST endpoint. It never
checks out or executes pull-request code to classify it. Merge-group
classification similarly executes
`merge_group.base_sha` code and compares the base and group head. Compare API
`Link` headers paginate commits rather than file slices, so the classifier uses
only the first compare response and selects the full suite on any link, 300
files, or other truncation uncertainty. The initial
rollout, metadata errors, unexpected events, missing policy, empty changes,
invalid paths, unclassified files, unknown statuses, renames, deletions, and the
3,000-file PR limit, 100-file merge-base Markdown fetch budget, or 300-file
compare limit all select every domain.

For PR events, the classifier compares the paginated current PR-file response
with the immutable event base/head compare snapshot. Any filename, status,
count, statistics, or patch mismatch indicates a moving-head race and forces
the full suite. Domain jobs validate only the event's captured merge-commit SHA,
never a moving PR ref.

Changed workflow, classifier, repository automation script, ruleset/security
policy, dependency-update configuration (including `.github/dependabot.yml`),
repository actions, CODEOWNERS, Copilot instructions, agent/skill governance,
Squad governance, central package/build/project, SDK, generated-schema, or
executable documentation inputs select the full suite. `.mcp.json` is active
runtime configuration and also selects the full governance/security suite. Only the enumerated
Funding file, current issue/PR templates, `.gitignore`, `.gitattributes`,
and `.env.example` are inert repository metadata. Any future or
unknown `.github/**` path is unclassified and therefore fails closed. GitHub Markdown patches
contain hunk bodies without `+++`/`---` file headers, so every `+` or `-` body
line counts, including `+- bullet` and `-- bullet`. The count must exactly equal
GitHub's `changes` value. The classifier reads only the trusted base Markdown to
establish fence state at each hunk. For pull requests and merge groups it
requires the REST compare `merge_base_commit` and reads the old file through the
read-only Contents API at that exact SHA; it does not assume the moving
base-branch tip matches the patch. Context and deleted lines must also match the
merge-base blob.
The scan treats additions, deletions, fence starts/ends, changed lines inside
unchanged backtick or tilde fences, and four-space/tab-indented code as
executable. Indented-block state is established from the trusted base and
advanced through exact hunk context; indentation ambiguity is treated as
executable rather than docs-only. Labeled, unlabeled, and unknown-language
fences are all treated conservatively. Markdown is docs-only only when its
complete API patch changes prose outside every executable block. Missing or
misaligned merge-base content, patch, or hunk state fails closed. A deployed
JavaScript or C# change also selects OCI because it changes image content.

The workflow defaults to no `GITHUB_TOKEN` permissions. Classification receives
only `contents: read` and `pull-requests: read`. A maintainer-authorized sampled
domain job receives only `contents: read`, checks out the immutable
`pull_request.merge_commit_sha` captured by the labeled event on a fresh runner,
and does not persist credentials. A missing merge SHA forces full classification
and an invalid checkout ref, so it cannot fall back to validating the base
branch. The job receives no secrets and uses no cache. Thus default-branch code
owns orchestration and classification, while validation of pull-request code is
confined to unprivileged jobs. External actions and service images are immutable
SHA/digest pins. The classification job uploads the decision, including each
file's rule and reasons. The stable `Change classification` and `Aggregate gate`
contexts are emitted by default-branch-owned orchestration; `Aggregate gate`
always runs. Its JSON and summary enumerate `passed`, `not-applicable`,
`unavailable`, and `failed` dispositions and fail if classification or any
selected domain did not pass.
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

Trusted classification enforces a 132-request metadata budget: at most 30 PR
file pages, one compare request, and 100 Markdown base-content requests plus one
guard slot. Classification JSON records the limit, requests used, exhaustion,
minimum observed `X-RateLimit-Remaining`, and reset epoch. Evidence verification
requires `exhausted=false`, `used<=132`, and enough observed remaining budget for
the same path to finish. HTTP 429, or HTTP 403 with zero remaining, becomes an
explicit metadata rate-limit failure and therefore a fail-closed red aggregate.
That red result is an environmental/request-budget outcome, not automatically a
classifier logic defect; preserve the artifact/headers, wait for reset, and
re-run only under the fixed slot protocol.
Preflight observation on 2026-08-26 was core `14987/15000` remaining and search
`30/30`; this is a point-in-time capacity observation, not a guaranteed budget
for later runs.

Opened and labeled `pull_request_target` events emit `Change classification` and
`Aggregate gate`; synchronizing or reopening does not run this shadow workflow
during collection. Domain jobs run only for the single `labeled` event that applies
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

Selected .NET and PostgreSQL jobs upload separate machine-readable JSON reports
(`selective-ci-nuget-solution-*` and `selective-ci-nuget-postgresql-*`) with
`if-no-files-found: error` and 14 requested retention days (subject to the
observed 10-day cap). Verify both target identities, direct/transitive flags,
finding counts, actual sizes, and expirations when evaluating parity.

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
classifier pricing.** The first three valid labeled runs must use PR bases
containing the merged classifier. For each, compute independently rounded fixed
overhead as `ceil(classification seconds/60) + ceil(aggregate seconds/60)`.
Set `F` to the maximum of those three observations. Do not start the remaining
seven samples until FinOps approves `F` and refreshes every formula, ceiling, and
headroom number below if it differs from 2 minutes.

Provisional sampled pricing includes that fixed charge:
a modeled docs sample is 3 minutes / USD 0.024 and a full sample is 16 minutes /
USD 0.128. Exactly five eligible docs and five full samples therefore plan at
95 minutes / USD 0.760; if all intended docs samples fail closed to full, their
conservative ceiling is 160 minutes / USD 1.280.

The weekly Tuesday 03:17 UTC full-safety schedule costs provisionally 16 minutes
/ USD 0.128 per run. A 14-day window can intersect at most three weekly
occurrences. Let `S <= 3` cover scheduled and manual full-safety runs combined:
a manual dispatch consumes the same cap, and collection must pause before a
later weekly run would exceed it. Merge queue is unavailable, so this collection
sets `M=0` and budgets no merge-group run. Let `F` be trusted fixed rounded
overhead (provisionally 2) and `N <= 100` all opened/labeled PR event runs. The
planned window is `F*N + 75 + (F+14)*S`; the fail-closed sampled ceiling is
`F*N + 140 + (F+14)*S`. At provisional `F=2`, `N=100`, and `S=3`, planned use
is 323 minutes / USD 2.584 and the ceiling is 388 minutes / USD 3.104. Do not
start without 25% headroom: 485 rounded minutes / USD 3.880.

The rate baseline observed 60 PR openings from 2026-08-24 through the
2026-08-26 observation. A paginated
`GET /repos/cyrusjamula/Andreja/issues/events` observation covering that window
found zero labeled PR events, including zero historical sample-label events.
With ten planned valid label events, `N=70` at that observed volume. The 60
openings arrived in roughly two days (43 on August 24 and 17 thereafter),
approximately 30/day, so `N=100` is likely to bind near collection day 3, well
before the 14-day backstop. This is a short-window operational rate
observation, not a forecast.

Jett Reno owns run-count and headroom checks every 12 hours, before every sample
label, and at the named collection-day 3 and day 6 checkpoints. The day-3/day-6
records are mandatory audit points, not the only cap enforcement. Using the
recorded UTC window bounds, run this exact paginated Actions query:

```powershell
gh api --paginate --slurp `
  "repos/Jamula/Andreja/actions/workflows/selective-ci-shadow.yml/runs?event=pull_request_target&created=<START>..<END>&per_page=100" `
  --jq '[.[].workflow_runs[]] | length'
```

Jett records the result with Jobs API spend and recomputes remaining headroom
using the approved `F`. To prevent a checkpoint race from spending through the
ceiling, disable the workflow proactively at `N >= 75` or when fewer than
`25*F` rounded minutes remain above the computed spend; those 25 unsampled-run
slots are a reserve, not collection capacity. Pause with
`gh workflow disable selective-ci-shadow.yml`; re-enabling requires refreshed
headroom and FinOps approval. Also pause immediately at ten
valid samples or `S=3`. If an activity spike crosses `N=100` despite the
reserve, collection has breached its approved ceiling: keep the workflow
disabled, record the overrun, and require new FinOps approval rather than
calling the original ceiling satisfied.

Live PR run
[32924008713](https://github.com/Jamula/Andreja/actions/runs/32924008713)
predates the `trusted_classifier` domain gate and ran from pull-request-owned
workflow YAML. Its six domains passed in 635 completed-job seconds and 16 rounded
minutes, so it retains duration/artifact observations only; it is **not trusted
controller evidence**. Run
[32931733394](https://github.com/Jamula/Andreja/actions/runs/32931733394):
both stable contexts passed, all domains skipped, and aggregate evidence recorded
`trustedClassifierAvailable=false`, `samplePreconditionFailed=false`,
`shadow-not-sampled`, and `not-applicable`. It also ran from pull-request-owned
YAML and is not trusted topology evidence. Neither run is trusted-classifier,
trusted-controller, docs-only, merge-group, or valid-sample evidence.
Default-branch-owned `pull_request_target` topology begins only after merge.

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
branch so its base includes the merged trusted classifier, then apply the label
so the labeled event evaluates the current head. Count it only after its artifact
shows `trustedClassifierAvailable=true` and no
`trusted-classifier-unavailable-on-base` reason. Any later failed/canceled
trusted sample consumes its valid slot and cost but is not evidence; incomplete
evidence remains a promotion blocker. Stop at 14 calendar days, ten valid slots, 100 PR event
runs, or exhausted headroom, whichever happens first; if blockers remain, keep
the ruleset unchanged and pause the shadow workflow.

## Shadow rollout and promotion hold

1. Merge only this shadow workflow while existing required checks continue.
   Do not count any pre-merge `pull_request` run as controller-trust evidence;
   the trusted `pull_request_target` trigger exists only after the workflow is
   present on the default branch.
2. A maintainer provisions the sample label once:
   `gh label create ci:selective-shadow-sample --repo Jamula/Andreja --color 5319e7 --description "Maintainer-authorized bounded selective-CI sample"`.
   Confirm the repository role policy still prevents fork authors from applying
   labels.
3. Select exactly five eligible prose-only and five full-suite PRs. On a
   quiescent head, first update the branch so `pull_request.base.sha` contains
   the merged classifier. Because `synchronize` is not a collection trigger,
   that update does not run shadow CI. A maintainer applies the label with
   `gh pr edit <number> --add-label ci:selective-shadow-sample`, waits for that
   `labeled` run to finish, and counts it only if classification JSON has
   `trustedClassifierAvailable=true`, no bootstrap reason, and an acceptable API
   request-budget observation. The maintainer records Jobs/artifact API evidence,
   then immediately removes the label with
   `gh pr edit <number> --remove-label ci:selective-shadow-sample`.
   Branch updates, retained labels, and reopened PRs do not trigger collection.
4. Treat a labeled bootstrap aggregate failure as a precondition failure: remove
   the label, update the branch, and do not increment the ten-slot counter. After
   the first three valid trusted samples, set `F` to their maximum independently
   rounded fixed overhead; FinOps approval of refreshed formulas/headroom is
   required before the remaining seven.
5. Confirm unsampled PRs emit `shadow-not-sampled` and skipped domain jobs,
   sampled docs/full runs have correct dispositions, no fork gets write
   permission or secrets, and classification failure reports every domain
   `unavailable`.
6. Do not enable merge queue or collect merge-group canaries in this window.
   Complete the named **Merge Queue Activation and Merge-Group Evidence**
   prerequisite as a separate reviewed ruleset decision; its budget and canary
   protocol are outside this collection (`M=0`).
7. Obtain independent workflow correctness, security, and FinOps approvals and
   resolve every finding.
8. Only then prepare, review, and execute an atomic ruleset update. At collection
   end, remove the label from every PR and delete it only after audit evidence
   confirms no active sample:
   `gh label delete ci:selective-shadow-sample --repo Jamula/Andreja --yes`.

Repository merge queue is not configured/available as of 2026-08-26: the
effective main rules have no merge-queue rule and the Actions API reports zero
`merge_group` runs. Enabling it changes the ruleset and requires the separately
reviewed **Merge Queue Activation and Merge-Group Evidence** prerequisite. This
collection budgets `M=0`; no merge-group evidence may be fabricated or implied.

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
