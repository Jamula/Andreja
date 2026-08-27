# Channel connector catalog

## Status

This catalog is authoritative. It supersedes the seed summaries in
[`docs/plan.md`](../plan.md) — the "Initial channel connectors" list under
["Roadmap catalogs at a glance"](../plan.md#roadmap-catalogs-at-a-glance) and
the full table under
["Connector catalog and release bands"](../plan.md#connector-catalog-and-release-bands).
Per [plan §"Required lifecycle frameworks"](../plan.md#required-lifecycle-frameworks),
documentation CI must check the plan's seed summaries against this file
rather than the reverse. Every entry follows the lifecycle defined in
[`docs/frameworks/channel-development.md`](../frameworks/channel-development.md).

Phase codes map one-to-one to GitHub milestones. See the phase-to-milestone
map in
[`docs/frameworks/prioritization-launch.md`](../frameworks/prioritization-launch.md#phase-to-milestone-map).

## Scope boundary

- This document covers **channels/connectors** only — typed, permissioned
  connections to an external identity, assistant provider, content source,
  communication surface, publication target, notification destination,
  support system, or approved business service
  ([plan §Channel Development Framework](../plan.md#channel-development-framework)).
  First-party skills that consume these channels live in
  [`first-party-skills.md`](first-party-skills.md).
- Connector identity, assistant, content, feedback, and publishing grants
  remain separate even when the same provider appears in multiple rows — for
  example GitHub assistant auth, GitHub content, and GitHub feedback
  publishing are three independent grants with distinct tokens.
- The **Feedback and Support Framework**'s tenant-less public intake service
  is owned by Guinan under issue #10 and its companion
  `docs/frameworks/feedback-support.md`. The "Support/project" and
  "Notification" rows below describe only the channel/provider mechanics
  (GitHub Issues API, transactional email delivery); they do not redefine
  Guinan's intake, triage, or privacy-screening lifecycle.

## Ownership caveat and open questions

Owners listed in the catalog below are Squad's **proposed** Phase 0
assignment, not yet ratified per-connector charters. Two items are flagged
for explicit confirmation before the relevant row's charter (stage 1) merges:

- **Rows 18–19 (Financial/card data; Loyalty/rewards)** name Jett Reno as the
  accountable Channel Framework owner (per
  [`docs/operating-model.md`](../operating-model.md#workstream-charters-owners-and-boundaries)'s
  Channels and Connectors workstream: Jett Reno, Seven of Nine, Tuvok) and
  reposition Quark as economic-methodology domain input rather than the
  primary owner — Quark's ratified workstreams are Executive/Business and
  Platform Operations/FinOps, not Channels. **Open question:** should Quark
  instead be added as a named co-lead of the Channels workstream for
  financial/rewards connectors specifically (an `docs/operating-model.md`
  change), or does the domain-input framing above resolve it? Unresolved
  pending Phase 0 ratification by Picard/Jett Reno/Quark.
- Owners for connectors without an explicit named owner in `docs/plan.md`'s
  guardrail sections were inferred from charters and the team roster rather
  than lifted verbatim; confirm at each connector's charter issue.

## Status vocabulary

| Status | Meaning |
|---|---|
| `Charter pending` | Row exists in this catalog; the channel charter (audience, jobs-to-be-done, manual fallback) has not yet been drafted as a GitHub issue. |
| `Charter drafted` | A GitHub issue captures the charter; awaiting Phase 0 ratification or provider qualification. |
| `Provider qualification` | Official API/account types, terms, OAuth verification, and business viability research in progress or required before Phase 0 exit (Channel Development Framework step 2). |
| `Manifest defined` | Channel manifest (Channel Development Framework step 3) is agreed. |
| `Design review` | Threat/privacy design (Channel Development Framework step 4) is in progress or complete, adapter implementation not yet started. |
| `In implementation` | Adapter under construction (Channel Development Framework step 5), not yet validated or dogfooded. |
| `In validation` | Sandbox/conformance validation (Channel Development Framework step 6) is in progress — the first stage requiring a live provider account. |
| `Dogfood` | Read-only/draft-first internal use per Channel Development Framework step 7. |
| `Invite alpha` / `Private beta` / `Public beta` / `GA` | Release progression per Channel Development Framework step 8. |
| `Deferred` | Explicitly out of scope until a later phase or regulated decision. |
| `Deprecated` | Retired per Channel Development Framework step 10. |

As of this ratification pass (Phase 0), no connector has left `Charter
pending`/`Charter drafted`/`Provider qualification`. Status changes only
through a merged issue/PR that updates this table.

## Channel catalog

| # | Category | Capability | Owner(s) | Band | Phase(s) | Dependencies | Status | Evidence anchor | Constraints |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Local identity (passkeys/local recovery) | Authenticate (passkey/local recovery) | Jett Reno (self-host packaging); Tuvok (security) | MVP | 1A | Identity/Tenancy/Authorization platform capability | Charter pending | [plan §Identity, tenancy, and authorization foundations](../plan.md#identity-tenancy-and-authorization-foundations) | HTTPS/WebAuthn RP config, first-admin bootstrap, recovery/unlink protection required before Phase 1A exit. |
| 2 | Linked identity — Microsoft account, Google, GitHub; later Apple, LinkedIn, Facebook, enterprise OIDC | Authenticate (federated identity link) | Jett Reno; Tuvok | MVP/research | 1A–2 | Local identity | Charter pending | [plan §Identity, tenancy, and authorization foundations](../plan.md#identity-tenancy-and-authorization-foundations) | Unique `(Issuer, Subject)` mapping; never auto-link accounts on matching email alone. |
| 3 | Assistant providers — deterministic fake, OpenAI-compatible BYOK; real GitHub Copilot OAuth only after accepted provider-scope decision; later Azure AI/local | Query/generate (assistant provider calls) | Seven of Nine (provider abstraction); Jett Reno (channel packaging) | MVP | Fake/BYOK in 1A; Copilot no earlier than gated 1B | Identity/Tenancy/Authorization | Charter pending | [Proposed ADR 0009](../adr/0009-copilot-provider-phase-scope.md) | Phase 1A has no real Copilot provider. ADR 0009 proposes a limited, gated Phase 1B canary or later deferral and remains Proposed pending Cyrus acceptance; the requested toolchain outcome is not phase/activation approval. GitHub assistant auth, content connector, and feedback publishing use three distinct tokens/grants, never reused. |
| 4 | Email intake/send — Gmail, Outlook.com/Hotmail, Microsoft 365 | Query, send (email intake/send) | Jett Reno (channel); Seven of Nine (skill consumption); Guinan (notification consumer) | MVP | 1B core; 3A expansion | Assistant providers; Open Loops and Tasks; Calendar and Commitments | Charter pending | [MVP Story 1](../plan.md#mvp-story-1---autonomous-email-triage-task-extraction-and-adaptive-management) | Least-privilege delegated grants only; unreviewed third-party MCP servers never receive email tokens/content. |
| 5 | In-app messaging — user-to-Andreja, feedback/support status, scoped trip collaboration | Send, sync (in-app messaging) | Jett Reno; Guinan (support/status) | MVP/early | 1B trip/support; general peer in 6 | Identity/Tenancy/Authorization; Travel and Social Planning (Group Travel) | Charter pending | [MVP Story 2](../plan.md#mvp-story-2---collaborative-group-travel-planning) | No ambient cross-tenant chat; proposal/status delivery only until Phase 6 federation. |
| 6 | Discord — official bot/app only | Send, query (bot/slash commands) | Jett Reno; Neelix (community) | Early pilot | 3A | Identity/Tenancy/Authorization | Charter pending | [plan §Channel Development Framework](../plan.md#channel-development-framework) | Interaction-first (slash commands) to start; no self-bot/user-token automation; ambient message content needs a separately approved privileged-intent case. |
| 7 | Gaming/hobby communities — in-app groups/calendar; Xbox-approved and later game/community providers | Query, sync (group/calendar data) | Jett Reno; Neelix | Early/later | 4 manual; 8 connected | Hobbies and Social Groups Manager (skill) | Charter pending | [plan §Hobbies and Social Groups Manager guardrails](../plan.md#hobbies-and-social-groups-manager-guardrails) | Official access only; no private-history scraping or gambling facilitation. |
| 8 | WhatsApp — user share/export; official WhatsApp Business Platform where applicable | Send, sync (message/share export) | Jett Reno; Sarek (terms/business-account review) | Early constrained | 3A pilot; expand in 8 | Identity/Tenancy/Authorization | Provider qualification | [plan §Phase 3A — Email and messaging channel expansion](../plan.md#phase-3a---email-and-messaging-channel-expansion) | No unsupported personal-history automation; Business Platform pilot scheduled only after an approved business-account scenario; not required for Phase 3A exit. |
| 9 | File/content — OneDrive/SharePoint, Google Drive, GitHub, Box | Query, sync (file content) | Jett Reno; Seven of Nine (skill dependency) | Early | 3B | Identity/Tenancy/Authorization | Charter pending | [plan §Connector platform](../plan.md#connector-platform) | Explicit query-in-place vs. sync/import choice with no default; bounded cache TTL/size/content-type/purge-on-disconnect. |
| 10 | Photo context — user-selected OneDrive photo files and Google Photos Picker items | Query (photo metadata, user-selected) | Jett Reno; Deanna Troi (privacy/biometric) | Sensitive/later | 8 pilot; premium on-device clustering separately gated | File/content; Relationships and Communities Map (skill) | Charter pending | [plan §Relationships and Communities Map guardrails](../plan.md#relationships-and-communities-map-guardrails) | Provider-returned basic metadata only; no labels/albums promise, automatic identity, full-library default, or Picker-media clustering. |
| 11 | Calendar — Microsoft 365/Outlook and Google Calendar | Query, sync (calendar read/write) | Jett Reno; Seven of Nine (Calendar skill dependency) | Early/later | 1B bounded email-provider invite surface; 3B/8 full channel | Email intake/send; Calendar and Commitments (skill) | Charter pending | [MVP Story 1](../plan.md#mvp-story-1---autonomous-email-triage-task-extraction-and-adaptive-management) | Manual calendar skill works earlier; ambiguous/conflicting/external changes require step-up review. |
| 12 | Developer/professional brand — GitHub profile/README/repos, LinkedIn | Query, publish (profile/content) | Jett Reno; Neelix (Personal Brand Studio) | Early/research | GitHub in 3B; publishing in 8 | Personal Brand Studio (skill) | Charter pending | [plan §Personal Brand Studio guardrails](../plan.md#personal-brand-studio-guardrails) | Draft/preview first; every publication channel-scoped, user-confirmed, auditable, reversible where the channel permits. |
| 13 | Social brand — Facebook and Instagram professional surfaces; later approved channels | Publish (social posts) | Jett Reno; Neelix; Deanna Troi (authenticity/privacy) | Research/later | 8 | Personal Brand Studio (skill) | Provider qualification | [plan §Personal Brand Studio guardrails](../plan.md#personal-brand-studio-guardrails) | Publishing only where official APIs, account types, and review permit; no identifiable third-party disclosure without consent. |
| 14 | Support/project — GitHub Issues/PRs/stacks and app-owned feedback publishing | Query, publish (issues/PRs, feedback) | Jett Reno (channel mechanics); Guinan (feedback lifecycle owner, issue #10) | MVP/early | 1B–2 | Identity/Tenancy/Authorization | Charter pending | [plan §Feedback and Support Framework](../plan.md#feedback-and-support-framework) | Private triage queue before public issue; see issue #10 for the full intake/lifecycle contract — not redefined here. |
| 15 | Notification — transactional email first; later mobile push and additional channels | Send (notification delivery) | Jett Reno; Guinan (support acknowledgments consumer) | MVP/early | 1B–2 | Email intake/send | Charter pending | [plan §Feedback and Support Framework](../plan.md#feedback-and-support-framework) | Sender identity, SPF/DKIM/DMARC, bounce/complaint handling required; no marketing mail without separate consent. |
| 16 | Sponsorship/payment — approved project sponsorship provider only | Query (sponsorship/payment status) | Quark (FinOps); Sarek (legal/tax); Jett Reno (integration) | Research | 0 policy; later activation | None (project-level, not user data) | Research | [plan §Initial sustainability model](../plan.md#initial-sustainability-model) | No user financial-data connector; sponsors receive no personal data, targeted ads, or privileged access. |
| 17 | Additional storage — Dropbox and other partner stores | Query, sync (file storage) | Jett Reno | Later | 8+ | File/content | Deferred | [plan §Connector platform](../plan.md#connector-platform) | Add only after connector contract and demand evidence exist. |
| 18 | Financial/card data — approved aggregators and provider/partner APIs (Quicken products, Chase, American Express, and successors) | Query (read-only financial/card data) | Jett Reno (adapter/Channel Framework owner); Tuvok (security); Sarek (regulatory); Quark (economic methodology input) | Regulated/later | 11 research/pilot | Lifestyle Rewards and Financial Optimization (skill); Miles and Points Manager (skill) | Deferred | [plan §Lifestyle rewards, miles, and points guardrails](../plan.md#lifestyle-rewards-miles-and-points-guardrails) | Read-only first; tokenized data only; no credential scraping; confirmed provider-supported actions require regulated gates. |
| 19 | Loyalty/rewards — AwardWallet, Rakuten, CardPointers, Bilt, airline/hotel/card reward programs | Query (loyalty/rewards balances) | Jett Reno (adapter/Channel Framework owner); Quark (economic methodology input) | Regulated/later | 11 research/pilot | Miles and Points Manager (skill) | Deferred | [plan §Lifestyle rewards, miles, and points guardrails](../plan.md#lifestyle-rewards-miles-and-points-guardrails) | Balances/expiry/status/benefits/transfers/redemption proposals and receipts only; no credential scraping. |
| 20 | Employer benefits/perks — user documents first; later approved HR/benefits, retirement, charitable-match, wellness, commuter, education, perk-provider channels | Query (benefits/perks documents) | Quark; Sarek; Jett Reno | Sensitive/later | 11 research/pilot | Employer Benefits and Perks Manager (skill) | Deferred | [plan §Employer Benefits and Perks Manager guardrails](../plan.md#employer-benefits-and-perks-manager-guardrails) | Eligibility/match/deadlines/elections/claims and user-confirmed official actions only; no payroll/portal credential scraping. |
| 21 | Health/wellbeing — user-controlled documents first; later SMART on FHIR/FHIR, patient portals, Apple Health/HealthKit, Android Health Connect, pharmacy/lab/imaging, approved wearables | Query (health records, read-only) | Beverly Crusher (safety); Jett Reno (adapter); Deanna Troi (privacy) | Sensitive/later | 4 manual; 12 connected | Health and Wellbeing Manager (skill) | Deferred | [plan §Health and Wellbeing Manager guardrails](../plan.md#health-and-wellbeing-manager-guardrails) | Highest-sensitivity records; no autonomous diagnosis or medication change; original clinical artifact preserved. |
| 22 | Household/assets — user documents/email/calendar first; later approved insurance, vehicle/telematics, utility, warranty, receipt, contractor/home-service, property channels | Query (household/asset documents) | Jett Reno; Quark (cost/deal methodology) | Sensitive/later | 4 manual; 5–6 collaboration; 8 connected | Household, Vehicle, Insurance and Projects Manager (skill) | Deferred | [plan §Household, Vehicle, Insurance and Projects Manager guardrails](../plan.md#household-vehicle-insurance-and-projects-manager-guardrails) | No credential/access-code scraping (insurer/utility/vehicle portals, alarm systems, garage codes). |
| 23 | Brokerage/trading execution | Query (research/market data only) | None assigned — explicitly deferred | Deferred | Unscheduled | Trading Research and Review (skill) | Deferred | [plan §Explicitly not day-one work](../plan.md#explicitly-not-day-one-work) | Research/checklists only; market data, brokerage access, and order execution require a separate regulated decision. |

## Delivery-topology note

Per [plan §Channel Development Framework step 3](../plan.md#channel-development-framework),
every channel manifest above additionally requires a provider delivery-topology
ADR (polling/manual, watch renewal, webhook, Pub/Sub, gateway/socket, public
callback, NAT/egress, optional Andreja relay) before implementation. Rows 4
(Email intake/send) and 11 (Calendar) are the first channels expected to
produce that ADR, per the Phase 3A delivery-topology deliverables in
[plan §Phase 3A](../plan.md#phase-3a---email-and-messaging-channel-expansion).

## MVP story mapping

- **MVP Story 1 — Email Triage** requires rows 3 (Assistant providers) and 4
  (Email intake/send), with row 11 (Calendar) for the bounded invite-accept
  surface. See
  [`first-party-skills.md`](first-party-skills.md#mvp-story-mapping).
- **MVP Story 2 — Group Travel** requires row 5 (In-app messaging) for
  proposal/status delivery inside the trip workspace. See
  [`first-party-skills.md`](first-party-skills.md#mvp-story-mapping).

## Phase 0 boundary

Phase 0 paper/qualification research is active for every connector plan.md
names in its Phase 0 deliverables and open investigations (see
[plan §Phase 0 deliverables](../plan.md#phase-0---govern-and-decide) and
[plan §Open Phase 0 investigations](../plan.md#open-phase-0-investigations)),
not sponsorship alone:

- Row 4 (Email intake/send) and row 15 (Notification) — Gmail, Outlook.com/
  Hotmail, Microsoft 365, and transactional outbound email/notification
  provider terms, deliverability, and SPF/DKIM/DMARC research.
- Row 6 (Discord) — official bot/app terms, scopes, and verification
  research.
- Row 8 (WhatsApp) — user-share/export and official WhatsApp Business
  Platform terms research.
- Row 9 (File/content) — OneDrive/SharePoint, Google Drive, GitHub, and Box
  API scopes, terms, limits, and delta/webhook-behavior research.
- Row 12 (Developer/professional brand) and row 13 (Social brand) —
  Personal Brand Studio's official API access research for GitHub, LinkedIn,
  Facebook, and Instagram.
- Row 16 (Sponsorship/payment) — sponsorship policy and provider research.

All of the above is **local and paper research only** — no provider sandbox
account, OAuth app registration, or trial tier is created for any row — under
the $0 no-cloud-provisioning rule documented in
[`docs/frameworks/prioritization-launch.md`](../frameworks/prioritization-launch.md#phase-0-0-no-cloud-rule)
and [ADR 0000](../adr/0000-plan-ratification.md). Every connector remains at
`Charter pending`/`Charter drafted`/`Provider qualification` with no
provider sandbox accounts created until Phase 1A/1B budget and legal
approval exist.

## Consistency rule

Every row's Phase(s), Dependencies, and Constraints must trace to a section of
`docs/plan.md`, an ADR, or a GitHub issue — never to an unlinked assumption.
When a connector's charter issue changes scope, owner, phase, or status,
update this table in the same pull request.
