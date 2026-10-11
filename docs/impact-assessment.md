# Proposed integrated impact and classification assessment

- **Status:** Proposed; not approved and not a gate-closing assessment
- **Prepared by:** Spock, Chief Architect
- **Independent revision owner:** Data, Quality Engineering Lead; Spock and
  Jett Reno are locked out of this revision following Tuvok's rejections.
  Jett's prior revision at `b816653e849135a4ab36af1b23e0ab8449309fff` is
  source evidence, not contribution to this independent correction
- **Tracking:** [Issue #2](https://github.com/Jamula/Andreja/issues/2)
- **Evidence baseline:** pinned `main` at `142f227e8457a573010a3c333e94e0eda0a8d6ac`
- **Revision integration base:** `main` at
  `6e9d06fb9642f724671bffa84bf3bbfcd590aa0d`; original evidence baseline
  retained above, not replaced with a claim of fresh runtime evidence
- **Required challenge:** Tuvok (security), Deanna Troi (privacy), Rai (AI safety); pending
- **Final human decision:** Cyrus; pending

## Authority and limits

This document is a reversible proposal prepared under coordinator-directed
autonomous documentation work. It does not record Cyrus's authorization,
approval, ratification, legal classification, residual-risk acceptance, or a
decision to close any gate. A picker response entered by Picard as a proxy was
explicitly corrected on issue #2 and is not human authorization. Merge,
specialist challenge, issue closure, and this document's existence cannot
substitute for a decision that requires Cyrus personally.

This snapshot integrates evidence already in the repository for the bounded
Phase 1A self-hosted application, contract-only seams, and future/gated
capabilities. It is not a runtime audit, penetration test, legal opinion,
accessibility conformance claim, production authorization, or Phase 1A exit
record. Its findings describe documentation and cited local evidence; they do
not establish live use, incidents, provider behavior, or harm.

## Scope and people potentially affected

The current documented user outcome is an owner-directed Open Loops flow:
prepare a task proposal, inspect it, confirm or reject it, and manage the
resulting task. The threat model and privacy baseline describe the deterministic
provider as the local/default path. BYOK is an optional, gated external
disclosure path; no paid live-provider evidence is claimed here. Connectors,
sharing/federation, managed hosting, public product/help hosting, support
intake, and third-party execution remain future/gated.

Potentially affected people include:

| People | Evidence-based exposure or benefit | Assessment boundary |
|---|---|---|
| Account owner and authenticated app user | Can manage owner-directed tasks and review proposed writes; task text may contain highly sensitive details. | Exact confirmation and tenant authorization have local evidence; informed, voluntary, accessible, and uncoerced consent is not proved by a passing test. |
| People named or described in user content, including household members, children, colleagues, and other non-users | Their personal or sensitive information may appear in ordinary task text even though they have no account or direct relationship with Andreja. | No authority, consent, or shared-tenant status may be inferred from mention or proximity. Current model/sharing exposure is denied by default; any exception needs a separate reviewed decision. |
| Self-host operator and other people with host, database, key, backup, or telemetry access | They control operational custody and may be able to inspect or lose locally stored content. | Application authorization does not protect against root/database/key custody. Operator duties do not confer user consent or permission to inspect another person's data. |
| Provider personnel or downstream subprocessors | Could receive content only if an external provider path is activated and data is deliberately sent. | No live paid call, provider-side retention behavior, or provider deletion evidence is claimed. |
| Developers, reviewers, and support responders | May encounter repository or diagnostic material during review or incident work; one mistaken attachment or paste to an existing issue/PR can disclose highly sensitive personal/non-user content. | Use synthetic, content-free evidence. Undeployed product intake and written prohibitions do not prevent that existing path; independent tested preventive barriers are not evidenced. Support/reviewer exposure is proposed High (12), not an observed leak. |

## Integrated impact findings

| Dimension | Proposed finding from current evidence | Evidence and confidence | Open decision or evidence |
|---|---|---|---|
| Human agency and control | Proposal-only assistant writes, exact review, tenant-scoped authorization, and server-side policy checks constrain the current task flow. Confirmation does not by itself prove comprehension, accessibility, voluntariness, or freedom from coercion. | `docs/threat-model.md`; `docs/privacy.md`; `docs/testing-matrix.md`. High confidence for documented contract; human outcome evidence is absent. | Accessibility and coercion review for supported paths; keep consequential action blocked on policy bypass, deceptive confirmation, or inaccessible review. |
| Data classes and purpose | The privacy inventory classifies identity, task, proposal, audit, provider-usage, and recovery data; free-text tasks can contain highly sensitive information. | `docs/privacy.md#data-inventory-and-classification`; current schema and local tests. High confidence that the documented data classes exist; no regulatory classification is made. | Reconcile data classes and purposes at any new recipient or data-flow boundary. |
| Provider exposure and authorization | Operator BYOK configuration and a provider/retention disclosure are documented. Operator enablement is not evidence that each affected user authorized a specific disclosure. | `docs/phase-1a/byok-security-privacy.md`; `docs/privacy.md#local-provider-and-model-exposure`. High confidence about the written contract; request-level user authorization evidence is absent. | Decide the proposed user-level authorization boundary below; prove it in UI/API and negative tests before external activation. |
| Non-user privacy and safety | A user may enter information about another person without that person's participation. The privacy baseline prohibits inferring consent, authority, or a shared tenant from mention or household status. | `docs/privacy.md#sensitive-life-household-and-non-user-data`; `docs/threat-model.md#abuse-and-misuse-cases`. High confidence for stated constraints; no representative user study is cited. | Preserve default denial for sensitive/non-user model exposure and sharing; require a separate approved review for any exception or household capability. |
| AI uncertainty, provenance, and misuse | Provider output is untrusted; the current provider can propose but not confirm a task. Semantic provenance is contract-only and in-memory, not a deployed graph or inference service. | `docs/threat-model.md#provenance-and-semantic-integrity`; `docs/privacy.md#provenance-audit-consent-and-sharing`; `docs/testing-matrix.md`. High confidence for documented local contracts; no live model-quality or harmful-output evidence. | Keep provider/semantic claims bounded to tested behavior; require adverse and provenance evidence before broadening model access. |
| Fairness, accessibility, and inclusion | Automated UI contracts cover labels, keyboard-native controls, reduced motion, and breakpoints, but manual assistive-technology and representative-user evidence remains open. | `docs/testing-matrix.md`. High confidence that manual evidence is marked blocked; no claim of equal outcomes. | Obtain human accessibility review and document supported-path gaps, owners, and remediation before launch. |
| Security, recovery, and service continuity | Local controls and negative tests exist for identity, isolation, proposal lifecycle, and portability; combined encrypted recovery/sign-in and genuine update/rollback evidence remain blocked. | `docs/threat-model.md#evidence-map`; `docs/phase-1a/evidence-44.md`; `docs/testing-matrix.md`. High confidence for listed evidence gaps; no production exposure or incident rate is inferred. | Keep missing recovery, trusted release, numeric objective, and high/critical-risk decisions as independent stops. |
| Financial and environmental sustainability | Phase 0 has a $0 cloud-infrastructure cap; the documented external model budget is zero absent separate approval. No spend or environmental measurement is established by this assessment. | `docs/adr/0000-plan-ratification.md`; `docs/phase-1a/byok-security-privacy.md`; `docs/cost-model.md`. High confidence for the written boundaries; no live consumption measurement. | Do not make a paid call or provision resources under this proposal. Any nonzero envelope needs its own explicit human approval and metering. |
| Stakeholders, incentives, and legal posture | Self-host ownership, provider/operator interests, and affected non-users may diverge. The current evidence does not resolve controller/processor roles, Washington or other jurisdictional applicability, lawful basis, or counsel conclusions. | `docs/charter.md#ethics-and-sustainability-impact-assessment`; `docs/legal/regulatory-applicability.md`. High confidence that no legal conclusion is established here. | Keep legal classification and qualified-counsel review separate. No jurisdiction or team location is treated as legal evidence. |
| Alternatives and evidence limits | The deterministic local path is the lower-disclosure option for this bounded task flow; the repository documents it as default. No comparative user-outcome or provider-quality study is cited. | `docs/privacy.md#local-provider-and-model-exposure`; test matrix. Medium confidence for relative disclosure; effectiveness comparison is not measured. | Reassess alternatives only with a defined user outcome and evidence; do not treat roadmap inclusion as approval. |

### Proposed accountability and evidence mapping

Each row below maps the corresponding open finding above to an existing
[threat-model owner and stop](threat-model.md#residual-risks-owners-and-stop-conditions).
The named lead is accountable for assembling evidence, not authorized to accept
risk, change runtime behavior, or close a gate. Minimum evidence is **required,
not supplied by this proposal**; absent evidence stays open. Indicators use
synthetic tests, reviewed artifacts, or privacy-safe safety reports, not new
monitoring or collection of people's content.

| Open finding | Accountable lead / existing owner group | Indicator / minimum evidence to resolve the gap | Pause and remediation mapping |
|---|---|---|---|
| Human agency and control | Jadzia Dax; Web, Public Site and User Experience, with Core Platform and Trust challenge | Bypassed, misleading, inaccessible, or coerced confirmation. Exact-diff/actor/version/expiry/policy/replay negatives plus human keyboard and assistive-technology review of supported confirmation/error/retry paths; separate coercion review. | Existing confirmation stop: block consequential actions on a failed path, correct the review/binding/accessibility gap, and re-evidence before resumption. Passing lifecycle tests alone do not resolve voluntariness. |
| Data classes and purpose | Deanna Troi; Trust, Security, Privacy and Legal | New class, purpose, recipient, or inventory mismatch. Reviewed flow-to-inventory map and minimization/access/retention negatives for each changed boundary. | Existing non-user/purpose and unauthorized-activation stops: do not activate the changed flow; correct inventory/policy and obtain required review/decision. |
| Provider exposure and authorization | T'Pol; Core Platform and Architecture, with Jett Reno/Quark and Deanna Troi | Operator-only enablement, stale recipient/scope disclosure, or calls after revocation. Before external activation: synthetic UI/API evidence of scoped affirmative opt-in, changed recipient/model/purpose/data ceiling/disclosure invalidation, and revocation stopping calls before credential resolution/network I/O; separately reviewed non-user basis. | Existing provider stop: keep endpoint/profile disabled until authorization, disclosure, terms, budget, and revocation are evidenced and required human decisions recorded. This is not a runtime implementation authorization. |
| Non-user privacy and safety | Deanna Troi; Trust, Security, Privacy and Legal, with Jett Reno/Quark for custody | Sensitive task text, unlocked-device access, operator custody, or credible coercion/stalking; no inferred consent. Synthetic current-task/device/custody abuse scenarios and independent safeguarding review identifying safeguards/gaps; operator-independent access/revocation/export/recovery proof before household features. | Current coercion/operator stop: pause affected use and review safety/custody before resumption. Future household/sharing amplification stays gated. Current High (15) candidate remains unresolved under the High/Critical exit stop. |
| AI uncertainty, provenance, and misuse | Seven of Nine; Core Platform and Architecture / First-party Skills and Developer Ecosystem | Output treated as authority, unreviewed inference, or provenance loss. Adverse typed-tool/confirmation/purpose fixtures and source/review/correction/retraction evidence for any proposed semantic expansion; live quality evidence remains absent. | Existing direct-mutation/confirmation and sensitive-inference stops: block the affected action/expansion, correct policy/provenance and rerun adverse evidence; contract-only semantics do not authorize deployment. |
| Fairness, accessibility, and inclusion | Jadzia Dax; Web, Public Site and User Experience, with Data for evidence and Rai for challenge | A supported review path cannot be understood or operated with keyboard/assistive technology, or outcomes differ without explanation. Human supported-path accessibility review with reproducible gaps/remediation, plus representative-user evidence before equal-outcome claims. | Existing inaccessible-confirmation stop blocks affected consequential actions; launch accessibility gate remains open. Correct the affected path and repeat human review; automated markup is not conformance proof. |
| Security, recovery, and service continuity | Jett Reno; Platform Operations, Hosting and FinOps, with Core Platform and Trust challenge; Guinan/Tuvok/Deanna Troi for review artifacts | Recovery/sign-in failure, missing trusted release/rollback proof, isolation failure, telemetry content, or suspected sensitive issue/PR attachment/paste. Existing #44 combined encrypted recovery/sign-in, independent-root hosted signature/second-revision rollback, approved numeric objectives, and new-instrumentation negative evidence. For review artifacts, path-specific synthetic evidence must demonstrate any claimed independent preventive barriers before crediting them; none is supplied here. | Existing compromise/recovery/release/telemetry stops apply: isolate/contain, rotate where needed, restore/correct and re-evidence. Stop affected artifact sharing on suspected exposure; restrict assessment without public content copies and use synthetic/content-free re-evidence. Telemetry and support/reviewer exposure are each High (12); all unresolved High/Critical candidates block release/Phase 1A exit. Neither canaries nor documented duties count as independent preventive barriers. |
| Financial and environmental sustainability | Quark; Platform Operations, Hosting and FinOps | Unknown/nonzero spend or unmeasured consumption claim. Separate approved numeric envelope and synthetic budget-stop/metering evidence before paid activation; defined measurement boundary before environmental claims. | Existing unknown/unapproved-spend stop: no paid call or provisioning; correct metering/limits and obtain the separate human decision. Phase 0 remains $0 cloud infrastructure. |
| Stakeholders, incentives, and legal posture | Sarek; Trust, Security, Privacy and Legal | Unresolved affected-person authority, recipient role, jurisdiction, or lawful/ethical purpose. Exact-flow classification with qualified-counsel evidence where required, stakeholder/non-user impact review, and designated human decision. | Existing non-user/purpose stop plus independent plan/legal gates: keep the affected activation gated, reconcile roles/purpose and counsel gaps; neither location nor specialist review substitutes for Cyrus's decision. |
| Alternatives and evidence limits | Seven of Nine; Core Platform and Architecture | Claimed benefit/quality exceeds the tested local contract or a proposed option adds egress without an evidenced outcome. Defined task outcome and synthetic adverse comparison before efficacy claims; reviewed recipient/data boundary before external comparison. | Existing provider/unauthorized-activation stops: retain the local/default boundary, withdraw unsupported claims, and re-evidence the proposal without activating alternatives. |

Tuvok, Deanna Troi, and Rai must independently challenge this exact revision.
Cyrus's assessment, authorization-boundary, and residual-risk decisions remain
separate and pending; these mappings do not convert advisory findings into
accepted policy or imply human ratification.

## Proposed BYOK authorization boundary

The existing contract describes a deliberate **operator** choice to configure
BYOK and an authenticated surface that discloses the selected provider/model and
operator-authored recipient/retention terms. Those controls do not demonstrate
that another signed-in user has authorized the disclosure, and they cannot
establish consent or authority for a non-user whose information appears in a
request.

For specialist and human review only, the proposed boundary is:

1. Operator configuration is necessary to make a provider profile available,
   but is not sufficient authority to send a user's content.
2. Before the first external request, the affected signed-in user must receive an
   accurate, accessible disclosure of recipient, model, purpose, data classes,
   provider retention limits/unknowns, and available controls, then affirmatively
   opt in within a defined scope. No default-on or silent enablement is proposed.
3. A change to endpoint/recipient, model, purpose, data ceiling, or material
   retention disclosure invalidates the prior authorization and requires fresh
   review before further egress.
4. The user-facing control must make revocation understandable and stop future
   calls before credential resolution or network I/O. Provider-side deletion or
   recall is not promised. Existing credential-file revocation is evidence for
   stopping access to the secret, not evidence of a complete user-consent UX.
5. A user cannot grant authority on behalf of a non-user merely by entering
   their information. Sensitive or non-user data remains denied by default
   unless a separately reviewed, approved purpose and data boundary establishes
   an acceptable basis.

These are proposed product and trust-boundary requirements, not descriptions of
current implementation. The runtime, UI, stored consent, invalidation on
recipient/scope changes, and affected-person handling have not been evidenced by
this proposal. Tuvok and Deanna Troi must challenge the boundary; Cyrus must
separately decide whether to approve it before implementation or external
activation.

## Evidence and stop conditions

This proposal cites repository artifacts and their stated local evidence only.
It does not add test results, user research, provider terms, incident data,
legal conclusions, or approvals. Before any gate can close, the exact revised
threat/privacy artifacts and this assessment need independent exact-revision
challenge, each material claim needs linked evidence or a named gap, and Cyrus
must separately decide the assessment and residual risks.

Pause the affected work or external flow if authorization scope, recipient,
purpose, data class, non-user impact, or revocation cannot be established; if a
high/critical residual threat remains unresolved; if evidence contradicts a
claim; or if a required reviewer or human decision is missing. This proposed
assessment does not waive any existing plan, ADR, legal, cost, evidence, or
release gate.
