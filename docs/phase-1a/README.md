# Phase 1A decision packet

- **Status:** Provisionally accepted as the governing work target under #62; not
  fully accepted; Phase 1A exit is not claimed
- **Date:** 2026-08-28
- **Issues:** Original packet [#9](https://github.com/Jamula/Andreja/issues/9);
  provisional amendment [#62](https://github.com/Jamula/Andreja/issues/62)
- **Scope:** Independent self-hosted assistant walking skeleton
- **Governing:** [Platform plan](../plan.md) and accepted
  [ADR 0000](../adr/0000-plan-ratification.md)

This packet originated under #9 and narrows the ratified
[platform plan](../plan.md) into the Phase 1A work target. Cyrus provisionally
accepted the evidence-reconciled packet under #62 so remaining work can proceed
against one target. That decision does not fully accept Proposed ADRs 0001–0005,
claim Phase 1A exit or readiness, amend the plan, authorize production, or
authorize external spend.

## Packet

1. [ADR 0001 — modular boundaries and typed client boundary](../adr/0001-phase-1a-modular-boundaries.md)
2. [ADR 0002 — identity and tenant isolation](../adr/0002-phase-1a-identity-tenancy.md)
3. [ADR 0003 — PostgreSQL persistence and data portability](../adr/0003-phase-1a-persistence-portability.md)
4. [ADR 0004 — assistant, skill, channel, and control-plane contracts](../adr/0004-phase-1a-assistant-skill-channel-contracts.md)
5. [ADR 0005 — independent self-host operations](../adr/0005-phase-1a-self-host-operations.md)
6. [Threat, privacy, cost, and test gates](evidence-gates.md)
7. [Exit and decision checklist](exit-checklist.md)
8. [BYOK security and privacy contract](byok-security-privacy.md)

The canonical cross-phase [privacy baseline](../privacy.md) and
[threat model](../threat-model.md) reconcile these Phase 1A overlays with current
implementation evidence and future gates.

## Decision boundary

The packet selects only the smallest reversible Phase 1A seams:

- a modular .NET monolith with Clean/Onion dependency direction;
- PostgreSQL for the independent self-hosted data plane;
- a typed HTTP boundary for the Blazor client;
- local passkey identity, local BYOK assistant access, and an Open Loops task
  slice;
- one OCI application artifact, a Compose contract, and local OpenTelemetry.

It deliberately **does not select** a cloud runtime, managed database, CIAM
provider, graph database, Kubernetes distribution, public connector, or managed
control plane. It authorizes no provisioning, account creation, subscription,
free tier, trial, package installation, or live paid model call.

## Current merged inputs, not accepted dependencies

The packet was checked against ADR 0000, the plan, the static Squad directives,
the assigned charter, issues #9 and #62, and these merged Phase 0 inputs:

- [PR #19 — catalog and launch frameworks](https://github.com/Jamula/Andreja/pull/19)
- [PR #20 — burn and sponsorship controls](https://github.com/Jamula/Andreja/pull/20)
- [PR #21 — regulatory applicability](https://github.com/Jamula/Andreja/pull/21)
- [PR #25 — Phase 1A semantic graph contract](https://github.com/Jamula/Andreja/pull/25)

Merge supplies repository inputs, not automatic ratification or acceptance.
These artifacts retain the authority stated in the plan and their canonical
status metadata. In particular, the packet uses the relational-source-of-truth
contract introduced by PR #25 without making every surrounding deferred semantic
decision authoritative.

## Human decisions still required

Cyrus must explicitly decide or accept:

1. final disposition of ADRs 0001–0005 after the named specialist reviews;
2. trusted operator signing and the external trust anchor for release evidence;
3. encrypted PostgreSQL and Data Protection key recovery into a clean instance,
   including restored passkey sign-in;
4. a genuine second, separately approved and signed revision for both update and
   rollback against preserved state;
5. numeric internal SLO and RPO/RTO limits, numeric retention limits, and a
   numeric Phase 1A model-spend envelope with an enforced hard stop; and
6. final residual-risk acceptance after all blocking evidence is complete.

Cloud runtime, managed database, CIAM, and graph database decisions are
explicitly deferred and are not hidden prerequisites for Phase 1A.
This amendment also does not choose among restoring local tooling, using an
isolated equivalent evidence host, or stopping; any such technical
evidence-host decision remains Cyrus's under #62.

## Ethics and sustainability impact assessment

This single packet-level assessment satisfies the
[charter's required eight points](../charter.md#ethics-and-sustainability-impact-assessment)
for ADRs 0001–0005; each ADR separately records its technical alternatives.

1. **People and agency:** Cyrus benefits first from a self-owned assistant/task
   outcome. Server-authorized proposals, explicit confirmation, correction,
   completion, export, deletion, revocation, and provider pause/kill preserve review
   and exit. Affected contacts or future peers receive no Phase 1A live sharing.
2. **Data and consent:** Identity, task/assistant content, grants/consent/share audit,
   credentials, recovery material, and usage evidence have explicit purpose,
   minimization, export/delete, retention, and model-exposure rules. Sharing defaults
   denied; contract fixtures confer no live authority.
3. **Equity and accessibility:** Local hosting, passkeys, technical recovery, and
   BYOK may exclude people with limited devices, connectivity, funds, or operational
   skill. Keyboard/accessibility and phone/tablet/desktop proof is required, while
   broader languages, assisted recovery, and managed onboarding remain known gaps.
4. **AI and safety:** AI is limited to a declared provider, exact typed tool, and
   proposal rather than direct task mutation. Provenance, errors, policy denials,
   budget stop, deterministic fakes, and human confirmation expose uncertainty and
   constrain prompt/tool injection or confused-deputy misuse.
5. **Sustainability:** One modular process, one relational store, local telemetry,
   deterministic fakes, bounded retention, and no inactive federation persistence
   reduce compute, storage, vendor, maintenance, financial, and environmental cost.
   Live model use remains blocked pending the canonical cost-model approval gate.
6. **Stakeholders and incentives:** The user/data subject, operator, contributors,
   providers, future peers, and affected non-users are considered. No sponsor,
   marketplace, growth, or provider incentive can widen grants, bypass confirmation,
   receive content, or authorize spend.
7. **Evidence and alternatives:** Architecture, isolation, recovery, portability,
   permission-negative, envelope, telemetry, accessibility, and cost evidence can
   disprove the design. Each ADR rejects a higher-risk or higher-resource alternative;
   failed evidence reopens the decision rather than converting assumptions to claims.
8. **Owner and stop conditions:** Cyrus owns acceptance; Spock, Tuvok, Deanna Troi,
   Data, Quark, Jett Reno, Seven of Nine, Rai, and Sarek own the listed challenges.
   Isolation, consent, recovery, content-suppression, accessibility, unknown spend,
   unexplained egress, or high/critical residual-risk failure pauses exit and triggers
   remediation, de-scope, rollback, or a new decision.

## Deferred ADR topics

The selected version fields in API/export/peer fixtures are sufficient for Phase 1A,
but the general compatibility/versioning policy remains a separate ADR. Also
deferred are reminders/recurrence; live federation discovery, trust, transport, and
relay; general grants/sharing UX; production channel/connector lifecycle; third-party
skill execution and marketplace governance; managed/remote control plane; CIAM and
cloud topology; graph infrastructure; public API compatibility; and provider PITR.
These are remaining plan ADR topics, not silently approved by this packet.

## Merged evidence and residual gaps

At the verified merged-main base,
[`evidence-44.md`](evidence-44.md) records synthetic local proof for the
PostgreSQL and architecture suites, explicit migrations, production passkey
bootstrap/sign-in/recovery, the confirmed task lifecycle, local provider-failure
conformance, contract-only skill/channel/grant/consent/peer and semantic seams,
application export/import, offline start/restart/no-egress, OTel content
suppression, basic 320/768/1280 and keyboard checks, and reproducible OCI audit
artifacts. The canonical [testing matrix](../testing-matrix.md),
[privacy baseline](../privacy.md), and [threat model](../threat-model.md) own the
current classifications and exclusions.

The passing local evidence does not supply trusted operator signing, combined
encrypted PostgreSQL-and-key recovery with restored passkey sign-in, a separately
signed update/rollback pair, approved numeric SLO/RPO/RTO and retention limits,
an approved numeric model-spend envelope/hard stop, the required specialist
reviews, or Cyrus's final residual-risk acceptance. The OCI evidence remains
unsigned and basic accessibility evidence is not a human assistive-technology
study. No external model call, provisioning, spend, release authorization,
readiness, or Phase 1A exit is claimed.
