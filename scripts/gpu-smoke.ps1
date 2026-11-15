#requires -Version 7.0
<#
.SYNOPSIS
    GPU smoke test of the GPU worker image (feature 003, FR-009, FR-003; quickstart G3/G4).

.DESCRIPTION
    Needs an NVIDIA GPU usable by Docker (docker run --gpus all). Never touches the compose stack:
    it uses throwaway containers that are removed afterwards. No secrets are read or needed.
      1. builds the GPU variant as -Image (unless -NoBuild)
      2. with --gpus all: torch +cu132, sm_<cc> in the arch list, matmul, onnxruntime CUDA EP after
         preload_dlls(), and /health deviceInfo.kind == gpu
      3. without --gpus: the worker still starts, /health deviceInfo.kind == cpu with a reason
    The checks are scripts/gpu_smoke_checks.py, run inside the image. Exit code 0 when all pass.

.PARAMETER Image
    The GPU image to build and test (default stemstudio/worker:local-gpu).

.PARAMETER NoBuild
    Tests an existing image instead of building it first.

.EXAMPLE
    ./scripts/gpu-smoke.ps1
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '', Justification = 'Developer script; console output is the UI.')]
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '', Justification = 'Private helpers that only change this script''s own throwaway containers or process environment.')]
[CmdletBinding()]
param(
    [string] $Image = 'stemstudio/worker:local-gpu',
    [switch] $NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$Checks = Get-Content -Raw (Join-Path $PSScriptRoot 'gpu_smoke_checks.py')
$Container = "stemstudio-gpu-smoke-$PID"

function Invoke-Docker {
    <# Runs docker with the arguments; throws when it exits non-zero. #>
    param([string[]] $Arguments)
    & docker @Arguments
    if ($LASTEXITCODE -ne 0) { throw "docker $($Arguments[0]) failed with exit code $LASTEXITCODE" }
}

function Remove-SmokeContainer {
    <# Removes the throwaway container and its anonymous volumes, if it exists. #>
    & docker rm -f -v $Container 2>&1 | Out-Null
}

function Test-Health {
    <# Starts the worker, checks /health deviceInfo.kind == $Expected, removes the container. #>
    param([string] $Expected, [string[]] $RunOptions = @())
    Write-Host "--- /health, expecting deviceInfo.kind == $Expected"
    Invoke-Docker (@('run', '-d', '--name', $Container, '--network', 'none', '-e', 'STEMSTUDIO_DEVICE=auto') + $RunOptions + @($Image))
    try {
        $Checks | & docker exec -i $Container python -I - health $Expected
        if ($LASTEXITCODE -ne 0) { & docker logs --tail 50 $Container; throw "/health check ($Expected) failed" }
    }
    finally { Remove-SmokeContainer }
}

try {
    if (-not $NoBuild) {
        Invoke-Docker @('build', '-f', (Join-Path $RepoRoot 'docker/worker.Dockerfile'), '--build-arg', 'TORCH_VARIANT=cu132',
            '--build-arg', 'ORT_VARIANT=gpu', '--build-arg', 'WORKER_DEVICE=auto', '-t', $Image, $RepoRoot)
    }
    Write-Host "--- runtime checks in $Image with --gpus all"
    $Checks | & docker run --rm -i --gpus all --network none --entrypoint python $Image -I - runtime
    if ($LASTEXITCODE -ne 0) { throw 'runtime checks failed' }
    Test-Health 'gpu' @('--gpus', 'all')
    Test-Health 'cpu'
    Write-Host "GPU smoke test passed for $Image"
}
finally { Remove-SmokeContainer }
