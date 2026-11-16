#requires -Version 7.0
<#
.SYNOPSIS
    Licence report of built images: one Markdown table per image of every Python distribution in it.

.DESCRIPTION
    Input for docs/THIRD-PARTY-LICENSES.md (009 T083 / 010 T061). Runs scripts/image_licences.py
    inside a throwaway container without network (name, version, licence, URL from the package
    metadata). Used for the GPU images (generator, singer), whose extras CI never installs.
    No secrets are read or needed.

.PARAMETER Image
    The images (default stemstudio/generator:local, stemstudio/singer:local).

.PARAMETER Out
    Folder for the <repository>-<tag>.md files (default licence-report).

.EXAMPLE
    ./scripts/image-licences.ps1 -Image stemstudio/generator:local
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '', Justification = 'Developer script; console output is the UI.')]
[CmdletBinding()]
param(
    [string[]] $Image = @('stemstudio/generator:local', 'stemstudio/singer:local'),
    [string] $Out = 'licence-report'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script = Get-Content -Raw (Join-Path $PSScriptRoot 'image_licences.py')
New-Item -ItemType Directory -Force $Out | Out-Null
foreach ($name in $Image) {
    $file = Join-Path $Out (($name -replace '[/:]', '-') + '.md')
    $table = $script | & docker run --rm -i --network none --entrypoint python $name -I -
    if ($LASTEXITCODE -ne 0) { throw "licence report of $name failed with exit code $LASTEXITCODE" }
    Set-Content -Path $file -Value (@("# Python licences in $name", '') + $table)
    Write-Host "$name -> $file ($($table[-1]))"
}
