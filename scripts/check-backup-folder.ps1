#requires -Version 7.0
<#
.SYNOPSIS
    Backups folder check: the host folder the api writes backups into exists, is writable and has free space (feature 013, FR-005).

.DESCRIPTION
    Runs scripts/backup_folder_checks.py on the host (no container is started). The folder is
    -Folder, else $env:STEMSTUDIO_BACKUP_HOST_DIR, else ./backups next to docker-compose.yml; it
    is bind-mounted into the api at /backups. On Windows (Docker Desktop) it must be writable for
    the current user; on Linux hosts for uid 1654 (sudo chown 1654:1654 <folder>). Run it before
    docker compose up; the api never changes host permissions. Exit code 0 when all checks pass,
    1 otherwise.

.PARAMETER Folder
    Folder to check (default: $env:STEMSTUDIO_BACKUP_HOST_DIR, else ./backups).

.PARAMETER MinFreeBytes
    Minimum free bytes (default 268435456 = 256 MiB, the api's stop threshold).

.EXAMPLE
    ./scripts/check-backup-folder.ps1
.EXAMPLE
    $env:STEMSTUDIO_BACKUP_HOST_DIR = 'D:\StemStudio\backups'; ./scripts/check-backup-folder.ps1
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '', Justification = 'Developer script; console output is the UI.')]
[CmdletBinding()]
param(
    [string] $Folder,
    [long] $MinFreeBytes = 268435456
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot

$checkArgs = @((Join-Path $RepoRoot 'scripts/backup_folder_checks.py'), '--repo-root', $RepoRoot, '--min-free-bytes', $MinFreeBytes)
if ($Folder) { $checkArgs += @('--folder', $Folder) }
# First interpreter that really runs (Windows has a python3 store stub), else uv's Python.
$python = @('uv', 'run', '--no-project', 'python', '-I')
foreach ($candidate in 'python3', 'python') {
    if (Get-Command $candidate -ErrorAction SilentlyContinue) {
        & $candidate -c 'import sys; sys.exit(sys.version_info < (3, 10))' *> $null
        if ($LASTEXITCODE -eq 0) { $python = @($candidate, '-I'); break }
    }
}

Write-Host 'Checking the backups folder ...'
& $python[0] @($python[1..($python.Count - 1)] + $checkArgs)
exit $LASTEXITCODE
