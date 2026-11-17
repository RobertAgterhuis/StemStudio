#requires -Version 7.0
<#
.SYNOPSIS
    Runs the lyric assistant E2E passes (feature 023, T069/T070; quickstart Q1-Q6) against a running fake stack.

.DESCRIPTION
    Same behaviour as scripts/lyric-e2e.sh. Only the api is recreated (--no-deps); worker and web keep running.
    fake: the api runs with STEMSTUDIO_AGENT_BACKEND=fake and STEMSTUDIO_LYRIC_ASSIST_FAKE_SCRIPT=/lyric-scripts
    (e2e/lyric-scripts/, mounted read-only by docker-compose.e2e.yml), then `npm run test:lyric-assist` runs.
    none: STEMSTUDIO_AGENT_BACKEND=none, then `npm run test:lyric-assist-none` (Q6: no assistant, no outbound request).
    The Claude double has no variable in the contracts; test:lyric-assist-claude self-skips and is not run here.
    docker-compose.e2e.yml is added with -f when COMPOSE_FILE is empty. Passes with a message while e2e/package.json
    has no test:lyric-assist script. Compose project: COMPOSE_PROJECT_NAME. Exits with 1 when a step failed.

.PARAMETER Backend
    fake or none.

.EXAMPLE
    ./scripts/lyric-e2e.ps1 fake
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '', Justification = 'Developer script; console output is the UI.')]
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '', Justification = 'Only recreates the local throwaway api container.')]
[CmdletBinding()]
param([Parameter(Mandatory)] [ValidateSet('fake', 'none')] [string] $Backend)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$LyricFolder = '/lyric-scripts'

function Invoke-Native {
    <# Runs a native command in a directory; throws when it exits non-zero. #>
    param([string] $Directory, [string[]] $Command)
    Push-Location $Directory
    try {
        $arguments = @($Command | Select-Object -Skip 1)
        & $Command[0] @arguments | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "$($Command -join ' ') failed with exit code $LASTEXITCODE" }
    }
    finally { Pop-Location }
}

function Set-LyricApi {
    <# Recreates only the api with the given agent backend and lyric script folder, and waits until healthy. #>
    param([string] $AgentBackend)
    Write-Host "=== api: agent backend $AgentBackend, lyric scripts $LyricFolder ===" -ForegroundColor Cyan
    $env:STEMSTUDIO_AGENT_BACKEND = $AgentBackend
    $env:STEMSTUDIO_AGENT_FAKE_SCRIPT = ''
    $env:STEMSTUDIO_LYRIC_ASSIST_FAKE_SCRIPT = $LyricFolder
    # The e2e override carries the /lyric-scripts mount: added when COMPOSE_FILE is empty (CI sets it with the override).
    $files = @()
    if (-not $env:COMPOSE_FILE) { $files = @('-f', 'docker-compose.yml', '-f', 'docker-compose.e2e.yml') }
    elseif ($env:COMPOSE_FILE -notlike '*docker-compose.e2e.yml*') { Write-Warning 'COMPOSE_FILE lacks docker-compose.e2e.yml: /lyric-scripts is not mounted' }
    Invoke-Native $RepoRoot (@('docker', 'compose') + $files + @('up', '-d', '--no-deps', '--wait', '--wait-timeout', '180', 'api'))
}

function Invoke-Npm {
    <# Runs one npm script of the e2e package. #>
    param([string] $Script)
    Invoke-Native (Join-Path $RepoRoot 'e2e') @('npm', 'run', $Script)
}

if (-not (Select-String -Path (Join-Path $RepoRoot 'e2e/package.json') -Pattern '"test:lyric-assist"' -Quiet)) {
    Write-Host "e2e/package.json has no test:lyric-assist script yet; lyric E2E ($Backend) skipped"
    exit 0
}
$previous = @{
    Backend = $env:STEMSTUDIO_AGENT_BACKEND
    Script = $env:STEMSTUDIO_AGENT_FAKE_SCRIPT
    Lyric = $env:STEMSTUDIO_LYRIC_ASSIST_FAKE_SCRIPT
}
try {
    if ($Backend -eq 'fake') { Set-LyricApi 'fake'; Invoke-Npm 'test:lyric-assist' }
    else { Set-LyricApi 'none'; Invoke-Npm 'test:lyric-assist-none' }
    exit 0
}
catch {
    Write-Host $_ -ForegroundColor Red
    exit 1
}
finally {
    $env:STEMSTUDIO_AGENT_BACKEND = $previous.Backend
    $env:STEMSTUDIO_AGENT_FAKE_SCRIPT = $previous.Script
    $env:STEMSTUDIO_LYRIC_ASSIST_FAKE_SCRIPT = $previous.Lyric
}
