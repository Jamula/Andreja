# Skill Development Framework

## Owner and status

Seven of Nine owns the Skill Development Framework end to end, per
[plan §Phase 0 deliverables](../plan.md#phase-0---govern-and-decide) ("Assign
... Seven as Skill Framework owner"). This document is the ratified,
authoritative expansion of
[plan §Skill Development Framework](../plan.md#skill-development-framework);
the plan section becomes a summary pointer once this file merges. Every skill
in [`docs/roadmap/first-party-skills.md`](../roadmap/first-party-skills.md)
must progress through the stages below, and the catalog's Status column must
match a stage name here.

## Definition

A skill is a versioned, declaratively permissioned capability package that
can contribute assistant tools, UI surfaces, domain templates/checklists,
scheduled jobs, and/or external service integrations
([plan §Skill ecosystem](../plan.md#skill-ecosystem)). First-party skills
follow the same platform contract and quality gates expected from future
external builders; they do not bypass permissions, provenance, proposals, or
testing because they ship in the repository.

Platform capabilities (Assistant Runtime and Review; Data Ownership and
Privacy; Identity/Tenancy/Authorization; Skill Host/Channel Host/Semantic
Profile Host/Feedback and Operability) are **not** skills and are not
governed by this framework — they are privileged product functions that
cannot be granted through a skill manifest. See
[`docs/roadmap/first-party-skills.md`](../roadmap/first-party-skills.md#platform-capabilities-privileged-not-skills).

## Phase 0 boundary — $0 no-cloud rule

Every stage a skill reaches during Phase 0 (charter, semantic/domain
contract, capability/permission design) is **local and paper research only**.
Per [ADR 0000](../adr/0000-plan-ratification.md) and the
[Prioritization and Launch Framework](prioritization-launch.md#phase-0-0-no-cloud-rule):

- No cloud accounts, subscriptions, free tiers, or trials are provisioned to
  support skill research (for example, no managed database or hosted model
  endpoint to prototype the Personal Semantic Profile).
- Live-model calls used for skill validation (stage 7) are capped, isolated,
  non-deterministic smoke evidence under the AI-credit envelope tracked
  separately from the $0 cloud-infrastructure cap — never the sole blocking
  assertion, and never used to justify cloud provisioning.
- Skills that depend on a not-yet-qualified channel (for example, Health and
  Wellbeing Manager's connected-health channels) cannot pass stage 8
  (Dogfood) until that channel clears its own Phase 0 boundary in
  [`channel-development.md`](channel-development.md#phase-0-boundary--0-no-cloud-rule).

## Lifecycle stages

### 1. Skill charter

- User outcome, target personas, scenarios, evidence, non-goals, owner,
  category, launch band, and stop criteria.
- Recorded as a GitHub issue using the "Feature, skill, or channel proposal"
  form (`First-party skill` type), linked to the relevant milestone and to
  its row in [`first-party-skills.md`](../roadmap/first-party-skills.md).
- Exit evidence: issue merged with problem/outcome, scope (including
  non-goals), data/permissions, and evidence sections filled in.

### 2. Semantic/domain contract

- Concepts, inputs/outputs, provenance, confidence/time/sensitivity,
  ontology extensions, retention, and migration/version rules.
- For skills that extend the personal semantic graph (Personal Semantic
  Profile, Life Context and Opportunity Navigator, Relationships and
  Communities Map, Life Event Planner), this stage must align with the
  ontology research led by Seven with Spock, T'Pol, Deanna Troi, Tuvok, Data,
  Hoshi, and Jadzia
  ([plan §Personal semantic graph](../plan.md#personal-semantic-graph)).
- Exit evidence: contract documented in the skill's issue or a linked
  `docs/skills/` entry.

### 3. Capability/permission design

- Manifest capabilities, data scopes, channel dependencies, grants,
  network/model access, proposal/confirmation tiers, and degraded/manual
  behavior.
- Every action is evaluated using the intersection of user/peer grants,
  skill capabilities, resource disclosure level, and current purpose.
- Exit evidence: manifest draft reviewed against
  [`docs/roadmap/channel-connectors.md`](../roadmap/channel-connectors.md) for
  every declared channel dependency.

### 4. Experience and help

- Conversational patterns, UI surfaces, accessibility, settings/preferences
  schema, consent previews, explanations, errors, examples, and help/support
  content.
- Exit evidence: bUnit component/accessibility test plan and help-content
  outline exist before implementation begins.

### 5. Implementation

- Domain/application use cases behind `ISkillHost`, typed tools, no ambient
  `DbContext`/secrets/service-provider access, structured results,
  cancellation, idempotency, telemetry suppression, and cost events.
- Exit evidence: architecture tests prove the skill has no ambient access;
  code merged behind the skill-host contract.

### 6. Safety/privacy/security review

- Threat and privacy artifacts, prompt-injection/untrusted-data boundaries,
  sensitive inference controls, abuse cases, publication/share risks, and
  provider terms.
- Tuvok challenges the security artifact; Deanna Troi challenges the privacy
  artifact; Rai reviews AI safety; Cyrus approves.
- Exit evidence: threat-model and privacy-classification entries merged for
  this skill.

### 7. Validation

- Unit/domain, manifest/schema, permission-negative, channel contract,
  component/accessibility, E2E, adversarial AI, failure/recovery,
  performance/cost, compatibility, export/delete, and help-link tests.
- Exit evidence: test suite green in CI; Data signs off on scenario coverage.

### 8. Dogfood

- Smallest complete vertical slice, draft/read-only before consequential
  side effects, user feedback captured through Guinan's workflow (issue
  #10), and measured outcome/usage/cost.
- Exit evidence: Cyrus (Customer Zero) uses the skill for real personal work;
  Quark's per-session usage evidence attached to the issue.

### 9. Release progression

- Dogfood, invite alpha, private beta, public beta, GA; every stage records
  supported capabilities, known limits, evidence, and rollback.
- Each stage maps to the launch stages and evidence gates in
  [`prioritization-launch.md`](prioritization-launch.md#launch-stages-and-evidence-gates).
- Exit evidence: a milestone/exit-gate issue records the stage and the
  decision to proceed, extend, de-scope, or stop.

### 10. Operate and improve

- SLOs, quality/freshness, model/channel changes, user feedback, outcome
  metrics, costs, incidents, compatibility, and backlog re-scoring.
- Exit evidence: dashboards and a named SLO owner exist per
  [plan §OpenTelemetry and operability](../plan.md#opentelemetry-and-operability);
  re-scoring recorded against the
  [issue scorecard](prioritization-launch.md#issue-scorecard).

### 11. Version/deprecate

- Semantic versioning, minimum platform/protocol, migration, re-consent for
  new scopes, compatibility window, export, replacement path, and
  revocation/retirement.
- Exit evidence: a deprecation issue records the compatibility window,
  migration path, and re-consent evidence. Status moves to `Deprecated` in
  the skill catalog only after this evidence exists.

## Non-negotiable rules

- A skill cannot declare itself successful from engagement alone; it must
  improve the user outcome stated in its charter without violating privacy,
  agency, authenticity, or cost guardrails.
- Skills never receive `DbContext`, secrets, or unrestricted
  service-provider access, at any stage, including dogfood.
- Third-party skills additionally require publisher identity,
  signature/provenance, remote execution trust, review status, resource
  limits, and ecosystem enforcement before they may enter this lifecycle
  (Phase 7+; out of scope for the first-party catalog).
- A skill's stage never advances in this framework without matching a status
  transition in
  [`docs/roadmap/first-party-skills.md`](../roadmap/first-party-skills.md) in
  the same pull request.

## Worked examples: the two MVP stories

The Skill Development Framework is proven end to end by the two ratified MVP
user stories, which are the reference cases for every future skill charter:

- **MVP Story 1 — Autonomous Email Triage, Task Extraction, and Adaptive
  Management** exercises Open Loops and Tasks and Calendar and Commitments
  through stages 1–9: charter (dry-run onboarding, classification, adaptive
  rules), semantic contract (task/relationship provenance), capability
  design (per-account policy, confirmation tiers for irreversible actions),
  safety review (quarantine/undo for OTPs, no autonomous send/reply), and the
  six acceptance scenarios in
  [plan §MVP Story 1](../plan.md#mvp-story-1---autonomous-email-triage-task-extraction-and-adaptive-management)
  as stage-7 validation evidence.
- **MVP Story 2 — Collaborative Group Travel Planning** exercises Travel and
  Social Planning through the same stages, with capability design centered
  on the Grant/ConsentRecord/proposal seams and the disclosure ladder
  (Existence/Timing/Summary/Full), and the five acceptance scenarios in
  [plan §MVP Story 2](../plan.md#mvp-story-2---collaborative-group-travel-planning)
  as stage-7 validation evidence.
- Both stories gate the Phase 1B exit in
  [`prioritization-launch.md`](prioritization-launch.md#launch-stages-and-evidence-gates).

## Cross-references

- Catalog of record: [`docs/roadmap/first-party-skills.md`](../roadmap/first-party-skills.md)
- Sibling framework: [`docs/frameworks/channel-development.md`](channel-development.md)
- Prioritization and launch gates: [`docs/frameworks/prioritization-launch.md`](prioritization-launch.md)
- Feedback lifecycle consumed by "Dogfood" (stage 8) and "Operate" (stage
  10): issue #10 and its companion `docs/frameworks/feedback-support.md`
  (not duplicated here).
