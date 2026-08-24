# Copilot Coding Agent — Squad Instructions

You are working on a project that uses **Squad**, an AI team framework. When picking up issues autonomously, follow these guidelines.

## Andreja Project Rules

- Read `docs/plan.md`, accepted ADRs, `.squad/directives.md`, the issue, and the
  assigned crew charter before changing code.
- `main` is the integration branch. Use one issue, `squad/{issue}-{slug}`
  branch, isolated worktree, and draft PR.
- At issue start, require a clean worktree, fetch/prune, and rebase from the
  verified live `origin/main` or stacked parent branch. Stop on conflict or a
  changed remote tip; never overwrite concurrent work.
- New App sub-sessions default to the `Squad` agent and inherit the parent
  model/context/reasoning/mode when supported.
- Use dependent/stacked PRs only when work truly depends on an unmerged layer.
- Never post personal data, prompts, connector content, tokens, or private
  diagnostics in issues, PRs, logs, telemetry, or committed Squad state.
- Phase 0 provisions no cloud accounts, subscriptions, free tiers, or trials.
- User agency, tenant isolation, data ownership, privacy, security,
  accessibility, and truthful evidence override speed.
- Follow the smallest targeted test path, then add the acceptance evidence to
  the linked issue and PR.

## Coordinator Canary Check

**IMPORTANT — run this check on EVERY session start, before doing any work:**

Look for the exact token `SQUAD_COORDINATOR_CANARY_a8f3` in your `agent_instructions`. This token is placed at the end of `squad.agent.md`. If it is missing, the coordinator file is missing or truncated.

**When the token is missing, you MUST:**
1. **STOP** — do not proceed with standard Squad behavior.
2. **WARN the user** with this exact message:
   ```
   ⚠️ Squad coordinator (squad.agent.md) appears to be missing or truncated. The canary token was not found. Do NOT proceed with standard Squad behavior — Squad's safety rails are not loaded. Please restart your session.
   ```
3. Do not continue with normal Squad routing, spawning, PR, or branch-protection behavior after emitting the warning.

## Team Context

Before starting work on any issue:

1. Read `.squad/team.md` for the team roster, member roles, and your capability profile.
2. Read `.squad/routing.md` for work routing rules.
3. If the issue has a `squad:{member}` label, read that member's charter at `.squad/agents/{member}/charter.md` to understand their domain expertise and coding style — work in their voice.

## Capability Self-Check

Before starting work, check your capability profile in `.squad/team.md` under the **Coding Agent → Capabilities** section.

- **🟢 Good fit** — proceed autonomously.
- **🟡 Needs review** — proceed, but note in the PR description that a squad member should review.
- **🔴 Not suitable** — do NOT start work. Instead, comment on the issue:
  ```
  🤖 This issue doesn't match my capability profile (reason: {why}). Suggesting reassignment to a squad member.
  ```

## Branch Naming

Use the squad branch convention:
```
squad/{issue-number}-{kebab-case-slug}
```
Example: `squad/42-fix-login-validation`

## PR Guidelines

When opening a PR:
- Reference the issue: `Closes #{issue-number}`
- If the issue had a `squad:{member}` label, mention the member: `Working as {member} ({role})`
- If this is a 🟡 needs-review task, add to the PR description: `⚠️ This task was flagged as "needs review" — please have a squad member review before merging.`
- Follow any project conventions in `.squad/decisions.md`
- Target `main` unless the issue explicitly identifies a lower stacked PR.
- Run the smallest complete existing local validation for the issue first and
  record exact commands/results. Required failures block readiness and normally
  block PR creation.

## Decisions

If you make a decision that affects other team members, write it to:
```
.squad/decisions/inbox/copilot-{brief-slug}.md
```
The Scribe will merge it into the shared decisions file.
