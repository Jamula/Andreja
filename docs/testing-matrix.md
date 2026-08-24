# Testing matrix

This matrix records the local evidence expected for shipped Phase 1A behavior.
Never report unavailable live dependencies as passed.

| Area | Evidence | Command | Current local status |
| --- | --- | --- | --- |
| Task domain and application | Validation, explicit proposal, exact diff, confirmation, idempotent retry, complete, export, delete, expiry | `dotnet test Andreja.slnx --no-build` | Automated |
| Assistant and skill boundary | Deterministic provider invokes only `open-loops.propose-task` through `ISkillHost`; provider failure and cancellation are safe | `dotnet test Andreja.slnx --no-build` | Automated |
| API and authorization | Anonymous and fake-header rejection, real Development cookie sign-in, antiforgery rejection, authoritative validation, typed DTO lifecycle | `dotnet test Andreja.slnx --no-build` | Automated; no header authentication handler exists |
| Local sign-in smoke | Anonymous UI redirects to passkey sign-in with local-only ReturnUrl; explicit Debug+Development cookie sign-in reaches UI/API; Production and Release cannot map the Development helper; API challenge is JSON 401/403 without redirects | `dotnet test Andreja.slnx --no-build`; Release build; follow-redirect live smoke | UI, security headers, unsafe ReturnUrl and production graph automated; real authenticator evidence remains blocked |
| Development origin | Committed HTTPS launch profile and `PublicOrigin` both use `https://localhost:5001`; documented command names the profile and development certificate | `dotnet test Andreja.slnx --no-build` | Automated |
| Interactive circuit delegation | Two actual DI scopes resolve the production `AddHttpClient` typed client with different circuit authentication providers and concurrently prove tenant/principal isolation while sharing one pooled stateless handler; Data Protection token is audience-bound, short-lived, single-use, and claim-complete; wrong audience, expiry, replay, tampering, and conflicting/missing claims fail closed | `dotnet test Andreja.slnx --no-build` | Automated against the real TestServer API and antiforgery filter with null `HttpContext` |
| Tenant and principal isolation | Wrong-tenant/wrong-principal proposal, list, complete, and delete negatives | `dotnet test Andreja.slnx --no-build` | Automated in memory |
| PostgreSQL | Empty migration, atomic/concurrent bootstrap, durable passkey, hashed recovery rotation, security-stamp invalidation, passkey limits, last-path safety, task lifecycle, and two-tenant filtering | `dotnet test tests\Andreja.PostgreSqlIntegrationTests\Andreja.PostgreSqlIntegrationTests.csproj` | Compiled in normal validation; runtime required only when `ANDREJA_TEST_POSTGRES` targets a disposable `andreja_test_*` database; unavailable runs are reported blocked, never skipped as passed |
| Architecture | Blazor page injects the typed API client and cannot inject modules, adapters, or EF | `dotnet test Andreja.slnx --no-build` | Automated |
| Accessibility and responsive UI | Semantic headings/labels, live status/error regions, keyboard-native controls, reduced motion, mobile breakpoint, and safe interactive initialization failure | `dotnet test Andreja.slnx --no-build`; manual keyboard and 320/768/1280 px viewport review | Initialization failure automated; manual viewport evidence required before launch |
| Browser E2E | Real WebAuthn bootstrap, restart sign-in/out, recovery, then task proposal/confirm/complete/export/delete at mobile/tablet/desktop widths | Real browser and authenticator | Blocked for #44/#62: this machine has no repository browser harness, virtual authenticator, or secret-free real authenticator automation. Unit/WAF/PostgreSQL evidence does not substitute for this gate |
| Telemetry privacy | Task/proposal content attributes suppressed while route/status allowlist remains | `dotnet test Andreja.slnx --no-build` | Automated |
| Hosted .NET gate | Restore; Debug and Release solution build/test; separate Debug and Release PostgreSQL-project compile | `.github\workflows\dotnet-validation.yml` | Hosted on PR, push to `main`, and `merge_group`; artifacts enumerate included, excluded, and unavailable runtime projects |
| Formatting and docs | Compiler analyzers, solution and PostgreSQL-project format verification, contract and docs consistency | See `docs\development.md` | Required before PR readiness |
| Vulnerabilities | Direct and transitive NuGet advisory scan of the service-free solution and separately restored PostgreSQL project | `pwsh -NoProfile -File .github\scripts\invoke-nuget-vulnerability-scan.ps1` | Required locally and hosted before PR readiness |
| C# SAST | Microsoft DevSkim CLI 1.0.90, pinned and executed locally on the hosted runner | `.github\workflows\dotnet-validation.yml` | CodeQL unavailable while private-repository Code Security is disabled; DevSkim fails on findings after the documented loopback-URL and Debug-guard lexical exclusions |

The API integration fixture uses the real cookie scheme through the explicit
Development-only sign-in endpoint for external API evidence. Its circuit regression
resolves the real typed client from the actual `AddHttpClient` registration in two
concurrent simulated circuit scopes. The scopes use distinct principals while the
factory reuses one stateless pooled handler; each sees only its own tasks. The test
also exercises the Data Protection token service, TestServer API authentication
scheme, antiforgery endpoint, and complete task lifecycle with no `HttpContext`.
Common fake-auth headers remain unauthorized and the Development endpoint is absent
in Production. Architecture tests reject fake/test authentication references,
cookie-forwarding handlers, and any pooled delegating handler that depends on
`AuthenticationStateProvider` or `IHttpContextAccessor`.
