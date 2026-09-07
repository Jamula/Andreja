# Phase 1A packet decision record

- **Status:** Decision-ready; **not accepted**. ADRs 0001–0005 remain
  **Proposed**. No approval, milestone exit, or readiness is claimed.
- **Date prepared:** 2026-09-07
- **Decision owner:** Cyrus Jamula (sole human accept/amend/reject authority)
- **Prepared by:** Picard (CEO and Lead/Captain), as a reconciliation of existing
  recorded evidence
- **Issue:** [#66](https://github.com/Jamula/Andreja/issues/66)
- **Governing:** [Platform plan](../plan.md) and accepted
  [ADR 0000](../adr/0000-plan-ratification.md)
- **Packet:** [Phase 1A decision packet](README.md),
  [evidence gates](evidence-gates.md), [exit checklist](exit-checklist.md)

This record reconciles the Phase 1A packet, the recorded specialist-challenge
state, and the outstanding residual risks into one place so Cyrus can accept,
amend, or reject. It records the state of the evidence as read on 2026-09-07. It
is **not** an approval, and preparing it changes no artifact status.

## What this document is and is not

| This document | Authority |
| --- | --- |
| Is a reconciliation of already-recorded evidence into a decision-ready form | Picard prepares; it binds nothing |
| Is the enumerated list of unresolved risks, owners, evidence, and stop conditions | Sourced from issues, PRs, and canonical docs |
| Is an accept/amend/reject template for Cyrus to complete | Cyrus alone completes it |
| **Is not** acceptance of ADRs 0001–0005 | Only Cyrus's explicit recorded decision accepts them |
| **Is not** Phase 1A exit, production readiness, or release authorization | See [exit checklist](exit-checklist.md) |
| **Is not** charter ratification, counsel approval, publication, provisioning, or paid-model authorization | Each has its own separate gate below |
| **Is not** a specialist challenge, and does not stand in for one | The nine named challenges remain unrecorded |

Preparation, review, merge, issue closure, a passing check, a document title, or
an agent statement is not Cyrus's decision. That rule is already recorded in
[#73](https://github.com/Jamula/Andreja/issues/73): "Issue closure, merge, agent
statement, and document title must not substitute for Cyrus's explicit human
decision."

## Human authority

Cyrus Jamula holds sole authority to accept, amend, or reject ADRs 0001–0005 and
the Phase 1A residual-risk packet. No agent, reviewer, workflow, or automation in
this repository may exercise, imply, infer, or retroactively construct that
decision. Picard's charter boundary applies: Picard "does not override security,
privacy, legal or evidence gates" and "cannot spend funds, sign contracts,
publish or make legal representations."

A decision is recorded only when Cyrus completes the
[decision template](#decision-template-for-cyrus) with approver, date, decision,
content hashes, source issue, challenge evidence, and residual-risk scope, as
required by [#66](https://github.com/Jamula/Andreja/issues/66).

## Current ADR status

All five ADRs are `Proposed`. Cyrus's 2026-08-24 comment on
[#66](https://github.com/Jamula/Andreja/issues/66) states: "ADRs 0001–0005 remain
Proposed and the stack has no recorded approval."

| ADR | Subject | Proposed by | Status | Human decisions still open in the ADR | Affected implementation layers |
| --- | --- | --- | --- | --- | --- |
| [0001](../adr/0001-phase-1a-modular-boundaries.md) | Modular boundaries and typed client boundary | Spock | **Proposed** | None stated in the ADR; disposition is part of the packet decision | PR [#46](https://github.com/Jamula/Andreja/pull/46) foundation; PR [#60](https://github.com/Jamula/Andreja/pull/60) Blazor circuit-client scope |
| [0002](../adr/0002-phase-1a-identity-tenancy.md) | Identity and tenant isolation | Tuvok | **Proposed** | RP IDs/origins, recovery-code count/lifetime, passkey device limits, operator break-glass custody, optional OIDC profile | PR [#49](https://github.com/Jamula/Andreja/pull/49) identity/tenancy; PR [#78](https://github.com/Jamula/Andreja/pull/78) production passkeys |
| [0003](../adr/0003-phase-1a-persistence-portability.md) | PostgreSQL persistence and portability | Spock and Jett Reno | **Proposed** | Application export v1 encoding and compatibility window | PR [#49](https://github.com/Jamula/Andreja/pull/49); PR [#80](https://github.com/Jamula/Andreja/pull/80) durable confirmation; PR [#83](https://github.com/Jamula/Andreja/pull/83) semantic contracts; PR [#86](https://github.com/Jamula/Andreja/pull/86) PostgreSQL fixes; PR [#89](https://github.com/Jamula/Andreja/pull/89) export/import |
| [0004](../adr/0004-phase-1a-assistant-skill-channel-contracts.md) | Assistant, skill, channel, and control-plane contracts | Seven of Nine | **Proposed** | BYOK compatibility profile, endpoint/model allowlist, credential custody UX, proposal expiry/confirmation tiers, live-model budget | PR [#47](https://github.com/Jamula/Andreja/pull/47) assistant/proposal contracts; PR [#81](https://github.com/Jamula/Andreja/pull/81) skill/channel contracts; PR [#82](https://github.com/Jamula/Andreja/pull/82) BYOK transport |
| [0005](../adr/0005-phase-1a-self-host-operations.md) | Independent self-host operations | Jett Reno | **Proposed** | Supported host/runtime matrix, HTTPS/passkey onboarding, key custody and backup destinations, update signature/distribution policy, local evidence backend, recovery objectives | PR [#48](https://github.com/Jamula/Andreja/pull/48) self-host operations; PR [#84](https://github.com/Jamula/Andreja/pull/84) OCI supply-chain gates; PR [#131](https://github.com/Jamula/Andreja/pull/131) keyless signing workflow |

Cross-cutting evidence for all five ADRs is recorded in
[`evidence-44.md`](evidence-44.md) (PR
[#88](https://github.com/Jamula/Andreja/pull/88)) and the provisional packet
amendment PR [#125](https://github.com/Jamula/Andreja/pull/125) under
[#62](https://github.com/Jamula/Andreja/issues/62).

## What was already authorized, and what was not

The issue title says "before merge," but the implementation stack merged on
2026-08-24 under a narrow human integration directive recorded on
[#66](https://github.com/Jamula/Andreja/issues/66). The decision this issue
exists for was deliberately preserved rather than satisfied.

| PR | Layer | Merged |
| --- | --- | --- |
| [#46](https://github.com/Jamula/Andreja/pull/46) | .NET modular foundation | 2026-08-24 |
| [#47](https://github.com/Jamula/Andreja/pull/47) | Assistant and proposal contracts | 2026-08-24 |
| [#49](https://github.com/Jamula/Andreja/pull/49) | Identity and tenant isolation | 2026-08-24 |
| [#48](https://github.com/Jamula/Andreja/pull/48) | Self-host operations | 2026-08-24 |
| [#60](https://github.com/Jamula/Andreja/pull/60) | Blazor circuit-client scope | 2026-08-24 |

Cyrus's recorded directive authorized "integration of the current reviewed,
fail-closed implementation after blocking review defects are fixed." The same
directive states that it "does **not** claim Phase 1A exit, production usability,
charter ratification, counsel approval, external publication, cloud provisioning,
paid model use, or acceptance of unresolved evidence," and that #66 "remains the
durable place to reconcile the ADR `Proposed` status and record the final
packet/residual-risk decision."

### Separate gates that this decision does not close

Decision-readiness and prior merge authorization are distinct from every gate
below. Accepting the packet closes none of them by itself.

| Gate | Current recorded state | Authoritative source |
| --- | --- | --- |
| Packet acceptance (this record) | Open; ADRs 0001–0005 `Proposed` | [#66](https://github.com/Jamula/Andreja/issues/66) |
| Merge/integration of the stack | Already authorized 2026-08-24, narrowly | [#66](https://github.com/Jamula/Andreja/issues/66) |
| Phase 1A exit | Not claimed; blocking evidence outstanding | [exit checklist](exit-checklist.md), [#44](https://github.com/Jamula/Andreja/issues/44) |
| Production readiness / release authorization | Not claimed; the audited OCI bundle is unsigned and unauthorized for release | [`evidence-44.md`](evidence-44.md) |
| Charter ratification | Charter `Proposed`; ADR 0006 `Proposed`; requires an explicit atomic acceptance | [#3](https://github.com/Jamula/Andreja/issues/3), [ADR 0006](../adr/0006-charter-ratification.md) |
| Qualified counsel approval | Not recorded; research artifacts are not legal advice | [#6](https://github.com/Jamula/Andreja/issues/6), [license evaluation](../legal/license-evaluation.md) |
| External publication / public claims | Blocked pending recorded research, materiality determination, and counsel review where material | [#3](https://github.com/Jamula/Andreja/issues/3), [#6](https://github.com/Jamula/Andreja/issues/6), [#93](https://github.com/Jamula/Andreja/issues/93) |
| Cloud provisioning | Not authorized; Phase 0 provisions no account, subscription, free tier, trial, or resource | [plan](../plan.md), [#38](https://github.com/Jamula/Andreja/issues/38) |
| Paid or external model use | Not authorized; blocked on an approved numeric spend envelope and hard stop | [evidence gates](evidence-gates.md#cost-index-and-stop-gates), [#74](https://github.com/Jamula/Andreja/issues/74) |
| Phase 1B, provider activation, federation, connectors | Deferred; out of Phase 1A scope | [ADR 0004](../adr/0004-phase-1a-assistant-skill-channel-contracts.md), [Proposed ADR 0009](../adr/0009-copilot-provider-phase-scope.md) |

## Specialist challenge ledger

[#66](https://github.com/Jamula/Andreja/issues/66) requires "Recorded challenges
from Spock, Tuvok, Deanna Troi, Data, Quark, Jett Reno, Seven, Rai, and Sarek."
As of 2026-09-07, **no packet-specific challenge is recorded for any of the
nine**. PR [#131](https://github.com/Jamula/Andreja/pull/131) restated on
2026-08-29 that "Required Spock, independent security, Jett Reno, Data, Deanna
Troi, Quark, Sarek, Rai … review remains pending."

No fetched review on the Phase 1A stack PRs carries GitHub `APPROVED` state; PRs
[#47](https://github.com/Jamula/Andreja/pull/47),
[#49](https://github.com/Jamula/Andreja/pull/49), and
[#60](https://github.com/Jamula/Andreja/pull/60) have no review objects at all,
and PR [#45](https://github.com/Jamula/Andreja/pull/45), which merged the packet
documentation, has none either. Generic statements that an "independent packet
review completed" ([#9](https://github.com/Jamula/Andreja/issues/9)) or that
"independent architecture, privacy, security, and FinOps reviews completed"
(PR [#119](https://github.com/Jamula/Andreja/pull/119)) name no reviewer, record
no findings, and appear alongside text that keeps the same reviews listed as
blockers. They are not treated here as challenge evidence.

| Specialist | Domain | Packet challenge | Nearest related record, and its explicit limit |
| --- | --- | --- | --- |
| Spock | Architecture boundaries, portability, alternatives, federation coherence | **Not recorded** | PR [#46](https://github.com/Jamula/Andreja/pull/46): "Spock/Data review remains appropriate for this draft." [#104](https://github.com/Jamula/Andreja/issues/104): "Data and Spock were locked out and did not contribute." |
| Tuvok | Threat and identity controls | **Not recorded** | [#104](https://github.com/Jamula/Andreja/issues/104), 2026-08-25: "Security (Tuvok): APPROVED repository scaffolding," expressly qualified as "advisory, head-bound review evidence—not the operational gate." Scope is the merge-gate scaffolding, not the packet. |
| Deanna Troi | Privacy, retention, model exposure, export, delete | **Not recorded** | [#104](https://github.com/Jamula/Andreja/issues/104): "Deanna Troi independently revised draft PR #115." That is a contribution to a different artifact, not a packet challenge. |
| Data | Test topology, production-impossible auth, SLO queries, evidence reproducibility | **Not recorded** | Evidence coordinator under [#62](https://github.com/Jamula/Andreja/issues/62) and author of PR [#125](https://github.com/Jamula/Andreja/pull/125), which itself retains "specialist reviews; and Cyrus's final residual-risk acceptance" as open. Authored reconciliation is not an independent challenge. |
| Quark | Numeric Phase 1A model-spend limits | **Not recorded** | [#9](https://github.com/Jamula/Andreja/issues/9) added the challenge gate; no cost challenge, envelope, or verdict is recorded anywhere. |
| Jett Reno | OCI/runtime, channel seam, offline start, telemetry, backup/update/recovery, operational support | **Not recorded** | [#104](https://github.com/Jamula/Andreja/issues/104): "Workflow correctness (Jett Reno): APPROVED repository scaffolding," under the same "advisory, head-bound … not the operational gate" limit. Named operations owner in [#62](https://github.com/Jamula/Andreja/issues/62) without a recorded packet verdict. |
| Seven of Nine | Assistant/skill contracts, grant/consent/disclosure/share-audit/peer-envelope conformance | **Not recorded** | Implementation authorship on PR [#47](https://github.com/Jamula/Andreja/pull/47). Implementation is not challenge. On 2026-09-07 assignment automation reported "No squad member found matching label `squad:seven-of-nine`" on [#74](https://github.com/Jamula/Andreja/issues/74). |
| Rai | AI safety, human confirmation, harmful misuse, fairness, packet impact assessment | **Not recorded** | Challenge gate added under [#9](https://github.com/Jamula/Andreja/issues/9); listed pending in PR [#131](https://github.com/Jamula/Andreja/pull/131). Also named pending on the canonical [privacy](../privacy.md) and [threat model](../threat-model.md) baselines. |
| Sarek | Legal/regulatory, consent/audit, provider terms, licensing/IP, claims | **Not recorded** | [#3](https://github.com/Jamula/Andreja/issues/3), 2026-09-07: "A Sarek research review of #168 found no blocking issue," expressly "research only, not legal advice, privilege, trademark clearance, or counsel approval," and scoped to a charter prerequisite rather than ADRs 0001–0005. |

Consequence: option 1 in [#66](https://github.com/Jamula/Andreja/issues/66)
("Accept the packet as written and record Cyrus's approval **plus challenge
evidence**") cannot currently be satisfied in full, because the challenge
evidence it requires does not exist. Accepting now is possible only as an
explicitly conditional acceptance that records the missing challenges as accepted
residual risk. That trade-off is Cyrus's to make, not Picard's.

## Residual-risk register

Every row is drawn from a recorded source. Owners are recorded owners; where no
owner was recorded, the row says so rather than assigning one. "Trigger" is the
recorded expiry or re-review condition; rows marked *(proposed)* are Picard
recommendations that are **not** approved and require Cyrus's decision.

### Governance and decision risks

| ID | Residual risk | Owner | Evidence required to close | Affected layers | Stop condition | Expiry / re-review trigger |
| --- | --- | --- | --- | --- | --- | --- |
| R-01 | ADRs 0001–0005 and the residual-risk packet have no recorded human decision, while the implementation is on `main` | Cyrus | A completed decision record with approver, date, hash, source issue, challenge evidence, and residual-risk scope | ADRs 0001–0005; PRs [#46](https://github.com/Jamula/Andreja/pull/46), [#47](https://github.com/Jamula/Andreja/pull/47), [#49](https://github.com/Jamula/Andreja/pull/49), [#48](https://github.com/Jamula/Andreja/pull/48), [#60](https://github.com/Jamula/Andreja/pull/60) | Do not represent Phase 1A as authorized or complete | Any trust-boundary assumption change, or any high/critical unresolved risk — pause and amend rather than infer approval |
| R-02 | None of the nine named specialist challenges is recorded | Spock, Tuvok, Deanna Troi, Data, Quark, Jett Reno, Seven of Nine, Rai, Sarek | A recorded, attributable challenge per domain with findings and verdict | Whole packet | Do not claim reviewed status or satisfy #66 option 1 as written | Re-check before any acceptance, and again if a challenge is later recorded |
| R-03 | The per-domain gate ledger (architecture, security, privacy, legal/regulatory, cost, quality, operations, public claims) is incomplete: owner, decision, residual risk, trigger, and affected PRs are not recorded per gate | Not recorded; #66 requires owners to be named | One row per gate with all five fields | Whole packet | Do not treat the packet as decision-complete | Before the final packet decision |
| R-04 | The classification/impact assessment and residual-risk acceptance remain open on the canonical privacy and threat baselines | Deanna Troi, Tuvok, Rai (challenge); Cyrus (acceptance) | Explicitly approved assessment with cited evidence | [privacy](../privacy.md), [threat model](../threat-model.md) | Do not represent those baselines as ratified | Any change to data classes, trust boundaries, or model exposure |
| R-05 | Phase 0 artifact authority remains unreconciled; merge/closure has been mistaken for approval before | Cyrus; qualified counsel where required | Explicit status decisions under [#73](https://github.com/Jamula/Andreja/issues/73) | Phase 0 artifact set | Stop publication, launch claims, spending, or policy enforcement when authoritative status cannot be proven | While [#73](https://github.com/Jamula/Andreja/issues/73) is open |

### Evidence and Phase 1A exit risks

| ID | Residual risk | Owner | Evidence required to close | Affected layers | Stop condition | Expiry / re-review trigger |
| --- | --- | --- | --- | --- | --- | --- |
| R-06 | No trusted hosted signing evidence exists; the audited OCI bundle is unsigned | Jett Reno (operations), Tuvok (security); Cyrus accepts | A reviewed hosted version-tag run producing accepted [ADR 0010](../adr/0010-keyless-sigstore-github-oidc.md)'s keyless bundle and retained root copy, exact-claim matched, verified with networking blocked against an independently held root | ADR 0005; PRs [#84](https://github.com/Jamula/Andreja/pull/84), [#131](https://github.com/Jamula/Andreja/pull/131) | No release, update, or production startup authorization; local operator-key evidence cannot substitute | The first reviewed, authorized protected version tag; stop on claim, chain, bundle, policy, or offline-verification failure |
| R-07 | Encrypted PostgreSQL plus Data Protection key recovery into a clean instance with restored passkey sign-in is unproven | Jett Reno (operations), Tuvok/Deanna Troi (challenge); Cyrus accepts | One clean-instance drill proving database, key custody, and restored sign-in together | ADRs 0002, 0003, 0005; PRs [#49](https://github.com/Jamula/Andreja/pull/49), [#48](https://github.com/Jamula/Andreja/pull/48), [#78](https://github.com/Jamula/Andreja/pull/78) | Recovery failure pauses exit and triggers remediation | On any key-custody, backup-destination, or identity-adapter change |
| R-08 | A genuine second, separately approved and signed revision has not completed update and rollback against preserved state | Jett Reno; Cyrus accepts | A second signed, approved revision plus a successful update and rollback drill preserving data, identity, configuration, audit, and idempotency | ADR 0005; PR [#48](https://github.com/Jamula/Andreja/pull/48) | Do not claim update/rollback safety | On any migration or image-distribution policy change |
| R-09 | Numeric internal SLO, RPO/RTO, retention, and Phase 1A model-spend limits with an enforced hard stop are unapproved; current SLO values are explicitly unapproved candidate targets | Quark (spend proposal), Data (evidence queries), Jett Reno (recovery objectives); Cyrus approves | Recorded numeric limits in the canonical [cost model](../cost-model.md) and evidence gates, plus an enforced hard stop | ADRs 0003, 0004, 0005; [evidence gates](evidence-gates.md) | Unmetered spend, envelope breach, or unreconciled provider usage triggers no-go and pause | Before any real BYOK call; on any provider or price-assumption change |
| R-10 | Exit-checklist traceability is incomplete: not every item links commands, results, artifact, owner, and build digest | Data | Completed traceability under [#44](https://github.com/Jamula/Andreja/issues/44) | [exit checklist](exit-checklist.md), [`evidence-44.md`](evidence-44.md) | Failures must block, not be represented as success | While [#44](https://github.com/Jamula/Andreja/issues/44) is open |
| R-11 | The technical evidence-host decision (restore local tooling, use an isolated equivalent host, or stop) remains undecided; [#62](https://github.com/Jamula/Andreja/issues/62) closed while provisional | Data, Jett Reno (options); Cyrus decides | An explicit recorded evidence-host decision | [evidence gates](evidence-gates.md), [`README`](README.md) | Do not represent any stack merge as milestone exit | Recorded trigger: "immediately when Docker daemon + disposable PostgreSQL + browser harness are available, before any Phase 1A stack merge is represented as milestone exit, or by 2026-09-07, whichever occurs first." **The 2026-09-07 date has arrived and the decision is still open.** |
| R-12 | Accessibility evidence is basic automated/keyboard proof only, and a complete provider-failure experience is unproven | Data (evidence), Rai (fairness/inclusion challenge); Cyrus accepts | Human assistive-technology review and a complete provider-failure experience pass | ADR 0001/0004 UI layers; PRs [#60](https://github.com/Jamula/Andreja/pull/60), [#47](https://github.com/Jamula/Andreja/pull/47) | Accessibility failure pauses exit | Before Phase 1A exit; on any UI or provider-error-path change |

### Boundary and authorization risks

| ID | Residual risk | Owner | Evidence required to close | Affected layers | Stop condition | Expiry / re-review trigger |
| --- | --- | --- | --- | --- | --- | --- |
| R-13 | Assistant-provider phase scope is undecided; [Proposed ADR 0009](../adr/0009-copilot-provider-phase-scope.md) is not accepted | Cyrus, with named architecture, privacy, security/abuse, FinOps/operations, and qualified legal verdicts | Accepted ADR 0009 plus exact SDK/runtime/account/model/topology evidence, numeric budgets/SLO/retention, and tested canary/rollback | ADR 0004; PR [#119](https://github.com/Jamula/Andreja/pull/119) | No account, runtime, content disclosure, model call, spend, or phase exit | Unclear entitlement, credential/session leakage, unbounded cost, retention uncertainty, or unavailable cancellation/revocation |
| R-14 | The always-present independent-review gate is not operational, and PRs have merged despite their own recorded "review remains pending" text | Data (assigned); operational authorization is Cyrus's | A provisioned, independently protected review-completion gate with passing live negative, dropped-delivery, rate-limit, base-push, reviewer-revocation, and merge-group canaries | Repository merge controls; [#104](https://github.com/Jamula/Andreja/issues/104), [#67](https://github.com/Jamula/Andreja/issues/67) | Do not treat a merge as evidence that its stated review gate was satisfied | While [#104](https://github.com/Jamula/Andreja/issues/104) and [#67](https://github.com/Jamula/Andreja/issues/67) are open |
| R-15 | Charter ratification is open; [ADR 0006](../adr/0006-charter-ratification.md) and [`docs/charter.md`](../charter.md) are Proposed | Cyrus | A separate atomic acceptance recording approval date, PR, and content hash | Charter-dependent claims and templates | Do not treat the charter as authoritative or co-governing | While [#3](https://github.com/Jamula/Andreja/issues/3) is open |
| R-16 | Licensing, IP, trademark, and contribution strategy lack the required human and qualified-counsel approval | Cyrus and qualified counsel | Recorded approval of the inbound/outbound license, IP, trademark, and collaborator governance path | [license evaluation](../legal/license-evaluation.md), [regulatory applicability](../legal/regulatory-applicability.md) | Accept no external contributions; publish no releases, packages, protocols, SDKs, docs sites, domains, or stable namespaces | While [#6](https://github.com/Jamula/Andreja/issues/6) is open |
| R-17 | Repository visibility observed as public on 2026-09-07 conflicts with the recorded publication boundary; the observation does not establish when, why, or what was exposed | Cyrus; qualified counsel where material | Preserved evidence, exposure history, and a recorded authorized response | Publication boundary; [#6](https://github.com/Jamula/Andreja/issues/6), [#114](https://github.com/Jamula/Andreja/issues/114) | Do not treat observed visibility as approval for publication or public claims | Immediately, and on any visibility change |
| R-18 | ADR-level human decisions remain unmade: RP IDs/origins, recovery-code policy, passkey device limits, break-glass custody, optional OIDC profile (0002); export v1 encoding and compatibility window (0003); BYOK profile, endpoint/model allowlist, credential custody UX, proposal expiry/confirmation tiers, live-model budget (0004); host/runtime matrix, onboarding, key custody and backup destinations, update signature/distribution policy, evidence backend, recovery objectives (0005) | Cyrus, on each ADR | Recorded decisions per ADR, or an explicit deferral with a trigger | ADRs 0002–0005 and their implementation layers | Do not treat implemented defaults as approved policy | With the packet decision; re-review on any change to the affected surface |

## No-cloud and no-paid-model boundary

This boundary is currently satisfied and must remain satisfied by any decision
recorded here. The [exit checklist](exit-checklist.md) already records it as the
one checked packet-approval row: "No cloud runtime, managed DB, CIAM, graph DB,
account, subscription, free tier, trial, provisioned resource, or paid live call
was selected or created."

- Phase 0 cloud-infrastructure spend remains `$0`; the packet performs local and
  paper analysis only.
- No cloud runtime, managed database, CIAM provider, graph database, Kubernetes
  distribution, public connector, or managed control plane is selected.
- No account creation, subscription, free tier, trial, provisioning, or package
  installation is authorized by the packet or by this record.
- A real BYOK or any external model call stays blocked until Quark records
  published-price assumptions and estimates and Cyrus approves a numeric Phase 1A
  model-spend envelope with a hard stop (R-09).
- Accepting the packet does **not** relax any item above. Managed hosting, cloud
  observability, CIAM, managed databases, and provisioning belong to a separately
  approved later spike.

## Decision-readiness assessment

Decision-readiness means the packet, its evidence state, its unresolved risks,
and its boundaries are reconciled and legible enough for a human decision. It
does **not** mean the evidence is complete.

| Criterion | State |
| --- | --- |
| The decision object is unambiguous (ADRs 0001–0005 plus the residual-risk packet) | Met |
| Current status of each ADR is recorded and unaltered | Met — all `Proposed` |
| Unresolved risks, owners, evidence, layers, stop conditions, and triggers are enumerated | Met, as recorded in R-01 – R-18 |
| Separate gates are distinguished from this decision | Met |
| Specialist challenge evidence required by #66 exists | **Not met** — R-02 |
| Blocking Phase 1A exit evidence is complete | **Not met** — R-06 – R-12 |
| Numeric operating and spend limits are approved | **Not met** — R-09 |

Cyrus can therefore make an informed accept/amend/reject decision now, but an
acceptance recorded today is necessarily conditional and cannot double as Phase
1A exit.

## Decision template for Cyrus

Record the decision as a comment on
[#66](https://github.com/Jamula/Andreja/issues/66) and, if accepted or amended,
in a follow-up PR that changes each ADR's `Status` line and appends the record
below to this file. Do not change any ADR status without a completed record.

Compute the content hashes that bind the decision with:

```powershell
Get-ChildItem docs/adr/000[1-5]-*.md, docs/phase-1a/packet-decision-66.md |
  ForEach-Object { "{0}  {1}" -f (Get-FileHash $_ -Algorithm SHA256).Hash.ToLower(), $_.Name }
```

### Template

```markdown
## Phase 1A packet decision — recorded

- Approver: Cyrus Jamula
- Date: <YYYY-MM-DD>
- Source issue: #66
- Decision: <Accept | Amend | Reject>
- Decision object: ADRs 0001, 0002, 0003, 0004, 0005 and the Phase 1A
  residual-risk packet
- Content hashes at decision time:
  - 0001: <sha256>
  - 0002: <sha256>
  - 0003: <sha256>
  - 0004: <sha256>
  - 0005: <sha256>
  - packet-decision-66.md: <sha256>
- Challenge evidence relied on: <cite each recorded challenge, or state
  "none recorded" and accept R-02 as residual risk>
- Residual-risk scope accepted: <list the R-ids explicitly accepted; any R-id
  not listed is NOT accepted>
- Residual risks explicitly NOT accepted: <list R-ids>
- Conditions attached: <e.g. no external model call until R-09 closes>
- Expiry / re-review trigger: <date or condition>
- Explicitly NOT decided by this record: Phase 1A exit, production readiness,
  release authorization, charter ratification, counsel approval, external
  publication, cloud provisioning, paid or external model use.
```

### Option 1 — Accept

Records acceptance of ADRs 0001–0005 as written and of the named residual-risk
scope. Requires stating whether the missing specialist challenges (R-02) are
accepted as residual risk or waived, because
[#66](https://github.com/Jamula/Andreja/issues/66) option 1 as written also
requires challenge evidence. Effect: ADR status changes `Proposed` →
`Accepted` with approver, date, and hash. It does not close R-06 – R-18, does not
authorize spend, provisioning, publication, or release, and does not exit Phase
1A.

### Option 2 — Amend

Records acceptance of some ADRs or risk decisions and amendment of others. Name
each amended ADR or risk, the required change, the affected implementation
layers to revalidate, and the evidence that must be re-run. Unamended ADRs stay
`Proposed` until separately decided. Preserves governance at the cost of rework.

### Option 3 — Reject or pause

Records rejection or a pause of the packet. Name whether the merged
implementation is retained as research evidence, reverted, or frozen, and the
condition that would reopen the decision. Avoids premature commitment and delays
Phase 1A.

### If no decision is recorded

The status quo holds: ADRs 0001–0005 remain `Proposed`, the merged
implementation remains implementation evidence only, Phase 1A exit stays
unclaimed, and every gate above stays closed. Nothing in this repository may
represent the packet as accepted.

## Re-review triggers for this record

This record is a snapshot of evidence read on 2026-09-07 and must be corrected,
not relied on, when it goes stale. Re-read and correct it when:

- any of ADRs 0001–0005 changes status or content;
- any named specialist records a challenge, or a high/critical residual risk is
  raised;
- a trust-boundary assumption changes;
- any of R-06 – R-12 closes with new evidence, or an evidence run supersedes
  [`evidence-44.md`](evidence-44.md);
- spend becomes unknown or moves outside an approved envelope; or
- isolation, consent, recovery, or content-suppression evidence fails.

The same change that makes a status, owner, link, or risk row here stale must
correct this file. Conflicts resolve in favor of the ratified
[plan](../plan.md) and the canonical owner named in the
[evidence gates](evidence-gates.md#canonical-artifact-links).

## Related records

- [Phase 1A decision packet](README.md) — packet contents and boundary
- [Phase 1A evidence-gate index](evidence-gates.md) — threat, privacy, test, cost gates
- [Phase 1A decision and exit checklist](exit-checklist.md) — exit conditions
- [Phase 1A evidence run 44](evidence-44.md) — current merged evidence
- [BYOK security and privacy contract](byok-security-privacy.md)
- [Platform plan](../plan.md) and [ADR 0000](../adr/0000-plan-ratification.md)
- [Canonical privacy baseline](../privacy.md) and [threat model](../threat-model.md)
