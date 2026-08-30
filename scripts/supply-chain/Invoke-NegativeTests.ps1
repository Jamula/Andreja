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
    $unpinnedPolicy = $policy | ConvertTo-Json -Depth 100 | ConvertFrom-Json -Depth 100
    $unpinnedPolicy.tools.syft.image = 'anchore/syft:latest'
    $unpinnedPolicy | ConvertTo-Json -Depth 100 |
        Set-Content -LiteralPath $unpinnedPolicyPath -Encoding utf8NoBOM
    Assert-Throw 'unpinned tool' { Read-SupplyChainPolicy -Path $unpinnedPolicyPath }

    $legacyPolicyPath = Join-Path $testRoot 'legacy-policy.json'
    $legacyPolicy = $policy | ConvertTo-Json -Depth 100 | ConvertFrom-Json -Depth 100
    $legacyPolicy.schemaVersion = '1.0'
    $legacyPolicy.PSObject.Properties.Remove('operatorHeldKey')
    $legacyPolicy.hostedSigning = [pscustomobject]@{
        mode = 'deferred'
        reason = 'Retained policy 1.0 operator evidence remains local-only.'
    }
    $legacyPolicy | ConvertTo-Json -Depth 100 |
        Set-Content -LiteralPath $legacyPolicyPath -Encoding utf8NoBOM
    $legacyPolicy = Read-SupplyChainPolicy -Path $legacyPolicyPath
    Assert-SigningStatus -Signing ([pscustomobject]@{
        mode = 'operator-held-key'
        status = 'signed'
        trustedPublicKeySha256 = ('f' * 64)
        signature = [pscustomobject]@{ path = 'provenance.sig'; sha256 = ('e' * 64) }
        hostedDeferral = $null
    }) -Policy $legacyPolicy -Source ([pscustomobject]@{
        commit = ('a' * 40)
    })

    Assert-Throw 'scanner unavailable' { Assert-RequiredCommand -Name 'andreja-missing-scanner-command' }
    Assert-Throw 'forbidden severity' { Assert-NoForbiddenFinding -Count 1 -Scope 'fixture' }
    Assert-Throw 'unsigned provenance' {
        Assert-SigningStatus -Signing ([pscustomobject]@{
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
            hostedDeferral = 'fixture'
        }) -Policy $policy -Source ([pscustomobject]@{
            commit = ('a' * 40)
            ref = 'refs/heads/main'
        })
    }

    $keylessSource = [pscustomobject]@{
        commit = ('a' * 40)
        ref = 'refs/tags/v1.2.3'
    }
    function Get-KeylessSigningFixture {
        [pscustomobject]@{
            mode = 'keyless-sigstore'
            status = 'signed'
            trustedPublicKeySha256 = $null
            signature = $null
            bundle = [pscustomobject]@{ path = 'provenance.sigstore.json'; sha256 = ('b' * 64) }
            trustedRoot = [pscustomobject]@{ path = 'sigstore-trusted-root.json'; sha256 = ('c' * 64) }
            certificateIdentity = Get-KeylessCertificateIdentity -Policy $policy -Ref $keylessSource.ref
            oidcIssuer = $policy.hostedSigning.oidcIssuer
            repository = $policy.hostedSigning.repository
            workflow = $policy.hostedSigning.workflow
            workflowRevision = $keylessSource.commit
            ref = $keylessSource.ref
            trigger = $policy.hostedSigning.trigger
            transparencyLogIncluded = $true
            certificateTransparencyIncluded = $true
            hostedDeferral = $null
        }
    }

    $keyless = Get-KeylessSigningFixture
    Assert-SigningStatus -Signing $keyless -Policy $policy -Source $keylessSource
    Assert-Throw 'operator evidence for hosted release tag' {
        Assert-SigningStatus -Signing ([pscustomobject]@{
            mode = 'operator-held-key'
            status = 'signed'
            trustedPublicKeySha256 = ('f' * 64)
            signature = [pscustomobject]@{ path = 'provenance.sig'; sha256 = ('e' * 64) }
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
        }) -Policy $policy -Source $keylessSource
    }
    foreach ($case in @(
        @{ Name = 'wrong OIDC issuer'; Property = 'oidcIssuer'; Value = 'https://issuer.invalid' },
        @{ Name = 'wrong repository'; Property = 'repository'; Value = 'Jamula/Other' },
        @{ Name = 'wrong workflow identity'; Property = 'certificateIdentity'; Value = 'https://github.com/Jamula/Andreja/.github/workflows/other.yml@refs/tags/v1.2.3' },
        @{ Name = 'wrong workflow revision'; Property = 'workflowRevision'; Value = ('d' * 40) },
        @{ Name = 'wrong tag ref'; Property = 'ref'; Value = 'refs/tags/v9.9.9' },
        @{ Name = 'wrong workflow trigger'; Property = 'trigger'; Value = 'workflow_dispatch' },
        @{ Name = 'transparency-log bypass'; Property = 'transparencyLogIncluded'; Value = $false },
        @{ Name = 'certificate-log bypass'; Property = 'certificateTransparencyIncluded'; Value = $false },
        @{ Name = 'mixed signing modes'; Property = 'trustedPublicKeySha256'; Value = ('e' * 64) }
    )) {
        $fixture = Get-KeylessSigningFixture
        $fixture.($case.Property) = $case.Value
        Assert-Throw $case.Name {
            Assert-SigningStatus -Signing $fixture -Policy $policy -Source $keylessSource
        }
    }

    $signedArtifact = Join-Path $testRoot 'signed-artifact.json'
    '{"digest":"sha256:fixture"}' | Set-Content -LiteralPath $signedArtifact -Encoding ascii
    $artifactDigest = [Convert]::ToBase64String(
        [Convert]::FromHexString((Get-FileSha256 -Path $signedArtifact))
    )
    function Get-SyntheticBundle {
        [pscustomobject]@{
            mediaType = $policy.hostedSigning.bundleMediaType
            verificationMaterial = [pscustomobject]@{
                certificate = [pscustomobject]@{ rawBytes = [Convert]::ToBase64String([byte[]](1, 2, 3)) }
                tlogEntries = @([pscustomobject]@{
                    logIndex = '1'
                    logId = [pscustomobject]@{ keyId = [Convert]::ToBase64String([byte[]](1)) }
                    kindVersion = [pscustomobject]@{ kind = 'hashedrekord'; version = '0.0.1' }
                    integratedTime = '1'
                    inclusionPromise = [pscustomobject]@{
                        signedEntryTimestamp = [Convert]::ToBase64String([byte[]](1, 2, 3))
                    }
                    inclusionProof = [pscustomobject]@{
                        logIndex = '1'
                        rootHash = [Convert]::ToBase64String([byte[]]::new(32))
                        treeSize = '2'
                        hashes = @()
                        checkpoint = [pscustomobject]@{ envelope = 'synthetic checkpoint' }
                    }
                    canonicalizedBody = [Convert]::ToBase64String([byte[]](1, 2, 3))
                })
            }
            messageSignature = [pscustomobject]@{
                messageDigest = [pscustomobject]@{
                    algorithm = 'SHA2_256'
                    digest = $artifactDigest
                }
                signature = [Convert]::ToBase64String([byte[]](1, 2, 3))
            }
        }
    }

    $bundlePath = Join-Path $testRoot 'synthetic.sigstore.json'
    (Get-SyntheticBundle) | ConvertTo-Json -Depth 100 |
        Set-Content -LiteralPath $bundlePath -Encoding utf8NoBOM
    Assert-KeylessBundleStructure -BundlePath $bundlePath -ArtifactPath $signedArtifact -Policy $policy

    $missingProof = Get-SyntheticBundle
    $missingProof.verificationMaterial.tlogEntries[0].inclusionProof = $null
    $missingProof | ConvertTo-Json -Depth 100 |
        Set-Content -LiteralPath $bundlePath -Encoding utf8NoBOM
    Assert-Throw 'missing inclusion proof' {
        Assert-KeylessBundleStructure -BundlePath $bundlePath -ArtifactPath $signedArtifact -Policy $policy
    }

    $alteredProof = Get-SyntheticBundle
    $alteredProof.verificationMaterial.tlogEntries[0].inclusionProof.rootHash =
        [Convert]::ToBase64String([byte[]](1, 2, 3))
    $alteredProof | ConvertTo-Json -Depth 100 |
        Set-Content -LiteralPath $bundlePath -Encoding utf8NoBOM
    Assert-Throw 'altered inclusion proof' {
        Assert-KeylessBundleStructure -BundlePath $bundlePath -ArtifactPath $signedArtifact -Policy $policy
    }

    $tamperedBundle = Get-SyntheticBundle
    $tamperedBundle.messageSignature.messageDigest.digest =
        [Convert]::ToBase64String([byte[]]::new(32))
    $tamperedBundle | ConvertTo-Json -Depth 100 |
        Set-Content -LiteralPath $bundlePath -Encoding utf8NoBOM
    Assert-Throw 'tampered bundle' {
        Assert-KeylessBundleStructure -BundlePath $bundlePath -ArtifactPath $signedArtifact -Policy $policy
    }

    $untrustedRoot = Join-Path $testRoot 'untrusted-root.json'
    [pscustomobject]@{
        mediaType = 'application/vnd.dev.sigstore.trustedroot+json;version=0.1'
        certificateAuthorities = @()
        tlogs = @()
        ctlogs = @()
    } | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $untrustedRoot -Encoding utf8NoBOM
    Assert-Throw 'untrusted certificate chain' {
        Assert-SigstoreTrustedRootStructure -Path $untrustedRoot
    }

    function Write-SyntheticTrustedRoot {
        param(
            [Parameter(Mandatory)][string] $Path,
            [Parameter(Mandatory)][string] $Marker
        )

        [pscustomobject]@{
            mediaType = 'application/vnd.dev.sigstore.trustedroot+json;version=0.1'
            certificateAuthorities = @([pscustomobject]@{ marker = $Marker })
            tlogs = @([pscustomobject]@{ marker = $Marker })
            ctlogs = @([pscustomobject]@{ marker = $Marker })
        } | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $Path -Encoding utf8NoBOM
    }

    $attackerRoot = Join-Path $testRoot 'evidence-attacker-root.json'
    $independentRoot = Join-Path $testRoot 'independently-trusted-root.json'
    Write-SyntheticTrustedRoot -Path $attackerRoot -Marker 'attacker'
    Write-SyntheticTrustedRoot -Path $independentRoot -Marker 'trusted'
    Assert-Throw 'evidence-supplied attacker root' {
        Assert-IndependentSigstoreTrustedRoot -EvidenceRootPath $attackerRoot `
            -TrustedRootPath $independentRoot `
            -ExpectedEvidenceSha256 (Get-FileSha256 -Path $attackerRoot)
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

    $evidenceSchemaPath = Join-Path $repositoryRoot 'docs\operations\oci-supply-chain-evidence-v1.1.schema.json'
    function Get-MinimalEvidenceFixture {
        param([Parameter(Mandatory)][string] $Ref)

        $hex40 = 'a' * 40
        $hex64 = 'b' * 64
        [pscustomobject]@{
            schemaVersion = '1.1'
            policy = [pscustomobject]@{ path = 'supply-chain-policy.json'; sha256 = $hex64 }
            source = [pscustomobject]@{
                repository = 'https://github.com/Jamula/Andreja'
                commit = $hex40
                tree = $hex40
                ref = $Ref
            }
            image = [pscustomobject]@{
                name = 'andreja-local'
                digest = "sha256:$hex64"
                configDigest = "sha256:$hex64"
                immutableReference = "andreja-local@sha256:$hex64"
                platform = 'linux/amd64'
                archive = [pscustomobject]@{ path = 'archive.tar'; sha256 = $hex64 }
            }
            build = [pscustomobject]@{
                sourceDateEpoch = 1
                firstDigest = "sha256:$hex64"
                secondDigest = "sha256:$hex64"
                reproducible = $true
                baseImages = @("base@sha256:$hex64", "runtime@sha256:$hex64")
            }
            tools = @(1..5 | ForEach-Object { [pscustomobject]@{ name = "tool$_"; version = '1.0.0' } })
            sboms = @(
                [pscustomobject]@{ path = 'sbom.spdx.json'; sha256 = $hex64; format = 'SPDX-2.3' },
                [pscustomobject]@{ path = 'sbom.cyclonedx.json'; sha256 = $hex64; format = 'CycloneDX-1.6' }
            )
            scans = @(
                [pscustomobject]@{ path = 'scan.dependencies.json'; sha256 = $hex64; scope = 'dependencies'; scanner = 'grype'; forbiddenFindings = 0; passed = $true },
                [pscustomobject]@{ path = 'scan.image.json'; sha256 = $hex64; scope = 'final-image'; scanner = 'grype'; forbiddenFindings = 0; passed = $true },
                [pscustomobject]@{ path = 'scan.iac.json'; sha256 = $hex64; scope = 'container-iac'; scanner = 'trivy'; forbiddenFindings = 0; passed = $true }
            )
            migrationNotes = [pscustomobject]@{ path = 'oci-migration-notes.md'; sha256 = $hex64 }
            provenance = [pscustomobject]@{ path = 'provenance.json'; sha256 = $hex64 }
            signing = [pscustomobject]@{
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
                hostedDeferral = 'fixture'
            }
            artifacts = @(1..9 | ForEach-Object { [pscustomobject]@{ path = "artifact$_.bin"; sha256 = $hex64 } })
        }
    }

    foreach ($acceptedPullRef in @('refs/pull/137/merge', 'refs/pull/137/head')) {
        $fixtureJson = Get-MinimalEvidenceFixture -Ref $acceptedPullRef | ConvertTo-Json -Depth 100
        if (-not ($fixtureJson | Test-Json -SchemaFile $evidenceSchemaPath -ErrorAction Stop)) {
            throw "Evidence schema unexpectedly rejected a pull-request source ref accepted by the generator: $acceptedPullRef."
        }
    }
    Write-Output 'PASS (accepted): pull-request source refs accepted by both generator and schema'

    Assert-Throw 'malformed pull-request source ref' {
        $fixtureJson = Get-MinimalEvidenceFixture -Ref 'refs/pull/abc/merge' | ConvertTo-Json -Depth 100
        $fixtureJson | Test-Json -SchemaFile $evidenceSchemaPath -ErrorAction Stop | Out-Null
    }

    if ($passed -ne 36) {
        throw "Expected 36 negative cases, observed $passed."
    }
    Write-Output 'All 36 supply-chain negative cases failed closed as expected.'
} finally {
    Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction SilentlyContinue
}
