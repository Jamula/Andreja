# Regulatory applicability and horizon-scanning framework

**Status:** Draft for Cyrus and qualified counsel review. Legal-research and governance
artifact only.

## Not legal advice, no privilege, no authority to bind

This document is Sarek's (General Counsel and Regulatory Research Lead) research
artifact under `.squad/agents/sarek/charter.md` and the Legal and regulatory charter in
`docs/plan.md`. Read every claim below with these boundaries in force:

- **Not legal advice.** Nothing here is a legal opinion, a compliance certification, or a
  substitute for jurisdiction-appropriate qualified counsel. It is a structured research
  hypothesis with sources, so a human decision-maker and counsel can evaluate it faster.
- **No privilege.** This file lives in a version-controlled repository and, per
  `.squad/directives.md`, must never contain privileged, confidential-counsel, or
  regulatory-inquiry material. Privileged analysis belongs in the confidential paths
  described below, not here.
- **No authority to sign, file, approve, or launch.** Sarek (the agent) does not
  interpret law as binding, create attorney-client privilege, sign contracts, submit
  regulatory filings, or approve a launch decision. Only qualified, jurisdiction-appropriate
  counsel and Cyrus (as the accountable human) can do that, per `.squad/agents/sarek/charter.md`
  and `docs/plan.md` (Business leadership responsibilities, Review gates).
- **Agent research is not a public claim.** A legal hypothesis in this document never
  silently becomes a marketing claim, a public compliance statement, or a technical
  authorization rule. Product policy and engineering controls are separate artifacts that
  must be explicitly approved before they cite this research.
- **Currency caveat.** Primary-source links and effective dates below were checked at the
  time noted in each entry. Regulatory status — especially proposed legislation, agency
  rulemakings, and enforcement posture — changes quickly and sometimes reverses (see the
  Colorado AI Act entry). Before relying on any entry for a decision, re-check the
  authoritative source, not this document, and prefer counsel's read of the live text.

## How to read this document

### Status taxonomy

Every entry below is tagged with one status so nobody treats a proposal as settled law or
a settled law as merely aspirational:

| Tag | Meaning |
|---|---|
| **Enacted** | Passed by the legislature/signed, but not yet legally operative. |
| **Effective** | Currently in force and enforceable. |
| **Proposed** | Bill, rulemaking, or ballot measure introduced but not enacted. |
| **Guidance** | Agency interpretation, technical assistance, or enforcement policy that is not binding law but signals enforcement risk. |
| **Contract term** | Obligation created by a provider/platform/vendor agreement, not by government action. |
| **Platform policy** | Non-negotiable rule imposed by an app store, aggregator, or connector provider (distinct from a bilateral contract). |

### Applicability matrix dimensions

Per the Legal and regulatory charter (`docs/plan.md`, "Legal and regulatory charter"),
Andreja maintains a versioned applicability matrix across these dimensions. Every
regulated-feature row in the ledger (see "Initial Phase 1A applicability assessment")
must be evaluated against all eight:

| Dimension | Definition | Andreja examples |
|---|---|---|
| **Capability** | Platform capability or first-party skill named in `docs/plan.md` roadmap catalogs. | Assistant Runtime, Identity/Tenancy, Finance Administration, Health and Wellbeing Manager, Miles and Points Manager. |
| **Data class** | Sensitivity tier of the data touched. | Public/marketing content; ordinary personal data; sensitive categories (health, biometric, precise location, financial account, children's data, immigration/religion/sexuality inferences). |
| **User/account type** | Who the data belongs to and their account status. | Adult individual user (adults-only policy), household/family tenant member, non-user `Contact` record, business-tenant customer/employee/vendor, minor referenced by an adult user's records (not an account holder). |
| **Actor/role** | Who or what performs the action. | End user, Andreja assistant/skill acting on a proposal, connector on the user's behalf, Andreja as data controller/processor, Andreja as a marketplace/platform intermediary. |
| **Transaction/action** | The specific act with legal consequence. | Read-only aggregation vs. consequential action (redemption, enrollment, filing, payment, publication), automated decision, disclosure to a third party, breach/incident. |
| **Deployment model** | Where and how the instance runs. | Self-hosted (user-owned data plane), Andreja-managed cloud hosting, optional control-plane services (Phase 0-era: none provisioned). |
| **Geography** | Jurisdiction(s) with a real nexus. | U.S. federal; specific U.S. states/localities where a user, provider, or Andreja entity is located; international only when a real nexus exists (`docs/plan.md`, Legal and regulatory charter). |
| **Launch stage** | Phase and launch-stage gate from the Roadmap prioritization and launch framework. | Phase 0 (no launch, no users, no cloud provisioning); Phase 1A self-hosted walking skeleton; later regulated-feature phases (4, 5-6, 8, 11, 12). |

A matrix row is not "done" until it states an applicability hypothesis, cites an
authoritative source, records the counsel question raised, names an owner, and links the
obligations/controls/evidence, deadline, residual risk, and re-review trigger. This
document is the narrative horizon scan; the row-level matrix is tracked in GitHub Issues
and linked ADRs so it stays synchronized with actual delivery work rather than duplicating
`docs/plan.md`.

### Current footprint bounds what applies today

Per `.squad/directives.md` (Phase 0 boundary) and `docs/plan.md` (Phase 0 deliverables),
Andreja currently has: a private GitHub repository, no cloud accounts or provisioning, no
external users, no revenue, and no public site. Most of the domain sections below describe
**future** applicability that attaches once a specific capability, deployment, or user
population goes live — they are horizon scanning, not a claim that Andreja is currently
subject to enforcement action. Each section states the trigger that would make it live.

International jurisdictions are intentionally out of scope until a specific offering,
user base, staff member, or provider creates a real nexus, per the Legal and regulatory
charter. This document therefore scans U.S. federal, state, and (where material) local law
first.

---

## 1. Privacy, data brokers, biometrics, and children

**Why this matters for Andreja:** the entire product is built on a personal semantic
graph, sharing/consent/federation model, and sensitive first-party skills (Health and
Wellbeing Manager, Relationships and Communities Map, Family and Relationships, Life
Context and Opportunity Navigator) that process personal and sensitive data by design
(`docs/plan.md`, "Personal semantic graph", "Sharing, consent, and federation
foundations"). The product also has an explicit adults-only account boundary and excludes
biometric face clustering pending a dedicated ADR (`docs/plan.md`, "Relationships and
Communities Map guardrails").

| Source | Status | Date checked | Applicability hypothesis |
|---|---|---|---|
| [California Consumer Privacy Act / CPRA](https://oag.ca.gov/privacy/ccpa) | Effective (CCPA 2020; CPRA amendments 2023) | 2026-08-23 | Applies if Andreja meets CCPA/CPRA business thresholds (revenue, records, or data-sale/share volume) and processes California residents' data — relevant once managed hosting or self-host users include California residents. |
| [Virginia Consumer Data Protection Act](https://law.lis.virginia.gov/vacode/title59.1/chapter53/) | Effective (Jan. 1, 2023) | 2026-08-23 | Template for the "second wave" state comprehensive privacy laws; representative of controller/processor obligations, data protection assessments, and sensitive-data opt-in consent. |
| Multi-state comprehensive privacy law tracker | Mixed enacted/effective/proposed — count and effective dates change frequently | 2026-08-23 | At least 20+ states have enacted comprehensive consumer privacy laws with staggered effective dates through 2028; do not rely on a fixed count. Re-pull a current tracker (for example the [IAPP US State Privacy Legislation Tracker](https://iapp.org/resources/article/us-state-privacy-legislation-tracker/) or counsel's internal matrix) at each review rather than trusting any cached list, including this one. |
| [FTC COPPA Rule](https://www.ftc.gov/legal-library/browse/rules/childrens-online-privacy-protection-rule-coppa) and [2025 final amendments](https://www.federalregister.gov/documents/2025/04/22/2025-04426/childrens-online-privacy-protection-rule) | Enacted amendments; effective June 23, 2025; full compliance April 22, 2026 | 2026-08-23 | Andreja's adults-only account policy is the primary control. COPPA still matters wherever a skill stores information *about* a minor referenced by an adult user (Life Event Planner "college for children," Family and Relationships, Household member records) without treating the minor as an account holder or knowingly collecting data directly from a child. The 2025 amendments add biometric identifiers to "personal information," a written retention/deletion policy requirement, and separate verifiable parental consent for third-party disclosure — all relevant if any future skill or channel could be "directed to children" or a "mixed audience" service. |
| [Illinois Biometric Information Privacy Act (BIPA)](https://www.ilga.gov/Documents/legislation/ilcs/documents/074000140K5.htm) (as amended by [Public Act 103-0769](https://www.ilga.gov/Legislation/PublicActs/PrinterFriendly/103-0769), effective Aug. 2, 2024) | Effective | 2026-08-23 | Directly implicated by the deferred "premium on-device face clustering" feature (`docs/plan.md`, Relationships and Communities Map guardrails), which is explicitly gated behind a future biometric/legal/privacy/security ADR. Requires written policy, retention schedule, and consent (now expressly permitting electronic signature) before any biometric identifier/template is collected; 2024 amendment caps damages per person rather than per scan but preserves the private right of action. |
| [Texas Capture or Use of Biometric Identifier Act (CUBI)](https://statutes.capitol.texas.gov/Docs/BC/htm/BC.503.htm) | Effective | 2026-08-23 | No private right of action (Texas AG enforcement only, with large settlements on record), notice/consent and destruction-timeline requirements comparable to BIPA. |
| [Washington biometric privacy law, RCW 19.375](https://app.leg.wa.gov/rcw/default.aspx?cite=19.375) | Effective | 2026-08-23 | Notice/consent and security/destruction obligations; AG enforcement only. |
| Data broker registration laws (California [Delete Act](https://www.oag.ca.gov/data-brokers), Vermont, Texas, Oregon data broker registries) | Effective in multiple states | 2026-08-23 | Relevant only if Andreja itself sells or licenses personal data to third parties for their own use, which is not the current business model (`docs/plan.md` treats user data as user-owned, not a data product); re-review if any future monetization approaches data brokerage. |
| State genetic-privacy and reproductive/location-privacy laws (e.g., Washington My Health My Data Act, RCW 19.373) | Effective (2023-2024) | 2026-08-23 | Relevant to Health and Wellbeing Manager and to precise-location handling called out in Relationships and Communities Map guardrails. |

**Counsel questions:** Does self-hosting under a user's own control change "business"/
"controller" status under CCPA/CPRA and comparable state laws? Does Andreja (the company)
ever become a data controller for managed-cloud tenants, and if so under which state
thresholds? What is the compliant consent/retention design for any future biometric
face-clustering feature before that ADR is drafted?

**Obligations/controls/evidence:** privacy notice and lawful-basis analysis per deployment
model; data-subject access/deletion/export mechanisms (already required by the Data
Ownership and Privacy platform capability); sensitive-data classification tiers with
biometric/health/precise-location/children flagged as highest sensitivity
(`docs/plan.md`, Privacy engineering); no biometric processing without the dedicated ADR.

**Deadlines:** none currently binding (Phase 0, no launch). Re-review before Phase 1B
managed-cloud hosting, before any biometric ADR, and before onboarding the first
non-Cyrus user.

**Residual risk:** high long-run complexity from a 20+ state patchwork with staggering
effective dates; mitigate by designing for the strictest applicable state baseline rather
than per-state branching where feasible, subject to counsel review.

**Re-review trigger:** any new state comprehensive privacy law passes; COPPA full
compliance deadline (April 22, 2026) approaches; before Phase 1B; before any biometric
feature ADR; before onboarding a second household member (minor-adjacent data).

---

## 2. Cybersecurity and breach reporting

**Why this matters for Andreja:** delegated connector tokens, personal semantic graph
data, and eventually financial/health data all create breach-notification exposure once
Andreja (rather than only the user's own infrastructure) touches or stores that data
(`docs/plan.md`, Security engineering; Deployment, data ownership, hosting, and scale).

| Source | Status | Date checked | Applicability hypothesis |
|---|---|---|---|
| State breach-notification statutes (all 50 states + D.C.; no single federal statute) | Effective, but each state's trigger/timeline/content differs | 2026-08-23 | Applies the moment Andreja (the company) is a "business"/"data owner" for unencrypted personal information under any state's definition — i.e., once managed-cloud hosting exists. Self-hosted-only deployments push this obligation to the user/operator, not Andreja, subject to counsel confirmation. Use a maintained 50-state matrix (for example a current law-firm breach-notification chart) rather than memorizing dates, since deadlines range from "without unreasonable delay" to fixed 30/45/60-day windows and change yearly. |
| [FTC Health Breach Notification Rule, 16 CFR Part 318](https://www.ecfr.gov/current/title-16/chapter-I/subchapter-C/part-318) (amended, effective July 29, 2024) | Effective | 2026-08-23 | See section 8 (Consumer health / HBNR / HIPAA boundary) — this rule is breach-notification law *and* a privacy law simultaneously. |
| [SEC cybersecurity disclosure rule, 17 CFR 229.106](https://www.sec.gov/rules/2023/07/cybersecurity-risk-management-strategy-governance-and-incident-disclosure) | Effective (2023) | 2026-08-23 | Applies only to SEC reporting companies; not currently applicable to a private, non-public Andreja entity, but relevant if Andreja ever pursues public capital markets. |
| State information-security-program statutes (e.g., New York [SHIELD Act](https://www.nysenate.gov/legislation/laws/GBS/899-BB), NY DFS Part 500 for regulated financial entities) | Effective | 2026-08-23 | SHIELD Act's general "reasonable safeguards" duty applies broadly to businesses handling NY residents' private information; NY DFS Part 500 becomes relevant only if Andreja itself becomes a licensed financial entity (unlikely under the current no-money-movement design). |

**Counsel questions:** At what point does Andreja (versus the user's self-hosted
instance) become the notifying party for a breach? What incident-response and
notification timeline should the confidential security channel target across the
strictest applicable state?

**Obligations/controls/evidence:** encryption of delegated tokens (`docs/plan.md`,
"User-owned data planes"), threat model and incident-response runbook (Phase 0
deliverable), a defined internal severity/notification escalation path distinct from the
public feedback channel (`docs/plan.md`, Feedback and Support Framework — "security/privacy
reporting" is explicitly separated from public feedback).

**Deadlines:** none binding pre-launch; build the incident-response runbook and
notification-timeline decision before Phase 1B managed-cloud hosting goes live.

**Residual risk:** self-host-only exposure is primarily the user's own; managed-hosting
exposure is Andreja's and scales with tenant count.

**Re-review trigger:** first managed-cloud tenant; any new state breach law; any actual
security incident (triggers the confidential path immediately, not this document).

---

## 3. AI and automated decisions

**Why this matters for Andreja:** the assistant makes/recommends consequential decisions
across finance, health, employment benefits, insurance, and hiring-adjacent contexts
("Life Context and Opportunity Navigator," "Employer Benefits and Perks Manager,"
"Lifestyle Rewards and Financial Optimization"). The RAI reviewer role and the "no hidden
engagement/optimization score" principle already anticipate this (`docs/plan.md`, Life
Context and Opportunity Navigator guardrails; Assistant and AI architecture).

| Source | Status | Date checked | Applicability hypothesis |
|---|---|---|---|
| [Colorado AI Act, SB 24-205](https://leg.colorado.gov/bills/sb24-205) | Enacted; original effective date Feb. 1, 2026; subsequently delayed/amended more than once (reported delay to mid-2026 and reported repeal/replacement discussions in 2026 press coverage) | 2026-08-23 (bill text fetched directly from leg.colorado.gov) | Regulates "developers" and "deployers" of "high-risk artificial intelligence systems" used in "consequential decisions" (employment, financial/lending, insurance, healthcare, housing, legal services, education). Andreja's Employer Benefits, Lifestyle Rewards, Household/Insurance, and Life Event Planner skills could qualify as high-risk if they make or are a substantial factor in such decisions. **This entry is a live, rapidly changing target** — the version fetched directly from the Colorado General Assembly site on 2026-08-23 states the original Feb. 1, 2026 effective date; secondary sources report subsequent delay and possible repeal/replacement legislation that was not independently verified against primary text in this pass. Confirm current status directly at leg.colorado.gov before any product or public claim relies on this row. |
| EEOC Title VII technical guidance on AI/algorithmic selection tools (May 2023) — **removed from eeoc.gov and not independently re-hosted; treat as rescinded**, per the withdrawal of Biden-era federal AI-bias guidance following [Executive Order 14179, "Removing Barriers to American Leadership in Artificial Intelligence" (Jan. 31, 2025)](https://www.federalregister.gov/documents/2025/01/31/2025-02172/removing-barriers-to-american-leadership-in-artificial-intelligence) | Rescinded (underlying Title VII statute remains fully in force) | 2026-08-23 | Relevant only if Andreja itself becomes an employer using AI in hiring (internal HR use, not a customer-facing feature), or if any future skill assists third-party employers with candidate screening — not currently in the roadmap. Absence of the technical-assistance document does not reduce Title VII adverse-impact liability; counsel should confirm current EEOC posture before any AI-assisted hiring use. |
| [NYC Local Law 144 (Automated Employment Decision Tools)](https://www.nyc.gov/site/dca/about/automated-employment-decision-tools.page) | Effective (enforcement since July 5, 2023) | 2026-08-23 | Same trigger as above: only relevant if Andreja builds or resells an automated hiring-decision tool used by NYC employers. |
| Illinois [AI Video Interview Act (820 ILCS 42)](https://www.ilga.gov/Documents/legislation/ilcs/documents/082000420K20.htm) | Effective | 2026-08-23 | Only relevant if a future feature analyzes video interviews — not in the current roadmap. |
| State "automated decision-making technology" (ADMT) rules under comprehensive privacy laws (e.g., [California CCPA ADMT regulations](https://cppa.ca.gov/regulations/)) | Rulemaking in progress/finalizing | 2026-08-23 | Would require pre-use notice, opt-out, or access rights when Andreja uses automated processing to make decisions with legal or similarly significant effects on a consumer (e.g., insurance/benefit eligibility framing). Track CPPA rulemaking directly; status changes by quarter. |
| [NIST AI Risk Management Framework](https://www.nist.gov/itl/ai-risk-management-framework) | Guidance/voluntary framework | 2026-08-23 | Not binding law, but a credible reference architecture for the impact-assessment and risk-documentation obligations that multiple state AI/ADMT laws require in substance. |

**Counsel questions:** Which first-party skills, if any, meet a "consequential decision"
or "high-risk AI system" definition under Colorado's (or another state's) current text?
Does presenting explainable options with human confirmation (the "no unattended
consequential action" pattern already used throughout `docs/plan.md`) satisfy a human-review/
appeal requirement by design, or does it still require a separate disclosure/impact
assessment?

**Obligations/controls/evidence:** RAI review gate (already required per `docs/plan.md`,
Review gates: "AI safety: Rai review"); explainability requirement already built into Life
Context and Opportunity Navigator guardrails; maintain an AI-feature inventory tagging
each skill against "consequential decision" criteria before any regulated-skill phase
(11, 12) begins implementation.

**Deadlines:** none binding at Phase 0; build the AI-feature inventory before Phase 11
(Lifestyle rewards/employer benefits) and Phase 12 (Health) implementation begins.

**Residual risk:** this is the fastest-moving domain in the entire matrix; treat every
date above as provisional.

**Re-review trigger:** quarterly, at minimum, given active state legislative sessions;
immediately before Phase 11/12 implementation; on any Colorado AI Act status change.

---

## 4. Consumer protection

**Why this matters for Andreja:** "best deal" recommendations, loyalty/rewards
optimization, and marketplace/skill-ecosystem claims all touch unfair-or-deceptive-
practices law and marketplace-specific statutes (`docs/plan.md`, Household/Insurance
guardrails "lowest price is not automatically best value"; Lifestyle rewards guardrails).

| Source | Status | Date checked | Applicability hypothesis |
|---|---|---|---|
| [FTC Act Section 5](https://www.ftc.gov/legal-library/browse/statutes/federal-trade-commission-act) (unfair or deceptive acts or practices) | Effective | 2026-08-23 | Baseline federal consumer-protection standard for every user-facing claim, recommendation, and "best value" framing across all skills. |
| [FTC "Made in USA"/endorsement and testimonial guides](https://www.ftc.gov/business-guidance/resources/ftcs-endorsement-guides-what-people-are-asking) | Guidance | 2026-08-23 | Relevant to Personal Brand Studio's AI-generated content and authenticity disclosure requirements (`docs/plan.md`). |
| [INFORM Consumers Act, 15 U.S.C. §45f](https://uscode.house.gov/view.xhtml?req=granuleid:USC-prelim-title15-section45f) | Effective (since June 27, 2023) | 2026-08-23 | Applies only if Andreja itself operates as an "online marketplace" connecting third-party sellers to consumers at "high-volume seller" thresholds. Not applicable to the current personal-assistant/skill model, but relevant if the future third-party skill ecosystem (Phase 7) evolves into a seller marketplace rather than a developer/skill distribution model. |
| State unfair/deceptive-practices ("Little FTC Act") statutes | Effective (all states, varying private-right-of-action scope) | 2026-08-23 | State-level analog to FTC Act Section 5; some states (e.g., state UDAP statutes with fee-shifting/private right of action) create materially higher exposure than others. |
| State automatic-renewal/subscription-disclosure laws (e.g., California ARL, various "click-to-cancel" statutes) | Effective/expanding | 2026-08-23 | Relevant once Andreja has a paid subscription (managed hosting, premium features); requires clear disclosure and easy cancellation parity with signup. |

**Counsel questions:** Does the Skill ecosystem's future marketplace (Phase 7-8) create
INFORM Consumers Act or state marketplace-facilitator exposure? What renewal/cancellation
disclosure design satisfies the strictest applicable state before any paid tier ships?

**Obligations/controls/evidence:** claims-review gate already assigned to Fact Checker
(`docs/plan.md`, Review gates: "Claims and external facts: Fact Checker review");
design cancellation flows with subscription-parity before any paid tier.

**Deadlines:** none until a paid tier or marketplace exists.

**Residual risk:** moderate; consumer-protection claims risk is largely a function of
marketing/UX language discipline, which is already gated by existing review roles.

**Re-review trigger:** before first paid subscription tier; before Phase 7-8 marketplace
design; on any new state click-to-cancel law.

---

## 5. Accessibility

**Why this matters for Andreja:** the public website, help/support surfaces, and the
authenticated Blazor app are all user-facing digital services (`docs/plan.md`, Public
website, help, and support; API-first UI boundary).

| Source | Status | Date checked | Applicability hypothesis |
|---|---|---|---|
| [Americans with Disabilities Act, Title III](https://www.ada.gov/topics/intro-to-ada/) (public accommodations) | Effective (statute since 1990); no finalized DOJ Title III web-specific regulation yet | 2026-08-23 | Courts have applied Title III to commercial websites/apps for years even without a dedicated regulation; the safest engineering target is WCAG conformance regardless of the regulatory gap. |
| [DOJ Title II web/mobile accessibility rule](https://www.federalregister.gov/documents/2024/04/24/2024-07758/nondiscrimination-on-the-basis-of-disability-accessibility-of-web-information-and-services-of-state) | Effective (final rule, April 2024); compliance deadlines April 2026/2027 depending on population served | 2026-08-23 | Applies directly only to state/local government entities, not Andreja. Relevant as a signal: DOJ selected **WCAG 2.1 Level AA** as the operative technical standard for that rule — the credible regulatory floor to design toward even for private-sector surfaces where Title III's technical standard remains judicially derived rather than codified. `docs/plan.md` already commits to the higher **WCAG 2.2 AA** bar (Public website, help, and support section), which meets or exceeds this floor. |
| [Section 508](https://www.section508.gov/) | Effective | 2026-08-23 | Applies only if Andreja sells to U.S. federal agencies — not in the current roadmap, but worth tracking if a future government/education customer segment emerges. |
| [W3C Web Content Accessibility Guidelines (WCAG) 2.1/2.2](https://www.w3.org/WAI/standards-guidelines/wcag/) | Effective (technical standard, not law) | 2026-08-23 | `docs/plan.md`'s own conformance target — WCAG 2.2 AA — is the design/engineering standard to build and evidence against across public site, help center, and app; this is stricter than the WCAG 2.1 AA floor DOJ has codified for government entities. |
| State accessibility statutes (e.g., California Unruh Act interplay with ADA) | Effective | 2026-08-23 | California's Unruh Civil Rights Act imports ADA violations and adds statutory damages with a private right of action — materially raises exposure for any California user base. |

**Counsel questions:** Does committing publicly to `docs/plan.md`'s stated **WCAG 2.2 AA**
target (which exceeds the WCAG 2.1 AA floor DOJ selected as its Title II technical
standard) create a heightened standard of care or an enforceable public representation, and
if so, what audit/evidence cadence supports that claim safely?

**Obligations/controls/evidence:** accessibility checks already required for Personal
Brand Studio publication content (`docs/plan.md`); extend WCAG 2.2 AA conformance checks to
the public website/help site design work (Jadzia-led, per `docs/plan.md` Public website
section) and to the Blazor app.

**Deadlines:** none binding pre-launch; target WCAG 2.2 AA conformance evidence (meeting or
exceeding the WCAG 2.1 AA regulatory floor) before the public site goes live (Phase 1B).

**Residual risk:** moderate-to-high litigation exposure once public-facing, especially in
California; mitigate with conformance testing rather than relying on the regulatory gap.

**Re-review trigger:** before Phase 1B public site launch; on any DOJ Title III web
rulemaking; on WCAG version updates.

---

## 6. Communications and marketing

**Why this matters for Andreja:** email/SMS/notification channels (Gmail, Outlook,
Discord, WhatsApp), the assistant's outbound reminders, and eventual marketing/sponsorship
communications all touch anti-spam and telemarketing law (`docs/plan.md`, Channel
Development Framework; Initial channel connectors). Personal Brand Studio's publication
proposals across GitHub, LinkedIn, portfolio sites, Facebook, and Instagram, and Small
Business and Entrepreneur Manager's "marketing/support" capability (`docs/plan.md`,
Initial first-party skill catalog), are the first-party skills most likely to generate
outbound or public-facing marketing-adjacent content and are cross-referenced here for
that reason; see also section 4 for Personal Brand Studio's separate FTC
endorsement/authenticity obligations.

| Source | Status | Date checked | Applicability hypothesis |
|---|---|---|---|
| [CAN-SPAM Act](https://www.ftc.gov/business-guidance/resources/can-spam-act-compliance-guide-business) and [16 CFR Part 316](https://www.ecfr.gov/current/title-16/chapter-I/subchapter-C/part-316) | Effective | 2026-08-23 | Applies to any commercial email Andreja itself sends (transactional/notification email is generally exempt from the marketing-specific provisions but still needs accurate headers and a working opt-out). |
| [Telephone Consumer Protection Act (TCPA)](https://www.fcc.gov/general/telemarketing-and-robocalls) and [47 CFR 64.1200](https://www.ecfr.gov/current/title-47/chapter-I/subchapter-B/part-64/subpart-L) | Effective | 2026-08-23 | Applies if any future SMS/voice reminder or marketing channel is added; requires prior express consent, especially for automated/prerecorded messages, with steep per-message statutory damages. |
| [CTIA Messaging Principles and Best Practices](https://www.ctia.org/the-wireless-industry/industry-commitments/messaging-principles-and-best-practices) | Contract term / platform policy (carrier self-regulation, not statute) | 2026-08-23 | Governs 10DLC/short-code SMS delivery in practice; relevant the moment any SMS channel is qualified. |
| State telemarketing/mini-TCPA statutes (e.g., Florida, Oklahoma, Washington) | Effective, expanding | 2026-08-23 | Several states have enacted private-right-of-action telemarketing statutes broader than federal TCPA; re-check before any SMS/voice feature. |
| [CAN-SPAM](https://www.ftc.gov) / state spam statutes interplay with Discord/WhatsApp bot messaging | Contract term (platform policy) | 2026-08-23 | Discord and WhatsApp Business Platform impose their own anti-spam/opt-in rules independent of statute (`docs/plan.md` already flags "official bot/app with explicit installation and channel scope" and defers WhatsApp Business pending official documentation verification). |

**Counsel questions:** Which channels, if any, will carry marketing (as opposed to purely
transactional/reminder) content, and does that change the CAN-SPAM/TCPA analysis?

**Obligations/controls/evidence:** anti-spam/rate/abuse controls already required for
Hobbies and Social Groups Manager invitations (`docs/plan.md`); extend the same pattern to
any future marketing send.

**Deadlines:** none until a marketing-communications feature or SMS channel ships.

**Residual risk:** TCPA statutory damages are per-message and severe; keep all outbound
messaging transactional/reminder-only until consent architecture is reviewed.

**Re-review trigger:** before any SMS/voice channel; before any marketing (non-
transactional) send capability; on WhatsApp Business Platform connector qualification
(`docs/plan.md` Phase 0 open investigation).

---

## 7. Records and e-signatures

**Why this matters for Andreja:** document management across Household/Vehicle/Insurance,
Health and Wellbeing, Small Business, and Life Event Planner skills implies retaining and
potentially generating signable records.

| Source | Status | Date checked | Applicability hypothesis |
|---|---|---|---|
| [Electronic Signatures in Global and National Commerce Act (ESIGN), 15 U.S.C. §7001 et seq.](https://www.law.cornell.edu/uscode/text/15/7001) | Effective (2000) | 2026-08-23 | Relevant only if Andreja itself facilitates execution of a legally binding e-signature (not currently a first-party feature; skills route users to "official provider paths" for binding actions per multiple guardrails). |
| [Uniform Electronic Transactions Act (UETA)](https://www.uniformlaws.org/committees/community-home?CommunityKey=2c04b76c-2b7d-4399-977e-d5876ba7e034) (adopted by nearly all states) | Effective (state-by-state adoption) | 2026-08-23 | State-law counterpart to ESIGN; relevant on the same trigger. |
| Record-retention rules embedded in sector law (HIPAA record retention, state insurance record-retention rules, IRS recordkeeping) | Effective | 2026-08-23 | Each sensitive-data skill inherits the retention rules of its sector rather than a generic records statute; tracked per-domain in sections 8-13 below. |

**Counsel questions:** Does Andreja need its own e-signature capability, or is routing
every binding action to the official provider's own signing flow (the current guardrail
pattern) sufficient indefinitely?

**Obligations/controls/evidence:** preserve the current design principle — consequential/
binding actions route to official provider channels, not an Andreja-native signature flow
— until/unless a specific business need and legal review justify building one.

**Deadlines:** none; re-review only if a first-party e-signature feature is proposed.

**Residual risk:** low under current design; would rise materially if Andreja ever signs
or witnesses documents on a user's behalf.

**Re-review trigger:** any proposal for Andreja-native contract/e-signature execution.

---

## 8. Consumer health, the FTC Health Breach Notification Rule, and the HIPAA boundary

**Why this matters for Andreja:** Health and Wellbeing Manager is explicitly flagged
"Sensitive/later" and already carries its own guardrails, but the plan explicitly warns
"do not assume HIPAA status from feature names" (`docs/plan.md`, Health and Wellbeing
Manager guardrails).

| Source | Status | Date checked | Applicability hypothesis |
|---|---|---|---|
| [HIPAA Privacy/Security Rules, 45 CFR Parts 160, 164](https://www.hhs.gov/hipaa/for-professionals/index.html) | Effective | 2026-08-23 | Applies only to "covered entities" (health plans, clearinghouses, most providers) and their "business associates." A consumer-facing personal health-record assistant that is not partnered with a covered entity, and does not process/transmit PHI *on behalf of* a covered entity under a Business Associate Agreement, is generally **not** a HIPAA covered entity or business associate by default. This must be re-confirmed the moment Andreja integrates with SMART on FHIR/patient-portal data *on behalf of* a provider, or signs any BAA. |
| [HHS "health apps" guidance / interactive tool](https://www.hhs.gov/hipaa/for-professionals/special-topics/health-apps/index.html) | Guidance | 2026-08-23 | HHS explicitly built this tool to help consumer-health-app developers determine HIPAA/FTC HBNR applicability; use it as the first triage step, not a final answer. |
| [FTC Health Breach Notification Rule, 16 CFR Part 318, as amended](https://www.federalregister.gov/documents/2024/05/30/2024-10855/health-breach-notification-rule) (effective July 29, 2024) | Effective | 2026-08-23 | This is the rule most likely to apply directly to Andreja's Health and Wellbeing Manager: it covers vendors of "personal health records" and related entities *not* covered by HIPAA. The 2024 amendments expressly broadened "breach of security" to include unauthorized disclosure (not just hacking), which the FTC has enforced against GoodRx, Premom (Easy Healthcare), and BetterHelp for sharing health data with advertising/analytics partners without proper authorization. Andreja's "no sponsor/business analytics use" rule for health data (`docs/plan.md`) is a direct, appropriate control against this exact enforcement pattern. |
| State consumer-health-data laws (Washington My Health My Data Act, Nevada SB 370, Connecticut) | Effective (2023-2024) | 2026-08-23 | Create a private right of action (Washington) and geofencing/consent restrictions around consumer health data broader than HIPAA's scope. |
| State "special category"/sensitive-data provisions inside comprehensive privacy laws | Effective, varies by state | 2026-08-23 | Most 2023+ state privacy laws classify health data as "sensitive" and require opt-in consent, layered on top of the HIPAA-boundary and HBNR analysis above. |

**Counsel questions:** Does any planned data source (SMART on FHIR, Apple HealthKit,
Android Health Connect, pharmacy/lab/imaging APIs) create a HIPAA business-associate
relationship, and if not, does it trigger FTC HBNR "PHR identifiable health information"
status? What data-sharing (including any future sponsor/analytics access) must be
categorically excluded to avoid the GoodRx/BetterHelp enforcement pattern?

**Obligations/controls/evidence:** treat health data as highest-sensitivity with separate
scopes, minimal telemetry, no sponsor/business-analytics use, short retention where
appropriate, and export/delete support (`docs/plan.md`, already required); complete the
HHS health-app tool triage and document the HIPAA/HBNR conclusion before any provider/
wearable connector is built (Phase 4 manual / Phase 12 connected).

**Deadlines:** none currently binding (feature is Phase 4/12); complete triage before
committing to any specific health-data API integration.

**Residual risk:** high if the HIPAA-boundary conclusion is wrong or if any future
monetization touches health-derived data; the current guardrails (no sponsor/analytics
use) are the key mitigant and must not be weakened without this review.

**Re-review trigger:** before Phase 4 manual health-data intake design; before any
provider/insurer/wearable API integration; on any FTC HBNR enforcement action against a
comparable consumer-health-app business model; on any new state consumer-health-data law.

---

## 9. Finance, rewards, payments, and securities

**Why this matters for Andreja:** Finance Administration, Trading Research and Review,
Lifestyle Rewards and Financial Optimization, and Miles and Points Manager are all
explicitly gated as "no money movement," "no brokerage data or order execution," and
"read-only aggregation is the default" (`docs/plan.md`). The Small Business and
Entrepreneur Manager skill's "invoices/expenses, cash-flow admin" capability
(`docs/plan.md`, Initial first-party skill catalog) raises the same read-only/no-money-
movement questions in a business rather than personal-finance context and is cross-
referenced here for that reason.

| Source | Status | Date checked | Applicability hypothesis |
|---|---|---|---|
| [Gramm-Leach-Bliley Act (GLBA) Safeguards Rule, 16 CFR Part 314](https://www.ftc.gov/business-guidance/resources/ftc-safeguards-rule-what-your-business-needs-know) | Effective | 2026-08-23 | Applies to "financial institutions" as broadly defined by GLBA; a pure aggregation/reminder tool with no lending, money transmission, or brokerage function is unlikely to qualify as a financial institution itself, but any connector partnership with a bank/aggregator inherits their GLBA posture — confirm with counsel before any financial-data connector partnership agreement. |
| State money transmitter licensing (via [NMLS](https://www.nmlsconsumeraccess.org/)) and [18 U.S.C. §1960](https://www.law.cornell.edu/uscode/text/18/1960) (federal unlicensed money transmitting business crime) | Effective | 2026-08-23 | Triggered by holding, transmitting, or enabling transfer of customer funds/stored value. Andreja's explicit "no money movement" design for Finance Administration and Small Business's "cash-flow admin" (both administrative/reminder-only, not custodial), plus "read-only... consequential actions enabled only for official provider/partner APIs" for rewards (`docs/plan.md`), is the correct control to stay outside this category; any future feature that touches custody or transfer of value must be reviewed against this line before shipping. |
| [Truth in Lending Act (Regulation Z)](https://www.consumerfinance.gov/rules-policy/regulations/1026/) / [Truth in Savings Act (Regulation DD)](https://www.consumerfinance.gov/rules-policy/regulations/1030/) | Effective | 2026-08-23 | Relevant only if Andreja ever originates or services credit/deposit products directly — not in the current roadmap. |
| [Investment Advisers Act of 1940](https://www.sec.gov/investment/investment-adviser-registration) and state investment-adviser statutes | Effective | 2026-08-23 | "Trading Research and Review... no brokerage data or order execution" and "watchlists, research prompts, thesis/checklist/journal reminders" (`docs/plan.md`) is designed to avoid "investment adviser" status, which generally requires giving individualized advice about securities for compensation. Confirm the feature never crosses into personalized buy/sell recommendations tied to compensation. |
| State insurance-producer/referral-licensing statutes (see section 10) and card-network/rewards-program terms of service | Effective / contract term | 2026-08-23 | "Personalized insurance steering, ranking, referrals, lead generation or compensation require jurisdiction-specific licensing... review before display or monetization" (`docs/plan.md`) — this line item is the licensing trigger to track before any monetized recommendation. |
| Card-issuer and loyalty-program terms of service (Chase, American Express, airline/hotel programs) | Contract term | 2026-08-23 | Scraping or terms-violating automation is explicitly prohibited by the plan; any integration must use official partner APIs/OAuth. |

**Counsel questions:** At what point (if any) does aggregation plus optimization
"recommendations" cross into regulated financial advice, referral/lead-generation
compensation, or money-transmission activity? What partner-API terms (Plaid/Finicity-style
aggregators, card issuers, airline/hotel programs) impose their own compliance
obligations Andreja must inherit contractually?

**Obligations/controls/evidence:** maintain the existing "read-only default, official
API-only for actions, no credential scraping, no card-churning encouragement" guardrails
(`docs/plan.md`) as the primary control; require legal/security/privacy review before
enabling any consequential (write) action per rewards/finance skill, exactly as already
specified.

**Deadlines:** none until Phase 11 (Lifestyle rewards, miles, points, employer benefits)
implementation begins.

**Residual risk:** the "read-only first, official-API-only for actions" design keeps
current risk low; risk rises sharply if any future feature is tempted toward
compensation-based referrals or money movement without a fresh licensing review.

**Re-review trigger:** before Phase 11 implementation; before any monetized referral/
affiliate relationship; before any feature that could custody or move funds.

---

## 10. Insurance

**Why this matters for Andreja:** Household, Vehicle, Insurance and Projects Manager
explicitly frames insurance guidance as "comparison/organization, not licensed insurance...
advice" and flags licensing review before any monetized steering (`docs/plan.md`).

| Source | Status | Date checked | Applicability hypothesis |
|---|---|---|---|
| State insurance producer/broker licensing statutes (via [NAIC](https://naic.org/) model acts, adopted with variation in each state) | Effective (state-by-state) | 2026-08-23 | Licensing is generally triggered by "soliciting, negotiating, or selling" insurance, or receiving transaction-based compensation for referrals. Neutral organization/comparison tools that do not receive compensation tied to a sale are the safer non-licensed pattern the current guardrails already describe; confirm state-specific thresholds before any monetized or ranked recommendation. |
| State insurance data-security laws (modeled on NAIC [Insurance Data Security Model Law, #668](https://content.naic.org/model-laws) — full model text is subscription-gated at NAIC; state enactments are public law, e.g. [Ohio Rev. Code Ch. 3965](https://codes.ohio.gov/ohio-revised-code/chapter-3965)), adopted in ~20+ states | Effective (state-by-state adoption) | 2026-08-23 | Relevant only if Andreja itself becomes a licensed insurance entity — unlikely under current design, but a factor if Andreja ever partners as an agency of record. |
| State unfair claims settlement practices acts | Effective | 2026-08-23 | Not directly applicable to an organizational tool that never adjusts, denies, or negotiates a claim on the user's behalf (current design routes claims to "official provider paths"). |

**Counsel questions:** Does neutral, unranked, non-compensated organization/comparison of
a user's own policies ever require licensing in any state, and does that change once any
affiliate/referral compensation is introduced?

**Obligations/controls/evidence:** keep insurance features to comparison/organization
without compensation-linked steering until a specific state-by-state licensing review is
complete (`docs/plan.md` guardrail already states this).

**Deadlines:** none until a monetized insurance-referral feature is proposed.

**Residual risk:** low under current non-monetized design; would become a hard blocker for
any compensated referral model without state-by-state licensing.

**Re-review trigger:** before any insurance affiliate/referral monetization; before
Phase 5-6 household collaboration expands insurance data sharing.

---

## 11. Employment and benefits

**Why this matters for Andreja:** Employer Benefits and Perks Manager processes
employer-plan, retirement, and compensation-adjacent data explicitly marked "highly
sensitive" (`docs/plan.md`), and Andreja itself will eventually have contributors/staff
subject to employment law.

| Source | Status | Date checked | Applicability hypothesis |
|---|---|---|---|
| [Employee Retirement Income Security Act (ERISA), 29 U.S.C. Ch. 18](https://www.dol.gov/agencies/ebsa/laws-and-regulations/laws/erisa) | Effective | 2026-08-23 | "No... ERISA... advice" (`docs/plan.md`) is the correct posture: Employer Benefits and Perks Manager must stay in document/inventory/reminder/what-if mode and route authoritative decisions to plan administrators/fiduciaries, never acting as a plan fiduciary itself. |
| [Fair Labor Standards Act (FLSA)](https://www.dol.gov/agencies/whd/flsa) and state wage/hour law | Effective | 2026-08-23 | Applies to Andreja as an employer of its own staff/contributors once any employees are hired; not a product-feature concern. |
| State paid-leave, pay-transparency, and worker-classification statutes | Effective, varies by state | 2026-08-23 | Same employer-side applicability as FLSA; also relevant if Andreja engages contributors as independent contractors versus employees. |
| IRS rules on HSA/FSA, 401(k) match, and fringe benefits (26 U.S.C. §§105, 125, 401(k), 132) | Effective | 2026-08-23 | Employer Benefits guardrails already require "do not provide individualized tax... advice" — track these as the underlying tax framework the feature must describe accurately without giving personalized tax advice. |

**Counsel questions:** Does explaining an employer's own plan documents (match formulas,
deadlines) at a factual/organizational level risk being construed as fiduciary or tax
advice? What contributor-classification model (employee vs. contractor) applies as Andreja
scales beyond Cyrus?

**Obligations/controls/evidence:** keep Employer Benefits and Perks Manager in
document/inventory/reminder/what-if mode only; route enrollment/contribution/claim actions
to official provider workflows (`docs/plan.md`, already required).

**Deadlines:** none until Phase 11 implementation or first non-Cyrus contributor/employee.

**Residual risk:** low for the product feature under current guardrails; employer-side
obligations become live the moment Andreja has its first employee or contractor.

**Re-review trigger:** before Phase 11 implementation; before hiring any employee or
contractor; before any tax-year-specific guidance is hardcoded into the product.

---

## 12. Education, family, and elder care

**Why this matters for Andreja:** Life Event Planner explicitly covers "college for
children" and "elder care," and Family and Relationships/Household skills involve
collaboration on behalf of dependents who are not account holders (`docs/plan.md`).

| Source | Status | Date checked | Applicability hypothesis |
|---|---|---|---|
| [Family Educational Rights and Privacy Act (FERPA), 20 U.S.C. §1232g](https://studentprivacy.ed.gov/ferpa) | Effective | 2026-08-23 | Applies to schools/educational agencies receiving federal funding, not directly to Andreja as a consumer tool, unless Andreja partners directly with an educational institution to receive student records — not in the current roadmap. Relevant as background law shaping what a user can lawfully share about a dependent's education records. |
| State guardianship/power-of-attorney and elder-care fiduciary statutes | Effective, varies by state | 2026-08-23 | Relevant to any future "adult-only account policy and future guardian/care authority rules" (`docs/plan.md` explicitly flags this as still-undecided policy) — this is a product-policy decision that needs its own ADR before any caregiver/guardian access model ships. |
| State elder-financial-abuse reporting statutes | Effective, varies by state | 2026-08-23 | Relevant once Life Event Planner or Household skills surface financial anomalies involving an elder-care context; some states impose mandatory-reporting duties on certain professionals, not generally on a consumer software company, but worth confirming if any care-coordination feature is built. |

**Counsel questions:** What guardian/caregiver authority model is legally sound for a
dependent (minor or elder) who is referenced in, but not an account holder of, Andreja
data? Does any planned feature require FERPA-covered data directly from a school, and if
so under what agreement?

**Obligations/controls/evidence:** the plan's existing rule that "children, elders and
other affected people are not silently profiled or enrolled" and that guardian/care
authority rules are still pending (`docs/plan.md`, Life Event Planner guardrails) should
remain a hard gate until a dedicated ADR resolves the guardian-access model.

**Deadlines:** none until a guardian/caregiver-access feature is proposed.

**Residual risk:** moderate; the sensitivity of elder/child-adjacent data makes this a
priority ADR before any caregiver-facing feature ships.

**Re-review trigger:** before any guardian/caregiver access ADR; before Phase 4-6
Life Event Planner elder-care features scale beyond single-user planning.

---

## 13. Tax, accounting, and sponsorship

**Why this matters for Andreja:** the plan already anticipates sponsorship revenue and
explicit "no legal, accounting, tax... advice" boundaries across Small Business, Employer
Benefits, and Household guardrails (`docs/plan.md`).

| Source | Status | Date checked | Applicability hypothesis |
|---|---|---|---|
| [IRC §513(i) qualified sponsorship payments](https://www.law.cornell.edu/uscode/text/26/513) and [26 CFR §1.513-4](https://www.law.cornell.edu/cfr/text/26/1.513-4) | Effective | 2026-08-23 | Relevant **only if Andreja is or becomes a tax-exempt entity**; Andreja's plan describes a company/commercial structure, so UBIT/qualified-sponsorship-payment rules likely do not apply directly. They remain useful reference for drafting any sponsorship-acknowledgment language that avoids implying an endorsement/advertising relationship, and would become directly applicable if any nonprofit/foundation arm is ever created. |
| State sales/use tax and marketplace-facilitator nexus rules (post-[*South Dakota v. Wayfair*](https://www.oyez.org/cases/2017/17-494)) | Effective | 2026-08-23 | Relevant once Andreja sells taxable digital goods/services (managed-hosting subscriptions, marketplace skill sales) across state lines; nexus and taxability of SaaS/digital-service subscriptions vary significantly by state. |
| Sponsorship-disclosure rules under FTC endorsement guides (see section 4) | Guidance | 2026-08-23 | Any sponsor acknowledgment on the future public site/culture page must avoid implying endorsement without disclosure, consistent with `docs/plan.md`'s instruction that "accepting sponsorship remains separately gated by legal/tax/payment review." |
| Corporate/franchise tax and business-entity formation requirements | Effective, varies by state | 2026-08-23 | Applies once Andreja formalizes a business entity; track alongside the licensing/IP investigation (issue tracked separately per `docs/plan.md`, Licensing section). |

**Counsel questions:** What business-entity structure is planned (and does it change the
UBIT analysis)? Which states create sales/use tax nexus for a future paid subscription or
marketplace transaction fee?

**Obligations/controls/evidence:** keep the "transparent sponsorship policy" private-repo-
documented per `docs/plan.md` until legal/tax/payment review clears public posting; no
sponsorship acceptance without that review.

**Deadlines:** none until sponsorship revenue or a paid subscription is proposed.

**Residual risk:** low at Phase 0 (no revenue); rises quickly once any payment processing
begins.

**Re-review trigger:** before accepting any sponsorship; before enabling any paid
subscription tier; before formal business-entity formation.

---

## 14. Marketplace and platform (third-party skill ecosystem)

**Why this matters for Andreja:** Phase 7 (third-party skill ecosystem) and Phase 8
(connector/ecosystem expansion) create an intermediary/marketplace role between skill
developers and end users (`docs/plan.md`, Skill ecosystem; Phase 7-8).

| Source | Status | Date checked | Applicability hypothesis |
|---|---|---|---|
| [INFORM Consumers Act](https://www.ftc.gov/legal-library/browse/statutes/inform-consumers-act) (see section 4) | Effective | 2026-08-23 | Becomes directly relevant if third-party skill developers are treated as "high-volume third party sellers" transacting through an Andreja-operated marketplace with payment flow-through, rather than a developer/distribution model without payment intermediation. |
| State marketplace-facilitator sales-tax laws | Effective (all states with sales tax) | 2026-08-23 | Relevant once Andreja collects payment on behalf of third-party skill developers and remits a share — creates marketplace-facilitator tax-collection duties in most states. |
| Platform liability frameworks (Section 230 of the Communications Decency Act, and its ongoing legislative/judicial narrowing) | Effective, but actively contested/reinterpreted in courts | 2026-08-23 | Relevant to any user-generated or third-party-skill content Andreja hosts or distributes; track ongoing Supreme Court and circuit-court activity narrowing platform immunity, since this area has shifted materially in recent years. |
| Skill developer agreement / marketplace terms-of-service design | Contract term (Andreja-authored) | 2026-08-23 | Andreja's own future marketplace terms must allocate IP ownership, liability, revenue share, and content-moderation responsibility — an internal drafting task, not an external statute, but gated by the licensing/IP investigation (`docs/plan.md`, Licensing section) before external contributions are accepted. |

**Counsel questions:** Will the skill marketplace ever intermediate payment between
developer and user, and if so does that trigger INFORM Consumers Act and marketplace-
facilitator tax obligations? What content-moderation and IP-ownership terms belong in the
skill developer agreement?

**Obligations/controls/evidence:** no external skill contributions until the inbound
contribution policy and marketplace terms are decided (`docs/plan.md`, Licensing section,
already a hard gate).

**Deadlines:** none until Phase 7 design begins.

**Residual risk:** currently low/dormant; will require a dedicated ADR and likely a new
matrix row before Phase 7 implementation starts.

**Re-review trigger:** before Phase 7 third-party skill ecosystem design; before any
payment-flow-through marketplace feature.

---

## 15. App stores

**Why this matters for Andreja:** the mobile client roadmap (Phase 9) and any future app-
store distribution inherit Apple/Google policy obligations independent of statute
(`docs/plan.md`, Mobile client roadmap).

| Source | Status | Date checked | Applicability hypothesis |
|---|---|---|---|
| [Apple App Store Review Guidelines](https://developer.apple.com/app-store/review/guidelines/) (Guideline 5.1.2(i), AI/third-party-data-sharing disclosure, updated Nov. 2025 per developer reporting) | Platform policy | 2026-08-23 | Requires explicit, opt-in disclosure before any user data is sent to a third-party AI service (cloud LLM, generative model). Directly relevant to Andreja's Assistant Runtime, which routes user context to Copilot/BYOK providers (`docs/plan.md`, Assistant and AI architecture) — verify current guideline text directly at the link above before mobile submission, since Apple updates these guidelines frequently without a fixed release cadence. |
| [Google Play Developer Program Policies — AI-Generated Content policy](https://support.google.com/googleplay/android-developer/answer/13985936?hl=en) | Platform policy | 2026-08-23 | Requires disclosure of AI-generated content features and personal-data flows, plus moderation for harmful/deceptive AI output. |
| [Apple Sign in with Apple requirement (Guideline 4.8)](https://developer.apple.com/app-store/review/guidelines/#sign-in-with-apple) | Platform policy | 2026-08-23 | If Andreja offers any third-party social/OAuth login on iOS, Apple generally requires offering Sign in with Apple as an equivalent option — relevant to the identity/CIAM ADR (`docs/plan.md`, Identity, tenancy, and authorization foundations). |
| Apple/Google in-app purchase and subscription billing rules | Platform policy | 2026-08-23 | Relevant once a paid subscription is sold through a native mobile app; affects pricing, revenue share, and whether an external payment link is even permitted on that platform. |

**Counsel questions:** Does routing user messages to a Copilot/BYOK backend for assistant
functionality require the Apple 5.1.2(i) disclosure-and-consent flow, and what is the
compliant consent UX? Will mobile monetization use platform billing (with revenue share)
or an external entitlement model, and is the latter permitted on each platform?

**Obligations/controls/evidence:** build the AI-data-sharing disclosure/consent flow into
the mobile client design before any iOS submission (Hoshi-led, per `docs/plan.md` mobile
client roadmap, currently deferred).

**Deadlines:** none until Phase 9 native mobile client work begins.

**Residual risk:** low today (no mobile app exists); will require dedicated design work
before first submission given how frequently these guidelines change.

**Re-review trigger:** before Phase 9 kicks off; immediately before any App Store/Play
Store submission (guidelines must be re-read fresh, not from this cached summary).

---

## 16. Contracts and intellectual property

**Why this matters for Andreja:** this is already flagged in `docs/plan.md` as "an
immediate investigation, not a casual file change and not legal advice," and is the
subject of the separate, parallel license/IP investigation tracked under GitHub issue #6.
That investigation has landed as
[`docs/legal/license-evaluation.md`](license-evaluation.md) — a counsel-ready research
packet recommending a private, no-new-recipient posture pending counsel and Cyrus review.
This section records the dependency rather than re-deriving that document's conclusions.
Personal Brand Studio's cross-platform publication (GitHub, LinkedIn, portfolio sites,
Facebook, Instagram) also raises third-party platform-terms and persona/content-ownership
questions and is cross-referenced here for that reason; see section 4 and section 6 for
its consumer-protection and marketing-law touchpoints.

| Source | Status | Date checked | Applicability hypothesis |
|---|---|---|---|
| [Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0) | Effective (already present in repository history) | 2026-08-23 | Per `docs/plan.md`: grants broad, perpetual, irrevocable rights to any recipient of code actually distributed under it. Qualified counsel must determine current exposure and the effect of any future relicensing before any collaborator receives code — this document does not resolve that question and defers to `docs/legal/license-evaluation.md` and qualified counsel. |
| U.S. Copyright Act (17 U.S.C.) | Effective | 2026-08-23 | Governs ownership of code, documentation, and creative brand assets; interacts with employer invention-assignment obligations that must be separately confirmed (`docs/legal/license-evaluation.md`). |
| Trademark law (Lanham Act, 15 U.S.C. §1051 et seq.) and state trademark registries | Effective | 2026-08-23 | Blocks public use of "Andreja" branding, Star Trek internal codenames, any adapted Amazon/Microsoft-inspired culture language, and Personal Brand Studio persona/content publication until clearance is complete (`docs/plan.md`, Licensing section; Squad crew section already requires Sarek's review before any public culture page). |
| Contributor License Agreement (CLA) vs. Developer Certificate of Origin (DCO) frameworks | Not yet adopted — decision pending | 2026-08-23 | No external contributions are accepted until this policy is decided (`docs/plan.md`, Phase 0 exit gate, already a hard gate; `docs/legal/license-evaluation.md` recommends an inbound CLA with rights matched to the eventual outbound strategy). |
| Third-party dependency licenses (npm, NuGet, and any AI-model provider terms) | Effective (varies per dependency) | 2026-08-23 | Requires an inbound license-compatibility review before any dependency is added at scale; folded into the same license/IP investigation track. |
| [GitHub Copilot product terms / acceptable use](https://docs.github.com/en/site-policy/github-terms/github-terms-for-additional-products-and-features) | Contract term | 2026-08-23 | `docs/plan.md` flags this explicitly: entitlement, redistribution, and commercial-use conditions for a paid third-party product used via end-user OAuth, BYOK, or a headless backend must be confirmed before the Assistant Runtime ships broadly. |
| Third-party social/publishing platform terms of service (GitHub, LinkedIn, portfolio hosts, Facebook, Instagram) | Contract term | 2026-08-23 | Personal Brand Studio's publication proposals must operate through official APIs/OAuth under each platform's developer terms; content ownership, license-back, and API-scraping restrictions vary by platform and require review before any publishing capability ships (`docs/plan.md`, Personal Brand Studio guardrails). |

**Counsel questions:** all items in this section are already explicitly delegated to the
separate license/IP investigation track named in `docs/plan.md` ("Licensing, IP, and
project governance") and now recorded in `docs/legal/license-evaluation.md`. This document
does not duplicate that work; it records the dependency so both tracks stay synchronized.
Additionally: what platform-specific terms govern Personal Brand Studio's content
ownership and republication rights once publishing (not just drafting) ships?

**Obligations/controls/evidence:** keep the repository private until licensing/trademark
posture is resolved; no external contributions without an approved CLA/DCO policy; no
product-name/domain/namespace publication before clearance (`docs/plan.md`, already
required); Personal Brand Studio remains draft-only (no automatic publishing) until
provider access and platform-terms review are complete (`docs/plan.md`).

**Deadlines:** license/IP decision is a Phase 0 exit-gate blocker per `docs/plan.md`.

**Residual risk:** high and already flagged as the top legal priority in `docs/plan.md`.

**Re-review trigger:** on any external-contribution request; before any repository
visibility change; before any public-name/domain/trademark use; when
`docs/legal/license-evaluation.md` is updated with counsel's final recommendation and
Cyrus's decision; before Personal Brand Studio's publication capability moves past
draft-only.

---

## Confidential paths

Per the Legal and regulatory charter and Sarek's charter boundaries, the following
channels must exist and stay separate from public GitHub issues/PRs before they are
needed:

- **Confidential counsel channel.** A private, non-public communication path (not a public
  GitHub issue or PR comment) for questions directed to qualified jurisdiction-appropriate
  counsel. Sarek drafts the question; only Cyrus and counsel exchange privileged analysis.
- **Security/privacy incident channel.** Already structurally distinct from public feedback
  per `docs/plan.md` (Feedback and Support Framework: "Security/privacy reporting... never
  require public issue disclosure for vulnerabilities or personal-data incidents"). Extend
  the same separation to any legal/regulatory-inquiry contact.
- **Litigation hold procedure.** Not yet needed (no litigation, no launch), but the
  procedure — what gets preserved, who is notified, how normal document-deletion/retention
  automation is suspended — must be drafted before Phase 1B managed hosting introduces
  real user data at scale.
- **Regulatory-inquiry procedure.** A defined intake and escalation path if any regulator
  contacts Andreja (state AG, FTC, CPPA, etc.), routing immediately to Cyrus and counsel
  rather than being answered ad hoc by an agent or through a public channel.
- **Privilege boundary rule.** Nothing in `docs/plan.md`, GitHub issues, PR descriptions,
  or this file should contain actual privileged legal advice received from counsel;
  summarize only the resulting product/engineering decision, and store privileged
  material exclusively in the confidential counsel channel once established.

None of these channels currently exist as provisioned infrastructure (Phase 0 boundary:
no cloud accounts/subscriptions). Establishing them is a paper/procedure task — decide the
channel (e.g., a private counsel email alias, a restricted repository, or an out-of-band
document store) and document the procedure without provisioning new cloud services ahead
of the Phase 0 architecture spike.

## Launch jurisdictions

**Current state:** Andreja has not launched. There are no external users, no public site,
and no cloud provisioning (`.squad/directives.md`, Phase 0 boundary). No jurisdiction is
currently a "launch jurisdiction" in the sense of active regulatory exposure beyond
ordinary U.S. federal law applicable to any private repository and any future U.S.
business.

**Sequencing principle:** per the Legal and regulatory charter, research United States
federal, state, and relevant local requirements first; add international jurisdictions
only when users, providers, staff, or offerings create a real nexus. When Phase 1A
self-hosted walking-skeleton work produces its first real (even if Cyrus-only) deployment,
and again before Phase 1B managed-cloud hosting accepts any user outside Cyrus, this
section must be updated with:

- The specific state(s) where Andreja's business entity is formed and where its
  principal(s)/staff are located (creates baseline nexus regardless of user location).
- The specific state(s) where early adopters/testers are located (drives which state
  privacy/breach/consumer-protection laws attach first).
- A go/no-go gate confirming the applicable-law rows above (privacy, breach notification,
  accessibility, AI/ADMT where relevant) have a documented control before that
  jurisdiction's users are onboarded.

## Re-review triggers (consolidated)

**Calendar-based:**

- Quarterly horizon scan of AI/ADMT legislation (section 3) given legislative velocity.
- Annual full-document review, or sooner if any phase gate below is reached.
- On COPPA full-compliance deadline, April 22, 2026 (section 1).
- On DOJ Title II state/local accessibility compliance deadlines, April 2026/2027
  (section 5, background signal only — not directly binding on Andreja).

**Event-based:**

- Before Phase 1B managed-cloud hosting (sections 1, 2, 5, 9).
- Before any biometric feature ADR (section 1).
- Before Phase 7-8 third-party skill marketplace design (sections 4, 14).
- Before Phase 9 native mobile client submission (section 15).
- Before Phase 11 (rewards/points/employer benefits) and Phase 12 (connected health)
  implementation (sections 3, 8, 9, 10, 11).
- Before any guardian/caregiver-access ADR (section 12).
- Before accepting any sponsorship or enabling any paid subscription (section 13).
- Before any repository-visibility change, external contribution, or public product-name/
  domain/trademark use (section 16).
- On any actual security or data incident (activates the confidential path immediately).
- On any regulatory inquiry or contact from an agency or attorney general (activates the
  confidential path immediately, not a public GitHub issue).
- Whenever `docs/legal/license-evaluation.md` (already landed, tracked under issue #6) is
  updated with counsel's final recommendation and Cyrus's decision (section 16).

## Initial Phase 1A applicability assessment

Phase 1A is the "self-hosted assistant walking skeleton" (`docs/plan.md`, Phase 1A -
Self-hosted assistant walking skeleton). Per that section's deliverables, Phase 1A's scope
is deliberately narrow: a provider-neutral Assistant Runtime with a BYOK/local
OpenAI-compatible model path, Identity/Tenancy (passkeys, recovery), Data Ownership and
Privacy (export, backup/restore, delete), and exactly one MVP skill — Open Loops and Tasks
— invoked through `ISkillHost`. Calendar and Commitments, Finance Administration, and every
other catalog skill are later-phase work and are explicitly out of scope for Phase 1A
(`docs/plan.md`, Phase 1A deliverables; "Explicitly not day-one work" list). No regulated
skill (health, rewards, insurance, employment benefits) is implemented yet. Applying the
matrix dimensions to that constrained scope:

| Dimension | Phase 1A value | Resulting applicability |
|---|---|---|
| Capability | Assistant Runtime (BYOK/local OpenAI-compatible provider only), Identity and Tenancy, Data Ownership and Privacy, Open Loops and Tasks. No Calendar and Commitments, no Finance Administration, and no other catalog skill ships in Phase 1A. | No regulated-skill exposure yet (health/rewards/insurance/benefits are later phases); no financial-data or scheduling-data processing to evaluate under sections 9 or elsewhere. |
| Data class | Ordinary personal data limited to task content (capture, status, reminders) plus local BYOK/runtime configuration; no biometric, no health, no payment-card data, no calendar data, and no live third-party connector/provider tokens by design in Phase 1A | Baseline privacy/security hygiene applies; sensitive-category triggers (sections 1, 8) and connector-specific obligations are not yet live. |
| User/account type | Cyrus (adult, self-hosted operator) as Customer Zero; no external tenant | Minimal external-facing consumer-protection/privacy-law exposure; Andreja-as-employer/contributor employment law (section 11) not yet triggered. |
| Actor/role | User-directed assistant actions with human confirmation for consequential steps | Supports, rather than undermines, any future AI/ADMT "human review" argument (section 3), but that argument is not yet load-bearing since no consequential regulated decision exists. |
| Transaction/action | Create/list/complete/export/delete a task via Open Loops and Tasks only; no payments, no insurance/benefit enrollment, no biometric capture, no calendar sync, and no external connector actions | Sections 9, 10, 11 remain dormant; section 7 (e-signature) not implicated. |
| Deployment model | Self-hosted, user-owned data plane, no Andreja cloud provisioning, no external connector tokens in scope | Breach-notification duty (section 2) likely rests with the operator, not Andreja as a company, pending counsel confirmation; GLBA/state-financial-institution status (section 9) is not triggered because Finance Administration is out of scope for this phase. |
| Geography | Wherever Cyrus's self-hosted instance runs; no other jurisdiction has a nexus yet | U.S. federal baseline only; no state-specific launch gate is currently active (see Launch jurisdictions). |
| Launch stage | Phase 1A, pre-launch, pre-revenue | Confirms this document's domain-by-domain "why this doesn't apply yet" framing throughout sections 1-16. |

**Conclusion for Phase 1A:** no domain in this document currently imposes a binding,
active compliance obligation on Andreja as a company, beyond ordinary baseline federal law
(FTC Act Section 5 general fair-dealing norms, copyright/license posture already gated by
the separate license/IP track). The primary Phase 1A legal task is not compliance
execution — it is completing this horizon scan, the confidential-path setup, and the
license/IP decision so that Phase 1B (first external/managed-hosting user) does not launch
into an unreviewed regulatory gap.

**Evidence for "done when":** this document, reviewed by Cyrus, is the first artifact
required by GitHub issue #8's "Done when" criterion ("every regulated feature has a
defined legal gate"). Every first-party skill in `docs/plan.md`'s "Initial first-party
skill catalog" whose Band column reads **Regulated**, **Sensitive**, or
**Business/sensitive** — Health and Wellbeing Manager (Sensitive/later), Employer Benefits
and Perks Manager (Sensitive/later), Lifestyle Rewards and Financial Optimization
(Regulated/later), Miles and Points Manager (Regulated/later), Relationships and
Communities Map (Sensitive/iterative), and Small Business and Entrepreneur Manager
(Business/sensitive) — is named and cross-referenced by name in at least one domain
section above, each with an explicit gate (obligations/controls/evidence, deadline, and
re-review trigger) that must clear before that skill's implementation phase begins. This
document also names several skills whose Band label is not itself "Regulated" or
"Sensitive" but whose capabilities independently implicate a domain here: Household,
Vehicle, Insurance and Projects Manager (section 10, insurance-producer licensing; also
section 7, records); Trading Research and Review (section 9, investment-adviser status);
Life Event Planner (section 7, records retention; section 12, education/elder care); Family
and Relationships (section 1, children's-data inference; section 12, family/elder-care
coordination); and Personal Brand Studio (Early band, but carrying FTC
endorsement/authenticity, marketing, and platform-terms exposure — sections 4, 6, and 16).
Skills that are neither
Regulated/Sensitive/Business-sensitive by Band nor implicated by a specific domain today —
for example Personal Semantic Profile and Travel and Social Planning, both
"Research/early"/"Early" band with no sensitive-data or regulated-transaction capability
currently defined — are intentionally not given a dedicated per-skill gate here; re-run
this cross-reference check if their Band classification or capabilities change. Qualified
counsel approval of this framework, and Cyrus's sign-off, remain the two conditions this
document cannot itself satisfy.

## Governance and maintenance

- **Owner:** Sarek (General Counsel and Regulatory Research Lead), per
  `.squad/agents/sarek/charter.md`.
- **Approval:** Cyrus and qualified jurisdiction-appropriate counsel, per this document's
  boundaries section and `docs/plan.md`'s Legal and regulatory charter.
- **Relationship to other artifacts:** this document is the narrative horizon scan.
  Row-level applicability decisions, obligations, and evidence for a specific feature are
  tracked in that feature's GitHub issue and linked ADR, not duplicated here. Do not let
  this document silently diverge from `docs/plan.md` — if a roadmap phase, skill, or
  guardrail changes, update the corresponding section here in the same pull request or
  open a follow-up issue.
- **Versioning:** treat every dated source-check above as provisional. When re-reviewed,
  update the "Date checked" value and the status tag rather than assuming the prior
  conclusion still holds — regulatory status, especially for AI/ADMT and state privacy law,
  changes fast enough that a stale, confidently-worded entry is more dangerous than an
  explicit "reverify" note.
- **Confidentiality:** this document must never contain privileged counsel communications,
  specific incident details, or any content excluded by `.squad/directives.md` (no secrets,
  no personal task/profile content, no prompts or connector payloads).

## Primary source index

Federal statutes/regulations and agency guidance cited above, consolidated for quick
access (all accessed 2026-08-23 unless otherwise noted in the relevant section; reverify
before relying):

- FTC Act Section 5: <https://www.ftc.gov/legal-library/browse/statutes/federal-trade-commission-act>
- FTC COPPA Rule: <https://www.ftc.gov/legal-library/browse/rules/childrens-online-privacy-protection-rule-coppa>
- FTC COPPA 2025 final amendments (Federal Register): <https://www.federalregister.gov/documents/2025/04/22/2025-04426/childrens-online-privacy-protection-rule>
- FTC Health Breach Notification Rule, 2024 amendments (Federal Register): <https://www.federalregister.gov/documents/2024/05/30/2024-10855/health-breach-notification-rule>
- HHS health-app privacy guidance: <https://www.hhs.gov/hipaa/for-professionals/special-topics/health-apps/index.html>
- HIPAA Privacy/Security Rules overview: <https://www.hhs.gov/hipaa/for-professionals/index.html>
- FTC Safeguards Rule (GLBA): <https://www.ftc.gov/business-guidance/resources/ftc-safeguards-rule-what-your-business-needs-know>
- INFORM Consumers Act (15 U.S.C. §45f): <https://uscode.house.gov/view.xhtml?req=granuleid:USC-prelim-title15-section45f>
- CAN-SPAM Act compliance guide: <https://www.ftc.gov/business-guidance/resources/can-spam-act-compliance-guide-business>
- TCPA / FCC telemarketing and robocall rules: <https://www.fcc.gov/general/telemarketing-and-robocalls>
- CTIA Messaging Principles and Best Practices: <https://www.ctia.org/the-wireless-industry/industry-commitments/messaging-principles-and-best-practices>
- Colorado AI Act, SB 24-205 (bill text/status): <https://leg.colorado.gov/bills/sb24-205>
- EEOC AI/Title VII technical guidance (rescinded Jan. 2025; documented via the rescinding order): <https://www.federalregister.gov/documents/2025/01/31/2025-02172/removing-barriers-to-american-leadership-in-artificial-intelligence>
- NYC Local Law 144 (AEDT): <https://www.nyc.gov/site/dca/about/automated-employment-decision-tools.page>
- NIST AI Risk Management Framework: <https://www.nist.gov/itl/ai-risk-management-framework>
- DOJ Title II web/mobile accessibility rule (Federal Register): <https://www.federalregister.gov/documents/2024/04/24/2024-07758/nondiscrimination-on-the-basis-of-disability-accessibility-of-web-information-and-services-of-state>
- ADA overview: <https://www.ada.gov/topics/intro-to-ada/>
- Section 508: <https://www.section508.gov/>
- WCAG: <https://www.w3.org/WAI/standards-guidelines/wcag/>
- Illinois BIPA (740 ILCS 14/5): <https://www.ilga.gov/Documents/legislation/ilcs/documents/074000140K5.htm>; as amended, Public Act 103-0769: <https://www.ilga.gov/Legislation/PublicActs/PrinterFriendly/103-0769>
- Illinois AI Video Interview Act (820 ILCS 42/20): <https://www.ilga.gov/Documents/legislation/ilcs/documents/082000420K20.htm>
- Texas CUBI: <https://statutes.capitol.texas.gov/Docs/BC/htm/BC.503.htm>
- Washington biometric law (RCW 19.375): <https://app.leg.wa.gov/rcw/default.aspx?cite=19.375>
- California CCPA/CPRA (Attorney General): <https://oag.ca.gov/privacy/ccpa>
- Virginia Consumer Data Protection Act: <https://law.lis.virginia.gov/vacode/title59.1/chapter53/>
- IAPP US State Privacy Legislation Tracker (secondary aggregator, reverify against primary text): <https://iapp.org/resources/article/us-state-privacy-legislation-tracker/>
- ERISA overview (DOL): <https://www.dol.gov/agencies/ebsa/laws-and-regulations/laws/erisa>
- FLSA overview (DOL): <https://www.dol.gov/agencies/whd/flsa>
- FERPA overview: <https://studentprivacy.ed.gov/ferpa>
- IRC §513(i) qualified sponsorship payments: <https://www.law.cornell.edu/uscode/text/26/513>
- 26 CFR §1.513-4: <https://www.law.cornell.edu/cfr/text/26/1.513-4>
- Investment Advisers Act / SEC investment-adviser registration overview: <https://www.sec.gov/investment/investment-adviser-registration>
- NMLS Consumer Access (money transmitter licensing lookup): <https://www.nmlsconsumeraccess.org/>
- 18 U.S.C. §1960 (unlicensed money transmitting business): <https://www.law.cornell.edu/uscode/text/18/1960>
- NAIC (insurance model acts and data security model law #668 — full text is subscription-gated at NAIC): <https://naic.org/> and <https://content.naic.org/model-laws>; representative state enactment: <https://codes.ohio.gov/ohio-revised-code/chapter-3965>
- Apache License 2.0: <https://www.apache.org/licenses/LICENSE-2.0>
- GitHub Copilot additional product terms: <https://docs.github.com/en/site-policy/github-terms/github-terms-for-additional-products-and-features>
- Apple App Store Review Guidelines: <https://developer.apple.com/app-store/review/guidelines/>
- Google Play Developer Program Policies (AI-generated content): <https://support.google.com/googleplay/android-developer/answer/13985936?hl=en>

Existing plan/charter sources this document draws its "why this matters" framing from:
`docs/plan.md` (Legal and regulatory charter; Licensing, IP, and project governance;
Initial first-party skill catalog and guardrails; Feedback and Support Framework; Phase 0
and Phase 1A deliverables; Investigation ledger); `.squad/directives.md`; and
`.squad/agents/sarek/charter.md`.
