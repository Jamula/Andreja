# Fail-closed selective CI

Status: **shadow only**. The existing required workflows and repository ruleset
remain unchanged. Auto-merge remains disabled.

## Trust and classification

`.github/ci/change-policy.v1.json` is the versioned path policy and
`.github/ci/change-classifier.js` is its repository-owned implementation. The
always-triggered `Selective CI (Shadow)` workflow has no path filter and listens
for `pull_request`, `merge_group`, `push` to `main`, `schedule`, and
`workflow_dispatch`.

For a pull request, the classification job checks out only
`pull_request.base.sha` into `trusted-ci` and gets changed-file metadata from the
read-only, paginated REST endpoint. It never checks out or executes pull-request
code to classify it. Merge-group classification similarly executes
`merge_group.base_sha` code and compares the base and group head. The initial
rollout, metadata errors, unexpected events, missing policy, empty changes,
invalid paths, unclassified files, unknown statuses, renames, deletions, and the
300-file compare limit all select every domain.

Changed workflow, classifier, repository automation script, ruleset/security
policy, Squad governance, central package/build/project, SDK, generated-schema,
or executable documentation inputs select the full suite. Markdown is docs-only only when its
API patch is present and does not change a fenced executable snippet. Missing
patches fail closed. A deployed JavaScript or C# change also selects OCI because
it changes image content.

The only permissions are `contents: read` and `pull-requests: read`. Checkout
does not persist credentials. External actions and service images are immutable
SHA/digest pins. The classification job uploads the decision, including each
file's rule and reasons. The stable `Aggregate gate` always runs. Its JSON and
summary enumerate `passed`, `not-applicable`, `unavailable`, and `failed`
dispositions and fail if classification or any selected domain did not pass.

## Domains

| Domain | Selected work |
| --- | --- |
| Docs | Consistency fixtures/check, deterministic architecture render, PNG policy |
| .NET | Restore, Debug/Release build and unit/architecture tests, format, direct/transitive vulnerability scan, pinned DevSkim |
| PostgreSQL | Compile, format, PostgreSQL-project vulnerability scan, and runtime tests against a disposable pinned PostgreSQL service |
| PowerShell | Pinned PSScriptAnalyzer with findings as failure |
| JavaScript/browser harness | Syntax checks and repository-owned Node fixtures; the existing separately controlled evidence Compose profile remains the source of real-browser evidence |
| Docker/OCI | Pin policy and negative cases, two-build reproducibility, SBOM/IaC/vulnerability scan, hosted unsigned provenance verification, then destruction of private layers/reports |

Pushes to `main`, schedules, and manual dispatches always select all domains.
This is the drift/safety net even after pull requests become selective.

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

Every aggregate artifact records diagnostic step-wall-clock seconds for
classification, selected domains, and aggregate evaluation, plus rounded-minute
and cost lower bounds and aggregate JSON bytes. Step timers exclude queue,
runner provisioning, and PostgreSQL service startup. Depending on step order,
individual timers can include pre-timing cleanup/upload, but always exclude work
after their timing step and the aggregate job's final upload. Therefore
authoritative post-change comparisons must use completed-run Jobs API
`started_at`/`completed_at`, the same method as the baseline. Verify actual
artifact `expires_at` rather than assuming requested retention. Compare at least
five eligible docs-only shadow runs and five full shadow runs before promotion.
Cap duplicate-heavy shadow collection at 14 calendar days or those ten qualified
runs, whichever happens first; if blockers remain, keep the ruleset unchanged
and explicitly decide whether to pause the shadow workflow.

## Shadow rollout and promotion hold

1. Merge only this shadow workflow while existing required checks continue.
2. Collect live PR evidence for prose-only, C#, migration, workflow/classifier,
   and Docker changes. Confirm no fork pull request gets write permission or
   secrets and every skipped domain says `not-applicable`.
3. Collect a real `merge_group` run with the same stable aggregate context.
4. Obtain independent workflow correctness, security, and FinOps approvals and
   resolve every finding.
5. Only then prepare, review, and execute an atomic ruleset update.

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
3. Preserve deletion/non-fast-forward/linear-history rules, all pull-request
   parameters (especially thread resolution), code-quality/Copilot review,
   strictness, zero bypass, and every existing required context during the first
   additive change. Add the live aggregate context with its observed Actions
   integration ID; never guess it.
4. Send the complete update with `If-Match: <live-etag>`. Re-GET and compare
   every preserved field. On mismatch, restore the saved body with the new live
   ETag and verify again.
5. Retiring old always-heavy required contexts is a separate reviewed change
   only after the selective workflow itself becomes trusted default-branch code
   and equivalent live evidence is complete.

Issue #115's blocked external review-gate contract is not a dependency or a
completed control here. Issue #104's independent review-completion protections
must not be weakened; this rollout does not alter them.
