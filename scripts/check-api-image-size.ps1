#requires -Version 7.0
<#
.SYNOPSIS
    API image size check: the vendored lyric dictionaries in the api image stay below 15 MB (023 T069, ADR-037).

.DESCRIPTION
    Read-only: `docker image inspect` and a throwaway `docker run --rm --entrypoint du` of the image,
    which is not changed. Runs scripts/api_image_size_checks.py: /app/LyricAssist/Dictionaries is below
    the budget; with -BaselineImage (an api image built before the dictionaries existed) the whole image
    is also less than the budget larger than the baseline (one-off measurement, not run in CI).
    Exit code 0 when every check passes, 1 otherwise.

.PARAMETER Image
    The api image to measure (default: stemstudio/api:local, the image CI's own compose project builds).

.PARAMETER BaselineImage
    Optional api image from before the dictionaries.

.PARAMETER BudgetBytes
    Budget in bytes (default 15000000).

.EXAMPLE
    ./scripts/check-api-image-size.ps1 -Image stemstudio/api:local
#>
[CmdletBinding()]
param(
    [string] $Image = 'stemstudio/api:local',
    [string] $BaselineImage = '',
    [int] $BudgetBytes = 15000000
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot

$checkArgs = @('scripts/api_image_size_checks.py', '--image', $Image, '--budget-bytes', $BudgetBytes)
if ($BaselineImage) { $checkArgs += @('--baseline-image', $BaselineImage) }
# First interpreter that really runs (Windows has a python3 store stub), else uv's Python.
$python = @('uv', 'run', '--no-project', 'python', '-I')
foreach ($candidate in 'python3', 'python') {
    if (Get-Command $candidate -ErrorAction SilentlyContinue) {
        & $candidate -c 'import sys; sys.exit(sys.version_info < (3, 10))' *> $null
        if ($LASTEXITCODE -eq 0) { $python = @($candidate, '-I'); break }
    }
}

Push-Location $RepoRoot
try { & $python[0] @($python[1..($python.Count - 1)] + $checkArgs) }
finally { Pop-Location }
exit $LASTEXITCODE
