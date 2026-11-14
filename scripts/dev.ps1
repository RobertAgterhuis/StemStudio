#requires -Version 7.0
<#
.SYNOPSIS
    Runs StemStudio without Docker: worker (uv, :8000), API (dotnet run, :8081) and web (dotnet run, :8080).

.DESCRIPTION
    Builds the audio-engine bundle first, then starts the three processes with data under <repo>/data.
    Press Ctrl+C to stop all of them. No secrets are read or needed.

.PARAMETER Fake
    Uses the fake separator (STEMSTUDIO_SEPARATOR=fake): stems are copies of the mix, no model download.

.PARAMETER SkipEngineBuild
    Does not run 'npm ci' / 'npm run build' in src/audio-engine (reuse the existing bundle).

.PARAMETER SmokeTest
    Starts everything, waits until the three /health endpoints answer 200, prints them and stops.
    Exit code 0 when all are healthy, 1 otherwise.

.PARAMETER TimeoutSeconds
    How long to wait for each /health endpoint (default 180).

.EXAMPLE
    ./scripts/dev.ps1 -Fake
.EXAMPLE
    ./scripts/dev.ps1 -Fake -SmokeTest
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '', Justification = 'Interactive developer script; console output is the UI.')]
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '', Justification = 'Private helpers that only start/stop this script''s own child processes.')]
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSReviewUnusedParameter', '', Justification = 'Script parameters are read inside helper functions (script scope).')]
[CmdletBinding()]
param(
    [switch] $Fake,
    [switch] $SkipEngineBuild,
    [switch] $SmokeTest,
    [ValidateRange(10, 3600)] [int] $TimeoutSeconds = 180
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot
$HealthUrls = [ordered]@{
    worker = 'http://localhost:8000/health'
    api    = 'http://localhost:8081/health'
    web    = 'http://localhost:8080/health'
}

function Invoke-Checked {
    <# Runs a native command in a directory and throws when it exits non-zero. #>
    param([string] $Directory, [string] $FilePath, [string[]] $Arguments)
    Push-Location $Directory
    try {
        & $FilePath @Arguments
        if ($LASTEXITCODE -ne 0) { throw "$FilePath $($Arguments -join ' ') failed with exit code $LASTEXITCODE" }
    }
    finally { Pop-Location }
}

function Initialize-Environment {
    <# Sets the variables the three processes inherit (quickstart.md, Configuration). #>
    $dataDir = Join-Path $RepoRoot 'data'
    New-Item -ItemType Directory -Force -Path (Join-Path $dataDir 'worker'), (Join-Path $dataDir 'api') | Out-Null
    $env:STEMSTUDIO_DATA_DIR = Join-Path $dataDir 'worker'
    $env:STEMSTUDIO_SEPARATOR = if ($Fake) { 'fake' } elseif ($env:STEMSTUDIO_SEPARATOR) { $env:STEMSTUDIO_SEPARATOR } else { 'demucs' }
    $env:StemStudio__DataDir = Join-Path $dataDir 'api'
    $env:StemStudio__WorkerBaseUrl = 'http://localhost:8000'
    $env:StemStudio__WebOrigin = 'http://localhost:8080'
    $env:StemStudio__ApiBaseUrl = 'http://localhost:8081'
    $env:StemStudio__PublicApiBaseUrl = 'http://localhost:8081'
    Write-Host "Separator: $env:STEMSTUDIO_SEPARATOR; data under $dataDir"
}

function Build-Engine {
    <# Builds src/audio-engine into src/StemStudio.Web/wwwroot/js/audio-engine.js. #>
    $engineDir = Join-Path $RepoRoot 'src/audio-engine'
    Invoke-Checked $engineDir 'npm' @('ci', '--no-audit', '--no-fund')
    Invoke-Checked $engineDir 'npm' @('run', 'build')
}

function Build-DotNet {
    <# Builds the solution once, so API and web do not race on shared outputs (Contracts). #>
    Invoke-Checked $RepoRoot 'dotnet' @('build', 'StemStudio.slnx')
}

function Start-Component {
    <# Starts one long-running process in the current console and returns it. #>
    param([string] $Name, [string] $Directory, [string] $FilePath, [string[]] $Arguments)
    Write-Host "Starting $Name ..."
    Start-Process -FilePath $FilePath -ArgumentList $Arguments -WorkingDirectory $Directory -NoNewWindow -PassThru
}

function Start-All {
    <# Starts worker, API and web; returns the processes. #>
    $workerArgs = @('run', 'uvicorn', '--factory', 'stemstudio_worker.main:create_app', '--host', '127.0.0.1', '--port', '8000')
    @(
        Start-Component 'worker' (Join-Path $RepoRoot 'worker') 'uv' $workerArgs
        Start-Component 'api' $RepoRoot 'dotnet' @('run', '--no-build', '--project', 'src/StemStudio.Api', '--launch-profile', 'http')
        Start-Component 'web' $RepoRoot 'dotnet' @('run', '--no-build', '--project', 'src/StemStudio.Web', '--launch-profile', 'http')
    )
}

function Test-Healthy {
    <# Returns $true when GET $Url answers 200 within the timeout. #>
    param([string] $Url)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -Uri $Url -TimeoutSec 5 -SkipHttpErrorCheck
            if ($response.StatusCode -eq 200) { Write-Host "$Url -> 200 $($response.Content)"; return $true }
        }
        catch { Write-Verbose "waiting for $Url" }
        Start-Sleep -Seconds 2
    }
    Write-Warning "$Url did not become healthy within $TimeoutSeconds s"
    $false
}

function Wait-AllHealthy {
    <# Waits for every /health endpoint; returns $true when all are healthy. #>
    $results = foreach ($url in $HealthUrls.Values) { Test-Healthy $url }
    -not ($results -contains $false)
}

function Stop-All {
    <# Stops the processes and their child processes (dotnet run and uv run spawn children). #>
    param([System.Diagnostics.Process[]] $Processes)
    foreach ($process in $Processes | Where-Object { $_ -and -not $_.HasExited }) {
        if ($IsWindows) { & taskkill.exe /PID $process.Id /T /F | Out-Null }
        else { $process.Kill($true) }
    }
    Write-Host 'Stopped worker, api and web.'
}

Initialize-Environment
if (-not $SkipEngineBuild) { Build-Engine }
Build-DotNet
$processes = Start-All
$exitCode = 0
try {
    $healthy = Wait-AllHealthy
    if ($SmokeTest) { $exitCode = if ($healthy) { 0 } else { 1 } }
    else {
        Write-Host 'StemStudio runs at http://localhost:8080 (API http://localhost:8081). Press Ctrl+C to stop.'
        Wait-Process -Id ($processes.Id)
    }
}
finally { Stop-All $processes }
exit $exitCode
