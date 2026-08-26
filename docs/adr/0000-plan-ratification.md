# ADR 0000: Ratify the Andreja platform plan

- **Status:** Accepted
- **Date:** 2026-08-23
- **Approver:** Cyrus Jamula
- **Plan:** [`docs/plan.md`](../plan.md)
- **Plan SHA-256:** `2adf7fc7b6fb2da57b13c3b3fd02f14041bff04f468dd0edfa302ca5b9f3bb3f`

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

### 2026-08-24 — public website execution tracking

- **Tracking issues:** [#94](https://github.com/Jamula/Andreja/issues/94) and
  [#93](https://github.com/Jamula/Andreja/issues/93)
- **Pull request:** [#95](https://github.com/Jamula/Andreja/pull/95)
- **Plan SHA-256:** `69133d886866d97474073814572a95c16031995c0eb3bf297b793591ae924a3b`
- **Approver:** Cyrus Jamula
- **Decision:** Logged amendment; no architecture, phase, budget, publication, or
  ratification change.
- **Scope:** Add explicit issue tracking for the private Phase 0 website
  design/hosting matrix and the separately gated Phase 1B public/help site.

### 2026-08-25 — Phase 0 artifact and execution status reconciliation

- **Tracking issue:** [#73](https://github.com/Jamula/Andreja/issues/73);
  containment evidence [#114](https://github.com/Jamula/Andreja/issues/114)
- **Pull request:** [#113](https://github.com/Jamula/Andreja/pull/113)
- **Plan SHA-256:** `bfc0c26c1ecb34e00fb91971fe2b11fa0eb484268a9aff276657a41bcba34f83`
- **Approver:** Cyrus Jamula
- **Approver status:** Explicit Cyrus direction recorded for this amendment on
  2026-08-25; merge is execution evidence and does not substitute for approval.
- **Classification:** Logged execution/status amendment; no re-ratification.
- **Scope:** Reconcile implemented evidence with still-Proposed ADRs; record the
  conservative provisional Phase 1A BYOK/fake versus Phase 1B Copilot mapping
  pending open decision #74; record Phase 1A evidence
  gaps, MSTest direction/deferred migration, required privacy/threat artifacts,
  historical website nonconformance and completed containment, deferred work,
  and safe execution order.
- **Authority unchanged:** No Proposed ADR is accepted, no Phase 1A exit is
  claimed, and no spend, publication, public launch, real Copilot, managed cloud,
  federation, or connector activation is authorized.

### 2026-08-25 — issue lifecycle status-artifact hash refresh

- **Tracking issue:** [#70](https://github.com/Jamula/Andreja/issues/70)
- **Pull request:** [#106](https://github.com/Jamula/Andreja/pull/106)
- **Plan SHA-256:** `d0d2400b0f617b8a752bf155b7698cd645fc1263b99cce9813dc5d42977d5afc`
- **Approver:** Cyrus Jamula
- **Approver status:** Cyrus directed this plan review/update on 2026-08-25 and
  kept merge through the protected pull request as final acceptance. This entry
  does not claim a separate decision before that merge.
- **Classification:** Logged mechanical/editorial amendment; no
  re-ratification.
- **Scope:** Record the operating-model issue-lifecycle reconciliation wording
  and its matching current status-artifact hash. No architecture, provider,
  publication, or exit-gate change.

### 2026-08-25 — canonical privacy and threat-model status

- **Tracking issue:** [#116](https://github.com/Jamula/Andreja/issues/116)
- **Pull request:** [#117](https://github.com/Jamula/Andreja/pull/117)
- **Plan SHA-256:** `2adf7fc7b6fb2da57b13c3b3fd02f14041bff04f468dd0edfa302ca5b9f3bb3f`
- **Approver:** Cyrus Jamula
- **Approver status:** Cyrus directed creation and review of the canonical
  artifacts on 2026-08-25 and kept merge through the protected pull request as
  final acceptance of this status amendment, not ratification of the artifacts.
  This entry does not claim a separate pre-merge decision.
- **Classification:** Logged documentation/status amendment; no re-ratification.
- **Scope:** Record that `docs/privacy.md` and `docs/threat-model.md` now provide
  canonical descriptive baselines but are not ratified. Tuvok/Deanna Troi/Rai
  challenge, Cyrus residual-risk acceptance, classification/impact assessment,
  numeric retention, residency, production export/purge, model-provider choices,
  and evidence exits remain open. Also record #106 as completed at `7a1fc20`, the
  #104 auto-merge race as contained only by disabled repository auto-merge, not as
  full completion. Issue #104 remains open; merged PR #115 (`c93c6be`) contributed
  human-blocked design evidence only, and no external GitHub App/worker or
  always-present review-completion gate exists or is authorized. Record #102 as
  the next safe engineering lane only with stable, always-present fail-closed
  aggregate checks that neither depend on nor weaken #115. No Proposed ADR is
  accepted and no production, provider, connector, federation, managed-hosting,
  public-site, or support-intake activation is authorized.
