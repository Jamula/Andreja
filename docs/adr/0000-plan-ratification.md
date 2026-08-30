# ADR 0000: Ratify the Andreja platform plan

- **Status:** Accepted
- **Date:** 2026-08-23
- **Approver:** Cyrus Jamula
- **Plan:** [`docs/plan.md`](../plan.md)
- **Accepted Plan SHA-256:** `2adf7fc7b6fb2da57b13c3b3fd02f14041bff04f468dd0edfa302ca5b9f3bb3f`
- **Current proposed Plan SHA-256:** `3c825b3a9f5d1e5327b1141a04a0516d7e2cd90facf4f6a5d0fcd0290b01c4ec`
- **Accepted plan content:** [PR #117](https://github.com/Jamula/Andreja/pull/117)
  at merge commit
  [`2e35d4da59b6b1c660b596dee527ec9eba2a4dda`](https://github.com/Jamula/Andreja/commit/2e35d4da59b6b1c660b596dee527ec9eba2a4dda)

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

The metadata uses a two-hash model while a proposal is pending:

- **Accepted Plan SHA-256** identifies the last accepted `docs/plan.md` content
  and must match the latest accepted hashed amendment. Its durable content pointer
  is the protected merged PR/commit recorded above.
- **Current proposed Plan SHA-256** identifies the current working proposal and
  must match the latest pending Proposed hashed amendment. It is not accepted
  content and does not replace the accepted pointer.
- With no pending proposal, one **Current Plan SHA-256** (or the historical
  **Plan SHA-256** legacy spelling) identifies the latest accepted hashed
  amendment.

Every amendment records the tracking issue and pull request, the new
`docs/plan.md` SHA-256 hash, approver and decision date, classification, and
whether the change is a logged amendment or re-ratification. Hashed amendment
records use this required grammar:

- Accepted: `- **Approver:** <name>` without `pending`, plus
  `- **Classification:** <accepted classification>` that does not begin
  `Proposed`.
- Pending proposal: `- **Approver:** <name>; **pending**`, plus
  `- **Classification:** Proposed <classification>`.

Documentation CI selects the latest record of each class rather than assuming
the final amendment section is the pending proposal.

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
- **Classification:** Logged repository-migration amendment; no re-ratification.
- **Decision:** Logged amendment; no architecture or ratification change.
- **Scope:** Update the canonical repository and organization Project references
  after transfer to `Jamula/Andreja`.

### 2026-08-24 — public website execution tracking

- **Tracking issues:** [#94](https://github.com/Jamula/Andreja/issues/94) and
  [#93](https://github.com/Jamula/Andreja/issues/93)
- **Pull request:** [#95](https://github.com/Jamula/Andreja/pull/95)
- **Plan SHA-256:** `69133d886866d97474073814572a95c16031995c0eb3bf297b793591ae924a3b`
- **Approver:** Cyrus Jamula
- **Classification:** Logged execution-tracking amendment; no re-ratification.
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
- **Accepted content pointer:** [PR #117](https://github.com/Jamula/Andreja/pull/117)
  merged as
  [`2e35d4da59b6b1c660b596dee527ec9eba2a4dda`](https://github.com/Jamula/Andreja/commit/2e35d4da59b6b1c660b596dee527ec9eba2a4dda).
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

### 2026-08-26 — proposed assistant-provider phase scope

- **Tracking issue:** [#74](https://github.com/Jamula/Andreja/issues/74)
- **Pull request:** [#119](https://github.com/Jamula/Andreja/pull/119)
- **Plan SHA-256:** `678124a48ea02fea7e3a58e8df36b48b363e102280bc2b4969cdfd9776acd7e6`
- **Approver:** Cyrus Jamula; **pending**
- **Approver status:** No Cyrus acceptance is claimed. Merge, issue closure, SDK
  availability, a compile spike, or prior Copilot access does not substitute for
  his explicit decision after the required reviews.
- **Requested outcome evidence:** Cyrus's
  [issue #74 direction comment](https://github.com/Jamula/Andreja/issues/74#issuecomment-5427814163)
  requests SDK toolchain integration; it explicitly does not approve phase
  placement or activation.
- **Classification:** Proposed editorial/status amendment; no re-ratification,
  provider activation, account, entitlement, spend, content disclosure, or
  Phase 1A/1B exit is authorized.
- **Scope:** Link Proposed ADR 0009 and make its recommendation consistent in the
  plan: deterministic fake plus optional Andreja-native OpenAI-compatible BYOK
  remain the only Phase 1A runtime providers; Phase 1A Copilot SDK work is
  limited to a credential-free, non-shipping compile/conformance toolchain
  spike; a limited real provider begins no earlier than Phase 1B after explicit
  acceptance and all provider gates. Unanswered or failed gates defer Copilot
  without weakening provider-neutral, self-hosted, offline, BYOK, portability,
  or user-owned-data boundaries.

### 2026-08-29 — proposed keyless signing status-artifact hash refresh

- **Tracking issue:** [#127](https://github.com/Jamula/Andreja/issues/127)
- **Pull request:** pending
- **Plan SHA-256:** `1827878653b67b3560334ae1c6dce8b58f52575971b8efdce14d1af33580eb1a`
- **Approver:** Cyrus Jamula; **pending**
- **Classification:** Proposed mechanical/status amendment; no re-ratification,
  release publication, production deployment, external spend, or Phase 1A exit
  is authorized.
- **Scope:** Refresh the canonical threat-model hash and issue pointer after
  accepted ADR 0010 added the approved GitHub Actions OIDC and Sigstore trust
  boundary. ADR 0010's acceptance is independent; this pending status-artifact
  refresh does not accept other Proposed plan or ADR content.

### 2026-08-30 — charter status-artifact hash refresh

- **Tracking issue:** [#3](https://github.com/Jamula/Andreja/issues/3)
- **Pull request:** [#50](https://github.com/Jamula/Andreja/pull/50)
- **Plan SHA-256:** `6f2f9769f684ac3211a383d0393ae00f6ac16b58cfb2f6ad3f7d7fe5f5fff355`
- **Approver:** Cyrus Jamula; **pending**
- **Approver status:** No Cyrus acceptance is claimed. This entry only records
  that `docs/plan.md`'s status-artifact hash for `docs/charter.md` now matches
  the current, still-Proposed charter content referenced by Proposed ADR 0006.
- **Classification:** Proposed editorial/status amendment; no re-ratification,
  charter acceptance, or authority change.
- **Scope:** Refresh the `docs/charter.md` status-artifact hash row to
  `d030d985c5de8260035eb83b17bc3be74876700487575408cf9679a05b4fa843` and
  reference the Proposed ADR 0006 ratification instrument alongside the
  existing issue/PR sources. The charter remains Proposed and not
  authoritative; no acceptance is claimed.

### 2026-08-29 — proposed legal-gate register status-artifact hash refresh

- **Tracking issue:** [#8](https://github.com/Jamula/Andreja/issues/8)
- **Pull request:** pending
- **Plan SHA-256:** `93348c96c52262a71c545df72088b7a26dc8b463fd97454831ca304008745d84`
- **Approver:** Cyrus Jamula; **pending**
- **Classification:** Proposed mechanical/status amendment; no re-ratification,
  release publication, production deployment, external spend, or Phase 1A exit
  is authorized.
- **Scope:** Refresh the `docs/legal/regulatory-applicability.md` status-artifact
  hash after that document added its regulated-feature legal-gate register and
  approval-status section, and correct the stale `docs/operating-model.md`
  status-artifact hash left behind by the issue-drain change (#136). Neither the
  regulatory framework nor the operating model gains approval from this
  mechanical refresh.

### 2026-08-30 — proposed charter and legal-gate reconciliation

- **Tracking issues:** [#3](https://github.com/Jamula/Andreja/issues/3) and
  [#8](https://github.com/Jamula/Andreja/issues/8)
- **Pull request:** [#50](https://github.com/Jamula/Andreja/pull/50)
- **Plan SHA-256:** `3ae445de2e701cce3a048dfc250b3bbb2ae51c3b4c4e93a37e4802bb6c1c1405`
- **Approver:** Cyrus Jamula; **pending**
- **Classification:** Proposed mechanical/status amendment; no re-ratification,
  charter acceptance, legal approval, release publication, production deployment,
  external spend, or Phase 1A exit is authorized.
- **Scope:** Reconcile the independently proposed charter and legal-gate
  status-artifact updates after merging their plan changes. Both underlying
  artifacts remain non-authoritative pending their respective required approvals.

### 2026-08-30 — proposed feedback-framework handoff status

- **Tracking issues:** Framework [#10](https://github.com/Jamula/Andreja/issues/10)
  and successor gates [#155](https://github.com/Jamula/Andreja/issues/155)
  through [#158](https://github.com/Jamula/Andreja/issues/158)
- **Pull request:** [#159](https://github.com/Jamula/Andreja/pull/159)
- **Plan SHA-256:** `3c825b3a9f5d1e5327b1141a04a0516d7e2cd90facf4f6a5d0fcd0290b01c4ec`
- **Approver:** Cyrus Jamula; **pending**
- **Classification:** Proposed mechanical/status amendment; no plan
  re-ratification, gate approval, collection, publication, provider selection,
  spend, deployment, launch, external email, or support-time commitment is
  authorized.
- **Scope:** Record Cyrus's separate decision that the Phase 0 feedback framework
  is complete, close #10 independently of the documentation PR, and move all
  unresolved policy, security, platform/vendor/cost, operational-readiness, and
  launch decisions to the four consolidated successor issues. Issue #158 owns a
  separate, expiring Cyrus authorization for synthetic-only non-production
  staging evidence after #155-#157 are approved; that evidence authorization is
  not a production/public launch decision. Pending status applies only to this
  plan amendment; it does not reopen the separately recorded framework handoff
  decision. Successor issue closure, labels, specialist verdicts, or
  documentation merge do not approve a gate package; each package requires
  Cyrus's explicit recorded decision.
