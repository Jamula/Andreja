[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../.."))
$compose = Get-Content -LiteralPath (Join-Path $root "compose.yaml") -Raw
$dockerfile = Get-Content -LiteralPath (Join-Path $root "Dockerfile") -Raw
$collector = Get-Content -LiteralPath (Join-Path $root "deploy/otel-collector.yaml") -Raw

$digest = "sha256:" + ("a" * 64)
$previousImage = $env:ANDREJA_IMAGE
$previousMigrationApproval = $env:MIGRATION_APPROVAL_FILE
$previousMigrationBackup = $env:MIGRATION_BACKUP_FILE
$previousMigrationScript = $env:MIGRATION_SCRIPT_FILE
try {
    $env:ANDREJA_IMAGE = "andreja@$digest"
    & docker compose --file (Join-Path $root "compose.yaml") config --quiet
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose config failed."
    }

    $images = & docker compose --file (Join-Path $root "compose.yaml") --profile evidence config --images
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose image expansion failed."
    }
    foreach ($image in $images) {
        if ($image -notmatch '(?:^|@)sha256:[0-9a-f]{64}$') {
            throw "Compose image is not digest-pinned: $image"
        }
    }

    & docker compose `
        --file (Join-Path $root "compose.yaml") `
        --file (Join-Path $root "deploy/compose.maintenance.yaml") `
        config --quiet
    if ($LASTEXITCODE -ne 0) {
        throw "The maintenance Compose override is invalid."
    }

    $env:MIGRATION_APPROVAL_FILE = [System.IO.Path]::GetFullPath(
        (Join-Path $root ".andreja/validation-approval.json"))
    $env:MIGRATION_BACKUP_FILE = [System.IO.Path]::GetFullPath(
        (Join-Path $root ".andreja/validation-backup.dump"))
    $env:MIGRATION_SCRIPT_FILE = [System.IO.Path]::GetFullPath(
        (Join-Path $root ".andreja/validation-migration.sql"))
    & docker compose `
        --file (Join-Path $root "compose.yaml") `
        --file (Join-Path $root "deploy/compose.migration.yaml") `
        config --quiet
    if ($LASTEXITCODE -ne 0) {
        throw "The explicit migration Compose override is invalid."
    }
}
finally {
    $env:ANDREJA_IMAGE = $previousImage
    $env:MIGRATION_APPROVAL_FILE = $previousMigrationApproval
    $env:MIGRATION_BACKUP_FILE = $previousMigrationBackup
    $env:MIGRATION_SCRIPT_FILE = $previousMigrationScript
}

$fromDefaults = [regex]::Matches(
    $dockerfile,
    '(?m)^ARG DOTNET_[A-Z_]+_IMAGE=[^@\r\n]+@sha256:[0-9a-f]{64}\r?$')
if ($fromDefaults.Count -ne 2) {
    throw "Dockerfile base image defaults must be digest-pinned."
}
if (-not [regex]::IsMatch(
    $dockerfile,
    '(?m)^\s+org\.opencontainers\.image\.licenses="Apache-2\.0"\r?$')) {
    throw "OCI license metadata must match the root Apache-2.0 license fact."
}
if ($dockerfile.Contains('org.opencontainers.image.licenses="MIT"')) {
    throw "OCI license metadata must not assert MIT."
}
if ([regex]::IsMatch($compose, '(?m)^\s+POSTGRES_PASSWORD:\s*')) {
    throw "Compose must use a password file, not a plaintext password variable."
}
if ([regex]::IsMatch($collector, '(?m)^\s+(debug|logging):\s*$')) {
    throw "Collector must not log telemetry payloads."
}

$schemaPath = Join-Path $root "docs/operations/application-export-v1.schema.json"
Get-Content -LiteralPath $schemaPath -Raw | ConvertFrom-Json -Depth 100 | Out-Null
Write-Output "Operations contracts passed static validation."
