[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'SupplyChain.Common.psm1') -Force

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$testRoot = Join-Path $repositoryRoot 'artifacts\supply-chain-negative-tests'
if (Test-Path -LiteralPath $testRoot) {
    Remove-Item -LiteralPath $testRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null

$passed = 0
function Assert-Throw {
    param(
        [Parameter(Mandatory)][string] $Name,
        [Parameter(Mandatory)][scriptblock] $Action
    )

    try {
        & $Action
    } catch {
        $script:passed++
        Write-Output "PASS (rejected): $Name"
        return
    }

    throw "Negative test did not fail closed: $Name"
}

try {
    & (Join-Path $PSScriptRoot 'Test-SupplyChainPolicy.ps1') -RepositoryRoot $repositoryRoot

    $policy = Read-SupplyChainPolicy -Path (Join-Path $repositoryRoot 'supply-chain-policy.json')
    $unpinnedPolicyPath = Join-Path $testRoot 'unpinned-policy.json'
    $policy.tools.syft.image = 'anchore/syft:latest'
    $policy | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $unpinnedPolicyPath -Encoding utf8NoBOM
    Assert-Throw 'unpinned tool' { Read-SupplyChainPolicy -Path $unpinnedPolicyPath }

    Assert-Throw 'scanner unavailable' { Assert-RequiredCommand -Name 'andreja-missing-scanner-command' }
    Assert-Throw 'forbidden severity' { Assert-NoForbiddenFinding -Count 1 -Scope 'fixture' }
    Assert-Throw 'unsigned provenance' {
        Assert-SigningStatus -Signing ([pscustomobject]@{
            mode = 'hosted-deferred'
            status = 'unsigned'
            trustedPublicKeySha256 = $null
            signature = $null
            hostedDeferral = 'fixture'
        })
    }

    function Get-InventoryFixture {
        param([Parameter(Mandatory)][string] $Name)

        $root = Join-Path $testRoot $Name
        New-Item -ItemType Directory -Path $root -Force | Out-Null
        'fixture' | Set-Content -LiteralPath (Join-Path $root 'expected.txt') -Encoding ascii
        '{}' | Set-Content -LiteralPath (Join-Path $root 'evidence.json') -Encoding ascii
        [pscustomobject]@{
            Root = $root
            Artifacts = @([pscustomobject]@{
                path = 'expected.txt'
                sha256 = Get-FileSha256 -Path (Join-Path $root 'expected.txt')
            })
        }
    }

    $inventory = Get-InventoryFixture -Name 'inventory-drift'
    'drift' | Set-Content -LiteralPath (Join-Path $inventory.Root 'unexpected.txt') -Encoding ascii
    Assert-Throw 'inventory drift' {
        Assert-ArtifactInventory -Root $inventory.Root -Artifacts $inventory.Artifacts
    }

    $hidden = Get-InventoryFixture -Name 'inventory-hidden'
    'hidden drift' | Set-Content -LiteralPath (Join-Path $hidden.Root '.hidden') -Encoding ascii
    Assert-Throw 'hidden inventory drift' {
        Assert-ArtifactInventory -Root $hidden.Root -Artifacts $hidden.Artifacts
    }

    $directory = Get-InventoryFixture -Name 'inventory-directory'
    New-Item -ItemType Directory -Path (Join-Path $directory.Root 'nested') | Out-Null
    Assert-Throw 'bundle directory' {
        Assert-ArtifactInventory -Root $directory.Root -Artifacts $directory.Artifacts
    }

    $symlink = Get-InventoryFixture -Name 'inventory-symlink'
    $symlinkPath = Join-Path $symlink.Root $(if ($IsWindows) { 'linked-dir' } else { 'linked.txt' })
    if ($IsWindows) {
        New-Item -ItemType Junction -Path $symlinkPath -Target $symlink.Root | Out-Null
    } else {
        New-Item -ItemType SymbolicLink -Path $symlinkPath `
            -Target (Join-Path $symlink.Root 'expected.txt') | Out-Null
    }
    Assert-Throw 'symlink or reparse entry' {
        Assert-ArtifactInventory -Root $symlink.Root -Artifacts $symlink.Artifacts
    }
    Remove-Item -LiteralPath $symlinkPath -Force

    $rootLinkTarget = Get-InventoryFixture -Name 'inventory-root-target'
    $rootLink = Join-Path $testRoot 'inventory-root-link'
    if ($IsWindows) {
        New-Item -ItemType Junction -Path $rootLink -Target $rootLinkTarget.Root | Out-Null
    } else {
        New-Item -ItemType SymbolicLink -Path $rootLink -Target $rootLinkTarget.Root | Out-Null
    }
    Assert-Throw 'symlink or reparse bundle root' {
        Assert-ArtifactInventory -Root $rootLink -Artifacts $rootLinkTarget.Artifacts
    }
    Remove-Item -LiteralPath $rootLink -Force

    $traversal = Get-InventoryFixture -Name 'inventory-traversal'
    $traversalArtifacts = @([pscustomobject]@{
        path = '../expected.txt'
        sha256 = ('a' * 64)
    })
    Assert-Throw 'artifact path traversal' {
        Assert-ArtifactInventory -Root $traversal.Root -Artifacts $traversalArtifacts
    }

    $badSpdx = Join-Path $testRoot 'bad-spdx.json'
    $badCdx = Join-Path $testRoot 'bad-cdx.json'
    '{}' | Set-Content -LiteralPath $badSpdx -Encoding ascii
    '{}' | Set-Content -LiteralPath $badCdx -Encoding ascii
    Assert-Throw 'invalid SBOM' {
        Assert-SbomDocument -SpdxPath $badSpdx -CycloneDxPath $badCdx
    }

    $evidence = [pscustomobject]@{
        image = [pscustomobject]@{
            name = 'andreja'
            digest = "sha256:$('a' * 64)"
            platform = 'linux/amd64'
        }
        source = [pscustomobject]@{
            repository = 'https://github.com/Jamula/Andreja'
            commit = ('b' * 40)
            tree = ('c' * 40)
        }
    }
    $provenance = [pscustomobject]@{
        _type = 'https://in-toto.io/Statement/v1'
        predicateType = 'https://slsa.dev/provenance/v1'
        subject = @([pscustomobject]@{
            name = 'andreja'
            digest = [pscustomobject]@{ sha256 = ('d' * 64) }
        })
        predicate = [pscustomobject]@{
            buildDefinition = [pscustomobject]@{
                externalParameters = [pscustomobject]@{ platform = 'linux/amd64' }
                resolvedDependencies = @([pscustomobject]@{
                    uri = "$($evidence.source.repository)@git:$($evidence.source.commit)"
                    digest = [pscustomobject]@{ gitTree = $evidence.source.tree }
                })
            }
        }
    }
    Assert-Throw 'digest mismatch' {
        Assert-ProvenanceBinding -Evidence $evidence -Provenance $provenance
    }

    $alternateDockerfile = Join-Path $testRoot 'Dockerfile.alternate'
    Get-Content -LiteralPath (Join-Path $repositoryRoot 'Dockerfile') -Raw |
        Set-Content -LiteralPath $alternateDockerfile -Encoding utf8NoBOM
    "`nFROM runtime-base AS alternate-final`n" |
        Add-Content -LiteralPath $alternateDockerfile -Encoding utf8NoBOM
    Assert-Throw 'alternate/default production target' {
        Assert-DockerfileProductionTarget -Path $alternateDockerfile
    }

    Assert-Throw 'tampered evidence platform' {
        Assert-PlatformApproved -Platform 'linux/s390x' -Policy $policy
    }

    $provenance.subject[0].digest.sha256 = ('a' * 64)
    $provenance.predicate.buildDefinition.externalParameters.platform = 'linux/arm64'
    Assert-Throw 'tampered provenance platform' {
        Assert-ProvenanceBinding -Evidence $evidence -Provenance $provenance
    }

    $manifestDigest = "sha256:$('d' * 64)"
    $configDigest = "sha256:$('e' * 64)"
    $index = [pscustomobject]@{
        schemaVersion = 2
        manifests = @([pscustomobject]@{
            mediaType = 'application/vnd.oci.image.manifest.v1+json'
            digest = $manifestDigest
            platform = [pscustomobject]@{ os = 'linux'; architecture = 'amd64' }
        })
    }
    $manifest = [pscustomobject]@{
        schemaVersion = 2
        mediaType = 'application/vnd.oci.image.manifest.v1+json'
        config = [pscustomobject]@{
            mediaType = 'application/vnd.oci.image.config.v1+json'
            digest = $configDigest
        }
    }
    $config = [pscustomobject]@{ os = 'linux'; architecture = 'amd64' }

    $wrongArch = [pscustomobject]@{ os = 'linux'; architecture = 'arm64' }
    Assert-Throw 'wrong OCI architecture' {
        Assert-OciPlatformBinding -ExpectedPlatform 'linux/amd64' -Index $index `
            -Manifest $manifest -Config $wrongArch -ExpectedManifestDigest $manifestDigest `
            -ExpectedConfigDigest $configDigest
    }

    $wrongOs = [pscustomobject]@{ os = 'windows'; architecture = 'amd64' }
    Assert-Throw 'wrong OCI operating system' {
        Assert-OciPlatformBinding -ExpectedPlatform 'linux/amd64' -Index $index `
            -Manifest $manifest -Config $wrongOs -ExpectedManifestDigest $manifestDigest `
            -ExpectedConfigDigest $configDigest
    }

    $multipleIndex = [pscustomobject]@{
        schemaVersion = 2
        manifests = @($index.manifests[0], $index.manifests[0])
    }
    Assert-Throw 'multi-manifest ambiguity' {
        Assert-OciPlatformBinding -ExpectedPlatform 'linux/amd64' -Index $multipleIndex `
            -Manifest $manifest -Config $config
    }

    $nestedIndex = [pscustomobject]@{
        schemaVersion = 2
        manifests = @([pscustomobject]@{
            mediaType = 'application/vnd.oci.image.index.v1+json'
            digest = $manifestDigest
        })
    }
    Assert-Throw 'nested manifest-list ambiguity' {
        Assert-OciPlatformBinding -ExpectedPlatform 'linux/amd64' -Index $nestedIndex `
            -Manifest $manifest -Config $config
    }

    $wrongDescriptor = [pscustomobject]@{
        schemaVersion = 2
        manifests = @([pscustomobject]@{
            mediaType = 'application/vnd.oci.image.manifest.v1+json'
            digest = $manifestDigest
            platform = [pscustomobject]@{ os = 'linux'; architecture = 'arm64' }
        })
    }
    Assert-Throw 'descriptor platform mismatch' {
        Assert-OciPlatformBinding -ExpectedPlatform 'linux/amd64' -Index $wrongDescriptor `
            -Manifest $manifest -Config $config
    }

    if ($passed -ne 20) {
        throw "Expected 20 negative cases, observed $passed."
    }
    Write-Output 'All 20 supply-chain negative cases failed closed as expected.'
} finally {
    Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction SilentlyContinue
}
