[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $BundleDirectory,
    [Parameter(Mandatory)][string] $TrustedRootPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'SupplyChain.Common.psm1') -Force

if ($env:GITHUB_ACTIONS -ne 'true' -or
    $env:GITHUB_EVENT_NAME -ne 'push' -or
    $env:GITHUB_REF_TYPE -ne 'tag') {
    throw 'Keyless signing is allowed only in the GitHub Actions tag signing job.'
}

$bundleRoot = (Resolve-Path $BundleDirectory).Path
$trustedRoot = (Resolve-Path $TrustedRootPath).Path
$bundlePrefix = [IO.Path]::GetFullPath($bundleRoot).TrimEnd(
    [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::AltDirectorySeparatorChar
) + [IO.Path]::DirectorySeparatorChar
if ([IO.Path]::GetFullPath($trustedRoot).StartsWith(
    $bundlePrefix,
    [StringComparison]::OrdinalIgnoreCase
)) {
    throw 'The independently trusted Sigstore root must be outside the evidence bundle.'
}
Assert-SigstoreTrustedRootStructure -Path $trustedRoot
$evidencePath = Join-Path $bundleRoot 'evidence.json'
$evidence = Get-Content -LiteralPath $evidencePath -Raw | ConvertFrom-Json -Depth 100
$policyPath = Join-Path $bundleRoot $evidence.policy.path
$policy = Read-SupplyChainPolicy -Path $policyPath

if ($env:GITHUB_REPOSITORY -ne $policy.hostedSigning.repository -or
    $env:GITHUB_REF -notmatch $policy.hostedSigning.allowedRefPattern -or
    $env:GITHUB_SHA -notmatch '^[0-9a-f]{40}$' -or
    $env:GITHUB_WORKFLOW_SHA -ne $env:GITHUB_SHA) {
    throw 'GitHub signing context is outside the approved repository, tag, or workflow revision.'
}

$expectedWorkflowRef = "$($policy.hostedSigning.workflow.Substring('https://github.com/'.Length))" +
    "@$($env:GITHUB_REF)"
if ($env:GITHUB_WORKFLOW_REF -ne $expectedWorkflowRef -or
    $evidence.source.repository -ne $policy.sourceRepository -or
    $evidence.source.commit -ne $env:GITHUB_SHA -or
    $evidence.source.ref -ne $env:GITHUB_REF -or
    $evidence.schemaVersion -ne '1.1' -or
    $evidence.signing.mode -ne 'hosted-unsigned-validation') {
    throw 'Evidence is not bound to the exact protected workflow execution.'
}

Assert-ArtifactInventory -Root $bundleRoot -Artifacts $evidence.artifacts
Assert-SigningStatus -Signing $evidence.signing -Policy $policy -Source $evidence.source `
    -AllowUnsignedHostedEvidence

Assert-RequiredCommand -Name 'docker'
& docker image inspect $policy.tools.cosign.image *> $null
if ($LASTEXITCODE -ne 0) {
    throw 'Pinned Cosign image is not preloaded; keyless signing cannot continue.'
}

$artifactMount = "type=bind,source=$bundleRoot,target=/work"
$trustedRootMount = "type=bind,source=$([IO.Path]::GetDirectoryName($trustedRoot)),target=/trust,readonly"
$containerTrustedRoot = "/trust/$([IO.Path]::GetFileName($trustedRoot))"

Invoke-CheckedCommand -FilePath 'docker' -Arguments @(
    'run', '--rm',
    '--env', 'ACTIONS_ID_TOKEN_REQUEST_TOKEN',
    '--env', 'ACTIONS_ID_TOKEN_REQUEST_URL',
    '--mount', $artifactMount,
    '--mount', $trustedRootMount,
    $policy.tools.cosign.image,
    'sign-blob',
    '--yes',
    '--oidc-provider', 'github-actions',
    '--trusted-root', $containerTrustedRoot,
    '--bundle', '/work/provenance.sigstore.json',
    '/work/provenance.json'
) -FailureMessage 'Keyless Sigstore provenance signing failed.'

$bundlePath = Join-Path $bundleRoot 'provenance.sigstore.json'
$trustedRootPath = Join-Path $bundleRoot 'sigstore-trusted-root.json'
Copy-Item -LiteralPath $trustedRoot -Destination $trustedRootPath
Assert-KeylessBundleStructure -BundlePath $bundlePath `
    -ArtifactPath (Join-Path $bundleRoot 'provenance.json') -Policy $policy
Assert-IndependentSigstoreTrustedRoot -EvidenceRootPath $trustedRootPath `
    -TrustedRootPath $trustedRoot -ExpectedEvidenceSha256 (Get-FileSha256 -Path $trustedRootPath)

$evidence.signing.mode = 'keyless-sigstore'
$evidence.signing.status = 'signed'
$evidence.signing.trustedPublicKeySha256 = $null
$evidence.signing.signature = $null
$evidence.signing.bundle = Get-ChecksummedFile -Root $bundleRoot -Name 'provenance.sigstore.json'
$evidence.signing.trustedRoot = Get-ChecksummedFile -Root $bundleRoot -Name 'sigstore-trusted-root.json'
$evidence.signing.certificateIdentity = Get-KeylessCertificateIdentity -Policy $policy `
    -Ref $evidence.source.ref
$evidence.signing.oidcIssuer = $policy.hostedSigning.oidcIssuer
$evidence.signing.repository = $policy.hostedSigning.repository
$evidence.signing.workflow = $policy.hostedSigning.workflow
$evidence.signing.workflowRevision = $evidence.source.commit
$evidence.signing.ref = $evidence.source.ref
$evidence.signing.trigger = $policy.hostedSigning.trigger
$evidence.signing.transparencyLogIncluded = $true
$evidence.signing.certificateTransparencyIncluded = $true
$evidence.signing.hostedDeferral = $null

$artifactNames = @($evidence.artifacts | ForEach-Object { $_.path }) +
    @('provenance.sigstore.json', 'sigstore-trusted-root.json')
$evidence.artifacts = @($artifactNames | ForEach-Object {
    Get-ChecksummedFile -Root $bundleRoot -Name $_
})
$evidence | ConvertTo-Json -Depth 100 |
    Set-Content -LiteralPath $evidencePath -Encoding utf8NoBOM

Assert-ArtifactInventory -Root $bundleRoot -Artifacts $evidence.artifacts
Assert-SigningStatus -Signing $evidence.signing -Policy $policy -Source $evidence.source

$offlineArguments = @(
    'run', '--rm', '--network', 'none',
    '--mount', "type=bind,source=$bundleRoot,target=/work,readonly",
    '--mount', $trustedRootMount,
    $policy.tools.cosign.image
) + (Get-KeylessVerificationArguments -Signing $evidence.signing -Policy $policy `
    -BundlePath '/work/provenance.sigstore.json' `
    -TrustedRootPath $containerTrustedRoot `
    -ArtifactPath '/work/provenance.json')
Invoke-CheckedCommand -FilePath 'docker' -Arguments $offlineArguments `
    -FailureMessage 'Immediate network-blocked keyless verification failed.'

Write-Output "Keyless Sigstore evidence completed for $($evidence.image.immutableReference)."
