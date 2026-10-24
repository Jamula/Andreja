param([string] $Caller, [string] $Config, [string] $Log, [string] $PowerShellLocation)
$ErrorActionPreference = "Stop"
$global:evidenceCounterReads = 0
if ($PowerShellLocation) {
    Set-Location -LiteralPath $PowerShellLocation
}
function docker {
    $Arguments = @($args)
    @{ command = "docker"; args = $Arguments } | ConvertTo-Json -Compress |
        Add-Content -LiteralPath $Log
    $global:LASTEXITCODE = 0
    if ($env:EVIDENCE_TEST_DOCKER_FAIL -and $Arguments -contains $env:EVIDENCE_TEST_DOCKER_FAIL) {
        $global:LASTEXITCODE = 73
        return "synthetic command failure"
    }
    if ($Arguments -contains "network") { return "true" }
    if ($Arguments -contains "inspect" -and $Arguments -notcontains "image") { return "healthy" }
    if ($Arguments -contains "ps") { return "synthetic-app-container" }
}
function node {
    $Arguments = @($args)
    if ($Arguments[0] -like "*browser-e2e.mjs") {
        @{ command = "browser"; args = $Arguments } | ConvertTo-Json -Compress |
            Add-Content -LiteralPath $Log
        $global:LASTEXITCODE = 0
        return '{"status":"passed"}'
    }
    & (Get-Command node -CommandType Application).Source @Arguments
}
function Invoke-WebRequest {
    param($Uri, [switch] $UseBasicParsing)
    @{ command = "http"; args = @([string]$Uri) } | ConvertTo-Json -Compress |
        Add-Content -LiteralPath $Log
    $global:evidenceCounterReads++
    $value = if ($global:evidenceCounterReads -le 3) { 1 } else { 100 }
    return @{ Content = @(
        "otelcol_receiver_accepted_spans_total $value",
        "otelcol_receiver_accepted_metric_points_total $value",
        "otelcol_receiver_accepted_log_records_total $value"
    ) -join "`n" }
}
function Invoke-RestMethod {
    param($Uri)
    @{ command = "http"; args = @([string]$Uri) } | ConvertTo-Json -Compress |
        Add-Content -LiteralPath $Log
    $value = if ($Uri -like "*violation*") { "0" } else { "1" }
    return @{ status = "success"; data = @{ result = @(@{ value = @(0, $value) }) } }
}
function Start-Sleep {}
try {
    if ($Caller -eq "offline") {
        & "$PSScriptRoot\..\Test-OfflineEvidence.ps1" -FixtureConfig $Config `
            -DestroySyntheticVolumes:($env:EVIDENCE_TEST_DESTROY_VOLUMES -eq "true")
    } else {
        & "$PSScriptRoot\..\Test-TelemetryEvidence.ps1" -FixtureConfig $Config
    }
} catch {
    Write-Error $_
    exit 1
}
