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

PowerShell parser/PSScriptAnalyzer validation includes the explicit
`scripts\operations\migrate-database.ps1` wrapper. Normal web startup is
schema-read-only; see the [self-host operations runbook](operations/self-hosting.md)
for the backup/review-gated migration command.

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
dotnet dev-certs https --trust
dotnet run --project src\Andreja.AppHost\Andreja.AppHost.csproj --launch-profile https
```

The committed `https` launch profile listens deterministically on
`https://localhost:5001` (and redirects the `http://localhost:5000` listener), which
matches `Andreja:OpenLoops:PublicOrigin`. Trust the standard ASP.NET Core development
certificate before starting. Do not change only one side of this origin contract.
For development self-calls only, the typed client accepts the development
certificate exclusively when the target URI is loopback. Production retains normal
certificate validation and never receives this relaxation.

Development enables the Open Loops UI with an in-memory task store and an explicit
**Sign in to the development workspace** action. That action uses a fixed local
development identity, is compiled only in Debug builds, and is mapped only when
`IWebHostEnvironment.IsDevelopment()` is true. A Release build cannot expose it even
if its environment is misconfigured as Development. It is not passkey evidence and
must never be enabled as a production workaround.

Production refuses to enable Open Loops without PostgreSQL. The self-host bundle
enables PostgreSQL and ASP.NET Core Identity cookies, but production passkey
bootstrap/sign-in/recovery is tracked by
[P0 issue #55](https://github.com/Jamula/Andreja/issues/55) and is required before
Phase 1A exit. Until that blocker is complete, the production login page truthfully
states that sign-in is unavailable; the self-host task flow is not production-usable.

The API routes are under `/api/v1/open-loops`; Blazor components use only
`IOpenLoopsApiClient` and versioned DTOs. See the
[Open Loops help](help/open-loops.md) and [testing matrix](testing-matrix.md).
Cookie challenges under `/api/**` return safe JSON `401`/`403` responses and never
redirect to an HTML login page. Browser UI challenges redirect only to the existing
`/Account/Login` route with a locally validated `ReturnUrl`.

Interactive Server components do not forward request cookies or use
`IHttpContextAccessor`; there is no stable request `HttpContext` after a Blazor
circuit starts. The typed client's delegating handler reads the circuit's
`AuthenticationState`, issues a Data Protection-protected, one-minute, single-use
token for the exact `andreja.internal.open-loops.v1` audience, and sends it to the
dedicated internal API authentication scheme. The protected token binds tenant, app
user, principal, subject, issue/expiry times, and nonce. Invalid, expired, replayed,
wrong-audience, missing, or conflicting identity context fails closed. External API
callers continue to use the approved Identity cookie/passkey scheme; unsigned
tenant/principal headers are never accepted.

The task page disables prerendering so initialization runs once inside the
authenticated circuit. API, transport, cancellation, and delegation initialization
failures become a visible safe error state rather than escaping as an unhandled
render exception.
