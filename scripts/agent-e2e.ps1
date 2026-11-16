#requires -Version 7.0
<#
.SYNOPSIS
    Runs the agent E2E specs (feature 011, T059; quickstart A5/A6) against a running fake stack.

.DESCRIPTION
    Same behaviour as scripts/agent-e2e.sh. Only the api is recreated with the agent settings
    (--no-deps); worker and web keep running.
    fake: for every e2e/agent-scripts/<name>.json the api runs with STEMSTUDIO_AGENT_BACKEND=fake and
    STEMSTUDIO_AGENT_FAKE_SCRIPT=/scripts/<name>.json, then the agent.spec.ts tests tagged @<name>
    run; finally the @agent tests without a script tag (fake backend, no script). A caller-set
    STEMSTUDIO_AGENT_FAKE_SCRIPT runs every @agent test once with that script.
    none: the api runs with STEMSTUDIO_AGENT_BACKEND=none, then the @agent-unavailable tests (SC-005).
    @agent-unavailable and @generative tests never run in the fake passes. Passes with a message
    while e2e/tests/agent.spec.ts does not exist. Compose project: COMPOSE_PROJECT_NAME. Exits with
    1 when a step failed.

.PARAMETER Backend
    fake or none.

.EXAMPLE
    ./scripts/agent-e2e.ps1 fake
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '', Justification = 'Developer script; console output is the UI.')]
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '', Justification = 'Only recreates the local throwaway api container.')]
[CmdletBinding()]
param([Parameter(Mandatory)] [ValidateSet('fake', 'none')] [string] $Backend)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$Spec = 'tests/agent.spec.ts'
$Excluded = '@agent-unavailable|@generative'

function Invoke-Native {
    <# Runs a native command in a directory; throws when it exits non-zero. #>
    param([string] $Directory, [string[]] $Command)
    Push-Location $Directory
    try {
        & $Command[0] @($Command | Select-Object -Skip 1) | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "$($Command -join ' ') failed with exit code $LASTEXITCODE" }
    }
    finally { Pop-Location }
}

function Set-AgentApi {
    <# Recreates only the api with the given agent backend and script, and waits until healthy. #>
    param([string] $AgentBackend, [string] $Script = '')
    Write-Host "=== api: agent backend $AgentBackend $Script ===" -ForegroundColor Cyan
    $env:STEMSTUDIO_AGENT_BACKEND = $AgentBackend
    $env:STEMSTUDIO_AGENT_FAKE_SCRIPT = $Script
    Invoke-Native $RepoRoot @('docker', 'compose', 'up', '-d', '--no-deps', '--wait', '--wait-timeout', '180', 'api')
}

function Invoke-AgentSpec {
    <# Runs the agent spec in both browsers with the given Playwright arguments. #>
    param([string[]] $Arguments)
    $command = @('npx', 'playwright', 'test', '--project=chromium', '--project=firefox', $Spec) + $Arguments
    Invoke-Native (Join-Path $RepoRoot 'e2e') $command
}

function Invoke-Scripted {
    <# One pass per agent script (tests tagged @<name>), then the @agent tests without a script tag. #>
    $tags = $Excluded
    foreach ($file in Get-ChildItem (Join-Path $RepoRoot 'e2e/agent-scripts') -Filter '*.json' -File -ErrorAction SilentlyContinue | Sort-Object Name) {
        Set-AgentApi 'fake' "/scripts/$($file.Name)"
        Invoke-AgentSpec @('--grep', "@$($file.BaseName)( |$)", '--grep-invert', $Excluded, '--pass-with-no-tests')
        $tags += "|@$($file.BaseName)( |$)"
    }
    Set-AgentApi 'fake'
    Invoke-AgentSpec @('--grep', '@agent', '--grep-invert', $tags, '--pass-with-no-tests')
}

function Invoke-Fake {
    <# The fake passes; a caller-set STEMSTUDIO_AGENT_FAKE_SCRIPT means one pass with that script. #>
    $callerScript = $env:STEMSTUDIO_AGENT_FAKE_SCRIPT
    if ($callerScript) {
        Set-AgentApi 'fake' $callerScript
        Invoke-AgentSpec @('--grep', '@agent', '--grep-invert', $Excluded, '--pass-with-no-tests')
        return
    }
    Invoke-Scripted
}

if (-not (Test-Path (Join-Path $RepoRoot "e2e/$Spec"))) {
    Write-Host "e2e/$Spec does not exist yet; agent E2E ($Backend) skipped"
    exit 0
}
$previous = @{ Backend = $env:STEMSTUDIO_AGENT_BACKEND; Script = $env:STEMSTUDIO_AGENT_FAKE_SCRIPT }
try {
    if ($Backend -eq 'fake') { Invoke-Fake }
    else {
        Set-AgentApi 'none'
        Invoke-AgentSpec @('--grep', '@agent-unavailable')
    }
    exit 0
}
catch {
    Write-Host $_ -ForegroundColor Red
    exit 1
}
finally {
    $env:STEMSTUDIO_AGENT_BACKEND = $previous.Backend
    $env:STEMSTUDIO_AGENT_FAKE_SCRIPT = $previous.Script
}
