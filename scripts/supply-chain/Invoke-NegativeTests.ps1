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

    $inventoryRoot = Join-Path $testRoot 'inventory'
    New-Item -ItemType Directory -Path $inventoryRoot -Force | Out-Null
    'fixture' | Set-Content -LiteralPath (Join-Path $inventoryRoot 'expected.txt') -Encoding ascii
    '{}' | Set-Content -LiteralPath (Join-Path $inventoryRoot 'evidence.json') -Encoding ascii
    'drift' | Set-Content -LiteralPath (Join-Path $inventoryRoot 'unexpected.txt') -Encoding ascii
    $inventory = @([pscustomobject]@{
        path = 'expected.txt'
        sha256 = Get-FileSha256 -Path (Join-Path $inventoryRoot 'expected.txt')
    })
    Assert-Throw 'inventory drift' {
        Assert-ArtifactInventory -Root $inventoryRoot -Artifacts $inventory
    }

    $badSpdx = Join-Path $testRoot 'bad-spdx.json'
    $badCdx = Join-Path $testRoot 'bad-cdx.json'
    '{}' | Set-Content -LiteralPath $badSpdx -Encoding ascii
    '{}' | Set-Content -LiteralPath $badCdx -Encoding ascii
    Assert-Throw 'invalid SBOM' {
        Assert-SbomDocument -SpdxPath $badSpdx -CycloneDxPath $badCdx
    }

    $evidence = [pscustomobject]@{
        image = [pscustomobject]@{ name = 'andreja'; digest = "sha256:$('a' * 64)" }
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

    if ($passed -ne 7) {
        throw "Expected 7 negative cases, observed $passed."
    }
    Write-Output 'All 7 supply-chain negative cases failed closed as expected.'
} finally {
    Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction SilentlyContinue
}
