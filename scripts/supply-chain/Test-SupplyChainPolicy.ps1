[CmdletBinding()]
param(
    [string] $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'SupplyChain.Common.psm1') -Force

$policyPath = Join-Path $RepositoryRoot 'supply-chain-policy.json'
$policy = Read-SupplyChainPolicy -Path $policyPath
$legacyEvidenceSchema = Get-Content -LiteralPath (
    Join-Path $RepositoryRoot 'docs\operations\oci-supply-chain-evidence-v1.schema.json'
) -Raw | ConvertFrom-Json -Depth 100
$keylessEvidenceSchema = Get-Content -LiteralPath (
    Join-Path $RepositoryRoot 'docs\operations\oci-supply-chain-evidence-v1.1.schema.json'
) -Raw | ConvertFrom-Json -Depth 100
if ($legacyEvidenceSchema.properties.schemaVersion.const -ne '1.0' -or
    @($legacyEvidenceSchema.properties.signing.required) -contains 'bundle' -or
    $keylessEvidenceSchema.properties.schemaVersion.const -ne '1.1' -or
    @($keylessEvidenceSchema.properties.signing.required) -notcontains 'trustedRoot' -or
    @($keylessEvidenceSchema.properties.source.required) -notcontains 'ref') {
    throw 'Evidence schemas must preserve v1 operator compatibility and require keyless fields only in v1.1.'
}
$dockerfilePath = Join-Path $RepositoryRoot 'Dockerfile'
$dockerfile = Get-Content -LiteralPath $dockerfilePath -Raw
Assert-DockerfileProductionTarget -Path $dockerfilePath

foreach ($baseImage in $policy.baseImages.PSObject.Properties.Value) {
    if (-not $dockerfile.Contains($baseImage)) {
        throw "Dockerfile does not contain policy base-image pin '$baseImage'."
    }
}

$workflowRoot = Join-Path $RepositoryRoot '.github\workflows'
$workflowFiles = @(Get-ChildItem -LiteralPath $workflowRoot -File |
    Where-Object { $_.Extension -in @('.yml', '.yaml') })
foreach ($workflow in $workflowFiles) {
    $lineNumber = 0
    foreach ($line in Get-Content -LiteralPath $workflow.FullName) {
        $lineNumber++
        if ($line -match '^\s*uses:\s*(\S+)') {
            $reference = $Matches[1]
            if ($reference -notmatch '^[^@\s]+@[0-9a-fA-F]{40}$') {
                throw "Unpinned action in $($workflow.Name):$lineNumber ($reference)."
            }
        }
    }
}

$supplyWorkflowPath = Join-Path $workflowRoot 'oci-supply-chain.yml'
if (-not (Test-Path -LiteralPath $supplyWorkflowPath)) {
    throw 'Hosted OCI supply-chain workflow is missing.'
}

$supplyWorkflow = Get-Content -LiteralPath $supplyWorkflowPath -Raw
foreach ($requiredText in @(
    'pull_request:',
    'merge_group:',
    'tags:',
    'contents: read',
    'persist-credentials: false',
    'sign-keyless:',
    'id-token: write',
    'git merge-base --is-ancestor',
    'docker pull $cosign',
    'trusted-root create --with-default-services',
    'Complete-KeylessOciEvidence.ps1',
    'Test-OciEvidence.ps1',
    'TrustedPolicyPath',
    'TrustedRootPath',
    'ExpectedSigningMode keyless-sigstore'
)) {
    if (-not $supplyWorkflow.Contains($requiredText)) {
        throw "Hosted OCI workflow is missing '$requiredText'."
    }
}

if (([regex]::Matches($supplyWorkflow, '(?m)^\s*id-token:\s*write\s*$')).Count -ne 1) {
    throw 'Hosted OCI workflow must grant id-token: write exactly once.'
}
foreach ($forbiddenText in @('packages: write', 'docker push', 'cosign sign ')) {
    if ($supplyWorkflow.Contains($forbiddenText)) {
        throw "Hosted OCI workflow contains forbidden publication/signing text '$forbiddenText'."
    }

    $generator = Get-Content -LiteralPath (Join-Path $RepositoryRoot 'scripts\supply-chain\New-OciEvidence.ps1') -Raw
    if (-not $generator.Contains("'--target', 'final'") -or $generator.Contains("'--target', 'supply-final'")) {
        throw 'Supply-chain generation must build the sole/default final target.'
    }
}

$completionScript = Get-Content -LiteralPath (
    Join-Path $RepositoryRoot 'scripts\supply-chain\Complete-KeylessOciEvidence.ps1'
) -Raw
foreach ($requiredText in @(
    "'--oidc-provider', 'github-actions'",
    "'--bundle', '/work/provenance.sigstore.json'",
    "'--trusted-root', `$containerTrustedRoot",
    "'--network', 'none'",
    'ACTIONS_ID_TOKEN_REQUEST_TOKEN',
    'GITHUB_WORKFLOW_REF',
    'GITHUB_WORKFLOW_SHA'
)) {
    if (-not $completionScript.Contains($requiredText)) {
        throw "Keyless completion script is missing '$requiredText'."
    }
}

$verifier = Get-Content -LiteralPath (
    Join-Path $RepositoryRoot 'scripts\supply-chain\Test-OciEvidence.ps1'
) -Raw
foreach ($requiredText in @(
    'Assert-IndependentSigstoreTrustedRoot',
    'target=/trust,readonly',
    '-TrustedRootPath "/trust/'
)) {
    if (-not $verifier.Contains($requiredText)) {
        throw "Offline verifier is missing independent-root control '$requiredText'."
    }
}
if ($verifier.Contains('-TrustedRootPath "/work/')) {
    throw 'Offline verifier must never pass an evidence-bundled root to Cosign.'
}

$generator = Get-Content -LiteralPath (
    Join-Path $RepositoryRoot 'scripts\supply-chain\New-OciEvidence.ps1'
) -Raw
foreach ($requiredText in @(
    "'--new-bundle-format=false'",
    "'--use-signing-config=false'"
)) {
    if (-not $generator.Contains($requiredText)) {
        throw "Operator-held Cosign signing is missing compatibility flag '$requiredText'."
    }
}
foreach ($forbiddenText in @(
    '--tlog-upload=false',
    '--insecure-ignore-tlog',
    '--insecure-ignore-sct',
    '--certificate-identity-regexp',
    '--certificate-oidc-issuer-regexp',
    'SIGSTORE_ID_TOKEN'
)) {
    if ($completionScript.Contains($forbiddenText)) {
        throw "Keyless completion script contains forbidden bypass '$forbiddenText'."
    }
}

$composeFiles = @(
    (Join-Path $RepositoryRoot 'compose.yaml')
) + @(Get-ChildItem -LiteralPath (Join-Path $RepositoryRoot 'deploy') -File |
    Where-Object { $_.Extension -in @('.yml', '.yaml') } |
    Select-Object -ExpandProperty FullName)

foreach ($composeFile in $composeFiles) {
    $lineNumber = 0
    foreach ($line in Get-Content -LiteralPath $composeFile) {
        $lineNumber++
        if ($line -notmatch '^\s*image:\s*(.+?)\s*$') {
            continue
        }

        $reference = $Matches[1].Trim("'`"")
        if ($reference -match '^\$\{[^:}]+:\?') {
            continue
        }

        if ($reference -match '^\$\{[^:}]+:-(.+)\}$') {
            $reference = $Matches[1]
        }

        Assert-PinnedImageReference -Reference $reference -Name "$composeFile`:$lineNumber"
    }
}

Write-Output "Supply-chain policy, base images, action pins, and Compose image pins are valid."
