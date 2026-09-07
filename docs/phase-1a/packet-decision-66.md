# Phase 1A packet decision record

- **Status:** Accepted as a bounded architecture baseline; Phase 1A exit is not
  accepted
- **Decision:** Amend, then accept ADRs 0001–0005
- **Decision date:** 2026-09-07
- **Approver:** Cyrus Jamula
- **Source issue:** [#66](https://github.com/Jamula/Andreja/issues/66)
- **Acceptance pull request:** [#174](https://github.com/Jamula/Andreja/pull/174)
- **Decision and challenge evidence:**
  [issue comment](https://github.com/Jamula/Andreja/issues/66#issuecomment-5576305726)
- **Reviewed base:** `0b8edac57e050cb5d51e0b4597b690cbd76b26c3`
- **Affected implementation:** PRs
  [#46](https://github.com/Jamula/Andreja/pull/46),
  [#47](https://github.com/Jamula/Andreja/pull/47),
  [#49](https://github.com/Jamula/Andreja/pull/49),
  [#48](https://github.com/Jamula/Andreja/pull/48), and
  [#60](https://github.com/Jamula/Andreja/pull/60)

## Decision

Cyrus accepts ADRs 0001–0005, as amended in the acceptance pull request, as the
private and reversible Phase 1A architecture baseline. The accepted scope is:

- one modular .NET monolith with a typed HTTP client boundary;
- tenant-aware PostgreSQL persistence and exact application-export v1;
- local passkey identity with the bounded parameters in ADR 0002;
- one structured-response assistant contract, deterministic default provider,
  exact Open Loops proposal tool, and local contract-only skill/channel seams; and
- a Linux-container Docker Compose v5 ARM64 evidence host with local telemetry.

The implementation stack was already merged under Cyrus's narrow 2026-08-24
integration directive. This decision records architecture acceptance now; it does
not retroactively claim that packet acceptance preceded those merges.

Acceptance includes these amendments:

1. ADR 0001 no longer claims compiler enforcement between modules that share an
   assembly.
2. ADR 0002 fixes the current identity parameters: installation-specific exact
   HTTPS origin/RP domain, 10 recovery codes valid for 90 days, at most 10
   passkeys, no break-glass path, and no optional OIDC profile.
3. ADR 0003 accepts archive v1/schema `1.0.0` exactly and states that AES-GCM
   authenticates key possession, not exporter identity.
4. ADR 0004 matches the implemented single structured response rather than
   claiming streaming, fixes the proposal expiry at 10 minutes, and keeps external
   model units at zero pending a separate budget decision.
5. ADR 0005 limits host support to the exercised Docker Compose v5 Linux/ARM64
   evidence environment and distinguishes endpoint allowlisting from
   network-level default-deny egress.
6. Every ADR records its cost delta, and the packet makes privacy and
   human-authority invariants binding.

## Content hashes

These SHA-256 hashes bind the five accepted ADR files at decision time:

| ADR | SHA-256 |
| --- | --- |
| 0001 | `a111cfebb677369e10526c6d21015d4c396a909dfb39e500fc9ba635d185f8c3` |
| 0002 | `140c23189ec82ff6174841e39565ca6307d166258bde3174288c7d8c83e837c5` |
| 0003 | `045290a05a354a52eac4377216babde164c68466f75b09f402355182dcd54411` |
| 0004 | `1e863f73c250be427e8f23caa79876167cafeeff70cc5b67157d541d3d109c92` |
| 0005 | `f68fabb69f47ee2cbf2b2fb8dd9d076ec02009d0fa477d57d4dde6512be71d6e` |

Any material change to an accepted ADR requires a new amendment record and new
hash. Formatting-only changes still update the hash but may be classified as a
logged non-material amendment.

## Specialist challenge record

The acceptance review recorded the following domain challenges. These are
engineering and policy reviews, not qualified legal advice or a production
certification. Their durable summary is the
[issue #66 challenge comment](https://github.com/Jamula/Andreja/issues/66#issuecomment-5576305726).

| Reviewer | Decision | Material challenge and disposition |
| --- | --- | --- |
| Spock | Accept amended | Correct shared-assembly enforcement and assistant-contract drift; re-review on a module split, provider activation, or trust-boundary change. |
| Tuvok | Accept amended | Fix identity parameters, exporter-authentication limits, and egress wording; combined recovery and hosted provenance remain exit blockers. |
| Deanna Troi | Accept amended | Bind purpose limitation, minimization, no inferred consent/shadow profiling, deletion/export limits, and deny-by-default exposure. |
| Data | Accept bounded baseline | Historical evidence proves only its recorded source; reconcile current-tip evidence before a current implementation or exit claim. |
| Quark | Accept amended | Add cost deltas; zero external units remains mandatory until numeric limits, durable hard stop, and reconciliation are approved. |
| Jett Reno | Accept amended | Limit the host matrix to exercised evidence and retain signing, recovery, update/rollback, and operating objectives as blockers. |
| Seven of Nine | Accept amended | Align ADR 0004 with the implemented response contract; contract fixtures do not establish live federation or provider conformance. |
| Rai | Accept amended | Preserve proposal-before-write and human authority; accessibility, coercion, overreliance, and consequential-action risks remain gated. |
| Sarek | Accept amended | Limit acceptance to private internal architecture; no counsel, compliance, IP, publication, or external-use approval is claimed. |

## Required gate ledger

| Gate | Owner | Decision | Residual risk | Expiry / re-review trigger | Affected PRs |
| --- | --- | --- | --- | --- | --- |
| Architecture | Spock; Cyrus decides | Accept amended ADRs 0001–0005 | Shared-assembly coupling and future contract drift | Module split, new provider/channel/federation boundary, or 90 days | #46, #47, #49, #48, #60 |
| Security | Tuvok; Cyrus accepts risk | Accept private baseline only | Host compromise, recovery loss, export-source authenticity, provider custody | Identity/key/export/egress change or before release | #49, #48 |
| Privacy | Deanna Troi; Cyrus accepts risk | Accept binding minimization and deny-by-default invariants | Non-user data, coercion, backup/provider deletion, remote telemetry | New data class, recipient, inference, household flow, or retention change | #47, #49, #48 |
| Legal/regulatory | Sarek; Cyrus and qualified counsel decide when triggered | Accept internal architecture only | IP, terms, jurisdiction, notice, regulated-domain and public-claim exposure | Before external user, distribution, provider, jurisdiction, or regulated data | #46, #47, #49, #48, #60 |
| Cost | Quark; Cyrus approves spend | Accept zero-external-unit baseline | Operator labor, host/storage cost, nondurable usage accounting | Before non-loopback call, provider/pricing change, or managed service | #47, #48 |
| Quality | Data; Cyrus accepts exit evidence | Accept historical evidence only within its recorded source | Evidence drift, incomplete accessibility and provider-failure UX | Relevant code/schema/UI/deployment change or before exit | #46, #47, #49, #48, #60 |
| Operations | Jett Reno; Cyrus accepts risk | Accept Docker Compose v5 Linux/ARM64 evidence host | Signing, combined recovery, update/rollback, host portability | Before release/update, host change, key rotation, or backup change | #48 |
| Public claims | Picard and Sarek; Cyrus approves publication | No public readiness/compliance claim authorized | Overstatement, licensing/IP, accessibility and provider-term exposure | Before publication, distribution, marketing, or external onboarding | #46, #47, #49, #48, #60 |

## Residual-risk disposition

The following risks are **not accepted for Phase 1A exit, release, production,
external distribution, or paid/external model use**:

| Risk | Owner | Required evidence | Re-review trigger |
| --- | --- | --- | --- |
| Hosted artifact provenance is unproven | Jett Reno, Tuvok; Cyrus accepts | Reviewed protected version-tag run satisfying accepted ADR 0010, with retained bundle/root and network-blocked verification | Before the first release or update artifact |
| Database-plus-key recovery with restored sign-in is unproven | Jett Reno, Tuvok, Deanna Troi; Cyrus accepts | Encrypted clean-instance restore of PostgreSQL and Data Protection history followed by passkey sign-in | Any key, backup destination, identity adapter, or recovery-procedure change |
| Genuine update and rollback are unproven | Jett Reno; Cyrus accepts | A second separately approved and signed revision updated and rolled back against preserved state | Any migration or distribution-policy change |
| Numeric SLO, RPO/RTO, retention, and model-spend limits are unapproved | Data, Jett Reno, Quark; Cyrus approves | Numeric limits in canonical artifacts, durable enforcement, and reconciliation | Before any non-loopback model call or Phase 1A exit; on provider/pricing change |
| Evidence is historical and accessibility review is incomplete | Data, Rai | Current-tip evidence reconciliation, human assistive-technology review, and complete provider-failure/reconnect evidence | Any relevant code, schema, deployment, or UI change |
| Host/operator compromise and export source authenticity remain outside application controls | Tuvok, Jett Reno | Approved custody procedures and, if required, a separately decided exporter-signature/trust design | Before importing non-operator-controlled archives or widening deployment |
| External legal, IP, privacy, accessibility, and provider obligations remain unresolved | Sarek, Deanna Troi, Rai; qualified counsel where triggered | Applicable human/counsel decisions, notices, terms, and evidence | Before external users, publication, distribution, regulated data, or public claims |

Any isolation, consent, recovery, content-suppression, signature, or spend-control
failure stops exit and reopens the affected decision. A newly discovered unresolved
high or critical risk also reopens this record.

## Explicit exclusions

This decision does **not**:

- claim Phase 1A exit, release, production readiness, availability, security,
  privacy, accessibility, or legal compliance;
- ratify the company charter or canonical privacy/threat baselines;
- provide qualified-counsel approval, IP clearance, or publication authority;
- authorize external users, managed hosting, cloud provisioning, an account,
  subscription, free tier, trial, public connector, or live federation;
- authorize a paid or non-loopback model call; or
- accept any residual risk listed above for release or production.

Phase 1A exits only after every unchecked item in
[`exit-checklist.md`](exit-checklist.md) is complete and Cyrus records a separate
final residual-risk decision.
