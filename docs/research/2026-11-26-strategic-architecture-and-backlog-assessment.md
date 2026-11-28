# Andreja (`Jamula/Andreja`) — Strategic Code, Architecture, Technology & Backlog Assessment

- **Assessment date:** `2026-11-26`
- **Repository baseline:** `Jamula/Andreja` (`main` commit `94725df`)
- **Authority & governance boundary:** This dated assessment is a reference analysis and execution planning guide. It does **not** amend `docs/plan.md`, modify any SHA-256-locked Phase 0 status artifact, ratify Proposed ADRs (`0006`, `0007`, `0011`, `0012`), authorize cloud infrastructure or spend, or declare any milestone exit complete.

---

## 1. Executive Understanding: What Andreja Is Building and Why

### 1.1 The Problem Worth Solving ("Why")
Modern productivity, inbox, and AI assistant ecosystems force an unacceptable choice:
- **Convenience through data surrender:** Hand over custody of personal email, calendars, financial notes, family coordination, and behavioral telemetry to cloud vendors whose business models drift toward lock-in, model training, or attention monetization; **or**
- **Privacy through fragmentation:** Maintain disconnected local notes, scripts, and task lists with no cross-domain continuity, no reliable follow-through, and no consent-safe way to collaborate with a spouse, family member, or travel group.

### 1.2 The Platform Thesis ("What")
Originating on `2026-08-22` for Customer Zero (`@cyrusjamula`) and ratified as a platform baseline on `2026-08-23` (`ADR 0000`), Andreja is a **privacy-first, user-owned personal chief-of-staff and capability ecosystem** governed by five architectural invariants:

1. **Explicit Human Agency (`Proposal -> Policy -> Confirmation -> Persistence -> Audit`):** AI providers, skills, and peers *never* write durable user state directly—assistant, skill, and peer writes require structured proposals with exact before/after diffs, policy evaluation, and explicit human confirmation (`docs/plan.md`). In current `M02` code (`OpenLoopsTaskApplication`), assistant task creation (`open-loops.create-task`) enforces canonical, SHA-256 digest-verified, 10-minute expiring proposals, whereas direct authenticated user mutations (`CompleteAsync`, `DeleteAsync`) invoke `IOpenLoopsTaskStore` directly under policy, optimistic-concurrency, idempotency, and audit checks.
2. **Self-Host First, Cloud-Portable by Contract:** One modular monolith deployable locally on digest-pinned rootless OCI images + PostgreSQL 17, backed by AES-256-GCM encrypted portability archives (`Andreja.Portability.Cli`), and designed to run unchanged across managed SaaS (`M08`), bilateral-consent federation (`M09`), and an isolated MCP/HTTP skill marketplace (`M10`).
3. **Offline Default & `$0`-Default Provider-Neutral AI:** Core workflows run 100% offline via a deterministic local provider; external BYOK inference requires explicit allowlisting and defaults to `ApprovedExternalTotalUnits = 0`.
4. **Zero-Trust Passkey Identity & Composite Tenancy:** WebAuthn passkeys + 210,000-iteration PBKDF2 offline recovery codes, GUIDv7 IDs, distinct `TenantId` / `AppUserId` / `PrincipalId` contexts, composite PostgreSQL constraints, and content-suppressed **OpenTelemetry (`OTel`)** (`CaptureContent = false`).
5. **Executable, Evidence-Gated Governance:** Dual-hash plan ratification (`ADR 0000`), 12 SHA-256-locked status artifacts (`DocsConsistencyChecker`), three strictly separated financial ledgers (`docs/cost-model.md`), Squad multi-agent reviewer lockouts, and explicit human release gates (`M01.99`–`M15.99`).

---

## 2. Deep Codebase, Test, Issue & Milestone Audit

### 2.1 Quantitative Codebase X-Ray (`100,463` Text Lines)

| Layer / Directory | Files | Text Lines | Share | Technical Reality |
|---|---:|---:|---:|---|
| `.github/` + `.squad/` | 138 | 23,999 | 23.9% | 12 CI workflows, `open-decisions` canvas extension, `review-gate-app.js`, 9-agent Squad charters/policies |
| `docs/` | 84 | 23,823 | 23.7% | Master plan (`1,928` lines), 13 ADRs (`0000`–`0012`), privacy/threat/legal/FinOps baselines, 19-skill & 23-channel catalogs |
| `src/Adapters/` | 78 | 14,887 | 14.8% | `7,096` lines handwritten adapters (Postgres, ASP.NET Passkeys, custom OpenAI transport, OTel) + `7,791` lines EF migrations |
| `tests/` | 42 | 14,537 | 14.5% | `373` solution tests (`Unit`, `Architecture`, `PublicSite`, `Docs` — passing in `3.76s`) + `22` disposable PostgreSQL tests |
| `scripts/` | 23 | 5,922 | 5.9% | Syft/Grype/Trivy/Cosign OCI supply-chain verification, backup-gated SQL migrations, Edge WebAuthn & offline E2E harnesses |
| `src/Docs/` + `src/PublicSite/` | 13 | 5,571 | 5.5% | Custom C# doc-hash checker (`1,272` LOC), Excalidraw/SVG/PNG chunk validator (`2,580` LOC), custom static site generator (`813` LOC) |
| `src/AppHost/` + `Portability.Cli` | 63 | 5,257 | 5.2% | Minimal HTTP APIs, Blazor Interactive Server UI, `CircuitDelegationTokenService`, health probes, AES-256-GCM portability CLI |
| **`src/Modules/` + `Contracts`** | **71** | **6,467** | **6.4%** | **All business domain & platform contracts** (`Identity`, `OpenLoops`, `Execution`, plus in-memory conformance fixtures) |

> **Core Diagnostic Finding — Meta-Engineering Inversion:** Approximately **59% of repository lines** enforce governance, documentation hashes, diagrams, CI gates, and supply-chain evidence, whereas **6.4%** implements domain modules and contracts. The engineering quality of what exists is exceptionally high (`TreatWarningsAsErrors=true`, `AnalysisMode=All`, zero test skips), but developer and AI-credit velocity must now pivot decisively from *governance scaffolding* to *Customer Zero daily-driver product depth*.

---

## 3. Architecture & Component Review

### 3.1 Standout Architectural Strengths
- **Reflection-Enforced Clean/Onion Core (`DependencyDirectionTests.cs`):** `Andreja.Modules` is restricted to `Andreja.Platform.Contracts` and 10 BCL `System.*` assemblies—zero ASP.NET Core, EF Core, Npgsql, or provider SDK leakage.
- **Reversible Blazor-over-HTTP Pattern:** `Home.razor` injects only `IOpenLoopsApiClient`, calling versioned Minimal APIs (`/api/v1/open-loops`) via single-use, 60-second `IDataProtector` nonces (`CircuitDelegationTokenService`) without capturing stateful `HttpContext` in pooled HTTP handlers.
- **Crash-Consistent Proposal & Identity Custody:** Microsecond timestamp canonicalization + constant-time SHA-256 payload digest checks (`OpenLoopsTaskApplication`), atomic proposal+task+receipt+audit transactions (`PostgreSqlProposalStore` with fault injection), `IsolationLevel.Serializable` + advisory-locked (`0x414E4452454A41`) WebAuthn bootstrap (`LocalIdentityOperations`), and `SensitiveBuffer` memory zeroing.
- **Hardened OCI & OTel Posture:** Rootless Ubuntu Noble chiseled image (`1654:1654`), `read_only: true`, `cap_drop: ALL`, per-request CSP nonces, and `ContentSuppressingActivityProcessor` stripping non-allowlisted telemetry attributes before OTLP export.

### 3.2 Structural Weaknesses & Technical Risks to Address Early
1. **The Three-Way Trust-Boundary Deadlock (`Proposed ADR 0012` vs. `Story 1` & `Story 2`):**
   - Current `M02` (`Phase 1A`) code stores `OpenLoopTask.Title` and `Details` as server-readable PostgreSQL text and forwards prompts to `OpenAiCompatibleTransport`.
   - Cyrus's `2026-10-11` direction (`#2` / `Proposed ADR 0012`) requires owner-device-only encryption with *no* server plaintext, *no* external plaintext LLM calls, and *no* other-human access—blocking real-data dogfooding, Cloud Email Triage (`M03.02`), Group Travel (`M03.03`), and Federation (`M09`) until reconciled (`[M01.02]`).
2. **Overloaded `AndrejaIdentityDbContext` (Shared-`DbContext` Coupling):**
   - PostgreSQL schemas are already separated across `identity` (`AndrejaIdentityDbContext.cs:62`), `open_loops` for tasks, audits, receipts, and proposals (`AndrejaIdentityDbContext.cs:367–536`), and `portability` for imports (`AndrejaIdentityDbContext.cs:592`), yet `AndrejaIdentityDbContext` couples all module models, migrations, and tenant guards inside one shared `DbContext`. Before `M04`–`M15` add skills and channels, persistence should split into module-scoped `DbContext`s sharing one `NpgsqlDataSource` (`[M02.03]`) while preserving existing schemas, composite `(TenantId, ...)` constraints/tenant guards, and single-transaction atomic confirmation (`PostgreSqlProposalStore.ConfirmAsync`).
3. **Process-Local State in Multi-Instance Paths:**
   - `CircuitDelegationTokenService.consumedNonces` and `AssistantUsageBudget` are in-memory structures—losing external token accounting across restarts and breaking horizontal scale-out (`M08`).
4. **Contract-Only Seams vs. Usable Daily UX:**
   - `Skills`, `Channels`, `Sharing`, and `Semantics` currently use in-memory conformance hosts (`InMemorySkillHost`, `InMemoryChannelHost`, `InMemorySemanticAssertionLedger`). `OpenLoops` supports only a single-turn 500-character prompt (`CompleteAsync` is non-streaming) without `Inbox / Today / Upcoming / Waiting` views, manual task creation/editing, search, or recurrence (`[M04.02]`).

---

## 4. Strategic Directives: `.NET 11` (`net11.0`) & End-to-End OpenTelemetry (`OTel`)

### 4.1 Adopting `.NET 11` (`net11.0`) Where Supported (`[M02.01]`)
- **Upstream Readiness Verified (`2026-11-26`):** `.NET 11.0.0` reached General Availability on `2026-11-10` (Standard Term Support through `2028-11-14`), shipping C# 15, EF Core 11 query optimizations, ASP.NET Core 11 Blazor/Identity improvements, and `.NET MAUI 11`.
- **100% Core NuGet GA Compatibility:** Live NuGet registry checks confirm every runtime dependency in `Directory.Packages.props` already ships stable `.NET 11` GA packages:
  - `Npgsql.EntityFrameworkCore.PostgreSQL` **`11.0.0`**
  - `Microsoft.AspNetCore.Identity.EntityFrameworkCore` **`11.0.0`**
  - `Microsoft.EntityFrameworkCore.Design` **`11.0.0`**
  - `Microsoft.AspNetCore.Mvc.Testing` **`11.0.0`**
  - `Microsoft.Extensions.AI` **`11.0.1`**
  - `OpenTelemetry.*` **`1.21.0`**
  - `ModelContextProtocol` **`2.4.0`**
- **Clean Adoption Strategy ("Where Possible"):**
  1. Pin `global.json` to `.NET SDK 11.0.100` (once installed on CI/dev hosts) and set `<TargetFramework>net11.0</TargetFramework>` in `Directory.Build.props`.
  2. Update `Dockerfile` and `supply-chain-policy.json` in lockstep to digest-pinned `mcr.microsoft.com/dotnet/sdk:11.0.100-noble@...` and `mcr.microsoft.com/dotnet/aspnet:11.0.0-noble-chiseled@...` (satisfying `Test-SupplyChainPolicy.ps1`).
  3. Multi-target `net10.0;net11.0` *only* on pure contract assemblies (`Andreja.Platform.Contracts`, `Andreja.Api.Contracts`) if any downstream tool/extension host still runs `.NET 10`.
  4. Plan the routine `.NET 12 LTS` hop in November 2027 well inside `.NET 11`'s November 2028 support window.

### 4.2 End-to-End Content-Suppressed OpenTelemetry (`OTel`) Architecture (`[M02.02]`)
Andreja already establishes a strong OTel baseline in `src/Adapters/Andreja.Adapters/OpenTelemetry/` (`OpenTelemetryServiceCollectionExtensions`, `AndrejaTelemetry`, `ContentSuppressingActivityProcessor`, and `deploy/otel-collector.yaml` over OTLP/gRPC). To make OTel the single observability backbone across all 15 milestones without ever leaking personal data:

1. **Standardized AI & Agent Telemetry (`gen_ai.*` Semantic Conventions):**
   - Wire `Microsoft.Extensions.AI`'s built-in `UseOpenTelemetry(...)` into the outer `IChatClient` adapter with **`EnableSensitiveData = false`** enforced by `Andreja.ArchitectureTests` and `ContentSuppressingActivityProcessor`.
   - Emit standard `gen_ai.client.token.usage`, `gen_ai.client.operation.duration`, model/provider identifiers, and error codes—retiring bespoke provider metric plumbing while feeding the FinOps product-metering ledger (`docs/cost-model.md`).
2. **Persistence, Outbound HTTP & Runtime Instrumentation:**
   - Add `Npgsql.OpenTelemetry` (with SQL statement text suppression where needed), `OpenTelemetry.Instrumentation.Http` (stripping query strings/tokens/paths on external channel calls), and `OpenTelemetry.Instrumentation.Runtime` (GC allocations, threadpool starvation, Blazor circuit counts).
3. **Proposal, Skill & Channel Control-Plane Spans:**
   - Instrument `OpenLoopsTaskApplication`, `ISkillHost`, `IChannelHost`, and `ExecutionAuthorizationEvaluator` with content-free `Andreja.Operations` spans and counters (`proposal_created`, `proposal_confirmed`, `proposal_expired`, `execution_denied` by denial code, `idempotent_replay`).
4. **Zero-Cloud Local & Self-Host Visualization:**
   - Add an optional digest-pinned **`.NET Aspire` Standalone Dashboard** (`mcr.microsoft.com/dotnet/aspire-dashboard`, MIT-licensed, loopback-only, zero external account) alongside `otel-collector` in `compose.yaml` so operators and developers can inspect live OTLP traces, structured logs, and metrics locally.

---

## 5. Technology Reuse Matrix (Stop Building Commodity Plumbing In-House)

| Capability Area | Current In-House Code / Plan | Reuse Standard (MIT / Apache-2.0 Safe) | Velocity & Maintenance Impact |
|---|---|---|---|
| **AI Provider Pipeline** | Hand-rolled `OpenAiCompatibleTransport.cs` | **`Microsoft.Extensions.AI` (`11.0.1`, `IChatClient`)** + **`Microsoft.Agents.AI`** | Backs local loopback (`Ollama` / `Foundry Local`) and opt-in BYOK behind `IAssistantProvider` in `[M02.04]`. Per Accepted `ADR 0009`, `GitHub.Copilot.SDK` in `M02` (`Phase 1A`) is limited to an isolated, credential-free, non-shipping toolchain spike (excluded from the shipping dependency graph); any real Copilot runtime provider remains gated to `M03+` (`Phase 1B+`) after separate approvals and synthetic-canary gates. |
| **Zero-Egress Private AI (`D3`)** | String-stub `DeterministicAssistantProvider` | **`Microsoft Foundry Local` / `Ollama` / `ONNX Runtime GenAI` / `WebLLM`** | Local LLM extraction on NPU/GPU at `$0` token cost eliminates external model exposure (`D3`) for synthetic/local evaluation; real personal-data dogfooding remains conditional on `ADR 0012` prerequisites (exact-plan re-ratification plus reviewed owner-client encryption, content-key recovery, client-release verification, and plaintext migration). |
| **Skill & Channel Protocol** | Custom `ToolFieldSchema` & `ArgumentSchemaValidator` | **Official `ModelContextProtocol` C# SDK (`2.4.0`)** + **`System.Text.Json.Schema`** | Makes `ISkillHost`/`IChannelHost` an MCP policy & proposal broker; taps into standard MCP tools without custom schema validators (`[M02.04]`). |
| **Owner-Device E2EE (`D2`)** | Unimplemented manual key files (`ADR 0012` draft) | **WebAuthn `prf` Extension** + **WebCrypto / Libsodium** | Cross-platform passkey `prf` (Win11 `WEBAUTHN_API_VERSION_8`, Chrome 147+, Firefox 148+, Safari/iOS 18.4+, Android) derives a hardware-backed KEK on login, paired with an offline Emergency Kit (`[M01.02]`). |
| **Consented Group Sharing (`D1`)** | Deadlocked between owner-only & server plaintext | **RFC 9420 MLS (`OpenMLS` / `mls-rs` WASM/FFI)** + **SQLite-WASM (OPFS)** | Personal vaults stay strictly owner-only; Trip Workspaces use isolated MLS group keys with forward secrecy and member revocation (`[M01.02]`, `M03.03`). |
| **Email, Calendar & Recurrence** | Planned custom MIME/ICS & RFC 5545 logic | **`Microsoft.Graph` (Kiota)**, **`Google.Apis.Gmail.v1`**, **`MimeKit`**, **`Ical.Net`**, **`NodaTime`** | Eliminates years of MIME, RRULE, and IANA DST edge-case bugs (`[M04.02]`, `M03.02`, `M07.01`). |
| **OAuth Token Lifecycle** | Custom OAuth engine *(Avoid **Nango**!)* | **`Duende.AccessTokenManagement`** / ASP.NET Core OAuth + AEAD per-grant store | **License safeguard:** Nango uses **Elastic License v2**, prohibiting managed-SaaS resale (`M08`). Keep all runtime dependencies MIT/Apache-2.0. |
| **Orchestration, DB Tests & Jobs** | `5,900` lines of custom PS1/Compose glue | **`.NET Aspire`**, **`Microsoft.Extensions.Http.Resilience`**, **`Testcontainers.PostgreSql`**, **Wolverine / TickerQ** | Single-command `dotnet test` Postgres containers (`[M02.03]`), Polly v8 resilience, and Postgres-backed durable outbox/scheduling (`M05`–`M07`). |
| **Public & Help Site (`#93`)** | Custom C# HTML builder (`src/Andreja.PublicSite`) | **Astro Starlight** or **VitePress** + **Pagefind** (WASM search) | Accessible WCAG-ready static docs + browser-local search with zero custom SSG maintenance while honoring `ADR 0008`. |

---

## 6. Value Proposition & Business Value Evaluation

### 6.1 High-Conviction Value Drivers
1. **"Sovereign Chief of Staff" Solves Peak AI Trust Fatigue:** High-agency professionals, founders, engineers, and families want proactive triage across email, commitments, bills, and life logistics *without* granting black-box agents autonomous write/send authority or training rights. **`Proposal -> Canonical Diff -> Confirm -> Audit`** turns AI safety into a visceral daily UX advantage.
2. **Purpose-Scoped Shared Workspaces Create Natural Multi-Player Virality:** Existing tools overshare whole calendars/docs or fragment plans in chat. Bilateral, expiring, disclosure-leveled workspaces (**Group Travel, Household Operations, Elder Care, Life Events**) naturally invite 2–5 adults per workspace without exposing anyone's private vault.
3. **Proven Bitwarden / 1Password / Obsidian Monetization Model:** Free, verifiable self-hosting builds deep technical trust and community connectors, converting convenience-seeking individuals, families, and entrepreneurs to high-retention subscriptions (**`$12–$20/mo` Personal**, **`$25–$35/mo` Family/Household**, **`$35–$50/seat` Small Business**) with zero ad-tech conflicts.

### 6.2 Candid Scope & Commercial Risks
- **Avoid Boiling the Ocean Across 19 Skills × 23 Channels:** Attempting to ship Email Triage, Travel, Social Brand Publishing, Trading Research, Credit-Card Points (`Plaid`/aggregators), ERISA Benefits, Small Business ERP, and `SMART on FHIR` Clinical Health (`M14`–`M15`) before earning **1 daily active user (Customer Zero)** risks severe dilution and compliance drag.
- **Strategic Wedge Recommendation:** Win **one indispensable daily loop** for Customer Zero first (**Capture + Email/Calendar Triage -> Open Loops Cockpit -> Weekly Review** in `M02`–`M04`), layer **Purpose-Scoped Shared Workspaces** (`M03.03` / `M09`) as the multi-player growth loop, and treat `S4`–`S19` as declarative MCP/skill packages built *after* core retention is proven.

---

## 7. Reorganized `M01`–`M15` Milestone & Sub-Milestone Map

All 15 GitHub milestones and 100% of open issues (`43/43`) in `Jamula/Andreja` now conform to the `M01`–`M15` numbering system with hierarchical `Mxx.yy` sub-milestones (and `Mxx.99` for every milestone's Human Gate & Release Authorization):

| Milestone | Legacy | Title | Sub-Milestones (`Mxx.yy`) |
|---|---|---|---|
| **`M01`** (`#1`) | Phase 0 | `M01 - Govern and decide` | `M01.01` Squad/retro readiness (`#298`, `#297`) · `M01.02` `D1`–`D3` + ADR `0006/0007/0011` decision prep (`#314`) · `M01.03` Licensing/IP (`#6`) · `M01.04` Ledgers & catalogs (`#259`) · `M01.99` Human gate (`#260`) |
| **`M02`** (`#2`) | Phase 1A | `M02 - Self-host MVP, .NET 11 & OTel baseline` | `M02.01` `.NET 11` upgrade (`#309`, PRs `#292`–`#295`) & separate `#211` gated scan diagnosis · `M02.02` End-to-end OTel `1.21+` & Aspire dashboard (`#312`) · `M02.03` `Testcontainers` Postgres & `DbContext` decoupling (preserving tenant constraints & atomic confirmation, `#315`) · `M02.04` `Microsoft.Extensions.AI` + MCP SDK + local AI (`#311`) · `M02.05` Self-host restore/Sigstore/rollback exit proofs (`#313`, `#38`) · `M02.99` Human gate (`#261`) |
| **`M03`** (`#3`) | Phase 1B | `M03 - Managed dogfood` | `M03.01` TTL-capped managed reference · `M03.02` Story 1 Gmail/M365 triage · `M03.03` `D1`-gated Story 2 Group Travel · `M03.04` Public/help site (`#93`) & feedback gate (`#158`) · `M03.99` Human gate (`#263`) |
| **`M04`** (`#4`) | Phase 2 | `M04 - Core assistant and skills` | `M04.01` Streaming `IChatClient` runtime & OTel budgets · `M04.02` Daily-driver Open Loops cockpit (`#310`) · `M04.03` Manual Calendar/Finance/Interests · `M04.04` Semantic Profile, Life Context & Brand draft · `M04.99` Human gate (`#265`) |
| **`M05`** (`#5`) | Phase 3A | `M05 - Email and messaging expansion` | `M05.01` OAuth token & webhook/delta engine · `M05.02` Gmail/Graph expansion & sender (`C4`, `C15`) · `M05.03` In-app (`C5`), Discord (`C6`) & WhatsApp share (`C8`) · `M05.99` Human gate (`#267`) |
| **`M06`** (`#6`) | Phase 3B | `M06 - Files, calendar and professional channels` | `M06.01` OneDrive/GDrive/GitHub/Box (`C9`) · `M06.02` Connected Calendar (`C11`) · `M06.03` GitHub/LinkedIn brand (`C12`) · `M06.99` Human gate (`#269`) |
| **`M07`** (`#7`) | Phase 4 | `M07 - Durable personal mechanisms` | `M07.01` RFC 5545 recurrence & Postgres scheduler · `M07.02` Manual Family/Household/Travel/Trading/Hobbies · `M07.03` Document-first Health, Life Events & Small Business · `M07.99` Human gate (`#271`) |
| **`M08`** (`#8`) | Phase 5 | `M08 - Multi-tenant service readiness` | `M08.01` CIAM & `(Issuer, Subject)` linking · `M08.02` Isolation/RLS, distributed state & Blazor scale · `M08.03` OTel tenant metering, billing & DSR automation · `M08.99` Human gate (`#273`) |
| **`M09`** (`#9`) | Phase 6 | `M09 - Federation and sharing` | `M09.01` Federation v1 spec, signing & conformance · `M09.02` Consent dashboard, disclosure ladder & revocation · `M09.99` Human gate (`#275`) |
| **`M10`** (`#10`) | Phase 7 | `M10 - Builder ecosystem and marketplace` | `M10.01` Public `.NET 11`/MCP SDK & signed manifests · `M10.02` Isolated remote MCP/HTTP capability broker · `M10.03` Publisher verification, ranking & payouts · `M10.99` Human gate (`#277`) |
| **`M11`** (`#11`) | Phase 8 | `M11 - Ecosystem expansion` | `M11.01` Expanded storage/community/household/business channels · `M11.02` Official social publishing & picker photo metadata · `M11.99` Human gate (`#279`) |
| **`M12`** (`#12`) | Phase 9 | `M12 - Native mobile` | `M12.01` `.NET 11` MAUI Hybrid vs. native & keystore/PRF proof · `M12.02` Offline sync, push, share targets & store readiness · `M12.99` Human gate (`#281`) |
| **`M13`** (`#13`) | Phase 10 | `M13 - Business analytics` | `M13.01` OTel metric catalog across 3 separated ledgers · `M13.02` Cohort privacy thresholds & `S19` analytics · `M13.99` Human gate (`#283`) |
| **`M14`** (`#14`) | Phase 11 | `M14 - Rewards, benefits and optimization` | `M14.01` Read-only card (`C18`), loyalty (`C19`) & benefits (`C20`) channels · `M14.02` `S12`–`S14` optimization skills · `M14.99` Human gate (`#285`) |
| **`M15`** (`#15`) | Phase 12 | `M15 - Connected health and wellbeing` | `M15.01` Clinical-fidelity & highest-sensitivity safety gates · `M15.02` `SMART on FHIR`, HealthKit, Health Connect & wearables · `M15.99` Human gate (`#287`) |

---

## 8. Low-Hanging Fruit Catalog (`velocity:low-hanging-fruit`)

The following items have been tagged in GitHub with **`velocity:low-hanging-fruit`** because they require bounded effort (`15 minutes` to `2 days`) while disproportionately accelerating all downstream engineering, testing, observability, and dogfooding velocity:

| Rank | Sub-Milestone / Ref | Effort | Why It Multiplies Future Velocity |
|---:|---|---|---|
| **1** | **`M02` Dependabot PR Queue Cleanup (`#292`–`#295`)** | ~30 min | Rebasing/merging the remaining Dependabot bumps (`OpenTelemetry 1.21.0`, `EF Design 10.0.15`, `Mvc.Testing 10.0.15`, `TrxReport 2.5.3`) clears dependency PR drift cleanly. *(Keep `#211` (`M02.01.01`) separate: a passing unsigned `main` scan does not identify the original 12 findings' root cause or satisfy independent-review and signed-release verification.)* |
| **2** | **`M01.01.01` (`#298`) & `M01.01.02` (`#297`) — Squad & Retro Automation** | ~1–2 hrs | Hosted heartbeats already pass; reconciling fresh-checkout `.squad/casting/*` health and scheduling weekly retro admission eliminates recurring process friction across every PR. |
| **3** | **`M02.03` (`#315`) — `Testcontainers.PostgreSql` + Module `DbContext` Decoupling** | ~0.5–1 day | Turns a manual 40-line Docker script into single-command `dotnet test` DB verification for humans and coding agents, and decouples the shared `AndrejaIdentityDbContext` across existing `identity` / `open_loops` / `portability` schemas *before* `M04`+ adds tables—preserving composite tenant constraints and single-transaction atomic confirmation. |
| **4** | **`M02.01` (`#309`) — `.NET 11` (`net11.0`) Upgrade** | ~0.5–1 day | All core NuGet packages (`Npgsql 11.0.0`, `ASP.NET Identity 11.0.0`, `MEAI 11.0.1`, `OTel 1.21.0`) are GA now; upgrading while the codebase is `~27K` C# LOC avoids migrating 5× more code later. |
| **5** | **`M02.02` (`#312`) — End-to-End Content-Suppressed `OTel` + Aspire Dashboard** | ~1 day | Standardizes `gen_ai.*`, Postgres, HTTP, runtime, and proposal spans with `CaptureContent=false` and gives instant local visual debugging via the standalone Aspire OTLP dashboard. |
| **6** | **`M01.02` (`#314`) — `D1`–`D3` & ADR `0006/0007/0011` Decision Preparation** | ~1–2 PRs | Prepares `D1`–`D3` architecture packets alongside already-implemented `ADR 0007`/`0011` reviews and `ADR 0006` prerequisites—preserving `ADR 0006`'s required sequence where reviewed `docs/plan.md` charter-seed and `docs/frameworks/feedback-support.md` routing/RACI amendments merge *before* a separate atomic charter-acceptance PR. |
| **7** | **`M02.04` (`#311`) — `Microsoft.Extensions.AI` + Official `MCP` C# SDK + Local AI** | ~1–2 days | Deletes custom LLM wire maintenance, adds MCP tool brokering, and enables `$0` zero-egress local inference (`Foundry Local` / `Ollama`) on synthetic fixtures while keeping `GitHub.Copilot.SDK` limited to `ADR 0009`'s isolated, credential-free, non-shipping toolchain spike in `M02`. |
| **8** | **`M04.02` (`#310`) — Daily-Driver Open Loops Cockpit (`Inbox/Today` + `.eml`/`.ics` Drop)** | ~2–3 days | Builds the daily workflow (`Inbox/Today`, recurrence, `.eml`/`.ics` drop-in) on synthetic data first, transitioning to real personal data once `ADR 0012` owner-client encryption, content-key recovery, release verification, and plaintext migration gates pass. |

---

## 9. Recommended 30-Day Execution Sequence

1. **Days 1–3 (Queue Cleanup & Governance Prerequisite Sequence):** Finish Dependabot PR cleanup (`#292`–`#295`) and `#298`; advance `[M01.02]` (`#314`) + `[M01.03]` (`#6`) decision packets, merge the reviewed `docs/plan.md` charter-seed and `docs/frameworks/feedback-support.md` routing/RACI prerequisite amendments first, and then submit the separate atomic `ADR 0006` acceptance PR toward `M01` (`#260`).
2. **Days 4–10 (`.NET 11`, `OTel`, `#211` Evidence & `M02` Exit):** Execute `[M02.01]` (`.NET 11`), `[M02.02]` (`OTel 1.21+` + Aspire dashboard), `[M02.03]` (`Testcontainers` + `DbContext` decoupling), `#211`'s gated historical-scan root-cause, independent-review, and signed-release verification, and `[M02.05]` (combined DB+DataProtection restore + `v0.1.0-alpha` keyless Sigstore tag + 2-revision rollback) to exit `M02` (`#261`).
3. **Days 11–21 (Daily-Driver Cockpit & Privacy-Gated Cutover):** Freeze new governance prose; ship `[M02.04]` (`Microsoft.Extensions.AI` + MCP SDK + zero-egress `Foundry Local`/`Ollama`, keeping `GitHub.Copilot.SDK` strictly in an isolated non-shipping `ADR 0009` spike) and `[M04.02]` (Open Loops Cockpit with `Ical.Net` recurrence & `.eml`/`.ics` drop-in), exercising synthetic workflows first and enabling real personal data only after `ADR 0012` re-ratification, reviewed owner-client encryption, key recovery, verified client release, and plaintext migration complete.
4. **Days 22–30 (Lean `M03` Dogfood Preparation):** After `M02` exits and the `[M01.02]` privacy/provider gates pass—neither of which authorizes a cloud-spend envelope—prepare bounded `M03.01`/`M03.02` (Gmail + Microsoft Graph read-only/dry-run triage; `~$30/mo` unapproved planning estimate), with any cloud provisioning strictly conditional on a separate Phase 1B (`M03`) budget decision plus enforceable quotas, maximum-resource policies, explicit TTLs, and automatic teardown (`docs/plan.md`, `docs/cost-model.md`; a budget is not a spending cap).
