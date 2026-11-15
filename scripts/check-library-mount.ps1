#requires -Version 7.0
<#
.SYNOPSIS
    Library mount check: the running worker sees the API data volume read-only at /library (feature 007, FR-012).

.DESCRIPTION
    Runs scripts/library_mount_checks.py inside the running worker container (ADR-016 D5):
    STEMSTUDIO_LIBRARY_DIR is /library, GET /health reports library.readable: true, and a write
    inside /library as the worker user fails with "Read-only file system". Exit code 0 when all
    three pass, 1 otherwise. No secrets are read or needed.

.PARAMETER Project
    Compose project of the running stack (default: the name in the compose file, stemstudio).

.PARAMETER File
    Compose file(s), as for docker compose -f (default: docker-compose.yml).

.EXAMPLE
    ./scripts/check-library-mount.ps1
.EXAMPLE
    ./scripts/check-library-mount.ps1 -Project stemstudio-e2e
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '', Justification = 'Developer script; console output is the UI.')]
[CmdletBinding()]
param(
    [string] $Project,
    [string[]] $File = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot

$composeArgs = @('compose')
if ($Project) { $composeArgs += @('-p', $Project) }
foreach ($path in $File) { $composeArgs += @('-f', $path) }
$composeArgs += @('exec', '-T', 'worker', 'python', '-I', '-')

Write-Host "Checking the worker's read-only /library mount ..."
Push-Location $RepoRoot
try {
    Get-Content -Raw (Join-Path $PSScriptRoot 'library_mount_checks.py') | & docker @composeArgs
}
finally { Pop-Location }
exit $LASTEXITCODE
