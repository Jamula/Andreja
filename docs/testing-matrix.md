# Testing matrix

This matrix records the local evidence expected for shipped Phase 1A behavior.
Never report unavailable live dependencies as passed.

| Area | Evidence | Command | Current local status |
| --- | --- | --- | --- |
| Task domain and application | Validation, explicit proposal, exact diff, confirmation, idempotent retry, complete, export, delete, expiry | `dotnet test Andreja.slnx --no-build` | Automated |
| Assistant and skill boundary | Deterministic provider invokes only `open-loops.propose-task` through `ISkillHost`; provider failure and cancellation are safe | `dotnet test Andreja.slnx --no-build` | Automated |
| API and authorization | Anonymous and fake-header rejection, real Development cookie sign-in, antiforgery rejection, authoritative validation, typed DTO lifecycle | `dotnet test Andreja.slnx --no-build` | Automated; no header authentication handler exists |
| Local sign-in smoke | Anonymous UI redirects to an existing login route with local-only ReturnUrl; explicit Debug+Development cookie sign-in reaches UI/API; Production and Release builds cannot map the Development sign-in endpoint; API challenge is JSON 401/403 without redirects | `dotnet test Andreja.slnx --no-build`; Release build; follow-redirect live smoke | Development fallback automated; production passkey flow blocked separately |
| Development origin | Committed HTTPS launch profile and `PublicOrigin` both use `https://localhost:5001`; documented command names the profile and development certificate | `dotnet test Andreja.slnx --no-build` | Automated |
| Tenant and principal isolation | Wrong-tenant/wrong-principal proposal, list, complete, and delete negatives | `dotnet test Andreja.slnx --no-build` | Automated in memory |
| PostgreSQL | Empty migration, durable task lifecycle, idempotent receipts, and two-tenant filtering | `dotnet test tests\Andreja.PostgreSqlIntegrationTests\Andreja.PostgreSqlIntegrationTests.csproj` | Required when a disposable `andreja_test_*` database is supplied; otherwise blocked |
| Architecture | Blazor page injects the typed API client and cannot inject modules, adapters, or EF | `dotnet test Andreja.slnx --no-build` | Automated |
| Accessibility and responsive UI | Authenticated SSR smoke for labels/help associations and explicit controls; semantic headings, live status/error regions, keyboard-native controls, reduced motion, and mobile breakpoint | `dotnet test Andreja.slnx --no-build`; manual keyboard and 320/768/1280 px viewport review | SSR structure automated; manual viewport evidence required before launch |
| Browser E2E | Sign in, assistant proposal, confirm, complete, export, delete at mobile/tablet/desktop widths | Playwright | Blocked: the repository has no Playwright package or browser harness; no package was added solely for this slice |
| Telemetry privacy | Task/proposal content attributes suppressed while route/status allowlist remains | `dotnet test Andreja.slnx --no-build` | Automated |
| Formatting and docs | Compiler analyzers, format verification, contract and docs consistency | See `docs\development.md` | Required before PR readiness |
| Vulnerabilities | Direct and transitive NuGet advisory scan | `dotnet list Andreja.slnx package --vulnerable --include-transitive` | Required before PR readiness |

The API integration fixture uses the real cookie scheme through the explicit
Development-only sign-in endpoint. It proves that common fake-auth headers remain
unauthorized and that the endpoint is absent in Production. Architecture tests
continue to reject fake/test authentication references or types in production
assemblies.
