# .NET test-suite pre-migration baseline for issue #112

**Measured:** 2026-10-04
**Source revision:** `a80ddef9a5938dba01c4c6e5a53747756a083a04`
**Host:** Windows arm64; pinned .NET SDK 10.0.301; `net10.0`
**Framework:** xUnit 2.9.3; `Microsoft.NET.Test.Sdk` 18.9.0; `xunit.runner.visualstudio` 4.0.0
**Raw counters and timing samples:** [`test-suite-baseline-112.json`](test-suite-baseline-112.json)

## Authorization and measurement boundary

Before measuring, issue [#112](https://github.com/Jamula/Andreja/issues/112) was
open, explicitly prioritized `priority:p2`, and assigned `squad:data`. Its
prerequisites were all closed and marked merged: #108 through PR #124, #109
through PR #153, and #110 through PR #152. No test source, package, runner,
solution, or workflow was changed before this baseline was captured.

The service-free solution contains `Andreja.UnitTests` and
`Andreja.ArchitectureTests`. `Andreja.PostgreSqlIntegrationTests` remains
outside the solution and requires a disposable PostgreSQL database. All three
projects were measured independently in Debug and Release. The PostgreSQL
runtime used the repository-pinned image
`postgres:17.6-bookworm@sha256:f3bd19c606e442c3d7bdfa8002e03fe260a1023351e0ea4598032022b68dd6e3`
on a random loopback port and an `andreja_test_*` database. The container and
database were removed after the runs; no database credential was persisted.

## Per-assembly inventory

`Discovered` is the count from `dotnet test --list-tests`. `TRX total`,
`Executed`, `Passed`, `Failed`, and `Skipped` are read from the generated TRX
counter block (`Skipped` is TRX `notExecuted`).

| Configuration | Assembly | Discovered | TRX total | Executed | Passed | Failed | Skipped |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Debug | `Andreja.UnitTests` | 256 | 257 | 257 | 257 | 0 | 0 |
| Debug | `Andreja.ArchitectureTests` | 18 | 18 | 18 | 18 | 0 | 0 |
| Debug | `Andreja.PostgreSqlIntegrationTests` | 22 | 22 | 22 | 22 | 0 | 0 |
| **Debug total** |  | **296** | **297** | **297** | **297** | **0** | **0** |
| Release | `Andreja.UnitTests` | 252 | 253 | 253 | 253 | 0 | 0 |
| Release | `Andreja.ArchitectureTests` | 18 | 18 | 18 | 18 | 0 | 0 |
| Release | `Andreja.PostgreSqlIntegrationTests` | 22 | 22 | 22 | 22 | 0 | 0 |
| **Release total** |  | **292** | **293** | **293** | **293** | **0** | **0** |

The one-case discovery/execution delta in the unit assembly is the existing
`PortabilityCommandTests.InfrastructureFailuresUseFixedContentMinimizedMessages`
theory. VSTest discovery reports its exception-bearing, non-pre-enumerated
`MemberData` once, while execution expands and runs both rows. Release excludes
the four Debug-only unit results. A runner migration must compare executable
rows with the prior TRX total, not mistake xUnit's lower list count for a lost
test.

The per-assembly first runs built and executed the project, so their elapsed
times include build work and are not the warm-run comparison below.

## Warm-run timing and reliability

The full service-free solution was run five times per configuration with
`--no-build`; every run produced the full expected TRX inventory with zero
failures and zero skips.

| Configuration | Cases per run | Warm durations (s) | Min / median / max (s) |
| --- | ---: | --- | --- |
| Debug | 275 | 10.57, 8.03, 7.52, 9.15, 7.48 | 7.48 / 8.03 / 10.57 |
| Release | 271 | 10.79, 9.67, 7.42, 8.67, 9.06 | 7.42 / 9.06 / 10.79 |

The live PostgreSQL suite was also run five times per configuration against the
same disposable server. All ten suite runs (220 individual test executions)
passed, with no failures or skips.

| Configuration | Cases per run | Warm durations (s) | Min / median / max (s) |
| --- | ---: | --- | --- |
| Debug | 22 | 21.03, 20.73, 21.36, 20.19, 20.12 | 20.12 / 20.73 / 21.36 |
| Release | 22 | 21.57, 21.51, 21.25, 22.29, 24.86 | 21.25 / 21.57 / 24.86 |

## Filter and architecture probes

| Project | Filter | Total / executed / passed / failed / skipped |
| --- | --- | ---: |
| `Andreja.UnitTests` | `FullyQualifiedName~SemanticAssertionConformanceTests` | 39 / 39 / 39 / 0 / 0 |
| `Andreja.UnitTests` | `FullyQualifiedName~PortabilityCommandTests` | 2 / 2 / 2 / 0 / 0 |
| `Andreja.ArchitectureTests` | `FullyQualifiedName~ModuleReferenceAllowlistRejectsNonApprovedAssemblies` | 8 / 8 / 8 / 0 / 0 |

These results are the pre-migration checkpoint. The MTP gate uses the prior
TRX executable-row count as the expected discovery count so that the known
exception-bearing data-row expansion is explicit and fail-closed.
