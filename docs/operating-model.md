# Andreja operating model

- **Status:** The exact source version below was accepted by Cyrus Jamula on
  2026-10-11 ([decision](https://github.com/Jamula/Andreja/issues/73#issuecomment-6115232586);
  [confirmed with the other plan-of-record baselines](https://github.com/Jamula/Andreja/issues/73#issuecomment-6115264261))
- **Accepted source SHA-256 (LF-normalized):**
  `ecfa53473cd08972f269a822e0b55e13b5f68bf64f787f11fb474b8be316136d`
- **Version note:** The decision accepts workstream ownership, handoffs, WIP
  rules, and the human/agent authority framework as written at that exact hash.
  This later copy proposes functional roles and manual issue ownership in place
  of agent identities and automation; it is not itself an accepted revision.
  It also updates status/authority statements;
  facts/policies separation is a later amendment and is not included. Counsel
  approval, residual-risk acceptance, spending, collection, deployment, launch,
  and Phase 0 exit remain separately gated.
- **Owner:** Product lead (CEO and Lead/Captain), with FinOps lead (CFO/FinOps)
- **Issue:** [#14 - Ratify cohesive workstreams and MVP operating model](https://github.com/Jamula/Andreja/issues/14)
- **Milestone:** [Phase 0 - Govern and decide](https://github.com/Jamula/Andreja/milestone/1)
- **Depends on / referenced by:** [`docs/plan.md`](plan.md) (`## Operating model and cohesive workstreams`,
  `## Roadmap prioritization and launch framework`, `## Phased execution`),
  [`docs/charter.md`](charter.md), [`docs/adr/0000-plan-ratification.md`](adr/0000-plan-ratification.md)

## Authority statement

This document is the **authoritative source for workstream ownership**: who is
accountable for each workstream, its boundaries and interfaces, GitHub
ownership, RACI, artifact
gates, handoffs, WIP limits, worktree/stacked-PR parallelism, integration
ownership, the Customer Zero loop, the protected critical path, delight/urgency
and stop/de-scope rules, and executive-versus-Cyrus authority.

It is explicitly **not authoritative** for, and does not define or duplicate,
scoring, sequencing, or launch: the issue scorecard, portfolio lanes,
Must/Should/Could/Won't stage scope, and launch-stage evidence gates remain owned
by [issue #5](https://github.com/Jamula/Andreja/issues/5) and its review-ready
`docs/frameworks/prioritization-launch.md` (see `docs/plan.md`
`## Roadmap prioritization and launch framework`). That framework remains a
separate review-ready draft without an explicit Cyrus ratification record, so
`docs/plan.md` remains authoritative for scoring, sequencing, and launch-stage
gating. This accepted operating model owns **who does the work, how they hand
it off, and what authority they hold**. If the prioritization framework is
separately ratified, it will own **scoring, sequencing, and launch-stage
gating**. Neither document may silently override the other; a conflict is a
`type:decision` issue, not a unilateral edit.

## Scope and non-duplication

This document is the operating contract for how Andreja's workstreams run
independently and cohesively toward a delightful MVP. It defines charters, boundaries,
interfaces, GitHub ownership, RACI, artifact gates, handoff format, WIP limits,
worktree/stacked-PR parallelism, integration ownership, the Customer Zero loop, the
protected Phase 0 -> 1A -> 1B critical path, delight/urgency and stop/de-scope rules,
and executive-versus-Cyrus authority.

It does **not** define or duplicate the issue scorecard, portfolio lanes,
Must/Should/Could/Won't stage scope, or launch-stage evidence gates. Those are owned
by [issue #5](https://github.com/Jamula/Andreja/issues/5) and live in
`docs/frameworks/prioritization-launch.md` once ratified (see `docs/plan.md`
`## Roadmap prioritization and launch framework`). This document references that
framework by name and label (`type:decision` + `area:product` issues that ratify
`docs/frameworks/*.md` and `docs/roadmap/*.md`) instead of re-stating its scoring
weights, lanes, or gate checklists.

## Single sources of truth

| Domain | Source of truth | Owner |
|---|---|---|
| Architecture, roadmap, phased scope | [`docs/plan.md`](plan.md) and accepted ADRs (`docs/adr/`) | Architecture lead (architecture); Product lead (roadmap) |
| Execution state (what's being worked, by whom, when done) | GitHub Issues and milestones in `Jamula/Andreja` | Integration owner |
| Prioritization, scoring, launch-stage gates | `docs/plan.md`; proposed operational expansion in `docs/frameworks/prioritization-launch.md` (issue #5) | Product lead/FinOps lead |
| Who owns which work right now | This document's workstream table + GitHub assignees and `area:*` labels | Product lead (triage) |
| Company financial/cost evidence | FinOps lead's session-close usage ledger and FinOps issues (`area:finops`) | FinOps lead |
| Legal/regulatory applicability | `docs/legal/regulatory-applicability.md` (pending ratification, issue #8) | Legal research lead |
| Feedback/support lifecycle | Cyrus-recorded Phase 0 framework handoff in `docs/frameworks/feedback-support.md` (issue #10); policy approval and operational activation remain gated by explicit Cyrus decisions in issues #155-#158, not issue closure | Support lead |

## Workstream charters, owners, and boundaries

Each workstream owns outcomes and evidence in its scope, not a private architecture
or a duplicate backlog. Accountable leads mirror `docs/plan.md`'s workstream table
with current assignees recorded in GitHub Issues.

Workstream names in the first column are copied verbatim from `docs/plan.md`
`## Operating model and cohesive workstreams` so the two documents never drift
into synonyms for the same accountable scope.

| # | Workstream (exact `docs/plan.md` name) | Accountable lead(s) | Core scope | Explicit boundary (does **not** own) |
|---|---|---|---|---|
| 1 | Executive, Product and Business | Product lead (CEO), FinOps lead (CFO) | Mission, strategy, portfolio, launch decisions, partnerships, sponsorship, capital allocation proposals, executive risk | Cannot override security/privacy/legal/evidence gates; per `docs/charter.md` `## Human and agent authority`, cannot spend funds, sign contracts, or make legal representations |
| 2 | Product Discovery and User Research | Product lead, UX lead, Support lead, Marketing lead | Jobs-to-be-done, dogfood evidence, interviews, usability, delight criteria, adoption learning | Does not set the prioritization scorecard weights (owned by issue #5/Product lead+FinOps lead) |
| 3 | Core Platform and Architecture | Architecture lead, Application lead, Assistant and skills lead | Clean/Onion modules, API/domain, identity/tenancy, semantic graph, assistant/provider seams, skill/channel hosts, data ownership, federation seams | Does not define product priorities or public claims |
| 4 | Web, Public Site and User Experience | UX lead, Marketing lead, Support lead | Blazor web app, accessible workflows, public site, help/docs, feedback/support surfaces | Does not own backend domain contracts (Core Platform) or channel provider qualification |
| 5 | Native Mobile and Device Experience | Mobile lead | iOS/Android architecture evaluation, offline sync, secure device storage, push/deep links, app-store lifecycle | Deferred activation; does not select a mobile stack before the mobile ADR/PoC lands |
| 6 | Platform Operations, Hosting and FinOps | Operations lead, FinOps lead | Self-host package, cloud adapters, OpenTofu, CI/CD, reliability, OTel, incidents, backups/DR, cost/burn evidence | Operations lead does not approve spend; FinOps lead does not own runtime architecture |
| 7 | Quality, Performance and Release | Quality lead | Test architecture, E2E/accessibility, performance/scale, provider conformance, release evidence, regression prevention | Does not waive evidence for schedule pressure; does not set product goals |
| 8 | Channels and Connectors | Operations lead, Assistant and skills lead, Security lead | Channel Development Framework, provider qualification, adapters, auth/scopes, channel runbooks, retirement | Does not own the Skill Development Framework (Assistant and skills lead+domain leads) though Assistant and skills lead co-owns both |
| 9 | First-party Skills and Developer Ecosystem | Assistant and skills lead + domain leads; FinOps lead co-leads only the five finance-adjacent skills named in the catalog | Skill Development Framework, manifests, first-party catalog, SDK/examples/conformance, third-party trust path | FinOps lead has no standing user-record access, launch authority, veto, or control over Assistant and skills lead's product recommendation; does not approve marketplace/legal terms (Legal research lead) or connector qualification alone (Channels) |
| 10 | Trust, Security, Privacy and Legal | Security lead, Privacy lead, Legal research lead | Threat/privacy/legal artifacts, data classification, auth/grants, abuse/safety, terms/licensing/trademark, public claims, regulatory gates | Legal research lead's legal research is not binding advice or a public authorization; does not sign contracts |
| 11 | Customer Success, Feedback and Support | Support lead | Intake/triage, support status, user communication, help gaps, resolution verification, feedback insights | Does not investigate security/privacy incidents in public channels; does not promise timelines before capacity is known |
| 12 | Marketing, Community and Partnerships | Marketing lead, Product lead, FinOps lead | Positioning, Personal Brand Studio input, public/community content, sponsorship communication, partner ecosystem | Activates only after product claims have evidence and licensing/trademark clears (Legal research lead) |
| 13 | Future Research and Innovation | Architecture lead, Assistant and skills lead, rotating specialists | Semantic/AI research, federation, on-device intelligence, new providers/skills/channels, bounded experiments (referred to as "R&D" elsewhere in this document) | Findings enter the catalog/backlog; do not interrupt the MVP unless they change a rewrite-level seam or safety boundary |

### Interfaces between workstreams

- **Shared APIs/manifests/schemas are interface-first.** Core Platform, Channels,
  and Skills agree the contract (issue + ADR or design note) before any
  implementation worktree opens. Once the contract lands, each side implements in
  an isolated worktree in parallel.
- **Web/UX and Mobile consume, not define, domain contracts.** They raise contract
  change requests as issues against Core Platform; they do not fork the API shape.
- **Trust/Security/Privacy/Legal review any change crossing a trust boundary,**
  product claim, paid commitment, or shared contract, regardless of which
  workstream authored the change.
- **Customer Success feeds Product Discovery and the owning workstream** through
  the feedback lifecycle (`docs/frameworks/feedback-support.md`); it never
  silently becomes a second prioritization backlog.
- **FinOps reviews every workstream's cost-bearing change** (compute, AI usage,
  vendor commitment) before merge when the change has a nonzero run-rate impact.

## Issue ownership, dependencies, and RACI

Every GitHub issue has exactly one accountable owner, recorded as a
GitHub assignee, plus an `area:*` label identifying the workstream. Use this
RACI pattern for cross-workstream issues:

| Role | Definition | Who |
|---|---|---|
| **Responsible (R)** | Does the work, keeps the issue/PR current | The GitHub assignee named on the issue |
| **Accountable (A)** | Accountable for the outcome and evidence; approves the PR | The workstream lead in the table above (may equal R) |
| **Consulted (C)** | Specialist whose artifact gate applies (security, privacy, legal, cost, quality, RAI, fact-check) | Named per the "Artifact gates" table below, invoked only when their gate is due |
| **Informed (I)** | Notified via issue comment/handoff note but not blocking | Integration owner, Support lead (user-facing changes), FinOps lead (cost-bearing changes), affected downstream workstream leads |

- Every issue has exactly one lifecycle label. `status:backlog`,
  `status:branch-only`, `status:pr-draft`, `status:ready`, `status:merged`, and
  `status:closed` describe only implementation/PR state. `status:ready` means
  ready for review, not approved, checks-passing, or safe to merge.
- Dependencies between issues are expressed with GitHub's "Depends on" /
  "Blocked by" links, never a private tracking sheet. Unresolved links add
  `blocked:dependency` without replacing lifecycle status. Classify the blocking
  issue with `blocks:evidence` or `blocks:human` when it represents external/exit
  evidence or an explicit human decision; dependents then expose
  `blocked:evidence` or `blocked:human`.
- A dependency closes only when its own outcome is complete. Code may therefore
  be `status:ready` or `status:merged` while exit evidence remains
  `blocked:evidence`; neither label implies that required checks, reviews,
  approvals, or milestone gates passed.
- Reassignment updates the GitHub assignee; the issue history preserves the accountability trail.

## Artifact gates

Gates are artifact-based, not agent-vote ceremonies (`docs/plan.md` `### Review
gates`). A gate activates only when its specialist's first required artifact is
due for that issue.

| Gate | Required artifact | Gatekeeper | Trigger label(s) |
|---|---|---|---|
| Architecture | ADR in `docs/adr/` | Architecture lead | `type:decision`, `area:architecture` |
| Security | Threat-model entry (`docs/threat-model.md`) | Security lead | `area:security` |
| Privacy | Privacy classification/consent entry (`docs/privacy.md`) | Privacy lead | `area:privacy` |
| Cost | Cost-model delta (`docs/cost-model.md`) or FinOps issue evidence | FinOps lead | `area:finops` |
| Quality | Test/evidence mapped in `docs/testing-matrix.md` or the PR's test evidence section | Quality lead | any implementation PR |
| Legal/regulatory | Entry in `docs/legal/regulatory-applicability.md` | Legal research lead | `area:legal` |
| AI safety | AI safety reviewer review | AI safety reviewer | any change touching model prompts/outputs/safety |
| Claim verification | Evidence reviewer review | Evidence reviewer | any public claim, metric, or external citation |
| Governance/ratification | Human approval recorded on the issue/PR | Cyrus | `type:governance` |

Batch related artifacts into themed review packets with at most three
evidence/revision cycles; unresolved items become
explicit decision issues (tag `type:decision`; use the
`blocks:human` label when the decision blocks another issue) instead of
silently blocking unrelated packets. Silence is never approval.

## Handoff format

Every session handoff (end of a work session, PR ready for review, or issue
passed to another owner) records, on the issue or PR:

1. **Decision** - what was decided or produced this session.
2. **Evidence** - links to tests, ADRs, artifacts, or diffs proving it.
3. **Changed contracts** - any API/manifest/schema change other workstreams must
   react to.
4. **Remaining risk** - open questions, known gaps, or deferred de-scope items.
5. **Next ready issue and owner** - the next unblocked issue and its
   GitHub assignee, so work never stalls waiting for a status meeting.

The issue and pull-request handoffs preserve user and operational context without a second backlog.

## WIP limits

- Each workstream lead limits concurrent `status:branch-only`,
  `status:pr-draft`, and `status:ready` issues to what one
  accountable owner can carry through review without queuing rejected work.
  Finish (merge or explicitly park with a recorded reason) before starting new
  work in the same file/module ownership area.
- The integration owner parallelizes only **ready, independent** issues — file/module
  ownership and contracts must be independent, or the shared contract must
  already be agreed and merged.
- Concurrency is bounded dynamically by dependency readiness, CI/test capacity,
  overlapping ownership, FinOps lead's measured session efficiency, and human review
  capacity — not a fixed fleet size (`docs/plan.md`
  `### Parallel worktree execution`).
- When two ready issues would touch the same file or contract, they do not run
  concurrently; one is sequenced or the contract is renegotiated first.

## Independent worktrees and stacked PRs

- Every implementation issue gets a `copilot/{issue-number}-{kebab-case-slug}`
  branch cut from the current integration branch, a sibling isolated worktree,
  one accountable owner, and an early draft PR.
- Issue owners maintain lifecycle and dependency labels manually; see
  [`docs/help/issue-status.md`](help/issue-status.md).
- Contributors never switch another worktree's branch, edit another contributor's files, or
  share mutable build/output directories.
- **Independent issues:** run concurrently in separate worktrees, each with its
  own PR targeting the integration branch.
- **Dependent slices:** stack bottom-to-top. The bottom PR targets the
  integration branch; each higher PR targets the branch immediately below it.
  A higher worktree opens only after its parent layer has committed and pushed a
  stable base. Use GitHub's native stack metadata when the entitlement/API
  version supports it; otherwise keep an ordinary dependent-PR chain rather than
  fake stack metadata (`docs/plan.md` `### Native stacked pull requests`).

## Integration and revision ownership

- The integration owner manages cross-contributor interface decisions, conflict
  detection, sequencing, and integration — this is not bounced between stream
  owners.
- A dedicated integration/revision owner resolves rejected or conflicting work;
  an author whose change is rejected does not revise that artifact or approve
  the replacement.
- After merge: remove the worktree, prune branch/stack metadata, update the
  issue/milestone, and record aggregate usage/validation evidence via FinOps lead's
  session-close ledger.

## Customer Zero loop

- Cyrus/Andreja company operations are Customer Zero for task/open-loop,
  Personal Brand, business management, support, FinOps, channels, semantic
  context, and other early skills (`docs/charter.md` `## Customer Zero`,
  originally proposed in `docs/plan.md` `### Customer Zero doctrine`).
- Dogfood evidence proves user outcome, operational pain, privacy/security,
  support load, cost, and API ergonomics — it does not by itself prove broad
  market demand; Product Discovery treats it as one evidence source among the
  issue #5 scorecard's inputs, not a bypass of that scorecard.
- The loop runs **Customer Success (Support lead) intake -> Product Discovery
  interpretation -> owning workstream fix/feature -> Quality evidence ->
  Customer Success verification with the user -> closed.** Every step is a
  GitHub issue with the originating feedback link; none of it lives in a
  private doc.
- Every Customer Zero pain point becomes feedback/evidence, every workaround
  becomes tracked debt, and every capability Cyrus wants generalized becomes a
  reviewed issue/ADR before it reaches other tenants. Customer Zero never uses
  privileges or bypasses that a real marketplace builder/customer could not
  safely obtain through supported contracts.

## Protected Phase 0 -> Phase 1A -> Phase 1B critical path

- **Phase 0 (Govern and decide)** ratifies charters, ADRs, frameworks, and
  catalogs with a $0 cloud-infrastructure cap and no provisioning. Its exit gate
  is: every artifact required to enter Phase 1A exists and has been challenged,
  and Cyrus approves the decision set and residual risks.
- **Phase 1A (Self-hosted assistant walking skeleton)** is the protected MVP
  critical path: independent self-host start, tenant isolation, the
  assistant-provider seam, telemetry redaction, backup/restore, and minimum
  end-to-end evidence. **Phase 1B and later channels/skills/marketplace work
  cannot silently expand or gate Phase 1A** (`docs/plan.md`
  `### MVP mission and urgency rules`, `### Program stop and de-scope rules`).
- **Phase 1B (Managed reference and public surfaces)** only opens after its own
  separate budget approval and Phase 1A's exit checklist is proven; it adds the
  managed deployment, public site, feedback intake, and the first managed
  dogfood cohort without touching Phase 1A's invariants.
- Milestone mapping: `phase:0` -> [milestone #1](https://github.com/Jamula/Andreja/milestone/1);
  Phase 1A -> [milestone #2](https://github.com/Jamula/Andreja/milestone/2);
  Phase 1B -> [milestone #3](https://github.com/Jamula/Andreja/milestone/3).
  Later phases (`4`-`15`) exist as milestones today and are sequenced, not
  reordered, except through an issue + ADR/plan update that preserves
  dependencies and user-data guarantees.
- Any workstream proposing to reorder phases or expand Phase 1A scope opens a
  `type:decision` issue against the architecture ADR chain; it does not silently
  add scope inside an unrelated implementation PR.

## Delight and urgency rules

- "Delight" means the user achieves a valuable outcome with clarity, trust,
  speed, control, and low friction — not feature count or engagement time
  (`docs/plan.md` `### MVP mission and urgency rules`).
- Optimize for the shortest end-to-end learning path, not the most framework
  code; build vertical slices a user can operate, observe, and critique.
- Every merge keeps the integration branch releasable, carries tests/help/
  telemetry appropriate to the slice, and supplies evidence to the current
  milestone.
- Research uses explicit questions, evidence, and at most three revision cycles
  before escalation; future ideas enter the catalog but do not interrupt the
  MVP unless they change a rewrite-level seam or safety boundary.
- Speed and urgency never waive tenant isolation, user data ownership, backup/
  restore, explicit consent, security/privacy, accessibility, or truthful
  product claims. These constraints outrank delivery speed in every conflict.

## De-scope and stop decisions

- Every phase has an approved cost/credit envelope, explicit exit evidence, and
  a bounded spike allowance. If a phase exceeds its envelope or cannot prove an
  exit condition after the approved spike cycles, **stop implementation and
  open a `type:decision` issue** rather than silently expanding scope.
- Walking-skeleton de-scope order (first to last cut): Phase 1B managed
  deployment -> public site/feedback surfaces -> second external provider
  integrations -> inactive federation/skill persistence -> noncritical UI
  polish. **Never cut:** the Phase 1A independent self-host path, tenant/access
  boundaries, the assistant-provider seam, telemetry redaction, backup/restore,
  or minimum end-to-end evidence.
- Stop/de-scope decisions are recorded on the phase's exit-gate issue with the
  dissent, evidence, and residual risk considered — never resolved by silence
  or by a single workstream unilaterally trimming another's scope.
- Later phases may be reordered only through an issue and ADR/plan update that
  preserves dependencies and user-data guarantees.

## Executive and specialist authority versus Cyrus

- Cyrus remains the human founder/product owner and final business, governance,
  ratification, and risk-acceptance decision-maker unless formal governance
  changes. Qualified counsel, not Cyrus or an agent, provides legal advice,
  clearance, and jurisdiction-specific legal opinions and directs
  privilege-sensitive handling; no wording here guarantees that privilege
  attaches. Cyrus records whether an exposure is material and decides whether
  Andreja proceeds after any required counsel review. This boundary is
  consistent with `docs/charter.md` `## Human and agent authority`: functional titles
  (CEO, CFO, General Counsel, etc.) describe accountable advisory/operating
  project roles; they do **not** grant agents authority to spend or commit
  funds, sign contracts or accept binding terms, make legal representations,
  publish public statements or customer communications, provision cloud
  resources or approve deployment, approve their own security/privacy/RAI/
  evidence/launch gates, or make irreversible/consequential decisions for
  Cyrus or any user. When authority is unclear, work pauses for a human
  decision.
- **Product lead (CEO):** proposes mission, strategy, portfolio, launch readiness, and
  capital allocation with FinOps lead, for Cyrus's approval. Cannot override security/
  privacy/legal/evidence gates and cannot spend, sign, or publish.
- **FinOps lead (CFO) — ledger and spend path:** FinOps lead owns the financial-controls
  source of truth (burn/income ledger, budgets, forecast/runway, unit
  economics — `docs/plan.md` `## Cost and FinOps` and the accepted financial-
  control framework in `docs/cost-model.md`). The cost-model decision accepts
  no numeric envelope or spend; `docs/business/sponsorship-policy.md` remains
  a separate draft under [issue #11](https://github.com/Jamula/Andreja/issues/11).
  FinOps lead facilitates the per-session Session Close Efficiency Review ledger entry
  (below) and posts aggregate findings to the relevant FinOps/retrospective
  issue. FinOps lead **proposes** budgets and no-go recommendations to Product lead and
  Cyrus; FinOps lead cannot spend, sign, commit funds, or accept sponsorship —
  every actual spend or sponsorship acceptance requires Cyrus's explicit
  approval per `docs/charter.md` `## Human and agent authority`.
- **Legal research lead (General Counsel):** maintains the legal/regulatory register and
  translates proposals into counsel questions. His research is a hypothesis and
  input, never binding advice, privileged communication, a signed filing, or a
  launch approval — Cyrus and qualified jurisdiction-appropriate counsel decide.
- **Product lead and FinOps lead co-own** the business case, scorecard weights, and
  stage-gate recommendation for issue #5's framework; neither can bypass Security lead/
  Privacy lead/Legal research lead/Quality lead evidence or Cyrus's final decision.
- **Every specialist (Security lead, Privacy lead, Quality lead, AI safety reviewer, Evidence reviewer)** can block a
  merge by withholding their required artifact; none of them can singlehandedly
  approve a decision that the charter reserves for Cyrus (irreversible,
  regulated, destructive, or trust-boundary changes).
- Reversible, low-risk choices favor action by the accountable workstream lead
  without escalation; irreversible, regulated, destructive, or trust-boundary
  changes require the specified evidence and Cyrus's explicit approval
  (`docs/charter.md`
  `## Precedence, ratification, and amendment`).

## Session and cost feedback loops

- **Session cost review** runs after any
  session that consumed AI credits or incurred external cost: record provider/
  model, credits or usage units, duration, retries, tools, and outcome; keep
  prompts/responses/personal data out of the ledger; identify failed/repeated
  work and one efficiency improvement; update the relevant FinOps or
  retrospective issue when action is required. FinOps lead facilitates and this
  ledger reconciles to the accepted financial-control framework in
  `docs/cost-model.md`; it does not approve any spend or numeric envelope.
- Review delivery and cost evidence weekly; record decisions and action items
  in GitHub Issues, with an owner and evidence for each action.
- **Phase Gate Review** runs before each milestone exit: required evidence,
  unresolved risks, cost, and user outcome; confirmed dependencies and
  rollback/stop criteria; recorded dissent; Cyrus decides proceed, extend
  learning, de-scope, or stop.
- FinOps lead's aggregate usage/cost findings post to the relevant retrospective or
  FinOps issue (`area:finops`) — never raw prompts, personal data, or
  connector payloads.

## Workstream-to-label/owner/milestone mapping

This maps the workstream table above onto area labels and functional ownership. It intentionally does
not introduce a parallel prioritization taxonomy — that belongs to issue #5.

| Workstream | Primary `area:*` label(s) today | Responsible function(s) | Applicable milestones |
|---|---|---|---|
| Executive, Product and Business | `area:product` | Product lead, FinOps lead | Phase 0 and every phase gate |
| Product Discovery and User Research | `area:product` | Product lead, UX lead, Support lead, Marketing lead | Phase 0, 1A, 1B, 2 |
| Core Platform and Architecture | `area:architecture` | Architecture lead, Application lead, Assistant and skills lead | Phase 0, 1A, 2, 5, 6 |
| Web, Public Site and User Experience | `area:product` (public site/UX has no dedicated area label yet) | UX lead, Marketing lead, Support lead | Phase 1A, 1B |
| Native Mobile and Device Experience | `area:architecture` (no dedicated `area:mobile` label yet) | Mobile lead | Phase 9 |
| Platform Operations, Hosting and FinOps | `area:finops` (ops/hosting shares `area:architecture` until a dedicated label exists) | Operations lead, FinOps lead | Phase 0, 1A, 1B, 5 |
| Quality, Performance and Release | none dedicated today (tag with the owning workstream's `area:*`) | Quality lead | every phase |
| Channels and Connectors | `area:architecture` (no dedicated `area:channels` label yet) | Operations lead, Assistant and skills lead, Security lead | Phase 1B, 3A, 3B, 8 |
| First-party Skills and Developer Ecosystem | `area:product` (no dedicated `area:skills` label yet) | Assistant and skills lead; FinOps lead only for the five finance-adjacent skills named in the catalog | Phase 2, 7 |
| Trust, Security, Privacy and Legal | `area:security`, `area:privacy`, `area:legal` | Security lead, Privacy lead, Legal research lead | every phase |
| Customer Success, Feedback and Support | `area:product`, `area:privacy` (feedback intake) | Support lead | Phase 0, 1B onward |
| Marketing, Community and Partnerships | `area:product` | Marketing lead, Product lead, FinOps lead | Phase 1B onward |
| Future Research and Innovation | `area:architecture` | Architecture lead, Assistant and skills lead | ongoing, gated before roadmap commitment |

Current ownership is maintained through GitHub assignees. Area-label changes
require an explicit repository-maintenance decision; no label-sync automation runs.

## Cross-references

- Company charter (proposed for ratification, tracks issue #3):
  `docs/charter.md`. Originating plan context: `docs/plan.md`
  `## Andreja company charter`.
- Customer Zero doctrine: `docs/charter.md` `## Customer Zero`, originally
  proposed in `docs/plan.md` `### Customer Zero doctrine`.
- Cohesive-workstream contract and MVP urgency rules: `docs/plan.md`
  `### Cross-workstream contract`, `### MVP mission and urgency rules`.
- Prioritization, scorecard, and launch-stage gates (owned by issue #5, not
  duplicated here): `docs/plan.md` `## Roadmap prioritization and launch
  framework`, future `docs/frameworks/prioritization-launch.md`.
- Feedback and support lifecycle (Cyrus-recorded Phase 0 framework handoff under
  issue #10; policy and operational gates require explicit Cyrus decisions in
  issues #155-#158, and closure alone has no approval effect):
  `docs/frameworks/feedback-support.md`.
- Financial-control framework (accepted by Cyrus under issue #73; no numeric
  envelope or spend authority): `docs/cost-model.md`, read with
  `docs/plan.md` `## Cost and FinOps`.
- Sponsorship policy (pending ratification under issue #11):
  `docs/business/sponsorship-policy.md`.
