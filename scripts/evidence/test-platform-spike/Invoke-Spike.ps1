[CmdletBinding()]
param(
    [string] $Dotnet = "dotnet",
    [ValidateRange(3, 21)]
    [int] $WarmRuns = 5
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

function Assert-TrxCounters {
    param(
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)][int] $Total,
        [Parameter(Mandatory)][int] $Executed,
        [Parameter(Mandatory)][int] $Passed,
        [Parameter(Mandatory)][int] $NotExecuted
    )

    [xml] $trx = Get-Content -LiteralPath $Path
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
            -Total 13 -Executed 12 -Passed 12 -NotExecuted 1
        Assert-TrxCounters `
            -Path (Join-Path $configurationResults "MSTestMtpSpike.$configuration.trx") `
            -Total 13 -Executed 12 -Passed 12 -NotExecuted 1
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
        -Total 6 -Executed 5 -Passed 5 -NotExecuted 1
    Assert-TrxCounters `
        -Path (Join-Path $filterResults "MSTestMtpSpike.Filter.trx") `
        -Total 6 -Executed 5 -Passed 5 -NotExecuted 1
    if (Test-Path $unrelatedFixture) {
        throw "The MSTest shared fixture initialized for an unrelated filter."
    }

    Remove-Item Env:ANDREJA_SPIKE_LIFECYCLE_FILE, Env:ANDREJA_SPIKE_FIXTURE_FILE `
        -ErrorAction SilentlyContinue
    $timings = 1..$WarmRuns | ForEach-Object {
        $xunit = (Measure-Command {
            Invoke-Dotnet @("test", "--project", $xunitProject, "-c", "Debug", "--no-build")
        }).TotalMilliseconds
        $mstest = (Measure-Command {
            Invoke-Dotnet @("test", "--project", $mstestProject, "-c", "Debug", "--no-build")
        }).TotalMilliseconds
        [pscustomobject]@{
            Run = $_
            XunitV3Milliseconds = [math]::Round($xunit)
            MSTestMtpMilliseconds = [math]::Round($mstest)
        }
    }
    $timings | Export-Csv (Join-Path $resultsRoot "warm-runs.csv") -NoTypeInformation
    $xunitMedian = ($timings.XunitV3Milliseconds | Sort-Object)[[math]::Floor($WarmRuns / 2)]
    $mstestMedian = ($timings.MSTestMtpMilliseconds | Sort-Object)[[math]::Floor($WarmRuns / 2)]
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
