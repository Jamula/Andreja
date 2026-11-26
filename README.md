# Andreja

Andreja is a privacy-first, user-owned personal assistant and skill platform
built as a modular `.NET 10` monolith for self-hosted operators first and future
cloud portability by contract. The ratified architecture, product roadmap, and
delivery phases are in [`docs/plan.md`](docs/plan.md), with historical context
and end-to-end milestone sequencing in
[`docs/project-origin-and-evolution.md`](docs/project-origin-and-evolution.md)
and
[`docs/roadmap/full-vision-completion-roadmap.md`](docs/roadmap/full-vision-completion-roadmap.md).
The proposed company mission, commitments, and operating culture are in
[`docs/charter.md`](docs/charter.md); the charter remains pending explicit
ratification and takes effect only if Cyrus approves it through the proposed
[ADR 0006 ratification instrument](docs/adr/0006-charter-ratification.md).

## Why Andreja exists and core principles

Andreja started as a personal system for Customer Zero (original author Cyrus
Jamula) to capture open loops, prepare reviewed follow-ups, and run repeatable
personal workflows without surrendering custody of personal records, prompts, or
credentials. Rather than shipping an opaque prompt wrapper that silently writes
state or requires third-party cloud lock-in, Andreja is engineered as an
evidence-gated platform across 15 planned delivery milestones (`Phase 0` through
`Phase 12` in [`docs/plan.md`](docs/plan.md); see
[`docs/project-origin-and-evolution.md`](docs/project-origin-and-evolution.md)),
currently spanning `Phase 0` governance baselines and the `Phase 1A` self-hosted
walking skeleton.

Five architectural boundaries govern the codebase:

1. **Explicit human agency (`Proposal → Policy → Confirmation → Persistence → Audit`):**
   Assistant outputs never mutate durable user records directly. Typed proposals
   pass policy checks, require explicit user confirmation, persist durably, and
   record auditable state transitions.
2. **Self-host first, cloud-portable by contract:**
   Andreja runs locally on digest-pinned OCI images and PostgreSQL
   ([`Dockerfile`](Dockerfile), [`compose.yaml`](compose.yaml)), with
   operator-invoked AES-256-GCM application export and dry-run/approved import
   in [`src/Andreja.Portability.Cli`](src/Andreja.Portability.Cli)
   ([`docs/operations/portability.md`](docs/operations/portability.md)) and
   shared contracts for future managed dogfood, multi-tenant SaaS,
   bilateral-consent federation, and isolated remote MCP/HTTP skill hosts.
   Current Phase 1A storage is operator-custodied rather than zero-knowledge
   ciphertext; owner-device content encryption in
   [ADR 0012](docs/adr/0012-owner-device-content-encryption.md) remains
   `Proposed` and unimplemented.
3. **Provider-neutral AI with an offline default and default-zero BYOK spend:**
   A deterministic local assistant is the offline default. Optional
   OpenAI-compatible Bring-Your-Own-Key (`BYOK`) requires explicit operator
   configuration, exact endpoint allowlisting, read-only file-backed
   credentials, no redirects, and a default-zero external usage envelope
   ([`docs/phase-1a/byok-security-privacy.md`](docs/phase-1a/byok-security-privacy.md)).
4. **Passkey identity and composite tenant isolation:**
   Self-hosted deployments authenticate via WebAuthn passkeys, one-time
   `SensitiveBuffer` bootstrap tokens, and single-use hashed recovery codes
   ([`docs/help/local-identity.md`](docs/help/local-identity.md)). Distinct
   `TenantId`, `AppUserId`, and `PrincipalId` boundaries are enforced across API
   contracts, modules, channels, skills, and composite PostgreSQL constraints.
5. **Evidence-gated governance:**
   `Phase 0` enforces a `$0` cloud-provisioning and no-unapproved-external-account
   rule ([`docs/cost-model.md`](docs/cost-model.md)). Proposed ADRs remain
   non-normative design records distinct from accepted ADRs, and SHA-256
   hash-locked governance artifacts are checked continuously in CI.

## Repository and solution layout

| Path | Role |
| --- | --- |
| [`src/Andreja.Platform.Contracts`](src/Andreja.Platform.Contracts) | Framework-neutral shared contracts (`ISkillHost`, `IChannelHost`, semantic conformance assertions, portability schema contracts). |
| [`src/Andreja.Api.Contracts`](src/Andreja.Api.Contracts) | Versioned HTTP request/response contracts (`/api/v1/open-loops`). |
| [`src/Modules/Andreja.Modules`](src/Modules/Andreja.Modules) | Capability modules for Identity, Open Loops, Assistant, Skills, Channels, Portability, and in-memory conformance hosts. |
| [`src/Adapters/Andreja.Adapters`](src/Adapters/Andreja.Adapters) | Outer adapters for PostgreSQL/EF Core, ASP.NET Core Identity (`BootstrapTokenVerifier`, `SensitiveBuffer`), OpenAI-compatible BYOK, and OpenTelemetry. |
| [`src/Andreja.AppHost`](src/Andreja.AppHost) | Modular monolith composition root, authenticated HTTP API, health probes, and responsive Blazor Open Loops UI. |
| [`src/Andreja.Portability.Cli`](src/Andreja.Portability.Cli) | Local operator-only CLI for encrypted application export and dry-run/approved import (never exposed as an HTTP endpoint). |
| [`src/Andreja.PublicSite`](src/Andreja.PublicSite) | Deterministic static field-guide generator and verifier (`local-unpublished` loopback-only mode; public publication remains gated on [issue #93](https://github.com/Jamula/Andreja/issues/93)). |
| [`tests/`](tests) | Four `.NET` `MSTest.Sdk` suites ([`Andreja.UnitTests`](tests/Andreja.UnitTests), [`Andreja.ArchitectureTests`](tests/Andreja.ArchitectureTests), and [`Andreja.PublicSiteTests`](tests/Andreja.PublicSiteTests) in [`Andreja.slnx`](Andreja.slnx), plus separately compiled [`Andreja.PostgreSqlIntegrationTests`](tests/Andreja.PostgreSqlIntegrationTests)) and doc-consistency tests (`tests/test_check_docs_consistency.py`). |
| [`deploy/`](deploy), [`scripts/`](scripts) | Digest-pinned Compose overrides, OpenTelemetry collector config, backup-gated migration scripts, OCI supply-chain verification, local public-site preview, and browser/offline evidence scripts. |
| [`docs/`](docs), [`.squad/`](.squad) | Ratified plan, ADRs (`0000`–`0012`), Phase 0/1A governance and evidence records, roadmap catalogs, operator/user guides, and Squad specialist team state ([`meet-the-squad.md`](meet-the-squad.md)). |

## Prerequisites

- Git.
- .NET SDK 10.0.404, pinned by [`global.json`](global.json).
- `dotnet-ef` 10.0.11 for migration inspection and script generation.
- Python 3, available as `python`, for documentation consistency checks.
- PowerShell 7 for repository validation scripts.
- Node.js 26.12.1 and Microsoft Edge for browser, passkey, viewport, and
  telemetry evidence.
- Docker with Buildx and Compose for live PostgreSQL, OCI, and self-host
  evidence.

## Clone

```powershell
git clone https://github.com/Jamula/Andreja.git
Set-Location Andreja
if ((dotnet --version) -ne "10.0.404") {
  throw "Install the .NET SDK version pinned by global.json."
}
python --version
pwsh --version
node --version

$efVersion = dotnet ef --version 2>$null
if ($LASTEXITCODE -ne 0) {
  dotnet tool install --global dotnet-ef --version 10.0.11
  $efVersion = dotnet ef --version
}
$efVersionText = $efVersion -join "`n"
if ($LASTEXITCODE -ne 0 -or $efVersionText -notmatch "10\.0\.11") {
  throw "Install or update the global dotnet-ef tool to version 10.0.11."
}
```

The repository is public. Write access and protected-branch operations still
require an authorized GitHub account.

## Restore and build

Restore the service-free solution and the separately gated PostgreSQL
integration project:

```powershell
dotnet restore Andreja.slnx
dotnet restore tests\Andreja.PostgreSqlIntegrationTests\Andreja.PostgreSqlIntegrationTests.csproj
```

Build both supported configurations:

```powershell
dotnet build Andreja.slnx --configuration Debug --no-restore
dotnet build Andreja.slnx --configuration Release --no-restore
dotnet build tests\Andreja.PostgreSqlIntegrationTests\Andreja.PostgreSqlIntegrationTests.csproj `
  --configuration Debug --no-restore
dotnet build tests\Andreja.PostgreSqlIntegrationTests\Andreja.PostgreSqlIntegrationTests.csproj `
  --configuration Release --no-restore
```

## Test and validate

Run the deterministic unit, architecture, and public-site suites:

```powershell
dotnet test --test-modules tests\Andreja.UnitTests\bin\Debug\net10.0\Andreja.UnitTests.dll --no-build
dotnet test --test-modules tests\Andreja.ArchitectureTests\bin\Debug\net10.0\Andreja.ArchitectureTests.dll --no-build
dotnet test --test-modules tests\Andreja.PublicSiteTests\bin\Debug\net10.0\Andreja.PublicSiteTests.dll --no-build
dotnet test --test-modules tests\Andreja.UnitTests\bin\Release\net10.0\Andreja.UnitTests.dll --no-build
dotnet test --test-modules tests\Andreja.ArchitectureTests\bin\Release\net10.0\Andreja.ArchitectureTests.dll --no-build
dotnet test --test-modules tests\Andreja.PublicSiteTests\bin\Release\net10.0\Andreja.PublicSiteTests.dll --no-build
```

For an aggregate run across all three solution test projects, `dotnet test --solution Andreja.slnx` remains supported.

Verify formatting, documentation, dependencies, operations, and supply-chain
policy:

```powershell
dotnet format Andreja.slnx --verify-no-changes --no-restore
dotnet format tests\Andreja.PostgreSqlIntegrationTests\Andreja.PostgreSqlIntegrationTests.csproj `
  --verify-no-changes --no-restore
python .github\scripts\check_docs_consistency.py
pwsh -NoProfile -File .github\scripts\invoke-nuget-vulnerability-scan.ps1
pwsh -NoProfile -File scripts\operations\validate-contract.ps1
pwsh -NoProfile -File scripts\supply-chain\Test-SupplyChainPolicy.ps1
pwsh -NoProfile -File scripts\supply-chain\Invoke-NegativeTests.ps1
```

### Run the live PostgreSQL suite

Database tests are intentionally outside `Andreja.slnx`. They require an empty,
disposable database named `andreja_test_*` and fail with `BLOCKED` when one is
not supplied.

This Docker example is loopback-only and disposable:

```powershell
$postgresImage = "postgres:17.6-bookworm@sha256:f3bd19c606e442c3d7bdfa8002e03fe260a1023351e0ea4598032022b68dd6e3"
$testPassword = [Convert]::ToBase64String(
  [Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$postgresContainer = docker run --rm --detach `
  --name andreja-tests-postgres `
  --publish 127.0.0.1:55432:5432 `
  --env "POSTGRES_PASSWORD=$testPassword" `
  --env POSTGRES_DB=andreja_test_local `
  $postgresImage
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($postgresContainer)) {
  throw "Unable to create the disposable PostgreSQL container."
}

$env:ANDREJA_TEST_POSTGRES = "Host=127.0.0.1;Port=55432;Database=andreja_test_local;Username=postgres;Password=$testPassword;SSL Mode=Disable"

try {
  $ready = $false
  for ($attempt = 0; $attempt -lt 30; $attempt++) {
    docker exec andreja-tests-postgres `
      pg_isready --username postgres --dbname andreja_test_local
    if ($LASTEXITCODE -eq 0) {
      $ready = $true
      break
    }
    Start-Sleep -Seconds 1
  }
  if (-not $ready) {
    throw "Disposable PostgreSQL did not become ready."
  }

  dotnet test --project tests\Andreja.PostgreSqlIntegrationTests\Andreja.PostgreSqlIntegrationTests.csproj `
    --configuration Debug
  if ($LASTEXITCODE -ne 0) {
    throw "The live PostgreSQL integration suite failed."
  }
}
finally {
  Remove-Item Env:\ANDREJA_TEST_POSTGRES -ErrorAction SilentlyContinue
  $testPassword = $null
  if (-not [string]::IsNullOrWhiteSpace($postgresContainer)) {
    docker stop $postgresContainer
  }
}
```

Never point this suite at a shared or production database. See the
[PostgreSQL evidence guide](tests/Andreja.PostgreSqlIntegrationTests/README.md)
for its safety checks and coverage.

### Run browser, telemetry, and offline evidence

These layers require the pinned evidence Compose profile, ignored synthetic
secrets, local TLS certificate, explicit migrations, and preloaded immutable
images. Complete the version-current setup in the self-host runbook, especially
[image acquisition and verification](docs/operations/self-hosting.md#acquire-and-verify-images),
[the TLS proxy](docs/operations/self-hosting.md#same-host-tls-reverse-proxy),
[explicit migrations](docs/operations/self-hosting.md#explicit-database-migration),
and [local telemetry](docs/operations/self-hosting.md#local-telemetry-and-evidence),
then run:

```powershell
node --check scripts\evidence\browser-e2e.mjs
if ($LASTEXITCODE -ne 0) {
  throw "The browser evidence script has a syntax error."
}

try {
  $env:ANDREJA_BOOTSTRAP_TOKEN_FILE =
    (Resolve-Path .andreja\bootstrap_token_source).Path
  pwsh -NoProfile -File scripts\evidence\Test-TelemetryEvidence.ps1
  if ($LASTEXITCODE -ne 0) {
    throw "Browser and telemetry evidence failed."
  }

  pwsh -NoProfile -File scripts\evidence\Test-OfflineEvidence.ps1 `
    -AuditedAppImage $env:ANDREJA_IMAGE `
    -DestroySyntheticVolumes
  if ($LASTEXITCODE -ne 0) {
    throw "Offline and no-egress evidence failed."
  }
}
finally {
  Remove-Item Env:\ANDREJA_BOOTSTRAP_TOKEN_FILE -ErrorAction SilentlyContinue
}
```

The telemetry command drives the real Edge virtual-authenticator flow and
requires nonzero browser-request traces, metrics, and logs with zero policy
violations. The offline command requires preloaded digest-pinned images,
`--pull never`, internal networks, restart health, and failed TEST-NET egress.

## Run locally end to end

The development profile uses HTTPS, in-memory task storage, the deterministic
assistant, and a development-only sign-in path that is excluded from Release
builds.

```powershell
dotnet dev-certs https --trust
dotnet run --project src\Andreja.AppHost\Andreja.AppHost.csproj `
  --launch-profile https
```

Open <https://localhost:5001>. Then:

1. Use the local development sign-in.
2. Ask the deterministic assistant to prepare an Open Loops task.
3. Inspect the exact proposal and confirm it.
4. List and complete the task.
5. Export the task data and exercise the explicit delete flow.

Health endpoints:

```powershell
Invoke-WebRequest https://localhost:5001/health/live
Invoke-WebRequest https://localhost:5001/health/ready
```

Stop the app with `Ctrl+C`.

## Run the production-like self-host

The self-hosted path uses a digest-pinned OCI image, PostgreSQL, explicit
migrations, passkeys, a trusted same-host TLS proxy, persistent Data Protection
keys, and local OpenTelemetry. It intentionally requires more operator review
than the development profile.

Start with the [self-host operations runbook](docs/operations/self-hosting.md).
It contains the authoritative commands for:

- generating and protecting local secret files;
- building, scanning, signing, and verifying the OCI evidence bundle;
- setting `ANDREJA_IMAGE` to an immutable verified digest;
- configuring the exact public WebAuthn origin and trusted proxy;
- backing up and reviewing the database before explicit migration;
- starting Compose and checking live/readiness;
- passkey bootstrap, recovery, restart, offline, update, and rollback evidence;
- PostgreSQL dump/restore and application
  [export/import](docs/operations/portability.md).

After completing the runbook's one-time signing-key, TLS proxy, secret-file,
backup, and reviewed-migration preparation, the minimum operator sequence is:

```powershell
# `.env`, secret files, the trusted proxy, the reviewed migration SQL, and the
# restore-tested backup are already prepared according to the runbook.

# Produce and verify the signed local image bundle. The key paths are created
# during the runbook's one-time signing-key setup.
$env:COSIGN_PASSWORD = Read-Host "Cosign key password" -AsSecureString |
  ConvertFrom-SecureString -AsPlainText
try {
  pwsh -NoProfile -File scripts\supply-chain\New-OciEvidence.ps1 `
    -OutputDirectory artifacts\supply-chain `
    -SigningKeyPath $HOME\.andreja-signing\andreja.key `
    -TrustedPublicKeyPath $HOME\.andreja-signing\andreja.pub
  if ($LASTEXITCODE -ne 0) {
    throw "OCI evidence generation failed."
  }
}
finally {
  Remove-Item Env:\COSIGN_PASSWORD -ErrorAction SilentlyContinue
}

pwsh -NoProfile -File scripts\supply-chain\Test-OciEvidence.ps1 `
  -BundleDirectory artifacts\supply-chain `
  -TrustedPublicKeyPath $HOME\.andreja-signing\andreja.pub `
  -ExpectedSigningMode operator-held-key
if ($LASTEXITCODE -ne 0) {
  throw "OCI evidence verification failed."
}

# For retained hosted keyless evidence, use independently acquired policy and
# Sigstore root files outside the evidence directory; verification blocks networking.
pwsh -NoProfile -File scripts\supply-chain\Test-OciEvidence.ps1 `
  -BundleDirectory artifacts\supply-chain `
  -TrustedPolicyPath D:\trusted\supply-chain-policy.json `
  -TrustedRootPath D:\trusted\sigstore-trusted-root.json `
  -ExpectedSigningMode keyless-sigstore

$evidence = Get-Content artifacts\supply-chain\evidence.json -Raw |
  ConvertFrom-Json
$env:ANDREJA_IMAGE = $evidence.image.immutableReference

pwsh -NoProfile -File scripts\operations\validate-contract.ps1
if ($LASTEXITCODE -ne 0) {
  throw "The Compose and operations contract is invalid."
}

docker compose up --detach --wait postgres otel-collector
if ($LASTEXITCODE -ne 0) {
  throw "PostgreSQL or OpenTelemetry failed to start."
}

# Create and restore-test the backup, review the generated migration SQL, and
# set the approved migration names before running this command.
$backupDumpPath = Read-Host `
  "Path to the restore-tested dump created by the backup runbook"
if (-not (Test-Path -LiteralPath $backupDumpPath -PathType Leaf)) {
  throw "The restore-tested backup dump was not found."
}
$approvedMigrations = @(
  "20260824031732_InitialIdentityTenancy"
  "20260824043341_Phase1AOpenLoopsTasks"
  "20260824075115_ProductionPasskeyIdentity"
  "20260824102012_DurableRecentAuthenticationGrants"
  "20260824154149_DurableProposalConfirmation"
  "20260825004005_ApplicationPortability"
)

pwsh -NoProfile -File scripts\operations\migrate-database.ps1 `
  -BackupDumpPath $backupDumpPath `
  -ReviewedMigrationScriptPath .andreja\reviewed-migration.sql `
  -DatabaseName andreja `
  -ApprovedMigrations $approvedMigrations `
  -ConfirmBackupRestoreAndMigrationReview
if ($LASTEXITCODE -ne 0) {
  throw "The explicit database migration failed."
}

docker compose up --detach --wait
if ($LASTEXITCODE -ne 0) {
  throw "The self-hosted stack failed to start."
}
docker compose ps
```

Use the configured public HTTPS origin, not the container's direct HTTP port:

```powershell
$compose = docker compose config --format json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) {
  throw "The rendered Compose configuration is invalid."
}
$publicOrigin =
  $compose.services.app.environment.Andreja__Identity__AllowedOrigins__0
if ($publicOrigin -notmatch "^https://") {
  throw "The configured public origin must be HTTPS."
}

Invoke-WebRequest "$publicOrigin/health/live"
Invoke-WebRequest "$publicOrigin/health/ready"
Start-Process "$publicOrigin/Account/Bootstrap"
```

On a fresh database, `/Account/Bootstrap` creates the owner passkey and shows
single-use recovery codes. Save those codes offline before continuing. Later
sessions use `/Account/Login`; authenticated users manage passkeys and sign out
at `/Account/Passkeys`. After sign-in, exercise the same proposal, confirm,
complete, export, and delete task flow described above.

Verify persistent state survives a restart, then stop without deleting volumes:

```powershell
docker compose restart app
if ($LASTEXITCODE -ne 0) {
  throw "The application failed to restart."
}
$ready = $false
for ($attempt = 0; $attempt -lt 60; $attempt++) {
  try {
    Invoke-WebRequest "$publicOrigin/health/ready"
    $ready = $true
    break
  }
  catch {
    Start-Sleep -Seconds 2
  }
}
if (-not $ready) {
  throw "The application did not become ready after restart."
}

docker compose down
if ($LASTEXITCODE -ne 0) {
  throw "The self-hosted stack failed to stop cleanly."
}
Remove-Item Env:\ANDREJA_IMAGE -ErrorAction SilentlyContinue
```

Use `docker compose down --volumes` only for an explicitly disposable evidence
instance after its required backup/restore and cleanup evidence is complete.

Do not commit `.env`, credentials, bootstrap or recovery material, provider
tokens, Data Protection or signing keys, database dumps, application exports,
runtime state, or user content.

## Documentation

- [Platform plan (`docs/plan.md`)](docs/plan.md)
- [Project origin, architectural evolution, and 15-milestone trajectory](docs/project-origin-and-evolution.md)
- [Full-vision completion roadmap](docs/roadmap/full-vision-completion-roadmap.md)
- [High-level architecture and data flows](docs/architecture/andreja-high-level.md)
- [Privacy baseline](docs/privacy.md)
- [Threat model](docs/threat-model.md)
- [Local development](docs/development.md)
- [Self-host operations](docs/operations/self-hosting.md)
- [Application export/import](docs/operations/portability.md)
- [Local identity and recovery](docs/help/local-identity.md)
- [Open Loops help](docs/help/open-loops.md)
- [Testing matrix](docs/testing-matrix.md)
- [Phase 1A evidence](docs/phase-1a/evidence-44.md)
- [Phase 1A packet decision record](docs/phase-1a/packet-decision-66.md)
- [Issue status and Squad labels](docs/help/issue-status.md)
- [Meet the Squad](meet-the-squad.md)

## Community, governance, security, and licensing

- **Contributions (currently paused for external PRs):** See
  [`CONTRIBUTING.md`](CONTRIBUTING.md). External pull requests remain paused
  pending inbound contributor-licensing and IP closure in
  [issue #6](https://github.com/Jamula/Andreja/issues/6).
- **Security disclosures and trust boundaries:** Report vulnerabilities privately
  following [`SECURITY.md`](SECURITY.md) — never in public issues or pull
  requests.
- **License and attribution:** Source code in this repository is licensed under
  the Apache License, Version 2.0 ([`LICENSE`](LICENSE)), subject to the
  attribution and Section 6 trademark notices in [`NOTICE`](NOTICE) and ongoing
  IP/licensing governance in
  [issue #6](https://github.com/Jamula/Andreja/issues/6)
  ([`docs/legal/license-evaluation.md`](docs/legal/license-evaluation.md),
  [`docs/legal/licensing-decision-packet.md`](docs/legal/licensing-decision-packet.md)).
- **Sponsorship boundaries:** [`.github/FUNDING.yml`](.github/FUNDING.yml) links
  only to creator Cyrus Jamula's personal GitHub Sponsors profile
  (`@cyrusjamula`) and grants zero user-data access, ads, ranking preference, or
  roadmap influence. Organization- and project-level sponsorship for
  `Jamula, Inc.` remains inactive under [`SPONSORS.md`](SPONSORS.md) and
  [`docs/business/sponsorship-policy.md`](docs/business/sponsorship-policy.md).

Phase 0 provisions no cloud accounts, subscriptions, free tiers, or trials.
