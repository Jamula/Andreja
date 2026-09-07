# ADR 0011: Default-branch required gates

- **Status:** Proposed
- **Date:** 2026-09-07
- **Issue:** [#67](https://github.com/Jamula/Andreja/issues/67)
- **Decision owner:** Cyrus Jamula
- **Governing:** [Platform plan](../plan.md) and
  [default-branch operations](../operations/default-branch-protection.md)

## Context

Andreja integrates and releases from `main`. A repository ruleset already
requires pull requests, squash-only linear history, resolved review threads,
strict current-branch checks, and five .NET validation contexts. It has no
bypass actors.

The repository currently has one qualified human maintainer. Requiring one
human approval, code-owner approval, or approval of the last push would either
let the author approve their own change or deadlock every pull request. Neither
outcome is independent review. Merge queue is useful for a busy multi-author
branch, but activating it without a real `merge_group` exercise would risk
waiting forever for a context that a workflow does not report.

The repository is public. GitHub secret scanning is available without an
additional paid entitlement, and repository push protection can reject
supported secrets before they enter Git history.

## Decision

Protect the default branch with repository ruleset `21199927`,
`Default-Ruleset`, in active enforcement with no bypass actors. Preserve:

1. pull requests as the only normal path to `main`;
2. squash as the only merge method and linear history;
3. deletion and non-fast-forward protection;
4. resolution of every review thread;
5. strict required checks against the current `main`; and
6. the following GitHub Actions checks, bound to integration ID `15368`:
   - `Build and test (Debug)`;
   - `Build and test (Release)`;
   - `Format verification`;
   - `NuGet vulnerability audit`; and
   - `C# SAST (DevSkim)`.

Keep the required approval count at zero, code-owner review disabled, and
last-push approval disabled while only one qualified human maintainer exists.
Copilot review and code-quality annotations remain advisory. They do not count
as independent human approval.

Enable GitHub secret scanning and repository push protection. A push-protection
bypass is an exception requiring a recorded GitHub reason and immediate review
of the resulting alert; it is not a routine path around the control. Do not
read, copy, or publish a detected credential while investigating an alert.

Do not enable merge queue yet. The activation change must be a separate,
reviewed operation that:

1. confirms every required workflow reports the same required contexts for
   `merge_group`;
2. runs a real queue canary and records its constituent pull request, generated
   merge-group SHA, required-check results, and final disposition;
3. uses squash, build concurrency `1`, only merges non-failing pull requests,
   and starts with group size `1`; and
4. retains a rollback snapshot and removes the queue rule if any required
   context is absent or the queue cannot drain.

Add advisory `CODEOWNERS` routing only when ownership can be assigned by path
without implying independent approval. Enable required code-owner review and
approval of the most recent reviewable push only after at least two qualified
human maintainers can satisfy the rules without self-approval. At that point,
set at least one required approval, dismiss stale approvals or require a fresh
last-push approval, and prove both rejection and recovery with a pull request
canary.

## Procedural controls

Until merge queue is activated, the maintainer serializes merges:

1. merge only one pull request at a time;
2. update it against current `main`;
3. verify every required check on the current head SHA;
4. resolve all review threads;
5. confirm no other merge changed `main` after the checks completed; and
6. use GitHub squash merge or auto-merge, never a direct push.

Emergency remediation follows the same pull-request and required-check path.
If GitHub cannot enforce the rules, stop merges. Do not create a bypass actor
or weaken a gate merely to restore velocity. A ruleset rollback is allowed only
to the last captured known-good body and only after recording the outage,
operator, reason, and follow-up issue.

## Consequences

- A pull request with a failing required .NET gate cannot merge.
- The current one-human repository remains operable without fictitious review.
- Review independence becomes stronger automatically when the documented
  maintainer-count threshold is met; it is not silently claimed today.
- Secret detection and supported push blocking are active without new paid
  services or production resources.
- Merge throughput remains serialized until queue behavior is proven.
- The repository depends on GitHub-hosted enforcement. The runbook therefore
  requires live API inspection, ETag-aware rollback evidence, and periodic
  negative canaries rather than relying on repository text alone.

## Alternatives considered

- **Require one approval now:** rejected because the sole maintainer cannot
  provide independent approval and the rule would deadlock legitimate work.
- **Count Copilot review as approval:** rejected because advisory automation is
  not an accountable human approver.
- **Enable merge queue immediately:** rejected because no real `merge_group`
  run proves that all required contexts report and drain.
- **Rely only on documented merge procedure:** rejected because enforceable
  status checks and secret protection are available and materially reduce
  error.
- **Allow a standing administrator bypass:** rejected because it turns the
  default path into an unenforced convention and weakens negative-test evidence.
