[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration,

    [string] $RepositoryRoot = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
)

$ErrorActionPreference = 'Stop'
$reportScript = Join-Path $PSScriptRoot 'write-dotnet-validation-report.ps1'
$parityScript = Join-Path $PSScriptRoot 'test-dotnet-test-parity.ps1'
$workspace = Join-Path (Join-Path $RepositoryRoot "artifacts/$Configuration") `
    "dotnet-validation-report-tests-$([guid]::NewGuid().ToString('N'))"
$fixtureRoot = Join-Path $workspace 'repository'
$summaryPath = Join-Path $workspace 'summary.md'
$resultsRoot = Join-Path $workspace 'test-results'
$parityReportPath = Join-Path $workspace 'missing-site-parity.json'
$previousSummary = $env:GITHUB_STEP_SUMMARY

function Assert-True {
    param([bool] $Condition, [string] $Message)
    if (-not $Condition) {
        throw $Message
    }
}

function Invoke-ExpectedFailure {
    param([scriptblock] $Action, [string] $ExpectedMessage)
    try {
        & $Action
    }
    catch {
        Assert-True ($_.Exception.Message -like "*$ExpectedMessage*") `
            "Expected failure containing '$ExpectedMessage'; got '$($_.Exception.Message)'."
        return
    }
    throw "Expected failure containing '$ExpectedMessage', but the operation succeeded."
}

try {
    Remove-Item -LiteralPath $workspace -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Path (Join-Path $fixtureRoot 'tests') -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $fixtureRoot 'global.json') `
        -Value '{"sdk":{"version":"10.0.301"}}' -Encoding utf8

    $expectedProjects = @(
        'Andreja.ArchitectureTests'
        'Andreja.PostgreSqlIntegrationTests'
        'Andreja.PublicSiteTests'
        'Andreja.UnitTests'
    )
    foreach ($project in $expectedProjects) {
        $projectDirectory = Join-Path (Join-Path $fixtureRoot 'tests') $project
        New-Item -ItemType Directory -Path $projectDirectory -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $projectDirectory "$project.csproj") `
            -Value '<Project />' -Encoding utf8
    }

    $validResults = [ordered]@{
        restore_solution = 'success'
        restore_postgresql = 'success'
        build_solution = 'success'
        build_postgresql = 'success'
        test_unit = 'success'
        test_architecture = 'success'
        test_public_site = 'success'
        evidence_contract = 'success'
        parity = 'success'
    }
    $env:GITHUB_STEP_SUMMARY = $summaryPath
    $validOutputPath = Join-Path $workspace 'valid-report.json'
    & $reportScript -Kind build-test -Configuration $Configuration `
        -ResultsJson ($validResults | ConvertTo-Json -Compress) `
        -OutputPath $validOutputPath -RepositoryRoot $fixtureRoot
    $validReport = Get-Content -LiteralPath $validOutputPath -Raw | ConvertFrom-Json
    $siteProject = @($validReport.test_projects.included |
        Where-Object path -eq 'tests/Andreja.PublicSiteTests/Andreja.PublicSiteTests.csproj')
    Assert-True ($siteProject.Count -eq 1) 'The public-site project was not classified as included.'
    Assert-True ($siteProject[0].runtime_result -eq 'success') `
        'The public-site runtime outcome was not preserved in the report.'
    Assert-True ((@($validReport.validation.commands) -match 'Andreja.PublicSiteTests').Count -gt 0) `
        'The report command list omitted the public-site test invocation.'
    $summary = Get-Content -LiteralPath $summaryPath -Raw
    Assert-True ($summary.Contains('Andreja.PublicSiteTests.csproj')) `
        'The step summary omitted the public-site project.'

    $missingResults = [ordered]@{}
    foreach ($name in $validResults.Keys) {
        if ($name -ne 'test_public_site') {
            $missingResults[$name] = $validResults[$name]
        }
    }
    Invoke-ExpectedFailure {
        & $reportScript -Kind build-test -Configuration $Configuration `
            -ResultsJson ($missingResults | ConvertTo-Json -Compress) `
            -OutputPath (Join-Path $workspace 'missing-result.json') `
            -RepositoryRoot $fixtureRoot
    } "outcome for 'test_public_site'"

    $invalidResults = [ordered]@{}
    foreach ($name in $validResults.Keys) {
        $invalidResults[$name] = if ($name -eq 'test_public_site') { 'passed' } else { $validResults[$name] }
    }
    Invoke-ExpectedFailure {
        & $reportScript -Kind build-test -Configuration $Configuration `
            -ResultsJson ($invalidResults | ConvertTo-Json -Compress) `
            -OutputPath (Join-Path $workspace 'invalid-result.json') `
            -RepositoryRoot $fixtureRoot
    } "outcome for 'test_public_site'"

    $failedResults = [ordered]@{}
    foreach ($name in $validResults.Keys) {
        $failedResults[$name] = if ($name -eq 'test_public_site') { 'failure' } else { $validResults[$name] }
    }
    $failedOutputPath = Join-Path $workspace 'failed-test-report.json'
    & $reportScript -Kind build-test -Configuration $Configuration `
        -ResultsJson ($failedResults | ConvertTo-Json -Compress) `
        -OutputPath $failedOutputPath -RepositoryRoot $fixtureRoot
    $failedReport = Get-Content -LiteralPath $failedOutputPath -Raw | ConvertFrom-Json
    $failedSiteProject = @($failedReport.test_projects.included |
        Where-Object path -eq 'tests/Andreja.PublicSiteTests/Andreja.PublicSiteTests.csproj')
    Assert-True ($failedSiteProject[0].runtime_result -eq 'failure') `
        'The report did not preserve a failed public-site outcome.'

    $unclassifiedDirectory = Join-Path (Join-Path $fixtureRoot 'tests') 'Andreja.UnclassifiedTests'
    New-Item -ItemType Directory -Path $unclassifiedDirectory -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $unclassifiedDirectory 'Andreja.UnclassifiedTests.csproj') `
        -Value '<Project />' -Encoding utf8
    Invoke-ExpectedFailure {
        & $reportScript -Kind build-test -Configuration $Configuration `
            -ResultsJson ($validResults | ConvertTo-Json -Compress) `
            -OutputPath (Join-Path $workspace 'unclassified-report.json') `
            -RepositoryRoot $fixtureRoot
    } 'Andreja.UnclassifiedTests.csproj'

    $actualResultsRoot = Join-Path $RepositoryRoot "artifacts/$Configuration/test-results"
    $siteTrxName = "Andreja.PublicSiteTests-$Configuration.trx"
    $actualSiteTrx = Join-Path (Join-Path $actualResultsRoot 'Andreja.PublicSiteTests') $siteTrxName
    Assert-True (Test-Path -LiteralPath $actualSiteTrx -PathType Leaf) `
        "Cannot exercise the missing-TRX case: '$actualSiteTrx' was not produced."
    New-Item -ItemType Directory -Path $resultsRoot -Force | Out-Null
    Copy-Item -Path (Join-Path $actualResultsRoot '*') -Destination $resultsRoot -Recurse -Force
    Remove-Item -LiteralPath (Join-Path (Join-Path $resultsRoot 'Andreja.PublicSiteTests') $siteTrxName) `
        -Force
    Invoke-ExpectedFailure {
        & $parityScript -Configuration $Configuration -ResultsRoot $resultsRoot `
            -InventoryPath 'docs/research/test-suite-current-inventory-112.json' `
            -OutputPath $parityReportPath -RepositoryRoot $RepositoryRoot
    } $siteTrxName
    $missingTrxReport = Get-Content -LiteralPath $parityReportPath -Raw | ConvertFrom-Json
    Assert-True ($missingTrxReport.status -eq 'failed') `
        'The parity report did not mark missing public-site TRX evidence as failed.'
    Assert-True ($missingTrxReport.error -like "*$siteTrxName*") `
        'The parity report did not identify the missing public-site TRX artifact.'

    Write-Host 'Validation-report contract checks passed: inclusion, missing/invalid/failed outcomes, unclassified projects, and missing TRX.'
}
finally {
    if ($null -eq $previousSummary) {
        Remove-Item Env:GITHUB_STEP_SUMMARY -ErrorAction SilentlyContinue
    }
    else {
        $env:GITHUB_STEP_SUMMARY = $previousSummary
    }
    Remove-Item -LiteralPath $workspace -Recurse -Force -ErrorAction SilentlyContinue
}
