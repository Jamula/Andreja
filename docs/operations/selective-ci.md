# Fail-closed selective CI

Status: **shadow only**. The existing required workflows and repository ruleset
remain unchanged. Auto-merge remains disabled.

## Trust and classification

`.github/ci/change-policy.v1.json` is the versioned path policy and
`.github/ci/change-classifier.js` is its repository-owned implementation. The
`Selective CI (Shadow)` workflow has no path filter and listens only for
read-only `pull_request_target` `opened`/`labeled` and `workflow_dispatch`.
There is no current `schedule`, `merge_group`, `synchronize`, `reopened`, or
automatic `push` trigger. The existing required workflows remain the `main`
push safety net.

`pull_request_target` is deliberate: GitHub loads the controller YAML from the
default branch rather than from the pull request. For a pull request, the
classification job checks out only `pull_request.base.sha` into `trusted-ci` and
gets changed-file metadata from the read-only, paginated REST endpoint. It never
checks out or executes pull-request code to classify it. Dormant merge-group
classifier/workflow expressions remain fail-closed code but are unreachable in
this collection. A separately reviewed activation/promotion must restore the
trigger and then execute `merge_group.base_sha` code to compare the base and
group head. Compare API
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
runtime configuration and also selects the full governance/security suite.
`.gitattributes` is active repository behavior because it controls merge drivers
and checkout/line-ending normalization, so it also selects the full suite. Only
the enumerated Funding file, current issue/PR templates, `.gitignore`, and
`.env.example` are inert repository metadata. Any future or unknown
`.github/**` path is unclassified and therefore fails closed. GitHub Markdown patches
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
executable, including those constructs nested in Markdown blockquotes.
Changed inline code spans also select the full suite. The scan recognizes
matching single- or multi-backtick delimiters, ignores backslash-escaped
backticks outside code spans, and preserves literal shorter backtick runs inside
longer delimiters. Additions or deletions within a trusted-base multiline code
span are executable even when the changed line contains no delimiter. Any
unmatched changed delimiter or other inline-span ambiguity fails closed.
Indented-block state is established from the trusted base and
advanced through exact hunk context; indentation ambiguity is treated as
executable rather than docs-only. Tabs in changed lines or the trusted base are
also treated as executable/ambiguous because Markdown expands them by column.
Labeled, unlabeled, and unknown-language
fences are all treated conservatively. Markdown is docs-only only when its
complete API patch changes prose outside every executable block. Missing or
misaligned merge-base content, patch, or hunk state fails closed. A deployed
JavaScript or C# change also selects OCI because it changes image content.

The workflow defaults to no `GITHUB_TOKEN` permissions. Classification receives
only `contents: read` and `pull-requests: read`. A repository-owned
`SELECTIVE_CI_SAMPLE_OPERATOR` Actions variable supplies the expected operator
login to default-branch `pull_request_target` orchestration; pull-request code
and event fields cannot override it. The classifier requires the trusted event
sender login to equal that operator before sampling and records both
`observedActor` and `expectedOperator` in classification JSON. An unset variable
or mismatched sender records `sample-operator-unconfigured` or
`sample-operator-mismatch`, keeps `sampled=false`, and schedules no domain job.
An operator-authorized sampled domain job receives only `contents: read`, checks out the immutable
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
`not-applicable`. An authorized sample uses the stable per-PR
`sample-pr-<number>` concurrency group with cancellation disabled, serializing
duplicate authorized events. Ordinary PR topology events use a separate
`topology-pull_request_target-<number>` group with cancellation enabled, so an
unrelated label cannot cancel a running authorized sample. Manual safety runs
never cancel each other. Aggregate revision evidence records the event base and
head SHAs separately from the revision used by domain jobs. A valid PR sample
records its immutable merge SHA as both the validated ref and SHA; merge-group
jobs record the group head, and workflow-dispatch jobs record the exact
default-branch `github.sha`. Dormant merge-group, push, and schedule handling is
unreachable until a separately reviewed trigger change. Unsampled or
bootstrap-blocked runs record no validated revision. The aggregate populates
`validatedRef` and `validatedSha` only when both are valid 40-hex revisions. A sampled run with a missing or
malformed merge SHA records both fields as `null`,
`validationRevisionAvailable=false`, and
`exact-validation-revision-unavailable`; every selected domain is unavailable
and the aggregate fails even if an upstream job result incorrectly appears
successful.

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
`ci:selective-shadow-sample` when its trusted sender equals
`vars.SELECTIVE_CI_SAMPLE_OPERATOR`. Merely retaining the label across a
synchronize event does not sample again. An ordinary unsampled PR records
`shadow-not-sampled`; a rejected sample-label event records its explicit operator
authorization failure;
affected domains remain visible in classification evidence while aggregate
evidence marks every unscheduled domain `not-applicable`. This is a non-required
shadow signal: the unchanged existing required workflows remain authoritative.
GitHub permits label application only to users with triage/write-equivalent
repository permission, but label permission alone does not authorize sampling.

## Domains

| Domain | Selected work |
| --- | --- |
| Docs | Consistency fixtures/check, deterministic architecture render, PNG policy |
| .NET | Restore, Debug/Release build and unit/architecture tests, format, direct/transitive vulnerability scan, pinned DevSkim |
| PostgreSQL | Compile, format, PostgreSQL-project vulnerability scan, and runtime tests against a disposable pinned PostgreSQL service |
| PowerShell | Pinned PSScriptAnalyzer with findings as failure |
| JavaScript/browser harness | Syntax checks and repository-owned Node fixtures; the existing separately controlled evidence Compose profile remains the source of real-browser evidence |
| Docker/OCI | Pin policy and negative cases, two-build reproducibility, SBOM/IaC/vulnerability scan, hosted unsigned provenance verification, then destruction of private layers/reports |

Manual dispatches always select all domains. The existing required
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
| Eligible docs-only | 0 | 0% |
| Partial domain selection | 0 | 0% |
| Fail-closed full suite | 20 | 100% |

The normalized planning model uses the measured 16-minute full baseline,
a provisional 2 fixed minutes for classification/aggregate, and measured rounded
domain minutes from bootstrap run 32924008713. After changed inline code spans
were classified fail closed, it estimates 320 rounded minutes across the sample:
zero minutes and USD 0 saved, or **0% portfolio savings**. The historical sample
contains no eligible prose-only or partial change, so it provides no portfolio
savings evidence. The **81.25% per-run** target remains only a target for a
future genuinely prose-only PR. This is a historical planning replay, not live
selective billing evidence. The cost baseline is **n=1**, and one PR is five
percentage points of the 20-PR sample. The zero result does not prove future
prose-only changes are impossible; it does mean this sample cannot support a
savings forecast. This **0% replay supersedes the earlier 8.75% planning
basis**. The feasibility window below measures fail-closed parity and whether a
natural candidate exists; it does not claim savings.

Shadow operation is additive because the existing five heavy required contexts
remain. The replay models **no steady-state portfolio saving** under the current
conservative classifier. Any later savings claim requires new representative
evidence and an independently reviewed ruleset change after parity is proven.
Until then, both this shadow and any additive promotion increase runner cost.

Bootstrap run 32927691826 measured 14 seconds for classification plus 5 seconds
for aggregation. Independent per-job rounding gives provisional fixed overhead
`F=2` Linux minutes / USD 0.016. This is bootstrap-only pricing, not trusted
classifier pricing.

This window is a **small feasibility gate**, not a savings collection:

- `N <= 25` counts every `opened`/`labeled` pull-request-target run.
- `S=1` is the single full-safety `workflow_dispatch` smoke.
- At most one pre-identified, naturally useful prose-only PR and one full-suite
  PR may receive the sample label.
- No synthetic or no-op change may be created solely to obtain evidence.

The prose candidate must be outside every known executable, generated-schema,
workflow, build, package, SDK, migration, security-policy, evidence-script,
Docker, and Compose boundary. Its changed Markdown must contain no code spans,
fences, or indented commands. If no qualifying prose candidate exists before a
cap or tripwire, feasibility fails and promotion remains blocked.

At provisional `F=2`, planned use is
`F*N + 15 + (F+14)*S = 81 minutes / USD 0.648`. The fail-closed ceiling is
`F*N + 28 + (F+14)*S = 94 minutes / USD 0.752`. Do not start without 25%
headroom: **118 minutes / USD 0.944**. The 15 planned domain minutes are one
docs minute plus fourteen full-suite domain minutes; fixed overhead for both PR
samples is already included in `N`. The 28-minute ceiling assumes both labeled
PRs select all domains.

For the smoke, docs sample, and full sample, compute
`F_i = ceil(classification seconds/60) + ceil(aggregate seconds/60)` from the
completed Jobs API records. Retain `F=2` provisionally and then set `F` to the
**maximum of the smoke, docs, and full trusted runs**. Any observed `F_i >= 4`,
a recomputed ceiling above the approved 118-minute headroom, exhausted API or
runner headroom, or an unavailable required Jobs/artifact observation
terminally disables this workflow and requires a separately approved new
window. There is no routine pause/re-enable path in this feasibility window.

Workflow job timeouts sum to **200 job-minutes for one full run**: classifier
10, docs 15, .NET 45, PostgreSQL 30, PowerShell 15, JavaScript 15, OCI 60, and
aggregate 10. The whole-window timeout-derived bound is
`20*N + 180*2 + 200*S = 1,060 job-minutes / USD 8.480`. This is a fail-safe
exposure bound, not approved spend. Terminally disable if completed Jobs API
rounded time for the docs sample exceeds 6 minutes or the smoke or full sample
exceeds 32 minutes (twice modeled duration).

The rate baseline observed 60 PR openings over roughly two days. This makes the
small cap intentionally short-lived, not a forecast. Jett Reno owns checks
every 12 hours and before each label. Using the recorded UTC bounds, run:

```powershell
$pages = gh api --paginate --slurp `
  "repos/Jamula/Andreja/actions/workflows/selective-ci-shadow.yml/runs?event=pull_request_target&created=<START>..<END>&per_page=100" |
  ConvertFrom-Json
$runs = @($pages | ForEach-Object { $_.workflow_runs })
$runs.Count
```

At `N >= 20`, terminally disable proactively to preserve five-run headroom.
`N=25` is the hard cap. The same terminal action applies after the one dispatch,
the two valid labeled samples, any tripwire, exhausted headroom, or failure to
find the prose candidate before a cap. Use
`gh workflow disable selective-ci-shadow.yml`, record the UTC shutdown time and
last run ID, and verify that no later run was created. Re-enable is not allowed
under this window; a new approved budget/window must start with its own charged
smoke.

Live PR run 32924008713 remains duration/artifact evidence only. Run 32931733394
remains prior unsampled topology evidence only. The first default-branch-owned
`pull_request_target` controller revision at `cb7a434` has never executed, and
the later trigger-reduced workflow revision at `95d7450` has likewise never
executed. The **current PR workflow has never executed from the default branch**;
only the exact merged revision can become trusted controller evidence.

Every aggregate artifact records diagnostic timings, rounded-minute/cost lower
bounds, exact serialized JSON bytes, API request-budget observations, revisions,
and domain dispositions. Authoritative pricing uses Jobs API
`started_at`/`completed_at`; verify actual artifact `expires_at`. A labeled
bootstrap failure consumes `N` overhead but is not a valid sample. A failed or
canceled trusted sample consumes the single applicable slot and cannot be
replaced under this window.

The gate's value is fail-closed parity and candidate discovery. Only positive
docs and full evidence, the charged smoke, and separate FinOps approval may
authorize a later 5+5 evidence collection or any promotion. That later work
requires a new, independently reviewed budget/window; there is no automatic
continuation.

## Shadow rollout and promotion hold

1. Merge only this shadow workflow while existing required checks continue.
   Do not count any pre-merge run as controller-trust evidence. Only the exact
   merged revision can execute as trusted default-branch orchestration.
2. Before provisioning any label, run one `workflow_dispatch` full-safety smoke
   from the merged default-branch workflow. Charge it against `S`, and require
   successful stable contexts, aggregate `schemaVersion: 2`, exact revision
   attribution, request-budget evidence, both vulnerability JSON artifacts, and
   acceptable Jobs API duration. This is the only dispatch (`S=1`). A failed
   smoke, `F_i >= 4`, more than 32 rounded minutes, or insufficient headroom
   terminally disables the workflow; do not provision the label.
3. Establish the monitored no-automation precondition. Jett Reno is the named
   sample operator; record his exact GitHub login before collection, then have an
   authorized repository administrator store that login in the repository-owned
   Actions variable:

   ```powershell
   gh variable set SELECTIVE_CI_SAMPLE_OPERATOR --repo Jamula/Andreja --body '<LOGIN>'
   $expectedOperator = gh variable get SELECTIVE_CI_SAMPLE_OPERATOR `
     --repo Jamula/Andreja --json value --jq '.value'
   if ($expectedOperator -cne '<LOGIN>') { throw 'Sample operator configuration mismatch' }
   ```

   Do not provision or change this setting merely by editing the runbook. Treat
   an absent or mismatched variable as a fail-closed setup blocker. The value is
   an operator login, not a secret; repository settings, rather than pull-request
   code or event input, own it. Inventory
   repository Apps/workflows and confirm none can apply
   `ci:selective-shadow-sample`. At every 12-hour checkpoint and before each
   sample, query paginated issue events and require every application actor for
   that label to equal the recorded login. Any other actor terminally disables
   this window. The supported audit source is
   `GET /repos/Jamula/Andreja/issues/events`. Audit with paginated
   `GET /repos/Jamula/Andreja/issues/events` from the recorded UTC start bound.
   Use the recorded UTC start bound:

   ```powershell
   $pages = gh api --paginate --slurp `
     "repos/Jamula/Andreja/issues/events?per_page=100" |
     ConvertFrom-Json
   @($pages | ForEach-Object { $_ }) |
     Where-Object {
       $_.event -eq 'labeled' -and
       $_.label.name -eq 'ci:selective-shadow-sample' -and
       [datetime]$_.created_at -ge [datetime]'<START>'
     } |
     Select-Object created_at, @{Name='actor'; Expression={$_.actor.login}},
       @{Name='pull'; Expression={$_.issue.number}}
   ```

   The maintainer then provisions the sample label once:
   `gh label create ci:selective-shadow-sample --repo Jamula/Andreja --color 5319e7 --description "Maintainer-authorized bounded selective-CI sample"`.
   Confirm the repository role policy still prevents fork authors from applying
   labels.
4. Before collection starts, identify at most one naturally useful prose-only
   candidate and one naturally useful full-suite candidate. The prose candidate
   must satisfy the boundaries above and contain no changed inline code,
   fences, or indented commands. Do not create a synthetic/no-op PR. If no
   qualifying prose candidate appears before `N >= 20`, `N=25`, or another
   terminal condition, feasibility fails and promotion remains blocked.
5. On a quiescent head, first update each candidate branch so
   `pull_request.base.sha` contains the merged classifier. Because `synchronize`
   is not a collection trigger, that update does not run shadow CI. The named
   maintainer applies the label with
   `gh pr edit <number> --add-label ci:selective-shadow-sample`, waits for that
   `labeled` run to finish, and does not remove, reapply, or apply it elsewhere
   while a sample is in progress. Count it only if classification JSON has
   `trustedClassifierAvailable=true`, `shadowSample.sampled=true`,
   `shadowSample.observedActor` and `shadowSample.expectedOperator` equal the
   configured login, no bootstrap reason, and an acceptable API request-budget
   observation. Confirm an unrelated label event uses the ordinary topology
   concurrency group and cannot cancel the authorized sample group. The
   maintainer records Jobs/artifact API evidence,
   then immediately removes the label with
   `gh pr edit <number> --remove-label ci:selective-shadow-sample`.
   Branch updates, retained labels, and reopened PRs do not trigger collection.
6. Confirm unsampled PRs emit `shadow-not-sampled` and skipped domain jobs,
   sampled docs/full runs have correct dispositions, no fork gets write
   permission or secrets, and classification failure reports every domain
   `unavailable`. Treat a labeled bootstrap failure as invalid evidence that
   still consumes `N`; it does not create a replacement slot. After smoke, docs,
   and full finish, set `F` to their maximum independently rounded fixed
   overhead and publish the repriced result.
7. Do not enable merge queue for this collection. Complete the named
   **Merge Queue Activation and Merge-Group Evidence** prerequisite as a separate
   future ruleset decision with its own approved budget before collecting any
   merge-group evidence. That change restores the `merge_group`
   trigger and revalidates the retained classifier/workflow code; no current
   shadow context or cost is claimed for merge groups.
8. Obtain independent workflow correctness, security, and FinOps approvals and
   resolve every finding. Positive docs/full evidence does not authorize a 5+5
   collection or promotion by itself; separate FinOps approval and a new
   reviewed window are mandatory.
9. At any tripwire or feasibility end, terminally disable the workflow:
   `gh workflow disable selective-ci-shadow.yml`. There is no routine
   pause/re-enable path. Verify `gh workflow view selective-ci-shadow.yml --json
   state` reports `disabled_manually`, record the UTC shutdown time and last run
   ID, and confirm no later run. Remove the label from every PR and delete it
   only after audit evidence confirms no active sample:
   `gh label delete ci:selective-shadow-sample --repo Jamula/Andreja --yes`.
   Land a separately reviewed workflow-only follow-up removing
   `pull_request_target` and `workflow_dispatch` unless a new window has already
   been approved. Do not re-enable merely to preserve non-required shadow
   contexts. Any later run requires a new approved budget/window and a newly
   charged smoke.

Repository merge queue is not configured/available as of 2026-08-26: the
effective main rules have no merge-queue rule and the Actions API reports zero
`merge_group` runs. Enabling it changes the ruleset and requires the separately
reviewed **Merge Queue Activation and Merge-Group Evidence** prerequisite. This
collection sets `M=0`; no merge-group evidence may be fabricated or implied.
Removing the trigger prevents an administrator enabling the queue from silently
adding collection cost. The future prerequisite must define its own trigger,
canary count, cost ceiling, and ruleset decision.

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
PR check-run name and Actions integration ID from default-branch code. This
shadow makes no merge-group context claim. Only after the separate merge-queue
activation prerequisite restores the trigger may its live canary establish the
merge-group name/ID. In an ETag-guarded update, add the proven stable aggregate
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
future merge-group canary required after separate activation.

Issue #115's blocked external review-gate contract is not a dependency or a
completed control here. Issue #104's independent review-completion protections
must not be weakened; this rollout does not alter them.
