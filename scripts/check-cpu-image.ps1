#requires -Version 7.0
<#
.SYNOPSIS
    CPU-image guard: fails when the worker image (or the fake generator image) holds any NVIDIA/CUDA runtime.

.DESCRIPTION
    Runs scripts/cpu_image_guard.py inside the image (feature 003 FR-008, quickstart G2, ADR-011).
    Worker profile (default): no distribution named nvidia-* other than nvidia-ml-py, no cuda-*,
    triton or onnxruntime-gpu, torch.version.cuda is None, no matplotlib (008), and torchaudio and
    stable_whisper (006) import. Generator profile (-Generator, 009/010 fake variant): the same
    NVIDIA/CUDA rules, no torch or model stack at all, and the generator package imports.
    Exit code 0 when the image passes, 1 otherwise. No secrets are read or needed.

.PARAMETER Image
    The image to check (default stemstudio/worker:local; with -Generator stemstudio/generator:local-fake).

.PARAMETER Build
    Builds the image with that tag first (worker: CPU variant; generator: GENERATOR_VARIANT=fake).

.PARAMETER Generator
    Checks the fake generator image (guard profile generator).

.EXAMPLE
    ./scripts/check-cpu-image.ps1 -Build -Image stemstudio/worker:cpu-check

.EXAMPLE
    ./scripts/check-cpu-image.ps1 -Generator -Build
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '', Justification = 'Developer script; console output is the UI.')]
[CmdletBinding()]
param(
    [string] $Image = '',
    [switch] $Build,
    [switch] $Generator
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot

if ($Generator) {
    $profileName = 'generator'
    if (-not $Image) { $Image = 'stemstudio/generator:local-fake' }
    $buildArgs = @('-f', (Join-Path $RepoRoot 'docker/generator.Dockerfile'), '--build-arg', 'GENERATOR_VARIANT=fake')
}
else {
    $profileName = 'worker'
    if (-not $Image) { $Image = 'stemstudio/worker:local' }
    $buildArgs = @('-f', (Join-Path $RepoRoot 'docker/worker.Dockerfile'))
}
if ($Build) {
    & docker build @buildArgs -t $Image $RepoRoot
    if ($LASTEXITCODE -ne 0) { throw "docker build failed with exit code $LASTEXITCODE" }
}
Write-Host "Checking $Image ($profileName profile) for NVIDIA/CUDA runtime libraries ..."
Get-Content -Raw (Join-Path $PSScriptRoot 'cpu_image_guard.py') |
    & docker run --rm -i --network none --entrypoint python $Image -I - $profileName
exit $LASTEXITCODE
