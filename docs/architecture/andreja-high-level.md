# Andreja high-level architecture and data flows

![Andreja high-level architecture and data flows](andreja-high-level.svg)

Use the [editable Excalidraw source](andreja-high-level.excalidraw) for changes.
The [PNG export](andreja-high-level.png) is available for viewers that do not
render SVG. This view summarizes the ratified [platform plan](../plan.md) and
[ADRs 0001–0005](../adr/0001-phase-1a-modular-boundaries.md); those documents
remain normative.

## Scope and notation

This is an evidence view of `origin/main` at commit `51b4eb4` on 2026-08-24.
"Current" means implementation or operational evidence is present in that
snapshot; it is not a production-readiness or availability claim.

| Notation | Meaning |
|---|---|
| **CURRENT PHASE 1A** — green, solid | Implemented walking-skeleton component or exercised local operational path. |
| **CURRENT CONTRACT-ONLY** — amber, dashed | Application-owned contract and deterministic local conformance proof; no live external service, listener, connector, or durable future-state schema is implied. |
| **FUTURE / GATED** — purple/red, dotted | Phase 1B+ or separately approved work. Technology, topology, and provider choices remain undecided unless an ADR says otherwise. |
| **TB1–TB6** | Named trust boundary. Color supplements the boundary label; it is never the only indicator. |
| **F1–F10** | Numbered flow described below. Solid flows are current; dotted flows are future or contract-only. |

The diagram deliberately omits detailed classes, infrastructure SKUs, cloud
topology, endpoints, tenant/user examples, credentials, recovery material, and
provider endorsements.

## Trust boundaries

| Boundary | Responsibility |
|---|---|
| **TB1 — client/browser** | The responsive Blazor client uses typed HTTP contracts. Future native or third-party clients must cross the same server-authoritative API boundary. The separately deployed public/help site has no app cookies, tokens, or product-data access. |
| **TB2 — authenticated app** | `Andreja.AppHost` composes the modular monolith, HTTP API, modules, and adapters. Co-hosting does not permit UI components to bypass HTTP authorization and validation. |
| **TB3 — tenant/principal/purpose isolation** | Scoped context, policy, access-scoped projections, and tenant-aware PostgreSQL constraints enforce ownership and purpose. IDs are not capabilities. |
| **TB4 — adapter/provider** | Framework, persistence, identity, assistant-provider, connector, and telemetry integrations remain outer adapters. Credentials do not enter modules, manifests, prompts, exports, or telemetry. |
| **TB5 — local operator custody** | PostgreSQL, key history, file-backed configuration/secrets, local telemetry, exports, and recovery sets remain under operator control. Database, keys, and configuration have distinct backup and portability roles. |
| **TB6 — external/peer** | Model providers, future connectors, peer instances, support intake, and backup destinations are separately trusted systems. Inbound content is untrusted data; every outbound user-content flow needs an explicit purpose and policy. |

## Numbered flows

| Flow | Path and controls |
|---|---|
| **F1 — passkey sign-in** | Browser -> authenticated identity adapter -> scoped tenant/principal context. HTTPS and WebAuthn origin checks apply. Passkeys, bootstrap/recovery material, and reusable credentials are excluded from logs, telemetry, database exports, and application exports. |
| **F2 — assistant request** | Typed client/API -> assistant runtime -> user-configured BYOK provider or deterministic local fake. User content may cross TB4 only for the configured provider and disclosed purpose; encrypted credential handles remain with the adapter. |
| **F3 — typed tool proposal** | The provider can select only an allowlisted typed tool. `ISkillHost` validates schema, grant/capability/purpose policy, and returns an exact proposal with provenance and expiry. It does not write. |
| **F4 — human confirmation** | The typed API presents the proposal. Confirmation binds proposal version, actor, tenant, and idempotency key. Rejection or expiry produces no domain write. |
| **F5 — persistence and audit** | The Open Loops use case re-evaluates policy, writes the task to tenant-scoped PostgreSQL, and records content-minimized audit/idempotency evidence transactionally. |
| **F6 — channel intake** | Future external input -> channel adapter/`IChannelHost` -> shared policy evaluator. Capability, purpose, operation, data class, grant, consent, and disclosure ceilings intersect fail-closed. No live connector is present in Phase 1A. |
| **F7 — application portability** | Versioned export/import archive with checksums and a write-free dry run. Supported user data and minimized provenance may move; credentials, passkeys, provider handles, Data Protection keys, caches, and external authority do not. |
| **F8 — backup and restore** | PostgreSQL logical backup and a separately inventoried key/configuration set are encrypted and restore-tested together. This operator recovery path is distinct from application portability. |
| **F9 — telemetry suppression** | The app exports allowlisted, low-cardinality operational fields to the local OpenTelemetry Collector. Task text, prompts, responses, tokens, identifiers, credentials, and connector content are prohibited. |
| **F10 — purpose-scoped sharing/proposals** | Current local contracts cover grants, bilateral consent, disclosure levels, minimized audit, and signed peer envelopes. A future peer may submit a purpose-scoped proposal; the owning tenant remains authoritative. Phase 1A has no live peer listener, discovery, relay, or federation traffic. |

## Components, stores, and custody

- `Andreja.AppHost` is the composition root and one current deployable; the
  diagram does not predict a service split.
- Current modules cover identity/tenancy, assistant runtime, Open Loops,
  proposals, first-party skill execution, portability, audit/idempotency, and
  content-safe observability.
- Channel, sharing/federation, and semantic-profile/provenance seams have local
  contracts/conformance fixtures. Their dashed notation prevents those seams
  from being mistaken for live integrations or durable future tables.
- PostgreSQL is the current tenant-scoped relational store. Attachments outside
  the database are shown as gated because Phase 1A does not select an object
  store or claim a live attachment path.
- Data Protection/encryption key history and operator secrets/configuration are
  outside the image and database. They are not application-export content.
- The public/help site and optional tenant-less feedback intake are separately
  deployed, future-gated surfaces with no privileged route to user data.

## Extension seams

| Seam | Contract intent and present status |
|---|---|
| `IAssistantProvider` / `IAssistantSession` | Current provider-neutral capability negotiation, typed-tool allowlist, structured events, cancellation, and content-free usage. |
| `ISkillHost` / `SkillManifest` | Current first-party, typed, policy-evaluated skill boundary. No unrestricted network, filesystem, secrets, `DbContext`, or service locator access. |
| `IChannelHost` / `ChannelManifest` | Current local conformance seam; external channel adapters are future-gated. |
| Identity/OIDC adapters | Built-in ASP.NET Core passkey adapter is current. Optional BYO OIDC and managed identity choices require separate approval/evidence. |
| Typed API clients/contracts | Current reversible client boundary for responsive web and future clients. Contracts contain no EF, framework, or provider SDK types. |
| Persistence/portability adapters | Current PostgreSQL and versioned application export/import boundaries. No second relational provider or object store is claimed. |
| OpenTelemetry exporters | Current standards-based, content-suppressed local export seam. Remote destinations require a separate purpose/privacy/retention review. |
| `IPeerChannel` / signed envelopes | Current local conformance contract; live federation, discovery, trust bootstrap, relay, and interoperability are future-gated. |

## Ownership, review, and revalidation

**Owner:** Core Platform and Architecture. **Decision owner:** Cyrus.

Revalidate and update the Excalidraw source plus both exports whenever a pull
request changes any trust boundary, module/deployable, durable store or custody
rule, external data flow, status classification, or extension contract. The PR
author must complete this checklist:

- [ ] Spock: architecture, current/future status, and no accidental topology claim.
- [ ] Tuvok and Deanna Troi: trust, authorization, secret, consent, and privacy flows.
- [ ] Seven of Nine and Jett Reno: assistant, skill, channel, peer, provider, portability, and telemetry seams.
- [ ] Data: source/render consistency and flow-to-document mapping.
- [ ] Jadzia Dax: label clarity, color-independent legend, contrast, normal-width readability, and no clipped/invisible text.
- [ ] Open the `.excalidraw` source at <https://aka.ms/excalidraw>; inspect at fit-to-screen and 100% zoom.
- [ ] Regenerate and verify artifacts:

  ```powershell
  python scripts\docs\generate_architecture_diagram.py --render-png
  python scripts\docs\generate_architecture_diagram.py --check
  python .github\scripts\check_docs_consistency.py
  ```

The generator derives the editable source and SVG from one model, embeds the
source SHA-256 in the SVG, and checks the committed PNG dimensions. A manual
Microsoft-internal Excalidraw review remains required because byte-level checks
cannot detect visual clipping or misleading layout.
