#requires -Version 7.0
<#
.SYNOPSIS
    Worker font check: the worker image lists the Leipzig and DejaVu fonts and imports the engraving stack.

.DESCRIPTION
    Feature 008, SC-004 (quickstart M2). In throwaway containers without network: fc-list inside the
    image lists the family "Leipzig" (Verovio's SVG text glyphs) and "DejaVu Sans" (setlist PDFs,
    feature 014), and verovio, cairosvg (with its native libcairo), music21 and reportlab import as
    the worker user. Exit code 0 when every check passes, 1 otherwise.
    No secrets are read or needed.

.PARAMETER Image
    The image to check (default stemstudio/worker:local).

.PARAMETER Build
    Builds the CPU variant of docker/worker.Dockerfile with that tag first.

.EXAMPLE
    ./scripts/check-worker-fonts.ps1 -Build -Image stemstudio/worker:font-check
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

if ($Build) {
    & docker build -f (Join-Path $RepoRoot 'docker/worker.Dockerfile') -t $Image $RepoRoot
    if ($LASTEXITCODE -ne 0) { throw "docker build failed with exit code $LASTEXITCODE" }
}
Write-Host "Checking $Image for the Leipzig and DejaVu fonts and the engraving imports ..."
$failed = 0
$families = & docker run --rm --network none --entrypoint fc-list $Image ':' 'family'
if ($LASTEXITCODE -eq 0 -and ($families -ccontains 'Leipzig')) {
    Write-Host 'OK   fc-list lists Leipzig'
} else {
    Write-Host 'FAIL fc-list does not list Leipzig'
    $failed = 1
}
if ($families | Where-Object { $_ -match '(?i)DejaVu Sans' }) {
    Write-Host 'OK   fc-list lists DejaVu Sans'
} else {
    Write-Host 'FAIL fc-list does not list DejaVu Sans'
    $failed = 1
}
& docker run --rm --network none --entrypoint python $Image -I -c 'import verovio, cairosvg, music21, reportlab'
if ($LASTEXITCODE -eq 0) {
    Write-Host 'OK   verovio, cairosvg, music21 and reportlab import as the worker user'
} else {
    Write-Host 'FAIL verovio, cairosvg, music21 or reportlab does not import'
    $failed = 1
}
exit $failed
