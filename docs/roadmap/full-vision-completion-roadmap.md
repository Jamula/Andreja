# Andreja: full-vision completion roadmap

> **Purpose:** An execution-ready roadmap for the entire documented Andreja vision, including the company, product, platform, ecosystem, UX, accessibility, testing, performance, operating model, and return on investment.
>
> **Requested outcome:** Attempt completion within **five days or less** using autonomous AI agents with minimal human supervision. This is a fixed target, not a forecast or a promise.
>
> **Evidence snapshot:** Repository `Jamula/Andreja`, source commit `7923b4d67f17f402c3e65d2c81dd1a981fa456e2`; GitHub status refreshed on 2026-11-08 at approximately 23:20 UTC. Recheck volatile facts before execution.
> **Authority:** This is a proposed supplemental roadmap. [`docs/plan.md`](../plan.md) and accepted ADRs remain authoritative; this document does not amend them, authorize implementation, replace a human decision, approve spend, approve release, or declare any phase complete.

## Reading guide and status vocabulary

1. Read **Executive assessment**, **Decision queue**, **Critical path**, and **Five-day execution attempt** for the leadership view.
2. Read **Delivery operating system**, **Phase-by-phase roadmap**, and the two **catalog matrices** for the work program.
3. Read **Acceptance scenarios**, **UX**, **validation**, **performance**, **ROI**, and **company readiness** for the detailed evidence expected from each workstream.
4. Read **Issue reconciliation**, **risks**, **assumptions**, and **sources** for provenance and counterarguments.

Status terms are deliberately not interchangeable:

- **Accepted** means an identified human accepted a specific scope and version, not every later edit or operational activity.
- **Implemented** means code or a local artifact exists; it does not mean current release, security, legal, user, or production evidence exists.
- **Contract-only** means a fixture or interface exists without an operational feature or live integration.
- **Historical evidence** applies only to its named source revision, configuration, artifact, and environment.
- **Proposed** means a design, policy, budget, or roadmap is not yet a governing approval.
- **Blocked** means a named prerequisite, external party, independent review, or human decision is missing; a closed GitHub issue does not remove that blocker.
- **Operationally complete** means the exact outcome, evidence, human approvals, support capability, and applicable launch gates all pass. Documentation, mocks, and automated issue closure alone do not qualify.

## Executive assessment

**The five-day target is not currently a credible commitment for the full vision.** The current repository contains a substantial self-hosted task slice and useful platform contracts, but the vision also requires a trusted owner client, managed service, real integrations, three-person travel, multiple mobile platforms, a marketplace, 19 first-party skills, 23 channel categories, substantial company operations, and numerous external and human-owned approvals. Parallel AI work can reduce coordination latency; it cannot establish that sufficient capacity exists, create third-party approvals, provide qualified legal opinions, or replace user and assistive-technology evidence.

Two requirements currently conflict. Cyrus explicitly directed owner-only decryption with no operator/provider access or plaintext recovery, and stated that user data must not be accessible to other humans ([decision record](https://github.com/Jamula/Andreja/issues/2#issuecomment-6115465929)). The documented vision also calls for cross-person group travel and external providers. The proposed [ADR 0012](https://github.com/Jamula/Andreja/blob/7923b4d67f17f402c3e65d2c81dd1a981fa456e2/docs/adr/0012-owner-device-content-encryption.md) explains one owner-device architecture and prohibits plaintext external calls and live sharing with another human; it is **not ratified**. Agents must not invent a sharing or provider exception. The human must resolve the conflict through separate, explicit, reviewed decisions before affected real-data behavior can proceed.

**Keep the historical gate and the requested end state distinct.** The accepted #66 Phase 1A checklist does not itself require the proposed ADR 0012 client; the later owner-only direction adds an unresolved full-vision requirement that current code does not satisfy. Report evidence for the accepted checklist separately from evidence for a privacy-aligned product; neither reclassifies ADR 0012 as accepted. A compliant owner-only design can be reviewed and, if separately approved, implemented under the existing no-sharing direction without first approving a sharing exception. The original and revised trust boundaries and any phase-exit interpretation require the applicable human decision, not an agent's unilateral rewrite.

The fastest responsible approach is to run **all authorized, independent synthetic/local and paper work in parallel**, establish the human and external decision queue immediately, integrate around stable contracts, and use exact-revision evidence. Attempt the complete vision in the target window only if the required approvals, capacity, real devices, participants, independent reviewers, third-party access, budgets, and release evidence actually arrive. Otherwise report each unfinished aspiration and its owner as **blocked or incomplete**, not as done or silently out of scope.

### What exists and what does not

| Area | Observed baseline | Meaning for this roadmap |
|---|---|---|
| Application | .NET 10 modular application, Blazor UI through a typed API, local/passkey identity, PostgreSQL adapters, task/proposal flow, OpenTelemetry controls, deterministic provider, and OpenAI-compatible BYOK transport exist. | Build on the working slice; do not describe the entire product as a skeleton or as complete. See `src\Andreja.AppHost\Program.cs`, `src\Andreja.AppHost\Components\Pages\Home.razor`, and `src\Modules\Andreja.Modules\OpenLoops`. |
| Task behavior | The current UI prepares a single task proposal, shows the canonical before/after, confirms, lists, completes, exports, and deletes tasks. Task/proposal idempotency and authorization have tests. | The full task product, email automation, adaptive control plane, and group travel are not demonstrated by this slice. |
| Privacy | Task title/details are server-side strings; the assistant may send submitted content to a configured provider; current portability can process plaintext server-side. | Existing encryption-at-rest and server-side behavior do **not** prove the human's owner-only requirement. Do not enter real personal data under an unproven boundary. |
| Skills, channels, semantic profile, federation | Skill/channel authorization hosts and semantic/grant/consent/peer-envelope conformance fixtures exist. Some evidence is explicitly in-memory and contract-only. | A manifest, mock transport, or signed-envelope test is not a live channel, durable semantic graph, peer network, or marketplace. |
| Website | A separate local public-site generator and tests exist. The checked-in `docs\adr\0008-public-website-artifact-boundary.md` at the snapshot still says **Proposed**. A later [#93 comment](https://github.com/Jamula/Andreja/issues/93#issuecomment-6728305706) reports a narrow decision and additional local work; its exact merged artifact and decision provenance must be reconciled before using it. | Local generation or `noindex` is not a private-access control, public release, or WCAG conformance claim. Publication remains gated. |
| Quality and releases | Recent `main` .NET Validation and OCI Supply Chain runs succeeded at the snapshot commit. Historical runtime evidence exists for other exact revisions. | Neither an ordinary green OCI push nor old local results prove a signed protected-tag release, current PostgreSQL runtime coverage, restore, rollback, or launch approval. |
| Backlog | Snapshot: 70 issues, 65 closed and 5 open. Open: [#6](https://github.com/Jamula/Andreja/issues/6), [#38](https://github.com/Jamula/Andreja/issues/38), [#93](https://github.com/Jamula/Andreja/issues/93), [#158](https://github.com/Jamula/Andreja/issues/158), and [#211](https://github.com/Jamula/Andreja/issues/211). One open PR, [#255](https://github.com/Jamula/Andreja/pull/255), was observed. | Counts are a point-in-time inventory, not a completion measure. Later milestones also lack a complete issue inventory. |
| Governance | The exact accepted plan and cost/operating baselines are recorded; the charter and ADR 0012 remain proposed in the checked-in records. The repository currently reports **public**, despite older private-repository wording. | Separate historical plan text, actual visibility, explicit decisions, current source, and current access controls. Do not describe a proposed charter as already ratified. |
| Agent operations | The current Squad team state reports a generic coordinator and no listed members; state health reports `FSStorageProvider`. `docs\operations\squad-rebuild-prompt.md` describes a desired Seven-led roster, but is a recommendation/approval boundary rather than proof those named agents are active. | Use functional roles in this roadmap. Verify real agent availability, durable state, permissions, costs, and ownership before dispatch; do not assume the former Star Trek roster or two-layer state is operational. |

### Problem and evidence limits

**Problem hypothesis:** A person trying to coordinate tasks, information, commitments, and collaboration across tools needs a trustworthy assistant that reduces effort without surrendering control or exposing private context. The project expresses that goal, but frequency, severity, willingness to adopt, user time savings, market size, and ability to operate every proposed integration are not yet established by representative user behavior. **Confidence in the scope of the project is high; confidence in market demand, ROI, and five-day feasibility is low.**

Problem-solution-fit status: existence, severity, frequency, willingness, and broad-market timing are **unvalidated** without appropriately consented user evidence. Technical solvability is **conditional** on the privacy decision, independent cryptographic review, external access, and evidence. Customer Zero is valuable but does not establish market demand. Opportunity size and commercial ROI are **not currently scorable**; collect the measurements below rather than inventing estimates.

## Decision queue: smallest necessary human interventions

Prepare decision packets concurrently, but obtain each distinct authorization separately. Present one focused question at a time; a choice on one row is not approval of any other row. Preserve already-settled decisions rather than asking for them again.

| ID | Decision and options | Responsible preparers and evidence | What it unblocks / does not unblock |
|---|---|---|---|
| D1 | **Owner-only versus cross-person sharing.** Preserve the existing no-other-human boundary and leave Group Travel/federation blocked; or consider a newly reviewed, narrowly scoped, explicitly consented sharing exception; or defer the decision. | Privacy and security leads produce data-flow alternatives, affected-person consent, revocation limits, metadata leakage, and conflicts with existing direction; Cyrus decides. | Only an explicit new decision can change the existing boundary. A cryptographic implementation idea is not a sharing authorization. |
| D2 | **Owner-client architecture.** Accept an independently reviewed exact amendment/ADR 0012 revision, request a different design that still honors owner-only custody, or stop. | Architecture, cryptography/security, privacy, UX/accessibility, operations, and quality challenge the exact client, key custody, migration, recovery, release-root and trust assumptions. | Unlocks only the specifically approved architecture and scope. No acceptance follows from merge or from the historical plan hash. |
| D3 | **External model/provider exposure.** Maintain no plaintext external exposure; or consider a separate, exact, privacy-consistent provider exception and risk decision. | Provider-specific legal/terms, privacy, security, retention, egress, cost, user disclosure, isolation and rollback packet; Cyrus decides. | Separate from D1 and D2. ADR 0009 accepts Copilot's phase placement only, not a provider, network call, canary, account, spend, or real-content use. |
| D4 | **Development-agent authority and AI-credit envelope.** Identify the existing approved accounting window and usage boundary; approve a specific exception only if additional consumption is needed. | FinOps reconciles provider usage statements, current sessions, credit and cash accounting, capacity, stop conditions, and least-cost alternatives. | Enables only the approved agent usage. The old uncapped research exception is not blanket authority for a new run. |
| D5 | **Model/BYOK spending envelope.** Set an exact funding mode, billable units, limits, hard stop, owner, and reconciliation for any live model use. | FinOps plus provider, privacy, security, and quality evidence. | No paid external model call until approved. Local deterministic/fake tests remain available within existing authorization. |
| D6 | **Cloud/managed-spike envelope.** Choose whether to approve specific accounts/resources, region, cap, quota, expiry, teardown, and operator. | Operations and FinOps prepare a priced, bounded, reversible proposal; legal/privacy/security review service-specific terms and data flows. | Phase 0 remains $0 and no provisioning, including free tiers/trials, until an applicable separate decision. A budget is not a launch approval. |
| D7 | **Professional-services and third-party commitments.** Decide whether to authorize any required legal, trademark/domain, device, app-store, or other paid engagement; each has its own scope and cap. | Legal research and FinOps prepare the task, actual provider terms, conflict checks, and cost. | Agent research is not counsel advice; no contract, purchase, or legal representation follows from roadmap approval. |
| D8 | **Numeric operating policy.** Approve or amend specific SLOs, RPO/RTO, retention and deletion limits, incident escalation, and model-spend thresholds after measurement. | Quality, operations, privacy, and FinOps supply baselines, queries, sample counts, uncertainty, and cost implications. | Required for Phase 1A exit and later stage gates. Existing example SLOs are candidates, not approved targets. |
| D9 | **Release signing and trust root.** Authorize a qualifying protected-tag build and the independently held trust anchor; separately authorize the distinct second signed revision needed for update/rollback. | Operations/security produce exact commits, workflow identity, issuer, tag/ref, artifacts, independent root custody, offline verification, and rollback plan. | Two distinct approved, signed immutable revisions plus recovery evidence are needed; a green unsigned push does not substitute. |
| D10 | **Charter and public claims.** Decide on the exact charter revision and ADR 0006 instrument after prerequisites and independent challenges; separately decide applicable public wording and publication. | Strategy, privacy, legal, responsible AI, security and evidence owners map exact versions and unresolved risks. | #3 is closed, but checked-in charter/ADR 0006 remain Proposed. Human-attested appointment of the independent recipient is a scoped completed prerequisite, not proof the reporting route was tested or activated. |
| D11 | **Feedback evidence authorization.** First resolve the outstanding evidence behind #155–#157; then decide the exact synthetic-only staging authorization in #158. Final readiness and launch are another separate decision. | Legal, privacy, security, support, quality, operations and FinOps prepare exact versions, sender/allowlist, resources, expiry, hard stops, teardown and 14 scenarios. | #155–#157 were closed administratively/specification-only despite missing evidence. Their closure does not authorize collection, sender setup, staging, spend, or launch. |
| D12 | **Real data, research, and people.** Decide whether and when real Customer Zero data, representative participants, three consenting adult tenants, human assistive-technology testing, and operator/support access are authorized. | UX/research/privacy/security propose recruitment, consent, synthetic alternatives, retention, compensation if any, withdrawal, and safe evidence. | Synthetic fixtures do not prove real usability or market demand. Enrollment and any spending remain separately authorized. |
| D13 | **Real provider, mobile-store, and regulated-skill activation.** Decide exact providers, account types, jurisdictions, use cases, safeguards and launch stages only after qualified external review. | Channel, mobile, health, finance, legal, privacy and security leads provide current terms, technical conformance, approvals, support and costs. | Provider OAuth/app review, store approval, qualified-counsel review, and licensed professional input are external dependencies, not agent-controlled milestones. |
| D14 | **Final proceed / extend / de-scope / stop.** Decide separately for each proposed phase exit and launch with current artifact-bound evidence and individual residual risks. | Independent reviewers prepare unresolved risks and alternatives; Cyrus or the separately qualified human decision-maker decides within their authority. | Roadmap review, issue closure, a green check, or an agent recommendation does not confer release authority. |

**Already recorded, not silently reopened:** #73 records acceptance of exact plan, financial-control, and operating-model baselines; #66 accepts a bounded Phase 1A architecture packet; #74 accepts provider **phase placement**. The private counsel-reference verification and its scoped outcome were attested as complete in [#6](https://github.com/Jamula/Andreja/issues/6#issuecomment-6115340637); do not request confidential details or infer a favorable disposition beyond the private outcome's actual scope.

### Decision timing and external-readiness protocol

The program coordinator maintains one decision register with the accountable human, packet preparer, exact artifact/version, reviewer and conflict check, prerequisite evidence, earliest useful decision point, deadline derived from the affected critical path, current status, decision link, expiry and next action. Present decisions as separate focused questions; a consolidated status report must not turn them into one blanket approval. The dates and reviewer availability are **unknown until actually confirmed**.

| Prepare immediately | Decision needed before | If the decision or resource is unavailable |
|---|---|---|
| D1: map the existing no-other-human instruction against each proposed cross-person flow, including exact disclosed fields, consent, revocation and whether copied information can actually be recalled. | Any real trip participant, federation exchange or other-human disclosure. | Continue synthetic contracts only; label the real Group Travel, household and federation outcomes blocked. No exception is inferred. |
| D2: request independent cryptography, privacy, architecture, operations and accessibility review of the exact owner-client design. Specify standard vetted AEAD and library choice, nonce/record format and rotation, separate identity/content keys, device enrollment and revocation, recovery-secret custody, independent client-release trust root, downgrade/tamper rejection, metadata, migration and irreversible loss UX. | Implementation, migration or client distribution under the proposed amendment. | Continue authorized design/threat-model work; report current plaintext paths and user data as not meeting the desired boundary. The owner-only path does not require D1 to authorize sharing. |
| D3/D5/D13: prepare provider-by-provider content flows, legal/terms, recipient and account type, scopes, applicable data restrictions, billing, hard stop and fallback. | Each real external plaintext/provider/model call, account grant or provider activation. | Use deterministic fakes and manual/document modes; a provider response or unsupported account is not proof of an approved integration. |
| D4/D6/D7: reconcile current authorized agent usage, credits and cash separately; identify actual local resources and any proposed cloud or professional-service request, with owner and teardown. | Additional agent usage beyond its actual authorization, any cloud account/resource including a free tier, or each external engagement/purchase. | Keep work within already authorized capacity; stop only affected work when authority or capacity is exhausted. Phase 0 remains no cloud provisioning. |
| D8/D9: prepare measured candidate limits, exact signed commits, protected workflow identities, independent trust-root custody, a second distinct signed revision, recovery and rollback procedure. | Phase exit/risk acceptance and each separately approved tag/release action. | Run permitted local evidence and local signed-artifact preparation; do not call an unsigned push, an unverified root or one signed revision sufficient. |
| D10/D11: reconcile the charter, public claims, exact feedback specifications, outstanding evidence, #158 synthetic-only authorization and a later independent launch decision. | Public claims/publication, feedback staging, sender setup, collection or launch, according to their respective gates. | Prepare local artifacts and decision packets; #155–#157 administrative closure does not clear missing operational evidence. |
| D12: identify the actual consent process, independent accessibility reviewers, assistive-technology/device combinations, candidate participants, accommodations and any payment approval. Confirm availability without presuming anyone is recruited. | Each real-user, real-data or human testing activity and any related compensation. | Run automated/synthetic checks and request authorization; lack of human assistive-technology evidence remains an exit blocker where the checklist requires it. |
| D13/D14: identify qualified counsel, vendor/API and app-store review prerequisites, actual response windows, human decision availability and support capacity; do not submit applications, sign agreements or incur costs without separate authority. | Each relevant provider/store launch, regulated claim and final phase-specific proceed/extend/de-scope/stop decision. | Record the external wait and owner; continue independent work but do not claim operational completion. |

For any needed decision, notify the accountable human when its packet is complete or an external prerequisite is blocked, rather than asking them to supervise routine implementation. At each target window, show the earliest required decisions, choices, consequences and exact version; keep one owner responsible for escalation and revalidate expired approvals before use.

## Critical path and dependency rules

The following is a dependency graph, not a duration estimate:

```text
Scope, live-state, agent/cost admission, evidence inventory
  +--> synthetic local tests, interface specifications, UX prototypes,
  |    catalog charters, legal/provider research, benchmark designs
  |
  +--> D2 independently reviewed owner-client decision under existing
  |    owner-only direction
  |      +--> verified client/key lifecycle + migration and negative proofs
  |      +--> aligned task/provider/portability flows and recovery UX
  |
  +--> D1, only if changing the existing no-other-human boundary
  |      +--> separately reviewed compatible sharing design and authorization
  |      +--> real Group Travel and later federation, if approved
  |
  +--> current-revision Phase 1A tests + applicable OCI security gates
         +--> approved signed artifact A + independently verified root
         +--> encrypted database + Data Protection keys + restored sign-in
         +--> separately approved signed artifact B + update AND rollback
         +--> numeric limits + independent challenge + human Phase 1A decision

Accepted Phase 1A decision + privacy-aligned milestone under the applicable
approved design + separate Phase 1B budget, legal, provider and data approvals
  +--> managed tenant isolation and provider qualification
  +--> six real email acceptance scenarios
  +--> five real three-tenant travel scenarios, if D1 permits
  +--> separately authorized feedback/public/help surfaces
  +--> later skills, connectors, federation, marketplace, native mobile,
       analytics, finance/rewards and health

In parallel: reproduce or recover #211 diagnostics; resolution of that historic
issue is a full-backlog requirement, not a separately enumerated #66 exit item.
The accepted checklist still requires the candidate's actual scan/risk evidence.
```

- Parallelize designs, synthetic conformance, threat models, accessibility prototypes, help drafts, and provider qualification research when their prerequisites permit. **Do not** run real-data, live-provider, provisioning, staging, public, payment, or release actions merely because parallel implementation is possible.
- The accepted plan's phase placement remains the baseline. Reordering work across phases or changing material trust boundaries requires the appropriate issue/ADR/plan amendment and, where required, exact-hash human re-ratification under ADR 0000. Do not silently rewrite the plan.
- Skills and channels can be drafted in parallel, but their lifecycle stages, provider qualification, privacy/security design, supporting infrastructure, and same-PR catalog status updates remain mandatory.
- Every outcome has one accountable owner, one authoritative contract or artifact, named collaborators, explicit dependencies, an exact source/artifact revision, a verifier distinct from the author, stop conditions, and a user-visible definition of done.
- A blocked workstream can deliver a **decision-ready or synthetic artifact**, but cannot claim the blocked operational outcome. Record externally deferred work separately from work fully completed inside the target window.

## Five-day execution attempt

The following are five fixed target windows requested by the user. They are **not estimates** that the work or third-party decisions will finish within them. Start as many ready workstreams as verified capacity and approved usage allow; tasks carry forward if prerequisites are missing. Day 5 is a truth-check, not an instruction to weaken a gate.

| Target window | Program coordination | Parallel outputs and evidence | Mandatory control point |
|---|---|---|---|
| Day 1 | Freeze the source/evidence snapshot; reconcile all open work, active sessions, existing PRs, decision versions and agent budget; identify one owner per contract. Queue D1–D14 with the smallest focused human prompts. | Start owner-client alternatives, cryptography/privacy review, local application/OCI inventory, #211 diagnostic preparation, UX journey inventory, all skill/channel charters, provider/terms research, baseline metric design, company/legal gap analysis and synthetic test design. | No real data, external accounts, cloud, public preview, provider calls, spend, or trusted-tag action without the exact applicable approval. Existing open #211 awaits access details for the authorized dedicated disposable Linux amd64 runner. |
| Day 2 | Integrate approved contract choices; keep unresolved branches isolated. Maintain decision ledger and avoid duplicate writers. | Build and test authorized synthetic foundations: owner-client prototype only after its design authorization, tenant/policy seams, deterministic assistant/skill contracts, reusable connector simulators, local site/help, mobile evaluation, accessibility prototypes, benchmark harnesses, legal/provider decision packets, and delivery pipeline evidence. | A successful mock, local client, or agent-produced review is not a human acceptance or permission to expose content. |
| Day 3 | Resolve integration conflicts and current-head review; recheck every input and decision. Dispatch independent reviewers who did not author the artifact. | Run exact-revision unit/architecture/PostgreSQL/browser/security/telemetry checks on safe infrastructure; attempt approved signing and clean key-aware recovery; advance email/travel synthetic scenarios, later skills and channels, company operations and marketplace/mobile design. | If owner-client, release signing, database/key restoration, provider or privacy gates fail, affected work stays blocked; do not pretend parallel downstream work is operational. |
| Day 4 | Assemble candidate release/evidence packets, with individual discrepancies, cost statements and human decisions visible. | Complete only separately authorized provider/managed/staging/device tests; perform update/rollback with a genuinely distinct signed candidate; run supervised accessibility and user research where available; measure benchmarks and ROI with consent; reconcile feedback, site, support, marketplace and regulated-domain evidence. | Third-party approval, qualified counsel, real device and participant evidence cannot be manufactured or substituted by synthetic output. |
| Day 5 | Verify every phase, catalog item, issue, release artifact, contract, human decision, and actual service condition against the completion ledger. | Produce exact pass/fail/blocked evidence, actual user outcomes, cost/ROI and support capacity; remediate only within authorized scope; present per-gate proceed/extend/de-scope/stop decisions. | Declare **full completion only if every required aspiration and applicable gate actually passes**. Otherwise preserve owner, next action, blocker, and any unspent/unused authorization; do not call a partial release the complete vision. |

### Five-day feasibility and capacity check

The target needs an observable capacity model, not an assumed agent count or an invented duration:

1. **Inventory actual resources:** eligible agent/session types and concurrent slots; required skills; independent reviewer availability and review capacity; CI queue and runner/database isolation; approved devices, human research/assistive-technology participants, provider accounts and decision-maker windows. Record current authorization, measurement source and unknowns for each. An empty historical Squad roster does not prove that other agent tooling is unavailable, and a listed agent type does not prove usable capacity.
2. **Decompose ready work:** break W01–W25 into single-owner, independently verifiable tasks, identifying sequential dependencies, contested files/contracts, exact evidence, reviewer, human or external wait, machine/CI requirements and cost. Record observed duration ranges only from comparable completed work; where no defensible measurement exists, duration is **unknown**, not zero.
3. **Calculate the optimistic lower bound:** for the actual dependency graph, the critical-path lower bound is the longest sequence of known minimum task and mandatory wait durations. In parallel, total measured work must fit available authorized worker/reviewer/CI capacity after non-overlapping assignments and integration overhead. Unknown durations, queue availability or external responses make the bound incomplete. Neither a favorable lower bound nor abundant nominal agents proves a five-day finish.
4. **Update with measured throughput:** at each window, compare finished, independently verified outcomes with remaining ready/blocked tasks, actual elapsed time, retries, review/merge queue, usage and authorization. Show optimistic and downside scenarios only when their inputs are measured and disclosed. A five-day commitment requires a feasible resource-constrained schedule and confirmed external dependencies; until then the full-scope date remains **unvalidated**.
5. **When capacity or decisions fall short:** first protect the self-host task/identity/security/recovery path, exact evidence, the privacy decision and release-critical reviews; in parallel, finish independent local/synthetic catalog, UX, test, company and decision packets where permitted. Do not silently drop later goals. Present the actual unfinished outcomes and choices under D14; any narrower release needs an explicit separate decision and is not full completion.

No percentage capacity cutoff, phase duration, reviewer turnaround, external approval date or numeric spend cap is assumed in this roadmap. Named role assignments become real only after a supported agent or qualified human accepts them and capacity and authority are confirmed.

### Earliest failure conditions for the full target

The complete target is blocked if any of the following remains unresolved: D1/D2 privacy conflict; required human decisions or numeric budgets; lack of independently trusted owner-client/release artifacts; no second signed revision or clean key-aware restoration; unavailable isolated runner/database/device; unavailable legal review or required provider/store consent; missing real three-adult/user/assistive-technology participation; incomplete marketplace, regulated-domain, support, or business capabilities; or insufficient verified agent/CI capacity. A user can choose a separately labeled interim milestone, but an interim milestone is **not** full-vision completion.

## Delivery operating system for autonomous agents

### Admission, ownership, and coordination

1. A coordinator verifies current GitHub issues/PRs, active sessions/worktrees, source/base revision, branch protections, queue and permitted usage. Do not dispatch a second owner for an active issue or shared file. Because the current Squad roster is empty, first verify supported agent types and the actual state backend; apply any roster rebuild only with its own approval.
2. Give each bounded task a named functional owner, user outcome, exact source revision, files/contracts owned, permitted actions, acceptance tests, evidence artifact, dependencies, cost/authority boundary, escalation trigger, and stop condition. Agents use isolated work surfaces; shared interfaces have a single integrator.
3. Partition by stable contract rather than by arbitrary file count. Useful parallel lanes are listed below. No agent receives secrets, personal content, production credentials, or unrestricted destructive permissions just to improve speed.
4. Use synthetic, clearly marked data. Keep prompts, recovery material, mailbox content, participant data, and credentials out of GitHub, telemetry, test fixtures, agent memory and public reports. Separate personal, product-metering, and company-financial data.
5. After an independent task produces a candidate, its reviewer checks the **exact current revision** and meaningful negative cases. A rejected author does not self-approve or bypass the repository's reviewer-lockout rules. Integration owners rerun the smallest affected tests, then the release evidence suite.
6. [#104](https://github.com/Jamula/Andreja/issues/104) was closed **not planned**; automated review-completion enforcement was not implemented, and repository auto-merge was preserved. Do not claim a native review gate exists. Do not request/enable auto-merge until the required independent review is complete for the exact head, all review threads and CI pass, and the human or approved workflow authorizes that merge. Re-evaluate after any head change; do not change repository settings as a shortcut.
7. Respect cost, authentication, rate, capacity, shared-host, branch, and artifact protections. On failure, attempt safe, materially different recovery paths; escalate promptly when only a human or external party can act. Never fabricate a success, repeat an expired authorization, or weaken a gate.
8. Maintain a content-free progress ledger with work-item ID, source/head, owner, stage, dependencies, exact evidence, separate decision status, blockers and next action. Use the actual configured state mechanism; never claim an unattended monitor or a durable handoff exists unless verified.

### Suggested independent lanes

| Lane | Accountable functional role | First deliverables | Integration boundary / independent challenge |
|---|---|---|---|
| Program and evidence | Coordinator / evidence custodian | Dependency graph, exact decision register, backlog coverage, artifact index, blocked-work aging | Does not approve technical, legal, risk, spend, or release decisions. |
| Privacy and cryptography | Security architecture lead | Owner-client decision alternatives, key hierarchy, metadata map, migration/recovery threat model | Independent cryptographic, privacy, operations and accessibility review before implementation/activation. |
| Self-host platform | Operations lead | Current-head OCI, #211 diagnostic plan, signed provenance, clean DB/key restore, offline and rollback evidence | Runs only on a verified disposable environment; no shared-database deletion or unapproved tag. |
| Identity and tenancy | Identity/application lead | Passkey, account recovery, tenant/principal policy, database constraints, portability contracts | Cross-tenant/authorization negative and recovery evidence reviewed by security and quality. |
| Assistant, skills and semantic model | Assistant/skills lead | Provider-neutral sessions, tool manifests, policy intersection, explainable provenance, synthetic evaluation | No provider SDK leakage, ambient credentials, silent side effects, or unapproved real-model exposure. |
| Channels and email | Channel lead | Provider qualification, topology ADRs, Gmail/Microsoft simulators, account policy, disconnect/purge | Separate provider grants and tokens; live account access only after the relevant authorization. |
| Collaboration and federation | Sharing lead | Proposal/grant/consent contracts, minimum-disclosure synthetic vectors, trip UX and interoperability spec | Real sharing remains blocked pending D1; contract conformance is not operational sharing. |
| UX and accessibility | Experience lead | End-to-end journey map, interaction states, accessible designs, help, user-research plans | Independent human assistive-technology and representative-user evidence is mandatory for applicable launch claims. |
| Quality and performance | Quality lead | Test inventory, scenario matrix, regression harnesses, load/benchmark protocol and evidence index | Must distinguish deterministic, live, historical, skipped, unavailable, and human evidence. |
| Mobile | Mobile lead | MAUI/native comparison, trusted-client distribution, device capability, offline/sync and accessibility plans | Owner-device trust decision comes first; signed app/store/device access remains independently gated. |
| Website, support and community | Support/communications lead | Versioned local help, claims inventory, intake/runbook designs, incident and support capacity | #6, #93 and #158 remain separate publication/feedback gates. |
| Company, legal and FinOps | Product/FinOps/legal research leads | Three ledgers, costs, outcome/ROI framework, counsel questions, vendor, IP and sponsorship packets | Qualified counsel and Cyrus decide; no spending or legal/public representations by agents. |
| Marketplace and ecosystem | Ecosystem lead | Publisher onboarding, API/SDK, remote execution isolation, fair ranking and appeals contracts | No untrusted in-process code, payment activation, publication, or monetization without separate gates. |
| Domain-specialist safety | Health/finance/insurance/benefits specialists | Document-first workflows, evidence quality, claims, jurisdiction/provider and professional handoff | No clinical diagnosis, legal/tax/financial advice, unauthorized money movement, or autonomous consequential actions. |

## Phase-by-phase roadmap

Each phase below preserves the existing phase name and intended outcomes. “Build” means only after its entry/authority gates; design and clearly marked synthetic research can proceed earlier where permitted. Every phase receives a tracking issue or issue set, milestone, scope, owner, dependency edges, exact-revision evidence, risk register, user help, and an explicit human exit decision. Each milestone also has a cumulative regression gate and a corresponding GitHub Release; a milestone is not complete until its release evidence and applicable publication approvals are recorded.

### Phase 0 — Govern and decide

**Outcome:** A coherent, explicitly authorized program and company foundation without cloud provisioning.

1. Reconcile accepted plan bytes and hashes, current proposed amendments, issue closure versus approvals, repository visibility, and the issue/milestone inventory. Retain the accepted plan hash and proposed-hash distinction in ADR 0000; classify all material amendments before updating them.
2. Map the already-attested independent-recipient appointment and private counsel outcome only to their actual scope; complete remaining charter, reporting-route, counsel/IP, licensing, contribution, name, trademark and public-claim prerequisites without publishing confidential information.
3. Decide the owner-only privacy conflict and approved architecture; update trust/data-flow diagrams, threat model, privacy inventory, impact assessment, release/client trust and migration plan in exact-version decision packets.
4. Confirm strategy, Customer Zero assumptions, company roles, risk ownership, responsible-AI and protected-dissent processes; identify actual agent availability rather than importing a historical roster.
5. Establish separate development AI, product metering and company financial ledgers, with separate cloud, model, AI-credit and professional-services approvals. Prepare no-provisioning provider/hosting/mobile/identity/marketplace research, benchmark and cost models.
6. Create or reconcile the missing issue inventory for every later milestone, every catalog item and company function; distinguish prerequisites from backlog and preserve already closed issue history.
7. Produce a complete testing, accessibility, privacy, security, SLO, performance, recovery, UX, support and evidence plan; document assumptions and stop/de-scope rules.

**Exit:** Exact artifacts and applicable independent challenges are complete; Cyrus explicitly decides the Phase 0 entry/exit set and remaining risks. Phase 0's $0 cloud/no-account rule remains effective until a separate applicable decision.

### Phase 1A — Independent self-hosted assistant

**Outcome:** A verified, recoverable, independent personal task assistant, aligned with the finally approved privacy architecture.

1. Reconcile current code against [#38](https://github.com/Jamula/Andreja/issues/38), the Phase 1A exit checklist, accepted ADRs 0001–0005, and the human owner-only direction. Do not treat the current server-side plaintext data path as compliant.
2. Close the design gap through the approved owner-client path: independently authenticated client release root; vetted cryptographic construction; owner-held content keys; explicit device enrollment/revocation, loss and recovery; local processing/search/export/import; minimally observable server metadata; no plaintext fallback. If D2 is not approved, limit work to allowed synthetic design and existing behavior remains nonconforming to the desired boundary.
3. Preserve independent offline startup, authenticated typed API, passkey bootstrap/recovery, tenant/principal enforcement, proposal confirmation, atomic task/audit/idempotency and portable encrypted data ownership. Prove fake/BYOK provider switching without unauthorized external calls.
4. Reproduce current-head .NET, architecture, PostgreSQL runtime, browser, WebAuthn, migration, authorization, privacy, failure, telemetry, export/import and accessibility evidence on the named environment. Verify test inventory parity and exclusions.
5. Diagnose [#211](https://github.com/Jamula/Andreja/issues/211) using recovered or safely reproduced sanitized dependency/advisory evidence. The 12 historical forbidden findings and their cause are unknown; a later green main run is not proof of their identities or root cause.
6. With D9, produce the protected-tag Sigstore bundle and retained root, verify exact issuer/repository/workflow/revision/ref and digest offline against an independently held root. Restore encrypted database and Data Protection keys together into a clean instance and sign in with a restored passkey. Exercise a separately approved, genuinely different signed revision and rollback with state preserved.
7. Obtain approved numeric SLO/RPO/RTO/retention/model-spend limits, exact-revision independent reviews, human assistive-technology and provider-failure UX evidence, and explicit residual-risk/exit decision.

**Exit:** Report the accepted #66 checklist and its explicit human decision separately from the additional owner-only milestone for this full-vision roadmap. The privacy-aligned outcome additionally requires the applicable approved client/design, migration and owner-controlled recovery evidence; an old checklist pass alone is not proof of that human-directed boundary. Applicable offline independence, key-aware recovery, update and rollback, isolation, redaction, accessibility and actual operator verification are proven at the candidate revision. No Phase 1B deployment or external provider is implied.

### Phase 1B — Managed reference, MVP stories and public surfaces

**Outcome:** A separately authorized managed reference and controlled adult dogfood demonstrating the two full MVP stories.

1. Only after a specific budget/TTL/teardown decision, deploy a selected managed reference with pinned OpenTofu, locked/encrypted state, workload identity, secrets, backups, quotas, observability and cost reconciliation. Prove self-host/managed conformance and deterministic tenant cleanup.
2. Qualify Gmail and Microsoft account types, official APIs, scopes, terms, provider review, data flows, encryption/token custody, rate/renewal, delivery topology, disconnect/purge and budgets. Implement per-account dry-run/live policy, sourced tasks, controlled calendar actions, quarantine/undo, adaptive-rule explanation and rollback. Pass all six email scenarios below on the actual approved providers.
3. If D1 explicitly permits it, implement host-owned trip workspaces across three separate adult tenants with bilateral grant/consent, minimal projections, proposals, conflict/idempotency, revocation/derived purge, audit and export. Pass all five travel scenarios below; otherwise record the real scenario as blocked.
4. Deliver local help/site artifacts and the public/private feedback designs. Resolve #6, #93, #155–#158 and the exact synthetic evidence authorization, independent review, runbooks, accessibility, name/domain, hosting, support capacity, claims, legal and release decisions before activating any public site, staging sender, feedback, or support promise.
5. Use deterministic fake and authorized BYOK with provider switching. A real Copilot provider is no earlier than Phase 1B and only after ADR 0009's separate privacy/security/legal/architecture/cost/quality verdicts, a specific synthetic canary decision, hard stops and isolation; otherwise explicitly defer it without claiming provider evidence.
6. Prove real-phone access with the dedicated approved test identity, WebSocket/reconnect/rolling revision, tenant non-enumeration, consent and notifications, support status, backup/PITR, clean portability, teardown and invoice reconciliation.

**Exit:** The actual six email and five three-tenant travel scenarios, approved managed/reference recovery and cost evidence, support/feedback requirements and all applicable reviews pass. Neither mocks nor a private artifact substitutes for live provider or human participation.

### Phase 2 — Assistant and core first-party skills

**Outcome:** A daily-use assistant and core skill system with meaningful user control.

1. Add provider selection, streaming and cancellation, prompt/context minimization, token/unit budgets, error/fallback UX, decision provenance, session audit and safe model switching.
2. Complete task Inbox/Today/Upcoming/Waiting For/completed views; edit, search, areas, tags, contacts, source, intended outcome, periodic review, corrections and undo, using declared skill capabilities.
3. Build manual Calendar and Commitments, Finance Administration, Interests/Reading/Podcasts and a minimal durable single-shot reminder; settings remain manifest-declared and exportable.
4. Ship draft-only Personal Brand Studio using user-approved evidence; implement reviewable Personal Semantic Profile, Life Context Navigator and manual Relationships/Communities Map with source, confidence, correction, expiry and no covert scoring.
5. Deliver responsive desktop/tablet/mobile journeys, complete versioned help, feedback/status lifecycle, provider-failure states and privacy-safe efficiency feedback.

**Exit:** An authorized Customer Zero can use the assistant and skills for real work, exercise data control and recovery, and see measured efficiency without confusing a personal trial with broad demand. Prompt injection, unauthorized capability widening and token-budget failures are tested.

### Phase 3A — Email and messaging expansion

**Outcome:** A reliable consented messaging/channel platform, beyond the narrow Phase 1B MVP.

1. Mature manifests, delegated-token lifecycle, provider health, scoped permissions, provenance, retries/idempotency, cursor and webhook reconciliation, user pause/disconnect/purge and portable fallback.
2. Expand Gmail and Outlook/Microsoft 365 to broader follow-up/reminder workflows; design Gmail Pub/Sub/history/watch renewal and Microsoft Graph notifications/delta renewal with documented polling/manual fallback.
3. Provide in-app user-to-Andreja and support/status messaging; implement interaction-first official Discord bot/app with explicit scope. Do not use self-bots, user-token automation or ambient privileged intents without separate approval.
4. Support user-controlled WhatsApp share/export; a Business Platform pilot is conditional on an approved business scenario and provider qualification, not a Phase 3A exit requirement.
5. Verify transactional sender, DNS authentication, bounce/complaint handling, abuse controls, cost, provider failure, rate limits, retries, revocation, redaction and human review.

**Exit:** Approved providers transform consented mail into reviewable tasks and deliver supported notifications without hidden copying, unreviewed sends or credential leakage.

### Phase 3B — File, calendar and professional channels

**Outcome:** User-approved access to files, calendars and professional content with clear copy and model-exposure choices.

1. Implement OneDrive/SharePoint, Google Drive, GitHub and Box through qualified official APIs, independent grants, scoped query-in-place versus sync/import choices, bounded caches, purge and export.
2. Deliver connected Microsoft/Google Calendar while preserving manual Calendar use; support conflict review, step-up approval and calendar-specific permission/recovery paths.
3. Support GitHub profile/README through a distinct professional-content grant; keep LinkedIn as research or user-controlled draft/export until official access is approved.
4. Test delta/event reconciliation, provider limits, sandbox/real account conditions, scope negatives, deletion of derived indexes, supported account types, cost and help.

**Exit:** Users can inspect and use approved files/calendars without default copying, ambient access, unapproved model exposure, hidden publication or lock-in.

### Phase 4 — Durable personal mechanisms

**Outcome:** Personal work survives restarts, changing devices and failure, and broader life skills have safe manual modes.

1. Add RFC 5545-compatible recurrence in the user's IANA timezone; idempotent queue/scheduler, notifications, retries, DST and duplicate-delivery handling.
2. Add checklists, dependencies, reading/podcast queues, travel/social commitments, trading watchlists/thesis notes and professional handoff; no brokerage execution.
3. Build manual Family/Relationships, Household/Vehicle/Insurance/Projects, Travel/Social and Hobbies/Social Groups. Keep sensitive household and relationship data scoped.
4. Before health dogfood, complete consumer-health data-flow, safety, breach/retention/export, model-content and escalation review; provide manual Health and Wellbeing without diagnosis, prescribing or autonomous medical decisions.
5. Build manual Life Event Planner and Small Business/Entrepreneur Manager for Customer Zero, separating business and personal personas; require qualified review before personalized insurance, finance, tax, health or legal claims.
6. Exercise backup, restore, export, purge, retention, disaster recovery and managed-to-self-host portability.

**Exit:** Scheduled obligations and data survive restart, upgrade, timezone/DST changes, duplicate delivery, migration and clean recovery within approved SLO/RPO/RTO.

### Phase 5 — Multi-tenant SaaS readiness

**Outcome:** Safely operated managed onboarding, tenant lifecycle and sustainable service economics.

1. Decide and qualify CIAM; implement identity linking, tenant creation/deletion, memberships, roles, quotas, billing, admin, support and migration without weakening self-hosting.
2. Prove code and database tenant isolation, including non-enumeration/inference, cross-tenant reference constraints, admin access and recovery; evaluate RLS as defense in depth.
3. If D1 permits, support same-tenant household and life-event collaboration only with explicit scoped roles, operations, disclosure, audit and revocation.
4. Complete privacy notices, processor records, access/export/delete, retention, residency, incident response, abuse and support; implement separate product metering and financially reconciled billing.
5. Benchmark Blazor runtime/render mode, memory, capacity, connection affinity, failover and unit costs before selecting production architecture.

**Exit:** Independent adults can onboard, use, export and delete their data safely, with proven isolation, measured unit economics, operational support and explicitly approved service limits.

### Phase 6 — Open federation and consented sharing

**Outcome:** Independent instances collaborate without ambient access or hidden disclosure, only if compatible with the approved privacy boundary.

1. Publish a versioned federation specification and conformance kit covering discovery/trust, capabilities, short-lived audience/purpose/resource-bound grants, signatures, key rotation, replay, versioning, rate limits and malicious input.
2. Implement bilateral consent, disclosure ladder, reference projections, proposal-based changes, participant authority, source/audit, expiry and one-step revocation; purge derived context.
3. Support scoped household, life-event and meaningful-connection proposals with explicit acceptance and minimal disclosure for each participant; no relationship type implies automatic access.
4. Test independent instances, retry/conflict, unavailable peer, revocation, compatibility, malicious payloads, spoof/replay, rate limits and recovery. No minor accounts before separate legal/privacy/safety decision.

**Exit:** Two separately hosted conformant instances exchange only explicitly authorized information, reject invalid/replayed messages, revoke access, preserve each owner's authority and recover safely. If D1 prohibits live sharing, this operational outcome remains blocked.

### Phase 7 — Third-party skills and fair marketplace

**Outcome:** External builders can safely create, distribute, support and, only if separately approved, monetize capabilities.

1. Publish versioned SDK/spec, signed manifests, compatibility/migration rules, reference implementations and conformance tests; verify publisher identity, scanning, review, revocation and support.
2. Use isolated remote MCP/HTTP execution with short-lived audience/purpose/resource-scoped capability tokens, strict destination/resource limits and brokered approved operations; never pass connector tokens or run untrusted code in process. Local sandbox execution remains a separately gated extension.
3. Implement discovery, transparent listing/ranking and self-preferencing, consent preview, install/update/uninstall, alerts, support contacts, incident takedown, notice, reasons and appeal.
4. Preserve builder IP/customer relationships, user data ownership, export and portability; prohibit hidden data capture and cross-skill behavioral profiling.
5. Separately decide payment processors, pricing/revenue share, tax, payouts, refunds/chargebacks, contracts, jurisdictions and qualified legal review before money movement.

**Exit:** An independent builder completes the end-to-end conformance, distribution, user consent, upgrade, support and portability scenario; commercialization is complete only with its separate legal/payment approvals.

### Phase 8 — Connector and ecosystem expansion

**Outcome:** Demand-backed expansion through supported, qualified official integrations.

1. Complete OneDrive/SharePoint, Google Drive, GitHub, Box and Microsoft 365 mail/calendar coverage where approved; qualify each account type, API, grant, terms, limits and failure/exit path.
2. Add professional/social publishing only for approved official API/account types, with draft/preview/confirmation, source evidence, audit and disclosure; no automatic social posting or unauthorized third-party disclosure.
3. Add approved community/game, household/assets and business accounting/invoicing/CRM/support/marketing/commerce channels with distinct business grants and no credential scraping.
4. Pilot selected OneDrive photo files and Google Photos Picker items using only provider-returned basic metadata and user review. No automatic face naming, full-library default, sensitive-trait inference or Picker-media clustering; any other on-device biometric work requires a distinct gate and lawful source.
5. Add additional notifications and partner integrations only after user demand, privacy/security, provider and operational evidence.

**Exit:** Every enabled channel passes its own lifecycle, supported-account, user-consent, provider, accessibility, reliability, cost and retirement criteria; unavailable provider capabilities remain explicitly unsupported.

### Phase 9 — Native mobile clients

**Outcome:** Secure, accessible, owner-controlled mobile experiences on real iOS and Android devices.

1. Compare .NET MAUI Blazor Hybrid and native approaches against the chosen independently verified owner-client design, key storage, release trust, APIs, accessibility, offline behavior, performance, support and cost.
2. Implement secure local storage/keystore, device enrollment and recovery, offline sync/conflict handling, push/background limits, deep links/share targets, self-host/managed instance trust and accessible error states.
3. Build a device/OS/browser/assistive-technology matrix; test low memory, battery/network restrictions, app termination, lost devices, revocation and tampered/downgraded client release.
4. Prepare store privacy/security/metadata, provenance, support and release/rollback materials; submit only after human, legal, vendor, cost and app-store approvals.

**Exit:** Actual supported iOS and Android devices complete the highest-value authorized scenarios across intermittent connectivity with correct data ownership, consent, accessibility and recovery. A simulator-only build is not sufficient.

### Phase 10 — Business and product analytics

**Outcome:** Product, support and finance decisions use trustworthy, privacy-minimized evidence.

1. Define a versioned metric catalog with human owner, purpose, population, unit, numerator/denominator, source, aggregation/privacy threshold, retention, access, freshness, target and action.
2. Separate project-delivery, opted-in product/adoption and resource/cost data products, including self-host opt-out/default-off behavior; prevent access to personal content and prohibited cross-domain joins.
3. Implement role-restricted aggregate dashboards, data quality/freshness, small-cohort protection, export and auditable access; test deletion, retention, wrong-role and privacy boundaries.
4. Use hypothesis-driven experiments with measured baselines, outcome and safety guardrails, cost, consent, rollback and retrospective; never use engagement alone as the product objective.

**Exit:** A real, documented decision is supported by verified, sufficiently fresh aggregate evidence without unnecessary personal data or default self-host telemetry.

### Phase 11 — Rewards, miles, benefits and financial optimization

**Outcome:** Explainable, accurate, user-controlled financial-adjacent organization with official, narrowly approved sources.

1. Qualify financial/card, loyalty, travel-rewards and employer-benefits providers and account types; evaluate official APIs, commercial access, terms, jurisdictions, tokenization, data rights, freshness and costs with qualified review.
2. Build Miles and Points Manager for balances, expiry, status, transfers, fees, valuation assumptions, goals and alerts; mark estimates and time sensitivity rather than promising value.
3. Build Lifestyle Rewards/Financial Optimization for benefits, offers, fees/renewals, preferences and explainable read-only proposals; no credential scraping, stored card/CVV/passwords or card-churning automation.
4. Build Employer Benefits/Perks Manager for plan-year inventory, eligibility, deadlines, reminders and what-if scenarios; no individualized tax, investment, ERISA or legal advice.
5. Project only authorized, minimal evidence into Life Event Planner. Any provider action requires separate activation, step-up confirmation, limits, idempotency, receipt and recovery.

**Exit:** Reconciled, time-stamped official sources support useful decisions without unauthorized action or misleading advice. Unsupported partners remain explicit blockers, not simulated successes.

### Phase 12 — Connected Health and Wellbeing

**Outcome:** Source-faithful, user-controlled health organization with strong privacy and clinical boundaries.

1. Define safety and requirements for appointments, medications/refills, allergies, providers, labs, imaging artifacts, referrals, questions, care plans, insurance and wellbeing goals with qualified domain input.
2. Preserve original clinical artifacts; implement highest-sensitivity classification, provenance, confidence-labeled extraction proposals, correction, derived-index deletion, retention, export, audit and approved local model rules.
3. Qualify SMART on FHIR/FHIR, patient portals, Apple Health/HealthKit, Android Health Connect, pharmacy, lab, imaging and wearables using current terms, authorized account types, data flows, security, privacy, safety and legal evidence.
4. Start with manual/documents, then separately approved read-only connected pilots; only later consider specifically approved, user-confirmed actions such as appointment or refill requests.
5. Test medication conflict/instruction handling, urgent-language escalation, provider failure, content minimization, recovery, accessibility and clinical handoff. No diagnosis, prescribing, autonomous medication change or image interpretation.

**Exit:** Users can organize and follow up on their authorized health information with source fidelity and safe boundaries; no clinician, provider, legal or data-rights approval is inferred from agent output.

## First-party skill coverage matrix

All 19 catalog entries are in scope. Each row inherits the full skill lifecycle: charter; semantic/domain contract; capability and disclosure design; UX/help; implementation; independent security/privacy/AI-safety challenge; unit/negative/E2E/performance validation; authorized real dogfood; staged release; operations; versioning/deprecation. Every status change must update the authoritative catalog in the same governed change. The catalog's historical Phase 0 status text is not proof that the currently implemented Open Loops slice is absent.

| # | Skill | Source phase(s) | Required outcome and special gate |
|---|---|---|---|
| S1 | Open Loops and Tasks | 1A–2 | Complete current proposal/confirm/list/complete/export/delete path plus edit, undo, source, search and review; no unauthorized mutation. |
| S2 | Calendar and Commitments | 1B, 2, 3B | Manual then connected calendars; policy-bound invites and step-up review for conflicts or consequential external changes. |
| S3 | Personal Semantic Profile | 0 research, 2 | User-owned claims and hypotheses with provenance, correction, expiry, sensitivity and purge; current semantic fixtures are not a durable graph. |
| S4 | Personal Brand Studio | 0 research, 2, 8 | Evidence-backed drafts, user approval and preview; publication only through separately authorized official channels. |
| S5 | Finance Administration | 2 | Bills, follow-ups, budget/checklists; separate user records from company accounts and product metering; no money movement. |
| S6 | Family and Relationships | 4, 6 | Consent-aware commitments and scoped collaboration; no ambient access or shadow profiles; live sharing requires D1. |
| S7 | Health and Wellbeing Manager | 4, 12 | Safety-reviewed manual organization first; highest-sensitivity connected read-only path later; no diagnosis or prescribing. |
| S8 | Household, Vehicle, Insurance and Projects Manager | 4–6, 8 | Manual records and approved scoped collaboration/connectors; insurance comparison and referral require qualified review; no access-code scraping. |
| S9 | Travel and Social Planning | 1B, 4, 6 | Three-tenant minimum-disclosure trip proposals and later full planning; live sharing depends on D1 and real participant evidence. |
| S10 | Interests, Reading and Podcasts | 2 | User-selected sources, queues, notes and recommendations; trace and delete source-derived records. |
| S11 | Trading Research and Review | 4 | Watchlists, thesis notes and journal reminders only; no order execution or brokerage connection. |
| S12 | Lifestyle Rewards and Financial Optimization | 11 | Explainable read-only reward/card benefits, fees and user preferences; provider, privacy and qualified methodology gates. |
| S13 | Miles and Points Manager | 11 | Reconciled balances, expiry, transfer ratios, fees and time-sensitive valuation estimates; no unattended redemption. |
| S14 | Employer Benefits and Perks Manager | 11 | Document-first eligibility, deadlines, reminders and what-if; no individualized tax/investment/ERISA/legal advice or payroll passwords. |
| S15 | Hobbies and Social Groups Manager | 4, 8 | Manual then official community channels; scoped group consent, no full-history import, shadow profiles or wagering execution. |
| S16 | Life Context and Opportunity Navigator | 0, 2, 6 | User-defined goals, explainable personal opportunities and later mutually consented connections; no hidden engagement/social/wealth score. |
| S17 | Life Event Planner | 4–6, 11–12 | Branchable scenarios, risks, source-scoped projections and professional handoff; never bypass source permissions or promise outcomes. |
| S18 | Relationships and Communities Map | 1B, 2, 8 | Reviewable user-defined relationships and hypotheses; selected-photo context only within specific consent and biometric restrictions. |
| S19 | Small Business and Entrepreneur Manager | 4, 8, 10 | Separate business persona, admin/customer workflows and privacy-safe analytics; official authorization required for banking, payroll, payments and filings. |

Finance-adjacent work preserves the existing co-lead boundary and keeps user finance data, tenant metering and company ledgers separate. Applicable human professional review precedes personalized guidance or public claims.

## Channel and connector coverage matrix

All 23 catalog categories are tracked. For **each provider/account type**, execute the channel lifecycle: charter and manual fallback; dated official API/terms/account/verification qualification; manifest and delivery-topology ADR; privacy/threat review; typed adapter; sandbox and permission/failure tests; approved read-only/draft dogfood; staged release; operations and retirement/purge. Separate identity, assistant, content, support and publishing grants even when one provider offers all of them.

| # | Channel category | Source phase(s) | Completion condition or explicit boundary |
|---|---|---|---|
| C1 | Local identity: passkeys/recovery | 1A | Real browser authenticator, HTTPS origin, first-admin bootstrap, recovery and key-aware restore; failed/replayed/collision cases. |
| C2 | Linked identity: Microsoft, Google, GitHub; later Apple/LinkedIn/Facebook/enterprise OIDC | 1A–2 | Exact issuer/subject identity, explicit linking and recovery; never auto-link on email equality. Each provider is separately qualified. |
| C3 | Assistant providers: fake, OpenAI-compatible BYOK, later Copilot/Azure/local | 1A–1B+ | Deterministic fake and permitted BYOK; Copilot canary only under ADR 0009 and D3/D5/D13; no implied content exposure or provider activation. |
| C4 | Email: Gmail, Outlook.com/Hotmail, Microsoft 365 | 1B, 3A | Official least-privilege delegated grants, six actual scenarios, provider topology/reconciliation, policy and revoke/purge; external-content boundary must be decided. |
| C5 | In-app messaging | 1B, 6 | User/support status and explicitly scoped trip proposals; no ambient peer chat; cross-person use requires D1. |
| C6 | Discord | 3A | Official bot/app and scoped slash interactions; no self-bots or user-token automation; privileged ambient content separately gated. |
| C7 | Gaming/hobby communities | 4, 8 | Manual group/calendar first, later supported official Xbox/partner APIs; no private-history scraping or gambling facilitation. |
| C8 | WhatsApp | 3A, 8 | User-controlled share/export; Business Platform only for approved business/account scenario; no unsupported personal-history automation. |
| C9 | File/content: OneDrive/SharePoint, Google Drive, GitHub, Box | 3B | Provider-by-provider official APIs, explicit query/sync choice, content/model disclosure, bounded cache and purge. |
| C10 | Photo context | 8 | User-selected OneDrive files/Google Photos Picker; basic provider metadata only; no Picker clustering or automatic biometric identity. |
| C11 | Calendar: Microsoft and Google | 1B, 3B/8 | Narrow invite behavior first, then full separately granted calendar; ambiguous/consequential actions require step-up review. |
| C12 | Developer/professional brand: GitHub, LinkedIn | 3B, 8 | Dedicated grant, evidence-backed draft/preview and explicit publication; LinkedIn access contingent on official qualification. |
| C13 | Social brand: Facebook/Instagram professional and later approved channels | 8 | Official supported account/API access, user-approved content and disclosure; no unauthorized third-party exposure. |
| C14 | Support/project: GitHub issues/PRs and feedback publishing | 1B–2 | Distinct tokens and private screening, preview/consent before publication; #158 controls operational feedback readiness. |
| C15 | Notifications: email, later push/other | 1B–2+ | Authorized sender/identity, delivery controls, SPF/DKIM/DMARC, bounce/complaint handling, user preferences and costs. |
| C16 | Sponsorship/payment for the project | 0 research, later activation | Separate sponsorship policy, legal/tax/payment review and explicit human decision; no sponsors' access to personal data or hidden influence. |
| C17 | Additional storage: Dropbox/partners | 8+ | Add only after demand and official provider qualification, documented scope, portability and retirement. |
| C18 | Financial/card data | 11 | Approved aggregators/provider APIs, tokenized read-only first, qualified regulatory/privacy/security and cost review; no credential scraping. |
| C19 | Loyalty/rewards | 11 | Official supported balances/expiry/benefits and consented proposals; provider terms, reconciliation and no credential scraping. |
| C20 | Employer benefits/perks | 11 | User documents first, official supported HR/benefit providers later; no employer/payroll password scraping. |
| C21 | Health/wellbeing | 4, 12 | Documents first, approved read-only clinical/health APIs later; source fidelity and no autonomous diagnosis/medication change. |
| C22 | Household/assets | 4–6, 8 | Documents first, later authorized insurance/vehicle/utility/home APIs; no insurer/vehicle/alarm/access-code scraping. |
| C23 | Brokerage/trading execution | Unscheduled; explicitly deferred | Research/checklists only under the current plan. A real brokerage/market-data integration or order execution needs a separate regulated decision and is **not authorized**. Because the user requested all aspirations, report it as an unresolved dependency rather than claiming the entire aspiration complete. |

## Non-negotiable end-to-end acceptance scenarios

### Email MVP: all six, with real approved Gmail and Microsoft accounts

1. Connect one Gmail and one Outlook.com or Microsoft 365 account with distinct personas and independently scoped policies/grants.
2. Run an explicitly scoped historical dry run, inspect volume/cost and relationship hypotheses, correct/reject them, and activate live mode only after authorization.
3. Classify incoming mail, create/complete a sourced task, and sync an invite only within the approved account/calendar policy.
4. Quarantine an eligible short-lived message, inspect reason and grace deadline, undo it, and prove permanent deletion still requires confirmation and honors holds.
5. Correct a classification/task; inspect the versioned adaptive rule, its evidence and permission boundaries; roll it back.
6. Pause processing, inspect/export the action ledger, revoke one account, purge eligible derived data, and prove the other account and tenant remain unaffected.

Synthetic fixtures are useful early, but they do not pass these real-provider scenarios. Every scenario must also test invalid tokens, expired grants, unexpected provider content, rate limits, provider outage, failed confirmation, replay, wrong tenant and no content in operational telemetry.

### Group travel MVP: all five, with three real consenting adult tenants if D1 permits

1. Invite and accept three adults in three independently isolated managed tenants through bilateral, purpose-scoped, time-bounded grants.
2. Share only chosen availability, preferences and budget ranges; prove unrelated calendar, finance, health, family, location and semantic data is not accessible or inferable.
3. Propose itinerary/tasks, resolve a concurrent edit without duplicate effects, and record actor, decision and minimized audit.
4. Revoke one availability grant without removing the participant; update projections and purge previously derived context within the approved semantics.
5. Export the minimum-disclosure itinerary, close the trip and expire grants without deleting private tenant records.

Until cross-human sharing is explicitly permitted, only synthetic contract tests may run and the real MVP is **blocked**, not waived.

### Platform extensibility

Gmail and Microsoft implement the same channel contract with provider-specific topology hidden outside domain logic. Email composes assistant, channel, task, calendar, semantic provenance, policy, control plane and support; travel composes identity, grants, consent, scoped projections, proposals, tasks, calendar, messaging, audit and help. Demonstrate that adding a separate approved skill/channel uses public contracts and does not require ambient secrets, direct database access or unsafe modification of another tenant.

## UX, accessibility and research program

Create one journey map with entry, success, empty/loading, denied, offline, cancellation, expiry, recovery, retry and support states for every shipped path:

1. **Discovery and trust:** honest availability, supported platforms/providers, privacy model, costs, known limits, independent client-verification explanation, self-host versus managed choices and accessible help.
2. **First run:** install/verify trusted client, instance trust, passkey bootstrap, device enrollment, understandable key custody and explicit irreversible total-key-loss warning. Identity recovery and content-key recovery must not be conflated.
3. **Daily assistant/tasks:** prompt disclosure, provider/model and budgets, accessible exact proposal, confirm/edit/dismiss, source/provenance, failure/retry, history, undo, export/delete and clear outcome. Preserve safe retry/idempotency when the result is unknown.
4. **Account/control plane:** account personas, least-privilege grants, dry-run/live state, model exposure and estimated cost, queues, policy versions, classified actions, explanations, correction, pause/kill, revoke/purge and export.
5. **Sensitive and collaborative work:** explicit scope/recipient/expiry, minimum-disclosure preview, approval, role differences, conflict resolution, changes in consent, revocation limits and understandable audit; no sharing where D1 disallows it.
6. **Recovery and offline:** expired tokens, unavailable provider, disconnected network, partial sync, lost/compromised device, restored server with missing owner keys, failed/tampered update, rollback and support handoff without exposing user content.
7. **Builder/marketplace:** trust and publisher identity, permissions and costs before installation, data access, update/re-consent, review/appeal, disable/revoke, export and replacement.
8. **Support/public help:** accessible, versioned and searchable setup, privacy, backup, account, error, incident, tracking, reopen, rights requests and safe private reporting only when the corresponding service exists.
9. **Business/domain UX:** distinct personal/business personas, uncertainty/freshness/source labels, no false professional claims, conservative financial/health interactions and professional handoff.
10. **Accessibility:** keyboard-only operation; focus order and restoration; screen-reader labels/live regions and real assistive technology; contrast, text zoom, reflow, reduced motion, touch targets, timeouts, error recovery, captions/alternatives and accessible auth. Target WCAG 2.2 AA, but make a conformance claim only after version-scoped automated and human review.
11. **Responsive and native:** mobile/tablet/desktop, real iOS/Android, orientation, slow network, battery/low-memory, browser support, localized formatting/time zones and internationalization readiness. Test supported languages rather than claiming untested translations.
12. **Research:** use consented, representative participants including varied abilities and technical comfort; observe task completion, errors, time, comprehension and trust; avoid leading prompts. Customer Zero findings are one input, not a population-wide validation. Do not recruit, collect or compensate without applicable approval.

Every journey is mapped to a design artifact, component/API, acceptance tests, accessibility finding owner, help page, actual participant or synthetic-evidence label, and a release decision. A visual mock is not proof of functionality or accessibility.

## Validation and release evidence

### Milestone regression and release contract

Produce one GitHub Release for every phase milestone after its exit criteria pass. The release is the durable, versioned record of what that milestone delivered; it is not merely a milestone closure or a claim that every future aspiration is complete. A phase with no shippable application artifact (for example, a governance/documentation milestone) still produces a clearly labeled evidence/documentation release rather than inventing a software capability.

**Cumulative regression gate for every milestone:**

1. Establish the baseline from the immediately preceding published milestone release: exact source tag/commit and artifact digests, test inventory and versions, environment/configuration, known limitations, and prior acceptance evidence. Phase 0 records the initial accepted baseline and all verified existing behavior.
2. At the candidate revision, run the current phase's acceptance scenarios and the complete regression suite for every earlier shipped milestone. Include each previously accepted critical user journey and its security, authorization, privacy, data-integrity, migration, recovery, accessibility, and provider-failure assertions. Add a specific phase suite rather than replacing earlier tests.
3. Compare test inventory and results to the baseline. A deleted, skipped, weakened, or newly non-executing assertion is a regression until explained and approved. Record exact commands, environment, counts, skips, failures, and artifacts. Do not convert an unavailable environment, zero-sample run, or historical green result into a pass.
4. Require no unexplained failure or degradation in any prior acceptance behavior. Any intentional behavior change requires an explicit compatibility/deprecation decision, migration and rollback evidence, updated help, and human approval; it cannot silently erase an earlier guarantee. Security/privacy boundary failures, unauthorized actions or disclosure, data loss/corruption, and failed recovery always block release.
5. Exercise the supported upgrade path from the previous release, data/schema compatibility, and rollback to the previous supported release on preserved state. Where the phase has no application change, verify prior product tests remain green and the versioned documentation/evidence changes do not weaken existing contracts.
6. Require an independent reviewer to attest to the exact candidate revision and regression evidence. A changed head invalidates that review and requires the affected checks to be rerun.

**Release record and publication gate:**

- Create one GitHub Release associated with the milestone after its exit and regression criteria pass. Bind it to the approved protected tag, exact commit and immutable artifact digests; use the repository's existing `v*` signed-tag/provenance process and version policy. Do not invent a version or bypass tag protection to meet a date.
- Release notes identify the milestone and linked work issues; what changed and did not change; supported upgrade/migration and rollback; compatibility and known limitations; the exact regression suite and results; applicable UX/accessibility, performance, cost/ROI and security evidence; provenance/SBOM/artifact links; and the human decision/reviewer evidence. The release is not a substitute for that evidence.
- Prepare the GitHub Release as a draft until the separate per-milestone human decision approves publishing and every applicable security, legal, privacy, budget, provider, accessibility, operations and launch gate is satisfied. Publishing remains blocked if a required approval, protected-tag proof, trusted-root verification, or required artifact is missing. Closing a milestone, issue, or release-decision issue is not blanket authorization.
- Capture release URL, tag, commit, asset digests, evidence links and publication state in the milestone and roadmap. After publication, retain the previous release and its regression baseline; do not overwrite or silently replace a published artifact. A corrective release must reference the affected release and document the fix and revalidation.
- If a regression appears after publication, stop further rollout/activation, preserve evidence, notify the accountable human, and use only the already-approved rollback or remediation path. Restore and verify the prior supported behavior/data state before resuming; then issue a separately versioned corrective release after the same regression and approval gates pass.

### Evidence contract

For each claim record: requirement ID; exact commit/tree; immutable artifact digest and configuration; environment/OS/browser/device/provider; command or test ID; synthetic versus real classification; start/end or observation window; sample and denominator; result; logs/artifact retention; exclusions; reviewer distinct from author; and remaining risk. Do not retain secrets, content or personally identifying participant evidence in public artifacts. A missing dependency, zero samples, unavailable human review or unexecuted suite is **blocked/not run**, never passed.

### Required test matrix

| Layer | Minimum evidence and realistic regressions |
|---|---|
| Domain/unit | Task and skill lifecycle, invariants, validation, time zones/DST, consent expiry, key-loss/error handling, state transitions and corrections. Tests assert externally observable outcomes, not merely non-null values. |
| Architecture | Inward dependency direction; no UI-to-EF/handler bypass; no provider SDK types in domain; no ambient service/secret/database access from skills/channels; production-impossible test authentication. |
| Identity/security | Passkey bootstrap/sign-in/recovery, origin/collision/replay/rate limits, session invalidation, recent-auth and step-up, CSRF, headers, wrong actor/user/tenant/purpose, admin and enumeration negatives. |
| PostgreSQL/migrations | Empty/prior schema, explicit backup/review-gated migration, composite constraints, atomic proposal/task/audit/receipt, crash before/after commit, conflict and concurrent retries, data deletion and restoration. Only a verified disposable `andreja_test_*` database is eligible; the integration fixture may drop it. Never point it at a shared or production database. |
| Owner-client cryptography | Independently reviewed standard construction, nonce/key lifecycle, signed-client trust root, unsigned/tampered/downgraded rejection, device enrollment/revocation, offline recovery, corruption/replay, total key loss and identity-key separation. Demonstrate no server/operator/provider plaintext or recovery key where required, while documenting metadata and compromised-device limits. |
| Privacy/data lifecycle | Synthetic canaries in DB/dumps/backups/server responses/logs/errors/OTLP/support artifacts; purpose and model-exposure restrictions; retention/delete/export and derived-index purge; no raw content in operational telemetry; evidence is nonzero before asserting zero violations. |
| Assistant/AI | Deterministic fake and malformed tool calls, prompt injection in untrusted content, unauthorized tool/permission widening, cancellation/timeouts/retry, output validation, groundedness/source distinctions, harmful/sensitive inference and safe fallback. Human-labeled evaluation and real-model use require separate authorization; a model judge alone is not evidence. |
| Skills/channels | Manifest version/digest tampering, declared-handler matching, authorization intersection, quotas, idempotency, schema validation, provider failure/429, cursor replay and gap recovery, token rotation/revocation, disconnect/purge, supported account types and official terms. |
| Sharing/federation | Bilateral offer/accept/activate, actor/tenant/grant/purpose/disclosure checks, malicious payload, signature/algorithm/key/audience/expiry/replay, concurrent proposals, revocation and derived-context removal. Real exchange remains D1-gated. |
| Browser/API | Real WebAuthn, proposal review/confirm, typed API and antiforgery, reconnect/unknown-result safe retry, provider failure, portability, supported viewport and keyboard paths on the exact candidate. Browser automation complements rather than replaces assistive-technology review. |
| UX/accessibility | Automated markup/contrast plus human keyboard, screen reader and supported assistive technology on desktop/mobile, with actual exclusion findings, owners and remediation. No WCAG conformance claim without complete version-scoped evidence. |
| Mobile/offline | Real iOS/Android device/OS matrix, secure storage, enrollment, offline/reconnect/conflict, low memory/battery/network, push permissions, deep links, lost-device recovery, signed-client update/rollback and accessible paths. |
| Operations/recovery | Offline self-host start after approved acquisition; no unexpected egress; health, restart, backup + Data Protection keys, restored passkey, clean application export/import, corrupt backup, second signed revision update/rollback, managed PITR and teardown where authorized. |
| Supply chain | Reproducible OCI, SBOM, dependency/image/IaC scans with fail-closed policy; pin and verify tools/actions/images; protected tag, exact OIDC claims, retained bundle, independently held trusted root and network-blocked verification. Diagnose #211 without suppression or invented cause. |
| Support/feedback/site | Exact #158 synthetic exercise and all 14 framework scenarios; privacy notice, routing, abuse, DSR/retention, private reporting, acknowledgments, controlled email, rollback/teardown, monitored `security.txt`, truthful versioned claims and accessible help. |
| Marketplace and commerce | Untrusted execution isolation, publisher impersonation, malicious package, capability escalation, revocation, data export, ranking fairness, notice/appeal, refunds/payout/tax/chargeback and supported legal/payment evidence. |
| Health/finance safety | Source fidelity, freshness, user review and professional handoff; prohibited advice/action, conflicts, urgent-language/escalation, financial action confirmation, provider disconnection, data loss and highest-sensitivity permissions. |
| Mutation and reliability | Failure injection, property-based and fuzz coverage where appropriate; network partitions, clock skew, duplicates, partial failures, restarts, concurrency and irreversibility. A failed side effect must not be reported as a successful one. |

### Existing command entry points

The exact command set is chosen for the changed surface and available infrastructure. On this repository's pinned .NET 10/Microsoft Testing Platform setup use `dotnet test --solution`, `--project`, or `--test-modules`; VSTest `--logger` is not the MTP TRX mechanism.

```powershell
dotnet test --solution Andreja.slnx --configuration Debug
dotnet test --solution Andreja.slnx --configuration Release
dotnet test --project tests\Andreja.PostgreSqlIntegrationTests\Andreja.PostgreSqlIntegrationTests.csproj
dotnet test --project tests\Andreja.PublicSiteTests\Andreja.PublicSiteTests.csproj --configuration Release
node scripts\evidence\browser-e2e.mjs
pwsh -NoProfile -File scripts\supply-chain\Test-SupplyChainPolicy.ps1
pwsh -NoProfile -File scripts\supply-chain\Invoke-NegativeTests.ps1
```

These are **planned verification entry points, not claims that this roadmap session ran them**. The PostgreSQL command requires an independently verified disposable test database and the documented connection setup. Browser, offline, telemetry and signing drills require their documented safe stack, exact artifact and approvals. Preserve all existing required CI checks, including repository-owned documentation checks; the user's preference is not to introduce or manually use Python for new work. Install/restore dependencies only when a chosen validation fails for missing packages or a dependency manifest changes.

## Performance and reliability benchmark program

Obtain approved numeric SLOs and operating limits after baselining. Existing [candidate queries](https://github.com/Jamula/Andreja/blob/7923b4d67f17f402c3e65d2c81dd1a981fa456e2/docs/phase-1a/evidence-gates.md) include candidate 99% task-capture success and p95 confirm-to-persist at most 2 seconds; these are **unapproved examples**, not promised targets. No zero denominator or zero policy-check count passes.

| Workload | Measurements and failure conditions | Representative partitions / prerequisites |
|---|---|---|
| First run and auth | Cold/warm startup, passkey/bootstrap/sign-in/recovery success and p50/p95/p99 duration; error and retry counts. | Supported browsers/devices, clean vs restored instance, normal vs degraded network, valid and revoked credentials. |
| Task loop | Proposal preparation, confirm-to-persist, list/search/export/delete latency; successful outcomes, duplicate/conflict rates, DB query count and memory. | Small and large synthetic task sets, concurrent users/tenants, cold/warm cache, restart and conflict. |
| Assistant | Time to first useful output and completion, typed-tool success/failure, tokens/units per accepted outcome, cancellation/retry, provider outage and local fallback. | Deterministic fake/local provider first; separately authorized real providers and model/version/funding mode only when permitted. |
| Email/calendar | Ingest-to-action latency, classification precision/recall sampled against labeled synthetic/approved data, cursor lag, provider quota/retry, task/calendar usefulness, undo/correction and deletion escapes. | Account/provider/policy, webhook renewal/polling fallback, provider outage/rate limit; real data requires approval. |
| Sharing/travel | Proposal/conflict/revocation propagation latency, throughput, denial/error rate, audit correctness and data-leak negatives. | Three synthetic tenants first; actual consenting participants and approved sharing boundary for operational claims. |
| Service and SaaS scale | Request throughput, p50/p95/p99, error budget, CPU/memory, DB pool/locks, storage/egress, queue lag, Blazor circuit memory, WebSocket reconnect/affinity, cold start and rolling revision. | Self-host and separately approved managed environments; isolated load profiles; enumerate exact concurrency rather than inventing an assumed user count. |
| Mobile and accessibility | Interaction/first-render latency, app memory, battery/network bytes, background behavior, offline sync/recovery, keyboard/screen-reader usability. | Real supported iOS/Android and assistive technology, device/OS/network matrix; synthetic or simulator results labeled separately. |
| Recovery/portability | Measured backup, restore, restored sign-in, data currency, key recovery, export/import, update and rollback time; actual RPO/RTO outcome. | Independent clean instance, encrypted DB plus Data Protection and owner keys as applicable, distinct signed artifacts, failure/corruption cases. |
| Build/release/agent operations | CI queue and execution, reproducibility, review latency, successful outcomes per development-credit and human supervision, retry/rework, artifact size and failed-run share. | Exact workflow/head/config, provider usage statements, separate agent/CI/user cost categories. |
| Sustainability | CPU/accelerator time, storage, network and avoidable recomputation per accepted outcome; measured energy/emissions only with actual factors and stated method. | Label proxies as proxies; no invented carbon figures or cross-tenant content collection. |

Benchmark protocol: define a synthetic, versioned workload and acceptance criteria; record build/digest, configuration, hardware, OS, data volume, concurrency, network and warmup/cold state; gather enough independent observations to report actual sample counts and uncertainty; retain raw content-free results; report percentiles and failures together; compare against an agreed baseline and human-approved regression threshold; rerun after changes and preserve the original baseline. Separate lab measurements from production SLO evidence. Investigate bottlenecks before adopting a permanent render/runtime/cloud choice.

## ROI, outcomes and cost measurement

### Measurement design

1. For each prioritized job, define a comparable **baseline** and assisted workflow before measuring. Under current restrictions use synthetic/time-motion practice only; start real Customer Zero or participant measurement only after D12 and applicable data decisions.
2. Where feasible, compare matched tasks or counterbalanced sequences, recording preparation, actual work, waiting, supervision, corrections, recovery and follow-up for both approaches. Record actual sample count, task difficulty, missing data, learning/order effects, outliers, consent and the participant's judgment of whether the result was useful.
3. Count an outcome only when the user confirms it was correct and useful. Separate completed from attempted, corrected, reopened, undone, abandoned, harmful and unauthorized outcomes. A fast incorrect or unsafe answer is not a saved task.
4. Measure provider units and actual invoices, runtime/infrastructure, support, maintenance, integration, legal/professional spend, human oversight and rework in their correct separate ledgers. Reconcile actual credits to the AI provider usage statement and cash charges to invoices. **AI credits are not currency**; do not convert them without an actual billed rate.
5. The historical cost model records 896.87 development AI credits for initial factory research, including 76.62 credits on failed/halted attempts. It is a historical credit baseline, not a current authorization, cash cost, or proof of a later run's ROI.
6. A single Customer Zero trial yields individual evidence only. Do not invent market demand, repeatability, statistical significance, percentage savings, adoption, or revenue. Report unknown, unavailable and zero-denominator measures as such.

### Definitions and formulas

- **User-confirmed net time saved** = sum over comparable accepted tasks of `(baseline human effort including baseline rework) - (assisted human effort including review, supervision, correction and follow-up)`. Keep negative results; record elapsed wall-clock time separately and do not count waiting as human effort unless it actually occupies the person.
- **Cost per successful outcome** = eligible actual costs for the defined period and population divided by user-confirmed successful outcomes. Show the cost scope and excluded shared overhead. If the denominator is zero, report **N/A**.
- **Quality-adjusted adoption** = users completing a defined beneficial workflow without a safety/privacy gate failure divided by eligible, consented users, with errors and corrections alongside it. Do not use tracking until purpose, consent, access and retention are approved.
- **Economic benefit** = verified, non-overlapping realized savings or incremental revenue plus, only if a human selects a defensible valuation, net time saved multiplied by that valuation. Do not count the same time or financial saving twice.
- **Net value** = economic benefit minus the incremental actual costs needed to achieve it. Separate product operation from development investment and count each human hour only once.
- **ROI** = `net value / incremental investment` for a stated period and accounting scope; where costs or benefit are unknown, or investment is zero, show **not calculable**, not a fabricated percentage.
- **Runway** = available, appropriately scoped cash divided by forecast net burn only when those actual inputs exist and the denominator is meaningful; sponsor income and customer revenue remain distinct.

### Minimum dashboard and guardrails

Record per-task outcome and denominator, user-confirmed net time, quality/correction/undo/reopen, user satisfaction/comprehension, accessibility failures, privacy/consent incidents, unauthorized actions, model units, cost per accepted outcome, agent credits, human supervision/rework, support load, availability, recovery results and trend versus approved SLO. Monitor user and non-user harms separately; an average improvement does not waive an individual harm or failed trust gate. Keep self-host analytics off by default and aggregate only under an approved purpose, threshold and retention policy.

## Company and launch-readiness work beyond product code

| Function | Required work and evidence | Human/external boundary |
|---|---|---|
| Governance and culture | Exact charter/ADR decision, ethical impact assessment, authority matrix, protected dissent, conflict-class routing, independent reporting, non-retaliation and remediation. | Recipient appointment is human-attested; real route operation, charter ratification and conflict-safe access require their own evidence. |
| Legal, IP and contributions | #6: inbound/outbound licensing, authorship, employer/IP obligations, contribution rules, trademark/domain/namespace, terms, counsel questions and safe decision records. | Qualified counsel and Cyrus decide; private matter details stay private. Current Apache-2.0 files and public visibility do not themselves settle future policy. |
| Privacy and rights | Data inventory, purposes, consent, retention/holds, access/correction/export/delete/revocation, non-user protection, processor/subprocessor and incident obligations. | Applicable jurisdiction and legal duties require qualified review; no claim of compliance from an agent or an unchecked document. |
| Security and resilience | Threat model, vulnerability intake, key custody, supply chain, incident roles, tabletop/drills, containment, responsible disclosure, business continuity, backups and restoration. | Independent security challenge and human residual-risk acceptance are separate from implementation. |
| Product strategy and discovery | User segments/jobs, evidence-graded assumptions, representative research, accessible prototypes, competitor/alternatives research, positioning and stage-specific success/stop criteria. | Demand, willingness to pay and demographic reach remain unvalidated until actual research; do not fabricate them. |
| FinOps and accounting | Development AI, tenant metering and company financial ledgers; approved budgets; actual-versus-budget, invoices, vendor commitments, renewals, cash, runway, unit economics and exit cost. | No unapproved spend, contract, account or credit increase; user finance data never flows into company accounting. |
| Business model and pricing | Test self-host/managed value and support costs; define free/paid packaging, sponsorship, marketplace fees, refunds, tax and payment flows only with appropriate evidence. | Separate customer, publisher and sponsor interests; no payment/provider activation or tax conclusion without approvals. |
| Support and operations | Staffing/hours/languages, severity, incident coverage, escalation, response/DSR targets, runbooks, retention, status, help, verify/reopen and measured capacity. | Do not advertise commitments, collect reports or send email until #158 and applicable external decisions clear. |
| Marketing, community and claims | Original evidence-backed story, claims inventory with owner/version/expiry, accessibility, security contact, correction path, community conduct and disclosures. | No public publication, endorsement implication, partner or sponsor commitment before name, legal, privacy, budget and human gates. |
| Vendor and partner management | Official API/terms, DPAs/subprocessors, regions/metadata, security reviews, renewal/cancel path, pricing, support, exit/portability and concentration. | Third-party account reviews and contract terms are outside agent control; no free-tier assumption. |
| Marketplace governance | Publisher identity, IP, fair/transparent ranking, review and appeals, security takedown, customer portability, support, money movement and disputes. | Legal, payments, vendor and human launch decisions are separate from a functioning registry. |
| People and responsible AI | Role/authority boundaries, safe agent tools, onboarding, audit, independent review, accessibility, bias/impact challenge, feedback and training. | Agents are not officers, lawyers, clinicians, accountable humans or approvers of their own work. |
| Reliability and sustainability | SLOs/alerts, on-call, capacity, cost, incident response, recovery, vendor exit, lifecycle maintenance and measurable resource proxies. | Targets and public SLAs require actual evidence, named human owner, approved support capacity and explicit decision. |
| Measurement and accountability | Outcome, safety, accessibility, privacy, security, AI efficiency, financial sustainability, vendor independence, environmental and marketplace-fairness indicators. | Every indicator needs named human owner, definition, baseline, target, cadence, source and stop/remediation threshold before GA; unknown values remain unknown. |

## Work-package queue and dependencies

These are execution packages, not evidence that agents have been started. Their scope is grouped under milestone implementation epics, with a separate human-decision/release issue for every milestone. Existing issues are reused where they already own work. The roadmap and issue descriptions are tracking artifacts, not authorization to start gated activity. Every package receives the evidence contract and independent review described above.

| ID | Accountable role and deliverable | Prerequisites | Primary acceptance evidence |
|---|---|---|---|
| W01 | Program: reconcile all 15 milestones, all 19 skills, all 23 channels, current issues/PRs, accepted versions and live owner/state. | None for read-only work. | Complete trace matrix; no duplicate owner; source snapshot and unresolved differences. |
| W02 | FinOps: establish current AI-credit/financial authorization and three-ledger baseline; propose distinct numeric envelopes. | Existing statement/approval access; human decisions for new spend. | Reconciled actuals, cost-scope exclusions, owners, hard stops; no guessed currency conversion. |
| W03 | Privacy/security/architecture: D1/D2/D3 decision packets and exact owner-client protocol, metadata and migration analysis. | Existing human direction; independent specialists. | Conflicts and alternatives explicit; review and human decisions bound to exact artifacts. |
| W04 | Legal/product: #6, charter, name/IP, contribution and public-claims status. | Scoped private counsel outcome; qualified review as applicable. | Exact-version decisions and unresolved conditions, without publishing private advice. |
| W05 | Quality/operations: identify an existing authorized local/disposable evidence environment or prepare a separately authorized isolated environment; inventory dependencies/CI/runner. | Actual runner access; Phase 0's no-cloud-provisioning boundary; no shared service destruction. | Named environment, disposable target, network scope, teardown and reproducible baseline. |
| W06 | Operations/security: diagnose #211 without policy weakening. First seek retained sanitized diagnostics for the historical PR head and unchanged base; if unavailable, reproduce both inputs on the authorized isolated runner, recording scanner/tool versions and advisory-feed timing. Scan the current candidate separately. | W05 or safely recovered sanitized diagnostics; actual runner access details. | Historical package/advisory/version identities and supported cause, or explicitly unknown if unrecoverable; independently document current-candidate findings. A fresh pass is not proof of the old cause, and no suppression or bypass is authorized. |
| W07 | Identity/application: current Phase 1A task/passkey/tenant and persistence gaps. | Stable existing contracts; W05 for live database proof. | Targeted and cross-tenant negatives, concurrency, crash and migration evidence. |
| W08 | Client/application: implement approved owner client and key lifecycle. | D2 and required architecture/security/privacy/accessibility approvals. | Independent client-verification, encryption, key-loss, recovery, metadata and no-plaintext-fallback evidence. |
| W09 | Operations/security: tag signing, trusted-root verification and two-revision update/rollback. | D9, exact reviewed candidate(s), approved root and W05. | Retained signed bundles, offline claim verification, second signed digest, preserved-state rollback. |
| W10 | Operations/quality: clean restore of encrypted DB and Data Protection keys, restored passkey; include owner keys if approved architecture requires them. | W05, reviewed backup/key custody, applicable W08 and authorization. | Clean-instance restore and actual restored sign-in; database-only restore is insufficient. |
| W11 | UX/quality: prepare recruitment, supported assistive-technology/device matrix and accommodations early; complete Phase 1A browser, provider-failure, viewport and real assistive-technology evidence. | Exact candidate, safe environment and D12 for human participation. | Reproducible browser results and independently reviewed human accessibility findings/closure; availability is unknown until confirmed. |
| W12 | Program/Cyrus: record the accepted #66 Phase 1A checklist decision and, separately, the privacy-aligned full-vision milestone. | W07, W09–W11, applicable current-candidate supply-chain evidence, D8, independent reviews and residual-risk decision for the baseline; W08 or an equivalent approved privacy design/evidence for the owner-only milestone. W06 diagnoses the separate historical #211 issue; its closure is not a separately enumerated #66 exit item, while actual candidate findings and unresolved security risks still require their applicable disposition. | Exact-release checklist results and separate human proceed/extend/stop; independently demonstrate the later owner-only direction before claiming that full-vision boundary is satisfied. |
| W13 | Channels: Gmail/Microsoft qualification, topology, simulators and account policies. | Privacy boundary and D3 where content exposure requires it; D13 for each live provider, and D6 where managed cloud resources are used. Synthetic contract work can precede live authorization. | Six actual email scenarios, per-account isolation and failure/purge evidence. |
| W14 | Sharing: trip workspace, grant/consent/proposal/revocation. | D1 for real sharing; managed tenant foundation and D12 for real adults. | Five actual three-tenant scenarios and negative disclosure proofs. |
| W15 | Platform: managed reference, tenant lifecycle and provider-neutral portability. | W12 and D6, legal/security/privacy approvals. | Quotas/TTL, isolation, recovery, teardown and reconciled usage. |
| W16 | Support/communications: #93 and #155–#158 evidence, local help and synthetic feedback readiness. | W04, applicable exact review/evidence, separate D11 staging authorization. | All 14 feedback scenarios, approved support capacity/runbooks and separately decided publication/launch. |
| W17 | Assistant/skills: Phase 2 core, semantic profile and manual domains. | Stable synthetic contracts for prototypes; W03/W08 or an equivalent approved privacy architecture for real content, plus applicable phase approvals. | Every skill lifecycle, consent, deterministic and user-centered tests; real dogfood separately authorized. |
| W18 | Channels/ecosystem: 3A/3B/8 provider-qualified integrations. | Stable common channel contracts from W13 for shared implementation, not completion of its live email scenarios; each provider's own D13 and data/cost approval for activation. | Official account/scope conformance, delivery topology, disconnect/purge, operational support. |
| W19 | Platform/domain: recurrence, reminders, manual sensitive/household/business skills. | W17, approved safety/data boundaries and scheduler/notification contracts. | DST/restart/duplicate/recovery evidence and per-skill domain safety reviews. |
| W20 | SaaS/sharing: Phase 5/6 onboarding, tenant lifecycle and federation. | W15, D1 for sharing, W19 as applicable, supported identity and legal gates. | Independent-instance conformance, revocation, isolation, migration and unit-cost evidence. |
| W21 | Ecosystem: Phase 7 SDK, remote sandbox, marketplace/fairness and commerce packet. | Stable capability/consent contracts; distinct legal/payment decision for money movement. | Third-party builder conformance, isolation/revocation, appeal/export and separately gated payouts. |
| W22 | Mobile: Phase 9 decision, trusted client and real-device experience. | Research/prototypes may start earlier; D2/W08 for the approved privacy-sensitive client path, D13 for store/provider activation, and D12 for human/device evidence. | iOS and Android real-device, offline, accessibility, key custody and release trust. |
| W23 | Analytics/FinOps: Phase 10 metrics, privacy-preserving dashboards and ROI experiment. | W02 and synthetic data for early designs; approved purpose/consent/retention and measured source events before collecting or claiming real outcomes. | Correct definitions and denominators, actual samples, data quality, safety counter-metrics. |
| W24 | Domain specialists: Phase 11 rewards/benefits and Phase 12 connected health. | W17/W18, per-provider D13, qualified finance/health/legal review, privacy and safety gates. | Source-fidelity, reconciliation, prohibited-action negatives and user-confirmed outcomes. |
| W25 | Release/evidence: compile all phase/catalog/company evidence and final exact-revision decisions. | All required W-packages and external gates for the claimed release. | No missing catalog row or failed/unavailable gate; complete independent and human signoffs. |

### GitHub milestone, implementation and human-decision map

The original milestone titles, open state and unset due dates are preserved. Each milestone description now contains its phase scope, entry/exit criteria, priority rationale, dependencies, cumulative regression criteria and one-release-per-milestone contract. Implementation packages below are tracked in the phase epics rather than duplicated as separate issues. Each separate human-decision/release issue is assigned to @cyrusjamula for front-loaded review. Priorities are provisional dependency/risk sequencing, not scorecard results.

| Phase | Milestone | Implementation tracker | Separate human decision/release gate | Provisional priority | Key prerequisites / lineage |
|---|---|---|---|---|---|
| 0 | [#1](https://github.com/Jamula/Andreja/milestone/1) | [#259](https://github.com/Jamula/Andreja/issues/259) | [#260](https://github.com/Jamula/Andreja/issues/260) | P0 | Existing legal/IP work [#6](https://github.com/Jamula/Andreja/issues/6); no-cloud/no-account rule remains. |
| 1A | [#2](https://github.com/Jamula/Andreja/milestone/2) | Existing epic [#38](https://github.com/Jamula/Andreja/issues/38) | [#261](https://github.com/Jamula/Andreja/issues/261) | P0 | Phase 0 release/decision; historical OCI diagnosis [#211](https://github.com/Jamula/Andreja/issues/211) is linked but not an extra #66 checklist item. |
| 1B | [#3](https://github.com/Jamula/Andreja/milestone/3) | [#262](https://github.com/Jamula/Andreja/issues/262) | [#263](https://github.com/Jamula/Andreja/issues/263) | P1 | #38 and Phase 1A release; reuse [#6](https://github.com/Jamula/Andreja/issues/6), [#93](https://github.com/Jamula/Andreja/issues/93), [#158](https://github.com/Jamula/Andreja/issues/158). |
| 2 | [#4](https://github.com/Jamula/Andreja/milestone/4) | [#264](https://github.com/Jamula/Andreja/issues/264) | [#265](https://github.com/Jamula/Andreja/issues/265) | P1 | Phase 1B release and Phase 2 human gate. |
| 3A | [#5](https://github.com/Jamula/Andreja/milestone/5) | [#266](https://github.com/Jamula/Andreja/issues/266) | [#267](https://github.com/Jamula/Andreja/issues/267) | P1 | Phase 2 release and Phase 3A human gate. |
| 3B | [#6](https://github.com/Jamula/Andreja/milestone/6) | [#268](https://github.com/Jamula/Andreja/issues/268) | [#269](https://github.com/Jamula/Andreja/issues/269) | P1 | Phase 3A release and Phase 3B human gate. |
| 4 | [#7](https://github.com/Jamula/Andreja/milestone/7) | [#270](https://github.com/Jamula/Andreja/issues/270) | [#271](https://github.com/Jamula/Andreja/issues/271) | P1 | Phase 3B release and Phase 4 human gate. |
| 5 | [#8](https://github.com/Jamula/Andreja/milestone/8) | [#272](https://github.com/Jamula/Andreja/issues/272) | [#273](https://github.com/Jamula/Andreja/issues/273) | P1 | Phase 4 release and Phase 5 human gate. |
| 6 | [#9](https://github.com/Jamula/Andreja/milestone/9) | [#274](https://github.com/Jamula/Andreja/issues/274) | [#275](https://github.com/Jamula/Andreja/issues/275) | P2 | Phase 5 release; live sharing remains blocked absent a separate compatible D1 decision. |
| 7 | [#10](https://github.com/Jamula/Andreja/milestone/10) | [#276](https://github.com/Jamula/Andreja/issues/276) | [#277](https://github.com/Jamula/Andreja/issues/277) | P2 | Phase 6 release and Phase 7 human gate. |
| 8 | [#11](https://github.com/Jamula/Andreja/milestone/11) | [#278](https://github.com/Jamula/Andreja/issues/278) | [#279](https://github.com/Jamula/Andreja/issues/279) | P2 | Phase 7 release and Phase 8 human gate. |
| 9 | [#12](https://github.com/Jamula/Andreja/milestone/12) | [#280](https://github.com/Jamula/Andreja/issues/280) | [#281](https://github.com/Jamula/Andreja/issues/281) | P2 | Phase 8 release; owner-client and store/device approvals remain distinct. |
| 10 | [#13](https://github.com/Jamula/Andreja/milestone/13) | [#282](https://github.com/Jamula/Andreja/issues/282) | [#283](https://github.com/Jamula/Andreja/issues/283) | P2 | Phase 9 release; approved purpose, consent and opt-out precede real telemetry. |
| 11 | [#14](https://github.com/Jamula/Andreja/milestone/14) | [#284](https://github.com/Jamula/Andreja/issues/284) | [#285](https://github.com/Jamula/Andreja/issues/285) | P2 | Phase 10 release; no financial action or advice without distinct approval. |
| 12 | [#15](https://github.com/Jamula/Andreja/milestone/15) | [#286](https://github.com/Jamula/Andreja/issues/286) | [#287](https://github.com/Jamula/Andreja/issues/287) | P2 | Phase 11 release; qualified clinical/legal/safety review and health-data authorization. |

| Work packages | Tracking issue(s) |
|---|---|
| W01–W05 | [#259](https://github.com/Jamula/Andreja/issues/259) |
| W06–W12 | [#38](https://github.com/Jamula/Andreja/issues/38) |
| W13–W16 | [#262](https://github.com/Jamula/Andreja/issues/262) |
| W17 | [#264](https://github.com/Jamula/Andreja/issues/264) |
| W18 (3A, 3B, 8 slices) | [#266](https://github.com/Jamula/Andreja/issues/266), [#268](https://github.com/Jamula/Andreja/issues/268), [#278](https://github.com/Jamula/Andreja/issues/278) |
| W19 | [#270](https://github.com/Jamula/Andreja/issues/270) |
| W20 (5, 6 slices) | [#272](https://github.com/Jamula/Andreja/issues/272), [#274](https://github.com/Jamula/Andreja/issues/274) |
| W21 | [#276](https://github.com/Jamula/Andreja/issues/276) |
| W22 | [#280](https://github.com/Jamula/Andreja/issues/280) |
| W23 | [#282](https://github.com/Jamula/Andreja/issues/282) |
| W24 (11, 12 slices) | [#284](https://github.com/Jamula/Andreja/issues/284), [#286](https://github.com/Jamula/Andreja/issues/286) |
| W25 | Each phase epic and its human release gate above; full-vision reconciliation is not a substitute for any milestone's release. |

## Closed and open issue reconciliation

The session snapshot contains the full bodies and available comments for 70 issues. Every closed issue's scope and latest recorded comment were inventoried; decision-critical histories were inspected in more depth. The following grouping covers all 65 closed issue numbers. “Closed” is metadata, not proof that every downstream acceptance item passed; inspect exact linked artifacts, comments and current source when executing.

| Closed issues | Delivered scope / what the roadmap must retain |
|---|---|
| [#2](https://github.com/Jamula/Andreja/issues/2), [#3](https://github.com/Jamula/Andreja/issues/3), [#5](https://github.com/Jamula/Andreja/issues/5) | Umbrella vision, charter and catalogs. #2/#3 closure does not ratify proposed owner-client or charter artifacts; retain the explicit human owner-only direction and every catalog row. |
| [#4](https://github.com/Jamula/Andreja/issues/4), [#63](https://github.com/Jamula/Andreja/issues/63) | Semantic-graph research and minimal contracts; distinguish provenance/fixture conformance from live graph storage and real inference. |
| [#7](https://github.com/Jamula/Andreja/issues/7), [#22](https://github.com/Jamula/Andreja/issues/22), [#29](https://github.com/Jamula/Andreja/issues/29), [#53](https://github.com/Jamula/Andreja/issues/53) | Historical Squad sanitization, configuration inheritance, template hardening and state bridge; current generic/empty roster and active backend must be reverified rather than inferred from historical completion. |
| [#8](https://github.com/Jamula/Andreja/issues/8), [#10](https://github.com/Jamula/Andreja/issues/10), [#11](https://github.com/Jamula/Andreja/issues/11), [#14](https://github.com/Jamula/Andreja/issues/14), [#37](https://github.com/Jamula/Andreja/issues/37) | Legal applicability, feedback framework, financial controls, operating model and finance-adjacent ownership. Existing framework decisions do not activate feedback, authorize budgets or provide counsel signoff. |
| [#9](https://github.com/Jamula/Andreja/issues/9), [#62](https://github.com/Jamula/Andreja/issues/62), [#66](https://github.com/Jamula/Andreja/issues/66), [#73](https://github.com/Jamula/Andreja/issues/73), [#74](https://github.com/Jamula/Andreja/issues/74) | Phase 1A packet and scoped architecture approval; exact-version plan/cost/operating baselines and BYOK/Copilot phase-placement decision. Preserve separate numeric, release, owner-only and activation gates. |
| [#30](https://github.com/Jamula/Andreja/issues/30), [#51](https://github.com/Jamula/Andreja/issues/51), [#65](https://github.com/Jamula/Andreja/issues/65), [#114](https://github.com/Jamula/Andreja/issues/114) | Organization/link migration, accurate OCI license metadata and containment of unapproved Pages publication. Current repository visibility is public; containment is not approval to publish a site. |
| [#39](https://github.com/Jamula/Andreja/issues/39), [#40](https://github.com/Jamula/Andreja/issues/40), [#41](https://github.com/Jamula/Andreja/issues/41), [#42](https://github.com/Jamula/Andreja/issues/42), [#43](https://github.com/Jamula/Andreja/issues/43), [#44](https://github.com/Jamula/Andreja/issues/44) | Phase 1A foundation, self-host/portability, identity, task UI, assistant contracts and evidence work. Local implementation/historical evidence is not current signed release, complete recovery, human UX evidence or Phase 1A exit. |
| [#55](https://github.com/Jamula/Andreja/issues/55), [#68](https://github.com/Jamula/Andreja/issues/68), [#72](https://github.com/Jamula/Andreja/issues/72), [#85](https://github.com/Jamula/Andreja/issues/85), [#87](https://github.com/Jamula/Andreja/issues/87) | Passkey flow, explicit migrations, crash-consistent proposal confirmation, PostgreSQL concurrency and clean application portability; rerun exact-candidate runtime/recovery evidence. |
| [#61](https://github.com/Jamula/Andreja/issues/61), [#67](https://github.com/Jamula/Andreja/issues/67), [#79](https://github.com/Jamula/Andreja/issues/79), [#102](https://github.com/Jamula/Andreja/issues/102), [#104](https://github.com/Jamula/Andreja/issues/104) | Hosted validation, branch protections, Actions capacity, selective-CI investigation and review race. #102 did not prove production savings; #104 was closed not planned, not technically fixed. |
| [#64](https://github.com/Jamula/Andreja/issues/64), [#75](https://github.com/Jamula/Andreja/issues/75) | Concrete OpenAI-compatible transport and shared skill/channel execution contracts. Keep provider exposure and real-call authorization separate from local conformance. |
| [#69](https://github.com/Jamula/Andreja/issues/69), [#70](https://github.com/Jamula/Andreja/issues/70) | Historical stack and readiness-status fixes; preserve exact base/head ownership and separate implementation status from release evidence. |
| [#71](https://github.com/Jamula/Andreja/issues/71), [#127](https://github.com/Jamula/Andreja/issues/127), [#142](https://github.com/Jamula/Andreja/issues/142), [#143](https://github.com/Jamula/Andreja/issues/143) | OCI SBOM/scanning, keyless signing implementation, PR-ref handling and pinned-action upgrade. A later actual signed release, trusted root and #211 diagnosis still require evidence. |
| [#94](https://github.com/Jamula/Andreja/issues/94), [#97](https://github.com/Jamula/Andreja/issues/97), [#99](https://github.com/Jamula/Andreja/issues/99), [#116](https://github.com/Jamula/Andreja/issues/116) | Website design/hosting matrix, developer setup, architecture diagrams, privacy and threat-model artifacts. Keep exact versions, documented-only limits, source updates and publication gates visible. |
| [#96](https://github.com/Jamula/Andreja/issues/96), [#108](https://github.com/Jamula/Andreja/issues/108), [#109](https://github.com/Jamula/Andreja/issues/109), [#110](https://github.com/Jamula/Andreja/issues/110), [#112](https://github.com/Jamula/Andreja/issues/112) | Test-framework evaluation, MSTest/MTP direction, adapter/inventory and migration. Current commands and live PostgreSQL/IDE evidence remain separately scoped; ADR 0007 acceptance must not be inferred. |
| [#121](https://github.com/Jamula/Andreja/issues/121), [#122](https://github.com/Jamula/Andreja/issues/122), [#132](https://github.com/Jamula/Andreja/issues/132), [#135](https://github.com/Jamula/Andreja/issues/135), [#166](https://github.com/Jamula/Andreja/issues/166) | Historical agent issue-drain, retrospective, admission/ACK and Squad CI work. Confirm actual installed capability, backend, queue and budget; do not treat historical concurrency as currently available. |
| [#155](https://github.com/Jamula/Andreja/issues/155), [#156](https://github.com/Jamula/Andreja/issues/156), [#157](https://github.com/Jamula/Andreja/issues/157) | Exact feedback specifications were approved and issues administratively closed; qualified-counsel disposition, independent reviews, runtime/control/vendor/cost evidence and activation remain outstanding. |
| [#222](https://github.com/Jamula/Andreja/issues/222) | Code-quality remediation was reported at a historical PR revision, with two JavaScript failures also reported on its unchanged baseline. Reverify current-head status rather than projecting the old result or assuming current failure. |

**Open work:** #6 legal/IP and contribution path; #38 Phase 1A full exit; #93 public/help artifact and publication; #158 synthetic evidence authorization and later operational/launch decision; #211 historical OCI findings diagnosis. Reopen or create follow-up issues only when the actual missing work is not already tracked; do not alter issue status merely to make the roadmap look complete. #255 is a dependency-update PR, not completion of any phase.

**Known source conflict:** `docs\plan.md` still contains historical text saying #155 is reopened and not approved. The current GitHub state and [latest direct decision record](https://github.com/Jamula/Andreja/issues/155#issuecomment-6139419194) instead show #155 closed administratively, with its exact specification approved and qualified-counsel/executable-disposition evidence still outstanding; [#156](https://github.com/Jamula/Andreja/issues/156#issuecomment-6198438831) and [#157](https://github.com/Jamula/Andreja/issues/157#issuecomment-6198529157) are likewise closed with exact specification approvals and remaining evidence. Preserve both the human's recorded approval and the actual evidence gaps. A future governed documentation correction should reconcile the stale text and ADR 0000 hash requirements; this session roadmap does not change repository files or reopen those decisions.

## Risks, assumptions and stop conditions

| Risk / assumption | Evidence and consequence | Owner / trigger / response |
|---|---|---|
| Owner-only privacy conflicts with group travel and provider exposure. | Direct human direction versus documented MVP sharing; proposed ADR 0012 is not ratified. | Privacy/security + Cyrus: stop real sharing/external plaintext until D1–D3 are resolved; preserve contract-only work. |
| Full-scope five-day delivery is infeasible despite aggressive parallelism. | Fifteen milestones, broad catalogs, incomplete release evidence and external/human gates; no verified agent capacity or effort baseline. | Program: compare actual critical-path evidence at each window; mark outcomes blocked, never relax gates or claim completion. |
| Agent availability or cost is overestimated. | Current Squad state has no named members; no current numeric AI-credit envelope verified. | Coordinator/FinOps: verify actual tools, WIP and existing authorizations before dispatch; reduce concurrency or request a specific decision when needed. |
| A closed issue is confused with an approval. | #3 charter still Proposed; #155–#157 administrative/specification-only; #104 not planned. | Evidence custodian: bind approvals to exact scope/version and preserve unchecked requirements. |
| The public-site decision is inconsistent across sources. | Snapshot ADR 0008 says Proposed; #93 later reports a narrow local decision/work item. | Website/evidence owner: verify direct decision, exact source and merge state before citing it as authoritative. |
| Legal or provider access takes longer than the target or is denied. | Counsel, OAuth verification, terms, stores, participants and provider APIs are outside agent authority. | Legal/channel/mobile owners: prepare alternative manual/document flows; explicitly block the applicable operational claim. |
| Owner-key loss or malicious client causes irreversible harm. | ADR 0012 explicitly excludes operator recovery and cannot protect compromised devices or signing chains. | Security/UX: independently review trust roots, loss warning, recovery, key rotation and rollback; stop on unsafe fallback. |
| Hidden telemetry, AI output or third-party context leaks sensitive content. | Current product content and provider paths exist; metadata remains visible even under ciphertext. | Privacy/security/quality: nonzero synthetic canary tests, scope/retention review, minimum telemetry, incident stop and remediation. |
| Synthetic tests are mistaken for live capability. | Current semantic/peer/channel evidence and historical browser/DB runs have bounded environments. | Quality: label synthetic, historical, current runtime and human evidence separately; require actual scenario proof. |
| Review and CI races cause incorrect merge/release. | #104 is not implemented as an automated review-completion gate; #211 root cause is unknown. | Release owner: exact-head independent review before any authorized auto-merge; fail closed on unknown scan findings, missing artifacts or stale revisions. |
| Weak demand or unprofitable economics. | No representative user behavior, actual time-savings baseline, revenue, per-outcome support or approved price. | Product/FinOps: run consented validation, report unscorable ROI, re-evaluate if verified outcomes do not justify cost. |
| Regulated domains generate unsafe advice or actions. | Finance, insurance, employment benefits and health involve distinct rules and affected people. | Domain/legal/safety owners: document-first, no professional claims or side effects until qualified review and explicit action gates. |

**Immediate stop triggers:** any unauthorized access or disclosure; unexplained outbound traffic; failed consent/tenant isolation; unsafe key custody or unverifiable client/release; unknown or exceeded cost/spend; a destructive test without a proven disposable target; unapproved live data/provider/account; absent independent review; misleading public claim; unresolved high/critical risk without a specific authorized disposition; or third-party/legal conditions not met. Record the event, contain safely, preserve content-free evidence, notify the accountable human and continue only unrelated authorized work.

## Definition of full completion and final handoff

**Full vision complete** only when all of the following are true:

1. Each of the 15 phase outcomes, all 19 skills, all 23 channel categories, both MVP scenario sets, the platform capabilities and company functions have an explicit accepted disposition and current, artifact-bound evidence. A category explicitly deferred or blocked by policy is shown as such and prevents representing the literal full aspiration as delivered.
2. All applicable user journeys, supported devices, accessibility, real integrations, independent security/privacy/legal/safety reviews, recovery, performance, cost and support evidence pass. Synthetic results and paper plans are not substituted for operational proof.
3. Every necessary human and external approval is obtained for its actual scope; all unresolved risks, unsupported providers, user-visible limitations and meaningful gaps are recorded. No blanket approval is inferred from this roadmap.
4. Exact release artifacts are independently verifiable and recoverable; updates/rollback and data portability work; monitoring and support have approved owners and capacity; actual costs and ROI are measured or honestly marked uncalculable.
5. The final ledger shows each requirement's owner, current status, artifact/commit, verification result, approver, blocker and next action. It distinguishes full completion from a narrower approved release or a decision-ready prototype.

## Assumptions and adversarial self-critique

1. **More agents might reduce speed.** Shared contracts, scarce reviewer capacity, duplicate work, incompatible data models, CI contention and unpaid rework can dominate. Watch verified outcomes per agent credit and integration failure/duplicate rate; reduce parallel writers and integrate around named contract owners if they worsen.
2. **The requested scope might not represent validated user demand.** Feature breadth can consume resources without solving a sufficiently painful problem. Watch consented task completion, user-confirmed time saved, corrections, retention of useful workflows and willingness to adopt; re-prioritize with an explicit human decision if evidence contradicts the hypothesis.
3. **A cryptographic design might satisfy storage secrecy but break usability or sharing.** Key loss, compromised client distribution, inaccessible recovery, metadata leakage and cross-person consent can defeat the intended trust outcome. Watch owner recovery success, client-verification failures, privacy negatives and usability findings; stop rather than add operator escrow or plaintext fallback without a new decision.
4. **The apparent human bottleneck may actually be an external or operational bottleneck.** A ready decision packet cannot compel counsel, provider OAuth review, store review, participant availability, incident support or signed artifact infrastructure. Watch each external dependency's actual state and authorization expiry; report blocked items without turning approval intent into evidence.
5. **A green pipeline may conceal absent coverage.** CI can compile but not execute PostgreSQL, use in-memory federation, miss assistive technology, run unsigned OCI audits, or pass on an unrelated revision. Watch exact-revision inventory, nonzero denominators, environment and exclusions; do not declare a release while any applicable evidence is missing.
6. **Company incentives can undermine the charter.** Sponsorship, marketplace rankings, growth, analytics or financial pressure could incentivize excessive data use or misleading claims. Watch sponsor influence, undisclosed ranking, privacy requests, support failures and impact assessments; stop or revise incentives rather than optimize engagement alone.

Reassess the roadmap when a privacy or authority decision changes, a provider/API/terms change, new user evidence contradicts the problem hypothesis, capacity or cost differs from baseline, a release/security incident occurs, a significant repository/source change lands, or the target window ends with incomplete evidence.

## Independent rubber-duck review and resolution

An independent reviewer challenged the draft against the repository and decision records. Findings were checked against the actual sources rather than accepted by authority:

| Challenge | Resolution in this roadmap |
|---|---|
| Owner-only privacy, multi-person travel and external providers conflict; the proposed client needs a specific security review. | Retained as separate D1–D3 gates; added an exact client/key/recovery/release-root review packet and clarified that an owner-only client does not require a sharing exception. ADR 0012 already requires a standard independently reviewed construction and an independent client trust root; the concrete profile and custody remain open. |
| No measured agent/reviewer capacity, cost envelope, effort baseline or realistic external wait supports a five-day forecast. | Added capacity admission, a measured resource-constrained critical-path check, explicit unknowns and a user-controlled fallback. The reviewer's suggested fixed capacity percentage and phase durations were unsupported and are not adopted. |
| Human/AT availability, provider/legal response windows, decision owners and exact prerequisites were underspecified. | Added a decision-timing register, early accessibility/device readiness and missing-resource paths; applicable human evidence remains mandatory. |
| #211 lacks retained package identities and a proven cause. | Added exact historical head/base reproduction and feed/tool capture where safely possible, with a separate current-candidate scan. Rejected unsupported suggestions about prior suppressions or treating an unapproved exception as the default. |
| Phase 1A exit and the later owner-only outcome could be confused. | Separated the accepted #66 checklist, its human exit decision and the additional full-vision privacy outcome; identified #211 as a distinct historical issue rather than inventing a universal checklist prerequisite. |
| The reviewer suggested deferring required Phase 1A accessibility evidence to after Phase 1B entry. | Rejected: the accepted checklist retains human assistive-technology and provider-failure UX evidence. Parallel preparation is allowed, but an absent required gate cannot be relabeled as passed. |
| The reviewer reported #155 as open. | Rejected as stale: a fresh GitHub read shows #155–#157 closed. Their exact specification approvals and administrative closures do not satisfy the remaining counsel, review, runtime, vendor or cost evidence; the older repository status text is separately flagged above. |
| Restrict all implementation to Phase 0 until every privacy decision is made. | Rejected as overbroad: independent work within existing authorization can proceed; only affected real-data, trust-boundary, provider, spending and launch behavior remains gated. A revised target is not a license to weaken the gates. |

**Second review cycle:** A narrower independent check confirmed the #66/ADR 0012 and D1/D2 distinctions, and requested clearer wording that historical #211 closure is not a separately enumerated #66 checklist item. The graph and W12 now state this explicitly while preserving current-candidate scan and unresolved-risk requirements.

**Remaining uncertainty after review:** This roadmap session did not verify a completed capacity/effort baseline, an independent review of a final client protocol, human/external availability, new spending envelopes, or the live-provider and release evidence required for full completion. Five-day full-vision completion therefore remains an unverified target, not a promise.

## Source index

- [Accepted plan authority and amendment policy, ADR 0000](https://github.com/Jamula/Andreja/blob/7923b4d67f17f402c3e65d2c81dd1a981fa456e2/docs/adr/0000-plan-ratification.md); [current platform plan and all phases](https://github.com/Jamula/Andreja/blob/7923b4d67f17f402c3e65d2c81dd1a981fa456e2/docs/plan.md).
- [Proposed company charter](https://github.com/Jamula/Andreja/blob/7923b4d67f17f402c3e65d2c81dd1a981fa456e2/docs/charter.md); [charter ratification instrument ADR 0006](https://github.com/Jamula/Andreja/blob/7923b4d67f17f402c3e65d2c81dd1a981fa456e2/docs/adr/0006-charter-ratification.md).
- [Direct owner-only human direction](https://github.com/Jamula/Andreja/issues/2#issuecomment-6115465929); [proposed owner-device encryption ADR 0012](https://github.com/Jamula/Andreja/blob/7923b4d67f17f402c3e65d2c81dd1a981fa456e2/docs/adr/0012-owner-device-content-encryption.md).
- [First-party skill catalog](https://github.com/Jamula/Andreja/blob/7923b4d67f17f402c3e65d2c81dd1a981fa456e2/docs/roadmap/first-party-skills.md); [channel catalog](https://github.com/Jamula/Andreja/blob/7923b4d67f17f402c3e65d2c81dd1a981fa456e2/docs/roadmap/channel-connectors.md); [skill lifecycle](https://github.com/Jamula/Andreja/blob/7923b4d67f17f402c3e65d2c81dd1a981fa456e2/docs/frameworks/skill-development.md); [channel lifecycle](https://github.com/Jamula/Andreja/blob/7923b4d67f17f402c3e65d2c81dd1a981fa456e2/docs/frameworks/channel-development.md).
- [Phase 1A exit checklist](https://github.com/Jamula/Andreja/blob/7923b4d67f17f402c3e65d2c81dd1a981fa456e2/docs/phase-1a/exit-checklist.md); [evidence index and candidate SLOs](https://github.com/Jamula/Andreja/blob/7923b4d67f17f402c3e65d2c81dd1a981fa456e2/docs/phase-1a/evidence-gates.md); [bounded historical evidence](https://github.com/Jamula/Andreja/blob/7923b4d67f17f402c3e65d2c81dd1a981fa456e2/docs/phase-1a/evidence-44.md); [test matrix](https://github.com/Jamula/Andreja/blob/7923b4d67f17f402c3e65d2c81dd1a981fa456e2/docs/testing-matrix.md).
- [Accepted Copilot phase placement ADR 0009](https://github.com/Jamula/Andreja/blob/7923b4d67f17f402c3e65d2c81dd1a981fa456e2/docs/adr/0009-copilot-provider-phase-scope.md); [cost model and separate ledgers](https://github.com/Jamula/Andreja/blob/7923b4d67f17f402c3e65d2c81dd1a981fa456e2/docs/cost-model.md).
- Source implementation: `src\Andreja.AppHost\Program.cs`, `src\Andreja.AppHost\Components\Pages\Home.razor`, `src\Andreja.AppHost\OpenLoops\OpenLoopsEndpoints.cs`, `src\Modules\Andreja.Modules\OpenLoops`, `src\Modules\Andreja.Modules\Execution`, `src\Adapters\Andreja.Adapters\Assistant.OpenAiCompatible`, `src\Adapters\Andreja.Adapters\PostgreSql`, `src\Andreja.PublicSite` and the four test projects under `tests`.
- GitHub snapshot: the research pass inspected all 70 issue bodies and available comments; the snapshot was an analysis input, not a committed repository artifact. Live open issues, PRs and recent `main` CI were refreshed at the stated snapshot time. Recheck GitHub because issue and PR states can change independently of this document.
