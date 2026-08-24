# Local development

## Prerequisites

- .NET SDK 10.0.301, pinned by `global.json`

The foundation does not require cloud accounts, provider credentials, containers,
or external services.

## Solution layout

- `src\Andreja.AppHost`: the composition root, authenticated typed HTTP API, and
  responsive Blazor Open Loops host.
- `src\Andreja.Api.Contracts`: the versioned HTTP contract boundary.
- `src\Andreja.Platform.Contracts`: framework-neutral shared boundary contracts.
- `src\Modules\Andreja.Modules`: capability seams for Identity, Open Loops,
  Assistant, Skills, Channels, and Portability.
- `src\Adapters\Andreja.Adapters`: outer seams for PostgreSQL, ASP.NET Core
  Identity, OpenAI-compatible assistants, and OpenTelemetry.
- `tests\Andreja.ArchitectureTests` and `tests\Andreja.UnitTests`: dependency
  direction, domain/application, typed API, authorization-negative, antiforgery,
  provider-failure, cancellation, and telemetry baselines.
- `compose.yaml`, `Dockerfile`, `deploy`, and `scripts\operations`: the local-only
  self-host, observability, backup, and recovery contract. See the
  [operations runbook](operations/self-hosting.md).
- `docs\operations\application-export-v1.schema.json`: the versioned,
  inspectable [application portability contract](operations/portability.md).

Modules and adapters begin as one assembly each. They should split only when a
real forbidden reference or adapter isolation requirement needs a compiler
boundary, as required by ADR 0001.

SDK analyzers, nullable reference types, warnings-as-errors, package versions,
and deterministic compilation are configured centrally.

## Validate

Run these commands from the repository root:

```powershell
dotnet restore Andreja.slnx
dotnet build Andreja.slnx --no-restore
dotnet test Andreja.slnx --no-build
dotnet format Andreja.slnx --verify-no-changes --no-restore
pwsh -NoProfile -File scripts\operations\validate-contract.ps1
bash -n scripts/operations/*.sh
```

The architecture test project enforces inward dependency direction. The unit test
project verifies composition-root registration and startup-options validation.

### Local PostgreSQL identity evidence

Identity database tests require a disposable local PostgreSQL database and are not
part of the service-free solution test. Follow
[`tests\Andreja.PostgreSqlIntegrationTests\README.md`](../tests/Andreja.PostgreSqlIntegrationTests/README.md).
When PostgreSQL is unavailable, record this evidence as blocked; do not mark it
skipped or successful.

## Run the host

```powershell
dotnet run --project src\Andreja.AppHost\Andreja.AppHost.csproj
```

Development enables the Open Loops UI with an in-memory task store for deterministic
local work. Production refuses to enable Open Loops without PostgreSQL. The
self-host bundle enables PostgreSQL, ASP.NET Core Identity cookies, tenant/principal
claim enforcement, antiforgery, and the task migration.

The API routes are under `/api/v1/open-loops`; Blazor components use only
`IOpenLoopsApiClient` and versioned DTOs. See the
[Open Loops help](help/open-loops.md) and [testing matrix](testing-matrix.md).
