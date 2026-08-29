# Issue 108 test-platform spike

This isolated harness compares xUnit.net v3 `xunit.v3` 4.0.0 and
`MSTest.Sdk` 4.3.3 on Microsoft.Testing.Platform 2.3.3 with the repository's
pinned .NET SDK 10.0.301. It is excluded from `Andreja.slnx` and does not replace
or modify the production xUnit 2 suite.

The equivalent projects exercise data and a runtime-expanded row, async
lifecycle/cleanup, class and cross-class fixtures, explicit class concurrency,
filters, an intentional skip, cooperative timeout/cancellation, captured output,
Andreja `WebApplicationFactory`, rejection of the fake authentication header,
and production EF migration metadata. The MSTest fixture is lazy and class
requested: a category-only filter proves it does not initialize for unrelated
tests. A conditional negative analyzer probe compares always-true assertion
diagnostics without adding that test to normal discovery.

Run from the repository root:

```powershell
pwsh -NoProfile -File scripts\evidence\test-platform-spike\Invoke-Spike.ps1 `
  -Dotnet .\artifacts\dotnet\dotnet.exe
```

Omit `-Dotnet` when the pinned SDK is already installed. Results are written
only under ignored `artifacts\test-platform-spike`.

This harness makes no network or database call while executing tests. Package
restore is the only networked step. Live disposable PostgreSQL and graphical
Visual Studio/VS Code discovery remain separate evidence gates; do not report
them as passed from this harness.
