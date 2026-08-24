# ADR 0000: Ratify the Andreja platform plan

- **Status:** Accepted
- **Date:** 2026-08-23
- **Approver:** Cyrus Jamula
- **Plan:** [`docs/plan.md`](../plan.md)
- **Plan SHA-256:** `9dfe3e8f81d140082270de0517ecc3f15d2a2090c5b6c530f83fe461ff8798c0`

## Decision

Ratify the architecture direction and bounded Phase 0 described in the plan.

Phase 0 permits local and paper research only. It has a $0 cloud-infrastructure
cap and prohibits cloud accounts, subscriptions, free tiers, and trials.
AI-credit and professional-services envelopes are governed separately.

Production choices remain gated by their ADRs, evidence, and explicit human
approval.

## Consequences

- `docs/plan.md` is the durable architecture and roadmap source of truth.
- Repository Issues and milestones become the execution source of truth.
- The documentation PR lands before the separate sanitized Squad scaffold PR.
- External contributions and repository visibility changes remain blocked by
  the licensing, IP, trademark, and governance decisions in Phase 0.
- Phase 1A protects the independent self-hosted MVP critical path.
- Phase 1B requires separate budget, legal, isolation, and SLO approval.

## Amendment policy

Every amendment records:

- The tracking issue and pull request.
- The new `docs/plan.md` SHA-256 hash.
- The approver and decision date.
- Whether the change is a logged amendment or re-ratification.

Changes to vision, data ownership, trust boundaries, the Phase 0 envelope,
public claims, legal posture, or non-negotiable architecture require explicit
re-ratification. Documentation CI must fail when this ADR's current hash does
not match the merged `docs/plan.md`.

## Amendments

### 2026-08-23 — organization repository migration

- **Tracking issue:** [#30](https://github.com/Jamula/Andreja/issues/30)
- **Pull request:** [#34](https://github.com/Jamula/Andreja/pull/34)
- **Plan SHA-256:** `9dfe3e8f81d140082270de0517ecc3f15d2a2090c5b6c530f83fe461ff8798c0`
- **Approver:** Cyrus Jamula
- **Decision:** Logged amendment; no architecture or ratification change.
- **Scope:** Update the canonical repository and organization Project references
  after transfer to `Jamula/Andreja`.
