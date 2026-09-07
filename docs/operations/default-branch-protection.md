# Default-branch protection

This runbook implements the proposed
[ADR 0011 default-branch policy](../adr/0011-default-branch-required-gates.md).
It is the operator procedure for inspecting, changing, testing, and recovering
the GitHub controls on `main`.

## Current enforced baseline

As observed on 2026-09-07, repository ruleset `21199927`,
`Default-Ruleset`, targets `~DEFAULT_BRANCH`, is active, and has no bypass
actors. Its observed weak ETag was:

```text
W/"d1c64ab7a588aba79381c85ebf59734037b203fb9b80e3edbcde5fd53e635961"
```

The ruleset requires pull requests, resolved review threads, squash-only linear
history, strict current-branch status checks, and the five contexts listed in
ADR 0011. It blocks deletion and non-fast-forward updates. Required human,
code-owner, and last-push approvals remain disabled for the documented
one-human operating state.

Secret scanning and repository push protection report `enabled`. Dependabot
security updates remain enabled. This record contains configuration state only;
it does not read or reproduce secret-scanning alerts.

The ETag and feature state are observations, not constants. Always read the
live values before making a decision.

## Inspect without mutation

```powershell
$repo = "Jamula/Andreja"
$rulesetId = 21199927

gh api -i "repos/$repo/rulesets/$rulesetId"
gh api "repos/$repo/branches/main"
gh api "repos/$repo/rules/branches/main"
gh api "repos/$repo" --jq "{
  default_branch,
  visibility,
  allow_squash_merge,
  allow_merge_commit,
  allow_rebase_merge,
  allow_auto_merge,
  security_and_analysis
}"
```

Confirm all of the following before merging:

- `main` is the default branch and reports `protected: true`;
- ruleset enforcement is `active`;
- `bypass_actors` is empty and `current_user_can_bypass` is `never`;
- the target condition includes only `~DEFAULT_BRANCH`;
- the pull-request rule requires thread resolution and allows only `squash`;
- strict required status checks are enabled;
- every required context has integration ID `15368`;
- secret scanning and push protection are enabled; and
- the pull request head SHA is the SHA on which every required check completed.

GitHub's legacy branch-protection endpoint may report no branch-protection rule
because this repository uses a ruleset. Use the repository rules and effective
branch-rules endpoints above rather than interpreting that response as an
unprotected branch.

## Serialized merge procedure

Merge one pull request at a time while merge queue is disabled:

1. Pause other merges.
2. Update the pull request with current `main`.
3. Wait for every required context on the current head SHA.
4. Resolve every review thread.
5. Re-read the base SHA immediately before merge. If it changed, update and
   rerun checks.
6. Squash merge through GitHub. Never push directly to `main`.
7. Confirm the resulting `main` push runs the required workflows.

Auto-merge may perform step 6 only after GitHub reports every required rule
satisfied. Auto-merge is not a merge queue and does not prove `merge_group`
coverage.

## Negative canary

At policy creation and after a material ruleset or required-workflow change,
open a short-lived draft pull request that intentionally introduces a harmless
C# compile failure outside generated or vendored content.

1. Record the canary branch, pull request, head SHA, and expected failing
   context.
2. Wait for the selected required context to conclude `failure`.
3. Confirm GitHub reports the pull request as blocked and does not offer a
   normal merge path.
4. Revert the intentional failure on the same branch.
5. Confirm the new head SHA receives all five required contexts and becomes
   eligible under the ruleset.
6. Close the canary without merging and delete its branch, or retain only the
   clean policy change if the policy pull request itself was used as the
   canary.

Never use a credential-shaped value for a branch-rules canary. Push-protection
testing needs a separately approved synthetic-pattern procedure because a
provider token can be live, reported to its issuer, or copied into audit data.

### 2026-09-07 enforcement evidence

Pull request
[#170](https://github.com/Jamula/Andreja/pull/170) used head
`fa55e5b863a532242be9937e9d6548187a9dc366` to add an intentional C# syntax
error. Required workflow run
[`34157536408`](https://github.com/Jamula/Andreja/actions/runs/34157536408)
reported:

- `Build and test (Debug)`: failure;
- `Build and test (Release)`: failure;
- `Format verification`: success;
- `NuGet vulnerability audit`: success; and
- `C# SAST (DevSkim)`: success.

With the pull request ready for review, GitHub reported
`mergeStateStatus: BLOCKED` while the Git commit graph itself remained
`mergeable: MERGEABLE`. That distinction proves ruleset enforcement, rather
than a merge conflict or draft state, blocked the pull request. The intentional
failure was then removed on the same branch. Required workflow run
[`34157956090`](https://github.com/Jamula/Andreja/actions/runs/34157956090)
on the resulting clean head `ed07de9db7323a1bfe94bf84e7568b28a86fac73`
confirmed recovery: `Build and test (Debug)`, `Build and test (Release)`,
`Format verification`, `NuGet vulnerability audit`, and `C# SAST (DevSkim)`
all reported success, so the negative canary demonstrates both rejection and
recovery as required by step 5 above.

## Safe ruleset change

Before a ruleset write:

1. Save the complete live response headers and body outside the repository.
2. Record the ETag, ruleset ID, update time, operator, and reason.
3. Build the candidate from the full live body. Preserve every rule and
   parameter not explicitly approved for change.
4. Abort if a second read has a different ETag or body.
5. Apply the smallest approved change with the write request itself carrying
   `If-Match: <live-etag>`. A re-read before the write does not close the race:
   another operator could change the ruleset between the read and the write,
   and an unconditional write would silently overwrite that change. If the
   write is rejected because the ETag no longer matches, restart from step 1.
6. Re-read the ruleset and effective branch rules, and confirm the new ETag.
7. Run the negative canary.

Do not add a bypass actor, reduce required contexts, disable strict checking,
or lower secret protection as an incident workaround.

## Merge-queue activation gate

GitHub only creates a `merge_group` and emits `checks_requested` after a pull
request is actually added to an enabled merge queue. The precondition below
cannot be exercised before the rule exists, so treat activation itself as part
of the canary, with an immediate rollback path:

1. Capture and save the complete pre-change ruleset body and ETag outside the
   repository (see the safe ruleset change procedure above).
2. Add the `merge_queue` rule with squash, build concurrency `1`, group size
   `1`, and only non-failing pull requests eligible.
3. Enqueue one real pull request and record its generated merge-group SHA.
4. Verify all five required contexts are actually emitted and pass for that
   merge-group SHA, and that the queue drains to merge.
5. If any context is missing, times out, or the queue cannot drain, restore
   the exact saved pre-change ruleset body immediately and continue serialized
   merges.

Do not infer queue readiness from a workflow file containing a `merge_group`
trigger. Only a real queue run proves event delivery, context identity, and
ruleset compatibility. Do not leave the `merge_queue` rule active after a
failed canary while investigating; roll back first, then investigate.

## Human-review activation gate

When a second qualified human maintainer is active:

1. add path-based `CODEOWNERS` entries with at least two qualified, active
   owners (or a team containing both) for every protected path. One owner per
   path can deadlock that owner's own pull requests, because self-review
   cannot satisfy required code-owner review and no other owner exists to
   approve it;
2. require one human approval and code-owner review;
3. require fresh review after the latest reviewable push, or dismiss stale
   approvals;
4. retain required thread resolution; and
5. run a canary proving that self-review, stale approval, and unresolved
   threads block merge while a qualified independent approval recovers it.

Until then, zero required approvals is an explicit availability tradeoff, not
a claim of independent review.

## Recovery and stop conditions

Stop all merges if the ruleset is absent, inactive, bypassable, missing a
required context, or inconsistent with effective branch rules. Open an incident
issue and preserve the live responses.

If a newly added rule deadlocks remediation, restore only the last captured
known-good full ruleset body. Record the rollback, reason, timestamps, actor,
before/after ETags, and canary result. Do not partially reconstruct a ruleset
from memory or this document.

## References

- [Available rules for rulesets](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/available-rules-for-rulesets)
- [Managing a merge queue](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/configuring-pull-request-merges/managing-a-merge-queue)
- [Secret scanning](https://docs.github.com/en/code-security/concepts/secret-security/secret-scanning)
- [Push protection](https://docs.github.com/en/code-security/concepts/secret-security/push-protection)
