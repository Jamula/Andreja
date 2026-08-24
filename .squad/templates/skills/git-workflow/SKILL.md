---
name: "git-workflow"
description: "Andreja protected-main, worktree, stacked-PR, and validation workflow"
domain: "version-control"
confidence: "high"
source: "team-decision"
---

# Andreja Git Workflow

## Integration model

- `main` is the integration and release branch.
- Never commit or push directly to `main`.
- One GitHub issue owns one `squad/{issue-number}-{slug}` branch, isolated
  worktree, accountable agent, and PR.
- Independent ready issues run in parallel worktrees.
- Dependent slices use ordinary dependent PRs and GitHub native stack metadata
  when the preview/API/entitlement supports it.

## Start-of-work preflight

Before creating or reusing an issue worktree:

1. Require a clean current/target worktree. Stop rather than move or overwrite
   somebody else's changes.
2. Validate the configured remote and run `git fetch --prune origin`.
3. Resolve and record the live base ref/SHA:
   - Independent work: `origin/main`.
   - Stacked work: the verified remote parent branch named by the issue.
4. Create a new worktree from that remote ref, or rebase a clean existing issue
   branch onto it.
5. On conflict, changed remote tip, missing parent, or force-with-lease failure,
   stop and restart preflight. Never force over concurrent work.

Example:

```powershell
git fetch --prune origin
git worktree add ..\Andreja-42 -b squad/42-fix-login origin/main
```

## Sub-session configuration

- In Copilot App, default new sub-sessions to the `Squad` custom agent.
- Preserve the parent model, context tier, reasoning effort, and mode when the
  child surface/model supports them.
- Diverge only for availability, explicit user direction, or a documented task
  need; report the fallback.
- The kickoff prompt still names the accountable crew specialist and includes
  their charter, issue, base ref/SHA, worktree path, and validation commands.

## Issue workflow

1. Add `status:in-progress` and the accountable `squad:{member}` label.
2. Create or reuse the isolated issue worktree after preflight.
3. Open a draft PR only after an initial coherent commit and required local
   validation.
4. Keep issue and PR updated with dependencies, evidence, risks, and blockers.
5. Merge through native protections/queue when available, or the documented
   reviewed-PR procedural fallback.
6. After merge, remove the worktree, prune metadata, delete the issue branch,
   and update the issue/milestone.

## Local validation before PR

Before creating or marking a PR ready:

- Run the smallest complete existing checks required by the issue: build, unit
  and integration tests, lint/format/type-check, docs/link/config validation,
  targeted E2E/scenario checks, and security/privacy evidence as applicable.
- Record exact commands and results in the issue and PR.
- A failing required check blocks PR creation/readiness unless Cyrus explicitly
  approves a draft blocker for an unavailable external dependency.
- Never describe untested work as ready to merge.

## Stacked PR rules

- Bottom PR targets `main`; each higher PR targets the branch immediately below.
- Create layers sequentially bottom-to-top after the lower branch is pushed.
- Register stack metadata only after every PR is open, same-repository, not
  queued/auto-merging, and has the exact verified base/head chain.
- Preserve stack identity after partial merges; append only above the verified
  top.
- Rebase/sync inside each layer's owning worktree. Use explicit
  `--force-with-lease` only after recording and revalidating the remote tip.
- If native stacks are unavailable, keep the ordinary dependent-PR chain.

## Multi-repository work

- Use separate sibling clones for separate repositories, not worktrees across
  repositories.
- Give each repository its own issue branch and PR.
- Link dependencies and merge dependency repositories first.
- Remove local package links/replacements before commit; CI must verify using
  published or PR-specific references.

## Anti-patterns

- Starting from stale local `main` or an unverified parent branch.
- Working in a dirty or somebody else's worktree.
- Multiple independent issues sharing one branch/PR.
- Opening a PR before running applicable local validation.
- Force-pushing without an explicit lease.
- Rebasing or pushing another active worktree's branch.
- Hiding test failures, dependency drift, or stacked-PR order.
- Leaving merged worktrees/branches behind.
