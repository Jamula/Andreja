# Test framework migration evidence for issue #112

- **Measured:** 2026-10-04
- **Pre-migration source:** `a80ddef9a5938dba01c4c6e5a53747756a083a04`
- **Baseline checkpoint:** `5aeeccf298e77b701c200b9f24d01b018932abfe`
- **Host:** Windows arm64; pinned .NET SDK 10.0.301; `net10.0`
- **Baseline:** xUnit 2.9.3, Microsoft.NET.Test.Sdk 18.9.0, VSTest adapter 4.0.0
- **Migration:** MSTest.Sdk 4.4.1, Microsoft Testing Platform 2.4.1, TRX report extension 2.4.1

## Authorization and scope

Before any test project, package, source, runner, or workflow was changed,
issue [#112](https://github.com/Jamula/Andreja/issues/112) was confirmed open
with `priority:p2` and `squad:data`. Prerequisites #108, #109, and #110 were
closed and merged through PRs #124, #153, and #152. The separate baseline
checkpoint records all three assemblies in Debug and Release before
conversion; its source counters and timing samples are in
[`test-suite-baseline-112.md`](test-suite-baseline-112.md) and
[`test-suite-baseline-112.json`](test-suite-baseline-112.json).

The priority authorized only this scoped test-framework migration. No
production behavior, database schema, or product capability was changed.
ADR 0007 remains Proposed and unaccepted; this evidence does not ratify it.

## Runner and package choices

The pinned .NET 10 SDK selects Microsoft Testing Platform (MTP) in
`global.json`. Every test project uses `MSTest.Sdk/4.4.1`, which resolves
`Microsoft.Testing.Platform/2.4.1` and `MSTest.TestFramework/4.4.1`. The only
explicit MTP extension is `Microsoft.Testing.Extensions.TrxReport/2.4.1`;
`TestingExtensionsProfile=None` avoids unused extensions, and the TRX
extension is MIT-licensed. NuGet's v3 flat-container indexes were checked on
2026-10-04 and showed each resolved version as the latest stable release. Both
.NET CLI and MTP telemetry opt-outs remain enabled.

NuGet metadata confirmed MIT licenses for the nine resolved MSTest/MTP
packages (SDK, analyzer, adapter, framework, platform, MSBuild, telemetry,
TRX report, and TRX abstractions). `dotnet nuget verify <package.nupkg> --all`
verified Microsoft author and NuGet.org repository signatures for all nine.
The direct and transitive vulnerability scan of the solution and separate
PostgreSQL project reported zero findings. DevSkim 1.0.90 reported zero
findings with only the repository's documented `DS137138` loopback-URL and
`DS162092` Debug-guard false-positive exclusions.

An `xunit` search over test source/projects, centralized package configuration,
`global.json`, and the .NET validation workflow/parity script returned no
matches. Historical ADR, research, and pre-migration baseline references remain
intentionally as decision and rollback evidence.

## Inventory, execution, and TRX parity

MTP discovery uses `dotnet test --project <project.csproj> --configuration
<Debug|Release> --no-build --list-tests`; the MTP direct-module CLI path is
used for execution and TRX generation. Discovery and execution are recorded
separately because the xUnit baseline's exception-bearing `MemberData` theory
was listed once but expanded to two executable rows.

| Configuration | Assembly | xUnit discovered / executed | MTP discovered / executed | MTP passed / failed / skipped |
| --- | --- | ---: | ---: | ---: |
| Debug | Unit | 256 / 257 | 257 / 257 | 257 / 0 / 0 |
| Debug | Architecture | 18 / 18 | 18 / 18 | 18 / 0 / 0 |
| Debug | PostgreSQL | 22 / 22 | 22 / 22 | 22 / 0 / 0 |
| **Debug total** | | **296 / 297** | **297 / 297** | **297 / 0 / 0** |
| Release | Unit | 252 / 253 | 253 / 253 | 253 / 0 / 0 |
| Release | Architecture | 18 / 18 | 18 / 18 | 18 / 0 / 0 |
| Release | PostgreSQL | 22 / 22 | 22 / 22 | 22 / 0 / 0 |
| **Release total** | | **292 / 293** | **293 / 293** | **293 / 0 / 0** |

The fail-closed parity script passed in Debug and Release. It verified the
three-project inventory, each MTP discovered count against the pre-migration
TRX executable-row count, and exact total/executed/passed/failed/skipped TRX
counters for the service-free assemblies. Hosted CI builds the configuration
first, runs Unit and Architecture modules with `dotnet test --test-modules`,
retains one TRX per assembly, and uploads parity/report evidence for 14 days.
PostgreSQL runtime is reported unavailable in hosted CI without a disposable
database; it is never represented as passed or skipped there.

Filtered MTP module runs passed in both configurations:

| Filter | Expected and passed |
| --- | ---: |
| `FullyQualifiedName~SemanticAssertionConformanceTests` | 39 |
| `FullyQualifiedName~PortabilityCommandTests` | 2 |
| `FullyQualifiedName~ModuleReferenceAllowlistRejectsNonApprovedAssemblies` | 8 |

The MTP project discovery command and project/module execution both work. A
`--test-modules --list-tests` probe did not provide discovery output, so the
parity script deliberately uses project mode for discovery and direct modules
for the CI runtime path.

## Lifecycle and semantic preservation

- Facts, theories, inline/member data, assertions, and test metadata were
  converted across all three assemblies. The measured test-row counts are
  unchanged; there are no new skips.
- The two xUnit `IAsyncLifetime` PostgreSQL fixtures remain per-test through
  async `[TestInitialize]` and `[TestCleanup]`. `ApplicationPortabilityTests`
  still creates unique source/target databases and drops them in cleanup;
  `PostgreSqlIdentityTests` still deletes/migrates its disposable database and
  removes its temporary bootstrap-token file.
- If either PostgreSQL fixture's initialization and cleanup both fail, the
  original and cleanup exceptions are aggregated. Cleanup independently
  attempts database deletion, provider disposal, and token-file removal; the
  portability fixture attempts both database drops even if one fails and
  retains failed database names for a retry.
- The `IClassFixture<WebApplicationFactory<...>>` unit fixture is represented
  by class-scoped initialization and cleanup. Assembly-level MSTest
  parallelization retains class-level concurrency without parallelizing test
  methods within a class.
- xUnit exact exception assertions and assignable-type `ThrowsAny` assertions
  were mapped separately to MSTest exact and assignable assertions. The first
  MTP execution exposed one incorrect `ThrowsAnyAsync` mapping; it was fixed
  before parity was accepted, and the full suites now pass.
- Architecture and negative-path suites remain included; the tested filters
  above select the same baseline rows.

## Warm-run performance and live PostgreSQL

The service-free timing comparison uses the exact warm CI execution mode:
prebuilt Unit and Architecture MTP modules, `--no-build`, TRX enabled, and the
two per-assembly commands measured together. Every one of the five runs per
configuration matched the full expected TRX counters.

| Configuration | xUnit baseline durations (s) | Baseline median | MTP direct-module durations (s) | MTP min / median / max | Median change |
| --- | --- | ---: | --- | ---: | ---: |
| Debug, 275 cases | 10.57, 8.03, 7.52, 9.15, 7.48 | 8.03 | 6.24, 6.43, 6.54, 2.87, 2.96 | 2.87 / 6.24 / 6.54 | -22.3% |
| Release, 271 cases | 10.79, 9.67, 7.42, 8.67, 9.06 | 9.06 | 6.64, 5.63, 6.06, 2.79, 2.73 | 2.73 / 5.63 / 6.64 | -37.9% |

The final live PostgreSQL benchmark used the documented MTP project invocation
after the cleanup hardening, with the pinned `postgres:17.6-bookworm` image
digest from the baseline. It ran five warm suites per configuration; each
generated 22/22 passed, zero failed or skipped, and the post-run database
query found no disposable test databases. The container was bound to an
ephemeral loopback port and removed afterward.

| Configuration | xUnit baseline durations (s) | Baseline median | MTP project-mode durations (s) | MTP min / median / max | Median change |
| --- | --- | ---: | --- | ---: | ---: |
| Debug, 22 cases | 21.03, 20.73, 21.36, 20.19, 20.12 | 20.73 | 20.18, 19.48, 18.90, 18.24, 22.35 | 18.24 / 19.48 / 22.35 | -6.0% |
| Release, 22 cases | 21.57, 21.51, 21.25, 22.29, 24.86 | 21.57 | 21.31, 20.05, 20.41, 19.35, 19.64 | 19.35 / 20.05 / 21.31 | -7.0% |

The PostgreSQL README and hosted boundary continue to use project mode for
database runtime; direct-module invocation is the selected CI path only for
the service-free Unit and Architecture modules. No material regression was
observed in the selected paths.

## Builds, rollback, and remaining boundary

After formatting, Debug and Release solution builds and separate PostgreSQL
project builds passed with zero warnings and errors. `dotnet format
--verify-no-changes` passed for the solution and PostgreSQL project;
`check_docs_consistency.py` passed, including the proposed plan hash and all
existing artifact hashes; `git diff --check` passed. The independent
architecture/quality review found a setup-failure cleanup leak in the identity
fixture. Both PostgreSQL fixtures now attempt independent cleanup actions and
aggregate cleanup errors; the reviewer re-checked the corrected paths and
confirmed the finding resolved with no new actionable cleanup issue.

The baseline-only checkpoint is a separate commit immediately before the
atomic migration. All runner selection, project SDKs, test conversions, CI,
parity, and related documentation belong to one migration commit; there is no
partial or mixed-runner state. Rollback is a single `git revert` of that
migration commit, returning to baseline checkpoint
`5aeeccf298e77b701c200b9f24d01b018932abfe` while preserving the measured
baseline. No database or production artifact requires rollback.

CLI discovery and execution are parity-gated. This host has no Visual Studio
installation or VS Code C# Dev Kit, so graphical Test Explorer discovery/debug
remains **unverified and blocked locally**; no IDE pass is claimed. The issue
remains open and this evidence does not accept ADR 0007.
