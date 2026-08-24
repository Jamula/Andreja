# Prioritization and Launch Framework

## Owner and status

Picard and Quark co-own the Prioritization and Launch Framework, per
[plan §Phase 0 deliverables](../plan.md#phase-0---govern-and-decide) ("Assign
... Picard/Quark as Prioritization/Launch Framework owners"). This document
is the ratified, authoritative expansion of
[plan §Roadmap prioritization and launch framework](../plan.md#roadmap-prioritization-and-launch-framework)
and [plan §Phased execution](../plan.md#phased-execution); those plan
sections become summary pointers once this file merges. Every roadmap issue,
skill, and channel must trace to a portfolio lane, a scored position, and a
launch-stage evidence gate defined here.

Picard and Quark maintain the scorecard weighting formula and stage-gate
recommendation; neither can bypass Tuvok/Deanna Troi/Sarek/Data evidence or
Cyrus's final decision ([plan §Business leadership
responsibilities](../plan.md#business-leadership-responsibilities)).

## Scope boundary

This framework governs **prioritization, scoring, workstream ownership, and
launch-stage gating** across the whole roadmap. It does not redefine:

- The Skill Development Framework's charter-to-retirement lifecycle
  ([`skill-development.md`](skill-development.md)).
- The Channel Development Framework's provider-qualification-to-retirement
  lifecycle ([`channel-development.md`](channel-development.md)).
- The Feedback and Support Framework's intake/triage/routing lifecycle,
  owned by Guinan under issue #10 and its companion
  `docs/frameworks/feedback-support.md`. Feedback themes feed this
  framework's re-scoring (see [Issue scorecard](#issue-scorecard)) but the
  intake pipeline itself is not duplicated here.

## Phase-to-milestone map

Every phase code used across `docs/plan.md`, the roadmap catalogs, and the
lifecycle frameworks maps one-to-one to a GitHub milestone. This is the
canonical mapping; update it in the same pull request as any milestone rename.

| Phase code | Milestone # | Milestone title |
|---|---|---|
| 0 | 1 | Phase 0 - Govern and decide |
| 1A | 2 | Phase 1A - Self-host MVP |
| 1B | 3 | Phase 1B - Managed dogfood |
| 2 | 4 | Phase 2 - Core assistant and skills |
| 3A | 5 | Phase 3A - Email and messaging expansion |
| 3B | 6 | Phase 3B - Files calendar and professional channels |
| 4 | 7 | Phase 4 - Durable personal mechanisms |
| 5 | 8 | Phase 5 - Multi-tenant service readiness |
| 6 | 9 | Phase 6 - Federation and sharing |
| 7 | 10 | Phase 7 - Builder ecosystem and marketplace |
| 8 | 11 | Phase 8 - Ecosystem expansion |
| 9 | 12 | Phase 9 - Native mobile |
| 10 | 13 | Phase 10 - Business analytics |
| 11 | 14 | Phase 11 - Rewards benefits and optimization |
| 12 | 15 | Phase 12 - Connected health and wellbeing |

## Phase 0: $0 no-cloud rule

[ADR 0000](../adr/0000-plan-ratification.md) records the binding constraint
that every roadmap issue, skill, and channel must respect while in Phase 0:

- **No cloud accounts, subscriptions, free tiers, or trials.** This includes
  free tiers and trial subscriptions that can convert to billable resources.
- **Local and paper research only** — local containers/benchmarks, official
  provider documentation, and pricing calculators stand in for provisioned
  infrastructure.
- **AI-credit and professional-services envelopes are tracked separately**
  from the $0 cloud-infrastructure cap. Domain registration and
  counsel/trademark fees are outside the cloud cap but require their own
  explicit professional-services budget approval before purchase.
- Cyrus explicitly left the initial `fleet-research` run uncapped to maximize
  foundational research quality; this is a recorded, one-time exception, not
  a precedent — no other Phase 0 activity is auto-exempted.
- Any Phase 1B cloud spike requires a new, separately approved budget
  decision with alerts, quotas, TTL, and automatic teardown before any
  resource is provisioned.
- This rule gates stage progression in both lifecycle frameworks: a channel
  cannot pass validation
  ([`channel-development.md` stage 6](channel-development.md#6-validate)) and
  a skill cannot pass safety/security review's live-provider steps
  ([`skill-development.md` stage 7](skill-development.md#7-validation))
  while Phase 0 is open for that item, because both require a live account or
  live model spend beyond the AI-credit envelope.

Every issue opened under Phase 0 must state, in its scope section, that no
cloud provisioning is required — or must explicitly request a budget
exception through this framework's stop/de-scope process before proceeding.

## Portfolio lanes

Every roadmap issue is tagged to exactly one primary portfolio lane (a
secondary lane may be noted for cross-cutting work):

1. Core assistant and user experience.
2. User-owned data, identity, privacy, security, backup, and portability.
3. First-party skills and personal semantic graph.
4. Connectors and ingestion.
5. Federation, sharing, and third-party ecosystem.
6. Public site, help/support, personal brand, community, and growth.
7. Platform reliability, testing, observability, FinOps, and business
   operations.

## Workstream and issue scorecard

### Workstream ownership

Portfolio lanes describe *what* is being built; workstreams describe *who* is
accountable end to end. Every issue links to exactly one accountable
workstream owner in addition to its portfolio lane. This table is the
authoritative expansion of
[plan §Operating model and cohesive workstreams](../plan.md#operating-model-and-cohesive-workstreams):

| Workstream | Accountable lead(s) | Core scope |
|---|---|---|
| Executive, Product and Business | Picard (CEO); Quark (CFO) | Vision, business model, portfolio, launch, sponsorship, unit economics, priorities, partnerships, executive risk |
| Product Discovery and User Research | Picard, Jadzia Dax, Guinan, Neelix | Jobs-to-be-done, dogfood, customer interviews/feedback, usability, roadmap evidence, adoption learning |
| Core Platform and Architecture | Spock, T'Pol, Seven of Nine | Clean/Onion modules, API/domain, identity/tenancy, semantic graph, assistant/provider, skill/channel hosts, data ownership, federation seams |
| Web, Public Site and User Experience | Jadzia Dax, Neelix, Guinan | Blazor web app, accessible workflows, public site, help/docs, feedback/support surfaces |
| Native Mobile and Device Experience | Hoshi Sato | iOS/Android architecture, offline sync, secure device storage, push/background, deep links, app-store lifecycle |
| Platform Operations, Hosting and FinOps | Jett Reno, Quark | Self-host package, cloud adapters, OpenTofu, CI/CD, reliability, OTel, incidents, backups/DR, cost/burn, scale evidence |
| Quality, Performance and Release | Data | Test architecture, E2E/accessibility, performance/scale, provider conformance, release evidence, regression prevention |
| Channels and Connectors | Jett Reno, Seven of Nine, Tuvok | Channel Framework, provider qualification, adapters, auth/scopes, email/messaging/files/photos/partner channels, retirement |
| First-party Skills and Developer Ecosystem | Seven of Nine plus domain leads | Skill Framework, manifests, first-party catalog, SDK/examples/conformance, third-party trust/marketplace path |
| Trust, Security, Privacy and Legal | Tuvok, Deanna Troi, Sarek | Threat/privacy/legal artifacts, data classification, auth/grants, abuse/safety, terms/licensing/trademark, regulatory gates |
| Customer Success, Feedback and Support | Guinan | Intake/triage, support status, user communication, help gaps, resolution verification, feedback insights (issue #10) |
| Marketing, Community and Partnerships | Neelix, Picard, Quark | Positioning, Personal Brand Studio input, public/community content, sponsorship communication, partner ecosystem |
| Future Research and Innovation | Spock, Seven of Nine, rotating specialists | Semantic/AI, federation, on-device intelligence, new providers/skills/channels, bounded experiments |

### Issue scorecard

Every roadmap issue records evidence and a 0–5 assessment for:

- User outcome/value and severity of the open loop solved.
- Strategic fit with assistant/skill differentiation and data ownership.
- Reach across target users, deployment modes, skills, or connectors.
- Learning value and uncertainty reduced.
- Confidence/evidence quality.
- Risk reduction for security, privacy, legal, reliability, portability, or
  cost.
- Dependency readiness and ability to ship a complete vertical slice.
- Implementation effort, ongoing operating/support cost, technical
  complexity, and reversibility.
- Privacy/security exposure and potential user-harm cost.

Scores order eligible work; they never override hard security/privacy/
legal/data-ownership gates or issue dependencies. Re-score when evidence,
cost, or provider access changes — including when Guinan's feedback themes
(issue #10) surface new severity or reach evidence.

Use **Must/Should/Could/Won't** scope per launch stage, with explicit
kill/de-scope criteria. Prefer the smallest vertical slice that proves a user
outcome, architecture seam, or market assumption over broad horizontal
framework work.

## Launch stages and evidence gates

Each stage has a GitHub milestone, exit-gate issue, metric/evidence links, a
known-risk list, and an explicit decision to proceed, extend learning,
de-scope, or stop.

1. **Architecture/Research** — Ratified Phase 0 scope, ADRs,
   threat/privacy/cost/test artifacts, no public product claims.
2. **Cyrus self-host technical dogfood** — Phase 1A assistant/task/data-
   ownership/recovery evidence before exposing a managed service.
3. **Small managed invite dogfood** — Phase 1B adult invitees in separate
   tenants, repeatable onboarding/recovery, tenant isolation,
   feedback/support, public help, and measured cost/usage. **This stage's
   exit evidence is the six MVP Email Triage acceptance scenarios and the
   five MVP Group Travel acceptance scenarios** (see [MVP launch
   validation](#mvp-launch-validation) below).
4. **Invite-only adult alpha** — Bounded connector/skill pilots,
   privacy/security review, support runbooks, no minor accounts.
5. **Private beta** — Stable self-host/managed upgrades, export/delete, SLO
   evidence, cost/unit economics, first useful channel set, help/support
   coverage, incident and rollback practice.
6. **Public beta** — Counsel-reviewed license/terms/privacy posture,
   evidence-controlled public claims, abuse/support operations, sponsor
   policy, capacity/cost guardrails, published limitations.
7. **General availability** — Proven reliability/recovery, security/privacy
   gates, support and lifecycle policy, transparent pricing/hosting terms if
   offered, upgrade compatibility, sustainable operations.

Personal Brand Studio can dogfood as a draft-only skill at stage 2;
connector-based publishing and social-brand expansion require provider
access, authenticity/privacy controls, evidence, and later launch gates
(stage 4+).

## MVP launch validation

The two ratified MVP user stories are the concrete Must-scope proof points
for stage 3 (Small managed invite dogfood):

- **MVP Story 1 — Autonomous Email Triage, Task Extraction, and Adaptive
  Management.** Six acceptance scenarios in
  [plan §MVP Story 1](../plan.md#mvp-story-1---autonomous-email-triage-task-extraction-and-adaptive-management),
  exercised through the Open Loops and Tasks and Calendar and Commitments
  skills (see
  [`first-party-skills.md`](../roadmap/first-party-skills.md#mvp-story-mapping))
  over the Email intake/send channel (see
  [`channel-connectors.md`](../roadmap/channel-connectors.md#mvp-story-mapping)).
- **MVP Story 2 — Collaborative Group Travel Planning.** Five acceptance
  scenarios in
  [plan §MVP Story 2](../plan.md#mvp-story-2---collaborative-group-travel-planning),
  exercised through the Travel and Social Planning skill over the In-app
  messaging channel and the Grant/ConsentRecord/proposal seams.
- Phase 1B's exit-gate checklist in
  [plan §Phase 1B](../plan.md#phase-1b---managed-reference-and-public-surfaces)
  items 9 and 10 require passing every scenario in both stories before Phase
  2 work begins. Neither story may be de-scoped without an explicit
  decision issue under [Program stop and de-scope rules](#program-stop-and-de-scope-rules).

## Program stop and de-scope rules

- Every phase has an approved cost/credit envelope, explicit exit evidence,
  and a bounded spike allowance.
- If a phase exceeds its envelope or cannot prove an exit condition after the
  approved spike cycles, stop implementation and open a decision issue
  rather than silently expanding scope.
- Walking-skeleton de-scope order: defer Phase 1B managed deployment, then
  public site/feedback surfaces, then second external provider integrations,
  then inactive federation/skill persistence, then noncritical UI polish. Do
  **not** cut the Phase 1A independent self-host path, tenant/access
  boundaries, assistant-provider seam, telemetry redaction, backup/restore,
  or minimum end-to-end evidence.
- Later phases may be reordered only through an issue and ADR/plan update
  that preserves dependencies and user-data guarantees.
- Quark issues a documented no-go recommendation when cost is unestimated,
  unmeasured, or outside the approved envelope; Cyrus decides whether to
  stop, fund, de-scope, or accept the recorded risk.

## Cross-workstream contract

- One roadmap/issue source of truth, one architecture decision system, one
  metric catalog, and one release definition. Workstreams do not maintain
  competing plans.
- Every initiative has one accountable owner, named contributors/review
  artifacts, dependencies, interface contract, acceptance evidence, cost,
  privacy/security classification, and user outcome.
- Shared APIs/manifests/schemas are agreed interface-first. After the
  contract lands, implementation streams use isolated worktrees and run
  independently in parallel.
- Changes crossing trust boundaries, product claims, paid commitments, or
  shared contracts trigger the relevant artifact review; ordinary local
  implementation does not wait for unrelated workstreams.
- Integration failures are owned by a dedicated integration/revision agent,
  not bounced between stream owners.

## Consistency rule

Every issue must carry a portfolio lane, a workstream owner, a phase
(mapped through the [phase-to-milestone map](#phase-to-milestone-map)), and a
launch stage. When any of these change, update the issue's labels/milestone
and, if the issue represents a catalog entry, the corresponding row in
[`first-party-skills.md`](../roadmap/first-party-skills.md) or
[`channel-connectors.md`](../roadmap/channel-connectors.md) in the same pull
request.

## Cross-references

- Skill lifecycle: [`docs/frameworks/skill-development.md`](skill-development.md)
- Channel lifecycle: [`docs/frameworks/channel-development.md`](channel-development.md)
- Skill catalog: [`docs/roadmap/first-party-skills.md`](../roadmap/first-party-skills.md)
- Channel catalog: [`docs/roadmap/channel-connectors.md`](../roadmap/channel-connectors.md)
- Feedback lifecycle (not duplicated here): issue #10 and its companion
  `docs/frameworks/feedback-support.md`.
