[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string] $DumpPath,

    [Parameter(Mandatory)]
    [string] $HostName,

    [ValidateRange(1, 65535)]
    [int] $Port = 5432,

    [Parameter(Mandatory)]
    [string] $Database,

    [Parameter(Mandatory)]
    [string] $Username,

    [Parameter(Mandatory)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string] $PasswordFile
)

$ErrorActionPreference = "Stop"
$pgRestore = Get-Command pg_restore -ErrorAction Stop
$psql = Get-Command psql -ErrorAction Stop
$resolvedDump = (Resolve-Path -LiteralPath $DumpPath).Path
$resolvedPasswordFile = (Resolve-Path -LiteralPath $PasswordFile).Path
$checksumPath = "$resolvedDump.sha256"

if (-not (Test-Path -LiteralPath $checksumPath -PathType Leaf)) {
    throw "Missing checksum sidecar: $checksumPath"
}

$expectedHash = ((Get-Content -LiteralPath $checksumPath -Raw) -split '\s+')[0].ToLowerInvariant()
$actualHash = (Get-FileHash -LiteralPath $resolvedDump -Algorithm SHA256).Hash.ToLowerInvariant()
if ($expectedHash -notmatch '^[0-9a-f]{64}$' -or $actualHash -ne $expectedHash) {
    throw "The dump checksum does not match its sidecar."
}

$previousPgPassFile = $env:PGPASSFILE
try {
    $env:PGPASSFILE = $resolvedPasswordFile
    $tableCount = & $psql.Source `
        --host $HostName `
        --port $Port `
        --username $Username `
        --dbname $Database `
        --no-password `
        --tuples-only `
        --no-align `
        --command "SELECT count(*) FROM pg_catalog.pg_tables WHERE schemaname NOT IN ('pg_catalog','information_schema');"
    if ($LASTEXITCODE -ne 0) {
        throw "The clean-target query failed with exit code $LASTEXITCODE."
    }
    if ([int]($tableCount.Trim()) -ne 0) {
        throw "Restore target is not clean; refusing to overwrite user tables."
    }

    & $pgRestore.Source `
        --host $HostName `
        --port $Port `
        --username $Username `
        --dbname $Database `
        --no-password `
        --exit-on-error `
        --single-transaction `
        --no-owner `
        --no-acl `
        $resolvedDump
    if ($LASTEXITCODE -ne 0) {
        throw "pg_restore failed with exit code $LASTEXITCODE."
    }
}
finally {
    $env:PGPASSFILE = $previousPgPassFile
}
