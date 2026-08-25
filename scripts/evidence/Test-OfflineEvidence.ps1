[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern("^.+@sha256:[0-9a-f]{64}$")]
    [string] $AuditedAppImage,

    [string] $ProjectName = "andreja",

    [switch] $DestroySyntheticVolumes
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../.."))
$composeFiles = @(
    (Join-Path $root "compose.yaml"),
    (Join-Path $root "deploy/compose.evidence.yaml"),
    (Join-Path $root "deploy/compose.offline-evidence.yaml")
)
$compose = @("compose", "--project-name", $ProjectName, "--profile", "evidence")
foreach ($file in $composeFiles) {
    $compose += @("--file", $file)
}

function Invoke-Docker {
    param(
        [Parameter(Mandatory)][string[]] $Arguments,
        [Parameter(Mandatory)][string] $FailureMessage,
        [switch] $Capture
    )

    $output = & docker @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "$FailureMessage (exit $LASTEXITCODE): $($output -join "`n")"
    }
    if ($Capture) {
        return @($output)
    }
}

function Wait-AppHealthy {
    $deadline = [DateTimeOffset]::UtcNow.AddMinutes(2)
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        $container = & docker @compose ps --quiet app
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($container)) {
            $health = & docker inspect $container --format "{{.State.Health.Status}}"
            if ($LASTEXITCODE -eq 0 -and $health -eq "healthy") {
                return $container
            }
        }
        Start-Sleep -Seconds 2
    }

    throw "Application did not become healthy within two minutes."
}

$result = $null
Push-Location $root
try {
    $images = Invoke-Docker -Arguments ($compose + @("config", "--images")) `
        -FailureMessage "Unable to resolve Compose images" -Capture
    foreach ($image in $images) {
        if ($image -notmatch "@sha256:[0-9a-f]{64}$") {
            throw "Offline evidence refuses non-immutable image reference '$image'."
        }
        Invoke-Docker -Arguments @("image", "inspect", $image) `
            -FailureMessage "Required preloaded image is unavailable: $image"
    }
    if ($AuditedAppImage -notin $images) {
        throw "Compose does not resolve to the expected audited application image."
    }

    Invoke-Docker -Arguments ($compose + @("down", "--remove-orphans")) `
        -FailureMessage "Unable to stop the prior evidence stack"
    Invoke-Docker -Arguments ($compose + @("up", "--detach", "--pull", "never")) `
        -FailureMessage "Offline Compose startup failed"
    $appContainer = Wait-AppHealthy

    foreach ($networkName in @("${ProjectName}_edge", "${ProjectName}_backend")) {
        $internal = (& docker network inspect $networkName --format "{{.Internal}}").Trim()
        if ($LASTEXITCODE -ne 0 -or $internal -ne "true") {
            throw "Network $networkName is not internal."
        }
    }

    Invoke-Docker -Arguments ($compose + @("restart", "app")) `
        -FailureMessage "Offline application restart failed"
    $appContainer = Wait-AppHealthy

    $caddyImages = @($images | Where-Object { $_ -like "caddy@sha256:*" })
    if ($caddyImages.Count -ne 1) {
        throw "Offline evidence requires exactly one pinned Caddy helper image."
    }
    $caddyImage = $caddyImages[0]
    $probeCommand =
        "wget -qO- http://127.0.0.1:8080/health/live >/dev/null && " +
        "wget -qO- http://127.0.0.1:8080/health/ready >/dev/null && " +
        "if wget -T 3 -qO- https://192.0.2.1 >/dev/null 2>&1; " +
        "then exit 41; fi"
    Invoke-Docker -Arguments @(
        "run", "--rm", "--pull", "never",
        "--network", "container:$appContainer",
        "--entrypoint", "/bin/sh",
        $caddyImage,
        "-c",
        $probeCommand
    ) -FailureMessage "Local readiness or TEST-NET egress negative failed"

    $result = [ordered]@{
        status = "passed"
        imagesPreloaded = @($images).Count
        auditedAppImage = $AuditedAppImage
        pullPolicy = "never"
        edgeInternal = $true
        backendInternal = $true
        startupHealthy = $true
        restartHealthy = $true
        testNetEgressBlocked = $true
    } | ConvertTo-Json -Compress
}
finally {
    try {
        $cleanup = $compose + @("down", "--remove-orphans")
        if ($DestroySyntheticVolumes) {
            $cleanup += "--volumes"
        }
        & docker @cleanup *> $null
    }
    finally {
        Pop-Location
    }
}

Write-Output $result
