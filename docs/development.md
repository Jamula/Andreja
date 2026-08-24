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
  Assistant, Skills, Channels, Portability, and in-memory semantic conformance.
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
dotnet restore tests\Andreja.PostgreSqlIntegrationTests\Andreja.PostgreSqlIntegrationTests.csproj
dotnet build Andreja.slnx --configuration Debug --no-restore
dotnet build tests\Andreja.PostgreSqlIntegrationTests\Andreja.PostgreSqlIntegrationTests.csproj --configuration Debug --no-restore
dotnet test Andreja.slnx --configuration Debug --no-build
dotnet build Andreja.slnx --configuration Release --no-restore
dotnet build tests\Andreja.PostgreSqlIntegrationTests\Andreja.PostgreSqlIntegrationTests.csproj --configuration Release --no-restore
dotnet test Andreja.slnx --configuration Release --no-build
dotnet format Andreja.slnx --verify-no-changes --no-restore
dotnet format tests\Andreja.PostgreSqlIntegrationTests\Andreja.PostgreSqlIntegrationTests.csproj --verify-no-changes --no-restore
pwsh -NoProfile -File .github\scripts\invoke-nuget-vulnerability-scan.ps1
pwsh -NoProfile -File scripts\operations\validate-contract.ps1
bash -n scripts/operations/*.sh
```

PowerShell parser/PSScriptAnalyzer validation includes the explicit
`scripts\operations\migrate-database.ps1` wrapper. Normal web startup is
schema-read-only; see the [self-host operations runbook](operations/self-hosting.md)
for the backup/review-gated migration command.

The architecture test project enforces inward dependency direction. The unit test
project verifies composition-root registration and startup-options validation.

### OpenAI-compatible transport conformance

`OpenAiCompatibleConformanceTests` hosts an OpenAI-shaped `/v1/chat/completions`
endpoint entirely in-process. It exercises local and HTTPS URI handling without DNS,
external traffic, provider accounts, funded proxies, paid tokens, connector
credentials, or Copilot entitlement. The deterministic provider remains the normal
offline test and CI default.

```powershell
dotnet test tests\Andreja.UnitTests\Andreja.UnitTests.csproj `
  --filter "FullyQualifiedName~OpenAiCompatibleConformanceTests|FullyQualifiedName~AssistantProviderTests"
```

The fixture verifies the exact typed Open Loops proposal boundary and never confirms
or writes the proposed task. It also covers timeout versus caller cancellation,
bounded retries, nonretryable rejection, oversized and malformed bodies, unknown or
invalid tool calls, redirect/allowlist confusion, secret redaction, provider-reported
usage, read-only file rotation/revocation, zero-budget stop, and deterministic
fallback. No live-provider smoke is authorized by these tests.

### Skill and channel contract fixtures

`Andreja.Platform.Contracts` owns provider-neutral `ISkillHost` and `IChannelHost`
boundaries. Their manifests carry schema and semantic versions, lifecycle,
publisher, permission/purpose/data-class/disclosure, execution, retention,
help/support, compatibility, and integrity metadata. Optional or future fields are
never omitted ambiguously: they carry an explicit `NotApplicable` state and reason.

Both in-memory hosts use the same `ExecutionAuthorizationEvaluator`. Every call keeps
`TenantId`, authoritative `AppUserId`, and `PrincipalId` distinct and must intersect
the user policy, active bilateral consent, active purpose-bound grant, declared
capability, operation, data class, time/revocation state, and disclosure ceiling.
Allow and deny audits contain identifiers and policy facts only, never invocation
arguments or returned content.

The channel implementation is a deterministic local conformance fixture. It has no
provider SDK, OAuth account/scope, credential or token passthrough, network
destination, query/sync/publish mode, webhook/change feed, external execution,
marketplace path, live federation traffic, or persistence migration. Open Loops
continues to return a proposal without writing and now propagates the authoritative
app-user identifier rather than substituting the principal identifier.

### Semantic profile and provenance fixtures

`SemanticAssertionConformanceTests` exercises the version `1.0` contracts and the
pinned `Fixtures\semantic-profile-v1.jsonld` projection. The in-memory ledger proves
append-only source references, explicit lineage/lifecycle, scope and purpose
failures, least exposure, privacy-preserving export/delete behavior, tamper and
concurrency handling. It is not production persistence or automatic inference and
requires no database, graph provider, embedding, model, credential, or network.

### Local PostgreSQL identity evidence

Identity database tests require a disposable local PostgreSQL database and are not
part of the service-free solution test. Follow
[`tests\Andreja.PostgreSqlIntegrationTests\README.md`](../tests/Andreja.PostgreSqlIntegrationTests/README.md).
When PostgreSQL is unavailable, record this evidence as blocked; do not mark it
skipped or successful.

### Hosted .NET validation

`.github\workflows\dotnet-validation.yml` runs on pull requests, pushes to
`main`, and merge-queue groups. It uses the exact SDK in `global.json` on the
explicit `ubuntu-24.04` runner, immutable action SHAs, read-only repository
permissions, non-persisted checkout credentials, concurrency cancellation, and
14-day evidence retention. Fork pull requests receive no secrets or
write-capable token.

Debug and Release each restore, build, and test the service-free solution, then
compile `Andreja.PostgreSqlIntegrationTests` separately. The hosted workflow
does not provision PostgreSQL or receive a database credential, so the database
runtime evidence remains **unavailable**, never passed or skipped. Every job
summary and machine-readable artifact enumerates:

- **Included runtime projects:** `Andreja.ArchitectureTests` and
  `Andreja.UnitTests`.
- **Excluded from solution runtime but compiled:**
  `Andreja.PostgreSqlIntegrationTests`.
- **Unavailable runtime projects:** `Andreja.PostgreSqlIntegrationTests`, with
  the missing disposable PostgreSQL dependency recorded.

The private repository currently reports `code_security.status=disabled`, and
the CodeQL configuration endpoint returns `403` with “Code Security must be
enabled for this repository to use code scanning.” The existing disabled CodeQL
workflow is therefore not presented as C# evidence. The hosted gate instead
runs the pinned Microsoft DevSkim CLI `1.0.90` locally on the runner, uploads no
source, and fails on remaining C# findings. Two noisy lexical rules are excluded
explicitly: `DS137138` flags the intentional loopback HTTP URLs used by local
development and transport-boundary tests, while `DS162092` flags the words
`Debug` and `Development` in safeguards that prevent development behavior from
shipping in Release. Their exclusion is recorded in every SAST artifact.
Re-evaluate both exclusions and CodeQL if the code or entitlement changes.

GitHub may also attach a platform-managed dynamic `Analyze (csharp)` CodeQL
check to a pull request. That supplementary check ran successfully while this
gate was introduced, but it is not a repository-owned substitute: the committed
CodeQL workflow remains manually disabled, the repository API still denies
CodeQL configuration, and the dynamic check does not establish committed
push-to-`main` or `merge_group` coverage.

Default-branch required-check and merge-queue enforcement is deliberately
tracked in [issue #67](https://github.com/Jamula/Andreja/issues/67). OCI SBOM,
image scanning, and provenance remain in
[issue #71](https://github.com/Jamula/Andreja/issues/71).

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
enables PostgreSQL, persisted Data Protection keys, and ASP.NET Core Identity
passkey bootstrap/sign-in/recovery. Apply the explicit identity migration, configure
the exact HTTPS RP origin, and follow
[local identity help](help/local-identity.md). Real browser/authenticator,
PostgreSQL runtime, backup/restore, and hosted checks remain required evidence; the
implementation alone does not establish Phase 1A exit.

The API routes are under `/api/v1/open-loops`; Blazor components use only
`IOpenLoopsApiClient` and versioned DTOs. See the
[Open Loops help](help/open-loops.md) and [testing matrix](testing-matrix.md).
Cookie challenges under `/api/**` return safe JSON `401`/`403` responses and never
redirect to an HTML login page. Browser UI challenges redirect only to the existing
`/Account/Login` route with a locally validated `ReturnUrl`.

Interactive Server components do not forward request cookies or use
`IHttpContextAccessor`; there is no stable request `HttpContext` after a Blazor
circuit starts. `OpenLoopsApiClient` is resolved inside the circuit scope and reads
that scope's `AuthenticationStateProvider` directly at each call. It issues a Data
Protection-protected, one-minute, single-use token for the exact
`andreja.internal.open-loops.v1` audience and attaches it to that request. The
`IHttpClientFactory` pipeline is pooled and deliberately stateless: no delegating
handler captures a principal, circuit service, request cookie, or `HttpContext`.

The protected token binds tenant, app user, principal, subject, issue/expiry times,
and nonce. Invalid, expired, replayed, wrong-audience, missing, or conflicting
identity context fails closed. External API callers continue to use the approved
Identity cookie/passkey scheme; unsigned tenant/principal headers are never
accepted.

The task page disables prerendering so initialization runs once inside the
authenticated circuit. API, transport, cancellation, and delegation initialization
failures become a visible safe error state rather than escaping as an unhandled
render exception.
