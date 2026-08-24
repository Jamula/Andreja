# Testing matrix

This matrix records the local evidence expected for shipped Phase 1A behavior.
Never report unavailable live dependencies as passed.

| Area | Evidence | Command | Current local status |
| --- | --- | --- | --- |
| Task domain and application | Validation, explicit proposal, exact diff, confirmation, idempotent retry, complete, export, delete, expiry | `dotnet test Andreja.slnx --no-build` | Automated |
| Assistant and skill boundary | Deterministic provider invokes only `open-loops.propose-task` through `ISkillHost`; provider failure and cancellation are safe | `dotnet test Andreja.slnx --no-build` | Automated |
| API and authorization | Anonymous rejection, test-only authenticated claims, antiforgery rejection, authoritative validation, typed DTO lifecycle | `dotnet test Andreja.slnx --no-build` | Automated; test authentication exists only in the test assembly |
| Tenant and principal isolation | Wrong-tenant/wrong-principal proposal, list, complete, and delete negatives | `dotnet test Andreja.slnx --no-build` | Automated in memory |
| PostgreSQL | Empty migration, durable task lifecycle, idempotent receipts, and two-tenant filtering | `dotnet test tests\Andreja.PostgreSqlIntegrationTests\Andreja.PostgreSqlIntegrationTests.csproj` | Required when a disposable `andreja_test_*` database is supplied; otherwise blocked |
| Architecture | Blazor page injects the typed API client and cannot inject modules, adapters, or EF | `dotnet test Andreja.slnx --no-build` | Automated |
| Accessibility and responsive UI | Semantic headings/labels, validation/help association, live status/error regions, keyboard-native controls, visible delete confirmation, reduced motion, mobile breakpoint | Manual keyboard and 320/768/1280 px viewport review | Manual evidence required before launch |
| Browser E2E | Sign in, assistant proposal, confirm, complete, export, delete at mobile/tablet/desktop widths | Playwright | Blocked: the repository has no Playwright package or browser harness; no package was added solely for this slice |
| Telemetry privacy | Task/proposal content attributes suppressed while route/status allowlist remains | `dotnet test Andreja.slnx --no-build` | Automated |
| Formatting and docs | Compiler analyzers, format verification, contract and docs consistency | See `docs\development.md` | Required before PR readiness |
| Vulnerabilities | Direct and transitive NuGet advisory scan | `dotnet list Andreja.slnx package --vulnerable --include-transitive` | Required before PR readiness |

The API integration fixture authenticates only when a test-only header is present.
Its handler is compiled into `Andreja.UnitTests`, and architecture tests reject test
authentication references or types in production assemblies.
