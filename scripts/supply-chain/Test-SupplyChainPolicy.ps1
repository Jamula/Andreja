[CmdletBinding()]
param(
    [string] $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'SupplyChain.Common.psm1') -Force

$policyPath = Join-Path $RepositoryRoot 'supply-chain-policy.json'
$policy = Read-SupplyChainPolicy -Path $policyPath
$dockerfile = Get-Content -LiteralPath (Join-Path $RepositoryRoot 'Dockerfile') -Raw

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
foreach ($requiredText in @('pull_request:', 'merge_group:', 'contents: read', 'persist-credentials: false')) {
    if (-not $supplyWorkflow.Contains($requiredText)) {
        throw "Hosted OCI workflow is missing '$requiredText'."
    }
}

foreach ($forbiddenText in @('id-token: write', 'actions/upload-artifact', 'docker push', 'cosign sign')) {
    if ($supplyWorkflow.Contains($forbiddenText)) {
        throw "Hosted OCI workflow contains forbidden publication/signing text '$forbiddenText'."
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
