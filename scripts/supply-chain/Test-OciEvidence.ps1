[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $BundleDirectory,
    [string] $TrustedPublicKeyPath,
    [switch] $AllowUnsignedHostedEvidence
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'SupplyChain.Common.psm1') -Force

$bundleRoot = (Resolve-Path $BundleDirectory).Path
$evidencePath = Join-Path $bundleRoot 'evidence.json'
$schemaPath = Join-Path $bundleRoot 'evidence.schema.json'
if (-not (Test-Path -LiteralPath $evidencePath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $schemaPath -PathType Leaf)) {
    throw 'Evidence manifest or evidence schema is missing.'
}

$evidenceJson = Get-Content -LiteralPath $evidencePath -Raw
if (-not ($evidenceJson | Test-Json -SchemaFile $schemaPath -ErrorAction Stop)) {
    throw 'Evidence manifest does not satisfy the committed schema.'
}

$evidence = $evidenceJson | ConvertFrom-Json -Depth 100
Assert-ArtifactInventory -Root $bundleRoot -Artifacts $evidence.artifacts

$policyPath = Join-Path $bundleRoot $evidence.policy.path
$policy = Read-SupplyChainPolicy -Path $policyPath
if ((Get-FileSha256 -Path $policyPath) -ne $evidence.policy.sha256) {
    throw 'Supply-chain policy checksum does not match the evidence manifest.'
}

if ($evidence.source.repository -ne $policy.sourceRepository) {
    throw 'Evidence source is outside the signed policy.'
}
Assert-PlatformApproved -Platform $evidence.image.platform -Policy $policy

$evidenceBaseImages = @($evidence.build.baseImages) -join "`n"
$policyBaseImages = @($policy.baseImages.PSObject.Properties.Value) -join "`n"
if ($evidenceBaseImages -ne $policyBaseImages) {
    throw 'Base-image inventory drifted from the signed policy.'
}

foreach ($toolProperty in $policy.tools.PSObject.Properties) {
    $recorded = @($evidence.tools | Where-Object { $_.name -eq $toolProperty.Name })
    if ($recorded.Count -ne 1 -or
        $recorded[0].version -ne $toolProperty.Value.version -or
        $recorded[0].image -ne $toolProperty.Value.image) {
        throw "Pinned tool inventory mismatch: $($toolProperty.Name)."
    }
}

Assert-SbomDocument -SpdxPath (Join-Path $bundleRoot 'sbom.spdx.json') `
    -CycloneDxPath (Join-Path $bundleRoot 'sbom.cyclonedx.json')

$forbiddenSeverities = @($policy.forbiddenSeverities | ForEach-Object { $_.ToUpperInvariant() })
$dependencyFindings = Get-GrypeForbiddenCount -Path (Join-Path $bundleRoot 'scan.dependencies.json') `
    -ForbiddenSeverities $forbiddenSeverities
$imageFindings = Get-GrypeForbiddenCount -Path (Join-Path $bundleRoot 'scan.image.json') `
    -ForbiddenSeverities $forbiddenSeverities
$iacFindings = Get-TrivyForbiddenCount -Path (Join-Path $bundleRoot 'scan.iac.json') `
    -ForbiddenSeverities $forbiddenSeverities

Assert-NoForbiddenFinding -Count $dependencyFindings -Scope 'Dependency'
Assert-NoForbiddenFinding -Count $imageFindings -Scope 'Final image'
Assert-NoForbiddenFinding -Count $iacFindings -Scope 'Container/IaC'

foreach ($scan in $evidence.scans) {
    $actualCount = switch ($scan.scope) {
        'dependencies' { $dependencyFindings }
        'final-image' { $imageFindings }
        'container-iac' { $iacFindings }
        default { throw "Unknown scanner scope: $($scan.scope)" }
    }

    if (-not $scan.passed -or $scan.forbiddenFindings -ne $actualCount) {
        throw "Scanner policy result mismatch: $($scan.scope)."
    }
}

$migrationNotes = Get-Content -LiteralPath (Join-Path $bundleRoot $evidence.migrationNotes.path) -Raw
if ([string]::IsNullOrWhiteSpace($migrationNotes) -or
    -not $migrationNotes.Contains($evidence.source.commit) -or
    -not $migrationNotes.Contains($evidence.image.digest)) {
    throw 'Migration notes are missing or are not bound to the target commit and image digest.'
}

$provenancePath = Join-Path $bundleRoot $evidence.provenance.path
$provenance = Get-Content -LiteralPath $provenancePath -Raw | ConvertFrom-Json -Depth 100
Assert-ProvenanceBinding -Evidence $evidence -Provenance $provenance

$artifactByName = @{}
foreach ($artifact in $evidence.artifacts) {
    $artifactByName[$artifact.path] = $artifact.sha256
}
foreach ($byproduct in $provenance.predicate.runDetails.byproducts) {
    if (-not $artifactByName.ContainsKey($byproduct.name) -or
        $artifactByName[$byproduct.name] -ne $byproduct.digest.sha256) {
        throw "Provenance byproduct checksum mismatch: $($byproduct.name)."
    }
}

Assert-SigningStatus -Signing $evidence.signing -AllowUnsignedHostedEvidence:$AllowUnsignedHostedEvidence

Assert-RequiredCommand -Name 'docker'
Assert-RequiredCommand -Name 'tar'

if ($evidence.signing.status -eq 'signed') {
    if ([string]::IsNullOrWhiteSpace($TrustedPublicKeyPath)) {
        throw 'A separately trusted public key is required for offline verification.'
    }

    $trustedPublicKey = (Resolve-Path $TrustedPublicKeyPath).Path
    if ((Get-FileSha256 -Path $trustedPublicKey) -ne $evidence.signing.trustedPublicKeySha256) {
        throw 'The supplied trust anchor does not match the signed evidence.'
    }

    & docker image inspect $policy.tools.cosign.image *> $null
    if ($LASTEXITCODE -ne 0) {
        throw 'Pinned Cosign verifier image is not preloaded; offline verification cannot continue.'
    }

    $artifactMount = "type=bind,source=$bundleRoot,target=/work,readonly"
    $publicKeyMount = "type=bind,source=$([IO.Path]::GetDirectoryName($trustedPublicKey)),target=/trust,readonly"
    Invoke-CheckedCommand -FilePath 'docker' -Arguments @(
        'run', '--rm', '--network', 'none',
        '--mount', $artifactMount,
        '--mount', $publicKeyMount,
        $policy.tools.cosign.image,
        'verify-blob',
        '--offline',
        '--insecure-ignore-tlog',
        '--key', "/trust/$([IO.Path]::GetFileName($trustedPublicKey))",
        '--signature', "/work/$($evidence.signing.signature.path)",
        "/work/$($evidence.provenance.path)"
    ) -FailureMessage 'Provenance signature is invalid or untrusted.'
}

$archivePath = Join-Path $bundleRoot $evidence.image.archive.path
$indexText = Invoke-CheckedCommand -FilePath 'tar' -Arguments @(
    '-xOf', $archivePath, 'index.json'
) -FailureMessage 'Unable to inspect the OCI archive.' -CaptureOutput
$index = $indexText | ConvertFrom-Json -Depth 100
if (@($index.manifests).Count -ne 1 -or
    $index.manifests[0].digest -ne $evidence.image.digest -or
    $index.manifests[0].mediaType -ne 'application/vnd.oci.image.manifest.v1+json') {
    throw 'OCI archive digest does not match the signed evidence.'
}

$manifestBlob = "blobs/sha256/$($evidence.image.digest.Substring(7))"
$manifestText = Invoke-CheckedCommand -FilePath 'tar' -Arguments @(
    '-xOf', $archivePath, $manifestBlob
) -FailureMessage 'Unable to inspect the OCI manifest.' -CaptureOutput
$manifest = $manifestText | ConvertFrom-Json -Depth 100
if ($manifest.mediaType -ne 'application/vnd.oci.image.manifest.v1+json' -or
    -not ($manifest.PSObject.Properties.Name -contains 'config') -or
    $manifest.config.digest -ne $evidence.image.configDigest) {
    throw 'OCI config digest does not match the signed evidence.'
}

$configBlob = "blobs/sha256/$($evidence.image.configDigest.Substring(7))"
$configText = Invoke-CheckedCommand -FilePath 'tar' -Arguments @(
    '-xOf', $archivePath, $configBlob
) -FailureMessage 'Unable to inspect the OCI config.' -CaptureOutput
$config = $configText | ConvertFrom-Json -Depth 100
Assert-OciPlatformBinding -ExpectedPlatform $evidence.image.platform -Index $index `
    -Manifest $manifest -Config $config -ExpectedManifestDigest $evidence.image.digest `
    -ExpectedConfigDigest $evidence.image.configDigest

if ((Get-Content -LiteralPath (Join-Path $bundleRoot 'image.digest') -Raw).Trim() -ne
    $evidence.image.digest) {
    throw 'Human-readable image digest does not match the signed evidence.'
}

if ($evidence.signing.status -eq 'unsigned') {
    Write-Output 'Hosted unsigned evidence validated structurally; it is not trusted for startup, update, or release.'
    return
}

Invoke-CheckedCommand -FilePath 'docker' -Arguments @(
    'load', '--input', $archivePath
) -FailureMessage 'Verified OCI archive could not be loaded into the local image store.'

$loadedImageJson = Invoke-CheckedCommand -FilePath 'docker' -Arguments @(
    'image', 'inspect', $evidence.image.immutableReference
) -FailureMessage 'The loaded image is not resolvable by its immutable digest reference.' -CaptureOutput
$loadedImage = @($loadedImageJson | ConvertFrom-Json -Depth 100)
$platformParts = $evidence.image.platform.Split('/', 2)
if ($loadedImage.Count -ne 1 -or
    $loadedImage[0].Descriptor.digest -ne $evidence.image.digest -or
    $loadedImage[0].Os -ne $platformParts[0] -or
    $loadedImage[0].Architecture -ne $platformParts[1] -or
    $loadedImage[0].Config.Labels.'org.opencontainers.image.revision' -ne $evidence.source.commit -or
    $loadedImage[0].Config.Labels.'org.opencontainers.image.source' -ne $evidence.source.repository -or
    $loadedImage[0].Config.Labels.'org.opencontainers.image.base.digest' -ne
        $policy.baseImages.runtime.Split('@')[1]) {
    throw 'Loaded image descriptor or source/base labels do not match the verified OCI archive.'
}

Write-Output "Verified offline image: $($evidence.image.immutableReference)"
Write-Output "Set ANDREJA_IMAGE=$($evidence.image.immutableReference) before Compose start/update."
