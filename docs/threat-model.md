# Threat model

- **Status:** Canonical descriptive baseline; not ratified
- **Scope:** Current Phase 1A implementation, contract-only seams, blocked evidence,
  and explicitly gated future capabilities
- **Owner:** Trust, Security, Privacy and Legal — Tuvok, Deanna Troi, Sarek
- **Required challenge:** Tuvok (security), Deanna Troi (privacy), and
  Rai (AI safety); pending
- **Residual-risk acceptance:** Cyrus; pending
- **Classification/impact assessment:** Open; this model does not satisfy that gate
  without an explicitly approved assessment and cited evidence
- **Tracking:** [Issue #116](https://github.com/Jamula/Andreja/issues/116)
- **Last reviewed:** 2026-08-25

This is Andreja's canonical threat model. It reconciles the ratified
[platform plan](plan.md), the
[architecture and data-flow companion](architecture/andreja-high-level.md),
Proposed ADRs, implementation, tests, and
[Phase 1A evidence](phase-1a/evidence-44.md). It is not a penetration-test report,
certification, compliance claim, production authorization, or acceptance of
[ADRs 0001–0005](adr/0001-phase-1a-modular-boundaries.md). The companion
[privacy baseline](privacy.md) owns data classification, handling, and lifecycle.
The proposed [company charter](charter.md#human-and-agent-authority) supplies the
human/agent authority constraints used here but is not itself ratified. The
[semantic-graph contract](semantic-graph.md) supplies detailed semantic/provenance
threat rules, and the [testing matrix](testing-matrix.md) owns the current
test/evidence inventory.

## Status vocabulary and method

| Label | Meaning |
|---|---|
| **Implemented/current** | A code control or local operational path exists and has repository evidence; production readiness is not implied. |
| **Contract-only** | Versioned contracts and deterministic local conformance tests exist, with no live integration or durable future-state store. |
| **Evidence-blocked** | A control is incomplete or lacks required independent/combined proof, so the associated exit remains blocked. |
| **Future/gated** | No current activation is authorized; a decision, design, evidence, and residual-risk acceptance are required first. |

The model uses STRIDE (Spoofing, Tampering, Repudiation, Information disclosure,
Denial of service, and Elevation of privilege) to examine security failures and
LINDDUN (Linkability, Identifiability, Non-repudiation, Detectability, Disclosure
of information, Unawareness, and Non-compliance) to examine privacy harms. It also
treats misuse by an authorized user, coercion, unsafe automation, operator error,
and ecosystem incentives as first-class abuse cases. Labels are reasoning aids,
not proof that every threat is enumerated.

## Security objectives

1. A principal can access or change only data allowed by tenant, purpose, operation,
   sensitivity, grant, consent, and disclosure policy.
2. An assistant, provider, skill, channel, peer, import, or public/support surface
   cannot convert untrusted content into authority.
3. Consequential assistant-originated actions require an exact, unexpired proposal
   and explicit confirmation bound to actor, tenant, version, and idempotency key.
4. Credentials, recovery material, key history, and reusable external authority
   remain out of prompts, manifests, product exports, telemetry, issues, and support.
5. User content remains local by default; every outbound flow has a recipient,
   purpose, data ceiling, disclosure, and kill/revocation path.
6. Audit/provenance explains material actions without becoming a second content
   store or permanent shadow profile.
7. Backups, updates, imports, and offline operation preserve integrity, availability,
   tenant isolation, identity, and user control.
8. Missing evidence, unknown spend, unexplained egress, or a changed trust assumption
   fails closed and pauses exit.

## Assets

| Asset | Why it matters |
|---|---|
| Task, proposal, assistant, semantic, relationship, household, health/finance-adjacent, and future connector content | May reveal intimate facts, intentions, schedules, relationships, finances, health, or location |
| Tenant, app-user, principal, membership, grants, consent, and policy state | Determines ownership and authority |
| Passkey public data, cookies, security stamp, recent-auth state, recovery-code hashes, bootstrap state | Protects account access and sensitive mutations |
| Bootstrap/recovery plaintext; database, Data Protection, TLS, signing, BYOK, and future connector secrets | Reusable authority or cryptographic custody |
| Proposal versions/payloads/digests, idempotency receipts, task/proposal/identity/share/semantic audit | Prevents duplicate effects and supports accountability |
| Provenance, source digests, epistemic/review state, lineage, export manifests | Supports explanation and integrity; can also preserve sensitive associations |
| PostgreSQL, key/config history, recovery sets, application exports, migrations | Durable custody, portability, and recoverability |
| OCI images, SBOMs, signatures/provenance, Compose/runbooks, dependencies | Software and operational supply chain |
| Telemetry, health endpoints, cost/usage state, incident/support evidence | Operational visibility without content leakage |
| Public/help content, claims, domain/CDN/preview configuration | Trust, safety information, and publication integrity |

## Actors and capabilities

| Actor | Capability / risk |
|---|---|
| Account owner | Legitimate control, but may make mistakes, over-share, misuse data about non-users, or operate under coercion |
| Self-host operator | Host/database/key/backup/telemetry access; can misconfigure, inspect, lose, or maliciously alter custody |
| Another household/device user | May access an unlocked browser, observe recovery material, or coerce confirmation |
| Remote unauthenticated attacker | Probes identity, recovery, health, public/preview, and future intake surfaces |
| Authenticated malicious or compromised principal | Attempts cross-tenant access, ID substitution, replay, policy bypass, or destructive actions |
| Assistant/model provider | Receives deliberately disclosed content, may retain it, return malicious output, misreport usage, or fail |
| Skill/channel/connector publisher or dependency maintainer | May ship malicious code/metadata, request excessive scope, exfiltrate data, or become compromised |
| Peer/federated instance | May spoof identity, replay envelopes, misuse grants, retain disclosures, or flood the owner |
| Import/export/backup recipient | May tamper with, copy, disclose, replay, or retain sensitive archives |
| Build/release/registry/hosting provider | May serve a compromised artifact, log metadata, expose previews, or change terms |
| Contributor/reviewer/support responder | May accidentally receive secrets or personal content in issues, logs, screenshots, or fixtures |

There is no current live connector, peer listener, marketplace, managed operator,
support intake, public product/help site, or Andreja cloud data plane. Those actors
remain future/gated. The operator and an abusive household member can be the same
person; root, database, key, backup, and telemetry custody then defeats application-
layer privacy controls.

## Entry points and attack surfaces

- `/Account/Bootstrap`, `/Account/Login`, `/Account/Recovery`,
  `/Account/Passkeys`, sign-out, cookies, WebAuthn ceremonies, forwarded headers,
  TLS termination, and return URLs;
- authenticated typed Open Loops APIs/UI for assistant request, proposal review,
  confirm/reject/cancel, list, complete, export, and delete;
- OpenAI-compatible outbound endpoint, authorization field, streaming response,
  tool-call JSON, retry/cancellation, budget, and provider configuration;
- `ISkillHost`, `IChannelHost`, manifests, schema versions/digests, invocation
  arguments, authorization context, and first-party handlers;
- PostgreSQL connections, migrations, composite constraints, locks, import ledger,
  proposal/task audit, and application portability CLI;
- secret/key/config files, Compose mounts/networks, reverse proxy, health probes,
  OTel endpoint, backup/update/restore scripts, and OCI artifacts;
- application export/import archives, checksums, paths, encryption keys, and
  reauthorization instructions;
- contract-only signed peer envelopes, consent/grant/share audit, semantic
  assertions/extensions/JSON-LD, provenance, and tombstones; and
- future public/help hosting, previews, DNS/CDN, remote support/feedback, connectors,
  federation, managed hosting/control plane, and third-party skills.

## Trust boundaries

The architecture defines six named boundaries:

| Boundary | Threat-model responsibility |
|---|---|
| **TB1 — client/browser** | Browser input, cookies, authenticator ceremonies, CSRF, return URLs, shared-device exposure, and typed HTTP contracts. Server policy remains authoritative. |
| **TB2 — authenticated app** | Composition root, API, modules, adapters, proposal control plane, validation, and process compromise. Co-hosting cannot bypass HTTP authorization. |
| **TB3 — tenant/principal/purpose isolation** | Scoped context, policy, projections, and PostgreSQL composite constraints. Resource IDs are never capabilities. |
| **TB4 — adapter/provider** | Identity, persistence, BYOK, future connectors, and telemetry. Credentials stay in adapters; outbound content needs a declared recipient/purpose. |
| **TB5 — local operator custody** | PostgreSQL, key history, config/secrets, exports, backups, telemetry, images, and runbooks. Operator compromise is not solved by application policy. |
| **TB6 — external/peer** | Model providers, future connectors/peers/support/public hosting/backup destinations. Inbound content is untrusted; external retention and authority are separate. |

### Phase 1A identity overlay and precedence

The [Phase 1A local identity threat-model
overlay](phase-1a/identity-threat-model.md) is subordinate detail, not a competing
canonical model. Its browser/authenticator boundary maps to **TB1**; ASP.NET Core
Identity and the application process map to **TB2**; tenant/principal context and
PostgreSQL isolation controls map to **TB3**; the identity adapter maps to **TB4**;
the TLS proxy, PostgreSQL, Data Protection history, and host-mounted secret/key
storage map to **TB5**; and any future external OIDC/identity provider would cross
**TB6**. HTTPS termination spans the TB1/TB2 traffic boundary while its proxy and
key custody remain TB5 operator responsibilities.

The ratified plan and this canonical model take precedence. The overlay narrows
identity abuse cases and evidence only. If it conflicts with TB1–TB6, status, or a
stop condition, the same change must correct the overlay; it cannot weaken or
silently redefine the canonical boundary.

## Assumptions and non-assumptions

Current controls assume a patched and access-controlled host/browser/authenticator,
correct DNS/clock where relevant, a trusted same-host TLS proxy configured exactly,
protected operator files, an empty/disposable database for evidence, and honest
execution of reviewed runbooks. A compromised host, browser, authenticator, database
administrator, TLS endpoint, or recovery destination can defeat application
controls. Passkeys are not assumed to be attested hardware or MFA.

Checksums detect corruption but do not authenticate an exporter. Hosted OCI
evidence is trusted only when its retained Sigstore bundle verifies against an
independently authenticated, pre-positioned root outside that evidence and a
separately trusted policy with exact GitHub
issuer, repository, workflow identity/revision, trigger, and tag-ref claims.
Operator-held-key evidence remains local-only and cannot downgrade that policy.
Telemetry suppression reduces content exposure but does not make an arbitrary remote
collector safe. Human confirmation reduces automation risk but does not prove
informed, voluntary, accessible, or uncoerced consent.

## STRIDE threat analysis

| Threat | Scenario | Current mitigation and evidence | Residual / status / owner |
|---|---|---|---|
| **Spoofing — bootstrap/recovery** | Stolen or replayed bootstrap/recovery material creates or takes over the owner | 256-bit read-only bootstrap file, exact HTTPS origin/RP, built-in WebAuthn verification, protected one-time state, bounded input, rate limits, transaction/advisory locks, generic failures, recovery rotation and session invalidation; browser/PostgreSQL negative tests | Host/browser theft and lost recovery set remain. **Implemented/current**. **Core Platform and Architecture — Spock, T'Pol, Seven of Nine; Trust, Security, Privacy and Legal challenge — Tuvok, Deanna Troi, Sarek.** Stop on origin/proxy uncertainty or secret exposure |
| **Spoofing — session/recent auth** | Stolen long-lived cookie adds/removes a passkey or survives recovery | Secure/HttpOnly/SameSite=Strict cookies, Data Protection, zero-interval security-stamp validation, antiforgery on mutations, short-lived audience/user/stamp-bound recent-auth marker and one-time nonce | Shared/unlocked or compromised device remains. **Implemented/current. Core Platform and Architecture — Spock, T'Pol, Seven of Nine; Trust, Security, Privacy and Legal challenge — Tuvok, Deanna Troi, Sarek.** |
| **Spoofing — tenant/principal** | Caller substitutes an ID, worker context, or external identity to access another tenant | Authenticated scoped tenant/app-user/principal context; policy and access projections; composite tenant foreign keys/uniqueness; external subject not a domain FK; two-tenant tests | New queries/background work can regress. **Implemented/current. Core Platform and Architecture — Spock, T'Pol, Seven of Nine.** Any isolation failure is a release stop |
| **Tampering — proposal** | Provider/client changes proposal payload, version, actor, or expiry before confirmation | Canonical payload/digest, durable proposal state, expected version, actor/tenant binding, expiry, policy re-evaluation, transactionally coupled task/audit/idempotency | User may confirm deceptive content or confirmation UI may regress. **Implemented/current. Core Platform and Architecture — Spock, T'Pol, Seven of Nine; Web, Public Site and User Experience — Jadzia Dax, Neelix, Guinan.** |
| **Tampering — manifest/tool call** | Prompt injection or publisher changes tool name/schema/capability/digest | Exact typed-tool allowlist, strict JSON/name/field/type validation, manifest version/digest, shared fail-closed evaluator, no ambient secrets/network/`DbContext`; adversarial unit tests | First-party in-process code shares process trust. Third-party execution is not isolated. **Implemented/current first-party; future/gated third-party. First-party Skills and Developer Ecosystem — Seven of Nine + domain leads; Channels and Connectors — Jett Reno, Seven of Nine, Tuvok.** |
| **Tampering — database/migration** | Implicit or malicious schema change corrupts state or weakens isolation | Explicit reviewed forward migrations, no startup migration, backup-before-update, empty/prior schema and constraint tests | Trusted administrator or malicious migration can still alter data. Genuine update/rollback proof is missing. **Evidence-blocked. Platform Operations, Hosting and FinOps — Jett Reno, Quark; Core Platform and Architecture — Spock, T'Pol, Seven of Nine.** |
| **Tampering — import/export** | Archive path traversal, checksum substitution, duplicate/conflicting import, or malicious semantic extension changes state | Authenticated encrypted archive, safe relative paths, SHA-256/length checks, schema/version/exclusion validation, clean-instance requirement, write-free dry run, serialized import ID, atomic commit, no remote context fetch or secrets | Archive key/distribution and exporter authenticity depend on operator custody. **Implemented/current portability. Core Platform and Architecture — Spock, T'Pol, Seven of Nine; Platform Operations, Hosting and FinOps — Jett Reno, Quark.** |
| **Spoofing/tampering — hosted release identity** | An actor, fork, unrelated workflow, branch, mutable tag, altered bundle/root, or transparency bypass is accepted as an Andreja release | Tag-only GitHub Actions job; commit-on-protected-main check; job-only `id-token: write`; exact issuer/repository/workflow identity/name/revision/trigger/ref matching; pinned Cosign; retained standardized bundle; independently authenticated/pre-positioned TUF root outside evidence must match the retained copy and is the only Cosign root; inclusion-promise/proof and SCT required; network-blocked verification; local operator mode cannot satisfy hosted release | GitHub repository/tag/workflow protection, Fulcio, CT, Rekor, and TUF compromise remain external trust risks. Public records permanently disclose approved repository/workflow/ref/digest metadata. **Implemented policy/workflow; first reviewed hosted signature evidence pending. Trust, Security, Privacy and Legal — Tuvok, Deanna Troi, Sarek; Platform Operations — Jett Reno; Cyrus accepts residual risk.** |
| **Repudiation** | Actor denies confirmation, recovery, deletion, sharing, or import | Content-minimized identity/task/proposal audit, version, actor, source, outcome, occurrence time, import ledger, idempotency evidence; share/semantic audit contracts | Audit is not non-repudiation, legal proof, or permission for indefinite retention; operator can alter DB. **Current plus contract-only. Trust, Security, Privacy and Legal — Tuvok, Deanna Troi, Sarek.** |
| **Information disclosure — BYOK** | Credential, prompt, task, error body, or provider endpoint details leak | Exact endpoint allowlist, non-loopback budget stop before credential resolution, file-only credential value, no redirects/decompression, bounded response, content-free errors/metrics, UI recipient/retention disclosure, rotate/revoke path | Provider receives the deliberately sent request/schema and may retain it; operator disclosure may be stale. No paid live evidence. **Current seam; gated external activation. Core Platform and Architecture — Spock, T'Pol, Seven of Nine; Platform Operations, Hosting and FinOps — Jett Reno, Quark.** |
| **Information disclosure — telemetry** | Auto-instrumentation exports URL/user/task/prompt/secret content | Attribute allowlist processor, no formatted/scoped/state logging, prohibited-key counters, synthetic canaries, local collector evidence | Values under allowed route/method/status fields and new instruments require review; remote collector is unapproved. **Implemented/current local; future/gated remote. Platform Operations, Hosting and FinOps — Jett Reno, Quark.** |
| **Information disclosure — backup/export** | Dump, archive, key history, or restore manifest leaks user data/secrets | Encryption, separate keys, declared exclusions, least-privilege destinations, checksum and clean-instance verification, no secret values in manifests | Combined encrypted recovery custody is unproven; operators may mishandle copies. **Application export current; recovery evidence-blocked. Platform Operations, Hosting and FinOps — Jett Reno, Quark.** |
| **Information disclosure — public/support** | Private docs, product data, preview, issue, screenshot, or feedback payload becomes public | Product/public-site separation contract, loopback/private-review-only prototype, no analytics/intake, secret/content prohibition in repository artifacts | Historical Pages bytes/provider logs/caches may remain; future host logs IPs. **Contained history; future/gated site/support. Web, Public Site and User Experience — Jadzia Dax, Neelix, Guinan; Customer Success, Feedback and Support — Guinan.** |
| **Denial of service — identity/provider** | Recovery guessing, oversized input, provider stall/429/5xx, response flood, or retry storm exhausts resources | Bounded inputs before allocation/hash, per-client plus global limits, timeout/cancellation/kill, bounded buffer, capped retries/backoff, nonretryable 3xx/4xx, health/readiness | Distributed abuse and local resource exhaustion remain; no production SLOs. **Implemented/current; objectives blocked. Core Platform and Architecture — Spock, T'Pol, Seven of Nine; Platform Operations, Hosting and FinOps — Jett Reno, Quark.** |
| **Denial of service — database/offline** | PostgreSQL/key loss, hidden egress dependency, bad migration, or unavailable registry prevents use | Durable volume, health/readiness, preloaded digest images, internal networks, `--pull never`, local fake, dump/restore tools, explicit migration, offline/no-egress evidence | Database-plus-key sign-in recovery and genuine rollback missing. **Evidence-blocked. Platform Operations, Hosting and FinOps — Jett Reno, Quark.** |
| **Elevation of privilege — direct mutation** | Model, skill, client, or replay writes without confirmation/policy | Provider can call only propose tool; skill returns proposal; durable confirm binds actor/tenant/version/idempotency; domain rechecks policy; direct UI actions remain server-authorized/audited | Confirmation fatigue/coercion or compromised app process remains. **Implemented/current. Core Platform and Architecture — Spock, T'Pol, Seven of Nine; Web, Public Site and User Experience — Jadzia Dax, Neelix, Guinan.** |
| **Elevation of privilege — grant/share** | Manifest, stale consent, disclosure request, or imported grant expands access | Policy intersects residency/principal/bilateral consent/active grant/purpose/operation/data class/sensitivity and reduces ordered disclosure; expiry/revocation; imported grants inactive; adverse fixtures | No live transport or persistent revocation proof. **Contract-only. Trust, Security, Privacy and Legal — Tuvok, Deanna Troi, Sarek; Core Platform and Architecture — Spock, T'Pol, Seven of Nine.** |

## LINDDUN privacy-threat analysis

| Threat | Scenario and control | Residual / status |
|---|---|---|
| **Linkability** | Stable IDs, provenance, audit, and usage could link actions or people. Keep IDs tenant-scoped, telemetry free of raw identity, provenance minimal, and external identities adapter-bound. | Operator/database access can still correlate. Cross-tenant/federated identity resolution is not approved |
| **Identifiability** | Task, schedule, relationship, health, finance, or support details identify the owner or non-users. Treat general task content as potentially highly sensitive; do not use personal content in evidence/support. | User-entered free text cannot be perfectly classified. **Implemented handling; residual user/operator duty** |
| **Non-repudiation harm** | Permanent audit could expose or punish a user after correction/deletion. Audit stores minimized facts, not content, and has no immutable-personal-data claim. | Numeric audit retention/deletion policy is missing. **Evidence-blocked** |
| **Detectability** | Existence/timing of tasks, recovery attempts, peer messages, or public queries reveals activity. Current sharing disclosure begins at `Existence`; local search stays in memory; identity failures are generic. | Traffic analysis and local operator visibility remain. Live peers/public hosting are **future/gated** |
| **Disclosure** | Provider, connector, peer, telemetry, backup, export, issue, or preview receives excessive data. Default local custody, explicit recipient/purpose, least disclosure, export exclusions, redaction, and kill/revoke paths constrain flows. | External retention cannot be controlled after disclosure. New recipients require review |
| **Unawareness** | User does not understand model recipient, retention, proposal consequence, inference, or sharing terms. UI discloses provider/model/recipient/retention and exact proposal; consent terms are versioned and purpose-bound. | Accessibility, coercion, provider-term drift, and comprehension need continuing evidence |
| **Non-compliance with stated policy** | Implementation, operator, or provider behavior diverges from this baseline. Docs/hash/contract tests, telemetry canaries, negative tests, explicit gates, and stop conditions expose drift. | No legal compliance claim; qualified counsel and jurisdiction-specific analysis remain separate |

## Abuse and misuse cases

| Abuse/misuse | Required response and boundary |
|---|---|
| **Coercive control or stalking** — an owner/operator uses tasks, schedule, contacts, household, or future location/connectors to monitor another person | Do not infer consent from relationship/household status; minimize non-user data; no live sharing/connectors; add survivor-safety, discreet revocation, delegated-access, and coercion review before household/federation features |
| **Child surveillance or unsafe child-directed use** | No current child account, guardian delegation, age assurance, or child-directed product. Do not market or activate until dedicated safety/privacy/legal/accessibility/abuse decisions and evidence exist |
| **Elder or dependent-adult financial/care exploitation** — a caregiver/operator uses future finance, benefits, trading, health, or household capabilities to divert assets, isolate someone, or control care/medication | No autonomous execution or inferred authority from caregiver status. Do not activate until capacity, consent, supported decision-making, safeguarding, appeal/revocation, privacy, legal, and abuse reviews are approved |
| **Health or finance overreach** — model produces diagnosis, medication change, trade, payment, benefit, or insurance action | Current tasks may record reminders only. No autonomous diagnosis, medication change, trade, money movement, filing, or regulated action; future workflows require qualified review and exact confirmation tiers |
| **Sensitive inference/shadow profile** — semantic graph derives relationship, health, financial, identity, or vulnerability claims | Inference is explicit, reviewable, purpose-bound, model/share denied by default, and contract-only; no embeddings/global graph; correction/delete invalidates dependants. Stop on unexplained or harmful inference |
| **Prompt/tool injection** — user/provider/connector content asks the model or host to bypass policy | Treat content as data; exact allowlisted typed tools, strict schema, purpose/capability checks, proposal-only side effects, output distrust, adversarial tests |
| **Confirmation fatigue/deceptive proposal** — repeated or misleading prompts cause accidental approval | Show exact diff/source/purpose/expiry, require intentional confirmation, reject stale versions, rate-limit/noise-control future sources, and never let provider confirm. UX/materially deceptive changes block release |
| **Destructive retry/race** — duplicate confirm, task mutation, recovery, or import causes repeated/lost effects | Expected versions, idempotency keys/receipts, transaction/advisory locks, atomic audit, conflicting-key rejection, concurrency tests |
| **Resource/cost exhaustion** — long input, response, retries, recovery guessing, telemetry cardinality, or paid calls consume resources | Input/response bounds, timeout/cancel/kill, retry caps, low-cardinality metrics, rate limits, zero external budget by default. Unknown/unapproved spend stops calls |
| **Malicious publisher/update** — compromised dependency, skill, channel, container, migration, or static-site toolchain exfiltrates or alters behavior | Pinned dependencies/images/actions, SBOM/scans, immutable digest, signed provenance/trust anchor, reviewed migrations, manifest integrity, least capability, rollback. Third-party execution/public deployment remains gated |
| **Backup/export coercion or theft** | Encrypt with separately held key, minimize copies, protect destination, verify import, exclude authority, and support revocation/deletion replay. Application export does not carry passkeys/tokens |
| **Support/reviewer exposure** | Use synthetic reproduction and content-free timestamps/result classes only; reject secrets, connector payloads, exports, dumps, screenshots, raw IDs, prompts, and personal task content |
| **Malicious operator/host compromise** | Application controls cannot protect against root/database/key custody compromise. Harden and separate custody, review access, restore from trusted artifacts, rotate credentials, and disclose this self-host limitation |

## Secrets, BYOK, and key custody

Secret values never belong in source, image layers, configuration values,
environment variables for the BYOK adapter, manifests, prompts, tool arguments,
telemetry, exports, issues, or support. Current BYOK uses a non-secret handle mapped
to one exact absolute file. The store caps bytes, requires restrictive attributes/
mode, rereads for rotation, and fails content-free on missing, writable, oversized,
empty/revoked, inaccessible, or invalid UTF-8 content.

TLS, Data Protection, database, bootstrap, signing, and recovery materials have
different purposes and rotation histories. Do not reuse one key across roles.
Database backup without matching Data Protection history is not identity recovery;
application export intentionally omits both. If exposure is suspected, pause/kill
the provider or app boundary, revoke at the issuer/provider, rotate from a trusted
host, preserve only minimized incident facts, and verify old authority fails.

## Supply chain and release integrity

The OCI policy requires a non-root immutable image, pinned bases/dependencies,
reproducible evidence, SPDX and CycloneDX SBOMs, vulnerability/IaC/container scans,
and in-toto provenance verified against a separately trusted operator key.
Evidence run #44 passed reproducibility/scans as audit evidence but could not satisfy
trusted signing authorization. It also lacked a genuine second signed revision for
update/rollback. These are stop conditions, not documentation caveats.

GitHub Actions are commit-pinned. Proposed third-party skills/channels, provider
SDKs, public-site generators, and connector dependencies need publisher/source
review, integrity metadata, scope isolation, vulnerability/deprecation response,
revocation, compatibility, and rollback. A valid signature proves key possession,
not benign behavior.

## Provenance and semantic integrity

The detailed [semantic threat/privacy questions and
controls](semantic-graph.md#threat-and-privacy-questions) govern this contract-only
seam. Current semantic contracts pin an embedded JSON-LD context/version, validate safe
predicates, distinguish observed/user-stated/inferred and verification/review state,
bind sources by digest, and track correction/supersession/retraction/deletion.
Remote context retrieval is forbidden during import. Unknown versions/classes and
tampered records fail closed. Sensitive inference, sharing, and model exposure
default denied.

Risks remain: false or poisoned source data, digest-valid but misleading evidence,
provenance that recreates deleted content, inference cascades, identity conflation,
extension-schema collision, and stale derived indexes/backups. The current in-memory
ledger/fixtures prove only contract behavior. No durable semantic store, vector
index, graph database, connector ingestion, or remote reasoning is active.

## Backup, restore, export, and import

Three mechanisms remain distinct:

- PostgreSQL logical dump/restore handles durable database recovery but excludes
  host keys/configuration;
- application export/import handles inspectable user portability and excludes
  reusable authority; and
- a full operational recovery set must combine encrypted database, matching key
  history, configuration, versions, checksums, and restore/sign-in evidence.

Import is an attack surface. Validate archive/version/schema/checksum/length/path and
all references before any write; run a write-free dry run; require a clean target;
serialize commit by import ID; remap ownership explicitly; keep grants inactive;
never fetch remote contexts or execute content; and reauthorize providers only after
review. Checksums do not authenticate the source. Backups/exports must be encrypted,
access-controlled, retention-bounded, and absent from repositories/support.

## Offline operation and observability

Offline evidence begins after image/source acquisition. Preloaded digest-pinned
images, `--pull never`, internal networks, local fake provider, health/readiness, and
failed TEST-NET egress demonstrated no hidden runtime call in the tested profile.
This does not prove a compromised image is safe or that first acquisition is
offline.

OpenTelemetry uses allowlisted attributes and a local collector in current evidence.
Task/prompt/response/token/recovery/raw-ID canaries were suppressed. Operators must
still review new instrumentation, route values, collector access, storage/retention,
and egress. Remote observability, crash reporting, session replay, analytics, and
support upload are not approved.

## Future/gated boundaries

### Federation and sharing

Before a live peer listener or relay exists, approve trust bootstrap, key rotation,
peer identity, replay storage, clock/expiry handling, grant/consent UX, least
disclosure, revocation/deletion propagation, abuse reporting/blocking, rate limits,
interoperability/versioning, outage behavior, metadata retention, backup/export, and
operator incident authority. Current signed-envelope tests are local fixtures only.

### Public/help site

The current prototype is loopback/private-review only. Any public launch needs a
separate origin/deployment identity, public-content-only artifact, no app cookie or
privileged route, access-controlled expiring previews, approved claims/license/
brand/domain, security headers and reporting, accessibility evidence, host/CDN log
review, reproducible immutable promotion, rollback, and cost approval. Analytics,
feedback, personalization, remote search, service worker, ads, sponsorship, and
third-party scripts require separate decisions.

### Managed hosting/control plane

Managed Phase 1B would add operator/admin separation, CIAM, cloud/network/database/
object-storage providers, control-plane/data-plane authorization, tenant placement,
support access, key management, billing, SLO/PITR, incident response, residency,
subprocessors, abuse prevention, and cross-tenant side channels. No provider,
topology, region, service, account, subscription, free tier, trial, or remote control
authority is selected or authorized.

### Connectors and third-party skills

Require official API terms, least scopes, separate identity/content/feedback/
publishing grants, adapter-owned token isolation, webhook signature/replay controls,
read-only/draft-first rollout, data-flow/retention/residency/model-exposure maps,
disconnect/purge/export/reauthorization, sandbox/adverse tests, publisher and
dependency review, process/resource isolation proportional to risk, support and
incident runbooks, and explicit residual-risk acceptance. Highly sensitive or
regulated-adjacent data gets a separate gate; roadmap inclusion is not approval.

## Evidence map

The canonical [testing matrix](testing-matrix.md) owns the full current evidence
inventory and exclusions; this table maps only threat-model controls.

| Control area | Primary repository evidence | Status |
|---|---|---|
| Architecture boundaries | `Andreja.ArchitectureTests`, typed API contracts, dependency tests, [architecture companion](architecture/andreja-high-level.md) | **Implemented/current** |
| Tenant constraints and atomic persistence | `Andreja.PostgreSqlIntegrationTests`, `PostgreSqlIdentityTests`, Open Loops persistence tests | **Implemented/current** |
| Passkey/bootstrap/recovery/session | `BootstrapCeremonyEndpointTests`, `PasskeyUserHandleTests`, identity and forwarded-header tests, real-browser #44 run | **Implemented/current** |
| Proposals/idempotency/confirmation | `ProposalLifecycleTests`, `OpenLoopsVerticalSliceTests`, API and PostgreSQL integration tests | **Implemented/current** |
| BYOK/provider adversity | `OpenAiCompatibleConformanceTests`, `AssistantProviderTests`, [BYOK contract](phase-1a/byok-security-privacy.md) | **Current local conformance; external activation gated** |
| Skill/channel/grant/peer/semantic | `SkillHostTests`, `ChannelHostTests`, `ExecutionAuthorizationEvaluatorTests`, `ConsentAndDisclosureTests`, `PeerChannelConformanceTests`, `SemanticAssertionConformanceTests` | **Current first-party skill; otherwise contract-only** |
| Portability | `ApplicationPortabilityTests`, `PortabilityCommandTests`, [portability runbook](operations/portability.md), supplemental #87 evidence | **Implemented/current** |
| Telemetry/offline | `Test-TelemetryEvidence.ps1`, `Test-OfflineEvidence.ps1`, operations tests and #44 evidence | **Implemented/current tested profile** |
| OCI trust and update/rollback | Accepted ADR 0010, supply-chain scripts/policy/workflow, negative cases, and #44 matrix | **Keyless policy/workflow implemented; first hosted signature and genuine update/rollback evidence blocked** |
| Combined encrypted recovery and restored sign-in | Backup/restore scripts, [recovery runbook](operations/identity-recovery.md), #44 matrix | **Evidence-blocked** |

Passing deterministic or local evidence does not replace the separately trusted OCI
key, encrypted combined recovery/sign-in, second signed update/rollback revision,
approved numeric SLO/RPO/RTO/retention/model-spend envelopes, or final residual-risk
acceptance.

## Residual risks, owners, and stop conditions

| Residual risk | Owner | Stop / acceptance condition |
|---|---|---|
| Compromised self-host, browser, authenticator, TLS proxy, database admin, or recovery destination | Platform Operations, Hosting and FinOps — Jett Reno, Quark; Core Platform and Architecture — Spock, T'Pol, Seven of Nine | Stop on suspected compromise; isolate, revoke/rotate, restore trusted artifacts, and re-evidence. Application policy cannot accept root compromise |
| Missing trusted OCI signature or genuine rollback proof | Platform Operations, Hosting and FinOps — Jett Reno, Quark; Trust, Security, Privacy and Legal — Tuvok, Deanna Troi, Sarek; Cyrus accepts residual risk | No Phase 1A exit until a reviewed keyless hosted run retains its bundle/root copy, passes exact-claim offline verification against an independently held root, and a second revision proves update/rollback |
| Database restored without encrypted matching key/config history and passkey sign-in | Platform Operations, Hosting and FinOps — Jett Reno, Quark; Core Platform and Architecture — Spock, T'Pol, Seven of Nine | Treat as unrecovered; no exit until combined clean-instance proof passes |
| No approved retention, SLO, RPO/RTO, spend, or high/critical residual-risk decision | Trust, Security, Privacy and Legal — Tuvok, Deanna Troi, Sarek; Platform Operations, Hosting and FinOps — Jett Reno, Quark; Cyrus decides | No exit, external paid call, or public commitment |
| Provider retains disclosed request or changes terms | Core Platform and Architecture — Spock, T'Pol, Seven of Nine; Platform Operations, Hosting and FinOps — Jett Reno, Quark | Disable endpoint/profile until disclosure, purpose, terms, budget, and revocation are current |
| New query/instrumentation leaks content or crosses tenants | Core Platform and Architecture — Spock, T'Pol, Seven of Nine; Platform Operations, Hosting and FinOps — Jett Reno, Quark | Immediately stop release/telemetry, contain, rotate if needed, correct and add negative evidence |
| Confirmation becomes bypassable, deceptive, inaccessible, or replayable | Core Platform and Architecture — Spock, T'Pol, Seven of Nine; Web, Public Site and User Experience — Jadzia Dax, Neelix, Guinan; Trust, Security, Privacy and Legal — Tuvok, Deanna Troi, Sarek | Stop consequential actions until exact review, binding, policy, idempotency, and accessibility pass |
| Non-user/child/household harm or sensitive inference lacks lawful/ethical purpose and consent | Trust, Security, Privacy and Legal — Tuvok, Deanna Troi, Sarek; Cyrus decides | Do not activate; dedicated decision and abuse evidence required |
| Self-host operator is also a coercive or abusive household actor | Trust, Security, Privacy and Legal — Tuvok, Deanna Troi, Sarek; Platform Operations, Hosting and FinOps — Jett Reno, Quark; Cyrus decides | App-layer controls are insufficient. No multi-person household feature until an operator-independent safety, access, revocation, export, and recovery design is approved and tested |
| Connector, federation, public site, managed hosting, remote telemetry/support, or third-party skill activates without its gate | Channels and Connectors — Jett Reno, Seven of Nine, Tuvok; First-party Skills and Developer Ecosystem — Seven of Nine + domain leads; Web, Public Site and User Experience — Jadzia Dax, Neelix, Guinan; Platform Operations, Hosting and FinOps — Jett Reno, Quark; Cyrus decides | Disable/contain immediately; no grandfathering from contracts, roadmap, or prior exposure |

Any unresolved high/critical threat, tenant-isolation failure, reusable-secret leak,
prohibited telemetry content, unexplained outbound call, untrusted artifact,
unrecoverable identity/data, stale consent/grant, or unknown/unapproved spend blocks
release or Phase 1A exit. A waiver must be explicit, scoped, time-bounded, owned, and
recorded by the decision owner; silence, merge, or passing unrelated tests is not
acceptance.

## Change and review process

Re-review this model whenever a trust boundary, entry point, identity factor,
tenant/purpose policy, consequential action, provider/model, data class, skill/
channel execution mode, connector/peer transport, semantic inference, persistent
store, secret/key path, telemetry destination, backup/import/update flow, public/
support surface, deployment topology, or operator role changes.

Each material update needs an issue and reviewed pull request; mapped code/tests or
an explicit evidence gap; architecture, privacy/security, abuse/misuse,
accessibility/documentation, and operability review; named residual-risk owners; and
stop conditions. Changes to ratified trust boundaries, data ownership, legal posture,
or non-negotiable architecture follow the plan-hash and re-ratification rules in
[ADR 0000](adr/0000-plan-ratification.md).
