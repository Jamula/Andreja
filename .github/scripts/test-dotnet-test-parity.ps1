[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration,

    [Parameter(Mandatory)]
    [string] $ResultsRoot,

    [Parameter(Mandatory)]
    [string] $OutputPath,

    [string] $InventoryPath = 'docs/research/test-suite-current-inventory-112.json',

    [string] $RepositoryRoot = (Get-Location).Path
)

$ErrorActionPreference = 'Stop'

$report = [ordered]@{
    schema_version = 1
    status = 'running'
    configuration = $Configuration
    inventory_path = $InventoryPath
    historical_baseline_path = $null
    sdk_version = $null
    runner = $null
    projects = [ordered]@{}
    error = $null
}

try {
    $inventoryPath = if ([IO.Path]::IsPathRooted($InventoryPath)) {
        $InventoryPath
    } else {
        Join-Path $RepositoryRoot $InventoryPath
    }
    $inventory = Get-Content -LiteralPath $inventoryPath -Raw | ConvertFrom-Json
    if ($inventory.schema_version -ne 1 -or $inventory.inventory_kind -ne 'current') {
        throw "Expected a schema-version 1 current test inventory at '$InventoryPath'."
    }
    if ([string]::IsNullOrWhiteSpace($inventory.historical_baseline_path)) {
        throw "Current inventory '$InventoryPath' must reference the immutable historical baseline."
    }
    $historicalBaselinePath = if ([IO.Path]::IsPathRooted($inventory.historical_baseline_path)) {
        $inventory.historical_baseline_path
    } else {
        Join-Path $RepositoryRoot $inventory.historical_baseline_path
    }
    if (-not (Test-Path -LiteralPath $historicalBaselinePath -PathType Leaf)) {
        throw "Historical baseline '$($inventory.historical_baseline_path)' is missing."
    }
    $global = Get-Content -LiteralPath (Join-Path $RepositoryRoot 'global.json') -Raw |
        ConvertFrom-Json
    $report.sdk_version = $global.sdk.version
    $report.runner = $global.test.runner
    $report.inventory_path = $InventoryPath
    $report.historical_baseline_path = $inventory.historical_baseline_path
    if ($report.sdk_version -ne $inventory.sdk_version) {
        throw "SDK version '$($report.sdk_version)' differs from current inventory '$($inventory.sdk_version)'."
    }
    if ($report.runner -ne 'Microsoft.Testing.Platform') {
        throw "Expected Microsoft.Testing.Platform in global.json; found '$($report.runner)'."
    }
    if ($inventory.runner -ne $report.runner) {
        throw "Runner '$($report.runner)' differs from current inventory '$($inventory.runner)'."
    }

    $expectedProjects = @(
        $inventory.projects.PSObject.Properties |
            ForEach-Object { $_.Value.path.Replace('/', [IO.Path]::DirectorySeparatorChar) } |
            Sort-Object
    )
    $actualProjects = @(
        Get-ChildItem -LiteralPath (Join-Path $RepositoryRoot 'tests') -Filter '*.csproj' -Recurse |
            ForEach-Object {
                [IO.Path]::GetRelativePath($RepositoryRoot, $_.FullName)
            } |
            Sort-Object
    )
    $projectDifference = @(Compare-Object $expectedProjects $actualProjects)
    if ($projectDifference.Count -gt 0) {
        $difference = $projectDifference |
            ForEach-Object { "$($_.SideIndicator) $($_.InputObject)" }
        throw "Test-project inventory differs from the current inventory:`n$($difference -join "`n")"
    }

    $resolvedResultsRoot = if ([IO.Path]::IsPathRooted($ResultsRoot)) {
        $ResultsRoot
    } else {
        Join-Path $RepositoryRoot $ResultsRoot
    }
    foreach ($property in $inventory.projects.PSObject.Properties) {
        $assembly = $property.Name
        $project = $property.Value
        $projectPath = Join-Path $RepositoryRoot (
            $project.path.Replace('/', [IO.Path]::DirectorySeparatorChar))
        if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
            throw "Current inventory project '$assembly' is missing at '$($project.path)'."
        }

        $discoveryOutput = & dotnet test `
            --project $projectPath `
            --configuration $Configuration `
            --no-build `
            --list-tests 2>&1
        $discoveryExitCode = $LASTEXITCODE
        $discoveryText = ($discoveryOutput | ForEach-Object { [string]$_ }) -join "`n"
        if ($discoveryExitCode -ne 0) {
            throw "MTP test discovery failed for '$assembly' ($Configuration), exit $discoveryExitCode."
        }

        $discoveryMatch = [regex]::Match($discoveryText, 'Discovered (\d+) tests\.')
        if (-not $discoveryMatch.Success) {
            throw "MTP discovery output for '$assembly' ($Configuration) contained no 'Discovered N tests.' count."
        }
        $discovered = [int]$discoveryMatch.Groups[1].Value
        $expected = $project.$Configuration
        $expectedDiscovery = [int]$expected.discovered
        if ($discovered -ne $expectedDiscovery) {
            throw "Discovery mismatch for '$assembly' ($Configuration): expected $expectedDiscovery rows from current inventory, found $discovered."
        }

        $projectReport = [ordered]@{
            path = $project.path
            discovered = $discovered
            expected_discovered = $expectedDiscovery
            runtime_status = 'not-run'
        }
        if ($assembly -eq 'Andreja.PostgreSqlIntegrationTests') {
            $projectReport.runtime_status = 'unavailable-on-hosted-runner'
            $projectReport.runtime_reason = 'The hosted gate does not provision or receive a disposable PostgreSQL database.'
        }
        else {
            $trxPath = Join-Path (Join-Path $resolvedResultsRoot $assembly) (
                "$assembly-$Configuration.trx")
            if (-not (Test-Path -LiteralPath $trxPath -PathType Leaf)) {
                throw "Missing TRX evidence for '$assembly' ($Configuration): '$trxPath'."
            }

            [xml]$trx = Get-Content -LiteralPath $trxPath -Raw
            $counters = $trx.TestRun.ResultSummary.Counters
            if ($null -eq $counters) {
                throw "TRX file for '$assembly' ($Configuration) has no result counters."
            }
            foreach ($name in @('total', 'executed', 'passed', 'failed', 'notExecuted')) {
                if (-not $counters.HasAttribute($name)) {
                    throw "TRX file for '$assembly' ($Configuration) is missing the '$name' counter."
                }
            }

            $actualCounters = @(
                [int]$counters.total,
                [int]$counters.executed,
                [int]$counters.passed,
                [int]$counters.failed,
                [int]$counters.notExecuted
            )
            $expectedCounters = @(
                [int]$expected.trx_total,
                [int]$expected.executed,
                [int]$expected.passed,
                [int]$expected.failed,
                [int]$expected.skipped
            )
            if (Compare-Object $expectedCounters $actualCounters -SyncWindow 0) {
                throw "TRX parity mismatch for '$assembly' ($Configuration): expected total/executed/passed/failed/skipped $($expectedCounters -join '/'), found $($actualCounters -join '/')."
            }

            $projectReport.runtime_status = 'passed'
            $projectReport.trx = [ordered]@{
                path = [IO.Path]::GetRelativePath($RepositoryRoot, $trxPath)
                total = $actualCounters[0]
                executed = $actualCounters[1]
                passed = $actualCounters[2]
                failed = $actualCounters[3]
                skipped = $actualCounters[4]
            }
        }
        $report.projects[$assembly] = $projectReport
    }

    $report.status = 'passed'
}
catch {
    $report.status = 'failed'
    $report.error = $_.Exception.Message
    throw
}
finally {
    $report.generated_utc = [DateTimeOffset]::UtcNow.ToString('O')
    $resolvedOutputPath = if ([IO.Path]::IsPathRooted($OutputPath)) {
        $OutputPath
    } else {
        Join-Path $RepositoryRoot $OutputPath
    }
    $outputDirectory = Split-Path -Parent $resolvedOutputPath
    if ($outputDirectory) {
        New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
    }
    $report | ConvertTo-Json -Depth 12 |
        Set-Content -LiteralPath $resolvedOutputPath -Encoding utf8
}

Write-Host "MTP inventory and TRX parity passed for $Configuration."
