#requires -Version 7.0
<#
.SYNOPSIS
    Generative smoke test of the generator (ACE-Step) and singer (SoulX-Singer-SVC) GPU images (009 Q3, 010).

.DESCRIPTION
    Needs an NVIDIA GPU usable by Docker (docker run --gpus all). The checks are
    scripts/generative_smoke_checks.py, run inside the images. No secrets are read or needed.

    Stack mode (default): starts (unless -NoUp) docker-compose.yml + docker-compose.gpu.yml +
    docker-compose.generative.yml and checks in the running generator and singer: torch
    2.10.0+cu128, sm_<cc> in the arch list, /health ready with their kinds, one 10 s restyle and one
    10 s voice conversion (skip with -SkipJobs). The stack keeps running afterwards.

    Image mode (-Images): throwaway containers only (never touches a compose stack): builds both
    images (unless -NoBuild), then with --gpus all checks torch/CUDA, the engine imports and the
    pre-downloaded weights (REVISION, SHA256SUMS, listed files) in the model volume mounted read-only.

.PARAMETER Images
    Image mode (throwaway containers).

.PARAMETER NoBuild
    Image mode: use the existing images.

.PARAMETER NoUp
    Stack mode: check the already running stack.

.PARAMETER SkipJobs
    Stack mode: skip the restyle and voice-conversion jobs.

.PARAMETER Project
    Stack mode: compose project name (default: compose's own default).

.PARAMETER ModelVolume
    Image mode: the model volume (default stemstudio_model-cache), mounted read-only.

.PARAMETER GeneratorImage
    Generator image tag (default stemstudio/generator:local).

.PARAMETER SingerImage
    Singer image tag (default stemstudio/singer:local).

.EXAMPLE
    ./scripts/generative-smoke.ps1 -Images -GeneratorImage stemstudio/generator:smoke -SingerImage stemstudio/singer:smoke

.EXAMPLE
    ./scripts/generative-smoke.ps1
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '', Justification = 'Developer script; console output is the UI.')]
[CmdletBinding()]
param(
    [switch] $Images,
    [switch] $NoBuild,
    [switch] $NoUp,
    [switch] $SkipJobs,
    [string] $Project = '',
    [string] $ModelVolume = 'stemstudio_model-cache',
    [string] $GeneratorImage = 'stemstudio/generator:local',
    [string] $SingerImage = 'stemstudio/singer:local'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$Checks = Get-Content -Raw (Join-Path $PSScriptRoot 'generative_smoke_checks.py')
$Compose = @('compose', '-f', (Join-Path $RepoRoot 'docker-compose.yml'), '-f', (Join-Path $RepoRoot 'docker-compose.gpu.yml'),
    '-f', (Join-Path $RepoRoot 'docker-compose.generative.yml'))
if ($Project) { $Compose += @('-p', $Project) }

function Assert-ExitCode {
    <# Throws when the last native command failed. #>
    param([string] $What)
    if ($LASTEXITCODE -ne 0) { throw "$What failed with exit code $LASTEXITCODE" }
}

function Invoke-InImage {
    <# Runs one check mode in a throwaway GPU container. #>
    param([string] $Image, [string] $Volume, [string[]] $Mode)
    Write-Host "--- ${Image}: $Mode"
    $Checks | & docker run --rm -i --gpus all --network none -v "${Volume}:/models:ro" --entrypoint python $Image -I - @Mode | Out-Host
    Assert-ExitCode "$Image $Mode"
}

function Invoke-InService {
    <# Runs one check mode in a running compose service. #>
    param([string] $Service, [string[]] $Mode)
    Write-Host "--- ${Service}: $Mode"
    $Checks | & docker @Compose exec -T $Service python -I - @Mode | Out-Host
    Assert-ExitCode "$Service $Mode"
}

function Invoke-ImageMode {
    <# Builds (unless -SkipBuild) and checks both images in throwaway containers. #>
    param([hashtable] $Images, [string] $Volume, [switch] $SkipBuild)
    if (-not $SkipBuild) {
        foreach ($build in $Images.GetEnumerator()) {
            & docker build -f (Join-Path $RepoRoot 'docker/generator.Dockerfile') --build-arg "GENERATOR_VARIANT=$($build.Key)" -t $build.Value $RepoRoot | Out-Host
            Assert-ExitCode "docker build $($build.Value)"
        }
    }
    & nvidia-smi --query-gpu=name,memory.used,memory.total,driver_version --format=csv | Out-Host
    foreach ($engine in $Images.GetEnumerator()) {
        Invoke-InImage $engine.Value $Volume @('runtime')
        Invoke-InImage $engine.Value $Volume @('imports', $engine.Key)
        Invoke-InImage $engine.Value $Volume @('models', $engine.Key)
    }
}

function Invoke-StackMode {
    <# Starts (unless -SkipUp) and checks the running GPU generative stack. #>
    param([switch] $SkipUp, [switch] $NoJobs)
    if (-not $SkipUp) {
        & docker @Compose up -d --build --wait --wait-timeout 900 | Out-Host
        Assert-ExitCode 'docker compose up'
    }
    Invoke-InService generator @('runtime')
    Invoke-InService generator @('health', 'restyle')
    Invoke-InService singer @('runtime')
    Invoke-InService singer @('health', 'voiceConversion')
    if (-not $NoJobs) {
        Invoke-InService generator @('restyle')
        Invoke-InService singer @('convert')
    }
}

if ($Images) {
    $engines = [ordered]@{ acestep = $GeneratorImage; soulx = $SingerImage }
    Invoke-ImageMode -Images $engines -Volume $ModelVolume -SkipBuild:$NoBuild
}
else { Invoke-StackMode -SkipUp:$NoUp -NoJobs:$SkipJobs }
Write-Host 'Generative smoke test passed'
