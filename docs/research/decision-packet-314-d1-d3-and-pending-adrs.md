# Decision Readiness Packet (#314): D1–D3 Encryption / Sharing / AI Boundary & Pending ADRs (`0006`, `0007`, `0011`, `0012`)

- **Tracking issue:** [#314](https://github.com/Jamula/Andreja/issues/314) (`[M01.02]`)
- **Decision owner:** Cyrus Jamula (`@cyrusjamula`)
- **Status:** **Proposed — review-ready decision packet; no ADR is ratified, no trust boundary is changed, and no spend or external provider call is authorized by this document.**
- **Baseline verified against:** `Jamula/Andreja` (`main` @ `9a6dfc8`)

---

## 1. Executive Summary for Human Disposition

Issue [#314](https://github.com/Jamula/Andreja/issues/314) consolidates two blocking `M01` governance items into one structured human decision surface without mixing independent approvals:

1. **Part I — Resolving the Three-Way Privacy / Product Boundary (`D1`–`D3` & `Proposed ADR 0012`):**
   - Reconciles owner-device confidentiality (`#2` / `Proposed ADR 0012`) with `MVP Story 1` (Inbox Triage), `MVP Story 2` (Group Travel), and `$0`-default AI inference.
2. **Part II — Prerequisite & Evidence Readiness Audit for Pending ADRs (`0006`, `0007`, `0011`):**
   - Verifies exact repository evidence on `main` so Cyrus can record explicit `ACCEPT` / `AMEND` / `DEFER` dispositions without hidden prerequisite gaps.

---

## 2. Part I: Cohesive `D1`–`D3` Architecture Options

### 2.1 Current Tension on `main`
- **Implemented `M02` (`Phase 1A`) baseline (`Accepted ADRs 0001–0005`):** Stores `OpenLoopTask.Title` and `Details` as server-readable PostgreSQL text (`open_loops.tasks`), uses operator-key AES-256-GCM archives (`Andreja.Portability.Cli`), and supports deterministic local plus allowlisted OpenAI-compatible inference.
- **`Proposed ADR 0012` (`#2`):** Proposes single-owner client-side AEAD encryption where the server holds only ciphertext and prohibits both external plaintext LLM calls and live sharing with other humans.

### 2.2 Decision Matrix (`D1`, `D2`, `D3` Evaluated Independently)

| Decision ID | Question | Recommended Architecture (Option A) | Conservative Fallback (Option B) | Self-Host Baseline Retention (Option C) |
|---|---|---|---|---|
| **`D2` — Owner-Device Content Custody** | How are personal vault records encrypted and recovered? | **WebAuthn `prf` hardware-derived KEK + 256-bit offline Emergency Recovery Kit.** Per-record AES-256-GCM / XChaCha20-Poly1305 DEKs wrapped under an owner content root key; server stores ciphertext + minimal routing metadata only; signed client artifact verified before key unwrap. | Same owner-only client encryption, using explicit passphrase/recovery-file unwrap where platform `prf` is unavailable. | Retain `ADRs 0001–0005` operator-custodied PostgreSQL + encrypted host backup for Customer Zero self-hosting only; defer `ADR 0012` to `M08` (managed multi-tenant SaaS). |
| **`D3` — AI Inference Exposure** | Where may plaintext context be processed for proposals? | **Zero-egress local inference default (`Foundry Local` / `Ollama` loopback via `Microsoft.Extensions.AI` `IChatClient`, `$0` spend, `ApprovedExternalTotalUnits = 0`).** Any external BYOK or `M03+` Copilot runtime (`ADR 0009`) requires separate per-account opt-in, explicit unit budget, and per-call disclosure ceiling. | Strictly local/on-device inference only; prohibit all external LLM providers even with opt-in. | Local deterministic/loopback default plus operator-configured BYOK within explicit unit caps. |
| **`D1` — Consented Multi-Person Sharing** | How do Group Travel (`M03.03`) and Household workspaces collaborate without exposing personal vaults? | **Strict Vault Separation + RFC 9420 MLS Shared Workspaces.** Personal vaults remain 100% single-owner (`D2`). Purpose-scoped Shared Workspaces (`Trip`, `Household`) use isolated MLS group epochs, bilateral `ConsentRecord`s, ordered `DisclosureLevel` projections, and member-revocation key rotation—never sharing personal vault keys. | Preserve strict no-other-human access; explicitly defer `M03.03` Group Travel and `M09` Federation. | Single-instance multi-principal sharing governed by `ExecutionAuthorizationEvaluator` + bilateral `ConsentRecord`. |

### 2.3 Required Pre-Activation Gates (If Option A Is Selected)
Selecting Option A in `#314` authorizes drafting the exact `ADR 0012` v2 + `docs/plan.md` re-ratification amendment; real personal-data cutover remains gated on:
1. Exact-head re-ratification of `docs/plan.md` (`ADR 0000`) and `ADR 0012` by Cyrus;
2. Independent architecture, cryptography/security, privacy, accessibility, and AI-safety review of key hierarchy, nonce discipline, MLS group epoch transitions, and Emergency Recovery Kit UX;
3. Negative synthetic tests proving PostgreSQL rows, dumps, logs, exceptions, HTTP payloads, and OTel traces/metrics contain neither plaintext nor unwrap keys;
4. Verified client-release signature check before key derivation/unwrap, plus tested plaintext-to-ciphertext migration without silent plaintext fallback.

---

## 3. Part II: Exact Readiness Audit for Pending ADRs (`0006`, `0007`, `0011`)

All three ADRs below remain **`Status: Proposed`** in this branch. Merging this packet does **not** ratify any of them.

### 3.1 `Proposed ADR 0006` — Company Charter Ratification (`docs/charter.md`)
- **Prerequisite verification on `main` (`9a6dfc8`):** **SATISFIED.**
  1. **`docs/plan.md` charter-seed reconciliation:** Merged on `main` (`docs/plan.md:299` uses `"understand material recommendations"`, and `docs/plan.md:315-322` aligns the pre-GA internal charter-indicator ownership/threshold regime).
  2. **`docs/frameworks/feedback-support.md` routing & RACI reconciliation:** Merged via PR [#198](https://github.com/Jamula/Andreja/pull/198) (`4a03437`, `docs/frameworks/feedback-support.md:593-594,605-607`), routing legal-advice/privileged inquiries exclusively to appointed qualified external counsel and excluding Cyrus from conflict-class report intake, triage, and notification.
- **Current LF-normalized `docs/charter.md` SHA-256:** `c5bae1b99c420823a3bf4a1586cd295e20a0b28c8540620d9b61d92d12881008` (matches `docs/plan.md:62`; note `docs/adr/0006-charter-ratification.md:10` still holds initial candidate hash `d030d985...` per its atomic-acceptance rule).
- **Atomic changes required when Cyrus records `ACCEPT`:**
  1. Update `docs/adr/0006-charter-ratification.md` to `Status: Accepted`, set `Charter SHA-256` to `c5bae1b99c420823a3bf4a1586cd295e20a0b28c8540620d9b61d92d12881008`, and record Cyrus's approval link, date, and accepting PR.
  2. Update `docs/charter.md` status header from `Proposed` to `Accepted` / ratified, update `README.md`, and update `docs/plan.md` + `docs/adr/0000-plan-ratification.md` status-artifact hash in the same atomic PR.
  3. Switch `.github/ISSUE_TEMPLATE/decision.yml` and `.github/pull_request_template.md` charter-impact fields from `proposed` (optional) to `accepted` (required), enforced by `DocsConsistencyChecker.CheckCharterHash`.

### 3.2 `Proposed ADR 0007` — Test Framework (`MSTest.Sdk` + Microsoft Testing Platform)
- **Implementation & evidence status on `main`:** **IMPLEMENTED & VERIFIED.**
  - All five test projects (`Andreja.UnitTests`, `Andreja.ArchitectureTests`, `Andreja.PublicSiteTests`, `Andreja.DocsTests`, and `Andreja.PostgreSqlIntegrationTests`) run on `MSTest.Sdk 4.5.1` + `Microsoft.Testing.Platform` (`global.json:7-8`) with MIT-only `Microsoft.Testing.Extensions.TrxReport 2.5.3` (`TestingExtensionsProfile=None`).
  - Fail-closed inventory parity (`test-dotnet-test-parity.ps1` against `docs/research/test-suite-current-inventory-112.json` and immutable `docs/research/test-suite-baseline-112.json`) runs on every Debug and Release CI build.
  - Known documented host caveat: graphical IDE Test Explorer walkthrough remains explicitly recorded as unverified on headless CI hosts rather than inferred.
- **Disposition ready for Cyrus:** Record `ACCEPT` (with the documented headless-host IDE note) or `KEEP PROPOSED` pending interactive IDE sign-off.

### 3.3 `Proposed ADR 0011` — Default-Branch Required Gates (`Default-Ruleset`)
- **Implementation & evidence status on `main`:** **IMPLEMENTED, WITH VISIBILITY DEPENDENCY ON `#6`.**
  - Ruleset `21199927` (`Default-Ruleset`) enforces PR-only linear squash history, resolved review threads, zero bypass actors, and the five required `.NET Validation` checks (`Build and test (Debug)`, `Build and test (Release)`, `Format verification`, `NuGet vulnerability audit`, `C# SAST (DevSkim)`), paired with `.github/scripts/review-gate-app.js` exact-head review policy tests.
  - **Explicit coupling check:** `ADR 0011` notes that free-entitlement secret scanning / push protection relies on current public repository visibility, which remains an open legal/governance question in [#6](https://github.com/Jamula/Andreja/issues/6) (`[M01.03]`).
- **Disposition ready for Cyrus:** Ratify `ADR 0011`'s branch/check/review-gate controls now while keeping entitlement-dependent secret-scanning re-evaluation linked to `#6`, or batch `ADR 0011` ratification together with `#6` in `M01.99` ([#260](https://github.com/Jamula/Andreja/issues/260)).

---

## 4. Explicit Human Decision Checklist for `#314`

Cyrus can resolve `#314` by recording choices on each independent item:

- [ ] **`D1` (Sharing boundary):** `Option A (Isolated MLS Shared Workspaces, owner-only Personal Vault)` / `Option B (Owner-only; defer Group Travel & Federation)` / `Option C (Self-host multi-principal baseline)`
- [ ] **`D2` (Owner-device custody):** `Option A (WebAuthn prf + offline Emergency Recovery Kit via ADR 0012 v2)` / `Option B (Passphrase/file unwrap)` / `Option C (Defer ADR 0012 to M08)`
- [ ] **`D3` (AI provider boundary):** `Option A (Zero-egress local IChatClient default; explicit per-account opt-in + unit cap for external BYOK/Copilot)` / `Option B (Local-only; no external providers)`
- [ ] **`ADR 0006` (`docs/charter.md`):** Authorize opening the atomic acceptance PR (`Prerequisites verified on main`) — `Yes` / `Hold`
- [ ] **`ADR 0007` (`MSTest.Sdk` + MTP):** Authorize recording `Status: Accepted` — `Yes` / `Hold`
- [ ] **`ADR 0011` (`Default-Ruleset`):** Authorize recording `Status: Accepted` (preserving `#6` visibility re-evaluation clause) — `Yes` / `Hold for #6`
