#requires -Version 7.0
<#
.SYNOPSIS
    Key volume check: the API key ring volume api-keys is mounted at /keys in api only (011 follow-up claude-key).

.DESCRIPTION
    Static check (T019/T020; CK-FR-003, CK-FR-005). Reads `docker compose config` (no container
    is started, stopped or changed) and runs scripts/key_volume_checks.py: volume api-keys at
    /keys only in api, StemStudio__DataProtection__KeyRingDir=/keys,
    StemStudio__Agent__KeyEntry__InternalCaller=web, docker/api.Dockerfile creates /keys 0700
    for $APP_UID and declares VOLUME /keys, ANTHROPIC_API_KEY stays a bare pass-through. The
    resolved config is piped, never printed. Exit code 0 when every check passes, 1 otherwise.

.PARAMETER File
    Compose file(s), as for docker compose -f (default: docker-compose.yml).

.EXAMPLE
    ./scripts/check-key-volume.ps1
.EXAMPLE
    ./scripts/check-key-volume.ps1 -File docker-compose.yml, docker-compose.gpu.yml, docker-compose.generative.yml
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '', Justification = 'Developer script; console output is the UI.')]
[CmdletBinding()]
param(
    [string[]] $File = @('docker-compose.yml')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot

$composeArgs = @('compose')
$checkArgs = @('scripts/key_volume_checks.py', '--config', '-', '--dockerfile', 'docker/api.Dockerfile')
foreach ($path in $File) {
    $composeArgs += @('-f', $path)
    $checkArgs += @('--compose-file', $path)
}
$composeArgs += @('config', '--format', 'json')
# First interpreter that really runs (Windows has a python3 store stub), else uv's Python.
$python = @('uv', 'run', '--no-project', 'python', '-I')
foreach ($candidate in 'python3', 'python') {
    if (Get-Command $candidate -ErrorAction SilentlyContinue) {
        & $candidate -c 'import sys; sys.exit(sys.version_info < (3, 10))' *> $null
        if ($LASTEXITCODE -eq 0) { $python = @($candidate, '-I'); break }
    }
}

Write-Host "Checking the api key ring volume ($($File -join ', ')) ..."
Push-Location $RepoRoot
try {
    $config = & docker @composeArgs
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    $config | & $python[0] @($python[1..($python.Count - 1)] + $checkArgs)
}
finally { Pop-Location }
exit $LASTEXITCODE
