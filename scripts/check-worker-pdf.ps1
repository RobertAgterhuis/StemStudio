#requires -Version 7.0
<#
.SYNOPSIS
    Worker PDF engine check: WeasyPrint imports, Pango is 1.44 or newer and DejaVu Sans is listed.

.DESCRIPTION
    Feature 020, FR-012, SC-005. In throwaway containers without network, as the worker user:
    weasyprint imports and reports its version, the Pango library it loads is 1.44 or newer
    (WeasyPrint's minimum) and fc-list lists "DejaVu Sans" (the handout body font fallback).
    Exit code 0 when every check passes, 1 otherwise. No secrets are read or needed.

.PARAMETER Image
    The image to check (default stemstudio/worker:local).

.PARAMETER Build
    Builds the CPU variant of docker/worker.Dockerfile with that tag first.

.EXAMPLE
    ./scripts/check-worker-pdf.ps1 -Build -Image stemstudio/worker:pdf-check
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '', Justification = 'Developer script; console output is the UI.')]
[CmdletBinding()]
param(
    [string] $Image = 'stemstudio/worker:local',
    [switch] $Build
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$PangoMin = [version] '1.44'

if ($Build) {
    & docker build -f (Join-Path $RepoRoot 'docker/worker.Dockerfile') -t $Image $RepoRoot
    if ($LASTEXITCODE -ne 0) { throw "docker build failed with exit code $LASTEXITCODE" }
}
Write-Host "Checking $Image for WeasyPrint, Pango >= $PangoMin and DejaVu Sans ..."
$failed = 0
# pango_version() is the version of the loaded library as major * 10000 + minor * 100 + micro.
$probe = 'import weasyprint' + "`n" + 'from weasyprint.text.ffi import pango' + "`n" + 'v = pango.pango_version()' + "`n" +
    'print(weasyprint.__version__, f"{v // 10000}.{v // 100 % 100}.{v % 100}")'
$versions = & docker run --rm --network none --entrypoint python $Image -I -c $probe 2>$null
if ($LASTEXITCODE -eq 0 -and $versions) {
    $weasy, $pango = ([string] $versions).Trim() -split ' '
    Write-Host "OK   weasyprint $weasy imports as the worker user"
    if ([version] $pango -ge $PangoMin) {
        Write-Host "OK   Pango $pango >= $PangoMin"
    } else {
        Write-Host "FAIL Pango $pango is older than $PangoMin (WeasyPrint needs $PangoMin or newer)"
        $failed = 1
    }
} else {
    Write-Host 'FAIL weasyprint does not import as the worker user (not installed, or Pango/HarfBuzz missing)'
    Write-Host "FAIL Pango version unknown (needs $PangoMin or newer)"
    $failed = 1
}
$families = & docker run --rm --network none --entrypoint fc-list $Image ':' 'family'
if ($families | Where-Object { $_ -match '(?i)DejaVu Sans' }) {
    Write-Host 'OK   fc-list lists DejaVu Sans'
} else {
    Write-Host 'FAIL fc-list does not list DejaVu Sans'
    $failed = 1
}
exit $failed
