[CmdletBinding()]
param(
    [string] $CollectorMetricsUrl = "http://127.0.0.1:18888/metrics",

    [string] $PrometheusUrl = "http://127.0.0.1:19090",

    [ValidateRange(2, 10000)]
    [int] $MinimumRequestSpanDelta = 10
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../.."))

function Get-CollectorCounter {
    param(
        [Parameter(Mandatory)][string] $Name,
        [Parameter(Mandatory)][string] $MetricsUrl
    )

    $metrics = (Invoke-WebRequest -UseBasicParsing -Uri $MetricsUrl).Content
    $total = 0.0
    foreach ($line in $metrics -split "`n") {
        if ($line -match "^$([Regex]::Escape($Name))(\{[^}]*\})?\s+([0-9.eE+-]+)\s*$") {
            $total += [double]::Parse(
                $Matches[3],
                [Globalization.CultureInfo]::InvariantCulture)
        }
    }

    return $total
}

function Get-PrometheusValue {
    param(
        [Parameter(Mandatory)][string] $Query,
        [Parameter(Mandatory)][string] $BaseUrl
    )

    $encoded = [Uri]::EscapeDataString($Query)
    $response = Invoke-RestMethod -Uri "$BaseUrl/api/v1/query?query=$encoded"
    if ($response.status -ne "success" -or @($response.data.result).Count -ne 1) {
        throw "Prometheus query did not return exactly one value: $Query"
    }

    return [double]::Parse(
        [string]$response.data.result[0].value[1],
        [Globalization.CultureInfo]::InvariantCulture)
}

$counterNames = [ordered]@{
    spans = "otelcol_receiver_accepted_spans_total"
    metricPoints = "otelcol_receiver_accepted_metric_points_total"
    logRecords = "otelcol_receiver_accepted_log_records_total"
}
$before = [ordered]@{}
foreach ($entry in $counterNames.GetEnumerator()) {
    $before[$entry.Key] = Get-CollectorCounter `
        -Name $entry.Value `
        -MetricsUrl $CollectorMetricsUrl
}

Push-Location $root
try {
    $browserOutput = & node scripts/evidence/browser-e2e.mjs
    if ($LASTEXITCODE -ne 0) {
        throw "Browser evidence failed with exit code $LASTEXITCODE."
    }
    $browser = $browserOutput | ConvertFrom-Json
    if ($browser.status -ne "passed") {
        throw "Browser evidence did not report a pass."
    }
}
finally {
    Pop-Location
}

Start-Sleep -Seconds 20
$after = [ordered]@{}
$delta = [ordered]@{}
foreach ($entry in $counterNames.GetEnumerator()) {
    $after[$entry.Key] = Get-CollectorCounter `
        -Name $entry.Value `
        -MetricsUrl $CollectorMetricsUrl
    $delta[$entry.Key] = $after[$entry.Key] - $before[$entry.Key]
}

if ($delta.spans -lt $MinimumRequestSpanDelta) {
    throw "Accepted trace delta $($delta.spans) is below the required non-health request threshold $MinimumRequestSpanDelta."
}
if ($delta.metricPoints -le 0 -or $delta.logRecords -le 0) {
    throw "The browser/restart flow did not increase all three accepted-signal counters."
}

$policyChecks = Get-PrometheusValue `
    -Query "sum(andreja_telemetry_policy_checks_total)" `
    -BaseUrl $PrometheusUrl
$suppressed = Get-PrometheusValue `
    -Query "sum(andreja_telemetry_suppressed_attributes_total)" `
    -BaseUrl $PrometheusUrl
$violations = Get-PrometheusValue `
    -Query "sum(andreja_telemetry_policy_violation_total) or vector(0)" `
    -BaseUrl $PrometheusUrl
if ($policyChecks -le 0 -or $suppressed -le 0 -or $violations -ne 0) {
    throw "Telemetry policy evidence requires nonzero checks/suppression and zero prohibited attributes."
}

[ordered]@{
    status = "passed"
    collectedAfterBrowserAndRestart = $true
    acceptedBefore = $before
    acceptedAfter = $after
    acceptedDelta = $delta
    minimumRequestSpanDelta = $MinimumRequestSpanDelta
    policyChecks = $policyChecks
    suppressedAttributes = $suppressed
    prohibitedAttributeViolations = $violations
} | ConvertTo-Json -Depth 5 -Compress
