#requires -Version 7.0
<#
.SYNOPSIS
    CPU-image guard: fails when the worker image holds any NVIDIA/CUDA runtime (feature 003, FR-008).

.DESCRIPTION
    Runs scripts/cpu_image_guard.py inside the image (quickstart G2, ADR-011): no distribution named
    nvidia-* other than nvidia-ml-py, no cuda-*, triton or onnxruntime-gpu, and torch.version.cuda
    is None. Exit code 0 when the image is CPU only, 1 otherwise. No secrets are read or needed.

.PARAMETER Image
    The image to check (default stemstudio/worker:local).

.PARAMETER Build
    Builds the CPU variant of docker/worker.Dockerfile with that tag first.

.EXAMPLE
    ./scripts/check-cpu-image.ps1 -Build -Image stemstudio/worker:cpu-check
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '', Justification = 'Developer script; console output is the UI.')]
[CmdletBinding()]
param(
    [string] $Image = 'stemstudio/worker:local',
    [switch] $Build
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot

if ($Build) {
    & docker build -f (Join-Path $RepoRoot 'docker/worker.Dockerfile') -t $Image $RepoRoot
    if ($LASTEXITCODE -ne 0) { throw "docker build failed with exit code $LASTEXITCODE" }
}
Write-Host "Checking $Image for NVIDIA/CUDA runtime libraries ..."
Get-Content -Raw (Join-Path $PSScriptRoot 'cpu_image_guard.py') |
    & docker run --rm -i --network none --entrypoint python $Image -I -
exit $LASTEXITCODE
