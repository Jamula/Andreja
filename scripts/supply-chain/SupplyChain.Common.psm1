Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-FileSha256 {
    param([Parameter(Mandatory)][string] $Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Required file is missing: $Path"
    }

    $item = Get-Item -LiteralPath $Path -Force
    $isLink = $item.Attributes.HasFlag([IO.FileAttributes]::ReparsePoint) -or
        (($item.PSObject.Properties.Name -contains 'LinkType') -and $null -ne $item.LinkType)
    if ($isLink) {
        throw "Artifact must be a regular file, not a symlink or reparse point: $Path"
    }

    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Assert-PinnedImageReference {
    param(
        [Parameter(Mandatory)][string] $Reference,
        [Parameter(Mandatory)][string] $Name
    )

    if ($Reference -notmatch '^[^@\s]+@sha256:[0-9a-f]{64}$') {
        throw "$Name must be an immutable image reference with a sha256 digest."
    }
}

function Assert-RequiredCommand {
    param([Parameter(Mandatory)][string] $Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required tool '$Name' is unavailable; no fallback is permitted."
    }
}

function Read-SupplyChainPolicy {
    param([Parameter(Mandatory)][string] $Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Supply-chain policy is missing: $Path"
    }

    $policy = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -Depth 100
    if ($policy.schemaVersion -notin @('1.0', '1.1') -or
        $policy.policyId -ne 'andreja-phase-1a-oci-v1') {
        throw 'Unsupported supply-chain policy schema or policy identifier.'
    }

    $severities = @($policy.forbiddenSeverities)
    if ($severities.Count -eq 0 -or $severities -contains 'UNKNOWN') {
        throw 'The forbidden severity policy must be explicit and cannot use UNKNOWN as a waiver.'
    }

    foreach ($severity in $severities) {
        if ($severity -notin @('LOW', 'MEDIUM', 'HIGH', 'CRITICAL')) {
            throw "Unsupported forbidden severity '$severity'."
        }
    }

    foreach ($property in $policy.tools.PSObject.Properties) {
        if ([string]::IsNullOrWhiteSpace([string] $property.Value.version)) {
            throw "Tool '$($property.Name)' has no pinned version."
        }

        Assert-PinnedImageReference -Reference $property.Value.image -Name "Tool '$($property.Name)'"
    }

    foreach ($property in $policy.baseImages.PSObject.Properties) {
        Assert-PinnedImageReference -Reference $property.Value -Name "Base image '$($property.Name)'"
    }

    $platforms = @($policy.platforms)
    if ($platforms.Count -eq 0 -or
        @($platforms | Where-Object { $_ -notin @('linux/amd64', 'linux/arm64') }).Count -gt 0) {
        throw 'Policy platforms must explicitly contain supported Linux OCI platforms.'
    }

    if ($policy.schemaVersion -eq '1.0') {
        if ($policy.hostedSigning.mode -ne 'deferred' -or
            [string]::IsNullOrWhiteSpace([string] $policy.hostedSigning.reason)) {
            throw 'Legacy hosted signing must retain its explicit deferral.'
        }
    } else {
        if (-not $policy.operatorHeldKey.enabled -or
            $policy.operatorHeldKey.scope -ne 'local-operator-evidence-only' -or
            $policy.operatorHeldKey.maySatisfyHostedRelease -ne $false) {
            throw 'Operator-held signing must remain local-only and cannot satisfy hosted release signing.'
        }

        $hosted = $policy.hostedSigning
        if ($hosted.mode -ne 'keyless-sigstore' -or
            $hosted.oidcIssuer -ne 'https://token.actions.githubusercontent.com' -or
            $hosted.repository -ne 'Jamula/Andreja' -or
            $hosted.workflow -ne 'https://github.com/Jamula/Andreja/.github/workflows/oci-supply-chain.yml' -or
            $hosted.workflowName -ne 'OCI Supply Chain' -or
            $hosted.trigger -ne 'push' -or
            $hosted.bundleMediaType -ne 'application/vnd.dev.sigstore.bundle.v0.3+json' -or
            $hosted.trustedRootSource -ne
                'Sigstore TUF trusted root authenticated and pre-positioned outside the evidence bundle before signing' -or
            -not $hosted.requireTransparencyLog -or
            -not $hosted.requireCertificateTransparency) {
            throw 'Hosted signing policy must exact-match the approved GitHub Actions Sigstore boundary.'
        }

        try {
            $null = [regex]::new($hosted.allowedRefPattern)
        } catch {
            throw 'Hosted signing allowedRefPattern is invalid.'
        }
        if ('refs/tags/v1.2.3' -notmatch $hosted.allowedRefPattern -or
            'refs/heads/main' -match $hosted.allowedRefPattern) {
            throw 'Hosted signing must allow version tags and reject branch refs.'
        }
    }

    $policy
}

function Assert-PlatformApproved {
    param(
        [Parameter(Mandatory)][string] $Platform,
        [Parameter(Mandatory)] $Policy
    )

    if ($Platform -notmatch '^linux/(amd64|arm64)$' -or $Platform -notin @($Policy.platforms)) {
        throw "Platform '$Platform' is outside the supply-chain policy."
    }
}

function Assert-DockerfileProductionTarget {
    param([Parameter(Mandatory)][string] $Path)

    $dockerfile = Get-Content -LiteralPath $Path -Raw
    $stageMatches = [regex]::Matches(
        $dockerfile,
        '(?im)^FROM\s+\S+\s+AS\s+([A-Za-z0-9._-]+)\s*$')
    $stages = @($stageMatches | ForEach-Object { $_.Groups[1].Value })
    if (($stages -join ',') -ne 'build,runtime-base,final') {
        throw "Dockerfile must contain only the audited build, runtime-base, and final stages in that order."
    }

    if ($stages[-1] -ne 'final' -or
        ([regex]::Matches($dockerfile, '(?im)^COPY\s+--from=build\b.*?/app/publish\s+\.\s*$')).Count -ne 1 -or
        ([regex]::Matches($dockerfile, '(?im)^COPY\s+--from=build\b.*?/app/state\s+/var/lib/andreja\s*$')).Count -ne 1) {
        throw 'The audited final stage must be the sole/default production image target.'
    }

    if (($dockerfile | Select-String -Pattern 'dotnet restore' -AllMatches).Matches.Count -ne 1 -or
        -not $dockerfile.Contains('from=nuget-cache') -or
        -not $dockerfile.Contains('target=/nuget-cache,readonly')) {
        throw 'The sole build stage must use the explicit read-only NuGet cache context.'
    }
}

function Assert-OciPlatformBinding {
    param(
        [Parameter(Mandatory)][string] $ExpectedPlatform,
        [Parameter(Mandatory)] $Index,
        [Parameter(Mandatory)] $Manifest,
        [Parameter(Mandatory)] $Config,
        [string] $ExpectedManifestDigest,
        [string] $ExpectedConfigDigest
    )

    $parts = $ExpectedPlatform.Split('/', 2)
    if ($parts.Count -ne 2) {
        throw "Invalid expected OCI platform '$ExpectedPlatform'."
    }

    $descriptors = @($Index.manifests)
    if ($Index.schemaVersion -ne 2 -or $descriptors.Count -ne 1) {
        throw 'OCI archive must resolve unambiguously to exactly one platform manifest.'
    }

    $descriptor = $descriptors[0]
    if ($descriptor.mediaType -ne 'application/vnd.oci.image.manifest.v1+json' -or
        $Manifest.schemaVersion -ne 2 -or
        $Manifest.mediaType -ne 'application/vnd.oci.image.manifest.v1+json' -or
        $Manifest.config.mediaType -ne 'application/vnd.oci.image.config.v1+json') {
        throw 'OCI archive must contain a direct image manifest, not a nested index or manifest list.'
    }

    if ($ExpectedManifestDigest -and $descriptor.digest -ne $ExpectedManifestDigest) {
        throw 'OCI manifest digest does not match the expected evidence digest.'
    }
    if ($ExpectedConfigDigest -and $Manifest.config.digest -ne $ExpectedConfigDigest) {
        throw 'OCI config digest does not match the expected evidence digest.'
    }

    if ($Config.os -ne $parts[0] -or $Config.architecture -ne $parts[1]) {
        throw "OCI config platform '$($Config.os)/$($Config.architecture)' does not match '$ExpectedPlatform'."
    }

    if ($descriptor.PSObject.Properties.Name -contains 'platform') {
        if ($descriptor.platform.os -ne $parts[0] -or
            $descriptor.platform.architecture -ne $parts[1]) {
            throw 'OCI descriptor platform does not match its config and expected evidence platform.'
        }
    }
}

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory)][string] $FilePath,
        [Parameter(Mandatory)][string[]] $Arguments,
        [string] $FailureMessage = 'External command failed.',
        [switch] $CaptureOutput
    )

    if ($CaptureOutput) {
        $output = & $FilePath @Arguments 2>&1
        $exitCode = $LASTEXITCODE
        if ($exitCode -ne 0) {
            throw "$FailureMessage Exit code: $exitCode."
        }

        return ($output -join [Environment]::NewLine).Trim()
    }

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FailureMessage Exit code: $LASTEXITCODE."
    }
}

function Get-ChecksummedFile {
    param(
        [Parameter(Mandatory)][string] $Root,
        [Parameter(Mandatory)][string] $Name
    )

    if ($Name -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$') {
        throw "Unsafe artifact name: $Name"
    }

    [ordered]@{
        path = $Name
        sha256 = Get-FileSha256 -Path (Join-Path $Root $Name)
    }
}

function Assert-ArtifactInventory {
    param(
        [Parameter(Mandatory)][string] $Root,
        [Parameter(Mandatory)][object[]] $Artifacts
    )

    $expected = @('evidence.json') + @($Artifacts | ForEach-Object { [string] $_.path })
    if ($expected.Count -ne (@($expected | Sort-Object -Unique)).Count) {
        throw 'Evidence artifact inventory contains duplicate paths.'
    }

    foreach ($name in $expected) {
        if ($name -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$') {
            throw "Evidence artifact path is unsafe: $name"
        }
    }

    $rootItem = Get-Item -LiteralPath $Root -Force
    $rootIsLink = $rootItem.Attributes.HasFlag([IO.FileAttributes]::ReparsePoint) -or
        (($rootItem.PSObject.Properties.Name -contains 'LinkType') -and $null -ne $rootItem.LinkType)
    if ($rootIsLink) {
        throw 'Evidence bundle root cannot be a symlink or reparse point.'
    }

    $entries = @(Get-ChildItem -LiteralPath $Root -Force)
    foreach ($entry in $entries) {
        $isLink = $entry.Attributes.HasFlag([IO.FileAttributes]::ReparsePoint) -or
            (($entry.PSObject.Properties.Name -contains 'LinkType') -and $null -ne $entry.LinkType)
        if ($isLink) {
            throw "Evidence bundle contains a symlink or reparse point: $($entry.Name)."
        }
        if ($entry.PSIsContainer) {
            throw "Evidence bundle schema permits no directories: $($entry.Name)."
        }
    }

    $actual = @(Get-ChildItem -LiteralPath $Root -Force -File |
        Select-Object -ExpandProperty Name)
    $difference = @(Compare-Object ($expected | Sort-Object) ($actual | Sort-Object))
    if ($difference.Count -gt 0) {
        throw "Evidence inventory drift detected: $($difference.InputObject -join ', ')."
    }

    foreach ($artifact in $Artifacts) {
        $actualHash = Get-FileSha256 -Path (Join-Path $Root $artifact.path)
        if ($actualHash -ne $artifact.sha256) {
            throw "Artifact checksum mismatch: $($artifact.path)."
        }
    }
}

function Assert-SbomDocument {
    param(
        [Parameter(Mandatory)][string] $SpdxPath,
        [Parameter(Mandatory)][string] $CycloneDxPath
    )

    $spdx = Get-Content -LiteralPath $SpdxPath -Raw | ConvertFrom-Json -Depth 100
    if ($spdx.spdxVersion -ne 'SPDX-2.3' -or @($spdx.packages).Count -eq 0) {
        throw 'SPDX SBOM is invalid or contains no package inventory.'
    }

    $cycloneDx = Get-Content -LiteralPath $CycloneDxPath -Raw | ConvertFrom-Json -Depth 100
    if ($cycloneDx.bomFormat -ne 'CycloneDX' -or -not $cycloneDx.specVersion.StartsWith('1.') -or
        @($cycloneDx.components).Count -eq 0) {
        throw 'CycloneDX SBOM is invalid or contains no component inventory.'
    }
}

function Get-GrypeForbiddenCount {
    param(
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)][string[]] $ForbiddenSeverities
    )

    $report = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -Depth 100
    if (-not $report.descriptor -or -not $report.source -or $null -eq $report.matches) {
        throw "Grype report is invalid: $Path"
    }

    @($report.matches | Where-Object {
        $_.vulnerability.severity.ToUpperInvariant() -in $ForbiddenSeverities
    }).Count
}

function Get-TrivyForbiddenCount {
    param(
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)][string[]] $ForbiddenSeverities
    )

    $report = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -Depth 100
    if ($null -eq $report.Results) {
        throw "Trivy report is invalid: $Path"
    }

    @($report.Results | ForEach-Object {
        $findings = @()
        if ($_.PSObject.Properties.Name -contains 'Misconfigurations') {
            $findings += @($_.Misconfigurations)
        }
        if ($_.PSObject.Properties.Name -contains 'Vulnerabilities') {
            $findings += @($_.Vulnerabilities)
        }
        $findings
    } | Where-Object {
        $_ -and $_.Severity.ToUpperInvariant() -in $ForbiddenSeverities
    }).Count
}

function Assert-NoForbiddenFinding {
    param(
        [Parameter(Mandatory)][int] $Count,
        [Parameter(Mandatory)][string] $Scope
    )

    if ($Count -gt 0) {
        throw "$Scope scan contains $Count forbidden finding(s)."
    }
}

function Assert-ProvenanceBinding {
    param(
        [Parameter(Mandatory)] $Evidence,
        [Parameter(Mandatory)] $Provenance
    )

    if ($Provenance._type -ne 'https://in-toto.io/Statement/v1' -or
        $Provenance.predicateType -ne 'https://slsa.dev/provenance/v1') {
        throw 'Provenance statement type is unsupported.'
    }

    $subject = @($Provenance.subject)
    if ($subject.Count -ne 1 -or
        "sha256:$($subject[0].digest.sha256)" -ne $Evidence.image.digest -or
        $subject[0].name -ne $Evidence.image.name) {
        throw 'Provenance image digest does not match the evidence manifest.'
    }

    $source = @($Provenance.predicate.buildDefinition.resolvedDependencies |
        Where-Object { $_.uri -eq "$($Evidence.source.repository)@git:$($Evidence.source.commit)" })
    if ($source.Count -ne 1 -or $source[0].digest.gitTree -ne $Evidence.source.tree) {
        throw 'Provenance source commit/tree binding is missing or mismatched.'
    }

    if ($Provenance.predicate.buildDefinition.externalParameters.platform -ne
        $Evidence.image.platform) {
        throw 'Provenance platform does not match the evidence image platform.'
    }
}

function Get-KeylessCertificateIdentity {
    param(
        [Parameter(Mandatory)] $Policy,
        [Parameter(Mandatory)][string] $Ref
    )

    "$($Policy.hostedSigning.workflow)@$Ref"
}

function Assert-KeylessBundleStructure {
    param(
        [Parameter(Mandatory)][string] $BundlePath,
        [Parameter(Mandatory)][string] $ArtifactPath,
        [Parameter(Mandatory)] $Policy
    )

    $bundle = Get-Content -LiteralPath $BundlePath -Raw | ConvertFrom-Json -Depth 100
    if ($bundle.mediaType -ne $Policy.hostedSigning.bundleMediaType) {
        throw 'Sigstore bundle media type is unsupported.'
    }

    if (-not $bundle.verificationMaterial.certificate.rawBytes) {
        throw 'Sigstore bundle is missing the Fulcio certificate.'
    }
    try {
        $null = [Convert]::FromBase64String([string] $bundle.verificationMaterial.certificate.rawBytes)
    } catch {
        throw 'Sigstore bundle contains an invalid Fulcio certificate.'
    }

    $entries = @($bundle.verificationMaterial.tlogEntries)
    if ($entries.Count -ne 1) {
        throw 'Sigstore bundle must contain exactly one transparency-log entry.'
    }
    $entry = $entries[0]
    if (-not $entry.inclusionPromise.signedEntryTimestamp -or
        -not $entry.inclusionProof.rootHash -or
        -not $entry.inclusionProof.treeSize -or
        -not $entry.inclusionProof.checkpoint.envelope -or
        -not $entry.canonicalizedBody) {
        throw 'Sigstore bundle is missing its transparency-log inclusion promise or proof.'
    }

    try {
        $logIndex = [long] $entry.logIndex
        $proofLogIndex = [long] $entry.inclusionProof.logIndex
        $treeSize = [long] $entry.inclusionProof.treeSize
        $rootHash = [Convert]::FromBase64String([string] $entry.inclusionProof.rootHash)
        if ($logIndex -ne $proofLogIndex -or $treeSize -le $logIndex -or $rootHash.Length -ne 32) {
            throw 'invalid proof'
        }
        foreach ($hash in @($entry.inclusionProof.hashes)) {
            if ([Convert]::FromBase64String([string] $hash).Length -ne 32) {
                throw 'invalid proof hash'
            }
        }
    } catch {
        throw 'Sigstore bundle contains an invalid transparency-log inclusion proof.'
    }

    $messageSignature = $bundle.messageSignature
    if ($messageSignature.messageDigest.algorithm -ne 'SHA2_256' -or
        -not $messageSignature.messageDigest.digest -or
        -not $messageSignature.signature) {
        throw 'Sigstore bundle is missing the required SHA-256 message signature.'
    }
    try {
        $bundleDigest = [Convert]::ToHexString(
            [Convert]::FromBase64String([string] $messageSignature.messageDigest.digest)
        ).ToLowerInvariant()
        $null = [Convert]::FromBase64String([string] $messageSignature.signature)
        $null = [Convert]::FromBase64String([string] $entry.inclusionPromise.signedEntryTimestamp)
        $null = [Convert]::FromBase64String([string] $entry.canonicalizedBody)
    } catch {
        throw 'Sigstore bundle contains invalid base64 verification material.'
    }

    if ($bundleDigest -ne (Get-FileSha256 -Path $ArtifactPath)) {
        throw 'Sigstore bundle message digest does not match the signed artifact.'
    }
}

function Assert-SigstoreTrustedRootStructure {
    param([Parameter(Mandatory)][string] $Path)

    $trustedRoot = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -Depth 100
    if ($trustedRoot.mediaType -ne 'application/vnd.dev.sigstore.trustedroot+json;version=0.1' -or
        @($trustedRoot.certificateAuthorities).Count -eq 0 -or
        @($trustedRoot.tlogs).Count -eq 0 -or
        @($trustedRoot.ctlogs).Count -eq 0) {
        throw 'Sigstore trusted root lacks required Fulcio, Rekor, or certificate-log trust material.'
    }
}

function Assert-IndependentSigstoreTrustedRoot {
    param(
        [Parameter(Mandatory)][string] $EvidenceRootPath,
        [Parameter(Mandatory)][string] $TrustedRootPath,
        [Parameter(Mandatory)][string] $ExpectedEvidenceSha256
    )

    $evidenceRoot = (Resolve-Path $EvidenceRootPath).Path
    $trustedRoot = (Resolve-Path $TrustedRootPath).Path
    if ([IO.Path]::GetFullPath($evidenceRoot) -eq [IO.Path]::GetFullPath($trustedRoot)) {
        throw 'The independently trusted Sigstore root must be outside the evidence bundle.'
    }

    Assert-SigstoreTrustedRootStructure -Path $evidenceRoot
    Assert-SigstoreTrustedRootStructure -Path $trustedRoot
    $evidenceHash = Get-FileSha256 -Path $evidenceRoot
    $trustedHash = Get-FileSha256 -Path $trustedRoot
    if ($evidenceHash -ne $ExpectedEvidenceSha256 -or $trustedHash -ne $evidenceHash) {
        throw 'The retained Sigstore root does not match the independently trusted root.'
    }
}

function Get-KeylessVerificationArguments {
    param(
        [Parameter(Mandatory)] $Signing,
        [Parameter(Mandatory)] $Policy,
        [Parameter(Mandatory)][string] $BundlePath,
        [Parameter(Mandatory)][string] $TrustedRootPath,
        [Parameter(Mandatory)][string] $ArtifactPath
    )

    @(
        'verify-blob',
        '--bundle', $BundlePath,
        '--trusted-root', $TrustedRootPath,
        '--certificate-identity', $Signing.certificateIdentity,
        '--certificate-oidc-issuer', $Signing.oidcIssuer,
        '--certificate-github-workflow-repository', $Signing.repository,
        '--certificate-github-workflow-name', $Policy.hostedSigning.workflowName,
        '--certificate-github-workflow-ref', $Signing.ref,
        '--certificate-github-workflow-sha', $Signing.workflowRevision,
        '--certificate-github-workflow-trigger', $Signing.trigger,
        $ArtifactPath
    )
}

function Assert-SigningStatus {
    param(
        [Parameter(Mandatory)] $Signing,
        [Parameter(Mandatory)] $Policy,
        [Parameter(Mandatory)] $Source,
        [switch] $AllowUnsignedHostedEvidence
    )

    $signingProperties = @($Signing.PSObject.Properties.Name)
    $getSigningValue = {
        param([string] $Name)
        if ($signingProperties -contains $Name) {
            return $Signing.PSObject.Properties[$Name].Value
        }
        return $null
    }
    $sourceRef = if (@($Source.PSObject.Properties.Name) -contains 'ref') {
        $Source.ref
    } else {
        $null
    }

    if ($Signing.mode -eq 'operator-held-key') {
        $hostedTag = $Policy.schemaVersion -eq '1.1' -and
            -not [string]::IsNullOrWhiteSpace([string] $sourceRef) -and
            $sourceRef -match $Policy.hostedSigning.allowedRefPattern
        if ($Signing.status -ne 'signed' -or
            [string]::IsNullOrWhiteSpace($Signing.trustedPublicKeySha256) -or
            -not $Signing.signature -or $hostedTag -or
            (& $getSigningValue 'bundle') -or (& $getSigningValue 'trustedRoot') -or
            (& $getSigningValue 'certificateIdentity') -or
            (& $getSigningValue 'oidcIssuer') -or (& $getSigningValue 'repository') -or
            (& $getSigningValue 'workflow') -or (& $getSigningValue 'workflowRevision') -or
            (& $getSigningValue 'ref') -or (& $getSigningValue 'trigger') -or
            $null -ne (& $getSigningValue 'transparencyLogIncluded') -or
            $null -ne (& $getSigningValue 'certificateTransparencyIncluded') -or
            (& $getSigningValue 'hostedDeferral')) {
            throw 'Signed evidence lacks the required operator trust metadata.'
        }

        return
    }

    if ($Signing.mode -eq 'keyless-sigstore') {
        if ($Policy.schemaVersion -ne '1.1') {
            throw 'Keyless evidence requires supply-chain policy schema 1.1.'
        }
        $expectedIdentity = Get-KeylessCertificateIdentity -Policy $Policy -Ref $Source.ref
        if ($Signing.status -ne 'signed' -or $Signing.trustedPublicKeySha256 -or
            $Signing.signature -or -not $Signing.bundle -or -not $Signing.trustedRoot -or
            $Signing.certificateIdentity -ne $expectedIdentity -or
            $Signing.oidcIssuer -ne $Policy.hostedSigning.oidcIssuer -or
            $Signing.repository -ne $Policy.hostedSigning.repository -or
            $Signing.workflow -ne $Policy.hostedSigning.workflow -or
            $Signing.workflowRevision -ne $Source.commit -or
            $Signing.ref -ne $Source.ref -or
            $Signing.ref -notmatch $Policy.hostedSigning.allowedRefPattern -or
            $Signing.trigger -ne $Policy.hostedSigning.trigger -or
            $Signing.transparencyLogIncluded -ne $true -or
            $Signing.certificateTransparencyIncluded -ne $true -or
            $Signing.hostedDeferral) {
            throw 'Keyless evidence does not exact-match the approved GitHub OIDC signing policy.'
        }

        return
    }

    if ($Signing.mode -ne 'hosted-unsigned-validation' -or -not $AllowUnsignedHostedEvidence) {
        throw 'Unsigned provenance is untrusted and blocks offline startup/update.'
    }

    if ($env:GITHUB_ACTIONS -ne 'true' -or
        $Signing.status -ne 'unsigned' -or
        $Signing.trustedPublicKeySha256 -or $Signing.signature -or
        $Signing.bundle -or $Signing.trustedRoot -or $Signing.certificateIdentity -or
        $Signing.oidcIssuer -or $Signing.repository -or $Signing.workflow -or
        $Signing.workflowRevision -or $Signing.ref -or $Signing.trigger -or
        $null -ne $Signing.transparencyLogIncluded -or
        $null -ne $Signing.certificateTransparencyIncluded -or
        [string]::IsNullOrWhiteSpace($Signing.hostedDeferral)) {
        throw 'Unsigned evidence is allowed only for the explicit hosted validation deferral.'
    }
}

Export-ModuleMember -Function @(
    'Assert-ArtifactInventory',
    'Assert-DockerfileProductionTarget',
    'Assert-KeylessBundleStructure',
    'Assert-NoForbiddenFinding',
    'Assert-OciPlatformBinding',
    'Assert-PinnedImageReference',
    'Assert-PlatformApproved',
    'Assert-ProvenanceBinding',
    'Assert-RequiredCommand',
    'Assert-SbomDocument',
    'Assert-SigningStatus',
    'Assert-IndependentSigstoreTrustedRoot',
    'Assert-SigstoreTrustedRootStructure',
    'Get-KeylessCertificateIdentity',
    'Get-KeylessVerificationArguments',
    'Get-ChecksummedFile',
    'Get-FileSha256',
    'Get-GrypeForbiddenCount',
    'Get-TrivyForbiddenCount',
    'Invoke-CheckedCommand',
    'Read-SupplyChainPolicy'
)
