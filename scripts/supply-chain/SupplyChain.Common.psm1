Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-FileSha256 {
    param([Parameter(Mandatory)][string] $Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Required file is missing: $Path"
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
    if ($policy.schemaVersion -ne '1.0' -or $policy.policyId -ne 'andreja-phase-1a-oci-v1') {
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

    if ($policy.hostedSigning.mode -ne 'deferred' -or [string]::IsNullOrWhiteSpace($policy.hostedSigning.reason)) {
        throw 'Hosted signing must record the approved explicit deferral.'
    }

    $policy
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

    $actual = @(Get-ChildItem -LiteralPath $Root -File | Select-Object -ExpandProperty Name)
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
}

function Assert-SigningStatus {
    param(
        [Parameter(Mandatory)] $Signing,
        [switch] $AllowUnsignedHostedEvidence
    )

    if ($Signing.status -eq 'signed') {
        if ($Signing.mode -ne 'operator-held-key' -or
            [string]::IsNullOrWhiteSpace($Signing.trustedPublicKeySha256) -or
            -not $Signing.signature) {
            throw 'Signed evidence lacks the required operator trust metadata.'
        }

        return
    }

    if (-not $AllowUnsignedHostedEvidence) {
        throw 'Unsigned provenance is untrusted and blocks offline startup/update.'
    }

    if ($env:GITHUB_ACTIONS -ne 'true' -or
        $Signing.mode -ne 'hosted-deferred' -or
        [string]::IsNullOrWhiteSpace($Signing.hostedDeferral)) {
        throw 'Unsigned evidence is allowed only for the explicit hosted validation deferral.'
    }
}

Export-ModuleMember -Function @(
    'Assert-ArtifactInventory',
    'Assert-NoForbiddenFinding',
    'Assert-PinnedImageReference',
    'Assert-ProvenanceBinding',
    'Assert-RequiredCommand',
    'Assert-SbomDocument',
    'Assert-SigningStatus',
    'Get-ChecksummedFile',
    'Get-FileSha256',
    'Get-GrypeForbiddenCount',
    'Get-TrivyForbiddenCount',
    'Invoke-CheckedCommand',
    'Read-SupplyChainPolicy'
)
