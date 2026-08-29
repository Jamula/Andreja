# Issue 108: xUnit v3 and MSTest/MTP evidence

- **Evidence date:** 2026-08-28
- **Host:** Windows arm64; .NET SDK 10.0.301
- **Scope:** bounded local/paper evidence only; no cloud account, trial, production
  suite migration, or product behavior change
- **Reproduction:** [`scripts/evidence/test-platform-spike`](../../scripts/evidence/test-platform-spike/)

## Outcome and authority

**Decision already recorded:** Cyrus selected MSTest with Microsoft Testing
Platform as Andreja's long-term direction in the ratified plan status amendment
and [issue #112](https://github.com/Jamula/Andreja/issues/112).

**Recommendation from this evidence:** retain that direction. `MSTest.Sdk`
4.3.3/MTP 2.3.3 preserved the representative semantics and evidence below,
adds stronger observed analyzer enforcement, is Microsoft-supported, and did
not cross the 10% sustained full-run regression stop threshold. This is not a
performance-win claim; the small samples do not identify a decision-grade
performance winner.

**Approval boundary:** ADR 0007 remains **Proposed**. This research does not
claim Cyrus accepted the ADR or authorize migration. Issue #112 owns the later
atomic production-suite conversion, exact parity, CI/IDE/live-PostgreSQL proof,
review, and rollback.

## Current first-party evidence

All sources in this section were accessed 2026-08-28.

| Concern | xUnit.net v3 | MSTest.Sdk/MTP | Fact and consequence |
| --- | --- | --- | --- |
| Current stable candidate | [`xunit.v3` 4.0.0](https://www.nuget.org/packages/xunit.v3/4.0.0), published 2026-08-15 ([catalog metadata](https://api.nuget.org/v3/catalog0/data/2026.08.15.01.53.55/xunit.v3.4.0.0.json)). The package major is 4, but the product generation remains xUnit.net v3. | [`MSTest.Sdk` 4.3.3](https://www.nuget.org/packages/MSTest.Sdk/4.3.3) and [MTP 2.3.3](https://www.nuget.org/packages/Microsoft.Testing.Platform/2.3.3), both published 2026-07-28 ([SDK](https://api.nuget.org/v3/catalog0/data/2026.07.28.07.35.43/mstest.sdk.4.3.3.json), [MTP](https://api.nuget.org/v3/catalog0/data/2026.07.28.07.35.43/microsoft.testing.platform.2.3.3.json)). | These were the latest listed stable versions in the NuGet version indexes. Both restored, built, discovered, and ran on pinned SDK 10.0.301/net10.0. |
| Support and maintenance | The [xUnit home page](https://xunit.net/) identifies v3 as the feature line, lists 4.0.0 as current, and limits v2 to critical fixes. xUnit is community-focused, part of the .NET Foundation, and [project-lead governed](https://xunit.net/governance). No dedicated security-policy file appeared in its [repository community profile](https://api.github.com/repos/xunit/xunit/community/profile) during review. | The [MSTest overview](https://learn.microsoft.com/dotnet/core/testing/unit-testing-mstest-intro) calls MSTest fully supported and open source, recommends MTP/MSTest.Sdk for new projects, and supports only the latest release. [MSTest running guidance](https://learn.microsoft.com/dotnet/core/testing/unit-testing-mstest-running-tests) calls its MTP runner the recommended path. | xUnit v3 is actively maintained but has no Microsoft support commitment. MSTest has an explicit Microsoft support statement, paired with an ongoing latest-release upgrade obligation. |
| Runtime compatibility | The [`xunit.v3` package](https://www.nuget.org/packages/xunit.v3/4.0.0) supports .NET 8+; 4.0.0 installs `xunit.v3.mtp-v2`. [Native MTP guidance](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform) says xUnit v3 4.0+ defaults to MTP v2. | [MTP](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-intro) supports .NET 8+ and is embedded in test executables. `MSTest.Sdk` enables MTP by default per [SDK configuration](https://learn.microsoft.com/dotnet/core/testing/unit-testing-mstest-sdk). | Both resolved MTP 2.3.3 in the spike. Neither candidate needs `Microsoft.NET.Test.Sdk` or a VSTest adapter for this native-MTP configuration. |
| License and provenance | `xunit.v3` metadata declares Apache-2.0, repository commit `8bf043c053fc133ad9da67709942e1dfb18583ea`, and a SHA-512 package hash. `dotnet nuget verify --all` verified xUnit.net/.NET Foundation author and NuGet.org repository signatures for `xunit.v3` and `xunit.v3.mtp-v2`. | MSTest.Sdk and MTP metadata declare MIT and repository commit `44aa76e6a61d4908f06dfd77a51d4b7e3e7ce40f`; [testfx's license](https://github.com/microsoft/testfx/blob/main/LICENSE) is MIT. Microsoft author and NuGet.org repository signatures verified for SDK/framework/adapter/MTP. [MSRC reporting](https://github.com/microsoft/testfx/security/policy) is documented. | The MSTest `Default` profile also resolved `Microsoft.Testing.Extensions.CodeCoverage` 18.9.0 under Microsoft .NET Library terms, not MIT. Profile and extension licenses require explicit review in #112. Signature verification establishes package signing/provenance, not absence of malicious or vulnerable code. |
| Advisories | `dotnet list package --vulnerable --include-transitive` reported no known vulnerable package in either resolved graph from the configured source. | Same result. | Point-in-time advisory evidence only; it is not proof of absence. |
| Runner and `dotnet test` | xUnit's [MTP guide](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform) documents executable tests, native MTP, `global.json`, and xUnit-provided MTP options. | [.NET 10 `dotnet test`](https://learn.microsoft.com/dotnet/core/testing/unit-testing-with-dotnet-test) requires `"test": {"runner": "Microsoft.Testing.Platform"}`, `--project`/`--solution`, and MTP options. MTP 2 removes .NET 10's legacy VSTest-mode bridge. | Andreja's current `dotnet test Andreja.slnx --logger ...` commands cannot carry over. Mixed VSTest/MTP mode remains unsupported, so #112 must cut the projects, root runner selection, commands, and CI together. |
| Filtering | Native xUnit MTP used `--filter-trait "Category=Smoke"`; xUnit also documents class/method filters and its query language. | MSTest used `--filter "TestCategory=Smoke"` as documented in [MSTest running guidance](https://learn.microsoft.com/dotnet/core/testing/unit-testing-mstest-running-tests). | The equivalent filters selected six result rows. Framework-specific switches differ; [.NET guidance](https://learn.microsoft.com/dotnet/core/testing/unit-testing-with-dotnet-test#solutions-with-mixed-test-frameworks-or-extensions) warns that passing one framework's option to another can fail. |
| TRX and artifacts | The native package exposed xUnit's `--report-xunit-trx`; it produced a parseable TRX with output and skip evidence. | The SDK `Default` profile includes TRX and uses `--report-trx`. [MTP report guidance](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-test-reports) documents deterministic names and streaming TRX writes in 2.3+. | Both produced exact result counters. The differing switches require explicit CI design unless #112 adds and reviews a common reporting extension to xUnit. |
| Analyzers | `xunit.analyzers` 2.0.0 resolved transitively and the clean spike built with warnings as errors. It did not flag the conditional always-true assertion probe. | `MSTest.Analyzers` 4.3.3 resolved transitively. `MSTestAnalysisMode=Recommended` failed the equivalent probe with `MSTEST0032`. The [analyzer reference](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/overview) documents framework, async, lifecycle, assertion, and data rules. | This repeats issue #96's stronger observed MSTest analyzer finding with current candidates. It is one probe, not a count or quality comparison of every rule. |
| Telemetry | xUnit v3 4.0.0 resolved `Microsoft.Testing.Extensions.Telemetry` 2.3.3. xUnit's MTP page explicitly documents `TESTINGPLATFORM_TELEMETRY_OPTOUT=1`. | MSTest/MTP resolved the same telemetry extension. [MTP telemetry](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-telemetry) says collection is on when the extension is present, lists usage fields, and accepts either `TESTINGPLATFORM_TELEMETRY_OPTOUT=1` or `DOTNET_CLI_TELEMETRY_OPTOUT=1`. | Both spike paths ran with both opt-outs set. Andreja already sets `DOTNET_CLI_TELEMETRY_OPTOUT=1` in CI; #112 must preserve and test that control. Self-host product telemetry remains unrelated and off by default. |
| Visual Studio and VS Code | The [xUnit MTP page](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform) documents current Visual Studio MTP Test Explorer integration. | [MTP run/debug guidance](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-run-and-debug) documents run/debug/Test Explorer support in Visual Studio and VS Code with C# Dev Kit. | Neither graphical IDE was installed on the evidence host. Support is first-party documented but local discovery/debug parity remains unobserved and belongs to #112. |
| Lifecycle and concurrency | [xUnit shared context](https://xunit.net/docs/shared-context) provides async lifetime, class, collection, and assembly fixtures. [Parallel guidance](https://xunit.net/docs/running-tests-in-parallel) keeps collection-level parallelism on by default. | [MSTest lifecycle](https://learn.microsoft.com/dotnet/core/testing/unit-testing-mstest-writing-tests-lifecycle) provides async assembly/class/test scopes. [Execution controls](https://learn.microsoft.com/dotnet/core/testing/unit-testing-mstest-writing-tests-controlling-execution) say execution is sequential by default and requires explicit `Parallelize`. | Migration is semantic, not an attribute-only rewrite. The spike explicitly enabled MSTest class-level parallelism and used a lazy, class-requested shared owner so unrelated filters did not initialize the fixture. |

The [MTP overview](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-intro),
[CLI options](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-cli-options),
and [run/debug guide](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-run-and-debug)
are the common first-party operational references.

## Local production baseline

No production test source, package, runner, or workflow was changed. Fresh
per-assembly inventory from the current branch:

| Configuration / assembly | Discovered | TRX total | Executed | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Debug `Andreja.UnitTests` | 256 | 257 | 257 | 257 | 0 | 0 |
| Debug `Andreja.ArchitectureTests` | 18 | 18 | 18 | 18 | 0 | 0 |
| **Debug service-free total** | **274** | **275** | **275** | **275** | **0** | **0** |
| Release `Andreja.UnitTests` | 252 | 253 | 253 | 253 | 0 | 0 |
| Release `Andreja.ArchitectureTests` | 18 | 18 | 18 | 18 | 0 | 0 |
| **Release service-free total** | **270** | **271** | **271** | **271** | **0** | **0** |

The Debug/Release difference is the existing conditional compilation boundary.
In both configurations,
`PortabilityCommandTests.InfrastructureFailuresUseFixedContentMinimizedMessages`
is one non-pre-enumerated discovery case and two runtime rows. PostgreSQL
discovery remained 22 in both configurations.

## Equivalent spike results

The committed harness references Andreja's real AppHost and adapters but is not
in the production solution.

| Configuration / candidate | Discovered | TRX total | Executed | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Debug xUnit v3/MTP | 12 | 13 | 12 | 12 | 0 | 1 |
| Debug MSTest/MTP | 13 | 13 | 12 | 12 | 0 | 1 |
| Release xUnit v3/MTP | 12 | 13 | 12 | 12 | 0 | 1 |
| Release MSTest/MTP | 13 | 13 | 12 | 12 | 0 | 1 |
| Filtered xUnit v3/MTP | n/a | 6 | 5 | 5 | 0 | 1 |
| Filtered MSTest/MTP | n/a | 6 | 5 | 5 | 0 | 1 |

xUnit intentionally deferred enumeration of its exception-bearing `MemberData`;
one discovery case expanded to two execution results. MSTest discovered its two
typed `DynamicData` rows. Both TRX files contained the intentional skip and
captured output.

Both candidates also passed:

- async initialize/cleanup markers;
- class fixture lifetime and one shared fixture instance across two classes;
- two-class concurrency rendezvous;
- bounded timeout/cancellation;
- anonymous Andreja API `401` and rejection of
  `X-Andreja-Test-Authenticate`;
- discovery of all six production EF migrations without a database connection;
- Debug/Release MTP executable operation and deterministic TRX creation.

The MSTest shared-resource prototype uses class initialization to request one
lazy owner and assembly cleanup that disposes only if created. A Smoke-only
filter left its start marker absent. This removes issue #96's eager
assembly-initialization defect, but it is custom static lifecycle code rather
than xUnit's named collection-fixture abstraction. #112 must review its
thread-safety, failure, and cleanup behavior before adapting it to PostgreSQL.

## Timing, repetition, and memory

Measurements used restored/built projects on one Windows arm64 host. They are
representative guardrails, not benchmarks.

| Measurement | xUnit v3/MTP | MSTest/MTP |
| --- | ---: | ---: |
| Five warm full runs, min / median / max | 1,249 / 1,341 / 1,456 ms | 1,162 / 1,222 / 1,809 ms |
| One clean build + full run | 2,293 ms | 2,153 ms |
| Sampled direct-runner largest-process working set | 99.4 MiB | 90.9 MiB |

MSTest's warm median was 8.9% faster and its clean build/run was 6.1% faster.
One MSTest warm run was a high outlier. Neither candidate crossed the 10%
sustained regression stop condition. Five stabilized
timing runs plus the final Debug, Release, filter, and direct-executable runs
had no intermittent failure. Memory is approximate and excludes child/process
tree aggregation.

Early discarded harness iterations exposed a missing Identity model setup and
an unsupported cancellation-token parameter shape. They were symmetric harness
or probe defects, corrected before measurement, and are not framework
reliability results.

## Package and security result

- xUnit had two direct spike packages; MSTest.Sdk materialized five top-level
  test packages under its `Default` profile. Both shared the same AppHost
  dependency graph and MTP 2.3.3.
- `dotnet nuget verify --all` passed for the candidate framework, runner, MTP,
  and MSTest default code-coverage packages.
- The point-in-time direct/transitive vulnerability query reported no known
  vulnerable packages from the configured package source.
- Dynamic MTP extensions are disabled by default according to the
  [CLI reference](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-cli-options).

## Bounded blockers and remaining gates

1. **Live PostgreSQL:** no `ANDREJA_TEST_POSTGRES` value, container engine, or
   listener on local port 5432 was available. The 22-test project compiled and
   listed in Debug/Release, but runtime was correctly recorded **blocked**, not
   passed or skipped. Issue #96 remains prior live xUnit-2/MSTest evidence;
   xUnit-v3 live parity remains unobserved.
2. **IDE:** Visual Studio and VS Code were unavailable. Their documented support
   was not represented as locally tested.
3. **Full conversion:** exact production-suite MTP counts, analyzer conversion,
   filter/FQN behavior, CI artifacts, live PostgreSQL cleanup, and rollback can
   only be proven on the atomic migration branch owned by #112.
4. **Profile/license:** #112 must choose and review the MSTest extension profile,
   especially the non-MIT code-coverage extension, and re-run package signature,
   advisory, and telemetry evidence.

These gaps do not overturn the existing long-term MSTest direction. They do
block describing the production suite as migrated or ADR 0007 as accepted.
