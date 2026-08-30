[CmdletBinding()]
param(
    [string] $OutputDirectory = 'artifacts/supply-chain',
    [string] $ImageName = 'andreja-local',
    [string] $Platform,
    [string] $SigningKeyPath,
    [string] $TrustedPublicKeyPath,
    [switch] $HostedUnsigned
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'SupplyChain.Common.psm1') -Force

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$policyPath = Join-Path $repositoryRoot 'supply-chain-policy.json'
$policy = Read-SupplyChainPolicy -Path $policyPath

if ($HostedUnsigned -and ($SigningKeyPath -or $TrustedPublicKeyPath)) {
    throw 'Hosted unsigned validation cannot receive signing material.'
}

if (-not $HostedUnsigned -and
    ([string]::IsNullOrWhiteSpace($SigningKeyPath) -or
     [string]::IsNullOrWhiteSpace($TrustedPublicKeyPath))) {
    throw 'Local trusted evidence requires both -SigningKeyPath and -TrustedPublicKeyPath.'
}

& (Join-Path $PSScriptRoot 'Test-SupplyChainPolicy.ps1') -RepositoryRoot $repositoryRoot

Assert-RequiredCommand -Name 'git'
Assert-RequiredCommand -Name 'docker'
Assert-RequiredCommand -Name 'tar'

$dirty = Invoke-CheckedCommand -FilePath 'git' -Arguments @(
    '-C', $repositoryRoot, 'status', '--porcelain'
) -FailureMessage 'Unable to inspect the source worktree.' -CaptureOutput
if (-not [string]::IsNullOrWhiteSpace($dirty)) {
    throw 'OCI evidence must be built from a clean reviewed commit.'
}

$sourceRevision = Invoke-CheckedCommand -FilePath 'git' -Arguments @(
    '-C', $repositoryRoot, 'rev-parse', 'HEAD'
) -FailureMessage 'Unable to resolve source commit.' -CaptureOutput
$sourceTree = Invoke-CheckedCommand -FilePath 'git' -Arguments @(
    '-C', $repositoryRoot, 'rev-parse', 'HEAD^{tree}'
) -FailureMessage 'Unable to resolve source tree.' -CaptureOutput
$sourceRef = if ($env:GITHUB_REF) {
    $env:GITHUB_REF
} else {
    $branch = Invoke-CheckedCommand -FilePath 'git' -Arguments @(
        '-C', $repositoryRoot, 'symbolic-ref', '--quiet', '--short', 'HEAD'
    ) -FailureMessage 'Supply-chain evidence requires a named source ref.' -CaptureOutput
    "refs/heads/$branch"
}
if ($sourceRef -notmatch '^refs/(heads|tags)/[A-Za-z0-9][A-Za-z0-9._/-]*$' -and
    $sourceRef -notmatch '^refs/pull/[0-9]+/(merge|head)$') {
    throw "Unsupported source ref '$sourceRef'."
}
$sourceDateEpochText = Invoke-CheckedCommand -FilePath 'git' -Arguments @(
    '-C', $repositoryRoot, 'show', '-s', '--format=%ct', 'HEAD'
) -FailureMessage 'Unable to resolve source timestamp.' -CaptureOutput
$sourceDateEpoch = [long]::Parse($sourceDateEpochText)

$dockerVersion = Invoke-CheckedCommand -FilePath 'docker' -Arguments @(
    'version', '--format', '{{.Server.Version}}'
) -FailureMessage 'Docker daemon is unavailable; the supply-chain gate cannot continue.' -CaptureOutput
$dockerArchitecture = Invoke-CheckedCommand -FilePath 'docker' -Arguments @(
    'info', '--format', '{{.Architecture}}'
) -FailureMessage 'Docker daemon architecture is unavailable.' -CaptureOutput
if ([string]::IsNullOrWhiteSpace($Platform)) {
    $Platform = switch ($dockerArchitecture) {
        'amd64' { 'linux/amd64' }
        'x86_64' { 'linux/amd64' }
        'arm64' { 'linux/arm64' }
        'aarch64' { 'linux/arm64' }
        default { throw "Unsupported Docker architecture '$dockerArchitecture'." }
    }
}
Assert-PlatformApproved -Platform $Platform -Policy $policy
$buildxVersionOutput = Invoke-CheckedCommand -FilePath 'docker' -Arguments @(
    'buildx', 'version'
) -FailureMessage 'Docker Buildx is unavailable; reproducible OCI output cannot be built.' -CaptureOutput
$buildxVersion = ($buildxVersionOutput -split '\s+' | Where-Object { $_ -match '^v?[0-9]+\.' } | Select-Object -First 1)
if (-not $buildxVersion) {
    throw 'Unable to record the Docker Buildx version.'
}

$dotnetVersion = Invoke-CheckedCommand -FilePath 'dotnet' -Arguments @('--version') `
    -FailureMessage 'Pinned .NET SDK is unavailable.' -CaptureOutput
$pinnedDotnetVersion = (Get-Content -LiteralPath (Join-Path $repositoryRoot 'global.json') -Raw |
    ConvertFrom-Json).sdk.version
if ($dotnetVersion -ne $pinnedDotnetVersion) {
    throw "Expected .NET SDK $pinnedDotnetVersion, found $dotnetVersion."
}

Invoke-CheckedCommand -FilePath 'dotnet' -Arguments @(
    'restore',
    (Join-Path $repositoryRoot 'src\Andreja.AppHost\Andreja.AppHost.csproj')
) -FailureMessage 'Host dependency restore failed; a complete verified NuGet cache is required.'

$globalNugetCache = if ($env:NUGET_PACKAGES) {
    $env:NUGET_PACKAGES
} else {
    Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget\packages'
}
if (-not (Test-Path -LiteralPath $globalNugetCache -PathType Container)) {
    throw "NuGet package cache is unavailable: $globalNugetCache"
}
$globalNugetCache = (Resolve-Path $globalNugetCache).Path

$nugetCache = Join-Path $repositoryRoot '.andreja\supply-chain-nuget-cache'
Remove-Item -LiteralPath $nugetCache -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $nugetCache -Force | Out-Null
$assetFiles = @(Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'src') `
    -Filter 'project.assets.json' -File -Recurse)
if ($assetFiles.Count -eq 0) {
    throw 'Host restore produced no project asset inventories.'
}
$packageInventory = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($assetFile in $assetFiles) {
    $assets = Get-Content -LiteralPath $assetFile.FullName -Raw | ConvertFrom-Json -Depth 100
    foreach ($library in $assets.libraries.PSObject.Properties) {
        if ($library.Value.type -eq 'package') {
            $null = $packageInventory.Add($library.Name)
        }
    }
}

foreach ($package in $packageInventory) {
    $parts = $package.Split('/', 2)
    if ($parts.Count -ne 2) {
        throw "Invalid package inventory entry '$package'."
    }

    $packageName = $parts[0].ToLowerInvariant()
    $packageVersion = $parts[1].ToLowerInvariant()
    $sourcePackage = Join-Path $globalNugetCache "$packageName\$packageVersion"
    if (-not (Test-Path -LiteralPath $sourcePackage -PathType Container)) {
        throw "Required restored package is missing from the cache: $package."
    }

    $destinationParent = Join-Path $nugetCache $packageName
    New-Item -ItemType Directory -Path $destinationParent -Force | Out-Null
    Copy-Item -LiteralPath $sourcePackage -Destination (Join-Path $destinationParent $packageVersion) `
        -Recurse -Force
}
$nugetCache = (Resolve-Path $nugetCache).Path

if (Test-Path -LiteralPath $OutputDirectory) {
    if (@(Get-ChildItem -LiteralPath $OutputDirectory -Force).Count -gt 0) {
        throw "Output directory must be empty: $OutputDirectory"
    }
} else {
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
}

$outputRoot = (Resolve-Path $OutputDirectory).Path
$cacheRoot = Join-Path $outputRoot '.scanner-cache'
New-Item -ItemType Directory -Path $cacheRoot -Force | Out-Null

function Initialize-ToolImage {
    param([Parameter(Mandatory)][string] $Reference)

    & docker image inspect $Reference *> $null
    if ($LASTEXITCODE -ne 0) {
        Invoke-CheckedCommand -FilePath 'docker' -Arguments @('pull', '--quiet', $Reference) `
            -FailureMessage "Pinned scanner image is unavailable: $Reference"
    }
}

function Get-ToolVersion {
    param(
        [Parameter(Mandatory)][string] $Reference,
        [Parameter(Mandatory)][string[]] $Arguments,
        [Parameter(Mandatory)][string] $Expected
    )

    $dockerArguments = @(
        'run', '--rm', '--network', 'none',
        '--env', 'SYFT_CHECK_FOR_APP_UPDATE=false',
        '--env', 'GRYPE_CHECK_FOR_APP_UPDATE=false',
        $Reference
    ) + $Arguments
    $output = Invoke-CheckedCommand -FilePath 'docker' -Arguments $dockerArguments `
        -FailureMessage "Unable to execute pinned tool $Reference." -CaptureOutput

    if (-not $output.Contains($Expected)) {
        throw "Pinned tool $Reference did not report expected version $Expected."
    }

    $Expected
}

foreach ($tool in $policy.tools.PSObject.Properties.Value) {
    Initialize-ToolImage -Reference $tool.image
}

$toolVersions = [ordered]@{
    syft = Get-ToolVersion -Reference $policy.tools.syft.image -Arguments @('version', '-o', 'json') -Expected $policy.tools.syft.version
    grype = Get-ToolVersion -Reference $policy.tools.grype.image -Arguments @('version', '-o', 'json') -Expected $policy.tools.grype.version
    trivy = Get-ToolVersion -Reference $policy.tools.trivy.image -Arguments @('--version') -Expected $policy.tools.trivy.version
    cosign = Get-ToolVersion -Reference $policy.tools.cosign.image -Arguments @('version', '--json') -Expected $policy.tools.cosign.version
}

$archivePath = Join-Path $outputRoot 'image.oci.tar'
$secondArchivePath = Join-Path $outputRoot 'image.reproducibility.oci.tar'
$firstMetadataPath = Join-Path $outputRoot '.build-first.json'
$secondMetadataPath = Join-Path $outputRoot '.build-second.json'
$imageTag = "$ImageName`:$sourceRevision"

function Build-OciArchive {
    param(
        [Parameter(Mandatory)][string] $Destination,
        [Parameter(Mandatory)][string] $MetadataPath
    )

    $previousEpoch = $env:SOURCE_DATE_EPOCH
    try {
        $env:SOURCE_DATE_EPOCH = [string] $sourceDateEpoch
        Invoke-CheckedCommand -FilePath 'docker' -Arguments @(
            'buildx', 'build',
            '--progress', 'plain',
            '--no-cache',
            '--platform', $Platform,
            '--network', 'none',
            '--build-arg', "SOURCE_REVISION=$sourceRevision",
            '--build-arg', "SOURCE_DATE_EPOCH=$sourceDateEpoch",
            '--build-context', "nuget-cache=$nugetCache",
            '--target', 'final',
            '--provenance=false',
            '--sbom=false',
            '--metadata-file', $MetadataPath,
            '--output', "type=oci,dest=$Destination,name=$imageTag,rewrite-timestamp=true,tar=true",
            $repositoryRoot
        ) -FailureMessage 'Reproducible OCI image build failed.'
    } finally {
        $env:SOURCE_DATE_EPOCH = $previousEpoch
    }
}

function Read-OciArtifact {
    param([Parameter(Mandatory)][string] $Archive)

    $indexText = Invoke-CheckedCommand -FilePath 'tar' -Arguments @(
        '-xOf', $Archive, 'index.json'
    ) -FailureMessage 'Unable to read OCI image index.' -CaptureOutput
    $index = $indexText | ConvertFrom-Json -Depth 100
    if (@($index.manifests).Count -ne 1 -or $index.manifests[0].digest -notmatch '^sha256:[0-9a-f]{64}$') {
        throw 'OCI archive must contain exactly one sha256-addressed platform manifest.'
    }

    $manifestDigest = [string] $index.manifests[0].digest
    $manifestBlob = "blobs/sha256/$($manifestDigest.Substring(7))"
    $manifestText = Invoke-CheckedCommand -FilePath 'tar' -Arguments @(
        '-xOf', $Archive, $manifestBlob
    ) -FailureMessage 'Unable to read OCI image manifest.' -CaptureOutput
    $manifest = $manifestText | ConvertFrom-Json -Depth 100
    if ($manifest.config.digest -notmatch '^sha256:[0-9a-f]{64}$') {
        throw 'OCI image manifest has no valid config digest.'
    }

    $configDigest = [string] $manifest.config.digest
    $configBlob = "blobs/sha256/$($configDigest.Substring(7))"
    $configText = Invoke-CheckedCommand -FilePath 'tar' -Arguments @(
        '-xOf', $Archive, $configBlob
    ) -FailureMessage 'Unable to read OCI image config.' -CaptureOutput
    $config = $configText | ConvertFrom-Json -Depth 100
    Assert-OciPlatformBinding -ExpectedPlatform $Platform -Index $index `
        -Manifest $manifest -Config $config -ExpectedManifestDigest $manifestDigest `
        -ExpectedConfigDigest $configDigest

    [ordered]@{
        digest = $manifestDigest
        configDigest = $configDigest
        platform = "$($config.os)/$($config.architecture)"
    }
}

Build-OciArchive -Destination $archivePath -MetadataPath $firstMetadataPath
$firstOci = Read-OciArtifact -Archive $archivePath
Build-OciArchive -Destination $secondArchivePath -MetadataPath $secondMetadataPath
$secondOci = Read-OciArtifact -Archive $secondArchivePath

if ($firstOci.digest -ne $secondOci.digest -or $firstOci.configDigest -ne $secondOci.configDigest) {
    throw "Independent image builds were not reproducible ($($firstOci.digest) != $($secondOci.digest))."
}

Remove-Item -LiteralPath $secondArchivePath, $firstMetadataPath, $secondMetadataPath -Force

$artifactMount = "type=bind,source=$outputRoot,target=/work"
$repositoryMount = "type=bind,source=$repositoryRoot,target=/src,readonly"
$cacheMount = "type=bind,source=$cacheRoot,target=/root/.cache"

Invoke-CheckedCommand -FilePath 'docker' -Arguments @(
    'run', '--rm', '--network', 'none',
    '--env', 'SYFT_CHECK_FOR_APP_UPDATE=false',
    '--mount', $artifactMount,
    $policy.tools.syft.image,
    'oci-archive:/work/image.oci.tar',
    '-o', 'spdx-json=/work/sbom.spdx.json',
    '-o', 'cyclonedx-json=/work/sbom.cyclonedx.json'
) -FailureMessage 'Pinned Syft SBOM generation failed.'

Assert-SbomDocument -SpdxPath (Join-Path $outputRoot 'sbom.spdx.json') `
    -CycloneDxPath (Join-Path $outputRoot 'sbom.cyclonedx.json')

Invoke-CheckedCommand -FilePath 'docker' -Arguments @(
    'run', '--rm',
    '--env', 'GRYPE_CHECK_FOR_APP_UPDATE=false',
    '--mount', $artifactMount,
    '--mount', $cacheMount,
    $policy.tools.grype.image,
    'sbom:/work/sbom.cyclonedx.json',
    '--output', 'json',
    '--file', '/work/scan.dependencies.json'
) -FailureMessage 'Pinned Grype dependency scan failed or its advisory database was unavailable.'

Invoke-CheckedCommand -FilePath 'docker' -Arguments @(
    'run', '--rm',
    '--env', 'GRYPE_CHECK_FOR_APP_UPDATE=false',
    '--mount', $artifactMount,
    '--mount', $cacheMount,
    $policy.tools.grype.image,
    'oci-archive:/work/image.oci.tar',
    '--output', 'json',
    '--file', '/work/scan.image.json'
) -FailureMessage 'Pinned Grype image scan failed or its advisory database was unavailable.'

Invoke-CheckedCommand -FilePath 'docker' -Arguments @(
    'run', '--rm', '--network', 'none',
    '--mount', $artifactMount,
    '--mount', $repositoryMount,
    $policy.tools.trivy.image,
    'config',
    '--format', 'json',
    '--output', '/work/scan.iac.json',
    '--skip-check-update',
    '--severity', ($policy.forbiddenSeverities -join ','),
    '/src'
) -FailureMessage 'Pinned Trivy container/IaC scan failed.'

$forbiddenSeverities = @($policy.forbiddenSeverities | ForEach-Object { $_.ToUpperInvariant() })
$dependencyFindings = Get-GrypeForbiddenCount -Path (Join-Path $outputRoot 'scan.dependencies.json') `
    -ForbiddenSeverities $forbiddenSeverities
$imageFindings = Get-GrypeForbiddenCount -Path (Join-Path $outputRoot 'scan.image.json') `
    -ForbiddenSeverities $forbiddenSeverities
$iacFindings = Get-TrivyForbiddenCount -Path (Join-Path $outputRoot 'scan.iac.json') `
    -ForbiddenSeverities $forbiddenSeverities

Assert-NoForbiddenFinding -Count $dependencyFindings -Scope 'Dependency'
Assert-NoForbiddenFinding -Count $imageFindings -Scope 'Final image'
Assert-NoForbiddenFinding -Count $iacFindings -Scope 'Container/IaC'

Remove-Item -LiteralPath $cacheRoot -Recurse -Force
Copy-Item -LiteralPath $policyPath -Destination (Join-Path $outputRoot 'policy.json')
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'docs\operations\oci-supply-chain-evidence-v1.1.schema.json') `
    -Destination (Join-Path $outputRoot 'evidence.schema.json')

$migrationSource = Get-Content -LiteralPath (Join-Path $repositoryRoot 'docs\operations\oci-migration-notes.md') -Raw
@"
# Andreja image migration notes

- Source commit: ``$sourceRevision``
- Image digest: ``$($firstOci.digest)``

$migrationSource
"@ | Set-Content -LiteralPath (Join-Path $outputRoot 'migration-notes.md') -Encoding utf8NoBOM

$firstOci.digest | Set-Content -LiteralPath (Join-Path $outputRoot 'image.digest') -Encoding ascii -NoNewline

$byproductNames = @(
    'image.oci.tar',
    'image.digest',
    'policy.json',
    'evidence.schema.json',
    'sbom.spdx.json',
    'sbom.cyclonedx.json',
    'scan.dependencies.json',
    'scan.image.json',
    'scan.iac.json',
    'migration-notes.md'
)
$byproducts = @($byproductNames | ForEach-Object {
    [ordered]@{
        name = $_
        digest = [ordered]@{ sha256 = Get-FileSha256 -Path (Join-Path $outputRoot $_) }
    }
})

$provenance = [ordered]@{
    _type = 'https://in-toto.io/Statement/v1'
    subject = @([ordered]@{
        name = $ImageName
        digest = [ordered]@{ sha256 = $firstOci.digest.Substring(7) }
    })
    predicateType = 'https://slsa.dev/provenance/v1'
    predicate = [ordered]@{
        buildDefinition = [ordered]@{
            buildType = 'https://andreja.local/buildtypes/oci-buildx/v1'
            externalParameters = [ordered]@{
                dockerfile = 'Dockerfile'
                platform = $firstOci.platform
                sourceRevision = $sourceRevision
                sourceDateEpoch = $sourceDateEpoch
            }
            internalParameters = [ordered]@{
                policyId = $policy.policyId
                dockerVersion = $dockerVersion
                buildxVersion = $buildxVersion
                dotnetVersion = $dotnetVersion
                scannerVersions = $toolVersions
                scannerConfiguration = [ordered]@{
                    dependencySbom = 'CycloneDX-1.6 ecosystem package identities'
                    trivyChecks = 'embedded checks from pinned scanner image; update disabled'
                }
            }
            resolvedDependencies = @(
                [ordered]@{
                    uri = "$($policy.sourceRepository)@git:$sourceRevision"
                    digest = [ordered]@{ gitTree = $sourceTree }
                },
                [ordered]@{ uri = $policy.baseImages.sdk },
                [ordered]@{ uri = $policy.baseImages.runtime }
            )
        }
        runDetails = [ordered]@{
            builder = [ordered]@{ id = 'https://andreja.local/builders/local-buildx/v1' }
            metadata = [ordered]@{
                sourceDateEpoch = $sourceDateEpoch
                reproducibleBuilds = 2
            }
            byproducts = $byproducts
        }
    }
}
$provenance | ConvertTo-Json -Depth 100 |
    Set-Content -LiteralPath (Join-Path $outputRoot 'provenance.json') -Encoding utf8NoBOM

$signing = if ($HostedUnsigned) {
    [ordered]@{
        mode = 'hosted-unsigned-validation'
        status = 'unsigned'
        trustedPublicKeySha256 = $null
        signature = $null
        bundle = $null
        trustedRoot = $null
        certificateIdentity = $null
        oidcIssuer = $null
        repository = $null
        workflow = $null
        workflowRevision = $null
        ref = $null
        trigger = $null
        transparencyLogIncluded = $null
        certificateTransparencyIncluded = $null
        hostedDeferral = 'Hosted validation is unsigned until the tag-gated keyless signing job completes.'
    }
} else {
    $signingKey = (Resolve-Path $SigningKeyPath).Path
    $trustedPublicKey = (Resolve-Path $TrustedPublicKeyPath).Path
    $keyMount = "type=bind,source=$([IO.Path]::GetDirectoryName($signingKey)),target=/signing,readonly"
    $publicKeyMount = "type=bind,source=$([IO.Path]::GetDirectoryName($trustedPublicKey)),target=/trust,readonly"

    Invoke-CheckedCommand -FilePath 'docker' -Arguments @(
        'run', '--rm', '--network', 'none',
        '--env', 'COSIGN_PASSWORD',
        '--mount', $artifactMount,
        '--mount', $keyMount,
        $policy.tools.cosign.image,
        'sign-blob',
        '--yes',
        '--new-bundle-format=false',
        '--use-signing-config=false',
        '--tlog-upload=false',
        '--key', "/signing/$([IO.Path]::GetFileName($signingKey))",
        '--output-signature', '/work/provenance.sig',
        '/work/provenance.json'
    ) -FailureMessage 'Operator-held provenance signing failed.'

    Invoke-CheckedCommand -FilePath 'docker' -Arguments @(
        'run', '--rm', '--network', 'none',
        '--mount', $artifactMount,
        '--mount', $publicKeyMount,
        $policy.tools.cosign.image,
        'verify-blob',
        '--offline',
        '--insecure-ignore-tlog',
        '--key', "/trust/$([IO.Path]::GetFileName($trustedPublicKey))",
        '--signature', '/work/provenance.sig',
        '/work/provenance.json'
    ) -FailureMessage 'Immediate provenance signature verification failed.'

    [ordered]@{
        mode = 'operator-held-key'
        status = 'signed'
        trustedPublicKeySha256 = Get-FileSha256 -Path $trustedPublicKey
        signature = Get-ChecksummedFile -Root $outputRoot -Name 'provenance.sig'
        bundle = $null
        trustedRoot = $null
        certificateIdentity = $null
        oidcIssuer = $null
        repository = $null
        workflow = $null
        workflowRevision = $null
        ref = $null
        trigger = $null
        transparencyLogIncluded = $null
        certificateTransparencyIncluded = $null
        hostedDeferral = $null
    }
}

$toolEvidence = @(
    [ordered]@{ name = 'docker'; version = $dockerVersion; image = $null },
    [ordered]@{ name = 'buildx'; version = $buildxVersion; image = $null },
    [ordered]@{ name = 'dotnet'; version = $dotnetVersion; image = $null }
)
foreach ($toolProperty in $policy.tools.PSObject.Properties) {
    $toolEvidence += [ordered]@{
        name = $toolProperty.Name
        version = $toolVersions[$toolProperty.Name]
        image = $toolProperty.Value.image
    }
}

$artifactNames = $byproductNames + @('provenance.json')
if ($signing.status -eq 'signed') {
    $artifactNames += 'provenance.sig'
}
$artifacts = @($artifactNames | ForEach-Object { Get-ChecksummedFile -Root $outputRoot -Name $_ })

$evidence = [ordered]@{
    schemaVersion = '1.1'
    policy = Get-ChecksummedFile -Root $outputRoot -Name 'policy.json'
    source = [ordered]@{
        repository = $policy.sourceRepository
        commit = $sourceRevision
        tree = $sourceTree
        ref = $sourceRef
    }
    image = [ordered]@{
        name = $ImageName
        digest = $firstOci.digest
        configDigest = $firstOci.configDigest
        immutableReference = "$ImageName@$($firstOci.digest)"
        platform = $firstOci.platform
        archive = Get-ChecksummedFile -Root $outputRoot -Name 'image.oci.tar'
    }
    build = [ordered]@{
        sourceDateEpoch = $sourceDateEpoch
        firstDigest = $firstOci.digest
        secondDigest = $secondOci.digest
        reproducible = $true
        baseImages = @($policy.baseImages.PSObject.Properties.Value)
    }
    tools = $toolEvidence
    sboms = @(
        [ordered]@{
            format = 'SPDX-2.3'
            path = 'sbom.spdx.json'
            sha256 = Get-FileSha256 -Path (Join-Path $outputRoot 'sbom.spdx.json')
        },
        [ordered]@{
            format = 'CycloneDX-1.6'
            path = 'sbom.cyclonedx.json'
            sha256 = Get-FileSha256 -Path (Join-Path $outputRoot 'sbom.cyclonedx.json')
        }
    )
    scans = @(
        [ordered]@{
            scope = 'dependencies'
            scanner = "grype@$($toolVersions.grype)"
            forbiddenFindings = $dependencyFindings
            passed = $true
            path = 'scan.dependencies.json'
            sha256 = Get-FileSha256 -Path (Join-Path $outputRoot 'scan.dependencies.json')
        },
        [ordered]@{
            scope = 'final-image'
            scanner = "grype@$($toolVersions.grype)"
            forbiddenFindings = $imageFindings
            passed = $true
            path = 'scan.image.json'
            sha256 = Get-FileSha256 -Path (Join-Path $outputRoot 'scan.image.json')
        },
        [ordered]@{
            scope = 'container-iac'
            scanner = "trivy@$($toolVersions.trivy)"
            forbiddenFindings = $iacFindings
            passed = $true
            path = 'scan.iac.json'
            sha256 = Get-FileSha256 -Path (Join-Path $outputRoot 'scan.iac.json')
        }
    )
    migrationNotes = Get-ChecksummedFile -Root $outputRoot -Name 'migration-notes.md'
    provenance = Get-ChecksummedFile -Root $outputRoot -Name 'provenance.json'
    signing = $signing
    artifacts = $artifacts
}

$evidence | ConvertTo-Json -Depth 100 |
    Set-Content -LiteralPath (Join-Path $outputRoot 'evidence.json') -Encoding utf8NoBOM

Assert-ArtifactInventory -Root $outputRoot -Artifacts $evidence.artifacts
Write-Output "OCI evidence created for $($evidence.image.immutableReference)."
if ($HostedUnsigned) {
    Write-Output 'Hosted evidence is intentionally unsigned and cannot authorize startup or release.'
}
