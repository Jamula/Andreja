# Privacy baseline

- **Status:** Canonical descriptive baseline; not ratified
- **Scope:** Current Phase 1A implementation, contract-only seams, blocked evidence,
  and explicitly gated future capabilities
- **Owner:** Trust, Security, Privacy and Legal — Tuvok, Deanna Troi, Sarek
- **Required challenge:** Deanna Troi (privacy), Tuvok (security), and
  Rai (AI safety); pending
- **Residual-risk acceptance:** Cyrus; pending
- **Classification/impact assessment:** Open; this inventory does not satisfy that
  gate without an explicitly approved assessment and cited evidence
- **Tracking:** [Issue #116](https://github.com/Jamula/Andreja/issues/116)
- **Last reviewed:** 2026-08-25

This document is Andreja's canonical privacy inventory and handling baseline. It
describes repository evidence; it does not authorize production use, public
hosting, a connector, federation, managed hosting, a provider, a retention period,
or a legal/compliance claim. The ratified [platform plan](plan.md) governs.
[ADRs 0001–0005](adr/0001-phase-1a-modular-boundaries.md) and
[ADR 0008](adr/0008-public-website-artifact-boundary.md) remain **Proposed**.
Implemented code and passing tests are evidence, not acceptance of those ADRs or
Phase 1A exit. The proposed [company charter](charter.md#commitments) supplies
the data-dignity and human-agency commitments used here but is not itself ratified.

The companion [threat model](threat-model.md) covers adversaries, abuse, controls,
and residual risk. Phase-specific overlays remain useful evidence inputs:
[identity privacy](phase-1a/identity-privacy.md),
[BYOK security/privacy](phase-1a/byok-security-privacy.md), and the
[Phase 1A evidence record](phase-1a/evidence-44.md). The
[semantic-graph contract](semantic-graph.md) supplies the detailed provenance,
non-user, sensitive-inference, and lifecycle rules summarized below; the
[testing matrix](testing-matrix.md) owns the current test/evidence inventory.

## Status vocabulary

| Label | Meaning |
|---|---|
| **Implemented/current** | Code or a local operational path exists and has repository evidence. This is not a production-readiness claim. |
| **Contract-only** | Application-owned types and deterministic local conformance tests exist, but no live integration or durable future-state data is implied. |
| **Evidence-blocked** | Some control exists, but required independent or combined evidence is missing. The associated exit remains blocked. |
| **Future/gated** | No current activation is authorized. A decision, privacy/security review, evidence, and usually a new ADR are required first. |

## Privacy commitments and boundaries

Andreja applies these rules to every current or proposed data flow:

1. collect or derive only data needed for an explicit user purpose;
2. keep tenant ownership, principal, purpose, sensitivity, grant, and disclosure
   explicit; IDs are not capabilities;
3. keep content local by default and deny model, skill, channel, sharing, and
   telemetry exposure unless the relevant boundary explicitly permits it;
4. separate credentials and recovery material from user-content storage, exports,
   manifests, prompts, telemetry, issues, and support artifacts;
5. require exact review and human confirmation for assistant-proposed consequences;
6. make provenance, uncertainty, correction, revocation, deletion, and export
   inspectable without copying sensitive source content into audit records;
7. do not create shadow profiles, infer sensitive facts by default, or treat
   household proximity as consent; and
8. stop rather than silently widen purpose, disclosure, retention, or authority.

The self-host operator controls the host, database, key material, configuration,
exports, backups, telemetry destination, and any enabled BYOK endpoint. That
custody creates duties; it does not give an operator permission to inspect another
person's data or weaken Andreja's policy boundary. Andreja currently provides no
managed data plane, support intake, public product site, live connector, or live
peer/federation service.

## Data inventory and classification

Classification is contextual. A task title that reveals health, finances, a child,
or a relationship is treated as **highly sensitive user content** even though the
task schema is general.

| Data area and examples | Classification | Purpose and access | Custody / exposure | Lifecycle and current status |
|---|---|---|---|---|
| Tenant, app-user, principal, membership, display/workspace names, internal IDs | Personal account/authorization data | Establish ownership, sign-in context, and tenant-scoped authorization; authenticated owner and server-side identity/policy paths only | PostgreSQL under operator custody; not sent to a model or telemetry | Included in application portability only where the current schema specifies; identity recovery remains separate. **Implemented/current** |
| Public passkey credential data, credential ID, security stamp, protected cookies, recent-auth nonce hash | Restricted authentication data | Verify sign-in and bind sensitive account mutations | Authenticator retains private key/biometric or PIN handling; app/database retain only public credential and protected/hashed state; Data Protection keys remain outside database | Excluded from application export and support/telemetry. Revocation and security-stamp rotation invalidate access. **Implemented/current** |
| Bootstrap token; recovery-code plaintext; recovery lookup/verification hashes; Data Protection, TLS, signing, and database secrets | Secret/recovery material | One-time bootstrap, account recovery, cryptographic protection, TLS, image trust, and database access | Plaintext token/codes/keys stay in browser memory, offline owner storage, or least-privilege host files as applicable; only hashes or consumption state enter PostgreSQL | Never application-exported. Recovery plaintext is shown once. Combined encrypted database-plus-key recovery and restored sign-in are **evidence-blocked** |
| Recovery client IP used by the limiter | Transient network metadata | Partition recovery attempts after trusted-proxy validation | In process only; not written to identity audit, logs, telemetry, or export | Discarded with limiter state. **Implemented/current** |
| Open Loops title, details, due time, status, timestamps, source references, owner | User content; potentially highly sensitive | User-directed task/open-loop management | Tenant-scoped PostgreSQL and authenticated API/UI; local deterministic assistant by default; selected BYOK exposure is described below | Explicit list, complete, export, and delete paths exist. Numeric retention is not approved. **Implemented/current** |
| Assistant request, typed tool schema/arguments, provider response and usage | User/model interaction content plus operational usage | Prepare an exact task proposal and account for bounded provider use | Deterministic provider is local. When BYOK is deliberately enabled, the current request and allowlisted tool schema cross to exactly one configured endpoint/model; provider output is untrusted | Andreja does not currently claim durable assistant transcript storage. Provider retention is external and must be disclosed accurately. Live paid evidence is blocked. **Implemented/current local path; gated BYOK activation** |
| Proposal payload/diff/digest, purpose, actor/tenant, source/provenance, expiry, version, idempotency key and transition outcome | Sensitive consequential-action data; minimized audit metadata | Let the user inspect a proposed write, bind confirmation, prevent replay/duplicates, and explain the result | Durable proposal and audit state in PostgreSQL; task content may appear in canonical proposal payload/diff but not in content-minimized transition audit | Rejection/expiry creates no task. Exact retention remains unapproved. **Implemented/current** |
| Task, proposal, and identity audit facts: actor/resource IDs, operation, outcome, source, time | Personal operational metadata | Security, accountability, idempotency, lifecycle explanation, and incident investigation | PostgreSQL; content-minimized by contract; no task title/details, prompt/response, credential, or recovery plaintext | Deletion may retain only separately approved minimal continuity; no immutable-personal-data claim. Retention schedule is **evidence-blocked** |
| Provider/model name, input/output units when reported, duration, result class, retry/tool counts | Content-free usage/financial metadata | Budget stop, reliability evidence, and local operations | App/telemetry paths under operator control; no prompt, response, task content, raw user/principal ID, credential, path, authorization value, or provider error body | Process-local budget counters are not billing reconciliation. Numeric spend and retention remain **evidence-blocked** |
| OTLP traces, metrics, and fixed logs: route template, method, status, protocol, scheme, result/count/timing fields | Operational metadata; can become personal if misconfigured | Local health, reliability, security, and evidence | Allowlisted to the operator-configured OTel endpoint; current runbook uses a local collector. Remote export is not approved | Content suppression and canary evidence exist; remote destinations and retention are **future/gated** |
| BYOK endpoint/model, authored provider/retention disclosure, numeric limits, `credential://` handle, exact secret-file path | Restricted configuration metadata | Select and constrain one operator-approved provider profile | Host configuration; credential value is read only from the mapped least-privilege file and sent only in the outbound authorization field | Handle/path/value are excluded from usage, telemetry, exports, errors, manifests, skill calls, and support evidence. **Implemented/current seam; live external use gated** |
| Skill/channel manifests, permissions, purpose, capabilities, data classes, disclosure ceiling, retention/help/integrity metadata; local invocation fixtures | Configuration and authorization metadata; fixture content only | Define fail-closed extension boundaries | In-process first-party skill and deterministic fixtures; no live third-party code or connector | No production connector, OAuth token, webhook, sync, publish, cache, or durable channel state. **Implemented/current skill; contract-only channel** |
| Grants, bilateral consent, disclosure level, share audit, signed peer-envelope metadata | Sensitive relationship/authorization metadata; no shared payload in audit | Prove purpose-scoped, least-disclosure sharing contracts | Deterministic in-memory fixtures only | No durable grant/consent/share tables, listener, discovery, relay, trust bootstrap, or live traffic. Imported grants would remain inactive. **Contract-only** |
| Semantic assertions, subject/contact references, value, sensitivity, epistemic/review state, confidence, minimized provenance/digests, lineage, retention/export/delete policy | Personal or highly sensitive profile and inference data | Prove inspectable semantics, provenance, review, and lifecycle contracts | Deterministic in-memory ledger and serialization fixtures only; relational domain data remains authoritative | No durable semantic store, embeddings, graph database, remote context retrieval, live model use, or live sharing. Sensitive inference is export-denied by default. **Contract-only** |
| Application export manifest/archive: supported records, audit, settings, provenance, checksums, exclusions, reauthorization instructions | Sensitive portable user data | User-controlled portability to a clean instance | Operator creates and encrypts the archive; current CLI uses authenticated AES-256-GCM packaging and a separately supplied key | Clean-instance round trip passed with synthetic data. Credentials, passkeys, recovery material, provider tokens, keys, caches, telemetry, secrets, and sensitive inference are excluded. **Implemented/current** |
| PostgreSQL dump, key/config inventory, image/schema/config versions, recovery checksums | Restricted recovery set | Disaster recovery and safe migration/update | Separate operator-controlled artifacts and destinations | Database-only restore passed, but encrypted combined custody, matching key recovery, and restored sign-in did not. **Evidence-blocked** |
| Public/help content and local search query | Public content; query may reveal interest | Explain approved public information and find it locally | Current prototype is loopback/private-review only; query remains in browser memory | No public hosting, analytics, feedback, third-party scripts, or remote search is authorized. Historical Pages exposure is contained but third-party copies/logs may remain. **Future/gated** |
| Feedback/support submission, contact details, diagnostics, attachments, and personal data about non-user subjects | Potentially sensitive user/support and third-party content | Future intake and remediation | No intake is deployed; the Phase 0 lifecycle framework is complete, but privacy/legal gate #155 remains open and unapproved | Purpose, fields, retention, submitter and non-user-subject rights/proof/conflict handling, redaction/deletion, accidental-publication remediation, and provider terms require Cyrus's explicit recorded approval before collection. Issue closure is not approval. **Future/gated** |

## Purposes, minimization, and access

The current allowed product purpose is the owner-directed Open Loops flow:
authenticate, prepare a task proposal, review it, confirm or reject it, persist it,
list/complete/delete it, and export supported data. The server re-evaluates
authorization and policy at the API, proposal, skill, and domain-write boundaries.
Tenant, app-user, and principal IDs remain distinct. Composite PostgreSQL
constraints and two-tenant negative tests provide an independent isolation layer.

Access is least privilege:

- the browser uses typed HTTP contracts and cannot bypass server authorization;
- the first-party skill receives an invocation context, typed arguments, and the
  effective disclosure, not secrets, `DbContext`, unrestricted network/filesystem,
  provider SDK objects, or a service locator;
- channel and peer contracts do not activate a connector or remote listener;
- telemetry receives allowlisted operational fields, never product content;
- application export includes declared user data and minimized provenance but not
  credentials or host authority; and
- operator filesystem/database access is operational custody, not a product grant.

Any new purpose, data class, recipient, model exposure, connector scope, sensitive
inference, or retention behavior requires an updated inventory, tests, and human
approval before activation.

## Local, provider, and model exposure

With the deterministic provider, task preparation stays inside the app process and
operator-controlled PostgreSQL. Andreja has no cloud dependency for normal local
identity, task, audit, export, backup, or restore operation.

Enabling the OpenAI-compatible BYOK adapter is a deliberate operator decision. The
authenticated UI must identify the selected provider/model and display an accurate
operator-authored recipient and retention disclosure. Only the current request and
the already intersected typed-tool schema are sent to the exact allowlisted
endpoint. The provider receives no task database, application export, passkey,
recovery material, raw identity ID, or credential value in the JSON body. Andreja
cannot verify or erase provider-side retention; the operator must disable the
profile when terms, endpoint, model, purpose, or approved budget changes.

Provider output, connector content, imported archives, peer payloads, and semantic
extensions are untrusted data, never instructions or authority. Model access to
other classes is deny-by-default. A model cannot confirm proposals, grant itself
permissions, activate sharing, or write tasks directly.

## Identity, passkeys, recovery, and shared devices

Local identity does not require email, phone, password, cloud identity, biometric
collection, or authenticator attestation. Biometric/PIN verification and passkey
private keys remain with the browser/authenticator. Passkeys are not automatically
claimed as hardware-backed or MFA.

Bootstrap and recovery use exact HTTPS origin/RP checks, trusted-proxy validation,
bounded inputs, generic failures, rate limits, one-time state, transaction locks,
short-lived protected cookies, security-stamp rotation, and content-free audit.
Recovery removes old passkeys, revokes old codes, invalidates sessions, and returns
new plaintext codes once. If every passkey and recovery code is lost, the current
system stops; it has no password, email, header, or operator bypass.

On shared or coerced devices, Secure/HttpOnly/SameSite cookies and recent-auth checks
reduce but do not eliminate shoulder-surfing, host compromise, unlocked-session, or
coercion risk. Users/operators must use trusted devices, sign out, protect offline
recovery material separately, and avoid entering sensitive household content where
another device user can inspect it.

## Sensitive life, household, and non-user data

The roadmap includes future family, relationship, health, wellbeing, finance,
trading, benefits, household, travel, and social capabilities. They are not current
skills or connectors. A general task can nevertheless contain this information now,
so task, proposal, backup, and export handling must assume high sensitivity.

- Andreja must not diagnose, prescribe, change medication, execute a trade, move
  money, file a benefit/insurance action, or publish/socially message autonomously.
- Contacts and household members are not verified users, consenting parties, or
  shared tenants merely because the owner mentions them.
- Data about a partner, family member, child, colleague, or other non-user is
  minimized to the owner's legitimate task purpose; no shadow dossier, identity
  resolution, cross-tenant merge, sensitive inference, or model/sharing exposure is
  allowed by default.
- Child-directed use, parental surveillance, age assurance, guardian authority,
  household delegation, emergency access, and competing household rights have no
  approved current design. Do not market to children or activate a child/household
  workflow until a dedicated safety, privacy, legal, consent, accessibility, and
  abuse review is approved.
- Caregiver status does not establish authority over an elder or dependent adult.
  Future finance, benefits, trading, health, and household features must not enable
  asset diversion, isolation, or unilateral care/medication control; capacity,
  supported decision-making, consent, safeguarding, appeal, and revocation require
  a dedicated approved review.
- Sensitive semantic inference, embeddings, and cross-domain optimization remain
  gated. User review and an explanation do not by themselves make a harmful or
  discriminatory inference acceptable.

## Skills, channels, connectors, and extension data

The first-party Open Loops skill is current. Its manifest declares publisher,
purpose, capabilities, operations, data class, disclosure ceiling, execution mode,
retention/help/compatibility, and integrity metadata. One evaluator intersects the
manifest with tenant/principal context, active grant and consent where applicable,
purpose, capability, operation, data class, time, revocation, and disclosure.
Manifests declare capabilities; they do not create authority.

The channel host, connector metadata, peer channel, and marketplace-facing fields
are contract-only. Before any channel becomes live it needs a separately reviewed
data-flow map, official API and account terms, least OAuth scopes, token isolation,
provider retention/residency, webhook/replay handling, cache/purge rules, model
exposure, read-only/draft-first behavior, disconnect/reauthorize/delete/export UX,
support/runbook, cost envelope, and adverse tests. Connector credentials remain
adapter-owned and unavailable to skills.

Third-party executable skills, unrestricted extension code, ambient network or
filesystem access, and marketplace distribution remain future/gated. Signature or
publisher metadata is not enough; review, isolation, revocation, compatibility, and
incident handling must also be proven.

## Provenance, audit, consent, and sharing

The detailed [semantic provenance and sharing
rules](semantic-graph.md#provenance-time-review-and-sharing-rules) govern the
contract-only semantic seam. Provenance records who or what produced a
proposal/assertion, when, for which
purpose, and by which method/version. It uses stable references and digests rather
than duplicating raw prompts, connector payloads, or deleted source content.
Unverified, user-stated, observed, and inferred information remain distinct.
Correction, rejection, expiry, retraction, and deletion invalidate downstream use.

Audit records are append-only for the operation they evidence, not an authorization
source or a license to retain personal content forever. Current task/proposal and
identity audits are content-minimized. Share and semantic audits are contract-only.
Any audit retention must be purpose-bound and compatible with deletion; the project
makes no immutable-personal-data or compliance-log claim.

A `Grant` is versioned, purpose-bound, scoped, operation-limited, disclosure-limited,
time-bounded, revocable, and tied to bilateral consent. Consent is an explicit
decision timeline, not a generic boolean. Policy can reduce but never widen
disclosure. Revocation and expiry must be checked at use time. The current fixtures
send no live content and confer no live authority.

Application export never means publish, peer discovery, model consent, or resumed
sharing. Imported grants remain inactive until identities are resolved and consent
is revalidated. Live federation needs approved trust bootstrap, key management,
transport, abuse handling, interoperability, deletion/revocation propagation, and
data-subject UX.

## Retention, deletion, and export

No numeric product-content, audit, provider, backup, or telemetry retention schedule
has been approved. This blocks Phase 1A exit and prevents any promise that data is
retained for a specific period.

| Mechanism | Current behavior | Limitation / required gate |
|---|---|---|
| Task delete | Authenticated two-step user action, version check, tenant/principal policy, idempotency, and content-free audit | Backups and any prior provider disclosure are separate copies; approved retention/deletion-replay policy remains missing |
| Account/identity delete | Narrow privacy contract requires credential and recovery-row deletion with only approved minimal audit continuity | Complete reviewed product flow and retention decision are not claimed by current evidence |
| Semantic delete | In-memory contract supports hard delete or configured tombstone, dependency invalidation, and minimized audit | No durable semantic/derived store; backup/replica/index purge proof remains gated |
| Provider deletion | Operator can pause/revoke the credential and must follow provider terms | Andreja cannot verify provider retention or deletion; no guarantee is made |
| Application export/import | Versioned, encrypted, integrity-checked clean-instance archive; dry run before commit; declared exclusions and reauthorization | It is not a disaster-recovery image and grants no credentials, external authority, or sharing activation |
| PostgreSQL recovery | Logical dump/restore tooling and database-only rehearsal exist | Database plus matching Data Protection/configuration history, encrypted custody, and restored sign-in remain blocked |
| Public-site caches/logs | Active Pages origin was disabled and stable 404 evidence recorded | Third-party/client caches and provider request logs may persist outside repository control |

Exports are sensitive. The operator must encrypt them, control destination and
access, verify checksums, avoid source-control/support upload, and securely remove
unneeded copies. Import is data-only, deny-by-default, rejects unsafe paths or
checksums, requires a clean instance, performs a write-free dry run, serializes
commit by import ID, and never imports reusable secrets.

## Telemetry, support, feedback, and public surfaces

Current telemetry is optional and operator-configured. The app removes all
non-allowlisted span attributes and records fixed, low-cardinality operational
fields. Task text, prompt/response, tool arguments, connector content, credentials,
authorization values, cookies, passkeys, recovery material, provider error bodies,
and raw user/principal identifiers are prohibited. Evidence with synthetic canaries
passed, but misconfiguration, new instrumentation, and destination behavior remain
review obligations.

Do not attach database dumps, exports, screenshots with user content, prompts,
provider responses, connector payloads, secrets, recovery material, raw identifiers,
or private host details to issues, pull requests, feedback, or support. Use
content-free time, operation, result class, version, and synthetic reproduction
data. The Phase 0 feedback/support framework decision is recorded, but its
privacy, security, platform/vendor, and operational-readiness gates remain open
and no tenant-less intake is deployed. Closing a successor issue or merging its
documentation does not approve its package; Cyrus must record the applicable
decision explicitly.

The public/help-site artifact is not the authenticated app. It must have no app
cookies, tokens, product data, privileged route, analytics, remote search, feedback
form, sponsorship, ads, or third-party scripts by default. It remains
loopback/private-review only; no deployment, preview, host, domain, CDN, DNS, or
public claim is authorized.

## Operator duties and incident handling

The operator must:

1. keep the host, browser, TLS proxy, PostgreSQL, container runtime, and dependencies
   patched and access-controlled;
2. use exact HTTPS origin/RP and trusted-proxy settings; never add a trust-all
   certificate callback;
3. protect and rotate bootstrap, recovery, database, Data Protection, signing, TLS,
   and BYOK material in separate least-privilege custody;
4. keep provider/model/recipient/retention disclosures accurate and stop the profile
   if terms or endpoints change;
5. set no positive external-model budget without recorded approval;
6. encrypt and restore-test database, key/config, backup, and export artifacts; keep
   application portability separate from disaster recovery;
7. review telemetry destinations, fields, retention, and access; use synthetic
   canaries only;
8. honor deletion, revocation, expiry, and provider disconnect across active stores
   and approved backup retention;
9. investigate with minimized evidence and rotate/revoke suspected secrets without
   copying them into reports; and
10. stop on unexplained egress, isolation failure, content leakage, lost key history,
    stale consent, inaccurate disclosure, unknown spend, or an unapproved recipient.

When the operator is also an abusive or coercive household actor, application-layer
controls cannot protect other people from root, database, key, backup, or telemetry
access. Multi-person household features therefore require an operator-independent
safety and revocation design before activation.

## Evidence, blockers, and stop conditions

Current evidence includes deterministic unit/architecture suites, disposable
PostgreSQL isolation and portability tests, real-browser passkey and recovery flows,
proposal/idempotency tests, skill/channel/peer/semantic conformance fixtures,
content-suppressed local OTel evidence, and offline/no-egress evidence. See
[evidence run #44](phase-1a/evidence-44.md) for exact versions and exclusions.

The following remain blocking:

- separately trusted OCI signing/verification evidence;
- encrypted combined PostgreSQL, Data Protection key history, and configuration
  recovery with restored passkey sign-in;
- update and rollback against a genuine second approved signed revision;
- approved numeric SLO, RPO, RTO, retention, and external-model spend envelopes;
- final residual-risk acceptance and acceptance/amendment of Proposed ADRs; and
- any future processor/recipient, connector, federation, managed host, public site,
  support intake, child/household workflow, or sensitive inference review.

Stop data entry, exposure, processing, or release when tenant isolation fails,
authentication/recovery material leaks, deletion/export exclusions are false,
telemetry contains prohibited content, provider disclosure is inaccurate, consent
or purpose cannot be established, a high/critical privacy risk lacks acceptance, or
required evidence is unavailable. Preserve only minimized incident facts; do not
collect additional personal data merely to investigate.

## Change and review process

Review this file whenever a data class, purpose, recipient, model/provider, identity
factor, tenant boundary, skill/channel capability, sharing contract, persistence
store, telemetry field/destination, retention/deletion/export rule, backup path,
support/public surface, or legal posture changes. Material trust-boundary or
data-ownership changes may require plan re-ratification under
[ADR 0000](adr/0000-plan-ratification.md). Ordinary updates use a reviewed issue and
pull request, named architecture/privacy/security/abuse/accessibility/operations
reviewers, evidence links, residual-risk owner, and explicit stop conditions.
