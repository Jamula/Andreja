# Security Policy

## Supported versions

Andreja is pre-release software developed in the public
[`Jamula/Andreja`](https://github.com/Jamula/Andreja) repository (`Phase 0`
governance and `Phase 1A` self-hosted walking skeleton). No tagged release or
hosted service currently carries a production security-support SLA. Security
fixes are landed on `main` and verified through repository CI and digest-pinned
OCI evidence; this support table will be updated before any supported public
release.

| Version / branch | Supported | Notes |
| --- | --- | --- |
| `main` (pre-release source) | Best-effort fixes | Active development branch; no production SLA. |
| Tagged or binary releases | None | No supported public release has been cut; outbound distribution gates remain open in [issue #6](https://github.com/Jamula/Andreja/issues/6). |

## Reporting a vulnerability

Report suspected security or privacy vulnerabilities privately through GitHub
Security Advisories:

- **Submit a private report:** <https://github.com/Jamula/Andreja/security/advisories/new>
- **View repository security policy:** <https://github.com/Jamula/Andreja/security/policy>

Do **not** disclose vulnerabilities, exploit steps, credentials, `.env` values,
bootstrap tokens, recovery codes, passkey ceremony payloads, task/open-loop
content, prompts, provider responses, or personal data in public issues,
discussions, or pull requests. Provide only the minimum synthetic reproduction
details needed to verify the issue. Internal triage and incident-routing rules
are documented in
[`docs/frameworks/feedback-security-abuse-incident-routing.md`](docs/frameworks/feedback-security-abuse-incident-routing.md).

## Current security and trust boundaries

Andreja enforces explicit, verifiable security boundaries in `Phase 1A` while
stating non-implemented controls plainly:

### Identity, passkeys, recovery, and proxy trust

- **Passkey authentication (`WebAuthn`):** Self-hosted deployments authenticate
  via ASP.NET Core Identity passkeys bound to an exact HTTPS `AllowedOrigins`
  origin and `RelyingPartyId`
  ([`docs/help/local-identity.md`](docs/help/local-identity.md),
  [`docs/operations/self-hosting.md`](docs/operations/self-hosting.md)).
- **Bootstrap token handling:** Initial owner enrollment (`/Account/Bootstrap`)
  reads the one-time token from an owner-read-only file (`0400` on Unix or
  `ReadOnly` attribute without environment interpolation on Windows) into pooled,
  deterministically zeroed `SensitiveBuffer` memory (`BootstrapTokenVerifier`)
  and verifies SHA-256 digests with `CryptographicOperations.FixedTimeEquals`.
  The raw bootstrap token is never persisted; PostgreSQL records only bootstrap
  completion metadata.
- **Single-use recovery codes:** Account recovery consumes hashed single-use
  recovery codes, rotates the recovery set, revokes previously registered
  passkeys, and rotates the user security stamp before enrolling a replacement
  passkey.
- **Internal delegation and step-up grants:** Interactive Server Blazor circuits
  authorize same-user `/api/v1/open-loops` calls with short-lived,
  audience-bound, single-use Data Protection tokens and require durable recent
  authentication grants before mutating passkeys or recovery state.
- **Exact reverse-proxy trust:** Forwarded headers trust only a single explicit
  proxy IP configured via `ANDREJA_TRUSTED_PROXY_IP`
  (`Andreja:Identity:TrustedProxyAddresses`). Wildcards, `0.0.0.0`, `::`, and
  CIDR ranges are rejected.
- **Compile- and environment-gated dev sign-in:** The local development sign-in
  endpoint is compiled inside `#if DEBUG` only and mapped only when
  `IsDevelopment()` is true; it is absent from `Release` builds and OCI images.

### Tenant isolation, mutation pipeline, migrations, and encryption posture

- **Composite tenant isolation:** Distinct `TenantId`, `AppUserId`, and
  `PrincipalId` identifiers are required across HTTP contracts, skills,
  channels, semantic conformance tests, and composite PostgreSQL constraints so
  cross-tenant reads and writes fail closed.
- **Explicit human confirmation (`Proposal → Policy → Confirmation → Persistence → Audit`):**
  Assistant turns and typed tools (`open-loops.propose-task`) can create
  proposals only; durable task creation and state changes require explicit user
  confirmation and emit auditable records.
- **Backup-gated schema migrations:** Normal web startup is schema-read-only.
  Database migrations execute only through
  [`scripts/operations/migrate-database.ps1`](scripts/operations/migrate-database.ps1),
  which requires a restore-tested backup dump, reviewed migration SQL, and an
  explicit migration allowlist.
- **Operator-custodied storage vs proposed owner-device encryption:**
  [`src/Andreja.Portability.Cli`](src/Andreja.Portability.Cli) encrypts
  operator-exported archives with AES-256-GCM and excludes credentials,
  passkeys, recovery codes, bootstrap tokens, and Data Protection keys
  ([`docs/operations/portability.md`](docs/operations/portability.md)). However,
  [`docs/adr/0012-owner-device-content-encryption.md`](docs/adr/0012-owner-device-content-encryption.md)
  is **Proposed (`Proposed; not ratified`) and not yet implemented** — current
  Phase 1A PostgreSQL tables and operator-decrypted exports store plaintext
  application fields under operator host custody, **not** zero-knowledge or
  end-to-end owner-device ciphertext.

### Assistant provider boundary

The deterministic assistant is the offline default. OpenAI-compatible BYOK is an
explicit operator choice with an exact endpoint allowlist, standard TLS
validation, loopback-only plain HTTP, no redirects, a read-only file-backed
credential (`credential://` reference), timeout and response-size limits,
bounded retries, typed-tool validation, content-free metrics, and a default-zero
external usage envelope. Credentials and provider request/response bodies must
not enter logs, telemetry, exports, configuration values, skill inputs, or
support artifacts. See the
[Phase 1A BYOK security/privacy contract](docs/phase-1a/byok-security-privacy.md).

### OCI supply chain and unpublished public-site containment

- **Digest-pinned OCI supply chain:** [`Dockerfile`](Dockerfile),
  [`compose.yaml`](compose.yaml), and
  [`supply-chain-policy.json`](supply-chain-policy.json) enforce `@sha256:`
  digest-pinned base, runtime, and scanner images, offline BuildKit NuGet
  restore, reproducible double-build digest checks, SBOM/vulnerability thresholds,
  and offline Cosign bundle verification ([`docs/operations/self-hosting.md`](docs/operations/self-hosting.md)).
- **Loopback-only static field guide:** [`src/Andreja.PublicSite`](src/Andreja.PublicSite)
  and [`scripts/public-website/local-preview.mjs`](scripts/public-website/local-preview.mjs)
  build and preview static field-guide HTML strictly in `local-unpublished`
  loopback mode (`127.0.0.1`) with zero access to application databases, session
  cookies, or runtime secrets; [ADR 0008](docs/adr/0008-public-website-artifact-boundary.md)
  is accepted for the static artifact boundary only while public website hosting
  and publication remain gated on
  [issue #93](https://github.com/Jamula/Andreja/issues/93).
