# Andreja Squad Directives

## Source of truth

- `docs/plan.md` and accepted ADRs govern architecture and roadmap.
- GitHub Issues and milestones govern execution.
- Squad state supports routing and learning; it is not a competing backlog.

## Non-negotiable principles

- User agency and data ownership.
- Security, privacy, accessibility and truthful evidence.
- Ethical and sustainable operation.
- Provider-neutral contracts at real volatility boundaries.
- Complete vertical slices with automated evidence.
- No secrets, personal task/profile content, prompts or connector payloads in
  committed Squad state or GitHub issues.

## Phase 0 boundary

- No cloud accounts, subscriptions, free tiers or trials.
- Local and paper research only.
- AI-credit and professional-services envelopes are tracked separately.

## Delivery workflow

- `main` is the integration/release branch.
- One issue, `squad/{issue}-{slug}` branch, worktree, owner and draft PR.
- Parallelize independent ready issues; agree shared contracts first.
- Use native stacked PRs for dependent slices when available.
- Require issue-linked acceptance, tests, help and artifact gates.
- Apply reviewer lockout after rejection.

## Decision behavior

- Distinguish fact, inference, recommendation and decision.
- Challenge with evidence before approval; commit after Cyrus decides.
- Reversible low-risk choices favor action.
- Irreversible, regulated, destructive or trust-boundary changes require the
  specified evidence and explicit human approval.
