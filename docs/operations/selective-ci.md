# Fail-closed selective CI

Status: **shadow only**. The existing required workflows and repository ruleset
remain unchanged. Auto-merge remains disabled.

## Trust and classification

`.github/ci/change-policy.v1.json` is the versioned path policy and
`.github/ci/change-classifier.js` is its repository-owned implementation. The
`Selective CI (Shadow)` workflow has no path filter and listens only for
read-only `pull_request_target` `labeled` and `workflow_dispatch`. There is no
current `opened`, `push`, `schedule`, `merge_group`, `synchronize`, or `reopened`
trigger. The existing required workflows remain the `main` push safety net.

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
the full suite. Before an event can be a labeled sample, the classifier also
reads the live PR and requires `mergeable=true` plus exact 40-hex event/live
base, head, and `merge_commit_sha` identities. It reads that test merge through
the Git commit API and requires parent 0 to equal the live base and parent 1 to
equal the live head. It re-reads the live PR after file/compare acquisition and
again after trusted Markdown Contents reads. A null, malformed, stale, raced,
or parent-mismatched test merge records
`pull-request-merge-integrity-unavailable`, sets
`trustedClassifierAvailable=false`, and fails classification before any domain
job. Classification evidence includes the event/live identities, mergeability,
commit SHA, both parent SHAs, and verification reason. Domain jobs validate
only the proven event merge-commit SHA, never a moving PR ref.

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
merge-base blob. Contents reads use at most eight concurrent requests; the
absolute 132-request budget still applies across merge proof, pagination,
compare, and Contents calls.
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
sender login to equal that operator with exact canonical GitHub login casing
before sampling and records both
`observedActor` and `expectedOperator` in classification JSON. An unset variable
or differently cased/mismatched sender records `sample-operator-unconfigured` or
`sample-operator-mismatch`, keeps `sampled=false`, and schedules no domain job.
An operator-authorized sampled domain job receives only `contents: read`, checks out the immutable
`pull_request.merge_commit_sha` captured by the labeled event on a fresh runner,
and does not persist credentials. A missing merge SHA forces full classification
and an invalid checkout ref, so it cannot fall back to validating the base
branch. The job receives no secrets and uses no cache. Thus default-branch code
owns orchestration and classification, while validation of pull-request code is
confined to unprivileged jobs. External actions and service images are immutable
SHA/digest pins. The classification job uploads the decision, including each
file's rule and reasons. `Change classification` and `Aggregate gate` are emitted only for labeled and
manual feasibility events; ordinary PRs emit no shadow contexts. Stable
every-PR contexts are future promotion scope. For a triggered event,
`Aggregate gate` always runs. Its JSON and summary enumerate `passed`, `not-applicable`,
`unavailable`, and `failed` dispositions and fail if classification or any
selected domain did not pass.
If classification fails, every domain is `unavailable` rather than
`not-applicable`. An authorized sample uses the stable per-PR
`sample-pr-<number>` concurrency group with cancellation disabled, serializing
duplicate authorized events. Ordinary PR topology events use a separate
`topology-pull_request_target-<number>` group with cancellation enabled, so an
unrelated label cannot cancel a running authorized sample. That separation and
unsampled topology are **static workflow-expression and fixture assertions
only** during this two-slot window; never emit an unrelated live label event to
test them because it consumes `N` and terminally disables collection. GitHub
expression string equality is only a scheduling prefilter; the repository
classifier's exact-case comparison is authoritative, so a differently cased
login schedules no domain. Manual safety runs never cancel each other.
Aggregate revision evidence records the event base and
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
non-sample labeled event, if one unexpectedly occurs, produces
`shadow-not-sampled` evidence with no heavy domain work, consumes `N`, and
terminally disables this window.

Trusted classification enforces an absolute 132-request metadata budget.
A labeled sample uses four merge-integrity requests (initial live PR, Git
commit parents, post-snapshot live PR, and post-Contents live PR), plus PR-file
pages, one compare request, and up to 100 Markdown base-content requests.
Combinations that cannot fit fail closed before the 132nd request rather than
relaxing the cap. Classification JSON records the limit, requests used, exhaustion,
minimum observed `X-RateLimit-Remaining`, and reset epoch. Evidence verification
requires `exhausted=false`, `used<=132`, and enough observed remaining budget for
the same path to finish. HTTP 429, or HTTP 403 with zero remaining, becomes an
explicit metadata rate-limit failure and therefore a fail-closed red aggregate.
Any other PR, commit, compare, file-page, or Markdown Contents HTTP failure also
records unavailable metadata, invalidates labeled-sample merge proof, and
prevents every domain job; it is never downgraded to a full-suite sample.
That red result is an environmental/request-budget outcome, not automatically a
classifier logic defect; preserve the artifact/headers, wait for reset, and
re-run only under the fixed slot protocol.
Preflight observation on 2026-08-26 was core `14987/15000` remaining and search
`30/30`; this is a point-in-time capacity observation, not a guaranteed budget
for later runs.

Only labeled `pull_request_target` events emit `Change classification` and
`Aggregate gate`; opening, synchronizing, or reopening does not run this shadow
workflow during feasibility. Domain jobs run only for a `labeled` event that applies
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
context. The dispatch smoke and existing always-heavy required workflows cover dependency
drift, but this is a
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
domain minutes from run 32924008713 at `fab046f6608fc93b032ed7e618b57f2547c88bdc`.
Those domain timings predate the trusted-classifier gate and are historical
planning inputs only, not trusted sample evidence. After changed inline code
spans were classified fail closed, the model estimates 320 rounded minutes across the sample:
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
for aggregation. That bootstrap is not trusted-classifier pricing. To cover
measurement uncertainty, this feasibility window uses pre-approved `F=3` as a
budget ceiling. The performance target remains modeled `F=2`: one rounded
classifier minute, one rounded aggregate minute, and one docs minute produce
the 3-minute / USD 0.024 / 81.25%-saving docs-only target.

This window is a **small feasibility gate**, not a savings collection:

- `N <= 2` counts all labeled events, including lightweight non-sample label
  events. Both slots are reserved for the intended docs and full samples.
- `S=1` is the single full-safety `workflow_dispatch` smoke.
- At most one pre-identified, naturally useful prose-only PR and one full-suite
  PR may receive the sample label.
- No synthetic or no-op change may be created solely to obtain evidence.

The prose candidate must be outside every known executable, generated-schema,
workflow, build, package, SDK, migration, security-policy, evidence-script,
Docker, and Compose boundary. Its changed Markdown must contain no code spans,
fences, or indented commands. If no qualifying prose candidate exists before a
cap or tripwire, feasibility fails and promotion remains blocked.

At pre-approved `F=3`, planned use is
`F*N + 15 + (F+14)*S = 38 minutes / USD 0.304`. The fail-closed ceiling is
`F*N + 28 + (F+14)*S = 51 minutes / USD 0.408`. Do not start without 25%
headroom: **64 minutes / USD 0.512**. The 15 planned domain minutes are one
docs minute plus fourteen full-suite domain minutes; fixed overhead for both PR
samples is already included in `N`. The 28-minute ceiling assumes both labeled
PRs select all domains.

Budget acceptance is not savings-target acceptance. A 4-minute total docs run
can pass feasibility parity and remain below the 6-minute duration tripwire,
but it misses the modeled 3-minute / 81.25% savings target. Such evidence does
not authorize promotion. Separate owner and FinOps approval may authorize only
a new evidence window to establish the target; it cannot waive the target or
directly authorize promotion.

For the smoke, docs sample, and full sample, compute
`F_i = ceil(classification seconds/60) + ceil(aggregate seconds/60)` from the
completed Jobs API records. Record the **maximum of the smoke, docs, and full
trusted runs**. Any observed `F_i >= 4`, a recomputed ceiling above the approved
64-minute headroom, exhausted API or
runner headroom, or an unavailable required Jobs/artifact observation
terminally disables this workflow and requires a separately approved new
window. There is no routine pause/re-enable path in this feasibility window.

Workflow job timeouts sum to **200 job-minutes for one full run**: classifier
10, docs 15, .NET 45, PostgreSQL 30, PowerShell 15, JavaScript 15, OCI 60, and
aggregate 10. The whole-window timeout-derived bound is
`20*N + 180*2 + 200*S = 600 job-minutes / USD 4.800`. This is a fail-safe
exposure bound, not approved spend. Terminally disable if completed Jobs API
rounded time for the docs sample exceeds 6 minutes or the smoke or full sample
exceeds 32 minutes (twice modeled duration).

There is no cadence-based run monitoring. Immediately before each of the two
serialized sample-label applications, query Actions and issue events from the
recorded UTC window start. Require zero prior labeled runs before the docs label
and exactly one before the full label. Any unexpected label event or actor
terminally disables the workflow. Do not claim repository automation is unable
to label PRs: `.github/workflows/issue-status.yml` is a known residual because a
PR body with a closing reference to a PR number can cause automated PR status
labels. Before the window and again before each sample, audit every open PR body
and its resolved closing references, plus every open PR's current labels. Any
PR-to-PR closing reference, automation-applied label, or non-sample label event
after `<START>` terminally disables the workflow.

```powershell
$pages = gh api --paginate --slurp `
  "repos/Jamula/Andreja/actions/workflows/selective-ci-shadow.yml/runs?event=pull_request_target&created=<START>..<END>&per_page=100" |
  ConvertFrom-Json
$runs = @($pages | ForEach-Object { $_.workflow_runs })
$runs.Count
```

`N=2` is the hard cap. The same terminal action applies after the one dispatch,
the two labeled samples, any unexpected label event/actor, tripwire, exhausted
headroom, or failure to find the prose candidate before the first intended
label. Use
`gh workflow disable selective-ci-shadow.yml`, record the UTC shutdown time and
last run ID, and verify that no later run was created. Re-enable is not allowed
under this window; a new approved budget/window must start with its own charged
smoke.

Live PR run 32924008713 at
`fab046f6608fc93b032ed7e618b57f2547c88bdc` remains historical
pre-trusted-classifier-gate domain timing/artifact evidence only. Run 32931733394
remains prior unsampled topology evidence only. After merge, record the exact
squash-merged controller SHA. It is the only trusted controller revision, and
the first post-merge dispatch smoke must record it as its validated revision.

Every aggregate artifact records diagnostic timings, rounded-minute/cost lower
bounds, exact serialized JSON bytes, API request-budget observations, revisions,
and domain dispositions. Authoritative pricing uses Jobs API
`started_at`/`completed_at`; verify actual artifact `expires_at`. A labeled
bootstrap failure consumes `N` overhead but is not a valid sample. A failed or
canceled trusted sample consumes the applicable labeled-event slot and cannot
be replaced under this window.

The gate's value is fail-closed parity and candidate discovery. Only positive
docs and full evidence, the charged smoke, and separate FinOps approval may
authorize a later 5+5 evidence collection or any promotion. That later work
requires a new, independently reviewed budget/window; there is no automatic
continuation.

## Shadow rollout and promotion hold

1. Merge only this shadow workflow while existing required checks continue.
   Before merge, inventory repository Apps/workflows that can apply PR labels
   and audit every open PR body, closing reference, and current label. Explicitly
   include the `issue-status.yml` PR-to-PR closing-reference residual; do not
   assert that automation cannot label PRs. Keep the audited no-unexpected-label
   precondition in force through terminal shutdown. Do not count any pre-merge run as controller-trust
   evidence. Immediately after merge, read the PR's authoritative `merged_at`
   timestamp and exact squash-merged controller SHA. Record `merged_at` as
   `<START>` before any workflow action; every labeled event and labeled
   workflow run at or after `<START>` counts against `N`, including any run
   between merge and the dispatch smoke. The squash-merged SHA is the only
   trusted controller revision. Any unexpected label event or actor after
   `<START>` terminally disables the workflow before the smoke.
2. Before provisioning any label, run one `workflow_dispatch` full-safety smoke
   from the merged default-branch workflow. Immediately before dispatch, run
   the paginated Actions-run and repository issue-event audits documented in
   this section from `<START>` through the current UTC time. Require zero
   labeled workflow runs and zero label events; any result terminally disables
   the workflow, and the smoke must not run. Charge it against `S`, and require
   successful stable contexts, aggregate `schemaVersion: 2`, exact revision
   attribution to that squash-merged SHA, request-budget evidence, both
   vulnerability JSON artifacts, and acceptable Jobs API duration. This is the
   only dispatch (`S=1`). A failed
   smoke, `F_i >= 4`, more than 32 rounded minutes, or insufficient headroom
   terminally disables the workflow; do not provision the label.
3. Reverify the monitored no-unexpected-label precondition. Jett Reno is the named
   sample operator; record his exact GitHub login before collection, then have an
   authorized repository administrator store that exact canonical-cased login
   in the repository-owned
   Actions variable:

   ```powershell
   gh variable set SELECTIVE_CI_SAMPLE_OPERATOR --repo Jamula/Andreja --body '<LOGIN>'
   $expectedOperator = gh variable get SELECTIVE_CI_SAMPLE_OPERATOR `
     --repo Jamula/Andreja --json value --jq '.value'
   if ($expectedOperator -cne '<LOGIN>') { throw 'Sample operator configuration mismatch' }
   ```

   Do not provision or change this setting merely by editing the runbook. Treat
   an absent, differently cased, or mismatched variable as a fail-closed setup blocker. The value is
   an operator login, not a secret; repository settings, rather than pull-request
   code or event input, own it. GitHub expression equality is a prefilter; the
   classifier's exact-case equality is authoritative and is covered by fixtures.
   Inventory label-capable repository Apps/workflows, including
   `issue-status.yml`. Immediately before each sample, query paginated
   issue events from the recorded UTC start and require no unexpected label
   event or actor. Any unexpected event terminally disables this window. The
   supported audit source is `GET /repos/Jamula/Andreja/issues/events`:

   ```powershell
   $pages = gh api --paginate --slurp `
     "repos/Jamula/Andreja/issues/events?per_page=100" |
     ConvertFrom-Json
   @($pages | ForEach-Object { $_ }) |
     Where-Object {
       $_.event -eq 'labeled' -and
       [datetime]$_.created_at -ge [datetime]'<START>'
     } |
     Select-Object created_at, @{Name='actor'; Expression={$_.actor.login}},
       @{Name='pull'; Expression={$_.issue.number}}
   ```

   Also paginate all open PRs before the window and before each sample. Record
   every body's closing references and the exact label names/actors. Resolve
   each referenced number through the Issues API; a response containing
   `pull_request` proves the body closes a PR rather than an issue and is a
   terminal blocker:

   ```powershell
   $pullPages = gh api --paginate --slurp `
     "repos/Jamula/Andreja/pulls?state=open&per_page=100" |
     ConvertFrom-Json
   $openPulls = @($pullPages | ForEach-Object { $_ })
   $closingLine = [regex]'(?im)^.*\b(?:close[sd]?|fix(?:e[sd])?|resolve[sd]?)\b.*$'
   $reference = [regex]'(?:Jamula/Andreja)?#(?<number>\d+)'
   foreach ($pull in $openPulls) {
     [pscustomobject]@{
       pull = $pull.number
       labels = @($pull.labels | ForEach-Object name)
     }
     foreach ($line in $closingLine.Matches([string]$pull.body)) {
       foreach ($match in $reference.Matches($line.Value)) {
         $target = gh api "repos/Jamula/Andreja/issues/$($match.Groups['number'].Value)" |
           ConvertFrom-Json
         if ($null -ne $target.pull_request) {
           throw "PR #$($pull.number) has a PR-to-PR closing reference"
         }
       }
     }
   }
   ```

   Preserve the pre-window label inventory and compare it with paginated issue
   events before each sample. Any label added by automation, or any non-sample
   label event after `<START>`, consumes a slot and terminally disables.

   The maintainer then provisions the sample label once:
   `gh label create ci:selective-shadow-sample --repo Jamula/Andreja --color 5319e7 --description "Maintainer-authorized bounded selective-CI sample"`.
   Confirm the repository role policy still prevents fork authors from applying
   labels.
4. Before collection starts, identify at most one naturally useful prose-only
   candidate and one naturally useful full-suite candidate. The prose candidate
   must satisfy the boundaries above and contain no changed inline code,
   fences, or indented commands. Do not create a synthetic/no-op PR. Before
   applying either label, locally classify each candidate with the exact merged
   classifier against current read-only PR metadata; record the squash-merged
   classifier SHA, policy digest, candidate head/base, and decision. If either
   decision is not the expected docs-only/full result, or no qualifying prose
   candidate is available, feasibility fails and promotion remains blocked.
5. On a quiescent head, first update each candidate branch so
   `pull_request.base.sha` contains the merged classifier. Because `synchronize`
   is not a collection trigger, that update does not run shadow CI. Before
   applying the label, poll the live PR a bounded six times (ten seconds apart)
   until `mergeable` is non-null/true and `merge_commit_sha` is exact 40-hex.
   Read that SHA through `GET /git/commits/{sha}`; require exactly the recorded
   live base/head as parent 0/parent 1 and record the PR snapshot plus parent
   proof. A null, false, stale, malformed, or mismatched result terminally
   disables without labeling:

   ```powershell
   $pr = $null
   foreach ($attempt in 1..6) {
     $pr = gh api "repos/Jamula/Andreja/pulls/<NUMBER>" | ConvertFrom-Json
     if ($pr.mergeable -eq $true -and
         [regex]::IsMatch([string]$pr.merge_commit_sha, '^[0-9a-f]{40}$')) { break }
     if ($attempt -lt 6) { Start-Sleep -Seconds 10 }
   }
   if ($pr.mergeable -ne $true -or
       -not [regex]::IsMatch([string]$pr.merge_commit_sha, '^[0-9a-f]{40}$')) {
     throw 'Current test merge unavailable'
   }
   $commit = gh api "repos/Jamula/Andreja/git/commits/$($pr.merge_commit_sha)" |
     ConvertFrom-Json
   if (@($commit.parents).Count -lt 2 -or
       $commit.parents[0].sha -cne $pr.base.sha -or
       $commit.parents[1].sha -cne $pr.head.sha) {
     throw 'Current test merge parent proof mismatch'
   }
   [pscustomobject]@{
     base = $pr.base.sha
     head = $pr.head.sha
     merge = $pr.merge_commit_sha
     parents = @($commit.parents | ForEach-Object sha)
   } | ConvertTo-Json -Depth 4
   ```

   The named maintainer then applies the label with
   `gh pr edit <number> --add-label ci:selective-shadow-sample`, waits for that
   `labeled` run to finish, and does not remove, reapply, or apply it elsewhere
   while a sample is in progress. Count it only if classification JSON has
   `trustedClassifierAvailable=true`, `shadowSample.sampled=true`,
   `shadowSample.observedActor` and `shadowSample.expectedOperator` equal the
   configured login, no bootstrap reason, and an acceptable API request-budget
   observation. Require `mergeCommitProof.verified=true` and exact equality with
   the pre-label base/head/merge/parent proof. Verify unrelated-label concurrency
   and unsampled topology only from the static workflow-expression/fixture
   tests; never emit a live unrelated label to test them. The
   maintainer records Jobs/artifact API evidence,
   then immediately removes the label with
   `gh pr edit <number> --remove-label ci:selective-shadow-sample`.
   Branch updates, retained labels, opened PRs, and reopened PRs do not trigger
   collection. A non-sample label event may emit lightweight
   `shadow-not-sampled` contexts but consumes one of the two `N` slots; because
   no replacement is allowed, treat it as terminal.
6. Confirm from static fixtures that a non-sample labeled event would emit
   `shadow-not-sampled` with no heavy domain work; do not create such an event.
   Confirm sampled docs/full runs have correct dispositions, no fork gets write
   permission or secrets, and classification failure reports every domain
   `unavailable`. Treat a labeled bootstrap failure as invalid evidence that
   still consumes `N`; it does not create a replacement slot. After smoke, docs,
   and full finish, publish their maximum independently rounded fixed overhead.
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
