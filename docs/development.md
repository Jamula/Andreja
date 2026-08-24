# Local development

## Prerequisites

- .NET SDK 10.0.301, pinned by `global.json`

The foundation does not require cloud accounts, provider credentials, containers,
or external services.

## Solution layout

- `src\Andreja.AppHost`: the composition root, minimal HTTP pipeline, and empty
  Blazor host.
- `src\Andreja.Api.Contracts`: the versioned HTTP contract boundary.
- `src\Andreja.Platform.Contracts`: framework-neutral shared boundary contracts.
- `src\Modules\Andreja.Modules`: capability seams for Identity, Open Loops,
  Assistant, Skills, Channels, and Portability.
- `src\Adapters\Andreja.Adapters`: outer seams for PostgreSQL, ASP.NET Core
  Identity, OpenAI-compatible assistants, and OpenTelemetry.
- `tests\Andreja.ArchitectureTests` and `tests\Andreja.UnitTests`: dependency
  direction and unit baselines.

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
```

The architecture test project enforces inward dependency direction. The unit test
project verifies composition-root registration and startup-options validation.

### Local PostgreSQL identity evidence

Identity database tests require a disposable local PostgreSQL database and are not
part of the service-free solution test. Follow
[`tests\Andreja.PostgreSqlIntegrationTests\README.md`](../tests/Andreja.PostgreSqlIntegrationTests/README.md).
When PostgreSQL is unavailable, record this evidence as blocked; do not mark it
skipped or successful.

## Run the empty host

```powershell
dotnet run --project src\Andreja.AppHost\Andreja.AppHost.csproj
```

This starts only the empty Blazor foundation host. Identity, persistence,
assistant, tasks, containers, and provider integrations are intentionally absent.
