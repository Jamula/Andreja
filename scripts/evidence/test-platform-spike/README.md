# Issue 108 test-platform spike

This isolated harness compares xUnit.net v3 `xunit.v3` 4.0.0 and
`MSTest.Sdk` 4.3.3 on Microsoft.Testing.Platform 2.3.3 with the repository's
pinned .NET SDK 10.0.301. It is excluded from `Andreja.slnx` and does not replace
or modify the production xUnit 2 suite.

The equivalent projects exercise data and a runtime-expanded row, async
lifecycle/cleanup, class and cross-class fixtures, explicit class concurrency,
filters, an intentional skip, captured output, Andreja `WebApplicationFactory`,
rejection of the fake authentication header, and production EF migration
metadata. Isolated negative timeout probes deliberately exceed 250 ms for both
frameworks; the harness requires a nonzero runner exit, one failed TRX result
whose runner-generated error message contains timeout/cancellation evidence,
and kills the process after 15 seconds if enforcement breaks. Authored TRX
identifiers are excluded from that assertion, and a startup regression check
proves a test name containing `Timeout` cannot satisfy it. These probes do not
change normal discovery inventory.
The MSTest fixture is lazy and class requested: a category-only filter proves it
does not initialize for unrelated tests. A conditional negative analyzer probe
compares always-true assertion diagnostics without adding that test to normal
discovery. Warm runs alternate candidate order, and startup checks exercise the
median calculation with odd and even samples before the 10% gate uses it.

Run from the repository root:

```powershell
pwsh -NoProfile -File scripts\evidence\test-platform-spike\Invoke-Spike.ps1 `
  -Dotnet .\artifacts\dotnet\dotnet.exe
```

Omit `-Dotnet` when the pinned SDK is already installed. Results are written
only under ignored `artifacts\test-platform-spike`.

Run only the deterministic helper checks with
`-ValidateHelpersOnly`; this does not restore, build, or execute either spike.

This harness makes no network or database call while executing tests. Package
restore is the only networked step. Live disposable PostgreSQL and graphical
Visual Studio/VS Code discovery remain separate evidence gates; do not report
them as passed from this harness.
