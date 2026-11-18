#requires -Version 7.0
<#
.SYNOPSIS
    Checks that a running StemStudio web serves the audio engine and its AudioWorklet modules
    (feature 016 T011, engine contract 2.6.0, ADR-032).

.DESCRIPTION
    PowerShell twin of scripts/check-web-assets.sh (same checks and messages):
    /js/audio-engine.js, /js/time-stretch-processor.js, /js/audio-engine-capture-worklet.js and
    /js/audio-engine-pitch-worklet.js are 200 text/javascript, and the pitch module registers the
    processor "stemstudio-pitch-tracker" (the real build of vite.pitch.config.ts).
    Exit code 0 when all checks pass, 1 when one fails (all failures are listed).

.PARAMETER BaseUrl
    Web base URL (default http://localhost:8080).

.EXAMPLE
    pwsh scripts/check-web-assets.ps1 http://localhost:8080
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '', Justification = 'Interactive developer script; console output is the UI.')]
[CmdletBinding()]
param(
    [string] $BaseUrl = 'http://localhost:8080'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$Base = $BaseUrl.TrimEnd('/')
$Modules = @('audio-engine.js', 'time-stretch-processor.js', 'audio-engine-capture-worklet.js', 'audio-engine-pitch-worklet.js')
$PitchProcessor = 'stemstudio-pitch-tracker'
$script:Failures = 0

function Write-Fail([string] $Message) { Write-Host "FAIL $Message"; $script:Failures++ }
function Write-Pass([string] $Message) { Write-Host "ok   $Message" }

# Returns @{ Status; Type; Text } (Status 0 when the server cannot be reached).
function Get-Served([string] $Path) {
    try {
        $r = Invoke-WebRequest -Uri "$Base/$Path" -SkipHttpErrorCheck -UseBasicParsing
        $text = if ($r.Content -is [byte[]]) { [Text.Encoding]::UTF8.GetString($r.Content) } else { "$($r.Content)" }
        @{ Status = [int]$r.StatusCode; Type = "$($r.Headers['Content-Type'])"; Text = $text }
    }
    catch { @{ Status = 0; Type = '-'; Text = '' } }
}

Write-Host "checking $Base"
foreach ($name in $Modules) {
    $path = "js/$name"
    $r = Get-Served $path
    if ($r.Status -ne 200 -or -not $r.Type.StartsWith('text/javascript')) {
        Write-Fail "${path}: expected 200 text/javascript, got $($r.Status) $($r.Type)"
        continue
    }
    Write-Pass "$path ($($r.Status) $($r.Type))"
    if ($name -eq 'audio-engine-pitch-worklet.js') {
        if ($r.Text.Contains('registerProcessor') -and $r.Text.Contains($PitchProcessor)) { Write-Pass "$path registers $PitchProcessor" }
        else { Write-Fail "$path does not register $PitchProcessor" }
    }
}
if ($script:Failures -gt 0) { Write-Host "$($script:Failures) check(s) failed"; exit 1 }
Write-Host 'all web asset checks passed'
