[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string] $BackupDumpPath,

    [Parameter(Mandatory)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string] $ReviewedMigrationScriptPath,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string] $DatabaseName,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string[]] $ApprovedMigrations,

    [string] $ApprovalFilePath = ".andreja/migration-approval.json",

    [Parameter(Mandatory)]
    [switch] $ConfirmBackupRestoreAndMigrationReview
)

$ErrorActionPreference = "Stop"
if (-not $ConfirmBackupRestoreAndMigrationReview) {
    throw "Migration requires explicit backup restore, rollback, and SQL review confirmation."
}

$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../.."))
$backup = (Resolve-Path -LiteralPath $BackupDumpPath).Path
$migrationScript = (Resolve-Path -LiteralPath $ReviewedMigrationScriptPath).Path
$backupChecksumPath = "$backup.sha256"
if (-not (Test-Path -LiteralPath $backupChecksumPath -PathType Leaf)) {
    throw "Missing backup checksum sidecar: $backupChecksumPath"
}

$expectedBackupHash =
    ((Get-Content -LiteralPath $backupChecksumPath -Raw) -split '\s+')[0].ToLowerInvariant()
$actualBackupHash =
    (Get-FileHash -LiteralPath $backup -Algorithm SHA256).Hash.ToLowerInvariant()
if ($expectedBackupHash -notmatch '^[0-9a-f]{64}$' -or
    $actualBackupHash -ne $expectedBackupHash) {
    throw "The backup checksum does not match its sidecar."
}

$migrationScriptHash =
    (Get-FileHash -LiteralPath $migrationScript -Algorithm SHA256).Hash.ToLowerInvariant()
$approvalPath = [System.IO.Path]::GetFullPath($ApprovalFilePath)
[System.IO.Directory]::CreateDirectory(
    [System.IO.Path]::GetDirectoryName($approvalPath)) | Out-Null

[ordered]@{
    databaseName = $DatabaseName
    backupPath = "/run/andreja/backup.dump"
    backupSha256 = $actualBackupHash
    migrationScriptPath = "/run/andreja/migration.sql"
    migrationScriptSha256 = $migrationScriptHash
    approvedMigrations = @($ApprovedMigrations)
} | ConvertTo-Json -Depth 4 |
    Set-Content -LiteralPath $approvalPath -Encoding utf8NoBOM

$previousApproval = $env:MIGRATION_APPROVAL_FILE
$previousBackup = $env:MIGRATION_BACKUP_FILE
$previousScript = $env:MIGRATION_SCRIPT_FILE
try {
    $env:MIGRATION_APPROVAL_FILE = $approvalPath
    $env:MIGRATION_BACKUP_FILE = $backup
    $env:MIGRATION_SCRIPT_FILE = $migrationScript

    $postgresContainer = & docker compose `
        --file (Join-Path $root "compose.yaml") `
        ps --status running --quiet postgres
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($postgresContainer)) {
        throw "PostgreSQL must already be running and healthy before migration."
    }

    & docker compose `
        --file (Join-Path $root "compose.yaml") `
        --file (Join-Path $root "deploy/compose.migration.yaml") `
        run --rm --no-deps app `
        --migrate-database `
        --approval-file /run/andreja/migration-approval.json
    if ($LASTEXITCODE -ne 0) {
        throw "The explicit database migration command failed with exit code $LASTEXITCODE."
    }
}
finally {
    $env:MIGRATION_APPROVAL_FILE = $previousApproval
    $env:MIGRATION_BACKUP_FILE = $previousBackup
    $env:MIGRATION_SCRIPT_FILE = $previousScript
}
