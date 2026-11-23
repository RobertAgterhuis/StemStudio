#requires -Version 7.0
<#
.SYNOPSIS
    Loopback ports check: api and web publish on 127.0.0.1 unless STEMSTUDIO_BIND_ADDRESS opens them (refactor 004).

.DESCRIPTION
    Static check (refactor 004, 003 step 4, ADR-027 D3, threat T7). Reads `docker compose config`
    (no container is started, stopped or changed) and runs scripts/loopback_ports_checks.py twice:
    with STEMSTUDIO_BIND_ADDRESS unset every published port of api and web binds to 127.0.0.1 and
    no other service publishes a port; with STEMSTUDIO_BIND_ADDRESS=0.0.0.0 the opt-in is honoured.
    A STEMSTUDIO_BIND_ADDRESS in a local .env is read by compose, so the default check then fails
    (intended). Exit code 0 when every check passes, 1 otherwise.

.PARAMETER File
    Compose file(s), as for docker compose -f (default: docker-compose.yml).

.EXAMPLE
    ./scripts/check-loopback-ports.ps1
.EXAMPLE
    ./scripts/check-loopback-ports.ps1 -File docker-compose.yml, docker-compose.e2e.yml
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
foreach ($path in $File) { $composeArgs += @('-f', $path) }
$composeArgs += @('config', '--format', 'json')
# First interpreter that really runs (Windows has a python3 store stub), else uv's Python.
$python = @('uv', 'run', '--no-project', 'python', '-I')
foreach ($candidate in 'python3', 'python') {
    if (Get-Command $candidate -ErrorAction SilentlyContinue) {
        & $candidate -c 'import sys; sys.exit(sys.version_info < (3, 10))' *> $null
        if ($LASTEXITCODE -eq 0) { $python = @($candidate, '-I'); break }
    }
}

function Test-PublishedPort([string] $Expected, [AllowNull()] [string] $BindAddress) {
    Write-Host "Checking the published ports ($($File -join ', '); STEMSTUDIO_BIND_ADDRESS=$(if ($BindAddress) { $BindAddress } else { '<unset>' })) ..."
    $env:STEMSTUDIO_BIND_ADDRESS = $BindAddress
    $config = & docker @composeArgs
    if ($LASTEXITCODE -ne 0) { return $LASTEXITCODE }
    $checkArgs = @('scripts/loopback_ports_checks.py', '--config', '-', '--expect', $Expected)
    $config | & $python[0] @($python[1..($python.Count - 1)] + $checkArgs) | Write-Host
    return $LASTEXITCODE
}

$saved = $env:STEMSTUDIO_BIND_ADDRESS
Push-Location $RepoRoot
try {
    $status = 0
    if ((Test-PublishedPort '127.0.0.1' $null) -ne 0) { $status = 1 }
    if ((Test-PublishedPort '0.0.0.0' '0.0.0.0') -ne 0) { $status = 1 }
}
finally {
    $env:STEMSTUDIO_BIND_ADDRESS = $saved
    Pop-Location
}
exit $status
