[CmdletBinding()]
param(
    [string] $Dotnet = "dotnet",
    [ValidateRange(3, 21)]
    [int] $WarmRuns = 5,
    [switch] $ValidateHelpersOnly
)

$ErrorActionPreference = "Stop"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:TESTINGPLATFORM_TELEMETRY_OPTOUT = "1"

if (Test-Path -LiteralPath $Dotnet) {
    $Dotnet = (Resolve-Path -LiteralPath $Dotnet).Path
}

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\..\.."))
$resultsRoot = Join-Path $repositoryRoot "artifacts\test-platform-spike"
$xunitProject = "XunitV3Spike\XunitV3Spike.csproj"
$mstestProject = "MSTestMtpSpike\MSTestMtpSpike.csproj"
New-Item -ItemType Directory -Path $resultsRoot -Force | Out-Null

function Invoke-Dotnet {
    param([Parameter(Mandatory)][string[]] $Arguments)

    & $Dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Invoke-DotnetExpectingTestFailure {
    param(
        [Parameter(Mandatory)][string[]] $Arguments,
        [Parameter(Mandatory)][string] $OutputPath,
        [int] $TimeoutSeconds = 15
    )

    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $Dotnet
    $startInfo.WorkingDirectory = $PSScriptRoot
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($argument in $Arguments) {
        $startInfo.ArgumentList.Add($argument)
    }

    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    try {
        if (-not $process.Start()) {
            throw "Could not start dotnet for the expected-failure probe."
        }

        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $process.Kill($true)
            $process.WaitForExit()
            throw "Expected-failure probe exceeded the $TimeoutSeconds-second process bound."
        }

        $output = @(
            $stdout.GetAwaiter().GetResult()
            $stderr.GetAwaiter().GetResult()
        ) -join [Environment]::NewLine
        $output | Set-Content -LiteralPath $OutputPath
        $output | Write-Host
        if ($process.ExitCode -eq 0) {
            throw "Expected-failure probe unexpectedly exited successfully."
        }
    }
    finally {
        $process.Dispose()
    }
}

function Get-Median {
    param([Parameter(Mandatory)][double[]] $Values)

    if ($Values.Count -eq 0) {
        throw "Cannot calculate a median for an empty sample."
    }

    $sorted = @($Values | Sort-Object)
    $middle = [math]::Floor($sorted.Count / 2)
    if (($sorted.Count % 2) -eq 1) {
        return $sorted[$middle]
    }

    return ($sorted[$middle - 1] + $sorted[$middle]) / 2
}

$medianCases = @(
    @{ Name = "odd"; Values = @(9, 1, 5); Expected = 5 }
    @{ Name = "even"; Values = @(10, 2, 8, 4); Expected = 6 }
)
foreach ($medianCase in $medianCases) {
    $actualMedian = Get-Median -Values $medianCase.Values
    if ($actualMedian -ne $medianCase.Expected) {
        throw "The $($medianCase.Name)-sample median check failed: $actualMedian."
    }
}

function Assert-TrxCounters {
    param(
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)][int] $Total,
        [Parameter(Mandatory)][int] $Executed,
        [Parameter(Mandatory)][int] $Passed,
        [Parameter(Mandatory)][int] $NotExecuted,
        [string] $ExpectedOutput,
        [string] $ExpectedSkipReason
    )

    $trxContent = Get-Content -LiteralPath $Path -Raw
    [xml] $trx = $trxContent
    $counters = $trx.TestRun.ResultSummary.Counters
    $actual = @(
        [int] $counters.total,
        [int] $counters.executed,
        [int] $counters.passed,
        [int] $counters.failed,
        [int] $counters.notExecuted
    )
    $expected = @($Total, $Executed, $Passed, 0, $NotExecuted)
    if (Compare-Object $expected $actual -SyncWindow 0) {
        throw "Unexpected TRX counters in '$Path': $($actual -join '/')."
    }
    if ($ExpectedOutput -and $trxContent -notmatch [regex]::Escape($ExpectedOutput)) {
        throw "Expected output marker '$ExpectedOutput' is absent from '$Path'."
    }
    if ($ExpectedSkipReason -and $trxContent -notmatch [regex]::Escape($ExpectedSkipReason)) {
        throw "Expected skip reason '$ExpectedSkipReason' is absent from '$Path'."
    }
}

function Assert-TimeoutFailureResult {
    param(
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)][string] $TestName
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Timeout probe did not create '$Path'."
    }

    [xml] $trx = Get-Content -LiteralPath $Path -Raw
    $counters = $trx.TestRun.ResultSummary.Counters
    $actual = @(
        [int] $counters.total,
        [int] $counters.executed,
        [int] $counters.passed,
        [int] $counters.failed
    )
    if (Compare-Object @(1, 1, 0, 1) $actual -SyncWindow 0) {
        throw "Unexpected timeout-probe counters in '$Path': $($actual -join '/')."
    }

    $results = @(
        $trx.SelectNodes("//*[local-name()='UnitTestResult']") |
            Where-Object { $_.testName -like "*$TestName*" }
    )
    if ($results.Count -ne 1 -or $results[0].outcome -notin @("Failed", "Timeout")) {
        throw "Expected one failed timeout result named '$TestName' in '$Path'."
    }

    # Restrict evidence to the runner's error message; result attributes contain authored identifiers.
    $diagnosticText = @(
        $results[0].SelectNodes(
            "./*[local-name()='Output']/*[local-name()='ErrorInfo']/*[local-name()='Message']"
        ) | ForEach-Object { $_.InnerText }
    ) -join [Environment]::NewLine
    if ($diagnosticText -notmatch "(?i)\b(?:timeout|timed\s+out|cancel(?:led|ed|lation|ling|ing)?)\b") {
        throw "The failed result in '$Path' contains no timeout/cancellation evidence."
    }
}

function Test-TimeoutDiagnosticAssertion {
    $regressionPath = Join-Path $resultsRoot "timeout-diagnostic-regression.trx"
    @'
<TestRun>
  <ResultSummary>
    <Counters total="1" executed="1" passed="0" failed="1" />
  </ResultSummary>
  <Results>
    <UnitTestResult testName="TimeoutNamedButUnrelatedFailure" outcome="Failed">
      <Output>
        <ErrorInfo>
          <Message>Expected values to be equal.</Message>
        </ErrorInfo>
      </Output>
    </UnitTestResult>
  </Results>
</TestRun>
'@ | Set-Content -LiteralPath $regressionPath

    try {
        $unrelatedFailureRejected = $false
        try {
            Assert-TimeoutFailureResult `
                -Path $regressionPath `
                -TestName "TimeoutNamedButUnrelatedFailure"
        }
        catch {
            if ($_.Exception.Message -notlike "*contains no timeout/cancellation evidence.") {
                throw
            }
            $unrelatedFailureRejected = $true
        }

        if (-not $unrelatedFailureRejected) {
            throw "A test name containing 'Timeout' incorrectly satisfied the diagnostic assertion."
        }
    }
    finally {
        Remove-Item -LiteralPath $regressionPath -Force -ErrorAction SilentlyContinue
    }
}

Test-TimeoutDiagnosticAssertion
if ($ValidateHelpersOnly) {
    Write-Host "Spike helper regression validation passed."
    return
}

function Get-DiscoveryCount {
    param(
        [Parameter(Mandatory)][string] $Project,
        [Parameter(Mandatory)][string] $Configuration,
        [Parameter(Mandatory)][string] $OutputPath
    )

    $output = & $Dotnet test --project $Project -c $Configuration --no-build --list-tests 2>&1
    $output | Set-Content -LiteralPath $OutputPath
    $output | Write-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Discovery failed for '$Project' ($Configuration)."
    }

    $match = [regex]::Match(($output -join "`n"), "Discovered (\d+) tests\.")
    if (-not $match.Success) {
        throw "Could not parse discovery count for '$Project' ($Configuration)."
    }

    return [int] $match.Groups[1].Value
}

Push-Location $PSScriptRoot
try {
    Invoke-Dotnet @("restore", $xunitProject)
    Invoke-Dotnet @("restore", $mstestProject)

    foreach ($configuration in @("Debug", "Release")) {
        $configurationResults = Join-Path $resultsRoot $configuration
        New-Item -ItemType Directory -Path $configurationResults -Force | Out-Null
        Invoke-Dotnet @("build", $xunitProject, "-c", $configuration, "--no-restore")
        Invoke-Dotnet @("build", $mstestProject, "-c", $configuration, "--no-restore")

        $xunitDiscovered = Get-DiscoveryCount `
            -Project $xunitProject `
            -Configuration $configuration `
            -OutputPath (Join-Path $configurationResults "xunit-list.txt")
        $mstestDiscovered = Get-DiscoveryCount `
            -Project $mstestProject `
            -Configuration $configuration `
            -OutputPath (Join-Path $configurationResults "mstest-list.txt")
        if ($xunitDiscovered -ne 12 -or $mstestDiscovered -ne 13) {
            throw "Unexpected discovery inventory: xUnit=$xunitDiscovered; MSTest=$mstestDiscovered."
        }

        $xunitLifecycle = Join-Path $configurationResults "xunit-lifecycle.txt"
        $mstestLifecycle = Join-Path $configurationResults "mstest-lifecycle.txt"
        $mstestFixture = Join-Path $configurationResults "mstest-fixture.txt"
        Remove-Item $xunitLifecycle, $mstestLifecycle, $mstestFixture `
            -Force -ErrorAction SilentlyContinue

        $env:ANDREJA_SPIKE_LIFECYCLE_FILE = $xunitLifecycle
        Remove-Item Env:ANDREJA_SPIKE_FIXTURE_FILE -ErrorAction SilentlyContinue
        Invoke-Dotnet @(
            "test", "--project", $xunitProject, "-c", $configuration, "--no-build",
            "--report-xunit-trx",
            "--report-xunit-trx-filename", "XunitV3Spike.$configuration.trx",
            "--results-directory", $configurationResults
        )

        $env:ANDREJA_SPIKE_LIFECYCLE_FILE = $mstestLifecycle
        $env:ANDREJA_SPIKE_FIXTURE_FILE = $mstestFixture
        Invoke-Dotnet @(
            "test", "--project", $mstestProject, "-c", $configuration, "--no-build",
            "--report-trx",
            "--report-trx-filename", "MSTestMtpSpike.$configuration.trx",
            "--results-directory", $configurationResults
        )

        Assert-TrxCounters `
            -Path (Join-Path $configurationResults "XunitV3Spike.$configuration.trx") `
            -Total 13 -Executed 12 -Passed 12 -NotExecuted 1 `
            -ExpectedOutput "xunit-v3-output" `
            -ExpectedSkipReason "intentional evidence skip"
        Assert-TrxCounters `
            -Path (Join-Path $configurationResults "MSTestMtpSpike.$configuration.trx") `
            -Total 13 -Executed 12 -Passed 12 -NotExecuted 1 `
            -ExpectedOutput "mstest-mtp-output" `
            -ExpectedSkipReason "intentional evidence skip"
        $lifecycleComplete =
            (Get-Content $xunitLifecycle) -eq "xunit-v3-cleanup" -and
            (Get-Content $mstestLifecycle) -eq "mstest-mtp-cleanup" -and
            (Get-Content $mstestFixture) -eq "started;disposed"
        if (-not $lifecycleComplete) {
            throw "Lifecycle or shared-fixture cleanup evidence is incomplete."
        }
    }

    $filterResults = Join-Path $resultsRoot "filters"
    New-Item -ItemType Directory -Path $filterResults -Force | Out-Null
    $unrelatedFixture = Join-Path $filterResults "mstest-unrelated-fixture.txt"
    Remove-Item $unrelatedFixture -Force -ErrorAction SilentlyContinue
    Remove-Item Env:ANDREJA_SPIKE_LIFECYCLE_FILE -ErrorAction SilentlyContinue
    Remove-Item Env:ANDREJA_SPIKE_FIXTURE_FILE -ErrorAction SilentlyContinue
    Invoke-Dotnet @(
        "test", "--project", $xunitProject, "-c", "Debug", "--no-build",
        "--filter-trait", "Category=Smoke",
        "--report-xunit-trx", "--report-xunit-trx-filename", "XunitV3Spike.Filter.trx",
        "--results-directory", $filterResults
    )
    $env:ANDREJA_SPIKE_FIXTURE_FILE = $unrelatedFixture
    Invoke-Dotnet @(
        "test", "--project", $mstestProject, "-c", "Debug", "--no-build",
        "--filter", "TestCategory=Smoke",
        "--report-trx", "--report-trx-filename", "MSTestMtpSpike.Filter.trx",
        "--results-directory", $filterResults
    )
    Assert-TrxCounters `
        -Path (Join-Path $filterResults "XunitV3Spike.Filter.trx") `
        -Total 6 -Executed 5 -Passed 5 -NotExecuted 1 `
        -ExpectedOutput "xunit-v3-output" `
        -ExpectedSkipReason "intentional evidence skip"
    Assert-TrxCounters `
        -Path (Join-Path $filterResults "MSTestMtpSpike.Filter.trx") `
        -Total 6 -Executed 5 -Passed 5 -NotExecuted 1 `
        -ExpectedOutput "mstest-mtp-output" `
        -ExpectedSkipReason "intentional evidence skip"
    if (Test-Path $unrelatedFixture) {
        throw "The MSTest shared fixture initialized for an unrelated filter."
    }

    Remove-Item Env:ANDREJA_SPIKE_LIFECYCLE_FILE, Env:ANDREJA_SPIKE_FIXTURE_FILE `
        -ErrorAction SilentlyContinue
    $timeoutResults = Join-Path $resultsRoot "timeout-probes"
    New-Item -ItemType Directory -Path $timeoutResults -Force | Out-Null
    $xunitTimeoutTrx = Join-Path $timeoutResults "XunitV3Spike.Timeout.trx"
    $mstestTimeoutTrx = Join-Path $timeoutResults "MSTestMtpSpike.Timeout.trx"
    Remove-Item $xunitTimeoutTrx, $mstestTimeoutTrx -Force -ErrorAction SilentlyContinue

    Invoke-Dotnet @(
        "build", $xunitProject, "-c", "Debug", "--no-restore",
        "-p:RunTimeoutProbe=true"
    )
    Invoke-DotnetExpectingTestFailure `
        -Arguments @(
            "test", "--project", $xunitProject, "-c", "Debug", "--no-build",
            "--report-xunit-trx",
            "--report-xunit-trx-filename", "XunitV3Spike.Timeout.trx",
            "--results-directory", $timeoutResults
        ) `
        -OutputPath (Join-Path $timeoutResults "xunit-timeout-output.txt")
    Assert-TimeoutFailureResult -Path $xunitTimeoutTrx -TestName "TimeoutEnforcementProbe"

    Invoke-Dotnet @(
        "build", $mstestProject, "-c", "Debug", "--no-restore",
        "-p:RunTimeoutProbe=true"
    )
    Invoke-DotnetExpectingTestFailure `
        -Arguments @(
            "test", "--project", $mstestProject, "-c", "Debug", "--no-build",
            "--report-trx",
            "--report-trx-filename", "MSTestMtpSpike.Timeout.trx",
            "--results-directory", $timeoutResults
        ) `
        -OutputPath (Join-Path $timeoutResults "mstest-timeout-output.txt")
    Assert-TimeoutFailureResult -Path $mstestTimeoutTrx -TestName "TimeoutEnforcementProbe"

    Invoke-Dotnet @("build", $xunitProject, "-c", "Debug", "--no-restore")
    Invoke-Dotnet @("build", $mstestProject, "-c", "Debug", "--no-restore")

    $timings = 1..$WarmRuns | ForEach-Object {
        if (($_ % 2) -eq 1) {
            $xunit = (Measure-Command {
                Invoke-Dotnet @("test", "--project", $xunitProject, "-c", "Debug", "--no-build")
            }).TotalMilliseconds
            $mstest = (Measure-Command {
                Invoke-Dotnet @("test", "--project", $mstestProject, "-c", "Debug", "--no-build")
            }).TotalMilliseconds
        }
        else {
            $mstest = (Measure-Command {
                Invoke-Dotnet @("test", "--project", $mstestProject, "-c", "Debug", "--no-build")
            }).TotalMilliseconds
            $xunit = (Measure-Command {
                Invoke-Dotnet @("test", "--project", $xunitProject, "-c", "Debug", "--no-build")
            }).TotalMilliseconds
        }
        [pscustomobject]@{
            Run = $_
            XunitV3Milliseconds = [math]::Round($xunit)
            MSTestMtpMilliseconds = [math]::Round($mstest)
        }
    }
    $timings | Export-Csv (Join-Path $resultsRoot "warm-runs.csv") -NoTypeInformation
    $xunitMedian = Get-Median -Values $timings.XunitV3Milliseconds
    $mstestMedian = Get-Median -Values $timings.MSTestMtpMilliseconds
    if ($mstestMedian -gt ($xunitMedian * 1.10)) {
        throw "MSTest median exceeded the 10% stop threshold: $mstestMedian vs $xunitMedian ms."
    }

    Invoke-Dotnet @(
        "build", $xunitProject, "-c", "Debug", "--no-restore",
        "-p:RunAnalyzerProbe=true"
    )
    $mstestProbe = & $Dotnet build $mstestProject -c Debug --no-restore `
        -p:RunAnalyzerProbe=true 2>&1
    $mstestProbe | Set-Content (Join-Path $resultsRoot "mstest-analyzer-probe.txt")
    if ($LASTEXITCODE -eq 0 -or ($mstestProbe -join "`n") -notmatch "MSTEST0032") {
        throw "The MSTest always-true assertion probe did not fail with MSTEST0032."
    }

    Invoke-Dotnet @("build", $xunitProject, "-c", "Debug", "--no-restore")
    Invoke-Dotnet @("build", $mstestProject, "-c", "Debug", "--no-restore")
    Write-Host "Test-platform spike passed. Results: $resultsRoot"
    Write-Host "Warm medians: xUnit v3 $xunitMedian ms; MSTest/MTP $mstestMedian ms."
}
finally {
    Remove-Item Env:ANDREJA_SPIKE_LIFECYCLE_FILE, Env:ANDREJA_SPIKE_FIXTURE_FILE `
        -ErrorAction SilentlyContinue
    Pop-Location
}
