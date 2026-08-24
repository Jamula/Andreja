# First-party skill catalog

## Status

This catalog is authoritative. It supersedes the seed summaries in
[`docs/plan.md`](../plan.md) — the "Initial first-party skills" list under
["Roadmap catalogs at a glance"](../plan.md#roadmap-catalogs-at-a-glance) and
the full table under
["Initial first-party skill catalog"](../plan.md#initial-first-party-skill-catalog).
Per [plan §"Required lifecycle frameworks"](../plan.md#required-lifecycle-frameworks),
after Phase 0 ratifies this catalog, documentation CI must check the plan's
seed summaries against this file rather than the reverse. Every entry follows
the lifecycle defined in
[`docs/frameworks/skill-development.md`](../frameworks/skill-development.md).

Phase codes (`0`, `1A`, `1B`, `2`, `3A`, `3B`, `4`–`12`) map one-to-one to
GitHub milestones. See the phase-to-milestone map in
[`docs/frameworks/prioritization-launch.md`](../frameworks/prioritization-launch.md#phase-to-milestone-map).

## Scope boundary

- This document covers **first-party skills** only. Channel/connector entries
  live in [`channel-connectors.md`](channel-connectors.md).
- The **Feedback and Support Framework** (intake, triage, routing, privacy
  screening of user feedback) is owned by Guinan under issue #10 and its
  companion `docs/frameworks/feedback-support.md`. It is not duplicated here;
  skills only reference it as a consumed capability (every skill's dogfood
  stage captures feedback through Guinan's workflow).
- Platform capabilities below are privileged product functions, not skills.
  They cannot be granted through a skill manifest
  (see [plan §"Skill ecosystem"](../plan.md#skill-ecosystem)).

## Status vocabulary

| Status | Meaning |
|---|---|
| `Charter pending` | Row exists in this catalog; the numbered charter (outcome, owner, non-goals, stop criteria) has not yet been drafted as a GitHub issue. |
| `Charter drafted` | A GitHub issue captures the charter; awaiting Phase 0 ratification or dependency readiness. |
| `Research` | Phase 0 (or later) research/spike explicitly required before implementation, per plan.md. |
| `Contract defined` | Semantic/domain contract and capability/permission design (Skill Development Framework steps 2–3) are agreed. |
| `In implementation` | Behind `ISkillHost`, not yet dogfooded. |
| `Dogfood` | Smallest complete vertical slice running for Cyrus/Customer Zero. |
| `Invite alpha` / `Private beta` / `Public beta` / `GA` | Release progression per Skill Development Framework step 9. |
| `Deprecated` | Retired per Skill Development Framework step 11. |

As of this ratification pass (Phase 0), no first-party skill has left
`Charter pending`/`Charter drafted`/`Research`. Status changes only through a
merged issue/PR that updates this table — never through comments alone.

## Platform capabilities (privileged, not skills)

| Capability | Owner(s) | Phase(s) | Dependencies | Status | Evidence anchor | Constraints |
|---|---|---|---|---|---|---|
| Assistant Runtime and Review | Seven of Nine (assistant/provider abstraction); T'Pol (implementation) | 1A–2 | Identity/Tenancy/Authorization; Skill Host | Charter pending | [plan §Assistant and AI architecture](../plan.md#assistant-and-ai-architecture) | No provider SDK types cross into domain/UI; all writes are confirmed proposals; `CaptureContent=false` verified independently. |
| Data Ownership and Privacy | Deanna Troi (privacy engineering); T'Pol (export/delete implementation) | 1A onward | Identity/Tenancy/Authorization | Charter pending | [plan §Privacy engineering](../plan.md#privacy-engineering) | Export/restore/retention/delete/consent/grant review/audit; sensitivity labels enforce behavior, not decoration. |
| Identity, Tenancy, Authorization | Tuvok (security); Spock (architecture); T'Pol (tenancy foundations) | 1A onward | None (foundational) | Charter pending | [plan §Identity, tenancy, and authorization foundations](../plan.md#identity-tenancy-and-authorization-foundations) | Tenant isolation enforced in code and database; two-tenant automated tests from first persistence release. |
| Skill Host, Channel Host, Semantic Profile Host, Feedback and Operability | Seven of Nine (skill/semantic host); Jett Reno (channel host); Guinan (feedback intake, see issue #10) | 0 research; 1A onward | Identity/Tenancy/Authorization | Research | [plan §Skill ecosystem](../plan.md#skill-ecosystem) | Skills never receive `DbContext`, secrets, or unrestricted service-provider access. |

## First-party skill catalog

| # | Skill | Capability | Owner(s) | Launch band | Phase(s) | Dependencies | Status | Evidence anchor | Constraints |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Open Loops and Tasks | Capture, triage, and track open loops/tasks from any source to completion | Seven of Nine (skill framework); T'Pol (implementation) | MVP | 1A–2 | Assistant Runtime and Review; Skill Host; Identity/Tenancy/Authorization | Charter pending | [plan §Core product roadmap](../plan.md#core-product-roadmap); [MVP Story 1](../plan.md#mvp-story-1---autonomous-email-triage-task-extraction-and-adaptive-management) | First skill routed through `ISkillHost`; every task traceable to source, editable, completable, undoable. |
| 2 | Calendar and Commitments | Surface, propose, and track calendar commitments and scheduling conflicts | Seven of Nine; T'Pol | MVP/early | 1B bounded invite intake/accept under email grant; 2 manual; 3B full channel | Open Loops and Tasks; Email intake/send channel (Phase 1B); Travel and Social Planning (Group Travel) | Charter pending | [MVP Story 1](../plan.md#mvp-story-1---autonomous-email-triage-task-extraction-and-adaptive-management); [MVP Story 2](../plan.md#mvp-story-2---collaborative-group-travel-planning) | Ambiguous/conflicting/external/consequential changes require step-up review; no autonomous send/reply/forward. |
| 3 | Personal Semantic Profile | Build and maintain a reviewable personal semantic knowledge graph | Seven of Nine (research lead); Spock (ontology/architecture); T'Pol (persistence); Deanna Troi (privacy/inference); Tuvok (authorization); Data (quality/provenance) | Research/early | 0 research; 2 profile | Skill Host; Data Ownership and Privacy | Research | [plan §Personal semantic graph](../plan.md#personal-semantic-graph) | Inferences are reviewable proposals, not facts; no covert scoring; user can inspect/correct/reject/expire/export/delete. |
| 4 | Personal Brand Studio | Draft and preview personal/professional brand content for user approval | Neelix (product/market); Seven of Nine (skill/semantic); Jadzia Dax (authoring UX); Deanna Troi (privacy/authenticity); Tuvok (connector/publishing security); Data (evidence quality) | Early | 0 research; 2 dogfood; 8 publishing | Personal Semantic Profile; Developer/professional brand channel; Social brand channel | Charter pending | [plan §Personal Brand Studio guardrails](../plan.md#personal-brand-studio-guardrails) | Draft/preview first — no auto-publish; claims require user-approved evidence; no fabricated employers/achievements/relationships. |
| 5 | Finance Administration | Track bills, banking follow-ups, and budgets/checklists | Quark (financial domain); Seven of Nine (skill framework) | Early | 2 | Open Loops and Tasks | Charter pending | [plan §Core product roadmap](../plan.md#core-product-roadmap) | Bills/banking follow-ups/budgets/checklists only — no money movement or trading execution. |
| 6 | Family and Relationships | Coordinate consent-aware family/relationship commitments and waiting-for items | Deanna Troi (consent-aware coordination); Seven of Nine | Early | 4; sharing in 6 | Personal Semantic Profile; Calendar and Commitments; Grants/Consent (federation seams) | Charter pending | [plan §Sharing, consent, and federation foundations](../plan.md#sharing-consent-and-federation-foundations) | Waiting-for/consent-aware coordination; no ambient cross-tenant access without a grant. |
| 7 | Health and Wellbeing Manager | Organize health/wellbeing follow-ups and reminders without diagnosis or prescribing | Beverly Crusher (health outcomes/safety); Seven of Nine (skill/channel architecture); Tuvok (access/action security); Deanna Troi (sensitive-data privacy); Sarek (legal/regulatory); Data (provenance/reconciliation) | Sensitive/later | 4 manual; 12 connected | Health/wellbeing channel (Phase 12); Data Ownership and Privacy | Charter pending | [plan §Health and Wellbeing Manager guardrails](../plan.md#health-and-wellbeing-manager-guardrails) | No diagnosis/prescribing/medication-change claims; highest-sensitivity data handling; no autonomous image interpretation. |
| 8 | Household, Vehicle, Insurance and Projects Manager | Track household, vehicle, insurance, and project follow-ups and comparisons | Jett Reno (workflow); Quark (cost/deal methodology); Seven of Nine (skills/channels); Tuvok (property/action security); Deanna Troi (household privacy); Sarek (insurance/contract boundaries); Data (comparison provenance) | Early/later | 4 manual; household collaboration in 5–6; connected in 8 | Household/assets channel; Finance Administration | Charter pending | [plan §Household, Vehicle, Insurance and Projects Manager guardrails](../plan.md#household-vehicle-insurance-and-projects-manager-guardrails) | Insurance guidance is comparison/organization, not licensed advice; no stored insurer/vehicle/alarm credentials. |
| 9 | Travel and Social Planning | Plan and coordinate group travel proposals across participants | Seven of Nine (skill/semantic); Tuvok (grants/consent security); T'Pol (tenancy) | Early | 1B trip-workspace MVP slice; 4 full skill; broader sharing in 6 | Calendar and Commitments; In-app messaging channel; Grants/ConsentRecord/proposal contracts | Charter pending | [MVP Story 2](../plan.md#mvp-story-2---collaborative-group-travel-planning) | Reference-based projections only; never expose unrelated calendar/finance/health/location; host tenant authoritative, participants authoritative for own data. |
| 10 | Interests, Reading, and Podcasts | Queue and recommend interests, reading, and podcast content from user sources | Seven of Nine (skill framework) | Early | 2 | Open Loops and Tasks | Charter pending | [plan §Core product roadmap](../plan.md#core-product-roadmap) | Queues/notes/recommendations from user-selected sources only. |
| 11 | Trading Research and Review | Maintain trading watchlists, thesis notes, and journal reminders (no execution) | Quark (valuation/financial framing); Seven of Nine (skill framework) | Later first-party | 4 | Open Loops and Tasks | Charter pending | [plan §Core product roadmap](../plan.md#core-product-roadmap) | Watchlists/thesis/journal reminders only; no brokerage connection or order execution (explicitly deferred, see [plan §Explicitly not day-one work](../plan.md#explicitly-not-day-one-work)). |
| 12 | Lifestyle Rewards and Financial Optimization | Aggregate lifestyle rewards/points value and optimization opportunities read-only | Quark (economic/value methodology); Seven of Nine (skills/channels); Tuvok (financial-action security); Deanna Troi (data minimization); Sarek (regulatory/terms); Data (reconciliation quality) | Regulated/later | 11 | Miles and Points Manager; Loyalty/rewards channel; Finance Administration | Charter pending | [plan §Lifestyle rewards, miles, and points guardrails](../plan.md#lifestyle-rewards-miles-and-points-guardrails) | Read-only aggregation by default; no stored card numbers/CVV/passwords; no credential scraping or card churning. |
| 13 | Miles and Points Manager | Track miles/points balances, transfer ratios, and time-sensitive valuations | Quark; Seven of Nine; Tuvok; Deanna Troi; Sarek; Data (same accountable set as row 12) | Regulated/later | 11 | Loyalty/rewards channel | Charter pending | [plan §Lifestyle rewards, miles, and points guardrails](../plan.md#lifestyle-rewards-miles-and-points-guardrails) | Valuations/transfer ratios/fees shown as time-sensitive estimates, never guaranteed value or financial advice. |
| 14 | Employer Benefits and Perks Manager | Inventory and remind on employer benefits/perks and what-if scenarios | Quark (benefit-value methodology); Seven of Nine (skill/channel design); Sarek (plan/tax/legal boundaries); Tuvok (high-assurance actions); Deanna Troi (privacy); Beverly Crusher (health-benefit boundaries); Data (reconciliation) | Sensitive/later | 11 | Employer benefits/perks channel; Finance Administration | Charter pending | [plan §Employer Benefits and Perks Manager guardrails](../plan.md#employer-benefits-and-perks-manager-guardrails) | Document/inventory/reminder/what-if only; no individualized tax/investment/ERISA/legal advice; no stored employer/payroll passwords. |
| 15 | Hobbies and Social Groups Manager | Coordinate hobby/social group scheduling and communication | Neelix (community/group outcomes); Seven of Nine (skill/channel integration); Deanna Troi (relationship/privacy); Tuvok (messaging/access abuse); Data (record quality) | Early/later | 4 manual; 8 connected | Discord channel; Gaming/hobby communities channel; Calendar and Commitments | Charter pending | [plan §Hobbies and Social Groups Manager guardrails](../plan.md#hobbies-and-social-groups-manager-guardrails) | In-app/calendar/manual first; no shadow profiles or full group-history import by default; no wagering execution. |
| 16 | Life Context and Opportunity Navigator | Surface personal opportunities/context from the semantic profile and connections | Seven of Nine (semantic/assistant design); Deanna Troi (human/privacy boundaries); Picard (outcome strategy); Neelix (community/opportunity framing); Tuvok (access/abuse controls); Data (evidence quality) | Core/iterative | 0 research; 2 personal insights; 6 mutual connections | Personal Semantic Profile; Relationships and Communities Map; Life Event Planner | Research | [plan §Life Context and Opportunity Navigator guardrails](../plan.md#life-context-and-opportunity-navigator-guardrails) | User defines goals/values/tradeoffs; no hidden engagement/wealth/social score; no ranking people by worth. |
| 17 | Life Event Planner | Compare cross-domain life-event scenarios and produce handoff packets | Seven of Nine (cross-skill orchestration); Picard (outcome/decision framing); Quark (financial scenarios); Beverly Crusher (care/health boundaries); Sarek (legal/tax boundaries); Deanna Troi (family consent); Data (assumptions/evidence) | Cross-domain | 4 manual; 5–6 collaboration; 11–12 connected evidence | Personal Semantic Profile; Finance Administration; Health and Wellbeing Manager; Employer Benefits and Perks Manager; Household Manager | Charter pending | [plan §Life Event Planner guardrails](../plan.md#life-event-planner-guardrails) | Scenario comparisons only, never guaranteed outcomes; produces professional-handoff packets rather than replacing professionals. |
| 18 | Relationships and Communities Map | Maintain a reviewable map of relationships and communities | Deanna Troi (relationship/privacy requirements); Seven of Nine (semantic/photo integration); Tuvok (biometric/location/access security); Neelix (community use cases); Data (provenance/quality) | Sensitive/iterative | 1B email-derived hypotheses; 2 manual; 8 photo/channel context; premium on-device clustering later | Personal Semantic Profile; Photo context channel; Email intake/send channel (Phase 1B) | Charter pending | [plan §Relationships and Communities Map guardrails](../plan.md#relationships-and-communities-map-guardrails) | No autonomous adversarial/value labels; no sensitive-trait inference from appearance/location/photos; Picker-derived media excluded from face clustering. |
| 19 | Small Business and Entrepreneur Manager | Support small-business/entrepreneur admin, customers, and analytics | Picard (business outcomes); Quark (financial administration); Seven of Nine (skill/channel contracts); Guinan (customer/support workflows); Neelix (marketing/community); Tuvok/Deanna Troi/Sarek (trust/legal boundaries); Data (reconciliation) | Business/sensitive | 4 Customer Zero/manual; 8 connectors; 10 analytics | Finance Administration; Family and Relationships (persona separation); Data Ownership and Privacy | Charter pending | [plan §Small Business and Entrepreneur Manager guardrails](../plan.md#small-business-and-entrepreneur-manager-guardrails) | Business persona kept separate from personal persona; banking/payroll/payments/filings require official channels and explicit authorization. |

## MVP story mapping

The two ratified MVP user stories exercise this catalog end-to-end and are the
worked examples referenced in
[`docs/frameworks/skill-development.md`](../frameworks/skill-development.md)
and [`docs/frameworks/prioritization-launch.md`](../frameworks/prioritization-launch.md):

- **MVP Story 1 — Autonomous Email Triage, Task Extraction, and Adaptive
  Management** composes Open Loops and Tasks (#1) and Calendar and
  Commitments (#2) over the Email intake/send channel, gated by the Assistant
  Runtime and Review and Identity/Tenancy/Authorization platform
  capabilities. Full acceptance scenarios: [plan §MVP Story
  1](../plan.md#mvp-story-1---autonomous-email-triage-task-extraction-and-adaptive-management).
- **MVP Story 2 — Collaborative Group Travel Planning** composes Travel and
  Social Planning (#9) and Calendar and Commitments (#2) over the In-app
  messaging channel and the Grant/ConsentRecord/proposal seams, gated by
  Identity/Tenancy/Authorization. Full acceptance scenarios: [plan §MVP Story
  2](../plan.md#mvp-story-2---collaborative-group-travel-planning).
- Both stories are the Phase 1B exit-gate evidence in
  [`docs/frameworks/prioritization-launch.md`](../frameworks/prioritization-launch.md#launch-stages-and-evidence-gates).

## Phase 0 boundary

All research rows above (Personal Semantic Profile, Life Context and
Opportunity Navigator, and the Phase 0 slice of Personal Brand Studio) are
**local and paper research only** under the ratified $0 no-cloud-provisioning
rule. See
[`docs/frameworks/prioritization-launch.md`](../frameworks/prioritization-launch.md#phase-0-0-no-cloud-rule)
and [ADR 0000](../adr/0000-plan-ratification.md).

## Consistency rule

Every row's Phase(s), Dependencies, and Constraints must trace to a section of
`docs/plan.md`, an ADR, or a GitHub issue — never to an unlinked assumption.
When a skill's charter issue changes scope, owner, phase, or status, update
this table in the same pull request.
