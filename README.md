# Andreja

Andreja is a user-owned personal assistant and skill platform. The ratified
architecture, product roadmap, and delivery phases are in
[`docs/plan.md`](docs/plan.md). The proposed company mission, commitments, and
operating culture are in [`docs/charter.md`](docs/charter.md); the charter
remains pending explicit ratification.

## Prerequisites

- Git.
- .NET SDK 10.0.301, pinned by [`global.json`](global.json).
- PowerShell 7 for repository validation scripts.
- Docker with Buildx and Compose for live PostgreSQL, OCI, and self-host
  evidence.

Confirm the pinned SDK is available:

```powershell
dotnet --version
```

## Clone

```powershell
git clone https://github.com/Jamula/Andreja.git
Set-Location Andreja
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
docker run --rm --detach `
  --name andreja-tests-postgres `
  --publish 127.0.0.1:55432:5432 `
  --env POSTGRES_PASSWORD=andreja-test-only `
  --env POSTGRES_DB=andreja_test_local `
  $postgresImage

$env:ANDREJA_TEST_POSTGRES = "Host=127.0.0.1;Port=55432;Database=andreja_test_local;Username=postgres;Password=andreja-test-only;SSL Mode=Disable"

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
}
finally {
  Remove-Item Env:\ANDREJA_TEST_POSTGRES -ErrorAction SilentlyContinue
  docker stop andreja-tests-postgres
}
```

Never point this suite at a shared or production database. See the
[PostgreSQL evidence guide](tests/Andreja.PostgreSqlIntegrationTests/README.md)
for its safety checks and coverage.

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

Do not commit `.env`, credentials, bootstrap or recovery material, provider
tokens, Data Protection or signing keys, database dumps, application exports,
runtime state, or user content.

## Documentation

- [Local development](docs/development.md)
- [Self-host operations](docs/operations/self-hosting.md)
- [Application export/import](docs/operations/portability.md)
- [Local identity and recovery](docs/help/local-identity.md)
- [Open Loops help](docs/help/open-loops.md)
- [Testing matrix](docs/testing-matrix.md)
- [Phase 1A evidence](docs/phase-1a/evidence-44.md)

Phase 0 provisions no cloud accounts, subscriptions, free tiers, or trials.
