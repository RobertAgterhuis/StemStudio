#requires -Version 7.0
<#
.SYNOPSIS
    Quality report setting check: api and web get StemStudio__QualityReport__Enabled, off by default (feature 020).

.DESCRIPTION
    Static check (owner decision 2026-11-23: gate G1 failed). Reads `docker compose config` (no
    container is started, stopped or changed) and runs scripts/quality_report_setting_checks.py:
    api and web both carry StemStudio__QualityReport__Enabled with the expected value (false in
    docker-compose.yml, true with docker-compose.e2e.yml) and no other service carries it. The
    resolved config is piped, never printed. Exit code 0 when every check passes, 1 otherwise.

.PARAMETER Expect
    The value api and web must have: true or false.

.PARAMETER File
    Compose file(s), as for docker compose -f (default: docker-compose.yml).

.EXAMPLE
    ./scripts/check-quality-report-setting.ps1 -Expect false
.EXAMPLE
    ./scripts/check-quality-report-setting.ps1 -Expect true -File docker-compose.yml, docker-compose.e2e.yml
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '', Justification = 'Developer script; console output is the UI.')]
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('true', 'false')] [string] $Expect,
    [string[]] $File = @('docker-compose.yml')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot

$composeArgs = @('compose')
foreach ($path in $File) { $composeArgs += @('-f', $path) }
$composeArgs += @('config', '--format', 'json')
$checkArgs = @('scripts/quality_report_setting_checks.py', '--config', '-', '--expect', $Expect)
# First interpreter that really runs (Windows has a python3 store stub), else uv's Python.
$python = @('uv', 'run', '--no-project', 'python', '-I')
foreach ($candidate in 'python3', 'python') {
    if (Get-Command $candidate -ErrorAction SilentlyContinue) {
        & $candidate -c 'import sys; sys.exit(sys.version_info < (3, 10))' *> $null
        if ($LASTEXITCODE -eq 0) { $python = @($candidate, '-I'); break }
    }
}

Write-Host "Checking the quality report setting ($($File -join ', '), expect $Expect) ..."
Push-Location $RepoRoot
try {
    $config = & docker @composeArgs
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    $config | & $python[0] @($python[1..($python.Count - 1)] + $checkArgs)
}
finally { Pop-Location }
exit $LASTEXITCODE
