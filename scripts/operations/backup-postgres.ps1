[CmdletBinding()]
param(
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
    [string] $PasswordFile,

    [string] $OutputDirectory = "backups/postgres"
)

$ErrorActionPreference = "Stop"
$pgDump = Get-Command pg_dump -ErrorAction Stop
$resolvedPasswordFile = (Resolve-Path -LiteralPath $PasswordFile).Path
$outputPath = [System.IO.Path]::GetFullPath($OutputDirectory)
[System.IO.Directory]::CreateDirectory($outputPath) | Out-Null

$stamp = [DateTimeOffset]::UtcNow.ToString("yyyyMMddTHHmmssZ")
$dumpPath = Join-Path $outputPath "andreja-$stamp.dump"
$checksumPath = "$dumpPath.sha256"
$metadataPath = "$dumpPath.metadata.txt"
$previousPgPassFile = $env:PGPASSFILE

try {
    $env:PGPASSFILE = $resolvedPasswordFile
    & $pgDump.Source `
        --host $HostName `
        --port $Port `
        --username $Username `
        --dbname $Database `
        --format custom `
        --compress 9 `
        --no-owner `
        --no-acl `
        --no-password `
        --file $dumpPath
    if ($LASTEXITCODE -ne 0) {
        throw "pg_dump failed with exit code $LASTEXITCODE."
    }
}
catch {
    Remove-Item -LiteralPath $dumpPath -Force -ErrorAction SilentlyContinue
    throw
}
finally {
    $env:PGPASSFILE = $previousPgPassFile
}

$hash = (Get-FileHash -LiteralPath $dumpPath -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $([System.IO.Path]::GetFileName($dumpPath))" |
    Set-Content -LiteralPath $checksumPath -Encoding ascii
@(
    "created_at_utc=$([DateTimeOffset]::UtcNow.ToString("O"))"
    "database=$Database"
    "host=$HostName"
    "port=$Port"
    "pg_dump=$(& $pgDump.Source --version)"
    "sha256=$hash"
) | Set-Content -LiteralPath $metadataPath -Encoding utf8NoBOM

Write-Output $dumpPath
