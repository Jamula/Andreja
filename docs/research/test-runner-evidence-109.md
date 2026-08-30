# xUnit VSTest adapter and Test SDK evidence

- **Issue:** [#109](https://github.com/Jamula/Andreja/issues/109)
- **Evaluated:** 2026-08-30
- **Quality owner:** Data
- **CI/operations reviewer:** Jett Reno
- **Pinned SDK:** .NET SDK 10.0.301 on Ubuntu 24.04
- **Outcome:** Retain `xunit.runner.visualstudio` 4.0.0 and
  `Microsoft.NET.Test.Sdk` 18.9.0 with xUnit 2.9.3.

## Scope and baseline

PR [#141](https://github.com/Jamula/Andreja/pull/141) had already updated
`Microsoft.NET.Test.Sdk` from 17.12.0 to 18.9.0 and
`xunit.runner.visualstudio` from 2.8.2 to 4.0.0 before this evaluation started.
To isolate those two test-tool changes from the other dependency updates in that
PR, the baseline used the current source tree with only those two versions
restored to 17.12.0 and 2.8.2. The candidate used the committed versions. No
test, production, xUnit v3, MSTest, runner-mode, or workflow change was made.

NuGet listed 18.9.0 and 4.0.0 as the latest stable, non-prerelease versions on
the evaluation date. The adapter's official 4.0.0 README says it supports
.NET 8 and later and can run xUnit.net 1.9.2 and later. Its release source also
pins xUnit 2.9.3 in `Versions.props`. Andreja's net10.0/xUnit 2.9.3 pairing is
therefore within the documented adapter range.

For net10.0, NuGet selects the adapter's net8.0 asset group, which has no
declared package dependency. `Microsoft.NET.Test.Sdk` supplies TestHost and
CodeCoverage 18.9.0, and TestHost supplies ObjectModel 18.9.0. The adapter's
`Microsoft.TestPlatform.ObjectModel >= 17.13.0` dependency applies only to its
.NET Framework 4.7.2 asset group.

## Resolved test infrastructure

`dotnet list package --include-transitive` produced the same graph in all three
test projects except for the intended adapter and Microsoft test-platform
versions:

| Package | Baseline | Candidate |
| --- | ---: | ---: |
| `Microsoft.NET.Test.Sdk` | 17.12.0 | 18.9.0 |
| `Microsoft.CodeCoverage` | 17.12.0 | 18.9.0 |
| `Microsoft.TestPlatform.TestHost` | 17.12.0 | 18.9.0 |
| `Microsoft.TestPlatform.ObjectModel` | 17.12.0 | 18.9.0 |
| `xunit.runner.visualstudio` | 2.8.2 | 4.0.0 |
| `xunit` / core / assert / extensibility | 2.9.3 | 2.9.3 |
| `xunit.abstractions` | 2.0.3 | 2.0.3 |
| `xunit.analyzers` | 1.18.0 | 1.18.0 |

The xUnit package's `xunit.analyzers >= 1.18.0` range resolves by NuGet's
lowest-applicable-version rule to 1.18.0 in both graphs. Although analyzer
2.0.0 exists, it is not selected. Builds with repository-wide warnings as
errors completed with zero warnings and errors, so this update introduces no
analyzer version or diagnostic change.

## Discovery and TRX parity

Each project was built, listed with `dotnet test --list-tests`, and executed to
a separate TRX in both configurations. Baseline and candidate produced the
same row in every case:

| Configuration | Assembly | Discovered | Executed | Passed | Failed | Skipped |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Debug | `Andreja.UnitTests` | 256 | 257 | 257 | 0 | 0 |
| Debug | `Andreja.ArchitectureTests` | 18 | 18 | 18 | 0 | 0 |
| Debug | `Andreja.PostgreSqlIntegrationTests` | 22 | 22 | 22 | 0 | 0 |
| Debug | **Total** | **296** | **297** | **297** | **0** | **0** |
| Release | `Andreja.UnitTests` | 252 | 253 | 253 | 0 | 0 |
| Release | `Andreja.ArchitectureTests` | 18 | 18 | 18 | 0 | 0 |
| Release | `Andreja.PostgreSqlIntegrationTests` | 22 | 22 | 22 | 0 | 0 |
| Release | **Total** | **292** | **293** | **293** | **0** | **0** |

The known one-row discovery/execution difference remains
`PortabilityCommandTests.InfrastructureFailuresUseFixedContentMinimizedMessages`:
one non-pre-enumerated `MemberData` theory is discovered and its two rows are
executed. Release excludes four Debug-only unit tests and preserves the same
one-row difference.

TRX `Counters` and individual `UnitTestResult` outcomes agreed for every
assembly/configuration. Local evidence used configuration- and
assembly-specific filenames. The existing hosted workflow continues to upload
separate Debug and Release TRX directories plus its validation report; this
evaluation does not change its commands, result paths, or artifact boundary.

## CLI filters and IDE discovery

The same VSTest `FullyQualifiedName` filters selected and passed the same rows
on baseline and candidate:

| Probe | Executed / passed / failed / skipped |
| --- | ---: |
| `SemanticAssertionConformanceTests` | 39 / 39 / 0 / 0 |
| `ModuleReferenceAllowlistRejectsNonApprovedAssemblies` | 8 / 8 / 0 / 0 |
| `ApplicationPortabilityTests` | 6 / 6 / 0 / 0 |

The suite declares no traits, so no trait/category parity claim is made.

The adapter README explicitly names Visual Studio 2022 Test Explorer and says
xUnit.net works with Visual Studio Code and all VSTest-compatible environments.
The three test projects retain the documented package-reference shape:
`PrivateAssets=all` and adapter `runtime`, `build`, `native`, `contentfiles`,
`analyzers`, and `buildtransitive` assets. Graphical Visual Studio and VS Code
were not installed on this Linux evidence host, so graphical discovery and
debugging are supported by official documentation but not represented as a
local pass.

## Behavior and repetition

The full unit suite continued to cover the production-impossible development
sign-in, anonymous/fake authentication rejection, antiforgery, WAF, tenant and
principal isolation, and EF migration boundaries. No production or test source
changed. All Debug and Release unit/architecture/PostgreSQL executions passed.

Five additional service-free Debug executions passed on each package set:

| Package set | Samples (seconds) | Median |
| --- | --- | ---: |
| 17.12.0 / 2.8.2 | 4.08, 4.10, 4.22, 4.22, 4.08 | 4.10 s |
| 18.9.0 / 4.0.0 | 4.29, 4.41, 4.35, 4.20, 4.51 | 4.35 s |

The candidate median was 6.1% slower on this shared host, below ADR 0007's 10%
stop threshold. This small sample is regression evidence, not a benchmark.

The pinned PostgreSQL 17.6 image ran on a random loopback port against an
explicit `andreja_test_*` database. Baseline/candidate Debug executions were
14.66/14.61 seconds. Five additional candidate Debug runs took 14.69, 14.68,
14.18, 14.30, and 14.54 seconds; all 22 tests passed every time. Debug,
Release, and filtered runs covered migration, WAF/EF, concurrency, restart,
crash, tenant isolation, and application portability behavior. After the runs, the configured database and generated
`andreja_test_port_*` databases were absent, and the disposable container was
removed.

## Security, license, and support review

`dotnet list package --vulnerable --include-transitive` reported no known
vulnerabilities for the solution or PostgreSQL project from NuGet.org on the
evaluation date. This is a point-in-time advisory result, not proof of absence.

| Package family | License | Review |
| --- | --- | --- |
| xUnit framework, adapter, abstractions, analyzers | Apache-2.0 | Unchanged license family |
| Microsoft Test SDK, TestHost, ObjectModel, CodeCoverage | MIT | Unchanged license family; Test SDK metadata requires acceptance |

The packages remain test/build dependencies. Adapter references retain
`PrivateAssets=all`, and no package enters the production dependency graph.
The xUnit 2.9.3 NuGet record remains marked `Legacy` and says it will receive
security updates only. This evaluation does not alter ADR 0007's bounded
holding state or authorize xUnit v3/MSTest migration.

## Decision and residual boundary

No rollback condition was observed: discovery/execution/TRX/filter evidence is
unchanged, all repetitions passed, the measured median stayed below the
threshold, PostgreSQL cleanup was complete, the version pairing is documented,
licenses are unchanged and reviewed, and the advisory scan was clean.

Retain the already-merged 18.9.0/4.0.0 versions. Continue to treat graphical
IDE execution as unavailable local evidence and xUnit 2.9.3's maintenance
status as the separately governed migration concern in issue #112.

## Official sources

Accessed 2026-08-30:

- [xunit.runner.visualstudio 4.0.0 NuGet catalog metadata](https://api.nuget.org/v3/catalog0/data/2026.08.15.01.54.36/xunit.runner.visualstudio.4.0.0.json)
- [xunit.runner.visualstudio 4.0.0 README](https://github.com/xunit/visualstudio.xunit/blob/05679a7ab5ca2461d06880faaefe26770e0fdf77/README.md)
- [xunit.runner.visualstudio 4.0.0 build versions](https://github.com/xunit/visualstudio.xunit/blob/05679a7ab5ca2461d06880faaefe26770e0fdf77/Versions.props)
- [Microsoft.NET.Test.Sdk 18.9.0 NuGet catalog metadata](https://api.nuget.org/v3/catalog0/data/2026.08.14.09.46.01/microsoft.net.test.sdk.18.9.0.json)
- [Microsoft.TestPlatform.TestHost 18.9.0 package metadata](https://www.nuget.org/packages/Microsoft.TestPlatform.TestHost/18.9.0)
- [Microsoft.TestPlatform.ObjectModel 18.9.0 package metadata](https://www.nuget.org/packages/Microsoft.TestPlatform.ObjectModel/18.9.0)
- [Microsoft.CodeCoverage 18.9.0 package metadata](https://www.nuget.org/packages/Microsoft.CodeCoverage/18.9.0)
- [xUnit 2.9.3 NuGet catalog metadata and deprecation](https://api.nuget.org/v3/catalog0/data/2026.04.10.00.22.07/xunit.2.9.3.json)
- [NuGet dependency resolution rules](https://learn.microsoft.com/nuget/concepts/dependency-resolution)
- [Testing with C# Dev Kit](https://code.visualstudio.com/docs/csharp/testing)
- [.NET `dotnet test` VSTest and MTP modes](https://learn.microsoft.com/dotnet/core/testing/unit-testing-with-dotnet-test)
