[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('build-test', 'format', 'vulnerability', 'sast')]
    [string] $Kind,

    [Parameter(Mandatory)]
    [string] $OutputPath,

    [Parameter(Mandatory)]
    [string] $ResultsJson,

    [string] $Configuration = 'N/A',

    [string] $RepositoryRoot = (Get-Location).Path
)

$ErrorActionPreference = 'Stop'

$expectedTestProjects = @(
    'tests/Andreja.ArchitectureTests/Andreja.ArchitectureTests.csproj'
    'tests/Andreja.PostgreSqlIntegrationTests/Andreja.PostgreSqlIntegrationTests.csproj'
    'tests/Andreja.UnitTests/Andreja.UnitTests.csproj'
)

$discoveredTestProjects = @(
    Get-ChildItem -Path (Join-Path $RepositoryRoot 'tests') -Filter '*.csproj' -Recurse |
        ForEach-Object {
            [IO.Path]::GetRelativePath($RepositoryRoot, $_.FullName).Replace('\', '/')
        } |
        Sort-Object
)

$inventoryDifference = @(
    Compare-Object -ReferenceObject $expectedTestProjects -DifferenceObject $discoveredTestProjects
)

if ($inventoryDifference.Count -gt 0) {
    $difference = $inventoryDifference | ForEach-Object { "$($_.SideIndicator) $($_.InputObject)" }
    throw "Test-project inventory changed. Classify every project before publishing evidence:`n$($difference -join "`n")"
}

$results = $ResultsJson | ConvertFrom-Json -AsHashtable
$sdkVersion = (Get-Content (Join-Path $RepositoryRoot 'global.json') -Raw | ConvertFrom-Json).sdk.version
$postgresProject = 'tests/Andreja.PostgreSqlIntegrationTests/Andreja.PostgreSqlIntegrationTests.csproj'

$commands = switch ($Kind) {
    'build-test' {
        @(
            "dotnet restore Andreja.slnx"
            "dotnet restore $postgresProject"
            "dotnet build Andreja.slnx --configuration $Configuration --no-restore"
            "dotnet build $postgresProject --configuration $Configuration --no-restore"
            "dotnet test Andreja.slnx --configuration $Configuration --no-build"
        )
    }
    'format' {
        @(
            'dotnet restore Andreja.slnx'
            "dotnet restore $postgresProject"
            'dotnet format Andreja.slnx --verify-no-changes --no-restore'
            "dotnet format $postgresProject --verify-no-changes --no-restore"
        )
    }
    'vulnerability' {
        @(
            'dotnet restore Andreja.slnx'
            "dotnet restore $postgresProject"
            'dotnet list Andreja.slnx package --vulnerable --include-transitive --format json --output-version 1'
            "dotnet list $postgresProject package --vulnerable --include-transitive --format json --output-version 1"
        )
    }
    'sast' {
        @(
            'dotnet tool install Microsoft.CST.DevSkim.CLI --version 1.0.90'
            'devskim analyze --source-code . --include-globs **/*.cs --ignore-rule-ids DS137138,DS162092 --output-file devskim-results.sarif'
        )
    }
}

$report = [ordered]@{
    schema_version = 1
    generated_utc = [DateTimeOffset]::UtcNow.ToString('O')
    validation = [ordered]@{
        kind = $Kind
        configuration = $Configuration
        sdk_version = $sdkVersion
        runner_image = 'ubuntu-24.04'
        workflow = $env:GITHUB_WORKFLOW
        run_id = $env:GITHUB_RUN_ID
        run_attempt = $env:GITHUB_RUN_ATTEMPT
        commit_sha = $env:GITHUB_SHA
        commands = $commands
        results = $results
    }
    test_projects = [ordered]@{
        included = @(
            [ordered]@{
                path = 'tests/Andreja.ArchitectureTests/Andreja.ArchitectureTests.csproj'
                classification = 'service-free runtime test'
                runtime_result = if ($Kind -eq 'build-test') { $results.test_solution } else { 'not-run-in-this-job' }
            }
            [ordered]@{
                path = 'tests/Andreja.UnitTests/Andreja.UnitTests.csproj'
                classification = 'service-free runtime test'
                runtime_result = if ($Kind -eq 'build-test') { $results.test_solution } else { 'not-run-in-this-job' }
            }
        )
        excluded = @(
            [ordered]@{
                path = $postgresProject
                excluded_from = 'Andreja.slnx runtime tests'
                reason = 'Requires a disposable PostgreSQL database and is intentionally outside the service-free solution.'
                compile_result = if ($Kind -eq 'build-test') { $results.build_postgresql } else { 'not-run-in-this-job' }
            }
        )
        unavailable = @(
            [ordered]@{
                path = $postgresProject
                runtime_result = 'unavailable'
                reason = 'Hosted validation does not receive a database credential or provision PostgreSQL; invoking the project without one fails BLOCKED rather than skipping.'
                runtime_command = "dotnet test $postgresProject --configuration $Configuration"
            }
        )
    }
    boundaries = [ordered]@{
        untrusted_pull_requests = 'No secrets or write-capable token; checkout credentials are not persisted.'
        codeql_entitlement = 'Unavailable: private-repository Code Security is disabled and the CodeQL configuration endpoint returns HTTP 403.'
        devskim_baseline = 'DS137138 excludes intentional loopback HTTP test/development URLs; DS162092 excludes false positives on explicit Debug/Development safeguards.'
        ruleset_enforcement = 'Tracked separately in GitHub issue #67.'
        sbom_and_provenance = 'Tracked separately in GitHub issue #71.'
    }
}

$outputDirectory = Split-Path -Parent $OutputPath
if ($outputDirectory) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}
$report | ConvertTo-Json -Depth 12 | Set-Content -Path $OutputPath -Encoding utf8

if ($env:GITHUB_STEP_SUMMARY) {
    $operationRows = @(
        foreach ($entry in $results.GetEnumerator()) {
            "| $($entry.Key) | $($entry.Value) |"
        }
    )

    @"
## $Kind validation evidence ($Configuration)

| Operation | Result |
| --- | --- |
$($operationRows -join "`n")

### Test-project inventory

| Classification | Project | Runtime evidence |
| --- | --- | --- |
| Included | tests/Andreja.ArchitectureTests/Andreja.ArchitectureTests.csproj | $(if ($Kind -eq 'build-test') { $results.test_solution } else { 'not run in this job' }) |
| Included | tests/Andreja.UnitTests/Andreja.UnitTests.csproj | $(if ($Kind -eq 'build-test') { $results.test_solution } else { 'not run in this job' }) |
| Excluded from solution runtime | $postgresProject | compiled separately in Debug and Release |
| Unavailable runtime | $postgresProject | no disposable hosted PostgreSQL; never reported as passed or skipped |
"@ | Add-Content -Path $env:GITHUB_STEP_SUMMARY -Encoding utf8
}
