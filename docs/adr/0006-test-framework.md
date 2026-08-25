# ADR 0006: Retain xUnit 2 while evaluating the long-term test platform

- **Status:** Proposed
- **Date:** 2026-08-24
- **Issue:** [#96](https://github.com/Jamula/Andreja/issues/96)
- **Decision owner:** Cyrus Jamula
- **Scope:** .NET 10 unit, architecture, WebApplicationFactory, EF Core, and
  PostgreSQL integration tests

## Recommendation

Retain xUnit 2.9.3 for the current suite and do not migrate it to MSTest now.
Modern MSTest 4.3.3 with `MSTest.Sdk` and Microsoft.Testing.Platform (MTP) 2.3.3
is viable, actively supported, and reached semantic parity in the spike. It did
not, however, produce a material execution, reliability, dependency, or
operability advantage that justifies a five-to-eight-engineer-day migration and
an all-at-once runner/CI change.

This is not an endorsement of xUnit 2 as the indefinite platform. NuGet and
xUnit's documentation mark v2 as deprecated/maintenance-only, with feature work
in v3. Before 2026-10-31, before the inventory reaches 400 tests, or before a
runner/SDK incompatibility blocks the suite (whichever comes first), compare
xUnit v3 with MSTest/MTP in a separate reviewed decision. Until then:

- add no new framework-specific fixture abstraction without review;
- keep exact test-inventory and skipped-test evidence;
- update the xUnit VSTest adapter/Test SDK only in a separate dependency PR;
- do not introduce a permanently mixed framework solution.

## Current evidence boundary

The spike started from then-live `origin/main` commit `51b4eb4` and .NET SDK
10.0.301. Before commit, the clean branch was rebased and revalidated against
live `origin/main` commit `c88e057`.

The current suite has:

| Item | Inventory |
| --- | ---: |
| Discovered service-free tests | 271 (253 unit + 18 architecture) |
| Discovered PostgreSQL tests | 22 |
| Total discovered tests | 293 |
| Declared test methods | 209 (188 facts + 21 theories) |
| Data declarations | 101 `InlineData` + 2 `MemberData` |
| Nonblank test C# lines | 10,439 |
| xUnit assertion calls | 923 across 18 assertion forms |
| Async lifecycle / class fixture use | 2 `IAsyncLifetime` classes / 1 `IClassFixture` |
| Collection fixtures, traits, explicit skips, output helpers | 0 |
| Explicit timeouts | 3 |

All three test projects currently use xUnit 2.9.3,
`xunit.runner.visualstudio` 2.8.2, and `Microsoft.NET.Test.Sdk` 17.12.0. The
service-free CI runs Debug and Release with VSTest-style `dotnet test`,
`--logger trx`, and uploaded result artifacts. PostgreSQL tests compile in
normal CI and run only against an explicitly disposable `andreja_test_*`
database.

## Method

An ignored, isolated pair of net10.0 projects exercised equivalent xUnit and
MSTest tests without editing the suite. The MSTest project used
`MSTest.Sdk/4.3.3`, its default MTP profile, and the .NET 10 MTP mode selected in
a spike-local `global.json`. Both projects covered:

- a fact/test method, two inline data rows, async setup/cleanup, skip, timeout,
  and captured output;
- class-level WebApplicationFactory lifecycle and anonymous API behavior;
- rejection of the fake `X-Andreja-Test-Authenticate` header, preserving the
  production-impossible test-authentication boundary;
- shared fixture lifecycle across two classes;
- opt-in/default class concurrency with a two-peer synchronization probe;
- category/trait filtering and ten-row discovery parity;
- EF Core production migrations against PostgreSQL
  `17.6-bookworm@sha256:f3bd19c606e442c3d7bdfa8002e03fe260a1023351e0ea4598032022b68dd6e3`.

PostgreSQL ran in a uniquely named container with a tmpfs data directory,
random host port, unique `andreja_test_framework_spike` database, and local-only
credentials. Both fixtures deleted the database during cleanup. The container,
credentials, binaries, logs, TRX files, and spike sources were removed after
recording the results.

The first harness iteration exposed missing Identity model configuration and a
blocking synchronization probe. Both were harness defects, were corrected
symmetrically, and are excluded from framework reliability results.

## Capability comparison

| Concern | xUnit 2.9.3 | MSTest 4.3.3 + MTP 2.3.3 | Andreja consequence |
| --- | --- | --- | --- |
| Basic tests | `[Fact]` | `[TestClass]` + `[TestMethod]` | MSTest adds one class attribute; both are clear. |
| Parameterized data | `[Theory]`, `InlineData`, `MemberData` | `DataRow`, `DynamicData`, typed `TestDataRow<T>` | Direct parity exists. The current 103 data declarations still require mechanical and behavioral review. |
| Async test methods | `Task`; v2 lifecycle through `IAsyncLifetime` | `Task`/`ValueTask`; async initialize/cleanup and `IAsyncDisposable` | Both work. MSTest has more lifecycle scopes and analyzers; conversion is not attribute-only. |
| Per-test lifecycle | constructor + dispose / `IAsyncLifetime` | constructor, `TestInitialize`/`TestCleanup`, dispose / async dispose | Equivalent outcomes, different ordering and failure semantics. |
| Class fixtures | `IClassFixture<T>` with constructor injection | static `ClassInitialize`/`ClassCleanup` or explicit shared owner | WAF worked in both. xUnit is more concise and strongly instance-oriented. |
| Cross-class fixtures | Named collection + `ICollectionFixture<T>` also forms a parallel boundary | Assembly lifecycle or project-owned static fixture; no stable named collection-fixture equivalent | The spike used assembly lifecycle. It caused PostgreSQL startup even when a filter selected only unit/WAF tests, unlike xUnit's unselected collection fixture. |
| Parallel execution | On by default between test collections; sequential within a class/collection | Sequential by default; explicit `Parallelize` supports class- or method-level scope; `DoNotParallelize` opts out | Both concurrency probes passed. A migration must explicitly reproduce current class-level behavior and audit shared resources. |
| Traits/filtering | `Trait` and VSTest filter expressions in the current runner | `TestCategory`/properties and MTP `--filter` | Both selected the same six rows. MTP and xUnit use different framework-specific filter syntax. |
| Skip | `Skip` on fact/theory | `Ignore`, conditional attributes, and row-level ignore | Both emitted one skipped result. xUnit's TRX contained the `NotExecuted` result but reported `notExecuted="0"` in summary counters; MSTest reported `notExecuted="1"`. The production suite currently has no skips. |
| Timeout | `Timeout` on fact/theory; accuracy depends on parallel algorithm | `Timeout` on tests and fixture methods, with cooperative cancellation options | Both bounded tests passed. Existing three timeouts require semantic review. |
| Output | constructor-injected `ITestOutputHelper` | `TestContext.WriteLine` | Both output strings appeared under their TRX test result. |
| Analyzers | `xunit.analyzers` is transitive | `MSTest.Analyzers` is transitive and broad | MSTest caught two always-true assertions as warnings-as-errors; the xUnit build did not. This is a real MSTest advantage. |
| WAF / EF / PostgreSQL | Framework-neutral product libraries with xUnit lifecycle glue | Same libraries with MSTest lifecycle glue | Anonymous WAF, fake-header rejection, EF migration count, and cleanup all passed in both. No production code change was needed. |
| IDE/debug | Visual Studio, VS Code, Rider, and VSTest adapter ecosystem | Visual Studio/VS Code Test Explorer plus directly runnable/debuggable MTP executables | Official documentation supports both. No graphical IDE was installed in the evidence host, so IDE parity is documented rather than locally observed. |
| TRX/artifacts | Current `--logger trx` workflow works | Default SDK profile includes TRX, invoked with `--report-trx` | MTP produced a valid deterministic TRX. The existing CI arguments do not carry over. |

## CLI and CI findings

The current repository is in the .NET 10 `dotnet test` **VSTest mode**. Running
the MSTest.Sdk spike through the current command shape failed with:

> Testing with VSTest target is no longer supported by
> Microsoft.Testing.Platform on .NET 10 SDK and later.

The successful MTP path required:

1. `"test": { "runner": "Microsoft.Testing.Platform" }` in `global.json`;
2. `dotnet test --project ...` (and `--solution ...` for a solution);
3. MTP options such as `--report-trx`, not VSTest's `--logger`;
4. validation of every workflow filter, results directory, debugger, and
   artifact consumer.

Official xUnit documentation says v2 has MTP support only through
`YTest.MTP.XUnit2`, a third-party package unsupported by xUnit or Microsoft.
Official .NET documentation also calls mixed VSTest/MTP solutions unsupported
and notes that framework-specific MTP options differ. Therefore a direct modern
MSTest/MTP migration cannot safely be incremental inside the current solution:
either retain VSTest during conversion (forgoing the target architecture) or
cut the full suite, `global.json`, local commands, and CI together.

MTP test executables, `dotnet test`, direct run/debug, Test Explorer, TRX, and
GitHub Actions reporting are documented. The default MSTest.Sdk profile also
enables code coverage and TRX extensions. Andreja already opts out of .NET CLI
telemetry; official MTP documentation confirms
`DOTNET_CLI_TELEMETRY_OPTOUT=1` also disables MTP telemetry.

## Measured results

Both projects discovered exactly ten rows and completed with nine passed, one
intentional skip, and zero failures. Filters selected the same six rows with
five passed and one intentional skip. Seven stabilized full runs per framework
(five timing runs, one memory run, one final evidence run) had no intermittent
failure.

Measurements were on one Windows arm64 host, with packages restored and builds
complete. The sample is deliberately representative, not a microbenchmark:

| Measurement | xUnit 2.9.3 | MSTest 4.3.3 / MTP 2.3.3 |
| --- | ---: | ---: |
| Full run, five samples (min / median / max) | 3,381 / 3,552 / 4,273 ms | 3,667 / 3,824 / 3,903 ms |
| Discovery, five samples (min / median / max) | 1,245 / 1,260 / 1,294 ms | 1,578 / 1,697 / 3,828 ms |
| Approximate peak process-tree working set | 307.9 MiB | 240.7 MiB |
| Approximate largest-process working set | 130.1 MiB | 137.5 MiB |

MSTest's full-run median was 7.7% slower and discovery median 34.7% slower;
its sampled process tree was 21.8% smaller while its largest process was 5.7%
larger. Database and WAF startup dominate this tiny suite, process-tree sampling
is approximate, and one MSTest discovery run was an outlier. These results show
no decision-grade performance winner. Neither framework crossed a 10% full-run
regression stop threshold consistently.

## Dependency, security, license, and maintenance evidence

The resolved framework-specific shapes differed:

- xUnit used four explicit test packages in the spike: MVC Testing,
  Microsoft.NET.Test.Sdk, xUnit, and its VSTest adapter. Its framework graph
  included xUnit core/assert/analyzers/abstractions and the VSTest
  object-model/testhost/code-coverage packages.
- `MSTest.Sdk` expanded to five top-level resolved entries (MVC Testing,
  MSTest framework/adapter, TRX, and code coverage) plus MTP, MTP MSBuild,
  VSTestBridge, telemetry, TRX abstractions, analyzers, and TestPlatform
  object-model dependencies.

`dotnet list package --vulnerable --include-transitive` reported no known
vulnerable package for either spike from the configured package source on
2026-08-24. This is a point-in-time advisory result, not proof of absence.
Both remain test-only dependency graphs and add no production authentication or
runtime surface.

xUnit is Apache-2.0 and part of the .NET Foundation. xUnit's official v2 page
and NuGet listing say 2.9.3 is maintenance/deprecated, receives critical or
security fixes only, and should move to v3.

MSTest, MSTest.Sdk, and MTP are Microsoft-supported, open source, and MIT
licensed. Microsoft's policy supports only the latest MSTest release, which
improves patch velocity but creates a continuing upgrade obligation. NuGet
showed 4.3.3 as the latest stable version (published 2026-07-28); the resolved
MTP version was 2.3.3. Microsoft provides coordinated vulnerability reporting
through MSRC.

The default MSTest.Sdk profile includes
`Microsoft.Testing.Extensions.CodeCoverage`, whose NuGet license is the
Microsoft .NET Library terms rather than MIT. The current xUnit/VSTest graph
also includes Microsoft.CodeCoverage, so MSTest does not remove proprietary
test tooling. Any future profile or extension change requires a fresh license
and telemetry review.

## Migration estimate

A conversion is not just 209 attribute substitutions:

| Work | Estimate |
| --- | ---: |
| Three projects, global SDK/runner selection, 27 test classes, 209 test attributes, and 103 data declarations | 1–1.5 engineer days |
| Review/convert 923 assertions, especially throws, predicates, strings, collections, and argument ordering | 1.5–2 days |
| Async lifecycle, WAF class fixture, PostgreSQL lifecycle, and explicit parallel/resource audit | 1–1.5 days |
| CLI, Debug/Release CI, filters, TRX/artifacts, analyzers, local docs, and IDE checks | 1–1.5 days |
| Exact 293-row parity, disposable PostgreSQL repetition, failure/skip/timeout/output evidence, and review fixes | 1–1.5 days |
| **Total** | **5–8 engineer days; approximately 1–2 calendar weeks with review** |

This excludes feature work and assumes mechanical assertion conversion does not
expose behavior differences. A permanent mixed-framework state would add
runner, filter, analyzer, and contributor complexity and is rejected.

## Pros and cons

### Retain xUnit 2 now

**Pros**

- zero suite behavior drift and no interruption to 293 working tests;
- preserves concise constructors, async lifetime, class fixture, collection,
  and default class-level concurrency semantics;
- current CLI, IDE adapter, TRX, Debug/Release CI, WAF, and PostgreSQL evidence
  remain proven;
- avoids spending five to eight days without a measured reliability or
  performance gain.

**Cons**

- the core package is deprecated and maintenance/security-only;
- the current Visual Studio adapter and Test SDK are behind current releases;
- MTP is unsupported officially for xUnit v2;
- xUnit TRX skip summary counters need an inventory-level guard if skips appear.

### Migrate to modern MSTest/MTP now

**Pros**

- Microsoft-supported, actively developed framework and recommended MTP path;
- stronger analyzer findings in the spike;
- direct executable/debug model, modern report extensions, and accurate skipped
  counter in the observed TRX;
- broader lifecycle, metadata, timeout, and conditional execution features.

**Cons**

- no measured full-run, discovery, flakiness, WAF, EF, PostgreSQL, or dependency
  advantage sufficient to fund the migration;
- parallelism defaults differ and cross-class collection fixtures lack a direct
  stable equivalent;
- current .NET 10 VSTest commands fail for the MTP project, forcing coordinated
  `global.json`, CI, filter, and artifact changes;
- incremental xUnit-v2/MTP coexistence is unsupported;
- the SDK profile adds extensions, telemetry configuration, and mixed license
  obligations that still require governance.

## Decision and consequences

1. Keep the three current test projects on xUnit 2.9.3. This ADR does not approve
   a framework, runner, Test SDK, CI, or suite migration.
2. Treat xUnit 2 as a bounded holding state, not the long-term default.
3. Preserve the current test-authentication boundary: fake headers remain
   rejected and no framework may add a production authentication handler.
4. Preserve exact discovered-test and skipped-test evidence in any future
   runner change. A passing command with fewer tests is a failure.
5. Do not mix frameworks permanently. A temporary conversion branch must stay
   isolated and must not delete the xUnit source until parity is reviewed.
6. Require architecture/quality, security/package provenance, CI/operations,
   migration-cost, and contributor-documentation review before changing this
   decision.

## Stop, rollback, and approval gates

Stop a future conversion and retain the last proven xUnit commit if any of these
occur:

- discovered inventory is below the baseline 293 plus reviewed suite changes;
- an intentional skip is absent from console, per-test TRX, or aggregate
  inventory evidence;
- anonymous, fake-header, antiforgery, tenant/principal, WAF, EF migration, or
  disposable PostgreSQL semantics change;
- Debug/Release, category/FQN filters, IDE discovery/debug, TRX, artifact
  upload, analyzer, vulnerability, or license gates regress;
- stabilized full-run median regresses by more than 10%, a repeated run flakes,
  or cleanup leaves a database/container/process behind;
- implementation exceeds eight engineer days without a new approval;
- evidence remains inconclusive.

Rollback is a branch reset to the xUnit baseline because no current suite files
are changed by this decision. A future migration must keep an inventory-parity
checkpoint before removing any xUnit package or source.

## Follow-up

1. Open a bounded xUnit v3 versus MSTest/MTP decision by 2026-10-31, with the
   same representative and full-suite gates.
2. Separately evaluate current compatible releases of
   `xunit.runner.visualstudio` and `Microsoft.NET.Test.Sdk`; do not combine that
   dependency update with a framework migration.
3. Add an inventory gate that records discovered, executed, passed, failed, and
   skipped counts per assembly and rejects silent count loss.
4. If MSTest remains the candidate, prototype a class-scoped PostgreSQL fixture
   design that does not run database setup for unrelated filters, then validate
   Visual Studio/VS Code discovery on supported developer hosts.

## Official sources

Accessed 2026-08-24:

- [xUnit v2 getting started and maintenance status](https://xunit.net/docs/getting-started/v2/getting-started)
- [xUnit shared context](https://xunit.net/docs/shared-context)
- [xUnit parallel execution](https://xunit.net/docs/running-tests-in-parallel)
- [xUnit v2 on MTP (third-party, unsupported bridge)](https://xunit.net/docs/getting-started/v2/microsoft-testing-platform)
- [xUnit 2.9.3 NuGet package, deprecation, and Apache-2.0 notice](https://www.nuget.org/packages/xunit/2.9.3)
- [MSTest overview and support policy](https://learn.microsoft.com/dotnet/core/testing/unit-testing-mstest-intro)
- [MSTest test attributes and assertions](https://learn.microsoft.com/dotnet/core/testing/unit-testing-mstest-writing-tests)
- [MSTest data-driven tests](https://learn.microsoft.com/dotnet/core/testing/unit-testing-mstest-writing-tests-data-driven)
- [MSTest lifecycle](https://learn.microsoft.com/dotnet/core/testing/unit-testing-mstest-writing-tests-lifecycle)
- [MSTest execution, parallelism, timeout, and skip controls](https://learn.microsoft.com/dotnet/core/testing/unit-testing-mstest-writing-tests-controlling-execution)
- [MSTest categories and metadata](https://learn.microsoft.com/dotnet/core/testing/unit-testing-mstest-writing-tests-organizing)
- [MSTest.Sdk profiles and migration](https://learn.microsoft.com/dotnet/core/testing/unit-testing-mstest-sdk)
- [MTP overview and supported frameworks](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-intro)
- [.NET 10 VSTest/MTP `dotnet test` modes and mixed-runner limits](https://learn.microsoft.com/dotnet/core/testing/unit-testing-with-dotnet-test)
- [MTP CLI, IDE, debugger, and CI operation](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-run-and-debug)
- [MTP TRX and report artifacts](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-test-reports)
- [MTP telemetry and opt-out](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-telemetry)
- [ASP.NET Core 10 integration testing and WebApplicationFactory](https://learn.microsoft.com/aspnet/core/test/integration-tests?view=aspnetcore-10.0)
- [MSTest.Sdk 4.3.3 package](https://www.nuget.org/packages/MSTest.Sdk/4.3.3)
- [MTP 2.3.3 package](https://www.nuget.org/packages/Microsoft.Testing.Platform/2.3.3)
- [Microsoft code-coverage extension license](https://www.nuget.org/packages/Microsoft.Testing.Extensions.CodeCoverage/18.9.0/License)
- [Microsoft testfx security policy](https://github.com/microsoft/testfx/security/policy)
