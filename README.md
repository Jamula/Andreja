# Andreja

Andreja is a user-owned personal assistant and skill platform. The ratified
architecture, product roadmap, and delivery phases are in
[`docs/plan.md`](docs/plan.md). The proposed company mission, commitments, and
operating culture are in [`docs/charter.md`](docs/charter.md); the charter
remains pending explicit ratification.

## Prerequisites

- Git.
- .NET SDK 10.0.301, pinned by [`global.json`](global.json).
- `dotnet-ef` 10.0.11 for migration inspection and script generation.
- Python 3, available as `python`, for documentation consistency checks.
- PowerShell 7 for repository validation scripts.
- Node.js 22 and Microsoft Edge for browser, passkey, viewport, and telemetry
  evidence.
- Docker with Buildx and Compose for live PostgreSQL, OCI, and self-host
  evidence.

## Clone

```powershell
git clone https://github.com/Jamula/Andreja.git
Set-Location Andreja
if ((dotnet --version) -ne "10.0.301") {
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

The repository is private. Your GitHub account must have access before cloning.

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

Run the deterministic unit and architecture suites:

```powershell
dotnet test Andreja.slnx --configuration Debug --no-build
dotnet test Andreja.slnx --configuration Release --no-build
```

Verify formatting, documentation, dependencies, operations, and supply-chain
policy:

```powershell
dotnet format Andreja.slnx --verify-no-changes --no-restore
dotnet format tests\Andreja.PostgreSqlIntegrationTests\Andreja.PostgreSqlIntegrationTests.csproj `
  --verify-no-changes --no-restore
python .github\scripts\check_docs_consistency.py
node --test .github\scripts\issue-status.test.js
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

  dotnet test tests\Andreja.PostgreSqlIntegrationTests\Andreja.PostgreSqlIntegrationTests.csproj `
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

Phase 0 provisions no cloud accounts, subscriptions, free tiers, or trials.
