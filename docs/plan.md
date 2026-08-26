# Andreja platform plan

## Plan status and source of truth

This is the approval copy of the living plan. Ratification approves the architecture direction and bounded Phase 0 local/paper discovery/governance only. ADR 0000 records a $0, no-provisioning cloud-infrastructure cap for Phase 0; unresolved production choices and any provisioned deployment remain separately budgeted and gated by their ADRs and measured evidence. Ratification records the approver, date, and exact SHA-256 content hash of `docs/plan.md` as merged in `docs/adr/0000-plan-ratification.md`. Implementation begins with two separate pull requests:

1. `docs/plan.md`, ADR skeleton, and ratification record.
2. A file-by-file reviewed Squad scaffold that removes machine-specific state, corrects the project identity to Andreja, resolves `squad doctor`, extends casting policy for Star Trek, and casts the approved crew.

After the documentation PR, create repository roadmap issues and milestones and link execution work back to the approved plan and ADRs. Repository documentation becomes the durable architecture source of truth; repository Issues and milestones become the execution source of truth; Squad state remains a routing and learning aid. A GitHub Project board is deferred until Andreja moves into an organization. The earlier PR request remains pending until the plan is ratified; do not publish the current 165-file scaffold as-is.

ADR 0000 stores the initial hash plus an append-only amendment log containing issue, PR, new hash, approver, and decision. Documentation CI verifies that the recorded hash matches merged `docs/plan.md`; a mismatch fails the build. Editorial clarifications and phase evidence are logged amendments; changes to vision, data ownership, trust boundaries, Phase 0 envelope, public claims, legal posture, or non-negotiable architecture require explicit re-ratification.

### Execution and decision status amendment — 2026-08-25

This narrow status amendment is tracked by
[#73](https://github.com/Jamula/Andreja/issues/73). It reconciles merged
implementation and investigation evidence with the plan; it does not accept a
Proposed ADR, waive an exit gate, authorize spend or publication, or redesign an
architecture or trust boundary.

Issue [#74](https://github.com/Jamula/Andreja/issues/74) remains the
provider-scope decision of record. Cyrus's
[durable direction comment](https://github.com/Jamula/Andreja/issues/74#issuecomment-5427814163)
requests the outcome “integrate Copilot SDK into the toolchain so I can use
Copilot to interact with the tools.” That comment records the requested outcome;
it does not select a phase, accept this mapping, or authorize provider activation,
an account/model call, content disclosure, or spend. [Proposed ADR
0009](adr/0009-copilot-provider-phase-scope.md) now recommends the explicit
resolution: Phase 1A runtime remains deterministic fake plus optional
Andreja-native OpenAI-compatible BYOK; Phase 1A may perform only a non-shipping,
credential-free Copilot SDK compile/conformance toolchain spike; a limited real
Copilot provider begins no earlier than Phase 1B after every provider entry gate
passes. ADR 0009 and this mapping still require Cyrus's explicit approval. They
do not record his acceptance or authorize runtime activation, spend, an account,
or content disclosure.

#### Current artifact and implementation status

| Area | Current evidence | Decision or remaining boundary |
|---|---|---|
| Modular boundaries, PostgreSQL, and passkeys | The Phase 1A modular slices, PostgreSQL reference persistence/migrations, production passkey bootstrap/recovery, and their architecture/integration/browser tests are implemented on `main`. Evidence run [#44](phase-1a/evidence-44.md) records the exact tested boundary. | ADRs 0001–0005 remain **Proposed and unaccepted** pending [#66](https://github.com/Jamula/Andreja/issues/66). Merge and passing tests are implementation evidence, not Cyrus's acceptance or Phase 1A exit. |
| Skill, channel, and semantic contracts | Application-owned skill/channel manifests and hosts, grants/audit/peer envelopes, and minimal semantic-profile/provenance/export contracts are implemented. | This is contract-seam evidence only: no third-party execution, production connector, federation transport, graph database, universal ontology, or inactive persistence is approved. ADR 0004 and the other Phase 1A ADRs remain Proposed. |
| Portability and recovery | The application export/import clean-instance round trip passed as supplemental evidence in [evidence run #44](phase-1a/evidence-44.md#supplemental-application-portability-evidence-87). PostgreSQL logical dump/restore was only partial exit evidence. | Portable application data does not substitute for an encrypted database-plus-Data-Protection-key recovery set and restored sign-in. Managed-provider PITR and managed-cloud portability remain deferred. |
| Architecture diagram and operator README | PR [#100](https://github.com/Jamula/Andreja/pull/100) originated the Excalidraw/SVG/PNG architecture diagram and PR [#107](https://github.com/Jamula/Andreja/pull/107) merged its reviewed correction, PNG hash, and generation/consistency tests. The root README records current local build, test, self-host, and evidence commands. | These are current documentation and reproducibility evidence, not acceptance of the Proposed ADR packet or release authorization. The canonical privacy/threat artifacts are present but not ratified; Tuvok/Deanna Troi/Rai challenges and Cyrus residual-risk acceptance remain pending. |
| Website matrix and prototype | PRs [#105](https://github.com/Jamula/Andreja/pull/105) and [#111](https://github.com/Jamula/Andreja/pull/111) merged the Phase 0 matrix, claims inventory, local prototype, validator, and hardened boundary wording. PR #105 initially used ADR number 0006; the current canonical, hardened file is **Proposed ADR 0008**. | The packet remains a recommendation for human decision. It authorizes no deployment, preview, hosting vendor, domain, CDN, DNS, public claim, or public launch. Issue [#93](https://github.com/Jamula/Andreja/issues/93) remains the separately gated Phase 1B public-site proposal. |
| Test framework | PR [#103](https://github.com/Jamula/Andreja/pull/103) merged a measured xUnit/MSTest investigation. Cyrus later selected MSTest as the long-term direction. | ADR 0007 remains Proposed, and its earlier xUnit-retention recommendation is superseded by its 2026-08-25 amendment. Migration is deliberately deferred to [#112](https://github.com/Jamula/Andreja/issues/112); no test migration occurs in this amendment. |

#### Phase 0 policy and governance artifact classification

These hashes classify the files in this amendment's source tree and are enforced
by `.github/scripts/check_docs_consistency.py`; missing, added, malformed, or
stale rows fail documentation CI. A closed issue, merged PR, title containing
“ratify,” or downstream description of an artifact as authoritative is not an
explicit Cyrus decision.

| Artifact | Source and current SHA-256 | Current authority |
|---|---|---|
| [`docs/operating-model.md`](operating-model.md) | Issue [#14](https://github.com/Jamula/Andreja/issues/14), PR [#17](https://github.com/Jamula/Andreja/pull/17); `1e33fcb736a916a6b191c9b36a8bd984e1fdc1f42ad251121df4ac7227a0c7e5` | **Draft for ratification.** The issue and PR merged/closed, but no separate explicit Cyrus ratification record was found; it is advisory and the plan remains authoritative. |
| [`docs/cost-model.md`](cost-model.md) | Issue [#11](https://github.com/Jamula/Andreja/issues/11), PR [#20](https://github.com/Jamula/Andreja/pull/20); provider-allowance proposal [#74](https://github.com/Jamula/Andreja/issues/74), PR [#119](https://github.com/Jamula/Andreja/pull/119); `8f7c708d63d389481c1e88964dba883ae6dc3ad7a73215ac5317471e55e1f13c` | **Draft for ratification.** It recommends controls but authorizes no spend; issue closure and merge are not approval. |
| [`docs/frameworks/feedback-support.md`](frameworks/feedback-support.md) | Issue [#10](https://github.com/Jamula/Andreja/issues/10), PR [#16](https://github.com/Jamula/Andreja/pull/16); `758ed2eedf765d8a6a875b53af21b72fc0af4212fc06e06d54876c076ccb83ed` | **Review-ready draft.** The source PR merged, but issue #10 and explicit Cyrus approval remain open; no intake deployment or support commitment is authorized. |
| [`docs/frameworks/prioritization-launch.md`](frameworks/prioritization-launch.md) | Issue [#5](https://github.com/Jamula/Andreja/issues/5), PR [#19](https://github.com/Jamula/Andreja/pull/19); `7ba4d30a901e881cf167afe8964c9c2e6ba2584ac94151f2e4f8bc9b2e79104c` | **Review-ready draft.** Its operational mechanics are advisory until explicitly ratified; the plan owns phased scope and exits. |
| [`docs/charter.md`](charter.md) | Issue [#3](https://github.com/Jamula/Andreja/issues/3), PRs [#15](https://github.com/Jamula/Andreja/pull/15) and [#27](https://github.com/Jamula/Andreja/pull/27); `aeeb83acda01c8c50c71f276d1959cfc929eea1e32e15a15a754590ea50441f1` | **Proposed; not authoritative.** Issue #3 remains open and the file becomes effective only after Cyrus explicitly ratifies it. The charter section in this ratified plan remains the seed summary. |
| [`docs/privacy.md`](privacy.md) | Issue [#116](https://github.com/Jamula/Andreja/issues/116), PR [#117](https://github.com/Jamula/Andreja/pull/117); `03f50c948cf07ee32360944be9c1c3d9ba6274467a02d158960ca4abd6a424a1` | **Canonical descriptive baseline; not ratified.** Required challenge: Deanna Troi (privacy), Tuvok (security), Rai (AI safety); pending. Cyrus residual-risk acceptance remains pending. The classification/impact assessment remains open unless explicitly approved with cited evidence. |
| [`docs/threat-model.md`](threat-model.md) | Issue [#116](https://github.com/Jamula/Andreja/issues/116), PR [#117](https://github.com/Jamula/Andreja/pull/117); `1b9d73f4328cc89da5046607bdbd34613fde4d4d9e0f92ec3f5e62050d827d29` | **Canonical descriptive baseline; not ratified.** Required challenge: Tuvok (security), Deanna Troi (privacy), Rai (AI safety); pending. Cyrus residual-risk acceptance remains pending. The classification/impact assessment remains open unless explicitly approved with cited evidence. |
| [`docs/legal/license-evaluation.md`](legal/license-evaluation.md) | Issue [#6](https://github.com/Jamula/Andreja/issues/6), merged source PR [#12](https://github.com/Jamula/Andreja/pull/12); `4b8f325941e79a3d802bd03ef1472d55d69540efddced54853acda43f801d5e2` | **Counsel-ready research; not approved policy.** Issue #6 remains open; qualified counsel and Cyrus approval are still required. It authorizes no visibility, contribution, license, trademark, domain, or publication change. |
| [`docs/legal/regulatory-applicability.md`](legal/regulatory-applicability.md) | Issue [#8](https://github.com/Jamula/Andreja/issues/8), PRs [#21](https://github.com/Jamula/Andreja/pull/21) and [#28](https://github.com/Jamula/Andreja/pull/28); `60e5bec8ce1c991f72446c16374dd27962ea9698374a2105fbd7edc837394d23` | **Draft legal-research/governance artifact.** Issue #8 remains open; qualified counsel and Cyrus approval are required before it is authoritative or supports a legal/compliance claim. |

#### Phase 0 website containment

Phase 0 website artifacts may be opened only on loopback or in a private,
access-controlled review boundary. GitHub Pages, public previews, CDN delivery,
DNS publication, and any other public endpoint must be disabled; if a private
preview is later explicitly approved, it must enforce authorization and expiry.
`noindex`, robots directives, and `X-Robots-Tag` are defense in depth and are
never authorization.

Earlier on 2026-08-25, GitHub Pages publicly served
`https://jamula.github.io/Andreja/` from `main:/docs`. Unauthenticated requests
returned `200` for `/plan`, `/public-website/prototype/`,
`/phase-1a/evidence-44`, and `/legal/regulatory-applicability`. The whole
`docs/` tree was in the Pages source with no `_config.yml` or `.nojekyll`
exclusion. That was an unapproved Phase 0 nonconformance.

Containment evidence is recorded in closed issue
[#114](https://github.com/Jamula/Andreja/issues/114). The Pages `DELETE`
returned `204` at `2026-08-25T15:55:36Z`; the Pages API then returned `404`.
The four routes above reached stable `404` at `2026-08-25T16:06:55Z` after
bounded CDN expiry and were independently reconfirmed `404`. Issue #114 closed
with no repository changes. The Pages-specific merge hold on PR
[#113](https://github.com/Jamula/Andreja/pull/113) is therefore lifted; normal
draft, review, and merge gates still apply.

This contained current state does not recall third-party/client caches, search
engine copies, previously downloaded bytes, or provider-retained request logs.
Those residual copies and logs are outside repository control and require their
own authorized provider/privacy process if further action is required.

#### Current Phase 1A evidence status

[Evidence run #44](phase-1a/evidence-44.md) is useful local evidence, but Phase
1A exit is not claimed. The following gates remain blocking without waiver:

1. a separately trusted operator reruns OCI evidence with an approved external
   signing key and trust anchor;
2. encrypted PostgreSQL data and recoverable Data Protection keys restore into a
   clean instance and complete restored passkey sign-in;
3. a genuine second, separately approved and signed revision completes both
   update and rollback against preserved state; and
4. Cyrus approves numeric SLO, RPO, RTO, retention, and external model-spend
   envelopes plus the final residual-risk decision.

Passing deterministic, PostgreSQL, browser, portability, offline, and telemetry
rows do not weaken these exits.

#### Required artifacts and next safe order

The canonical [`docs/privacy.md`](privacy.md) and
[`docs/threat-model.md`](threat-model.md) now provide descriptive cross-phase
baselines under [#116](https://github.com/Jamula/Andreja/issues/116), but are not
ratified. Tuvok's security challenge, Deanna Troi's privacy challenge, Rai's
AI-safety challenge, Cyrus's explicit residual-risk acceptance, and the
classification/impact assessment remain open. They do not accept a Proposed ADR,
authorize production or a future
integration, or close the retention, residency, model-provider, and evidence
decisions they identify. The narrower files under `docs/phase-1a/` remain evidence
inputs and do not replace them.

Pages containment is the completed prerequisite under
[#114](https://github.com/Jamula/Andreja/issues/114). PR
[#106](https://github.com/Jamula/Andreja/pull/106) completed the status
reconciliation at merge commit `7a1fc20`.

The exact premature-auto-merge race in
[#104](https://github.com/Jamula/Andreja/issues/104) is currently contained because
repository auto-merge is disabled (`allow_auto_merge=false`). This is containment,
not the full required outcome: #104 remains open, and merged PR
[#115](https://github.com/Jamula/Andreja/pull/115) (`c93c6be`) contributed
human-blocked design evidence only. No external GitHub App/worker or always-present
review-completion gate exists or is authorized.

The next safe engineering lane is fail-closed selective CI under
[#102](https://github.com/Jamula/Andreja/issues/102), only while repository
auto-merge remains disabled. It must keep stable, always-present, fail-closed
aggregate checks on pull-request, merge-group, and default-branch paths.
Implementation of #102 must not depend on, substitute for, or weaken #115 or #104;
unknown or unclassified changes must fail closed into the full relevant suite.

Public launch, a real Copilot provider, managed cloud, federation, and production
connectors remain deferred to their later phases and explicit gates.

### Provider-scope proposal — 2026-08-26

Issue [#74](https://github.com/Jamula/Andreja/issues/74) is addressed at proposal
level by [ADR 0009](adr/0009-copilot-provider-phase-scope.md). Cyrus's
[direction comment](https://github.com/Jamula/Andreja/issues/74#issuecomment-5427814163)
is the durable source for the requested SDK-toolchain outcome, not approval of
the recommended phase placement or any activation:

- Phase 1A runtime is deterministic fake plus optional Andreja-native
  OpenAI-compatible BYOK. It does not include a real Copilot provider.
- Before runtime, the requested outcome is advanced only by a pinned, isolated,
  non-shipping, credential-free compile/conformance adapter and developer checks;
  usable Copilot interaction remains unsatisfied until a separately approved
  provider activation. The spike is absent from the app dependency graph,
  container, startup, service registration, UI/configuration, auth, egress, model
  usage, account provisioning, spend, and product claims. CI scrubs ambient
  Copilot/GitHub credentials and stores, removes Copilot/GitHub executables,
  denies egress, prohibits runtime-start/session APIs, and proves the shipping
  graph/artifact remains Copilot-free.
- The earliest real Copilot work is a limited Phase 1B canary after explicit ADR
  acceptance and current evidence for SDK/support, entitlement/billing,
  tenant/user/session/tool/filesystem isolation, prompt/tool/data exposure,
  retention/residency/training/abuse handling, authentication custody, hard
  budgets/reconciliation, fallback/offline behavior, consent/disclosure,
  audit/provenance, tests/canaries, legal/privacy/security approval,
  operability/support, and rehearsed rollback.
- A failed or unanswered gate defers Copilot beyond Phase 1B. It does not weaken
  offline startup, deterministic conformance, BYOK, provider neutrality,
  self-hosting, portability, or user-owned-data boundaries.

ADR 0009 is **Proposed**. Required review is Cyrus after named architecture,
privacy, security/abuse, FinOps/operations, and qualified legal verdicts.
Documentation merge, issue closure, SDK general availability/package presence,
a compile spike, or an existing subscription does not record Cyrus acceptance or
authorize a runtime, account, network/model call, content disclosure, or spend.

## Vision

Andreja is an assistant-and-skill platform. Personal task and open-loop management is the first substantial first-party skill set, not the platform boundary. Andreja begins as Cyrus's private, cross-device assistant across finances, bills, trips, social commitments, family support, health, home maintenance, reading and podcast queues, and trading research. It is intentionally built on foundations that can become:

- A mature multi-tenant SaaS product anyone can use.
- An ecosystem of independently hosted Andreja instances that can collaborate through an open, versioned federation protocol.
- A consent-driven sharing platform where friends, partners, and family expose only the information needed for a purpose.
- An extensible skill platform where first- and third-party capabilities are versioned, permissioned, isolated, and discoverable.
- A user-owned data platform with two deployment choices:
  - Fully independent self-hosting through a containerized bundle with PostgreSQL.
  - A secure managed-cloud data plane offered as a freemium/paid service, with the cloud and relational database selected through measured architecture decisions.
- A connector platform for user-authorized access to OneDrive, Google Drive, GitHub, Box, and later partner systems.
- A Personal Brand Studio that helps users develop evidence-based professional and social personas across GitHub, LinkedIn, portfolio sites, Facebook, Instagram, and later channels.
- A user-owned personal semantic graph that connects identities, roles, relationships, commitments, interests, artifacts, preferences, claims, sources, consent, and time without making private information public by default.
- A Life Context and Opportunity Navigator that helps users understand people, money, things, preferences, time, self-reported stressors, career, commitments and events; surface opportunities; make meaningful consensual connections; and optimize toward user-defined wellbeing rather than platform engagement.
- A creator/developer marketplace where external builders can publish trusted skills/channels, serve customers and build sustainable businesses under transparent platform rules.

The first releases will not implement that entire vision. They will establish only the structural seams that are cheap now and rewrite-level expensive later.

## Company, platform, brand and marketplace model

- **The company:** governs mission, ethics, capital/burn, legal/IP, product portfolio, partnerships, support and sustainable business operations.
- **The platform:** provides identity/data ownership, assistant runtime, semantic context, skills/channels, proposals/consent, federation, developer contracts, security/privacy, observability and portability.
- **The product/client experiences:** web, self-host/managed deployment, public/help site and future mobile clients turn platform capabilities into user outcomes.
- **The brand/community:** communicates evidence-based value, trust, user stories, developer opportunity, support, sponsorship and company principles.
- **The marketplace/ecosystem:** enables approved external publishers to distribute skills/channels and build businesses without ambient user access or lock-in.

### Customer Zero doctrine

- Cyrus/Andreja company operations are Customer Zero for task/open-loop, Personal Brand, business management, support, FinOps, channels, semantic context and other early skills.
- Dogfood proves user outcomes, operational pain, privacy/security, support, cost and API ergonomics. It does not automatically prove broad market demand.
- Separate Customer-Zero-specific policy/configuration from reusable product contracts. Before generalizing, test with the trusted Phase 1B cohort across different workflows and technical comfort.
- Record every Customer Zero pain point as feedback/evidence, every workaround as debt and every generalized capability as a reviewed issue/ADR.
- Never use customer-zero privileges, internal data or bypasses that marketplace builders/customers cannot safely obtain through supported contracts.

## Andreja company charter

### Mission

Empower people to understand and improve their lives through a trustworthy, user-owned assistant and capability ecosystem that turns context into meaningful, consented action.

### Commitments

- **Human agency:** people set goals, control data, inspect reasoning, approve consequential actions and can leave with their information.
- **Respect and inclusion:** design for varied abilities, cultures, families, identities, resources and technical comfort; treat every person affected by the system with dignity.
- **Integrity and accountability:** tell the truth about capabilities, uncertainty, evidence, cost, sponsorship, incidents and limits; own outcomes and correct harm.
- **Growth mindset:** assume people and teams can learn; cultivate curiosity, experimentation, mentorship, collaboration, shared accountability and learning from mistakes.
- **Trustworthy human-AI collaboration:** prioritize transparency, fairness, privacy, security, safety, accessibility and the effects of AI on people and society.
- **Ethical optimization:** optimize for user-defined wellbeing and meaningful outcomes, never addiction, manipulation, covert profiling, discriminatory exclusion or engagement at any cost.
- **Privacy and data dignity:** collect the minimum, use data only for stated purposes, protect sensitive context, avoid shadow profiles and make deletion/export/revocation real.
- **Sustainable stewardship:** design for financially sustainable operations, efficient compute/model usage, maintainable systems, durable support, responsible vendor choices and reduced environmental waste.
- **Broad responsibility:** consider users, family/collaborators, employees/contributors, providers, communities and future society as Andreja scales.
- **Independent judgment:** challenge decisions with evidence, protect dissent and safety reporting, then execute approved decisions cohesively without concealing residual risk.

### How the charter is enforced

- Every major ADR, launch gate, sponsorship/partner decision and public claim includes an ethics/sustainability impact section.
- Tuvok, Deanna Troi, Quark, Sarek, Data and Rai challenge security, privacy, cost/sustainability, legal/evidence and responsible-AI effects; Cyrus retains final human accountability.
- Publish measurable indicators for accessibility, privacy incidents, security, user control, support quality, AI/cloud efficiency, cost/runway and vendor concentration before GA.
- Provide confidential reporting, non-retaliation, incident/remediation and partner/sponsor exit mechanisms.
- The charter outranks growth, launch dates, sponsor requests, cost savings and agent recommendations.

## Roadmap catalogs at a glance

### Platform capabilities

1. Assistant Runtime and Review.
2. Data Ownership, Privacy, Export, Delete, Grants, and Audit.
3. Identity, Tenancy, Authorization, Consent, and Proposals.
4. Skill Host, Channel Host, Semantic Profile, Feedback, and Operability.

Platform capabilities are privileged product functions governed by architecture/security/privacy artifacts. They are not ordinary skills and cannot grant themselves tenant-wide access through a skill manifest.

### Initial first-party skills

1. Open Loops and Tasks.
2. Calendar and Commitments.
3. Personal Semantic Profile.
4. Personal Brand Studio.
5. Finance Administration.
6. Family and Relationships.
7. Health and Wellbeing Manager.
8. Household, Vehicle, Insurance and Projects Manager.
9. Travel and Social Planning.
10. Interests, Reading, and Podcasts.
11. Trading Research and Review.
12. Lifestyle Rewards and Financial Optimization.
13. Miles and Points Manager.
14. Employer Benefits and Perks Manager.
15. Hobbies and Social Groups Manager.
16. Life Context and Opportunity Navigator.
17. Life Event Planner.
18. Relationships and Communities Map.
19. Small Business and Entrepreneur Manager.

### Initial channel connectors

1. Local/linked identity: passkeys, Microsoft, Google, GitHub; later Apple, LinkedIn, Facebook, and enterprise OIDC.
2. Assistant providers: under Proposed ADR 0009, deterministic fake and
   OpenAI-compatible BYOK are the Phase 1A runtime providers. A credential-free,
   non-shipping Copilot SDK compile/conformance toolchain spike may occur in 1A;
   a limited real GitHub Copilot provider begins no earlier than Phase 1B after
   ADR 0009 acceptance and all entry gates pass; later Azure AI and local
   providers.
3. **Email first:** Gmail, Outlook.com/Hotmail, and Microsoft 365 mail; then their calendar surfaces.
4. In-app messaging: user-to-Andreja, support/status, and later consented peer-assistant messaging.
5. Discord: official bot/app installation only, with explicit server/channel scope and no self-bot/user-token automation.
6. WhatsApp: user-controlled share/export and supported official WhatsApp Business Platform scenarios; no unofficial personal-history automation by default.
7. File/content: OneDrive/SharePoint, Google Drive, GitHub, and Box; later Dropbox/partners.
8. Professional brand: GitHub profile/README/portfolio and LinkedIn.
9. Social brand: supported Facebook and Instagram professional surfaces; later approved channels.
10. Project/support: GitHub Issues, pull requests, native stacks, and app-owned feedback publishing.
11. Notification: email first, then push/mobile and additional user-approved channels.
12. Sponsorship/payment: approved project sponsorship provider only; no user financial-data connector.
13. Regulated finance/rewards: candidate approved aggregators and provider/partner APIs for Quicken products, financial institutions/cards such as Chase and American Express, AwardWallet, Rakuten, CardPointers, Bilt, and successors; actions remain separately gated.
14. Health data: user-controlled files first; later SMART on FHIR/FHIR, patient portals, Apple Health/HealthKit, Android Health Connect, pharmacy and approved wearable/provider APIs.
15. Employer benefits: user-controlled plan documents first; later approved HR/benefits, retirement, charitable-match, wellness, commuter, education and perks-provider channels.
16. Hobbies/social groups: in-app groups and calendars first; Discord official app, Xbox-approved surfaces and later community/game/provider channels where official access exists.
17. Household/assets: user documents/email/calendar first; later approved insurance, vehicle, utility, warranty, home-service, property and telematics channels.
18. Photo context: user-selected OneDrive photo files and Google Photos Picker items using official APIs and only provider-returned basic metadata; no automatic biometric identity, provider-derived labels promise, or full-library access.

### Required lifecycle frameworks

- The **Channel Development Framework** governs every connector from provider qualification through retirement.
- The **Skill Development Framework** governs every first- and third-party skill from charter through deprecation.
- The **Feedback and Support Framework** governs website, in-app, contributor, and support feedback through closure.
- The **Prioritization and Launch Framework** governs issue scoring, stage scope, evidence, exit, and stop decisions.

This at-a-glance section and the in-plan skill/channel tables are seed summaries. After Phase 0 creates them, `docs/roadmap/first-party-skills.md` and `docs/roadmap/channel-connectors.md` are authoritative and documentation CI checks every summary against them.

## MVP user stories

### MVP Story 1 - Autonomous Email Triage, Task Extraction, and Adaptive Management

**As a user, I want an intelligent, context-aware email assistant that automatically triages my inbox, extracts actionable tasks, and adapts to my preferences over time with full transparency and control, so that I can reduce email overload and focus on high-value work.**

#### Account and context onboarding

- Connect multiple Gmail, Outlook.com/Hotmail and Microsoft 365 mail accounts through official APIs and least-privilege delegated grants. Andreja exposes its capabilities through skills/MCP; unreviewed third-party MCP servers do not receive email tokens/content.
- The user assigns an account persona such as work/business, personal, household, project, or spam/burner and can define distinct priority, retention, task, calendar and autonomy policies per account.
- Onboarding starts in dry-run mode with an explicit historical scope (time window, folders/labels and content classes), estimated volume/cost, model/content policy and cancel/purge controls.
- Historical analysis proposes a contact/relationship model from interaction metadata and consented content. Relationship/importance clusters and next-step recommendations are reviewable hypotheses, not facts or cross-tenant profiles.

#### Classification and lifecycle

- Classify incoming mail into user-visible categories such as critical alert, action/follow-up, calendar/commitment, reference/learning, personal, receipt/record, promotion/newsletter, spam/suspicious and sensitive short-lived.
- Prioritize critical alerts and suppress promotional noise according to per-account policy, with reason, confidence, source signals and correction controls.
- OTP/password-reset and other short-lived messages may be scheduled for deletion only when the account policy enables it. Use quarantine/grace periods, cancellation/undo, legal/hold exceptions and permanent-deletion confirmation.
- Never autonomously send/reply/forward, change financial/health/legal/employment state, or make irreversible provider changes without the action-specific step-up confirmation.

#### Calendar and tasks

- Parse calendar invites and email-derived commitments. Automatically sync/accept only within the user's account/calendar policy and confidence threshold; ambiguous, conflicting, external or consequential changes require step-up review.
- Extract actionable items, owners, dates, dependencies, follow-ups and reference/learning notes into the built-in Open Loops and Tasks skill with source/provenance, deduplication and lifecycle state.
- Full-autonomy task creation is permitted within policy; every task is visible, editable, completable, traceable to source and undoable.

#### Control plane and adaptation

- Provide a dedicated control plane showing connected accounts, provider/token health, dry-run/live state, current policies/instructions/skills/models, processing queue, every classification/action/rule change, reason/confidence, grace deadline, undo and pause/kill controls.
- Users can select assistant/LLM provider, model, budget, retention/content rules, custom instructions and approved custom skills per account.
- Corrections create versioned learning evidence. Adaptive rules/contact weights may update automatically only within configured bounds, remain inspectable/explainable, support rollback and never silently expand permissions, retention, deletion scope or provider/model data exposure.

#### Success and safety evidence

- Track per-account volume, precision/acceptance/correction rate by action/class, false-positive and false-negative sampling, task/calendar usefulness, undo/reopen rate, deletion escapes, latency, provider/model usage/cost and user-validated time saved.
- Estimated time saved is an explicit transparent estimate, never presented as measured fact without a user-validated methodology.
- Strict tenant/account isolation, encrypted tokens, source-content minimization, no email content in operational telemetry, provider rate/renewal/reconciliation handling, retention/delete/export and full audit are required.
- Expose bounded typed email/task/calendar capabilities through the Skill/Channel/MCP architecture so external tools can integrate without raw mailbox access.

#### MVP acceptance scenarios

1. Connect one Gmail and one Outlook.com or Microsoft 365 account with different account policies.
2. Complete dry-run historical calibration, review relationship hypotheses and activate live mode.
3. Autonomously classify mail, create/complete a sourced task, and sync a permitted invite.
4. Quarantine an OTP for scheduled deletion, inspect reason/deadline, undo it and prove permanent deletion still requires confirmation.
5. Correct a classification/task, observe a versioned adaptive rule change, explain it and roll it back.
6. Pause the assistant, inspect/export the action ledger and revoke one account without affecting the other tenant/account.

### MVP Story 2 - Collaborative Group Travel Planning

**As a group traveler, I want my Andreja tenant to collaborate with the separate Andreja tenants of invited adults on a shared trip, so that we can coordinate preferences, itinerary, tasks, budget assumptions and decisions without exposing unrelated personal data.**

- The host tenant owns the trip workspace. Each participant remains in a separate tenant and receives a bilateral, purpose-scoped, time-bounded grant only after accepting the invitation.
- Share reference-based trip projections: participant display identity, availability windows, explicit preferences/constraints, itinerary options, tasks, votes/proposals, budget ranges, selected documents/reservations, reminders and decisions.
- Never expose a participant's unrelated calendar, finances, health, family, travel history, loyalty balances, location or private Life Context graph. Participants choose which constraints/preferences are shared.
- Changes from other tenants arrive through proposals with idempotency, optimistic concurrency, source, actor, timestamp and audit. The host remains authoritative for the workspace; each participant remains authoritative for their own private data.
- Support invite/accept/decline, role/capability scopes, comments/proposals/voting, revocation/leave, participant removal, expiry, conflict handling, access history and a consent dashboard.
- Booking, payment, loyalty transfer/redemption, calendar writes and external messages remain provider/action-gated and require the affected participant's confirmation.
- MVP validates managed-platform cross-tenant grants only. General resource sharing and independent-instance federation remain Phase 6.

#### MVP acceptance scenarios

1. Three adults in three managed Andreja tenants join one trip through bilateral invitations.
2. Each shares only chosen availability, preferences and budget range; tenant-isolation tests prove unrelated data is inaccessible and not inferable.
3. Participants propose itinerary/task changes, resolve a concurrent edit and record a decision/audit trail.
4. One participant revokes availability access while remaining in the trip; shared projections update and derived context is purged.
5. The group exports a minimum-disclosure itinerary and closes the workspace, expiring grants without deleting private tenant records.

### MVP extensibility proof

The MVP is successful as a platform proof only if:

- Gmail and Microsoft mail implement the same channel contract while preserving provider-specific topology.
- Email Triage composes Assistant Runtime, Channel Host, Open Loops/Tasks, Calendar, Semantic Profile, policy, control plane and feedback without provider types leaking into domain/use cases.
- Group Travel composes identity, tenant isolation, grants/consent, access-scoped projections, proposals, tasks, calendar, in-app messaging, audit and help through published contracts.
- At least one model provider can be swapped, one policy/custom instruction can be changed, and one first-party skill can be added without changing the core assistant/domain contracts.
- Contract/conformance tests, manifests, example extension and documentation show a future external builder how to add a channel or skill without raw database, mailbox or tenant access.
- The same core capability runs self-hosted where applicable and managed without forked domain logic.
- Extension friction, implementation effort, permission surface, support cost and developer feedback are measured and feed the Phase 2/7 roadmap.

## Working-profile product principles

- Lead with the decision or next action.
- Preserve source, scope, timeframe, ownership, and intended outcome.
- Make dependencies, blockers, and follow-up responsibility visible.
- Turn recurring questions into mechanisms such as recurrence, reusable checklists, reviews, and reminders.
- Use more rigor for money, health, family, identity, privacy, and externally shared data.
- Keep facts, assumptions, interpretations, and recommendations distinct.
- Treat profile-derived defaults as editable preferences, not immutable claims.
- Keep the attached working-profile document out of the repository and production prompts; store only user-approved preferences.

## Rubber-duck corrections to the original plan

The architecture review identified these required changes:

- `OwnerId` is not a tenancy model. Introduce tenant, user, external identity, and membership concepts before persistence APIs harden.
- Blazor Interactive Server is acceptable for the personal MVP only if components consume a typed API/client boundary that makes Interactive Auto or WebAssembly a reversible choice.
- GitHub Copilot SDK is a supported personal and SaaS provider option, but not the only assistant backend. Product use still requires a provider abstraction, per-user entitlement or BYOK handling, metering, and provider-specific policy.
- Infrastructure, security, privacy, observability, cost controls, and end-to-end testing start in the walking skeleton rather than being final hardening work.
- Federation requires access-scoped projections, grants, bilateral consent, and contact/principal separation before read models and identifiers become entrenched.
- Skills require a manifest, capability model, and narrow host boundary now; third-party execution and a marketplace remain later phases.
- The first release was too large. The correct response to a larger future is a smaller production walking skeleton with deliberate seams and explicit exit gates.

## Non-negotiable engineering principles

### Modular architecture

- Use Clean/Onion dependency rules without a rigid project-per-ring ceremony: domain and use cases point inward; frameworks and providers remain replaceable outer adapters.
- Implement the product as a modular monolith organized into vertical business-capability slices, with hexagonal ports/adapters at genuine volatility boundaries.
- Keep domain and application code independent of ASP.NET Core, EF Core, Azure, GitHub, and model SDK types.
- Phase 1 ports/adapters cover identity, persistence, assistant providers, skill execution, secrets/keys, and the deployment/data-plane boundary. Peer and connector ports exist initially as contracts only.
- Add notification, feedback-publisher, or telemetry-enrichment abstractions only when multiple implementations or a proven volatility boundary exists; OpenTelemetry already supplies the telemetry standard.
- Enforce module and dependency direction with architecture tests.
- Prefer one deployable until operational evidence justifies a separately scalable component.
- Split independently only where execution characteristics demand it, such as scheduled reminders or isolated third-party skills.
- Separate the optional Andreja cloud control plane from user data planes. Self-hosted instances remain fully functional without Andreja cloud and opt in separately to registry, update, discovery, support, or federation services.
- Avoid lowest-common-denominator abstractions. Define Andreja capabilities and allow provider adapters to expose optional extensions through explicit capability negotiation.

### Cloud portability

- Application code targets provider-neutral capabilities: relational persistence, object/blob storage, queue/event delivery, secrets, identity/OIDC, email/notification delivery, distributed locks when justified, assistant providers, telemetry export, and key management.
- Use OpenTelemetry, OAuth 2.0 Authorization Code with PKCE/OIDC following RFC 9700, CloudEvents-compatible envelopes, transactional outbox/inbox patterns, and standard HTTP/MCP contracts rather than cloud-specific application APIs.
- Package application runtimes as OCI-compatible container images. OCI standardizes image/runtime/distribution concerns only; ingress, persistence, identity, networking, scaling, and orchestration remain deployment-adapter responsibilities.
- Defer the runtime orchestrator choice to the architecture spike. Start with no Kubernetes dependency and define measurable adoption triggers such as customer-hosted enterprise requirements, sustained scale, multi-service scheduling, networking complexity, or managed-platform limits.
- Use OpenTofu/Terraform with an Azure reference module and paper mappings for AWS/GCP; add another provider module only when a defined trigger exists. Pin providers, commit dependency locks, use encrypted remote state with locking and recovery, and authenticate CI through workload identity rather than long-lived credentials.
- Keep cloud SDKs inside adapter projects and run provider contract/conformance tests. Do not pretend infrastructure is portable merely because interfaces exist.
- Preserve portability without funding active-active multi-cloud before reliability, regulatory, customer, or business evidence justifies its cost.

### IoC and dependency injection

- Use constructor injection and explicit interfaces at external or cross-module boundaries.
- Keep service registration in a small composition root with feature-level `AddFeature` registration extensions.
- Forbid service locator patterns, ambient mutable singletons, and direct `IServiceProvider` use in business logic.
- Define lifetimes intentionally and test captive-dependency errors.
- Use typed `HttpClient` registrations with resilience, timeouts, cancellation, and trace propagation.

### Configuration

- Use strongly typed Options classes with validation and `ValidateOnStart`.
- Do not read raw `IConfiguration` outside the composition root.
- Layer checked-in safe defaults, local user-secrets, environment variables, and adapter-specific managed configuration/secrets services; Azure App Configuration and Key Vault are candidates only for the initial Azure deployment.
- Use feature flags for incomplete, risky, expensive, or provider-specific functionality.
- Fail startup clearly for invalid required configuration; do not silently substitute insecure defaults.

### API-first UI boundary

- Start with a .NET 10 Blazor Web App and Interactive Server for the personal MVP.
- Components depend on typed API clients and DTOs, not EF entities or in-process application handlers.
- Publish a versioned minimal HTTP API even when client and server are initially co-hosted.
- Keep validation rules shared where safe, while treating server authorization and validation as authoritative.
- Measure mobile latency, circuit memory, reconnect behavior, and concurrent circuit capacity before choosing the SaaS render mode.
- Qualify Interactive Server with WebSocket/proxy behavior, scale-out affinity, circuit memory, reconnect/cold-start behavior, rolling deployment effects, and failure recovery. Externalized Data Protection keys preserve cryptographic continuity, not live circuits.
- Preserve a path to Interactive Auto/WebAssembly and a PWA-like installable experience without rewriting application use cases.

## Identity, tenancy, and authorization foundations

- Generate internal GUIDv7 identifiers in application code with .NET's `Guid.CreateVersion7()` for tenants, users, principals, contacts, tasks, grants, and shareable resources.
- GUIDv7 values reveal approximate creation time and are identifiers, not capability tokens; possession of an ID never grants access.
- Model:
  - `Tenant`: data residency, lifecycle, plan, status, and policy boundary.
  - `AppUser`: internal product identity.
  - `ExternalIdentity`: provider/issuer and immutable external subject mapping; never use a provider subject such as Entra `oid` as a domain foreign key.
  - `PrimaryIdentityLink`: the user's selected primary sign-in identity, independently changeable after secure verification.
  - `TenantMembership`: role and status connecting users to tenants.
  - `Principal`: an identity that can own data or receive grants.
  - `Contact`: a tenant-local person record used by tasks and relationships.
  - `ContactPrincipalLink`: optional link when a contact has an Andreja identity.
- Every persisted tenant-owned record carries `TenantId`.
- Resolve tenant and principal into scoped context services.
- Apply tenant isolation globally for normal data access, reject mismatched writes, and enforce tenant-aware composite alternate keys/foreign keys/uniqueness so cross-tenant references fail in the database as well as policy code.
- Route any future cross-tenant or federated read only through one grant-aware authorization/query service; never scatter filter bypasses across features.
- Return access-scoped projections from every read path. Never bind UI, APIs, skills, or assistants to raw persistence entities.
- Begin with one tenant and one user, but exercise isolation with automated two-tenant tests from the first persistence release.
- Provisional self-host default: built-in ASP.NET Core Identity with passkeys and secure local recovery, behind the same identity boundary as external providers.
- Allow users to link multiple verified identities such as Google, Microsoft account, LinkedIn, Facebook, GitHub, enterprise OIDC, or later providers while selecting one primary identity.
- Enforce a unique `(Issuer, Subject)` external identity mapping. Never auto-link accounts based only on matching email addresses.
- Require HTTPS, correct WebAuthn relying-party ID/origin/Host configuration, explicit first-admin bootstrap, recent authentication, anti-account-takeover checks, collision handling, audit, notification, unlink protection, and at least one remaining recovery/sign-in path.
- Define passkey registration/device limits, duplicate/replace/revoke flows, lost-device recovery and multiple independent recovery paths. Passkeys are not treated as built-in MFA or trusted-attestation evidence without a separate verified policy.
- Back up and test recovery of ASP.NET Core Identity/Data Protection keys, historical wrapping keys and recovery material without exposing reusable secrets.
- Support optional bring-your-own OIDC for self-hosting. Keep the final self-host identity/provider set as a Phase 0/implementation ADR informed by security, usability, maintenance, and portability research.
- Use Entra only through a managed-cloud identity adapter. Record an ADR comparing Entra External ID/CIAM and alternatives so public onboarding does not require a data-model rewrite.

## Personal semantic graph

- Treat the "semantic web of the user" as a private, user-owned personal semantic graph, not as automatic publication to the public web.
- Seven of Nine leads the research with Spock for ontology/architecture, T'Pol for persistence, Deanna Troi for privacy/inference controls, Tuvok for authorization, Data for quality/provenance, and Hoshi/Jadzia for usable review experiences.
- Research and compare JSON-LD, RDF/OWL, schema.org, PROV-O, ActivityStreams, Solid concepts, portable graph formats, relational/JSON projections, graph databases, and embeddings. Reuse standards selectively; do not adopt a graph database or universal ontology without measured use cases.
- Candidate concepts include:
  - Person, Principal, Contact, Relationship, Organization, Account, Role, Project, Goal, Commitment, Task, Event, Place, Artifact, Skill, Interest, Preference, Persona, Claim, Source, Connector, Grant, Consent, and Publication.
- The holistic context vocabulary also investigates Asset/Thing, Financial Obligation, Benefit, TimeBlock, Routine, Like/Dislike, Stressor, Energy, Career Role/Capability, Opportunity, Community/Group, Need, Constraint, Tradeoff and user-defined Wellbeing Outcome.
- Profile, claim, and inference records carry tenant/user ownership, source/provenance, observed/asserted/inferred status, confidence, applicable/valid time, sensitivity, purpose, review state, and sharing policy. Ordinary task/calendar/contact entities retain normal domain and audit fields unless Phase 0 evidence justifies semantic projection.
- Inferences are proposals, not facts. The user can inspect, correct, reject, expire, export, and delete them. Andreja must not infer or expose sensitive traits merely because sources can be connected.
- Likes/dislikes, stressors, relationship quality, career progress, health/wellbeing and opportunity fit are self-declared or confidence-labeled reviewable hypotheses—not covert scores. Users can disable entire context domains and see exactly why an insight was produced.
- Embeddings and search indexes are derived/rebuildable accelerators, not the source of truth; deletion and grant revocation must propagate to derived indexes.
- Define a versioned ontology/profile schema, extension mechanism for skills, migration rules, and federation mappings. Skills request semantic capabilities/scopes rather than unrestricted graph access.
- Phase 0 produces representative user journeys, threat/privacy analysis, standards matrix, minimal ontology, portability format, and a decision on the smallest useful storage/projection approach.
- Bitemporal storage and graph-database adoption remain Phase 0 research questions, not Phase 1 assumptions.

## Sharing, consent, and federation foundations

### Strategic commitment

Andreja will preserve a path to fully open federation between independently hosted instances. The first releases will define and test the contract, not operate an internet-scale federation network.

### Invariants

- Data never changes tenant merely because it is shared.
- Access is granted, purpose-bound, minimized, time-bounded where appropriate, auditable, and revocable.
- Relationship labels such as friend, partner, or family are UX presets, not authorization rules.
- Incoming peer and skill content is untrusted data, never model instructions.
- Non-owner changes arrive as proposals; the owning tenant remains authoritative.
- Reference sharing is the default. Copying into another tenant is an explicit, separately consented action whose revocation limits are disclosed.

### Structural seams created early

- `Grant`: resource or scope, grantee principal, purpose, disclosure level, allowed operations, expiry, revocation, and consent reference.
- `ConsentRecord`: offered, accepted, active, rejected, expired, or revoked; bilateral and timestamped.
- `ShareAuditEntry`: append-only access and change audit distinct from normal task activity.
- A disclosure ladder that avoids premature arbitrary field ACLs:
  - `Existence`: availability/duration only.
  - `Timing`: title, time, and location needed for plans.
  - `Summary`: outcome, status, and participants.
  - `Full`: notes, amounts, attachments, and provenance when explicitly allowed.
- `IPeerChannel` and a signed, versioned envelope with sender, recipient, grant, purpose, nonce, idempotency key, issue time, expiry, payload type, and protocol version.
- One authorization policy evaluator that calculates the intersection of tenant residency, principal grant, disclosure level, purpose, and operation.
- One proposal concept reused for assistant writes, peer-suggested changes, and skill-originated changes.

### Federation roadmap requirements

- Publish protocol specifications, schemas, discovery, capability negotiation, identity/trust, signing, replay protection, revocation, error semantics, and conformance tests before external interoperability.
- Compare established standards rather than inventing everything: OAuth 2.0/OIDC with RFC 9700 and PKCE, authorization-server/protected-resource metadata, resource indicators, sender-constrained short-lived tokens such as DPoP when justified, HTTP Message Signatures (RFC 9421), DID/VC only if justified, ActivityPub/Matrix concepts only as references, and MCP for tool interoperability.
- Use single-owner records and proposals before considering shared mutation. Do not adopt CRDTs without evidence that collaborative document editing requires them.
- Provide a subject-visible consent dashboard showing what is shared, with whom, for what purpose, at which level, recent access, expiry, and one-step revocation.
- Add per-grant rate limits, abuse controls, block/report mechanisms, and bilateral audit evidence.
- Support adults only initially. Family-sharing seams are preserved, but guardian-managed minor profiles require a separate legal, safety, age-assurance, privacy, and UX design before implementation.

## Skill ecosystem

### Definition

A skill is a versioned, declaratively permissioned capability package that can contribute one or more of:

- Assistant tools.
- UI surfaces.
- Domain templates and checklists.
- Scheduled jobs.
- External service integrations.

### Taxonomy

- User-facing activity categories begin with Calendar, Finance, Interests, and additional areas such as Health, Home, Social, Travel, Learning, and Trading.
- Preferences are per-user configuration schemas contributed or consumed by skills, not ambient permission and not unbounded executable behavior.
- Technical capabilities are separate from browse categories, for example `read:tasks`, `propose:tasks`, `read:calendar`, `network:external`, `invoke:ai`, and `read:shared:timing`.

### Early seam

- Define a skill manifest containing stable ID, semantic version, publisher identity, display metadata, activity categories, declared capabilities, data scopes, settings schema, minimum platform/protocol version, execution mode/entrypoints, declared network destinations, data retention, resource limits, package digest, signature, provenance/SBOM references, and compatibility metadata.
- Define a registry and narrow `ISkillHost`; skills never receive `DbContext`, secrets, or unrestricted service-provider access.
- Route the first product capabilities through this boundary to prove it: task/open-loop management, Calendar, a Finance checklist/review template, and an Interests queue.
- Evaluate every action using the intersection of user/peer grants, skill capabilities, resource disclosure level, and current purpose.

### Later ecosystem

- Start third-party executable skills as remote-only versioned MCP/HTTP services. Consider local sandbox execution only after isolation, update, provenance, revocation, and resource-control evidence exists. Untrusted code is never loaded into the main web process.
- Remote skills receive short-lived audience-bound capability tokens, destination/network allowlists and per-call authorization narrowed to the invoking principal, purpose and resource. Connector/provider tokens are never passed through; Andreja brokers approved operations and records provenance/audit.
- Third-party UI is either schema-driven/declarative or hosted on an isolated origin and embedded through a constrained capability bridge. Native in-process Blazor components remain first-party or separately trusted platform code.
- Add publisher verification, package signing, provenance/SBOM, automated scanning, capability review, resource/time limits, kill switches, revocation, compatibility testing, and user consent.
- Build a marketplace only after the authoring and trust model is validated.
- Publish SDKs, schemas, examples, conformance suites, and compatibility policy so external builders can participate without access to Andreja internals.

## Skill Development Framework

First-party skills follow the same platform contract and quality gates expected from future external builders; they do not bypass permissions, provenance, proposals, or testing because they ship in the repo.

1. **Skill charter:** user outcome, target personas, scenarios, evidence, non-goals, owner, category, launch band, and stop criteria.
2. **Semantic/domain contract:** concepts, inputs/outputs, provenance, confidence/time/sensitivity, ontology extensions, retention, and migration/version rules.
3. **Capability/permission design:** manifest capabilities, data scopes, channel dependencies, grants, network/model access, proposal/confirmation tiers, and degraded/manual behavior.
4. **Experience and help:** conversational patterns, UI surfaces, accessibility, settings/preferences schema, consent previews, explanations, errors, examples, and help/support content.
5. **Implementation:** domain/application use cases behind `ISkillHost`, typed tools, no ambient `DbContext`/secrets/service-provider access, structured results, cancellation, idempotency, telemetry suppression, and cost events.
6. **Safety/privacy/security review:** threat and privacy artifacts, prompt-injection/untrusted-data boundaries, sensitive inference controls, abuse cases, publication/share risks, and provider terms.
7. **Validation:** unit/domain, manifest/schema, permission-negative, channel contract, component/accessibility, E2E, adversarial AI, failure/recovery, performance/cost, compatibility, export/delete, and help-link tests.
8. **Dogfood:** smallest complete vertical slice, draft/read-only before consequential side effects, user feedback captured through Guinan's workflow, and measured outcome/usage/cost.
9. **Release progression:** dogfood, invite alpha, private beta, public beta, GA; every stage records supported capabilities, known limits, evidence, and rollback.
10. **Operate and improve:** SLOs, quality/freshness, model/channel changes, user feedback, outcome metrics, costs, incidents, compatibility, and backlog re-scoring.
11. **Version/deprecate:** semantic versioning, minimum platform/protocol, migration, re-consent for new scopes, compatibility window, export, replacement path, and revocation/retirement.

- A skill cannot declare itself successful from engagement alone; it must improve the user outcome in its charter without violating privacy, agency, authenticity, or cost guardrails.
- Third-party skills additionally require publisher identity, signature/provenance, remote execution trust, review status, resource limits, and ecosystem enforcement.

## Assistant and AI architecture

- Define application-owned `IAssistantProvider` and `IAssistantSession`; no provider SDK types cross into domain or UI contracts.
- [Proposed ADR 0009](adr/0009-copilot-provider-phase-scope.md) recommends that
  Phase 1A ship only the deterministic fake and Andreja-native OpenAI-compatible
  BYOK provider. Phase 1A may pin the Copilot SDK only in an isolated,
  non-shipping, credential-free compile/conformance spike. A limited real
  `GitHub.Copilot.SDK` 1.0.x provider begins no earlier than Phase 1B after ADR
  0009 is accepted and its SDK/support, entitlement/billing, tenant/user
  isolation, data exposure, retention/residency/abuse monitoring, auth custody,
  authenticated SDK-to-runtime control channel, budget, fallback,
  consent/disclosure, audit/provenance, offline, test/canary,
  legal/privacy/security, operability, and rollback gates pass.
- Keep GitHub OAuth account linking separate from primary app identity.
- Treat GitHub assistant authentication, the GitHub content connector, and GitHub feedback publishing as three independent grants with distinct tokens, scopes, consent, storage, and revocation. Never reuse one token across roles.
- Ship an OpenAI-compatible BYOK adapter alongside the deterministic fake in the walking skeleton so fully independent self-hosting has a working assistant and the provider-neutral seam has deterministic conformance evidence.
- Evaluate Azure AI Foundry/Azure OpenAI, local models, and other commercially appropriate providers for users without GitHub accounts.
- Register a narrow typed tool allowlist. Never expose arbitrary SQL, shell, filesystem, or unrestricted network tools.
- Any Phase 1B shared Copilot runtime uses empty mode, explicit available tools, per-session credentials, tenant-scoped session identifiers/state, and deliberate cleanup only after the ADR 0009 gates pass. Per-user runtime isolation is the initial preference; a shared runtime requires adversarial evidence and explicit residual-risk acceptance. Provider capability negotiation decides which features are available rather than assuming Copilot semantics.
- All assistant writes become structured proposals with exact diff, policy evaluation, and explicit confirmation; sharing/grant changes always use the strongest confirmation tier.
- Treat peer, connector, and skill data as untrusted prompt content and structurally separate it from instructions.
- Scope every tool invocation by tenant, principal, purpose, grants, skill capabilities, and conversation context.
- Set `CaptureContent=false` or the provider-equivalent for operational telemetry where supported and independently test suppression. Redact tokens and personal content from logs/traces; provide cancellation, timeout, retries only where safe, session cleanup, and user-visible errors.
- Store optional assistant conversation/session persistence separately from the content-free usage ledger. Treat prompts, responses, tool results, plans, and artifacts as sensitive tenant content with explicit retention, encryption, export, deletion, provider-retention disclosure, and opt-in/resume behavior.
- Deterministic fakes gate CI. Live model tests are capped, isolated, and non-deterministic smoke evidence rather than the sole blocking assertion.

### Assistant usage feedback loop

- Record one append-only usage event after every Copilot/Squad/assistant session: session ID, agent, provider, model, input/output/cache tokens when reported, provider usage units, duration, retries, tool calls, outcome, and phase/task attribution.
- Never persist prompts, responses, task content, or personal data in the usage ledger.
- Mark unavailable measurements as unknown; do not invent token or cost estimates.
- Treat Copilot `assistant.usage` as ephemeral per-call telemetry and provider `cost` as a usage value, not invoice currency. Reconcile provider usage, GitHub AI credits, invoices, and internal attribution separately before publishing financial totals.
- Quark owns session-close aggregation and reports cost per successful outcome, model mix, retry waste, and unusual growth to the retrospective.
- Feed Quark's evidence into model routing, economy mode, prompt/context reduction, agent fan-out limits, caching, and budget decisions.
- Set warning and hard-stop budgets for sessions, days, phases, tenants, and expensive live-test suites.
- Keep raw per-session usage in runtime/operational storage; commit only reviewed aggregate decisions or cost-model updates.
- Phase 0 must verify which fields each provider and Copilot CLI/SDK actually emits. Before product storage exists, write raw usage to session-scoped local operational storage; if token counts are unavailable, use explicitly labeled fallback units such as turns, sessions, duration, tool calls, and successful outcomes.

## Core product roadmap

The personal product eventually covers:

- Quick capture and full editing.
- Inbox, Today, Next Actions, Upcoming, Waiting For, Someday/Maybe, completed, and periodic review.
- Areas, lists/projects, contacts, tags, checklists, dependencies, provenance, reminders, and recurrence.
- Manual social commitments and family follow-ups.
- Reading and podcast queues.
- Trading research/watchlists, reminders, and review checklists, with no brokerage connection or order execution.
- User-editable interaction and privacy preferences.
- Future email, messaging, and calendar connectors.

The walking skeleton and first core release intentionally implement only a subset, defined by the phases below.

## Initial first-party skill catalog

Privileged platform capabilities are not skills:

| Platform capability | Responsibilities | Phase |
|---|---|---|
| Assistant Runtime and Review | Conversations, provider sessions, context/tool policy, proposals, confirmations, daily/weekly review orchestration | 1A-2 |
| Data Ownership and Privacy | Export, restore, retention, delete, consent, grant review, audit and privacy controls | 1A onward |
| Identity/Tenancy/Authorization | Passkeys/linking, tenant/principal context, access policy, recovery, consent and proposal enforcement | 1A onward |
| Skill Host, Channel Host and Semantic Profile Host | Manifest validation, capability policy, semantic projections, feedback and operability services | 0 research; 1A onward |

All actual first-party skills use the same manifest, capability, proposal, consent, audit, help, test, and semantic-extension contracts required of the future ecosystem:

| Skill | Initial capabilities | Band | Phase |
|---|---|---|---|
| Open Loops and Tasks | Capture, Inbox/Today/Upcoming/Waiting, dependencies, reminders, recurrence, checklists, outcomes, review | MVP | 1A-2 |
| Calendar and Commitments | Availability, commitments, follow-ups, conflict detection, trip/social planning, reminder proposals | MVP/early | 1B bounded invite intake/accept under email grant; 2 manual; 3B full channel |
| Personal Semantic Profile | User-reviewed identities, roles, relationships, interests, preferences, claims, sources, provenance, graph export | Research/early | 0 research; 2 profile |
| Personal Brand Studio | Professional/social persona, profile audit, evidence-backed claims, content plan, drafts, publication proposals | Early | 0 research; 2 dogfood; 8 publishing |
| Finance Administration | Bills, banking/admin follow-ups, budgets/checklists, document reminders; no money movement or trading execution | Early | 2 |
| Family and Relationships | Contact context, family support, school/health follow-ups, waiting-for and consent-aware coordination | Early | 4; sharing in 6 |
| Health and Wellbeing Manager | Appointments, medications/refills, allergies, providers, labs, imaging reports/scans, questions, care plans, wellness goals and reminders | Sensitive/later | 4 manual; 12 connected |
| Household, Vehicle, Insurance and Projects Manager | Homes/rentals, cars, insurance, utilities, appliances/systems, maintenance, warranties, renewals, inventory, vendors/quotes and collaborative improvement projects | Early/later | 4 manual; household collaboration in 5-6; connected in 8 |
| Travel and Social Planning | Itineraries, reservations, packing, guests, dinner/trip proposals, shared timing | Early | 1B trip-workspace MVP slice; 4 full skill; broader sharing in 6 |
| Interests, Reading, and Podcasts | Queues, notes, recommendations from user-selected sources, completion and follow-up | Early | 2 |
| Trading Research and Review | Watchlists, research prompts, thesis/checklist/journal reminders; no brokerage data or order execution | Later first-party | 4 |
| Lifestyle Rewards and Financial Optimization | Card benefits/offers, spending-category and shopping/cashback opportunities, fee/renewal reminders, user goals and optimization proposals | Regulated/later | 11 |
| Miles and Points Manager | Airline/hotel/card loyalty accounts, balances, expirations, status/benefits, transfer partners, valuations, goals, award/redemption planning | Regulated/later | 11 |
| Employer Benefits and Perks Manager | Eligibility, enrollment/deadlines, retirement/401(k) match, charitable-giving match, health/wellness, commuter, education, discounts, reimbursements and plan-year optimization | Sensitive/later | 11 |
| Hobbies and Social Groups Manager | Memberships, schedules, events/RSVPs, contacts, venues, dues, gear/projects, game collections/decks, tournaments and group follow-up | Early/later | 4 manual; 8 connected |
| Life Context and Opportunity Navigator | Explainable synthesis of people, money, things, preferences, time, stressors, career, events, tradeoffs, opportunities and consensual connections | Core/iterative | 0 research; 2 personal insights; 6 mutual connections |
| Life Event Planner | Scenario/milestone/dependency planning for retirement, home/car, college, elder care, moving, job/family/care transitions with collaborative tasks, documents, costs, risks and professional handoffs | Cross-domain | 4 manual; 5-6 collaboration; 11-12 connected evidence |
| Relationships and Communities Map | User-defined family/friend/frenemy context, contacts, interactions, communities/clusters, shared interests/events and relationship follow-ups from consented sources | Sensitive/iterative | 1B email-derived hypotheses; 2 manual; 8 photo/channel context; premium on-device clustering later |
| Small Business and Entrepreneur Manager | Business persona, customers/partners, projects, obligations, documents, invoices/expenses, cash-flow admin, deadlines, operations, marketing/support and owner dashboard | Business/sensitive | 4 Customer Zero/manual; 8 connectors; 10 analytics |

### Personal Brand Studio guardrails

- Support separate user-controlled personas and goals for professional channels, public portfolio/GitHub, and social channels.
- Build claims from user-approved evidence and provenance. Never fabricate employers, achievements, endorsements, credentials, relationships, experiences, or engagement.
- Provide profile audits, positioning options, channel-specific bios, portfolio/GitHub profile content, content calendars, draft posts, image/asset briefs, accessibility checks, and outcome review.
- Draft and preview first. Initial releases do not auto-publish. Every publication is channel-scoped, user-confirmed, auditable, reversible where the channel permits, and checked for accidental personal/sensitive disclosure.
- Keep private identity/relationship/task graph data separate from publishable brand facts. Moving information into a public persona is an explicit disclosure proposal.
- Do not include identifiable third-party names, photos, relationships, workplace details, family context, or private communications in a publication proposal without a specific disclosure check and consent where required.
- Record each channel's AI-generated-content disclosure, automation, authenticity, rate, and publishing rules in its channel manifest and enforce them before publication.
- Neelix leads product/market requirements, Seven owns the skill and semantic integration, Jadzia owns authoring UX, Deanna Troi owns privacy/authenticity, Tuvok owns connector/publishing security, Data owns evidence quality, and Cyrus approves publication.

### Lifestyle rewards, miles, and points guardrails

- Separate inventory/management from optimization:
  - Miles and Points Manager tracks airline miles, hotel points, credit-card points, expiration, status, transfer relationships, valuations, goals and redemption options.
  - Lifestyle Rewards Optimizer combines user preferences, travel goals, card benefits, offers, fees, shopping/cashback and loyalty inventory to produce explainable proposals.
- Read-only aggregation is the default. User-confirmed redemptions, transfers, offer activation, account/card changes, or other actions are enabled only for official provider/partner APIs after legal/security/privacy review.
- Consequential actions require step-up authentication, exact value/fee/expiry/irreversibility preview, idempotency, provider confirmation/receipt, limits, audit, cancellation/recovery where possible, and no unattended automation.
- Never store or request full card numbers, CVV, banking passwords, loyalty passwords, security answers, or session cookies. Use provider-hosted OAuth/tokenization or approved aggregators/partners.
- No credential scraping, browser self-bots, terms-violating automation, card churning, deceptive applications, manufactured spending, or recommendations that misrepresent eligibility/income.
- Show valuations, transfer ratios, fees, taxes, availability, devaluation/expiration risk, and assumptions as time-sensitive estimates rather than guaranteed value or financial advice.
- Research current product/API status before promising integrations. Named candidates include approved financial aggregators, Quicken products/successors, Chase, American Express, AwardWallet, Rakuten, CardPointers, Bilt, airline programs, hotel programs, and card reward programs.
- Quark owns economic/value methodology, Seven owns skills/channels, Tuvok owns financial-action security, Deanna Troi owns data minimization, Sarek owns regulatory/terms questions, Data owns reconciliation quality, and Cyrus confirms consequential actions.

### Employer Benefits and Perks Manager guardrails

- Track employer, plan year, eligibility, enrollment windows, benefit documents, contacts, deadlines, user elections and evidence for:
  - Retirement/401(k) match, stock/ESPP where applicable, charitable-giving match, health/wellness, HSA/FSA, commuter, education/tuition, insurance, leave, discounts, reimbursements, memberships and employer-specific perk platforms.
- Explain match formulas, deadlines, vesting, limits, fees, tax assumptions and required actions from current user-approved plan documents; do not assume two employers/plans are equivalent.
- Initial mode is document/inventory/reminder/what-if analysis. Enrollment, contribution, claim, donation-match or account changes require official provider workflows, exact user review, step-up authentication, receipts/audit and no unattended execution.
- Do not provide individualized tax, investment, ERISA or legal advice. Surface assumptions and direct users to plan administrators, fiduciaries, tax professionals or counsel for authoritative decisions.
- Never store employer, payroll, retirement or benefits passwords/session cookies. Use provider-hosted OAuth/SSO/partner APIs or user-controlled documents.
- Keep employer/confidential plan data, compensation, elections, donations, health benefits and inferred financial/health traits highly sensitive and excluded from sponsors, public brand, business analytics and unrelated skills.
- Research current approved access for employer portals and providers such as Workday/benefits systems, retirement custodians, charitable-match platforms, wellness/perks services and employer-specific systems before promising automation.
- Quark owns benefit-value methodology, Seven owns the skill/channel design, Sarek owns plan/tax/legal boundaries, Tuvok owns high-assurance actions, Deanna Troi owns privacy, Beverly Crusher reviews health-benefit boundaries, Data owns reconciliation and Cyrus confirms consequential actions.

### Health and Wellbeing Manager guardrails

- Organize appointments, providers, medications/doses/schedules/refills, allergies, vaccinations, labs, imaging reports/scans, referrals, questions, instructions, care plans, insurance artifacts, wellness routines and user-defined goals.
- Preserve the original clinical artifact and source. AI/OCR extraction creates a confidence-labeled proposal linked to the source; it never replaces or silently edits the clinician/lab/radiology record.
- Initial imaging support manages files, reports, dates and follow-up—not autonomous interpretation of medical images.
- Do not diagnose, prescribe, recommend stopping/changing medication, claim clinical certainty, or replace clinicians/pharmacists/emergency services. Escalate urgent symptoms or conflicting medication instructions to appropriate human care without attempting triage beyond approved safety guidance.
- Medication, refill, appointment, record-request or provider-message actions require official channels, exact user review, step-up authentication where sensitive, receipt/audit, and no unattended execution.
- Health/wellbeing optimization is user-goal-driven: routines, preparation, adherence reminders, sleep/activity/nutrition reflections and trend questions. Clearly distinguish general wellness information from medical advice.
- Treat health data and inferred health traits as highest-sensitivity. Use separate scopes, encryption/key/access review, minimal telemetry, explicit sharing, short retention where appropriate, export/delete, derived-index purge, and no sponsor/business analytics use.
- Research applicable consumer-health, medical-record, HIPAA/covered-entity/business-associate, state privacy, GDPR special-category, app-store and provider requirements based on deployment and partnerships; do not assume HIPAA status from feature names.
- Candidate channels require official access and conformance evidence: user-controlled documents, SMART on FHIR/FHIR, patient portals, Apple Health/HealthKit, Android Health Connect, pharmacy, lab, imaging and approved wearable/provider APIs.
- Beverly Crusher owns health outcomes/safety requirements; Seven owns the skill/channel architecture; Tuvok owns access/action security; Deanna Troi owns sensitive-data privacy; Sarek owns legal/regulatory research; Data owns provenance/reconciliation; Cyrus confirms consequential actions.

### Household, Vehicle, Insurance and Projects Manager guardrails

- Track multiple homes/rentals, vehicles, policies, utilities, appliances/systems, registrations, service history, recalls, warranties, inventories, receipts, renewals, recurring day-to-day/month-to-month obligations and improvement projects.
- Support vendor/contractor discovery, comparable quote normalization, total-cost and coverage tradeoffs, project scope/budget/schedule, permits/inspections, documents/photos, tasks, dependencies, decisions and completion evidence.
- "Best deal" recommendations disclose coverage/specification differences, exclusions, fees, deductibles, assumptions, incentives/sponsorship and data freshness; lowest price is not automatically best value.
- Insurance guidance is comparison/organization, not licensed insurance/legal advice. Binding/changing/canceling coverage, claims, financing, vehicle/property actions or purchases require official provider paths, step-up authentication, exact confirmation, receipt/audit and no unattended execution.
- Personalized insurance steering, ranking, referrals, lead generation or compensation require jurisdiction-specific licensing/producer/referral/advertising review before display or monetization; otherwise provide neutral organization and user-defined comparisons.
- Household collaboration uses tenant membership/grants and shared projects. A spouse/family relationship does not imply full access: money, insurance, location, vehicles, documents and contractor communications have explicit disclosure/operation scopes and revocation.
- Same-tenant household work arrives in Phase 5; independently hosted family collaboration uses Phase 6 federation proposals/reference sharing. Single-owner records remain authoritative.
- Never store insurer/utility/vehicle portal passwords, full payment card data, garage/access codes, alarm credentials, vehicle keys or sensitive property-security details outside dedicated approved secret/key facilities.
- Candidate channels require official/partner access: user-controlled documents/email/calendar, insurance/provider APIs, vehicle/telematics, utilities, warranty/receipt services, home-service/contractor platforms and approved smart-home sources.
- Jett Reno owns household/vehicle/project workflow, Quark owns cost/deal methodology, Seven owns skills/channels, Tuvok owns property/action security, Deanna Troi owns household privacy, Sarek owns insurance/contract boundaries, Data owns comparison provenance and Cyrus confirms consequential actions.

### Hobbies and Social Groups Manager guardrails

- Support local/user-owned groups and memberships such as Xbox/Discord gaming groups, MTG playgroups/decks/events, poker clubs, garden clubs, hobby projects and recurring meetups.
- Track group purpose, membership/contact references, roles, schedule/events/RSVPs, venue, reminders, dues/fees, gear/projects, collections/decks, tournament/league records, notes and follow-ups.
- Use in-app/calendar/manual operation first. Discord uses an official bot/app with explicit installation and channel scope; Xbox/game/community integrations require official supported access and no user-token/session scraping.
- Group members' identities, availability, messages, photos, locations and attendance remain private to the user's tenant unless each share has purpose, scope and consent. Do not build shadow profiles or import entire group history by default.
- Sending invitations/messages, changing group state, publishing attendance/results or sharing contact/location data requires preview and confirmation; apply anti-spam/rate and abuse controls.
- Poker support is social scheduling, records and user-defined entertainment budgeting only; no wagering execution, payment custody, odds/betting optimization, illegal-game facilitation or claims of gambling profit.
- Public game/card/reference data may enrich user-owned records only under compatible terms and provenance; distinguish community facts from private group data.
- Neelix owns community/group outcomes, Seven owns skill/channel integration, Deanna Troi owns relationship/privacy controls, Tuvok owns messaging/access abuse controls, Data owns record quality and Cyrus confirms consequential sharing.

### Life Context and Opportunity Navigator guardrails

- Build a user-visible "world model" across enabled domains: people/relationships, money/obligations, possessions/artifacts, likes/dislikes, time/routines, stressors/energy, career/capabilities, commitments, communities and events.
- The user defines goals, values, constraints and acceptable tradeoffs. Andreja performs multi-objective reasoning and presents options; it does not optimize a hidden engagement, productivity, wealth or social score.
- Every insight/opportunity explains the source, inference, confidence, affected goals, tradeoffs, why-now signal, privacy scope and next reversible action.
- Career insights use user-approved evidence and distinguish skills/opportunities from employer performance judgments. Do not ingest or expose confidential employer/coworker information without authorization.
- Stress/wellbeing signals are self-reported or reviewable hypotheses, never diagnoses or covert psychological profiling. Sensitive inferences are excluded from sponsorship, business analytics, public brand and unrelated skills.
- Meaningful connection proposals require purpose, mutual benefit and consent. Do not rank people by worth, reveal private compatibility/stress/finance/health data, create shadow profiles, manipulate relationships or contact others without confirmation.
- Event opportunities may include learning, career, community, family, health, travel, volunteering or financial deadlines, but provider/public-source terms, time/location privacy and user availability are respected.
- Users can inspect, correct, disable or delete any context domain/inference and run the assistant in a deliberately narrow mode.
- Seven owns semantic/assistant design, Deanna Troi owns human/privacy boundaries, Picard owns outcome strategy, Neelix owns community/opportunity framing, Tuvok owns access/abuse controls, Data owns evidence quality and Cyrus controls goals and actions.

### Life Event Planner guardrails

- Support user-defined life-event workspaces such as retirement, new home, new car, college for children, elder care, moving, job change, family/care transitions and other major commitments.
- Model goals, stakeholders, scenarios, assumptions, decisions, milestones, dependencies, costs/funding, documents, risks, deadlines, professionals, tasks, communication and completion evidence.
- Compare scenarios with explainable assumptions and sensitivity ranges; never present uncertain future returns, health outcomes, admission, property value, care needs or eligibility as guaranteed.
- Produce professional-handoff packets and questions for financial planners, lenders, real-estate professionals, lawyers, tax professionals, schools and clinicians rather than replacing them.
- Collaboration is purpose-scoped: spouse/family/caregivers receive only the event resources/operations they need. Children, elders and other affected people are not silently profiled or enrolled; adult-only account policy and future guardian/care authority rules still apply.
- Financial/health/legal/employer inputs remain within their source skills and grants. The planner consumes access-scoped projections and cannot bypass confirmations or perform transactions/actions itself.
- Every recommendation states user goals, sources, scenario assumptions, tradeoffs, uncertainty, reversibility and next review point. Users can branch scenarios without overwriting the agreed plan.
- Seven owns cross-skill orchestration, Picard owns outcome/decision framing, Quark owns financial scenarios, Beverly Crusher owns care/health boundaries, Sarek owns legal/tax boundaries, Deanna Troi owns family consent, Data owns assumptions/evidence and Cyrus chooses the plan.

### Relationships and Communities Map guardrails

- Support user-defined private relationship labels such as family, friend, colleague, community member or "frenemy." Andreja never assigns adversarial/value labels to people autonomously.
- Build relationship/community context from explicit contacts, calendars, user notes, consented messages, group membership and user-selected photo artifacts. Co-occurrence or cluster observations are confidence-labeled hypotheses the user must review.
- Initial photo mode uses official OneDrive file selection and Google Photos Picker-selected items with only the basic metadata actually returned by each provider and shown in the consent preview. It does not promise provider albums/person labels, automatically identify faces, or ingest an entire library.
- Google Photos Picker-derived media is excluded from face clustering. Any future on-device clustering requires a separately lawful/user-controlled source and the premium biometric gate.
- Do not infer race, ethnicity, religion, politics, health, sexuality, immigration, finances, relationship intimacy or other sensitive group membership from appearance, location, photos or association.
- Strip or minimize precise location/device metadata unless explicitly needed. Never publish photos, names, locations, relationship labels or cluster membership to brand/social/sharing features without a separate disclosure proposal.
- Non-user contacts remain tenant-local `Contact` records, not principals or shadow accounts. The user can correct, merge, unlink, expire and delete relationship/cluster hypotheses and all derived indexes.
- Future premium on-device face clustering requires a separate biometric/legal/privacy/security ADR, explicit opt-in, local encrypted templates, no automatic names, no cloud template upload, reset/delete/export and device capability evidence.
- Deanna Troi owns relationship/privacy requirements, Seven owns semantic/photo integration, Tuvok owns biometric/location/access security, Neelix owns community use cases, Data owns provenance/quality and Cyrus approves any expansion beyond non-biometric mode.

### Small Business and Entrepreneur Manager guardrails

- Model a business persona separately from the user's personal/household persona: legal/display identity, roles, customers/leads/partners/vendors, projects, commitments, products/services, documents, support, marketing, invoices/expenses, cash-flow administration, taxes/compliance deadlines and operating metrics.
- Allow explicit cross-persona references (for example owner contribution or shared calendar) without silently merging personal and business contacts, finances, files, brand, retention or analytics.
- Initial mode is planning, records, reminders, drafts, reconciliation and professional handoff. Banking, payroll, payments, contracts, tax filings, invoices sent, purchases, ads or public communications require official channels, business authorization, exact confirmation and receipts/audit.
- No legal, accounting, tax, investment, HR or employment advice. State assumptions and route authoritative decisions to qualified professionals.
- Multi-user businesses require business-tenant roles, least-privilege access, separation of duties, audit, offboarding, records retention and ownership transfer; a personal relationship does not imply business access.
- Customer/employee/vendor personal data is not available to Personal Brand, Life Context or unrelated personal skills without purpose and explicit policy.
- Customer Zero uses this skill to operate Andreja company/brand/marketplace and expose platform gaps, while keeping internal company data out of generic samples/tests.
- Picard owns business outcomes, Quark owns financial administration, Seven owns skill/channel contracts, Guinan owns customer/support workflows, Neelix owns marketing/community, Tuvok/Deanna/Sarek own trust/legal boundaries, Data owns reconciliation and Cyrus confirms consequential actions.

## Deployment, data ownership, hosting, and scale

### User-owned data planes

- Define a portable data-plane contract that includes user data, metadata, grants, audits, skills/settings, connector state, assistant sessions, export, deletion, and migrations.
- Ship a containerized self-host bundle that is fully functional without Andreja cloud and uses PostgreSQL.
- Offer optional managed data planes with security, backup, recovery, observability, and support included in the service.
- Keep the managed relational database choice open until the Phase 0 spike compares managed PostgreSQL, Azure SQL, and credible AWS/GCP equivalents against portability, features, operations, cost, migration, and scale requirements.
- Keep provider-specific EF Core mappings and migrations isolated. Run the persistence conformance suite against PostgreSQL and every selected managed provider, and forbid provider-specific behavior from leaking into domain rules.
- Provide a versioned cloud-neutral Andreja application export containing manifest, schema/protocol version, checksums, supported user data, attachments, grants/consent/audit, derived-data exclusions and import/migration instructions. Secrets, provider tokens and credentials are excluded and require reauthorization.
- Keep three mechanisms distinct: provider operational backup/PITR, PostgreSQL-specific logical dump/restore, and portable Andreja application export/import. Phase 1A verifies all applicable local restore/import paths; later phases automate retention, DR and larger recovery drills.
- Encrypt delegated connector/provider tokens in the user's chosen data plane. Andreja cloud must not receive self-hosted tokens or content unless the user explicitly enables a cloud capability that requires them.

### Optional control plane

- Self-hosted instances opt in separately to skill-registry discovery, signed updates, federation discovery, support diagnostics, licensing if applicable, and managed relay services.
- The open protocol and self-host bundle must not require a central Andreja service for normal assistant, skill, data, identity, backup, or federation operation.
- Separate control-plane identity and telemetry from user content, and document every outbound call from a self-hosted instance.

### Initial managed-cloud hosting

- Use Azure as the first managed-cloud candidate, but make compute and database choices ADRs informed by a measured Phase 0 spike and portability evidence.
- Compare Azure App Service B1, App Service Standard with slots, and Azure Container Apps Consumption/Dedicated for WebSockets, cold starts, child-process constraints, scale-out, operational burden, and monthly cost.
- Compare managed PostgreSQL and Azure SQL options; preserve a tenant-aware routing abstraction for later pools, partitioning, sharding, or database-per-tenant tiers.
- Persist and encrypt ASP.NET Core Data Protection keys outside ephemeral compute, retain required historical wrapping keys, and test identity/cookie/recovery behavior after restore and key rotation.
- Use managed identity for Azure resources, Key Vault for application secrets, and encrypted database storage for per-user delegated tokens with revocation/unlink support.
- Run reminders through an idempotent durable mechanism such as Azure Functions plus Storage Queue rather than an in-process timer.
- Use explicit migration bundles/jobs; never run uncontrolled schema migration on web startup.
- Design stateless HTTP APIs and externalize only the state required for scale. Treat Blazor circuits and assistant sessions as measured constraints.
- Build one authenticated-app OCI artifact. The public site may be a separately deployed pre-generated static artifact. Add a separate external Copilot/runtime image only if the measured topology requires it.
- Pin self-host Compose/runtime images by immutable digest, provide HTTPS/passkey onboarding, and verify restart/revision behavior without silently losing tenant data, keys, or assistant configuration.
- Add health/readiness endpoints, safe deployment rollback, backup/restore drills, tenant export, disaster-recovery objectives, and runbooks.
- Validate responsive access from phone, tablet, and desktop browsers in every release.

## Connector platform

- Treat channels/connectors and skills as separate artifacts: a channel manifest describes the external provider binding, grants and operating contract; a skill manifest declares dependencies on stable channel IDs/capabilities. Both are enforced by the same policy evaluator.
- Begin with OneDrive/Microsoft Graph, Google Drive, GitHub, and Box.
- Apply the Channel Development Framework for lifecycle requirements. During setup, require the user to choose explicitly between:
  - Query-in-place with least-privilege OAuth and minimal encrypted cache.
  - Sync/import of selected scopes into the user's Andreja data plane.
- Provide no default mode. Preview scopes, content classes, storage impact, assistant/model exposure, retention, deletion, and ongoing sync behavior before consent.
- Query-in-place cache must declare and enforce maximum TTL, maximum size, content type, and purge-on-disconnect behavior in the consent preview. It stores metadata and bounded retrieved excerpts only; any content retained beyond those bounds is sync/import mode.
- Treat provider change feeds/delta cursors as authoritative and webhooks as delivery hints. Use idempotency, replay/reconciliation, and provenance to prevent loss or duplication.
- Separate OneDrive selected-file/folder and broader-drive authorization profiles; prefer the smallest profile that satisfies the skill.
- Default Google Drive integrations to `drive.file` or narrower scopes where possible and expand only through explicit verification/consent. Prefer GitHub Apps with fine-grained repository permissions over broad user tokens.
- Separate read, search, metadata, content, write, delete, and webhook permissions. Request scopes progressively rather than up front.
- Revoke tokens, stop jobs, clear eligible caches, and explain retained imported copies when a connector is disconnected.
- Keep connector content structurally untrusted for assistant prompting and subject every tool call to skill, user, tenant, and sharing policy.
- Add provider contract tests, sandbox accounts, rate-limit/error tests, and terms/privacy reviews before release.
- Research and budget provider approval obligations before scheduling public connectors, including Google OAuth verification/restricted-scope security assessment requirements and Microsoft Graph, GitHub, and Box equivalents.

### Connector catalog and release bands

Connector identity, assistant, content, feedback, and publishing grants remain separate even when the same provider appears in multiple rows.

| Category | Baseline connectors | Planned use and boundary | Band | Phase |
|---|---|---|---|---|
| Local identity | Passkeys/local recovery | Independent self-host sign-in and recovery | MVP | 1A |
| Linked identity | Microsoft account, Google, GitHub; later Apple, LinkedIn, Facebook and enterprise OIDC | Verified account linking and primary-identity flexibility; no email-only linking | MVP/research | 1A-2 |
| Assistant providers | Deterministic fake and OpenAI-compatible BYOK; non-runtime SDK qualification in 1A; limited real GitHub Copilot only after ADR 0009 acceptance and entry gates; later Azure AI/local providers | User-selected assistant runtime with isolated credentials, retention and usage policy | MVP | Proposed ADR 0009: BYOK/fake in 1A; earliest real Copilot is gated 1B |
| Email intake/send | Gmail, Outlook.com/Hotmail, Microsoft 365 | MVP autonomous triage/task/calendar/control-plane story, with per-account policy; broader email workflows later | MVP | 1B core; 3A expansion |
| In-app messaging | User-to-Andreja, feedback/support status and scoped trip collaboration; later general peer assistants | Private product messaging and proposal/status delivery; no ambient cross-tenant chat | MVP/early | 1B trip/support; general peer in 6 |
| Discord | Official Discord bot/application | Explicit installation and server/channel scope; never self-bot or user-token automation | Early pilot | 3A |
| Gaming/hobby communities | In-app groups/calendar; Xbox-approved and later game/community providers | Membership/events/collections/projects with official access; no private-history scraping or gambling execution | Early/later | 4 manual; 8 connected |
| WhatsApp | User share/export; official WhatsApp Business Platform where applicable | No unsupported personal-history access; official/business or user-controlled paths only | Early constrained | 3A pilot; expand in 8 |
| File/content | OneDrive/SharePoint, Google Drive, GitHub, Box | Explicit query-in-place or sync/import; least privilege, provenance, bounded cache | Early | 3B |
| Photo context | User-selected OneDrive photo files and Google Photos Picker items through official APIs | Provider-returned basic metadata and reviewable non-biometric context only; no labels/albums promise, automatic identity, full-library default or clustering of Picker-derived media | Sensitive/later | 8 pilot; premium on-device clustering separately gated |
| Calendar | Microsoft 365/Outlook and Google Calendar | Commitments, reminders, availability and user-reviewed intake; manual calendar skill works earlier | Early/later | 1B bounded email-provider invite surface; 3B/8 full channel |
| Developer/professional brand | GitHub profile/README/repos, LinkedIn | Evidence-backed professional persona and draft/update proposals | Early/research | GitHub in 3B; publishing in 8 |
| Social brand | Facebook and Instagram professional surfaces; later approved channels | Draft/export first; publishing only where official APIs, account types and review permit | Research/later | 8 |
| Support/project | GitHub Issues/PRs/stacks and app-owned feedback publishing | Feedback, project work, release/support evidence; private triage queue before public issue | MVP/early | 1B-2 |
| Notification | Transactional email first; later mobile push and additional channels | Support acknowledgments, reminders and status; no marketing mail without separate consent | MVP/early | 1B-2 |
| Sponsorship/payment | Approved sponsorship provider | Project donations/recognition only; no user financial-data connection | Research | 0 policy; later activation |
| Additional storage | Dropbox and other partner stores | Add only after connector contract and demand evidence | Later | 8+ |
| Financial/card data | Approved aggregators and provider/partner APIs; candidates include Quicken products, Chase and American Express | Read-only first; tokenized data only; confirmed provider-supported actions require regulated gates | Regulated/later | 11 research/pilot |
| Loyalty/rewards | AwardWallet, Rakuten, CardPointers, Bilt, airline, hotel and card reward programs where official/partner access exists | Balances, expiry, status, benefits, transfers, redemption/offer proposals and receipts; no credential scraping | Regulated/later | 11 research/pilot |
| Employer benefits/perks | User documents; later approved HR/benefits, retirement, charitable-match, wellness, commuter, education and perk-provider channels | Eligibility, match, deadlines, elections, claims and user-confirmed official actions; no payroll/portal credential scraping | Sensitive/later | 11 research/pilot |
| Health/wellbeing | User-controlled documents; later SMART on FHIR/FHIR, patient portals, Apple Health/HealthKit, Android Health Connect, pharmacy/lab/imaging and approved wearables | Highest-sensitivity records, reminders and user-reviewed actions; no autonomous diagnosis or medication change | Sensitive/later | 4 manual; 12 connected |
| Household/assets | User documents/email/calendar; later approved insurance, vehicle/telematics, utility, warranty, receipt, contractor/home-service and property channels | Maintenance, renewals, inventory, quotes/deals and collaborative projects; no credential/access-code scraping | Sensitive/later | 4 manual; 5-6 collaboration; 8 connected |
| Brokerage/trading execution | None initially | Research/checklists only; market data, brokerage access and orders require a separate regulated decision | Deferred | Unscheduled |

- Each connector issue records provider terms, supported account types, required verification/security assessment, scopes, rate limits, webhook/change-feed behavior, data residency/retention, cost, test tenant, support burden, and exit strategy.
- Social/professional APIs change frequently and often restrict personal profiles or publishing. The roadmap promises capabilities only after official provider access is verified; draft/export/manual workflows remain the safe fallback.

## Channel Development Framework

A channel is a typed, permissioned connection between Andreja and an external identity, assistant provider, content source, communication surface, publication target, notification destination, support system, or approved business service.

1. **Charter the user outcome:** audience, jobs-to-be-done, read/write/publish behavior, success evidence, non-goals, and manual fallback.
2. **Qualify the provider:** official API and account types, terms, app review, OAuth verification/security assessment, pricing, limits, regions, support, deprecation policy, and business viability.
3. **Define the channel manifest:** stable ID/version, category, provider, account type, capabilities, OAuth scopes, data classes, query/sync/publish modes, webhook/change-feed support, retention/cache, costs, and minimum platform version.
   - Add a provider delivery-topology ADR: polling/manual, provider watch renewal, webhook, Pub/Sub/event bus, Gateway/socket, public callback, NAT/egress, optional Andreja relay, exposed metadata/content, reconciliation and cost.
   - Do not assume every self-host connector can or must expose a public callback. Offer a documented polling/manual fallback where provider terms and freshness requirements permit; any managed relay is separate opt-in with explicit data disclosure.
4. **Threat/privacy design:** least privilege, token isolation, consent preview, provenance, untrusted-content handling, data-flow/retention map, model exposure, abuse/rate limits, disconnect/purge, and user export.
5. **Implement the adapter:** typed capability interface, provider mapping, idempotency, delta reconciliation, retries, backoff, circuit breaking, health, tracing, cost/usage events, and no provider types in domain/application code.
6. **Validate:** sandbox/test account, contract/conformance tests, permission-negative tests, failure/rate-limit/replay tests, telemetry-redaction tests, E2E user scenarios, provider-review evidence, help content, and runbook.
7. **Dogfood read-only/draft first:** query and draft/export before writes or publishing; require proposal/preview/confirmation for side effects.
8. **Release by stage:** internal dogfood, invite alpha, private beta, public beta, and GA gates with explicit supported account types and limitations.
9. **Operate:** SLOs, freshness/quality, token expiry, webhook/delta reconciliation, provider incidents, cost, support ownership, API/version monitoring, and user-facing status.
10. **Change or retire safely:** compatibility window, migration/export, re-consent for scope expansion, advance deprecation notice, token revocation, cache purge, and tested shutdown.

- Channel writes, sends, shares, publishes, deletes, or financial actions always require the capability and confirmation tier defined in the manifest and policy evaluator.
- One provider may expose multiple isolated channels/grants. GitHub assistant, content, identity, feedback, and publishing roles are never collapsed into one token.
- Guinan receives channel feedback/support trends; the channel owner remains accountable for technical resolution and user-visible status.

## OpenTelemetry and operability

- Instrument traces, metrics, and structured logs with OpenTelemetry from the walking skeleton.
- Use standard ASP.NET Core, `HttpClient`, relational database, queue/worker, and custom activity instrumentation. Export to the selected provider's managed backend; Azure Monitor/Application Insights is only the initial Azure candidate.
- Propagate correlation, causation, proposal, peer-envelope, and idempotency identifiers without recording task titles, notes, financial amounts, health details, message content, tokens, or raw user identifiers.
- Prohibit personal content and unbounded/high-cardinality identifiers in propagated baggage and metric dimensions. Permit pseudonymous tenant/principal correlation on access-controlled spans and structured logs when needed for isolation/incident response, with retention and query controls. Titles, notes, amounts, health data, prompts, tokens and connector content remain prohibited everywhere in operational telemetry.
- Add automated telemetry-redaction tests.
- Define initial SLOs and alerts for:
  - Task capture success and latency.
  - Mobile page responsiveness and Blazor reconnect success.
  - Error rate and dependency availability.
  - Reminder delivery accuracy and lateness when reminders ship.
  - Assistant response/tool success and latency when AI ships.
  - Federation delivery, proposal, and revocation latency when federation ships.
- Every SLO definition names its SLI, evidence query, evaluation window, numeric target, error budget, owner, alert path, and review cadence.
- Phase 0 sets provisional internal numeric targets after local baselines; Phase 1B revises them from managed evidence. Do not publish SLA commitments before Phase 1B evidence, support capacity and legal/business approval.
- Add sampling, retention, and daily ingestion caps to prevent telemetry cost runaway.
- Record tenant usage in a controlled metering ledger rather than unbounded high-cardinality metric labels.
- Include dashboards, alerts, trace links, deployment markers, and runbook links in each operational phase gate.

## Security engineering

- Create a threat model before the first deployed slice and update it per phase.
- Model external attackers, malicious or compromised users, hostile peers, malicious skills, prompt injection, account linking attacks, confused deputies, insider/admin access, supply-chain compromise, and telemetry leakage.
- Require a security artifact for identity, authorization, cryptography, grants, delegated tokens, federation, sandboxing, and data export/delete changes; Tuvok challenges the artifact and Cyrus approves it.
- Use secure headers, antiforgery, output encoding, CSP, rate limiting, abuse controls, secret scanning, dependency review, SAST, container/IaC scanning, signed build provenance, and SBOM generation.
- Encrypt in transit and at rest; document key ownership/rotation and when customer-managed keys become justified.
- Verify tenant isolation at every API and background-worker boundary.
- Make security failures explicit and observable without leaking sensitive details.
- Invoke the dedicated security-review specialist for any explicit vulnerability review.

## Privacy engineering

- Classify identity, family, health, finance, location, calendar, preference, message, and AI interaction data.
- Apply purpose limitation, collection minimization, least disclosure, retention schedules, hard-delete/purge, export, correction, consent withdrawal, and account/tenant deletion.
- Make sensitivity labels enforce behavior rather than serve as decorative metadata.
- Document whether each data class can be sent to each assistant provider and require informed user opt-in.
- Define data residency, subprocessors, DPA needs, privacy notices, records of processing, and data-subject request workflows before public SaaS launch.
- Define controller/processor roles for self-host versus managed deployments and a handling path for non-user data subjects whose personal data appears in another person's tenant, including contacts, group members, photo subjects, care recipients, business customers/employees/vendors and family members. Decide correction/access/erasure/objection handling before Phase 2 Relationships/Communities and before public onboarding.
- Perform privacy impact assessments for AI, federation, family sharing, connectors, and third-party skills.
- Require a privacy artifact for new data fields, telemetry changes, sharing UX, AI context, connectors, retention, and external processors; Deanna Troi challenges the artifact and Cyrus approves it.
- AI agents assist with privacy analysis but do not replace qualified privacy or legal counsel.

## Cost and FinOps

- Create `docs/cost-model.md` with per-provider SKUs/services, expected monthly fixed cost, variable unit drivers, assumptions, free-tier limits, and scale thresholds.
- Phase 0 uses no cloud provisioning, including free tiers and trial subscriptions that can convert to billable resources. Use local benchmarks, official documentation, pricing calculators and paper provider mappings. Before Phase 1B provisioning, approve a separate spike budget with alerts, quotas, TTL and automatic teardown, then define the steady-state personal-use budget.
- Budgets and alerts do not cap cloud spending. Enforce TTLs, quotas, maximum-resource policies, automatic teardown, approval boundaries, anomaly alerts, and invoice reconciliation separately.
- Require a cost delta in every architecture ADR and material feature proposal.
- Use telemetry sampling/caps, SQL auto-pause only with a cold-start UX, consumption hosting where reliable, and ephemeral test environments where cheaper than permanent staging.
- Track costs for compute, SQL, storage, telemetry, egress, model tokens, queues/functions, secrets, backups, federation traffic, audits, and CI.
- Establish per-tenant and per-capability usage metering before pricing the SaaS product.
- Quark owns the complete burn ledger: cloud and self-host reference infrastructure, domains/CDN, CI, observability, model usage, app stores, connector verification, security assessments, legal, support, tooling, contractors, and other operating expenses.
- Keep three ledgers distinct: development-time Squad/session usage, product tenant/provider metering, and business financial burn/income. They have different schemas, storage, retention, access and reconciliation; never combine raw user/product telemetry with company accounting.
- Quark publishes recurring actual-versus-budget, forecast, runway, cost per active user/tenant/capability, sponsor income, and variance explanations using aggregate financial data only.
- Every paid dependency has an owner, renewal/usage trigger, cancellation path, and lower-cost alternative where credible.
- Quark issues a documented no-go recommendation when cost is unestimated, unmeasured or outside the approved envelope; Cyrus decides whether to stop, fund, de-scope or accept the recorded risk.
- Cyrus explicitly left the initial `fleet-research` run uncapped to maximize foundational research quality. Quark records actual usage and uses it to propose later limits; no automatic cap is applied retroactively to this run.

### Initial sustainability model

- Monetization is not a near-term product gate, but architecture and operations must preserve measurable unit economics and future plan/billing boundaries.
- Begin with transparent project sponsorship/donations and optional sponsor recognition on the public site.
- Sponsors receive no personal data, targeted advertising, privileged telemetry, hidden product influence, security/privacy exceptions, or access to private planning.
- Publish a sponsorship policy covering eligibility, disclosures, recognition format, conflicts, independence, refunds/termination, and prohibited sponsors; Sarek reviews terms, Deanna Troi reviews privacy/trust, Tuvok reviews integrations, Neelix manages public communication, and Cyrus approves.
- Evaluate GitHub Sponsors, Open Collective, or direct sponsorship only after licensing, tax/accounting, payment-processing, and repository-ownership questions are reviewed.
- Treat sponsor income separately from user subscription revenue and do not assume it proves a durable SaaS business model.
- Revisit managed-host pricing, subscriptions, marketplace economics, and support tiers only after usage and cost evidence exists.

## Validation and regression strategy

- Maintain `docs/testing-matrix.md`, mapping every shipped behavior and failure path to a test identifier, level, owner, and environment.
- Establish the test pyramid in the walking skeleton:
  - Domain and policy unit tests.
  - Application command/query and authorization tests.
  - PostgreSQL plus selected managed-provider persistence conformance, integration, and migration tests.
  - Contract tests for typed APIs, assistant providers, skills, peer envelopes, and external clients.
  - bUnit component and accessibility tests.
  - Playwright scenario tests at desktop, tablet, and mobile viewports.
  - Architecture tests for dependency and data-access boundaries.
  - Load, soak, reconnect, fault-injection, and recovery tests at phase gates.
- Define explicit PR, nightly, pre-production, and authenticated live-smoke suites with separate cost/flakiness policies.
- Any test-auth replacement is compiled/configured for integration environments only and has a production-startup assertion proving it cannot activate. Deployed E2E uses a dedicated OIDC issuer/tenant and per-worker accounts; auth state files are ignored and never committed.
- Isolate and seed E2E tenant data deterministically and purge it after each run.
- Give every preview/test deployment TTL, quota, owner, and deterministic teardown/purge evidence.
- Add a tenant-isolation suite proving principal A cannot read, write, infer, enumerate, search, log, or receive notifications for principal B across every endpoint and worker.
- Add adversarial AI/federation tests proving instructions in peer or skill data cannot escape grant/tool scope.
- Add federation and skill conformance suites before third-party adoption.
- Require staged smoke tests, migration verification, rollback proof, telemetry evidence, cost check, security/privacy gates, and the scenario matrix before production promotion.

## Documentation structure

The approved implementation creates:

- `docs/plan.md`: living phased plan.
- `docs/architecture.md`: system context, containers, components, data flows, and deployment topology.
- `docs/adr/`: one decision per tenancy, identity, Blazor boundary, hosting, persistence/partitioning, assistant providers, reminders, federation, sharing, skills, observability, and cost.
- `docs/threat-model.md`: assets, trust boundaries, actors, abuse cases, mitigations, and residual risk.
- `docs/privacy.md`: classification, purposes, retention, consent, model-provider rules, and data-subject workflows.
- `docs/cost-model.md`: budgets, SKUs, estimates, unit economics, and scale thresholds.
- `docs/testing-matrix.md`: feature and failure-path regression coverage.
- `docs/federation/`: protocol goals, envelope schemas, trust, consent, versioning, and conformance.
- `docs/skills/`: manifest, capabilities, execution modes, trust, authoring, and compatibility.
- `docs/roadmap/first-party-skills.md`: visible catalog, owners, capabilities, dependencies, stage, and evidence.
- `docs/roadmap/channel-connectors.md`: visible channel catalog, provider/access status, scopes, mode, stage, and constraints.
- `docs/frameworks/skill-development.md`: charter-to-retirement lifecycle and gates.
- `docs/frameworks/channel-development.md`: provider qualification-to-retirement lifecycle and gates.
- `docs/frameworks/feedback-support.md`: channels, envelope, triage lifecycle, routing, privacy, response, and metrics.
- `docs/frameworks/prioritization-launch.md`: scorecard, portfolio lanes, launch stages, exit/stop evidence, and re-scoring.
- `docs/semantic-graph.md`: standards research, ontology/provenance/privacy model, portability, and decisions.
- `docs/runbooks/`: deployment, rollback, database recovery, identity, incident, privacy, and cost-response procedures.
- `docs/legal/license-evaluation.md`: licensing/IP investigation and counsel-reviewed decision record.
- `docs/charter.md`: ratified company mission, ethical/sustainability commitments and operating/culture principles.
- `docs/legal/regulatory-applicability.md`: Sarek's jurisdiction/capability applicability and horizon register with counsel decisions.
- `docs/business/sponsorship-policy.md`: eligibility, disclosures, recognition, conflicts, independence, termination and prohibited sponsors.
- `docs/marketplace/governance.md`: publisher/user rights, review/ranking/appeal, portability, commercial and trust rules.
- `docs/repository-migration.md`: canonical repository ownership, roadmap Project, migration checks, and operations follow-ups.

## GitHub project management

- Keep planning repository-native: Issues, milestones, labels, issue forms, pull requests, and repository automation all live in `Jamula/Andreja`.
- Use the organization-owned [Andreja Roadmap](https://github.com/orgs/Jamula/projects/2) Project for portfolio views. Repository issues remain canonical, milestones represent delivery phases, and the Project exposes only useful built-in planning fields.
- Create one roadmap/epic issue that links `docs/plan.md`, every phase milestone, open ADR decision, and the current risk register.
- Represent each delivery phase as a GitHub milestone with an explicit exit-gate issue.
- Convert the tracking todos into scoped GitHub Issues with:
  - User/problem outcome and rationale.
  - In-scope and out-of-scope boundaries.
  - Acceptance criteria and failure paths.
  - Required automated/live test evidence.
  - Architecture, security, privacy, cost, legal, and operability gates.
  - Dependencies and links to ADRs, threat/privacy artifacts, and related issues.
- Encode Status, Phase, Workstream, Type, Priority, Risk, Data Classification, Cost Impact, Squad Owner, Human Owner, and Evidence through milestones, labels, issue forms, assignees, and linked artifacts until organization Project fields are available.
- Use labels consistently for feature areas, architecture spikes, security/privacy/legal/FinOps gates, connectors, skills, federation, bugs, regressions, and Squad routing.
- Add issue forms for feature work, bugs, architecture spikes/ADRs, security/privacy review, connectors/skills, cost review, and retrospective actions.
- Require implementation pull requests to link an issue, describe acceptance/test evidence, and update issue/milestone state automatically where practical.
- Treat ADRs and `docs/plan.md` as decisions; do not duplicate or contradict them in issue comments. Proposed plan/architecture changes require an issue and reviewed documentation PR.
- Keep personal task content, profile details, tokens, connector data, and private diagnostics out of issues and project fields.
- Configure Squad to consume GitHub Issues as the work queue and write status/evidence back to the issue, avoiding a parallel markdown backlog.
- Add Quark's aggregate usage/cost findings to the relevant retrospective or FinOps issue; never post raw prompts or personal data.

## Licensing, IP, and project governance

- Treat licensing as an immediate investigation, not a casual file change and not legal advice.
- The repository is currently private and contains the Apache License 2.0 from the initial commit. Apache-2.0 grants broad perpetual and irrevocable rights to recipients of versions actually distributed under it; qualified counsel must determine current exposure and the effect of any future change before collaborators receive code.
- Audit prior recipients/distribution, repository access, commit authorship, and employer IP obligations before assuming sole relicensing authority.
- Investigate separately:
  - Copyright ownership and the limits of protecting an idea versus code, brand, protocol, and trade secrets.
  - Proprietary, source-available, dual-license, open-core, and open-source strategies.
  - Whether platform code, federation specifications, skill SDKs, examples, and documentation need different licenses.
  - Contributor License Agreement versus Developer Certificate of Origin, what each does and does not transfer, IP assignment, patent grants, and inbound/outbound license compatibility.
  - Trademark ownership and usage policy for Andreja.
  - Repository governance, branch protection, CODEOWNERS, release authority, maintainer admission/removal, and prevention of unauthorized releases.
  - Employer invention-assignment/conflict obligations and third-party dependency terms.
  - GitHub Copilot product terms, acceptable-use rules, entitlement, and redistribution/commercial conditions for a paid third-party product using end-user OAuth, BYOK, or a headless backend.
- Do not invite or merge external code contributions until the inbound contribution policy and ownership model are decided with qualified counsel.
- The open federation commitment does not require all implementation code to use the same license; the legal task must recommend a coherent interoperability and business strategy.
- Keep the repository private while licensing/trademark posture is under review. Any visibility change requires an explicit legal/governance issue and approval; do not rely on a README statement alone to change license effect.
- Complete product-name, trademark, domain and namespace clearance before publishing a public site, package/container IDs, semantic/federation namespace URIs, or external protocol/SDK artifacts. Use internal placeholders or a gated `noindex` site until cleared.
- Make GitHub branch protection, merge queue, native stacks, Projects, security features, and other controls entitlement/plan-aware; define a documented fallback when a private-repository feature is unavailable.

### Legal and regulatory charter

- Maintain a versioned applicability matrix by product capability, data class, user/account type, actor/role, transaction/action, deployment model, geography and launch stage.
- Research United States federal, state and relevant local requirements first; add international jurisdictions only when users, providers, staff or offerings create a real nexus.
- Track laws, regulations, agency guidance, enforcement, provider/app-store rules, contracts and pending changes across:
  - Privacy/data brokers/biometrics/children, cybersecurity/breach reporting, AI/automated decisions, consumer protection, accessibility, communications/marketing, records/e-signatures.
  - Health/consumer health, financial/rewards/payments/securities, insurance, employment/benefits, education/family/elder care, tax/accounting/sponsorship, marketplace/platform and intellectual property.
- For every regulated feature, document applicability hypotheses, authoritative sources, counsel questions, owner, decision, obligations, controls/evidence, deadlines, residual risk and re-review trigger.
- Sarek monitors legal changes and opens impact issues; he does not interpret law as binding advice, create attorney-client privilege, sign contracts, submit filings or approve launch without Cyrus and qualified jurisdiction-appropriate counsel.
- Separate legal research from product policy and technical controls. A legal hypothesis never silently becomes an authorization rule or public claim.
- Preserve source/date/jurisdiction and clearly distinguish enacted law, effective law, proposed legislation, guidance, contract terms and platform policy.
- Establish confidential counsel/security/privacy channels, litigation/hold and regulatory-inquiry procedures before they are needed; do not place privileged or sensitive legal material in public GitHub issues.

## Feedback and Support Framework

Guinan is the User Feedback and Support Lead and owns acknowledgment, privacy screening, deduplication, severity, reproduction quality, routing, status communication, resolution evidence, and closing the loop with the user. Feature/domain owners remain accountable for investigation and fixes.

### Feedback channels

- **Authenticated app/web feedback button:** previews category, title, description, expected/actual behavior, app/version/context, and explicitly selected diagnostics. In the personal/private-repo stage it can create a GitHub issue through a dedicated feedback grant after sanitization and user confirmation.
- **Public website feedback/support form:** accepts product, website/docs, sales/sponsorship, accessibility, and general feedback without requiring a GitHub account. It uses spam/abuse controls and enters a private triage queue before any GitHub publication.
- **Repository contributor channel:** GitHub issue forms for bugs, features, docs, architecture, security/privacy, connectors/skills, cost, and retrospective actions.
- **Security/privacy reporting:** a clearly separated private route; never require public issue disclosure for vulnerabilities or personal-data incidents.
- **Help/support channel:** searchable help pages, known issues, status, guided diagnostics, and escalation into the same triage workflow.

### Feedback record and lifecycle

- Define a typed `FeedbackEnvelope`: source channel, feedback type, user/contact preference, tenant-safe pseudonymous reference, affected surface/version, summary, expected/actual outcome, reproduction, severity/impact, consented diagnostics, privacy classification, attachments, dedupe key, and correlation/tracking ID.
- Public unauthenticated feedback enters a separately deployed tenant-less intake service/queue, never the Andreja user data plane. It has its own privacy notice, lawful-basis/consent analysis, retention clock, encryption/access controls, deletion/access request path, abuse controls, and data-flow owner.
- Default exclusions: task/file/prompt content, tokens, identities, family/health/finance details, connector payloads, raw logs, and secrets. Diagnostics are field-level opt-in with a preview.
- Lifecycle: `Received` -> `PrivacyScreened` -> `Acknowledged` -> `NeedsInformation` or `Triaged` -> `Planned`/`InProgress`/`Declined`/`Duplicate` -> `Delivered` -> `Verified` -> `Closed`.
- Guinan acknowledges receipt through the user's chosen safe channel, supplies a tracking reference, requests missing information, explains routing/priority decisions, and sends resolution/release/help links.
- While the project repository is private, an external submitter's tracking reference is opaque and status is communicated through the chosen return channel; never send an inaccessible private GitHub link as the status experience.
- Before issue creation, search for duplicates and link/comment rather than opening another issue. Public GitHub publication requires explicit consent and a final content preview.
- Route validated work into repository Issues/milestones with category, severity, privacy, phase, owner, dependency, acceptance/test evidence, and source-feedback link that reveals no personal data.
- Feedback priorities use impact, affected users, severity, security/privacy risk, strategic fit, evidence, workaround, and cost—not loudness or social reach.
- Define response/update targets, escalation, aging, closure/reopen, and user-satisfaction measures in Phase 0; do not promise targets before support capacity exists.
- Measure aggregate themes, time-to-triage/resolution, reopen rate, documentation deflection, and outcome quality without turning feedback content into unrestricted business analytics.
- Before public SaaS, use an app-owned GitHub App or dedicated feedback service/queue so customers do not need GitHub accounts and cannot spam or expose data in the repository.

## Public website, help, and support

- **Tracking:** Phase 0 design/hosting matrix [#94](https://github.com/Jamula/Andreja/issues/94);
  gated Phase 1B public/help site [#93](https://github.com/Jamula/Andreja/issues/93).
- Build a separate public-site project in this repository and .NET solution, deployed independently from the authenticated Andreja application and every user data plane.
- Launch a minimal site with the walking skeleton covering the vision, capabilities, user data ownership, self-hosted and managed choices, privacy/security posture, roadmap boundaries, and honest availability status.
- Expand the same site into the canonical help and support destination:
  - Getting started for self-hosted and managed modes.
  - Assistant, skill, connector, sharing, identity, backup/export, and troubleshooting guides.
  - Versioned product and federation/skill developer documentation.
  - Security/privacy disclosures, release notes, known issues, status/support routes, and escalation guidance.
- Include Guinan's public feedback/support entry point, tracking guidance, security/privacy reporting route, and clear explanation of what may become a GitHub issue.
- Add a transparent sponsorship page only after the sponsorship policy is approved; disclose sponsors and aggregate project support without targeted ads, tracking-based placement, or access to user data.
- Author documentation as reviewed repository content with link checking, search indexing, versioning, accessibility, and stale-page ownership. Product behavior changes are incomplete until matching help content and scenario tests are updated.
- Keep the public site product-data-free and auth-independent. It must not query task data, reuse app cookies, share user tokens, or become a privileged route into the application. Its optional feedback form posts only to the separately governed tenant-less intake service.
- Start with analytics off. Add only privacy-preserving aggregate analytics after a documented purpose, field list, retention, consent/legal basis, and opt-out review.
- Jadzia Dax leads a design-and-hosting decision matrix; Jett Reno evaluates deployment/CDN/runtime operations, Spock validates architecture and attack-surface separation, and Quark validates cost. Their reviewed artifact informs one ADR; they do not cast four independent approval votes.
- Distinguish pre-generated static hosting from server-rendered Blazor SSR, which still requires compute. Evaluate static generation, CDN/static hosting, search, documentation tooling, preview environments, custom domains, localization readiness, and portability. Prefer a low-cost static outcome when requirements permit.
- Meet WCAG 2.2 AA, publish `security.txt`, and gate public security/privacy/availability/sponsorship claims on reviewed evidence with a named owner and expiry/revalidation rule.
- Neelix joins when marketing/community content begins, after product claims have evidence and licensing/trademark decisions permit public promotion.

## Mobile client roadmap

- Keep the responsive web app as the first universal client, while treating native mobile as a distinct future client rather than wrapping server-only Blazor behavior.
- Hoshi Sato owns the mobile architecture evaluation with Jadzia Dax for UX/accessibility, Spock for contracts, Tuvok for device security, Deanna Troi for mobile privacy, Jett Reno for delivery/operations, Data for device testing, and Quark for lifecycle cost.
- Compare .NET MAUI Blazor Hybrid, native iOS/Android, and other credible approaches against:
  - Reuse of API contracts and safe presentation components.
  - Offline-first data, synchronization, conflict/proposal handling, and federation behavior.
  - Encrypted local storage, secure token/key storage, passkeys/biometrics, device loss, remote sign-out, and jailbreak/root posture.
  - Push notifications, background execution, deep links, share targets, camera/files, and calendar/contact integrations.
  - Accessibility, performance, battery/network use, telemetry/privacy, and app-store policy.
  - Self-host instance discovery, TLS/trust, managed-cloud connectivity, release cadence, and long-term maintenance cost.
- Preserve mobile readiness now through stable versioned APIs, generated clients where useful, explicit sync/version tokens, idempotency, no server-session dependency in domain workflows, and portable identity/account-linking contracts.
- Do not select MAUI or native technology until the mobile ADR and proof-of-concept measure the highest-risk flows on real iOS and Android devices.

## Business and product analytics dashboard

- Defer the dashboard until the MVP is useful and the managed/self-host product boundaries produce meaningful evidence.
- Quark and Neelix co-lead future requirements:
  - Quark owns burn, sponsor income, unit economics, resource use, cost efficiency, and forecast metrics.
  - Neelix owns adoption, onboarding, engagement, help/support effectiveness, skill/connector discovery, retention, and market-learning metrics.
- Picard ensures metrics connect to product outcomes rather than vanity; Data owns definitions and statistical/data quality; Jett Reno owns reliable telemetry pipelines; Deanna Troi owns privacy/minimization; Tuvok owns access and abuse controls.
- Maintain a versioned metric catalog with owner, definition, numerator/denominator, source, freshness, quality checks, privacy classification, retention, and decision/use case.
- Candidate views:
  - Project delivery: issue throughput, cycle/lead time, release frequency, blocked work, defect/regression escape, test health, review evidence, and AI credits/cost per successful outcome.
  - Adoption: active users/tenants, self-host/managed mix, onboarding completion, activation, retention, feature/skill/connector usage, help success, feedback themes, and satisfaction signals.
  - Resources: compute, database, storage, egress, model tokens/AI units, connector calls, telemetry volume, reliability/SLOs, cost per user/tenant/capability, and burn versus sponsor/other income.
- Never collect task titles, notes, connected-file content, prompts, family/health/finance details, or cross-tenant raw data for business analytics.
- Managed analytics uses aggregated/pseudonymous data with role-based access and minimum cohort thresholds. Self-host telemetry is off by default and requires explicit opt-in with a preview of exact fields, destination, retention, and revocation.
- Separate product/business analytics from operational observability even when they share collection infrastructure; use distinct purposes, schemas, retention, access, and consent.
- Build workflow experiments only after the metric, hypothesis, success criterion, guardrails, and rollback are documented. Never optimize engagement at the expense of user agency, privacy, or task outcomes.

## Operating model and cohesive workstreams

Andreja uses independent workstreams with one integrated product mission, shared architecture contracts, GitHub issue dependencies, artifact gates, and a protected-main target with procedural fallback when repository entitlements do not support a control. A workstream owns outcomes and evidence, not a private architecture or duplicate backlog.

### Squad guiding principles

These are Andreja-specific adaptations of Amazon's published Leadership Principles. They guide decisions and retrospectives; they are not copied corporate slogans or a substitute for product evidence.

1. **User trust and outcome obsession:** start from the user's meaningful outcome, work backward, preserve agency and earn trust continuously.
2. **Ownership:** think long term across the whole product, close loops, fix root causes and never discard a problem at a workstream boundary.
3. **Invent and simplify:** create useful mechanisms, remove accidental complexity and make the safe path the easiest path.
4. **Evidence-informed judgment:** form a clear view, seek diverse perspectives, test assumptions, distinguish fact/inference and actively look for disconfirming evidence.
5. **Learn and stay curious:** run bounded experiments, study users/providers/standards and convert learning into documented decisions.
6. **Develop the crew and ecosystem:** raise capability through charters, coaching, reusable skills, clear handoffs, documentation and external-developer enablement.
7. **Insist on durable standards:** quality, security, privacy, accessibility, operability and testability are part of done; defects are fixed so they stay fixed.
8. **Think big, slice small:** preserve the bold assistant/skills/federation vision while shipping the smallest complete vertical slice that proves value.
9. **Bias for reversible action:** move quickly on reversible, low-risk choices; escalate irreversible, consequential or trust-boundary decisions for evidence and human approval.
10. **Practice frugality:** measure burn and AI usage, reuse standards, choose simple operations and spend only when learning or user outcomes justify it.
11. **Earn trust through candor:** listen, communicate limits, admit errors, protect confidentiality, avoid inflated claims and close the feedback loop.
12. **Dive deep without losing the system:** audit source data, traces, costs, tests and user anecdotes; reconcile metric/anecdote conflicts while maintaining end-to-end context.
13. **Have backbone, then commit:** challenge respectfully with evidence before a decision; after Cyrus decides, execute cohesively and record the dissent/risk rather than relitigating silently.
14. **Deliver outcomes:** focus on critical inputs, remove blockers and ship timely, validated user value despite setbacks.
15. **Build a great place to contribute:** create a safe, inclusive, sustainable, growth-oriented team/ecosystem with humane review, clear authority and no heroics dependency.
16. **Scale responsibly:** consider effects on users, families, collaborators, providers, society and the environment; greater reach requires stronger privacy, safety, support and accountability.

**Precedence:** user agency/data ownership, security/privacy, legal/ethical obligations, accessibility and truthful evidence are non-negotiable. Bias for action, frugality, disagreement/commit and delivery never override those constraints.

Use the principles in issue/PR templates, architecture/business decisions, retrospectives, crew charters and recognition. Evaluate observable behavior and evidence, not personality conformity.

### Microsoft-inspired company culture

Microsoft's official mission emphasizes empowering every person and organization; its current careers culture emphasizes growth mindset, curiosity, continuous learning, experimentation, agility, smart risks, learning from mistakes, mentorship, collaboration and shared accountability. Its official responsible-AI approach highlights transparency, fairness, human-AI collaboration, privacy, security, safety and societal impact.

Andreja incorporates those themes as:

- **Empower people and builders:** increase user and ecosystem capability rather than dependency on Andreja.
- **Growth mindset:** treat skills and judgment as developable; coach, document, mentor and create safe learning loops.
- **Curiosity over certainty:** ask, research and experiment; make uncertainty visible and revise views when evidence changes.
- **One cohesive team:** workstreams share success, context and accountability; local optimization cannot harm the end-to-end user outcome.
- **Inclusive design:** seek varied perspectives and build accessible, culturally aware products for different resources and technical comfort.
- **Integrity and accountability:** own impact, speak candidly, admit mistakes, remediate and never hide behind the model, provider or another workstream.
- **Responsible innovation:** evaluate fairness, transparency, privacy, security, safety, human control and societal effects before scale.
- **Shared growth:** customer and builder success should expand opportunity; the platform does not win by trapping, exploiting or displacing its ecosystem.

| Workstream | Accountable lead(s) | Core scope |
|---|---|---|
| Executive, Product and Business | Picard/CEO; Quark/CFO | Vision, business model from free/self-host to freemium/premium, portfolio, launch, sponsorship, unit economics, priorities, partnerships and executive risk |
| Product Discovery and User Research | Picard, Jadzia, Guinan, Neelix | Jobs-to-be-done, dogfood, customer interviews/feedback, usability, delight criteria, roadmap evidence and adoption learning |
| Core Platform and Architecture | Spock, T'Pol, Seven | Clean/Onion modules, API/domain, identity/tenancy, semantic graph, assistant/provider, skill/channel hosts, data ownership and federation seams |
| Web, Public Site and User Experience | Jadzia, Neelix, Guinan | Blazor web app, accessible workflows, public site, help/docs, feedback/support surfaces, evidence-based product communication |
| Native Mobile and Device Experience | Hoshi | iOS/Android architecture, offline sync, secure device storage, push/background, deep links, app-store integration and lifecycle |
| Platform Operations, Hosting and FinOps | Jett Reno, Quark | Self-host package, cloud adapters, OpenTofu, CI/CD, reliability, OTel, incidents, backups/DR, cost/burn and scale evidence |
| Quality, Performance and Release | Data | Test architecture, E2E/accessibility, performance/scale, provider conformance, release evidence, regression prevention and quality metrics |
| Channels and Connectors | Jett Reno, Seven, Tuvok | Channel Framework, provider qualification, adapters, auth/scopes, email/messaging/files/photos/partner channels, support/runbooks and retirement |
| First-party Skills and Developer Ecosystem | Seven plus domain leads | Skill Framework, manifests, first-party catalog, SDK/examples/conformance, third-party trust/marketplace path and compatibility |
| Trust, Security, Privacy and Legal | Tuvok, Deanna Troi, Sarek | Threat/privacy/legal artifacts, data classification, auth/grants, abuse/safety, terms/licensing/trademark, public claims and regulatory gates |
| Customer Success, Feedback and Support | Guinan | Intake/triage, support status, user communication, help gaps, resolution verification, feedback insights and close-the-loop quality |
| Marketing, Community and Partnerships | Neelix, Picard, Quark | Positioning, Personal Brand Studio input, public/community content, sponsorship communication, partner/channel ecosystem and launch learning |
| Future Research and Innovation | Spock, Seven, rotating specialists | Semantic/AI, federation, on-device intelligence, new providers/skills/channels and bounded experiments before roadmap commitment |

### Cross-workstream contract

- One roadmap/issue source of truth, one architecture decision system, one metric catalog and one release definition. Workstreams do not maintain competing plans.
- Every initiative has one accountable owner, named contributors/review artifacts, dependencies, interface contract, acceptance evidence, cost, privacy/security classification and user outcome.
- Shared APIs/manifests/schemas are agreed interface-first. After the contract lands, implementation streams use isolated worktrees and run independently in parallel.
- Changes crossing trust boundaries, product claims, paid commitments or shared contracts trigger the relevant artifact review; ordinary local implementation does not wait for unrelated workstreams.
- Session handoffs record decision, evidence, changed contracts, remaining risk, next ready issue and owner. Guinan/Scribe preserve user/operational context without creating a second backlog.
- Integration failures are owned by a dedicated integration/revision agent, not bounced between stream owners.

### MVP mission and urgency rules

- Cyrus proves Phase 1A locally; the first managed MVP dogfood cohort is Cyrus plus a small invite-only adult group in separate tenants. The MVP mission is: **a trustworthy assistant that runs independently or in the managed reference, understands a bounded user context, invokes one useful task skill through proposals, preserves/exports/restores user data, proves tenant isolation, and closes the feedback loop.**
- Optimize for the shortest end-to-end learning path, not the most framework code. Build vertical slices that a user can operate, observe and critique.
- Protect the critical path: Phase 0 decisions -> Phase 1A self-host MVP -> dogfood. Phase 1B managed/public surfaces and later channels/skills cannot silently expand Phase 1A.
- Limit work in progress per stream, finish before starting, and parallelize only ready independent issues. Use stacked PRs for dependent slices and worktrees for independent slices.
- Research uses explicit questions, evidence and at most three revision cycles before escalation. Future ideas enter the catalog/backlog but do not interrupt the MVP unless they change a rewrite-level seam or safety boundary.
- Every merge keeps the main branch releasable, has tests/help/telemetry appropriate to the slice, and supplies evidence to the current milestone.
- "Delight" means the user achieves a valuable outcome with clarity, trust, speed, control and low friction—not feature count or engagement time.
- Speed and urgency never waive tenant isolation, user data ownership, backup/restore, explicit consent, security/privacy, accessibility or truthful product claims.

## Squad crew

Squad is already initialized, but the specialist roster and routing are empty. Keep the built-in Scribe, Ralph, Rai, and Fact Checker identities upgrade-safe. Add the following Star Trek crew members only after Cyrus approves this plan:

| Crew member | Squad role | Accountabilities | Activation |
|---|---|---|---|
| Jean-Luc Picard | CEO and Lead/Captain | Vision, strategy, product/business portfolio, company principles, launch decisions, stakeholder alignment, sponsorship/funding strategy, executive risk and cross-role decisions | Phase 0 |
| Spock | Chief Architect | Architecture, ADRs, boundaries, scale, federation coherence | Phase 0 |
| Tuvok | Security Engineer | Threat model, identity, authorization, cryptography, AppSec | Phase 0 |
| Deanna Troi | Privacy and Consent Lead | Privacy engineering, understandable consent, data minimization | Phase 0 |
| Jett Reno | Platform/SRE and Channel Platform Lead | Channel Development Framework, cloud portability, reliability, OTel, delivery, hosting evaluation, incident readiness | Phase 0 |
| Data | Quality Engineering Lead | Scenario matrix, E2E, performance, regression evidence | Phase 0 |
| Quark | CFO, FinOps and Sustainability Lead | Financial controls, burn ledger, budgets, forecast/runway, unit economics, sponsor income, per-session Copilot usage, efficiency feedback, cost gates | Phase 0 |
| T'Pol | Domain/Application Lead | .NET core, APIs, portable relational persistence, tenancy and data boundaries | Phase 1 |
| Jadzia Dax | Blazor Experience Lead | Cross-device UX, accessibility, workflow clarity | Phase 1 |
| Seven of Nine | Assistant, Skills, Semantic Graph, and Federation Lead | Skill Development Framework, provider abstraction, Copilot, skill host, personal ontology/provenance, open protocol | Phase 0 design; Phase 1-2 implementation |
| Hoshi Sato | Mobile Platform Lead | Native/mobile architecture, offline sync, secure device storage, push/background capabilities, app-store delivery | Deferred mobile phase |
| Beverly Crusher | Health and Wellbeing Lead | Health artifact requirements, clinical-safety boundaries, medication/lab/imaging workflows, wellbeing outcomes | Manual skill in Phase 4; connected health in 12 |
| Sarek | General Counsel and Regulatory Research Lead | Federal/state regulatory applicability, legal horizon scanning, licensing/IP, contracts, contributor/marketplace terms, privacy/AI/consumer/industry issues and counsel coordination | Phase 0 and every regulated gate |
| Neelix | Marketing/Community and Personal Brand Advisor | Personal Brand Studio requirements, positioning, product story, adoption, ecosystem community, transparent sponsorship communication | Brand dogfood and evidence-ready public communication |
| Guinan | User Feedback and Support Lead | Intake, privacy screening, dedupe, severity, routing, user communication, resolution verification, feedback insights | Phase 0 framework; Phase 1 channels |

Cyrus remains the human founder/product owner and sole legal decision-maker unless formal governance changes. CEO/CFO and other crew titles describe accountable advisory/operating roles for Squad; they do not grant agents authority to sign contracts, spend funds, make legal representations, or publish without human approval.

Star Trek character names are internal/private Squad codenames pending trademark/licensing review. Any repository visibility change re-reviews committed codename usage. Do not use them in public product branding, marketplace roles, endorsements or commercial claims without counsel approval.

### Business leadership responsibilities

- **Picard/CEO:** maintain mission, strategy, product portfolio, launch readiness, company principles/culture, customer trust, competitive/market learning, public claims, partnership/sponsorship strategy, organizational design, executive risk register and cross-functional decisions. He proposes priorities and capital allocation with Quark for Cyrus's approval.
- **Quark/CFO:** maintain financial controls, complete burn/income ledger, budgets, forecasts/runway, unit economics, provider/vendor commitments, sponsorship receipts, invoice reconciliation, tax/accounting questions, cost scenarios and financial risk. He challenges unfunded scope and reports tradeoffs to Picard and Cyrus.
- **Sarek/General Counsel:** maintain the legal/regulatory issue register, identify applicable federal/state/local and later international requirements, translate product proposals into questions for qualified counsel, track obligations/deadlines/terms, preserve privilege boundaries and block unsupported legal/public claims.
- Picard and Quark co-own the business case, scorecard weights and stage-gate recommendation; neither can bypass Tuvok/Deanna/Sarek/Data evidence or Cyrus's final decision.

### Review gates

- Gates are artifact-based, not agent-vote ceremonies. The required ADR, threat/privacy entry, cost delta, test evidence, or runbook must exist and Cyrus gives human approval.
- Spock, Tuvok, Deanna Troi, Jett Reno, Quark, Data, and other specialists produce or challenge the relevant artifact; they do not all need to run for every change.
- Activate a crew member only when that specialist's first required artifact is due.
- Batch related ADRs/artifacts into themed review packets with a decision checklist and at most three evidence/revision cycles. Unresolved items become explicit `needs-decision` issues; silence is never approval and unrelated packets do not block each other.
- AI safety: Rai review.
- Claims and external facts: Fact Checker review.
- Legal and privacy conclusions requiring professional judgment remain human/counsel decisions.
- Before casting, reproduce and diagnose the npm 12 `EALLOWREMOTE` failure hypothesis involving a transitive `isexe` tarball through the configured Microsoft/Azure feed; fix or pin the trusted registry/package path rather than granting `allow-remote=all`. Restrict Squad MCP tools to required capabilities instead of `"*"`. Then extend `.squad/casting/policy.json` with an approved Star Trek universe/capacity, add the Star Trek casting roster, correct the project identity to Andreja, remove machine-specific paths/state, and re-run `squad doctor`.

### Parallel worktree execution

- Andreja intentionally uses `main` as its only integration/release branch. Target native branch protection/merge queue when current GitHub entitlement supports them; otherwise enforce reviewed PRs, status checks, no direct pushes and serialized coordinator merges procedurally. Project directives override Squad's generic dev-first template.
- Every implementation issue gets:
  - A `squad/{issue-number}-{kebab-case-slug}` branch from current `origin/main`.
  - A sibling isolated worktree.
  - One accountable owning agent.
  - An early draft pull request targeting `main`.
  - Issue-linked acceptance criteria, test evidence, and required artifact gates.
- When two or more ready issues are independent, Squad should fan them out concurrently into separate worktrees by default.
- Parallelize only when file/module ownership and contracts are independent. For shared interfaces, land or agree the contract first; express dependencies in GitHub Issues and the todo graph.
- Prefer a protected-main merge queue with required CI/security/architecture checks, `merge_group` workflow coverage, up-to-date branch requirements, CODEOWNERS/artifact gates and deterministic asynchronous merge policy. If unavailable, use documented equivalent procedural controls and ordinary reviewed PRs.
- Agents never switch the branch of another worktree, edit another agent's files, or share mutable build/output directories.
- The coordinator owns cross-agent interface decisions, conflict detection, sequencing, and integration. A dedicated integration/revision owner resolves rejected or conflicting work; the original author follows reviewer-lockout rules.
- Runtime Squad state uses the configured state backend. Worktrees commit only authoritative project configuration and issue changes; agents do not rewrite append-only or state-backend-owned files.
- After merge, remove the worktree, prune metadata, delete the issue branch, update the issue/milestone, and record aggregate usage/validation evidence.
- Limit concurrency dynamically using dependency readiness, test/CI capacity, overlapping ownership, Quark's measured efficiency, and human review capacity rather than a fixed fleet size.

### Native stacked pull requests

- Use GitHub native PR stacks, currently a preview/API-version dependency, as the preferred option for small dependent changes when available. Do not force independent or trivial work into a stack.
- The bottom PR targets `main`; every higher PR targets the branch immediately below it.
- Each layer remains one GitHub Issue, one app session, one branch, one isolated worktree, one accountable agent, and one ordinary app-created PR.
- Create layers sequentially from bottom to top. A higher worktree is created only after its parent layer has committed and pushed so the base snapshot is stable. Other independent stacks/issues may still run in parallel.
- After every intended layer PR exists and preflight passes, register bottom-to-top membership with GitHub's Stacks REST API. The API records stack metadata only; app-native flows continue to own branches, commits, pushes, PRs, review, and tests.
- Before registration or append, verify every PR is open/unmerged, in the base repository rather than a fork, not queued/auto-merging, and has the exact base/head chain and unchanged SHAs.
- Use native stack navigation, final-base checks/protection, server-side rebase, and cascading merge where available. Preserve stack identity after partial merges and append new work only above the verified top.
- Rebase or sync inside each layer's owning session. Never force-push another active worktree's branch.
- Remove, insert below the top, or reorder layers only through an explicit guarded restack decision with recovery data; normal synchronization does not rebuild metadata.
- If native registration/API version/entitlement is unavailable or a layer is cross-fork, keep an ordinary dependent-PR chain and do not fake stack metadata.
- Add stack membership/order, final base, dependency links, and validation state to issue/PR templates and Squad routing instructions.

## Tool, skill, and agent readiness

### Available now

- .NET SDK 10.0.301, Node.js 22.23.2, npm 12.0.1, GitHub CLI 2.96.0, and GitHub Copilot CLI 1.0.80 are installed.
- `GitHub.Copilot.SDK` 1.0.11 is available from the configured NuGet feed, and
  GitHub announced the SDK as generally available on 2026-06-02. Official
  documentation describes OAuth, per-user subscriptions, organization
  server-to-server authentication, BYOK, headless backends, and multiple
  isolation patterns. Documentation and package availability establish
  qualification inputs, not Andreja entitlement, commercial permission,
  effective isolation, retention/residency, support fitness, or activation.
- The repository has Squad scaffolding and built-in Scribe, Ralph, Rai, and Fact Checker agents.
- Built-in specialist agents cover architecture critique/rubber-duck review, code review, security review, research, exploration, test/build execution, and general implementation.
- Existing Squad templates include architectural proposals, CI validation gates, E2E template testing, distributed-mesh concepts, economy mode, secret handling, reviewer protocol, and test discipline.
- Azure, GitHub, filesystem, shell, web research, Context7, SQL tracking, browser/UI automation, and Canvas tooling are available.

### Candidate specialist resources found, not installed

Agent Finder returned these relevant resources. Scores are relevance scores, not trust or safety ratings:

| Resource | Type | Relevance |
|---|---|---|
| Cloud Solution Architect | AI skill | 80 |
| Azure Architecture Autopilot | AI skill | 70 |
| Azure Well Architected Review | AI skill | 50 |
| Azure MCP | MCP server | 45 |
| GDPR Compliant | AI skill | 75 |
| Security Threat Model | AI skill | 85 |
| Az Cost Optimize | AI skill | 85 |
| Azure Pricing | AI skill | 50 |
| Webapp Testing / Playwright | AI skill/plugin | 80 |
| ASP.NET Core | AI skill | 60 |
| Legal | Copilot plugin | 70 |
| GTM positioning/product-led growth | AI skills | 70 |

Before installation, review source/publisher provenance, immutable digest/version, license, permissions/capabilities, outbound endpoints, data retention, telemetry/content capture, prompt-injection surface, update/revocation path, maintenance, overlap and isolated-trial evidence. The recommended initial evaluation set is Cloud Solution Architect, Security Threat Model, GDPR Compliant, Az Cost Optimize, ASP.NET Core, and a Playwright/Webapp Testing skill. Legal and Marketing resources remain deferred. No discovered resource substitutes for counsel, a penetration test, privacy review, or human architecture accountability.

## Investigation ledger

### Completed for planning

- Repository baseline, private visibility, Apache-2.0 file, and license commit history inspected.
- .NET, Node, npm, GitHub CLI, and Copilot CLI prerequisites verified.
- Working-profile document extracted and translated into product principles.
- Initial personal scope, trading boundary, adults-only boundary, responsive Blazor direction, open-federation commitment, self-host independence, connector mode choice, third-party skill isolation, Star Trek crew, repo-native tracking, public-site separation, help-site consolidation, and Hoshi-led future mobile role selected.
- Squad scaffold and empty specialist roster/routing placeholders inspected.
- `squad-state` verified as the intended orphan mutable-state branch containing decisions and agent histories; it is not merged into main. Authoritative static config is reviewed separately in the Squad PR.
- Official Copilot SDK backend/OAuth documentation reviewed; stable NuGet 1.0.11 located.
- Agent Finder searches completed for architecture, ASP.NET, security, privacy, FinOps, testing, Legal, and Marketing candidates.
- Five rubber-duck passes, including federation/skills, open-thread ratifiability, catalog/framework/feedback review and final post-closure ratification review, completed; latest verdict is RATIFIABLE after the applied Phase 0 envelope wording fix.
- Two uncapped `fleet-research` reviews completed with primary-source-backed architecture, identity, assistant, connector, security/privacy, testing/observability, FinOps, licensing, website/mobile and expanded regulated-skill findings; confirmed corrections are incorporated in this revision.
- Current implementation direction reconciled: built-in passkey/local self-host identity, PostgreSQL reference persistence without permanent managed-database lock-in, artifact-based gates, and Phase 1A deterministic fake plus OpenAI-compatible BYOK are implemented or represented in the current plan. Proposed ADR 0009 recommends a credential-free, non-shipping Copilot SDK compile/conformance toolchain spike in 1A and a limited real provider no earlier than gated Phase 1B. Neither the proposal nor implementation records Cyrus acceptance or accepts the Proposed ADR packet.
- Initial first-party skill and connector catalogs, Personal Brand Studio guardrails, portfolio lanes, and launch-stage framework are explicitly documented.

### Partial or provisional

- Squad bootstrap validation: `squad doctor` was attempted but npm blocked a transitive remote package with `EALLOWREMOTE`; diagnose the configured npm feed/policy before treating the scaffold as validated.
- Working-profile defaults: principles are known, but user-approved preference fields/default values still need a settings-schema review.
- WhatsApp API limitations: directionally researched and conservatively deferred, but verify against official Meta documentation before connector work.
- Copilot SDK qualification and Phase 1B operational fit: before runtime, “SDK
  integrated into the toolchain” means only a pinned isolated development/test
  project that compiles a conformance adapter against synthetic fixtures,
  checks schema/version/dependency drift, and does not ship, authenticate, start
  a runtime, make network/model calls, provision an account, or incur usage.
  Before a limited real provider, Andreja still needs every Proposed ADR 0009
  entitlement, isolation, exposure, retention/residency, auth, cost, consent,
  audit, offline, test, legal/privacy/security, operations, and rollback gate.
- External resource readiness: discovery is complete; source, publisher, permission, maintenance, license, and data-handling trust reviews are not.
- Testing strategy: layers and scenarios are defined; test-auth impossibility, environment topology, thresholds, and budget require Phase 0 decisions.
- Earlier capped `fleet-research` attempts exhausted credits/subagent limits without an artifact. Preserve them as Quark failure-cost evidence; the later uncapped run succeeded.
- Initial factory research burn: 896.87 AI credits total—820.25 from two successful fleet runs and 76.62 from failed/halted attempts. AI credits are not currency; Quark reconciles them separately with provider billing and uses the failed-run ratio to improve future orchestration.
- Existing worktree/PR: 165 generated Squad files remain uncommitted, include machine/worktree-specific values, and are intentionally excluded from the future plan/ADR PR. The earlier PR request remains pending.

### Open Phase 0 investigations

- License/IP, employer obligations, contribution terms, trademark, governance, and counsel-reviewed decision.
- Clean/Onion module boundaries and ports/adapters are implemented and architecture-tested; ADR acceptance and the final inventory remain open under #66.
- PostgreSQL reference persistence, migrations, and local conformance are implemented; a managed database choice and provider migration/managed cost evidence remain Phase 1B decisions.
- OCI runtime target and Kubernetes adoption triggers; runtime choice intentionally deferred.
- Local performance/operability benchmarks plus Azure/AWS/GCP documentation, pricing and OpenTofu paper mappings; all provisioned measurement moves to separately budgeted Phase 1B.
- Built-in ASP.NET Core Identity/passkeys and local recovery flows are implemented; encrypted PostgreSQL-plus-Data-Protection-key clean restore with sign-in, genuine approved update/rollback, final support boundary, linked providers, and any optional control plane remain open.
- CIAM provider/migration path and pricing.
- Numeric budget, unit-cost model, Copilot token budgets, and numeric SLOs.
- Canonical `docs/threat-model.md` and `docs/privacy.md` descriptive baselines are present but not ratified; Tuvok/Deanna Troi/Rai challenge, Cyrus residual-risk acceptance, classification/impact assessment, numeric retention, residency, production export/purge, and model-provider decisions remain open.
- Federation standards comparison, identity/trust/discovery, versioning, reference/copy semantics, and conformance scope.
- Skill/channel manifest, host, capability, grant, peer-envelope, and minimal semantic contracts are implemented; UI isolation, signing, third-party execution, and authoring compatibility remain open.
- OneDrive, Google Drive, GitHub, and Box API scopes, terms, limits, delta/webhook behavior, and connector security/privacy.
- Product packaging and economics for independent self-hosting versus managed freemium/paid data hosting.
- The Phase 0 website matrix, claims inventory, local prototype, and Proposed ADR 0008 are complete as recommendation evidence; unintended GitHub Pages publication was contained under #114. Acceptance, any future hosting/CDN/DNS, evidence-based public claims, domain/trademark readiness, and publication remain open.
- Native mobile architecture proof-of-concept, offline synchronization, device security, push/background behavior, and app-store lifecycle cost.
- Minimal semantic-profile/provenance/export contracts are implemented; Seven-led standards, ontology, privacy, representative-journey, storage, and ADR decisions remain open.
- Phase 0 prioritization score weights and stage-specific Must/Should/Could/Won't scope.
- Personal Brand Studio evidence model and official LinkedIn/Facebook/Instagram API/account-type access before any publishing capability.
- Gmail/Graph/Discord/WhatsApp delivery-topology, scope, verification, relay/polling, deliverability, cost and support ADRs.
- Company charter/culture, federal/state legal applicability, sustainability, sponsorship and builder-marketplace governance.
- Consumer-health manual-mode gate, financial/rewards/employer/insurance action eligibility, launch jurisdictions and professional/counsel boundaries.

### Primary research references

- GitHub Copilot SDK multi-tenancy: <https://docs.github.com/en/copilot/how-tos/copilot-sdk/setup/multi-tenancy>
- GitHub Copilot session persistence: <https://docs.github.com/en/copilot/how-tos/copilot-sdk/features/session-persistence>
- GitHub Copilot usage and billing: <https://docs.github.com/en/copilot/how-tos/copilot-sdk/features/usage-and-billing>
- GitHub Copilot SDK NuGet package: <https://www.nuget.org/packages/GitHub.Copilot.SDK/>
- OAuth security best current practice (RFC 9700): <https://www.rfc-editor.org/rfc/rfc9700.html>
- PKCE (RFC 7636): <https://www.rfc-editor.org/rfc/rfc7636.html>
- OAuth 2.1 draft status: <https://datatracker.ietf.org/doc/draft-ietf-oauth-v2-1/>
- OCI specification scope: <https://opencontainers.org/about/overview/>
- Blazor Server hosting/scale considerations: <https://learn.microsoft.com/en-us/aspnet/core/blazor/host-and-deploy/server/?view=aspnetcore-10.0>
- EF Core multiple-provider migrations: <https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/providers>
- PostgreSQL logical dump/restore: <https://www.postgresql.org/docs/current/backup-dump.html>
- Azure PostgreSQL operational backup/PITR: <https://learn.microsoft.com/en-us/azure/postgresql/backup-restore/concepts-backup-restore>
- OpenTofu sensitive state: <https://opentofu.org/docs/language/state/sensitive-data/>
- Azure budget behavior: <https://learn.microsoft.com/en-us/azure/cost-management-billing/costs/tutorial-acm-create-budgets>
- Amazon Leadership Principles: <https://www.amazon.jobs/content/en/our-workplace/leadership-principles>
- Microsoft mission/about: <https://www.microsoft.com/en-us/about>
- Microsoft careers culture/growth mindset: <https://careers.microsoft.com/v2/global/en/culture>
- Microsoft AI principles and approach: <https://www.microsoft.com/en-us/ai/principles-and-approach>
- ASP.NET Core Identity passkeys: <https://learn.microsoft.com/en-us/aspnet/core/security/authentication/passkeys/?view=aspnetcore-10.0>
- Gmail push notifications/watch renewal: <https://developers.google.com/gmail/api/guides/push>
- Microsoft Graph Outlook change notifications: <https://learn.microsoft.com/en-us/graph/outlook-change-notifications-overview>
- Discord Gateway and interaction intents: <https://discord.com/developers/docs/events/gateway>
- WhatsApp Business Platform overview: <https://developers.facebook.com/documentation/business-messaging/whatsapp/about-the-platform>
- Google Photos Picker: <https://developers.google.com/photos/picker/guides/get-started-picker>
- Google Photos API policy: <https://developers.google.com/photos/support/api-policy>
- HHS health-app privacy guidance: <https://www.hhs.gov/hipaa/for-professionals/special-topics/health-apps/>
- FTC Health Breach Notification Rule guidance: <https://www.ftc.gov/business-guidance/resources/health-breach-notification-rule>
- MCP security best practices: <https://modelcontextprotocol.io/docs/2025-11-25/tutorials/security/security_best_practices.md>
- GitHub stacked pull requests preview: <https://docs.github.com/en/pull-requests/get-started/about-stacked-prs>
- GitHub Copilot SDK scaling: <https://docs.github.com/en/copilot/how-tos/copilot-sdk/setup/scaling>

## Roadmap prioritization and launch framework

### Portfolio lanes

- Core assistant and user experience.
- User-owned data, identity, privacy, security, backup, and portability.
- First-party skills and personal semantic graph.
- Connectors and ingestion.
- Federation, sharing, and third-party ecosystem.
- Public site, help/support, personal brand, community, and growth.
- Platform reliability, testing, observability, FinOps, and business operations.

### Issue scorecard

Every roadmap issue records evidence and a 0-5 assessment for:

- User outcome/value and severity of the open loop solved.
- Strategic fit with assistant/skill differentiation and data ownership.
- Reach across target users, deployment modes, skills, or connectors.
- Learning value and uncertainty reduced.
- Confidence/evidence quality.
- Risk reduction for security, privacy, legal, reliability, portability, or cost.
- Dependency readiness and ability to ship a complete vertical slice.
- Implementation effort, ongoing operating/support cost, technical complexity, and reversibility.
- Privacy/security exposure and potential user-harm cost.

Picard and Quark maintain a documented weighting formula after dogfood evidence exists. Scores order eligible work; they never override hard security/privacy/legal/data-ownership gates or issue dependencies. Re-score when evidence, cost, or provider access changes.

Use Must/Should/Could/Won't scope per launch stage, with explicit kill/de-scope criteria. Prefer the smallest vertical slice that proves a user outcome, architecture seam, or market assumption over broad horizontal framework work.

### Launch stages and evidence gates

1. **Architecture/Research:** Ratified Phase 0 scope, ADRs, threat/privacy/cost/test artifacts, no public product claims.
2. **Cyrus self-host technical dogfood:** Phase 1A assistant/task/data-ownership/recovery evidence before exposing a managed service.
3. **Small managed invite dogfood:** Phase 1B adult invitees in separate tenants, repeatable onboarding/recovery, tenant isolation, feedback/support, public help and measured cost/usage.
4. **Invite-only adult alpha:** Bounded connector/skill pilots, privacy/security review, support runbooks and no minor accounts.
5. **Private beta:** Stable self-host/managed upgrades, export/delete, SLO evidence, cost/unit economics, first useful channel set, help/support coverage, incident and rollback practice.
6. **Public beta:** Counsel-reviewed license/terms/privacy posture, evidence-controlled public claims, abuse/support operations, sponsor policy, capacity/cost guardrails, and published limitations.
7. **General availability:** Proven reliability/recovery, security/privacy gates, support and lifecycle policy, transparent pricing/hosting terms if offered, upgrade compatibility, and sustainable operations.

- Each stage has a GitHub milestone, exit-gate issue, metric/evidence links, known-risk list, and explicit decision to proceed, extend learning, de-scope, or stop.
- Personal Brand Studio can dogfood as a draft-only skill; connector-based publishing and social brand expansion require provider access, authenticity/privacy controls, evidence, and later launch gates.

## Phased execution

### Program stop and de-scope rules

- Every phase has an approved cost/credit envelope, explicit exit evidence, and a bounded spike allowance.
- If a phase exceeds its envelope or cannot prove an exit condition after the approved spike cycles, stop implementation and open a decision issue rather than silently expanding scope.
- Walking-skeleton de-scope order: defer Phase 1B managed deployment, then public site/feedback surfaces, then second external provider integrations, then inactive federation/skill persistence, then noncritical UI polish. Do not cut the Phase 1A independent self-host path, tenant/access boundaries, assistant-provider seam, telemetry redaction, backup/restore, or minimum end-to-end evidence.
- Later phases may be reordered only through an issue and ADR/plan update that preserves dependencies and user-data guarantees.

### Phase 0 - Govern and decide

Deliverables:

- Memorialize the ratified plan through a documentation pull request, roadmap issue, phase milestones, scoped repository issues, labels, issue forms, and automation; document the later organization Project migration.
- ADR 0000 records the Phase 0 cloud-infrastructure cap of $0 and the no-provisioning rule—no cloud accounts, subscriptions, free tiers or trials—plus separate AI-credit/professional-services envelopes, approved uncapped `fleet-research` exception, maximum review cycles, owners, stop conditions and amendment policy. Any Phase 1B cloud spike requires a new approved budget decision.
- Complete product-name/trademark/domain/namespace clearance before public or externally stable artifacts; complete license/IP tracking enough to define external-contribution rules and obtain counsel before a final license change or collaborator intake.
- Domain registration and counsel/trademark fees are outside the $0 cloud-infrastructure cap but require their own explicit professional-services budget approval before purchase.
- Cast the approved Squad crew and replace routing placeholders.
- Repair and re-run `squad doctor`; establish Quark's automatic session-close usage ledger and retrospective report.
- Create documentation skeleton, architecture diagrams, ADR template, and risk register.
- ADRs for Clean/Onion boundaries, modular slices, tenancy, identity/CIAM path, API-first Blazor boundary, self-host/control-plane separation, persistence portability, assistant providers, reminders, open federation, grants/consent, connectors, skills, and versioning.
- Seven-led personal semantic graph research: representative journeys, standards/ontology matrix, provenance/inference/privacy model, minimal schema, portability format, and smallest viable storage/projection ADR.
- Ratify the initial first-party skill catalog, connector release bands, issue scorecard fields, launch stages, and stage-specific Must/Should/Could/Won't scope.
- Ratify workstream charters, owners, shared contracts, WIP/integration/handoff rules and the protected Phase 0 -> Phase 1A MVP critical path.
- Ratify the Andreja company charter, Amazon-inspired operating principles, Microsoft-inspired culture, Customer Zero doctrine, ethical/sustainability indicators and confidential accountability mechanisms.
- Sarek reviews attribution/trademark posture before adapted Amazon/Microsoft-inspired principles appear on a public culture page.
- Ratify Sarek's legal/regulatory charter and initial federal/state applicability/horizon matrix before regulated feature research or public legal/compliance claims.
- Ratify the Channel Development Framework, Skill Development Framework, and Guinan-owned Feedback and Support Framework, including manifests, templates, lifecycle states, privacy defaults, routing, and stage gates.
- Assign Jett Reno as Channel Framework owner, Seven as Skill Framework owner, Guinan as Feedback Framework owner, and Picard/Quark as Prioritization/Launch Framework owners.
- Personal Brand Studio requirements/guardrails and official API access research for GitHub, LinkedIn, Facebook, and Instagram, with draft/export fallbacks.
- Qualify Gmail, Outlook.com/Hotmail, Microsoft 365, transactional outbound email/notification, Discord, WhatsApp user-share/export, and official WhatsApp Business options through the Channel Development Framework, including deliverability, SPF/DKIM/DMARC, provider terms, scopes, verification, cost, abuse and support.
- Define the tenant-less public feedback intake ADR: separate deployment/storage, privacy notice, retention/deletion/access, spam/abuse controls, GitHub publication consent, and safe user status responses.
- Approve and record the transparent sponsorship policy and initial business burn controls in private repository documentation; public posting waits for Phase 1B site/name clearance, and accepting sponsorship remains separately gated by legal/tax/payment review.
- Local/paper architecture spike covering managed database options, OCI artifact/runtime candidates, WebSockets, scale-out affinity, reconnects, rolling revisions, circuit memory, headless assistant runtime/process limits/isolation/storage, cold starts, portability and modeled cost. Use local containers/benchmarks plus Azure/AWS/GCP documentation/pricing mappings; no cloud provisioning in Phase 0, including free tiers and trials.
- Self-host identity qualification for HTTPS/WebAuthn RP configuration, first-admin bootstrap, recovery, external identity collision/linking, Data Protection key backup, and restoration.
- OpenTofu state/provider design and local-backend validation with pinned providers/lock files, remote-state encryption/locking/recovery design, CI workload-identity design, deployment TTLs/quotas and teardown. Provisioning remote state and CI identity moves to separately budgeted Phase 1B.
- Copilot event-schema and billing-reconciliation spike covering usage events, AI credits, session content persistence, provider retention, credential/tool isolation, and invoice-grade limits.
- Jadzia-led public website design/hosting matrix with Jett Reno, Spock, and Quark; cover static/SSR options, help/search tooling, CDN/hosting, portability, security separation, preview environments, and cost. Tracked by [#94](https://github.com/Jamula/Andreja/issues/94).
- Keep all Phase 0 website review on loopback or behind private access control. GitHub Pages, public previews, CDN delivery, and DNS publication remain disabled. Only a separately approved private preview may use authorization and expiry; `noindex` never substitutes for authorization. The historical public `main:/docs` GitHub Pages nonconformance was contained under #114 and is not evidence of an approved launch.
- Initial threat model, privacy classification, numeric spike/steady-state cost envelopes, numeric SLO definitions with owners/evidence queries, and test strategy.
- Evaluate but do not automatically install the recommended external skills/tools.

Exit gate:

- Every artifact required to enter Phase 1A exists and has been challenged; Cyrus approves that decision set and residual risks. Remaining Phase 0 research packets retain their own stated gates and do not block Phase 1A unless they change a Phase 1A trust boundary or invariant.
- No external contributions are accepted without an approved inbound contribution policy.

### Phase 1A - Self-hosted assistant walking skeleton

Deliverables:

- Pinned .NET 10 solution with Clean/Onion dependency rules, modular vertical slices, hexagonal adapters, DI, validated Options, typed clients, analyzers, nullable, warnings as errors, central package management, and architecture tests.
- One authenticated-app OCI artifact and immutable-digest local self-host bundle with PostgreSQL, HTTPS/passkey bootstrap/recovery, recoverable Data Protection keys, health, and local OTLP/OpenTelemetry content suppression.
- Internal tenant/user/external-identity/membership/principal/contact foundations, access-scoped projections, policy evaluator, and two-tenant isolation tests.
- Minimal semantic-profile/provenance contracts and export shape, without graph database or bitemporal assumptions.
- Skill and channel manifest/host contracts, plus grant/consent/share-audit and peer-envelope contracts/tests without inactive persistence migrations.
- Provider-neutral assistant shell with an Andreja-native OpenAI-compatible BYOK provider and deterministic fake. A Copilot SDK compile/conformance spike may exist only in the non-shipping, credential-free pre-runtime toolchain boundary defined by Proposed ADR 0009; it is not a Phase 1A provider.
- The Phase 1A BYOK path may use a local OpenAI-compatible runtime; any external model/API spend requires an explicit Phase 1A model-spend envelope before use.
- Responsive Blazor/API vertical slice where the assistant invokes Open Loops and Tasks through `ISkillHost` to propose, confirm, list, complete, export and delete a task.
- Encrypted PostgreSQL logical dump/restore plus versioned portable Andreja application export/import into a clean instance, both distinct from provider PITR.
- Unit, architecture, integration, permission-negative, backup/restore, restart, provider-failure, mobile-viewport Playwright, telemetry-redaction and clean-install smoke evidence.

Exit gate checklist:

1. Start fully independently without GitHub or Andreja cloud.
2. Complete passkey first-admin bootstrap and tested recovery.
3. Use BYOK assistant -> task skill -> confirmed proposal -> persisted result.
4. Prove tenant isolation and no personal content in operational telemetry.
5. Restore data and identity keys into a clean instance.
6. Restart/revise the app without losing data, identity, or configuration.

### Phase 1B - Managed reference and public surfaces

Deliverables:

- After a separate budget approval, one capped Azure managed reference deployment using the Phase 0-selected relational provider—PostgreSQL is the provisional reference—through OpenTofu with pinned providers, locked/encrypted state, CI workload identity, quotas, TTL/teardown, secrets, backups, health, OTel export, and cost evidence.
- After Proposed ADR 0009 is accepted and every entry gate has current approval
  and evidence, a limited real Copilot 1.0.x provider canary using synthetic
  content and one dedicated non-user test identity. A consenting adult identity
  is allowed only after that synthetic canary completes every required live gate.
  Start with per-user runtime isolation. Use a shared runtime only after adversarial evidence proves
  filesystem/session/tool/credential/concurrency/cleanup/cost isolation and
  Cyrus accepts the residual risk. Keep separate credentials/tools/session
  state, usage reconciliation, hard budgets, provider pause/kill, and an optional
  external runtime image only if the measured topology requires it. SDK-to-CLI
  traffic stays on loopback/isolated sidecar or uses mutually authenticated
  encryption plus strict workload/network policy; the headless RPC listener is
  never public. Otherwise defer Copilot and retain fake/BYOK.
- Invitees may link their own BYOK provider. Copilot remains restricted to the
  one-identity ADR 0009 canary until a separate expansion decision repeats the
  provider gates for the cohort. Only after Sarek's counsel/vendor-reviewed
  answer on Copilot entitlement, acceptable use and redistribution may an
  approved canary identity opt into an Andreja-funded dogfood allowance. Funded
  usage has explicit consent, a separately approved product-provider/model
  envelope, per-tenant quotas, model/tool limits, attribution, budget exhaustion
  behavior and no hidden transfer of one user's entitlement to another.
- Independently deployed pre-generated public-site artifact, conditional on product-name/domain clearance, covering vision, data ownership, privacy posture, help/support and evidence-based availability. Tracked by [#93](https://github.com/Jamula/Andreja/issues/93).
- Tenant-less public feedback intake plus authenticated in-app feedback, Guinan triage/status workflow, and sanitized/consented GitHub issue publication.
- Minimal transactional outbound email for Guinan acknowledgments/status, with sender identity, SPF/DKIM/DMARC, bounce/complaint handling, abuse controls and cost evidence. Phase 3A expands email intake, reminders and provider choice.
- Invite-only adult onboarding for a small dogfood cohort, with each invitee in a separate tenant, repeatable recovery and explicit support/feedback consent. Cross-tenant access is limited to the Group Travel workspace contract below.
- MVP Email Triage across Gmail and Outlook.com/Microsoft 365: per-account policies, dry-run historical relationship proposals, autonomous classification/tasks/permitted calendar sync/quarantined deletion, adaptive rules, full control-plane audit/undo and success metrics.
- A narrow managed cross-tenant Group Travel slice: host-owned trip workspace, bilateral participant grants, minimum-disclosure projections, proposals/voting, in-app collaboration, audit/revocation and no general resource sharing.
- Dedicated deployed-test OIDC/tenant, deterministic cleanup, managed/self-host conformance, WebSocket/affinity/reconnect/cold-start/revision tests, provider switching, invoice reconciliation and smoke evidence.

Exit gate checklist:

1. Use the managed deployment from a phone through a dedicated test identity.
2. If and only if ADR 0009 and every provider gate are accepted, complete the
   same assistant/skill task scenario with the limited Copilot canary and switch
   between Copilot, deterministic fake, and BYOK. Otherwise run that same scenario
   on deterministic fake and BYOK, demonstrate switching between those providers,
   and record Copilot as deferred without weakening offline behavior.
3. Inspect trace and Quark reconciled usage/cost evidence without content leakage.
4. Verify WebSocket reconnect, rolling revision, cold-start and circuit behavior.
5. Submit public and in-app feedback, receive an opaque tracking update, and verify privacy/abuse controls.
6. Onboard the invite cohort, prove each tenant cannot read/write/infer/enumerate another tenant's data, and complete deterministic tenant cleanup.
7. For every activated provider, including BYOK at minimum, demonstrate the
   approved funding mode, quota enforcement, attribution/reconciliation, and
   budget-stop behavior. Record each non-activated funding mode as deferred,
   never “not applicable”; Copilot deferral does not waive fake/BYOK evidence.
8. Demonstrate managed backup/PITR, PostgreSQL logical restore, cloud-neutral Andreja export/import and teardown within the approved spike envelope.
9. Pass all six Email Triage MVP acceptance scenarios for Gmail plus Outlook.com or Microsoft 365.
10. Pass all five Group Travel MVP acceptance scenarios across three separate dogfood tenants.

### Phase 2 - Assistant and core first-party skills

Deliverables:

- Production-quality assistant sessions, provider selection/BYOK, metering/budgets, streaming, context controls, proposal confirmation, audit, and failure handling.
- Task/open-loop skills for Inbox, Today, Upcoming, Waiting For, completed, search, edit, areas, tags, contacts, intended outcome, source, and periodic review.
- Calendar, Finance, and Interests skill packages through the same registry/host; Preferences exposed through declared settings.
- Draft-only Personal Brand Studio dogfood for user-approved professional persona, evidence-backed claims, GitHub/portfolio profile content, and content planning; no automatic publishing.
- User-reviewed Personal Semantic Profile and Life Context Navigator dogfood across explicitly enabled people, preferences, time, commitments and career/opportunity domains, with source/explanation/correction and no cross-user connection.
- Manual Relationships and Communities Map with user-defined labels, contacts/groups, interaction notes, cluster hypotheses and follow-ups; no photo/biometric inference.
- A minimal durable single-shot reminder with in-app status/notification; Phase 1B's minimal support sender exists, general reminder email expands in Phase 3A and advanced recurrence in Phase 4.
- Responsive and accessible desktop/tablet/mobile behavior.
- Mature feedback issue/status/response flow for the personal deployment, including user tracking, duplicate handling, release/help links, reopen/verify, and aggregate feedback-quality metrics.
- Public help/support content for every shipped assistant, skill, identity, data ownership, backup/export, and troubleshooting scenario, with search, link validation, accessibility, and version ownership.
- Prompt-injection, permission-intersection, provider-failure, token-budget, and full scenario regression coverage.

Exit gate:

- Cyrus can use the assistant and skills daily for real personal work, retain full data control through self-hosting or managed hosting, and see Quark's efficiency feedback after every session.

### Phase 3A - Email and messaging channel expansion

Deliverables:

- Expand the Phase 1B email channel host into a general channel host with manifest validation, encrypted delegated-token lifecycle, provenance, idempotency, disconnect/purge, consent preview, support/runbook and provider conformance.
- Expand the Phase 1B Gmail, Outlook.com/Hotmail and Microsoft 365 MVP channels into broader intake/follow-up/reminder workflows and provider choice; mature the transactional outbound email provider.
- Expand the Phase 1B delivery topology: Gmail covers Pub/Sub, history cursors and watch expiration/renewal; Microsoft Graph covers change notifications/subscription renewal and delta reconciliation; both define polling/manual fallback and optional relay behavior for self-hosting.
- In-app messaging for user-to-Andreja and feedback/support status; no ambient cross-tenant messaging.
- Discord starts interaction-first (slash commands/interactions). Ambient message content requires a separately approved privileged-intent use case, explicit server/channel scope and review; no self-bot/user-token automation.
- WhatsApp is conditional: user-controlled share/export is supported; a Business Platform pilot is scheduled only after a specific approved business-account scenario/provider review. It is not required for the Phase 3A exit gate.
- Deliverability/security evidence for outbound email including sender identity, SPF/DKIM/DMARC, bounce/complaint handling, abuse controls and cost.
- Sandbox/test accounts, permission-negative, rate-limit, replay, failure, telemetry-redaction, privacy/security and E2E tests.

Exit gate:

- A user can convert consented Gmail/Outlook.com/Microsoft 365 messages into reviewable task proposals, receive in-app/email status, and use an interaction-first Discord pilot without hidden copying, unsupported automation, token leakage or unreviewed sends. WhatsApp share/export is available; Business Platform remains conditional.

### Phase 3B - File, calendar, and professional channels

Deliverables:

- Explicit query-in-place or sync/import selection with no default, bounded cache, model-exposure preview, retention, copy semantics and derived-index purge.
- OneDrive/SharePoint, Google Drive, GitHub and Box adapters, with Google/Box provider review and contract evidence.
- Microsoft/Google calendar channels for connected Calendar and Commitments; manual calendar operation remains available without them.
- GitHub professional-profile/README support through a dedicated grant; LinkedIn remains profile research or user-controlled draft/export until official access is verified.
- Provider change-feed reconciliation, webhook hints, sandbox accounts, support/help, cost and failure evidence.

Exit gate:

- The assistant can use user-approved files, repositories and calendars through least-privilege channels without hidden copying, ambient access or provider lock-in; professional updates remain explicit drafts/proposals.

### Phase 4 - Durable personal mechanisms

Deliverables:

- RFC 5545-compatible recurrence expanded in the user's IANA timezone.
- Portable idempotent reminder execution through a queue/scheduler adapter and at least one useful notification channel.
- Checklists, dependencies, reading/podcast queues, social/travel commitments, and trading research/watchlists.
- Family/Relationships, Health/Wellbeing manual artifacts, Household/Vehicle/Insurance/Projects, Travel/Social and Hobbies/Social Groups first-party skills in manual/in-app mode.
- Before manual Health/Wellbeing dogfood: approved consumer-health privacy/data-flow assessment, breach-response/HBNR analysis, retention/delete/export, model-provider/content rules, support/escalation language and Beverly safety artifact.
- Before personalized insurance comparisons/referrals: jurisdiction, licensing, compensation, disclosure and neutral-ranking review.
- Manual Life Event Planner with branchable scenarios, milestones, dependencies, documents, risks, professional questions and private stakeholder roles.
- Customer Zero Small Business and Entrepreneur Manager for Andreja company operations in manual/document mode, with personal/business persona separation.
- Backup restore, export, purge, retention, managed-to-self-host migration, and disaster-recovery drills.

Exit gate:

- Recurring and delayed obligations survive app restarts, cloud changes, deployments, DST transitions, and duplicate delivery.

### Phase 5 - Multi-tenant SaaS readiness

Deliverables:

- Public onboarding through the selected CIAM path.
- Tenant lifecycle, memberships, self-host/managed plans, freemium/paid packaging, admin, isolation/RLS defense, quotas, metering, billing integration, and support operations.
- Same-tenant household workspaces with explicit member roles, resource disclosure/operation grants, shared project proposals, audit and revocation; relationship type never implies blanket access.
- Same-tenant collaborative life-event workspaces for spouses/family/caregivers with event-scoped roles and data minimization.
- Data-subject export/delete, retention, residency, privacy notices, processor records, and public operational posture.
- Load and scale tests that choose the production Blazor render/runtime/database model based on evidence while preserving migration to another cloud.

Exit gate:

- Independent adult users can safely onboard, use, export, and delete their data with measured unit cost and proven isolation.

### Phase 6 - Open federation and consented sharing

Deliverables:

- Public federation specification, schemas, discovery/trust, capability negotiation, signing, replay protection, short-lived grant-scoped authorization, versioning, and conformance suite.
- Bilateral consent, disclosure levels, reference sharing, proposal-based changes, access audit, revocation, rate limiting, abuse controls, and consent dashboard.
- Cross-instance spouse/family household project collaboration through reference sharing and proposals, preserving one authoritative owner per resource.
- Cross-instance life-event collaboration through minimum-disclosure reference sharing and proposals.
- Consent-aware meaningful connection and shared-opportunity proposals that reveal only the purpose/minimum disclosure and require acceptance from every participating principal.
- Roll out by increasing risk: friend dinner/trip planning, then partner sharing, then adult-family sharing.
- No minor accounts until the separate legal/privacy/safety gate is approved.

Exit gate:

- Two independently hosted conformant Andreja instances can exchange only explicitly granted data, revoke access, audit use, reject malicious content, and recover safely from retries and conflicts.

### Phase 7 - Third-party skill ecosystem

Deliverables:

- Public authoring SDK/spec, signed manifests, remote MCP/HTTP execution, publisher verification, review/scanning, consent UI, resource limits, revocation, compatibility policy, and conformance tests. Local sandbox execution remains a separately gated extension after isolation evidence exists.
- Marketplace/registry only after the trust model is proven, with publisher onboarding, identity/organization verification, listings, search/discovery, compatibility, trial/install/update/uninstall, support contact, reviews/appeals, incident/revocation and transparent status.
- Builder business framework: free/open and paid offerings, customer/license ownership, billing/tax/payout/refund/chargeback models, revenue share, fees, invoices and reporting are explicit and reviewed before money movement.
- Marketplace governance prevents hidden self-preferencing, pay-to-rank without disclosure, retaliation, lock-in and arbitrary enforcement. Ranking factors, featured placement, review criteria, data access and policy changes are transparent with appeal.
- Publishers own their IP/customer relationship subject to marketplace terms; users can export settings/data and move between self-host/managed registries. Andreja does not claim builder customer data.
- Marketplace analytics are aggregate/minimized; publishers never receive unrelated user data or cross-skill behavioral profiles.
- Provide sandbox/test tenants, reference implementations, certification/conformance, migration tools, docs, support and partner feedback so builders can develop sustainable businesses.

Exit gate:

- A third party can build, test, distribute, support and—after commercial/legal/payment gates—monetize a skill/channel without ambient access, in-process untrusted code, hidden ranking, customer-data capture or irreversible platform lock-in.

### Phase 8 - Connector and ecosystem expansion

Deliverables:

- Complete production OneDrive, Google Drive, GitHub, and Box coverage plus Microsoft 365 email and calendar.
- Expand evidence-approved professional/social brand connectors, including LinkedIn and supported Facebook/Instagram professional surfaces, only where official APIs/account types and review permit.
- Add evidence-approved hobby/community channels such as Xbox or game/community providers only through official APIs and the Channel Development Framework.
- Add evidence-approved household/asset channels such as insurance, vehicle/telematics, utilities, warranties, receipts and contractor/home-service providers only through official APIs and scoped grants.
- Add evidence-approved small-business accounting, invoicing, CRM, support, marketing and commerce channels only through official APIs and distinct business-tenant grants.
- Pilot user-selected OneDrive photo files and Google Photos Picker items using official APIs/provider-returned basic metadata and non-biometric reviewable context; never cluster Picker-derived media; premium on-device face clustering remains a separate future gate with a different lawful source.
- Messaging adapters only through supported APIs or explicit user-controlled imports.
- WhatsApp personal-history access remains unsupported officially; do not adopt unofficial automation without a separate risk decision.
- Additional activity categories, skills, notifications, and partner integrations based on validated use.

### Phase 9 - Native mobile clients

Deliverables:

- Hoshi-led ADR and proof-of-concept comparing .NET MAUI Blazor Hybrid and native approaches on real iOS and Android devices.
- Production mobile client architecture using stable Andreja APIs, portable identity linking, secure local storage, offline sync, push/background behavior, deep links/share targets, accessibility, and privacy-safe telemetry.
- Self-host and managed-cloud connection flows with explicit trust, instance discovery, and recovery.
- Device/browser compatibility matrix, automated device tests, app-store release/security/privacy checklists, and lifecycle cost model.

Exit gate:

- A user can securely complete the highest-value Andreja assistant and skill scenarios on iOS and Android across intermittent connectivity without violating data ownership, grant, audit, or cost policies.

### Phase 10 - Business and product analytics

Deliverables:

- Quark/Neelix requirements brief and versioned metric catalog approved through privacy, security, data-quality, operability, and product-outcome review.
- Privacy-separated project, adoption, and resource/cost data products with explicit managed/self-host collection boundaries.
- Role-restricted dashboard, quality/freshness indicators, cohort thresholds, drill-through that never exposes personal content, and exportable aggregate evidence.
- Workflow experiment framework with hypotheses, success/guardrail metrics, rollback, cost accounting, and retrospective outcomes.

Exit gate:

- The business team can make a documented product, workflow, cost, or support decision from trustworthy aggregate evidence without collecting unnecessary personal data or turning self-host telemetry on by default.

### Phase 11 - Lifestyle rewards, miles, points, employer benefits, and financial optimization

Deliverables:

- Provider/API/aggregator feasibility, terms, account types, regulatory, security/privacy, verification, commercial-access, support and cost matrix covering candidate financial institutions/cards, Quicken products/successors, AwardWallet, Rakuten, CardPointers, Bilt, airline, hotel and card reward programs.
- Miles and Points Manager with reconciled balances, expirations, status/benefits, transfer graph, valuation assumptions, travel/redemption goals, alerts and explainable options.
- Lifestyle Rewards Optimizer with card benefits/offers, fees/renewals, category/shopping/cashback opportunities, user preferences and explainable proposals.
- Employer Benefits and Perks Manager with plan-year inventory, eligibility/deadlines, match/benefit explanations, reminders, what-if analysis and approved user-confirmed provider actions.
- Financial/rewards/employer data projections available to Life Event Planner scenarios without bypassing source-skill permissions or action gates.
- Tokenized/OAuth integrations, provider sandbox/partner testing, reconciliation/freshness, privacy/security/regulatory artifacts, support/help, failure/revocation, and full audit.
- Any action capability is separately activated per provider after step-up-auth, confirmation, idempotency, limits, receipt and recovery evidence; no unattended financial or loyalty action.

Exit gate:

- Users can understand and improve loyalty/reward/employer-benefit outcomes from accurate, consented, time-stamped data, and every enabled action is official, strongly confirmed, auditable, reversible where possible, and compliant with provider terms and applicable requirements.

### Phase 12 - Connected Health and Wellbeing Manager

Deliverables:

- Beverly-led requirements/safety model for appointments, medications/refills, allergies, providers, labs, imaging reports/scans, referrals, questions, care plans, insurance artifacts, wellness routines and goals.
- Highest-sensitivity data model, source-preserving artifact storage, confidence-labeled AI/OCR extraction proposals, derived-index deletion, sharing/consent, retention/export/delete and audit.
- Feasibility/terms/security/privacy/regulatory matrix for SMART on FHIR/FHIR, patient portals, Apple Health/HealthKit, Android Health Connect, pharmacy, lab, imaging and approved wearable/provider APIs.
- Manual/document mode first; connected read-only pilot before any user-confirmed refill, appointment, record-request or provider-message action.
- Health/care projections available to elder-care and other Life Event Planner scenarios only through explicit highest-sensitivity grants.
- Safety and E2E evidence for provenance, medication conflicts/instructions, urgent-language handling, no diagnosis/image interpretation, action confirmation, provider failure, recovery and help/support.

Exit gate:

- Users can organize and follow through on health/wellbeing artifacts and goals with source fidelity, privacy, safe boundaries and clinician-facing questions, without Andreja presenting itself as a clinician or changing care autonomously.

## Tracking todos

1. Phase 0: investigate licensing, IP, contribution, trademark, employer obligations, sponsorship, public culture and governance.
2. Phase 0: persist the ratified plan, ADR 0000, company charter, legal/regulatory matrix, architecture documentation and ADR system.
3. Phase 0: create repository roadmap issues, milestones, labels, forms, automation and the later organization Project migration plan.
4. Phase 0: establish Quark's separate burn/usage/metering ledgers, budgets, unit economics and sponsorship controls.
5. Phase 0: sanitize/validate Squad, extend Star Trek casting, cast the crew and trust-review external resources.
6. Phase 0: complete architecture, portability, threat/privacy, cost, identity, federation, channel/skill, website, mobile-readiness and testing decisions.
7. Phase 1A: deliver the independent self-hosted assistant MVP with tenancy, passkeys/recovery, BYOK, one task skill, backup/restore, portable export and local E2E evidence.
8. Phase 1B: after separate budget/legal approval, deliver managed reference,
   invite cohort, BYOK, public/help site and feedback; add a limited real Copilot
   provider only after ADR 0009 acceptance and all provider gates, otherwise
   defer it; pass six Email Triage and five Group Travel acceptance scenarios
   and record Seven's extensibility-proof measurements.
9. Phase 1B-2: mature the independently deployed public website and canonical versioned help/support documentation.
10. Phase 2: build the daily-use assistant and first-party skill product.
11. Phase 3A: build email-first Gmail/Outlook.com-Hotmail/Microsoft 365 intake, minimal outbound email, in-app messaging, interaction-first Discord and conditional WhatsApp share/export.
12. Phase 3B: build OneDrive/SharePoint, Google Drive, GitHub, Box and calendar channels with query/sync, delivery-topology and provider evidence.
13. Phase 4: add recurrence, reminders, manual sensitive/domain skills, migration and recovery mechanisms.
14. Phase 5: prepare managed freemium/premium multi-tenant onboarding, household/business collaboration, isolation, operations, portability and unit economics.
15. Phase 6: implement open federation, bilateral consent, life-event/household collaboration and meaningful connection proposals.
16. Phase 7: build remote third-party skill/channel ecosystem and fair marketplace; commercial money movement remains separately gated.
17. Phase 8: expand approved business, brand, hobby, household, photo, calendar and partner channels.
18. Phase 9: evaluate and build secure native mobile clients.
19. Phase 10: define and build privacy-preserving business/product analytics and Small Business owner dashboard.
20. Phase 0: research personal semantic graph provenance, ontology, portability, privacy, non-user data subjects and skill-extension model.
21. Phase 0: ratify skill/channel catalogs, Channel/Skill/Feedback/Prioritization frameworks, issue scorecard, launch stages and roadmap metadata.
22. Phase 0/2: research Personal Brand provider access/guardrails, dogfood draft-only skill and gate publishing by evidence/provider approval.
23. Phase 0-1B: establish Guinan's feedback/support framework and privacy-safe public/in-app channels with user-visible follow-through.
24. Phase 11: research/build Lifestyle Rewards Optimizer and Miles/Points Manager with read-only-first approved channels and gated confirmed actions.
25. Phase 11: research/build Employer Benefits and Perks Manager with document-first tracking and gated provider actions.
26. Phase 4: dogfood manual Health and Wellbeing artifacts only after consumer-health privacy/breach/retention/model/Beverly safety gates.
27. Phase 12: add approved connected Health and Wellbeing channels and separately gated actions.
28. Phase 4/8: build Hobbies/Social Groups manually, then official Discord/Xbox/community channels.
29. Phase 0/2/6: research/build Life Context and Opportunity Navigator from private explainable insights to consented connections.
30. Phase 4-6: build collaborative Life Event Planner, then integrate access-scoped financial/employer/health evidence.
31. Phase 2/8: build Relationships/Communities Map manually, then non-biometric selected-photo context; gate premium on-device clustering.
32. Phase 0: ratify cohesive workstreams, shared contracts, GitHub ownership, WIP limits, integration responsibility and protected MVP critical path.
33. Phase 4/8/10: dogfood Small Business Manager for Andreja company, add approved channels and privacy-safe owner analytics.
34. Phase 4/5-6/8: build Household/Vehicle/Insurance/Projects manually, add household collaboration, then approved channels.
35. Phase 0: ratify ethical/sustainable company charter, Amazon-inspired operating principles and Microsoft-inspired culture.
36. Phase 0+: maintain Sarek's federal/state/local legal applicability and horizon register for every regulated gate.

## Explicitly not day-one work

- Public user onboarding.
- Live federation transport, internet discovery, or cryptographic peer infrastructure.
- General sharing UI or cross-tenant grants in Phase 1A; the Phase 1B Group Travel slice is the only approved MVP cross-tenant scope.
- Minor accounts.
- Third-party code execution or a marketplace.
- Brokerage integration or trade execution.
- General-purpose email automation beyond the MVP policy, WhatsApp personal-history ingestion, and unsupported messaging automation.
- A full recurrence/dependency/checklist domain in the walking skeleton.
- A permanent choice of Blazor render mode or Azure compute SKU without measurement.
- Any single assistant provider as the universal backend.
- A permanent OCI runtime, Kubernetes, managed database, or cloud-provider choice without measured evidence.
- Native mobile apps before the API, identity, synchronization, and highest-value mobile scenarios are stable.
- Business/adoption dashboards before the MVP has real users, stable metric definitions, and approved privacy boundaries.
- Automatic professional/social posting, engagement automation, or publication of inferred personal facts.
- A graph database, universal ontology, or public personal semantic web before representative use cases and privacy/portability evidence justify it.
- Financial/card/loyalty credential scraping, unsupported APIs, unattended redemptions/transfers/account changes, or brokerage/trading execution.
- Automatic face naming, cloud biometric templates, covert social clustering, full-photo-library ingestion by default, or sensitive-trait inference from photos/associations.
