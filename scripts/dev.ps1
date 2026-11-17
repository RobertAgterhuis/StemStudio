#requires -Version 7.0
<#
.SYNOPSIS
    Runs StemStudio without Docker: worker (uv, :8000), API (dotnet run, :8081) and web (dotnet run, :8080).

.DESCRIPTION
    Builds the audio-engine and web-interop bundles first, then starts the three processes with data under <repo>/data.
    Press Ctrl+C to stop all of them. No secrets are needed; the optional Claude key (feature 011)
    is entered in Settings (stored encrypted; the key ring directory is printed at start, per user,
    outside the repository) or inherited from your session ($env:ANTHROPIC_API_KEY = Read-Host
    -MaskInput) and never written anywhere by this script.

.PARAMETER Fake
    Uses the fake separator (STEMSTUDIO_SEPARATOR=fake): stems are copies of the mix, no model download.
    The detail phase is faked too (STEMSTUDIO_FAKE_DETAIL_STEP_SECONDS per parent step, default 1).

.PARAMETER SkipEngineBuild
    Does not run 'npm ci' / 'npm run build' in src/audio-engine and src/web-interop (reuse the
    existing bundles in src/StemStudio.Web/wwwroot/js).

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
    # Detail model weights (~1.9 GB, first real detail phase); data/ is git-ignored; existing values win.
    if (-not $env:STEMSTUDIO_MODEL_DIR) { $env:STEMSTUDIO_MODEL_DIR = Join-Path $dataDir 'models' }
    if (-not $env:STEMSTUDIO_FAKE_DETAIL_STEP_SECONDS) { $env:STEMSTUDIO_FAKE_DETAIL_STEP_SECONDS = '1' }
    # Fake device of the fake separator (cpu, gpu, gpu-oom; feature 003); an existing value wins.
    if (-not $env:STEMSTUDIO_FAKE_DEVICE) { $env:STEMSTUDIO_FAKE_DEVICE = 'cpu' }
    $env:StemStudio__DataDir = Join-Path $dataDir 'api'
    # Backups folder (feature 013, FR-005): <repo>/data/backups unless StemStudio__BackupDir is set.
    if (-not $env:StemStudio__BackupDir) { $env:StemStudio__BackupDir = Join-Path $dataDir 'backups' }
    if (-not $env:StemStudio__BackupHostDirHint) { $env:StemStudio__BackupHostDirHint = $env:StemStudio__BackupDir }
    New-Item -ItemType Directory -Force -Path $env:StemStudio__BackupDir | Out-Null
    $env:StemStudio__WorkerBaseUrl = 'http://localhost:8000'
    $env:StemStudio__WebOrigin = 'http://localhost:8080'
    $env:StemStudio__ApiBaseUrl = 'http://localhost:8081'
    $env:StemStudio__PublicApiBaseUrl = 'http://localhost:8081'
    Write-Host "Separator: $env:STEMSTUDIO_SEPARATOR (fake device $env:STEMSTUDIO_FAKE_DEVICE); data under $dataDir; models in $env:STEMSTUDIO_MODEL_DIR"
    Initialize-LibraryEnvironment (Join-Path $dataDir 'api')
    Initialize-AgentEnvironment
    Initialize-KeyRingEnvironment
}

function Initialize-AgentEnvironment {
    <# Feature 011 (AI composer agent, api only): Ollama is optional and runs on this host; existing
       values win. STEMSTUDIO_AGENT_FAKE_SCRIPT (with STEMSTUDIO_AGENT_BACKEND=fake) is a host path,
       e.g. <repo>/e2e/agent-scripts/<name>.json. ANTHROPIC_API_KEY passes through unchanged. #>
    $env:StemStudio__Agent__Ollama__BaseUrl = if ($env:STEMSTUDIO_OLLAMA_URL) { $env:STEMSTUDIO_OLLAMA_URL } else { 'http://localhost:11434' }
    $env:StemStudio__Agent__Backend = if ($env:STEMSTUDIO_AGENT_BACKEND) { $env:STEMSTUDIO_AGENT_BACKEND } else { 'ollama' }
    # Feature 023 (lyric writing assistant, T069): host folder of the fake assistant scripts (a file or a
    # folder, resolved per assist as <title-slug>-<kind>.json, else <kind>.json); read only with the fake
    # backend, like the Docker E2E override (/lyric-scripts). An existing value wins.
    if (-not $env:STEMSTUDIO_LYRIC_ASSIST_FAKE_SCRIPT) { $env:STEMSTUDIO_LYRIC_ASSIST_FAKE_SCRIPT = Join-Path $RepoRoot 'e2e/lyric-scripts' }
    $keyState = if ($env:ANTHROPIC_API_KEY) { 'set' } else { 'unset' }
    Write-Host "Agent: backend $env:StemStudio__Agent__Backend; Ollama $env:StemStudio__Agent__Ollama__BaseUrl; Claude key $keyState"
}

function Initialize-KeyRingEnvironment {
    <# 011 follow-up claude-key (api only): Data Protection key ring of the Claude key stored in
       Settings, per user and outside the repository and data/ (never copy it with the library or a
       backup); an existing value wins. Windows: %LOCALAPPDATA%\StemStudio\keys (DPAPI-protected by
       the API); Linux/macOS: $XDG_DATA_HOME/stemstudio/keys or ~/.local/share/stemstudio/keys. #>
    if (-not $env:StemStudio__DataProtection__KeyRingDir) {
        $env:StemStudio__DataProtection__KeyRingDir = if ($IsWindows) {
            Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'StemStudio' 'keys'
        }
        elseif ($env:XDG_DATA_HOME -and [IO.Path]::IsPathRooted($env:XDG_DATA_HOME)) {
            Join-Path $env:XDG_DATA_HOME 'stemstudio' 'keys'
        }
        else { Join-Path $HOME '.local' 'share' 'stemstudio' 'keys' }
    }
    Write-Host "Claude key ring (api, per user; never copy it with data/): $env:StemStudio__DataProtection__KeyRingDir"
}

function Initialize-LibraryEnvironment {
    <# Feature 007: the worker reads the API data dir (never writes; the API is the only writer).
       Feature 005: STEMSTUDIO_ONLINE_LOOKUPS, STEMSTUDIO_CONTACT_URL, STEMSTUDIO_WIKIPEDIA_LANGUAGES and
       STEMSTUDIO_FAKE_ONLINE pass through from your shell unchanged (worker defaults: on, generic
       User-Agent token, en, found); no value is set here. Features 006/008 likewise:
       STEMSTUDIO_ALIGN_MODEL, STEMSTUDIO_ALIGN_AUTO, STEMSTUDIO_FAKE_LYRICS, STEMSTUDIO_TRANSCRIBER and
       STEMSTUDIO_TRANSCRIBE_THREADS (worker defaults: turbo, gpu, word, automatic, 8). Sheet-music
       PDFs need the native Cairo library and the Leipzig font on the host (the Docker image has both). #>
    param([string] $ApiDataDir)
    $env:STEMSTUDIO_LIBRARY_DIR = $ApiDataDir
    $lookups = if ($env:STEMSTUDIO_ONLINE_LOOKUPS) { $env:STEMSTUDIO_ONLINE_LOOKUPS } else { 'on' }
    Write-Host "Library (read-only for the worker): $env:STEMSTUDIO_LIBRARY_DIR; online lookups: $lookups"
}

function Build-Engine {
    <# Builds src/audio-engine (audio-engine.js) and src/web-interop (web-interop.js, first-paint.js) into wwwroot/js. #>
    foreach ($bundle in 'src/audio-engine', 'src/web-interop') {
        $bundleDir = Join-Path $RepoRoot $bundle
        Invoke-Checked $bundleDir 'npm' @('ci', '--no-audit', '--no-fund')
        Invoke-Checked $bundleDir 'npm' @('run', 'build')
    }
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
