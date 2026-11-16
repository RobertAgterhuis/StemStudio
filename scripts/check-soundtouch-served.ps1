#requires -Version 7.0
<#
.SYNOPSIS
    Checks that a running StemStudio web serves the SoundTouch module and its LGPL-2.1 distribution
    folder completely and unchanged (feature 012 T067, FR-006, ADR-021 D7).

.DESCRIPTION
    PowerShell twin of scripts/check-soundtouch-served.sh (same checks and messages):
    /js/soundtouch/soundtouch.wasm is 200 application/wasm (with -ExpectWasm byte-identical to that
    file); /third-party/soundtouch/index.html is 200 text/html; SHA256SUMS lists the licence,
    notice, readme, lock, the archive pinned in soundtouch.lock, header, shim and both build
    scripts, and every listed file is served with exactly that SHA-256.
    Exit code 0 when all checks pass, 1 when one fails (all failures are listed).

.PARAMETER BaseUrl
    Web base URL (default http://localhost:8080).

.PARAMETER ExpectWasm
    Local soundtouch.wasm the served module must equal (e.g. a scripts/build-soundtouch.ps1 build).

.PARAMETER Lock
    Lock file (default third_party/soundtouch/soundtouch.lock).

.EXAMPLE
    pwsh scripts/check-soundtouch-served.ps1 -ExpectWasm src/StemStudio.Web/wwwroot/js/soundtouch/soundtouch.wasm
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '', Justification = 'Interactive developer script; console output is the UI.')]
[CmdletBinding()]
param(
    [string] $BaseUrl = 'http://localhost:8080',
    [string] $ExpectWasm = '',
    [string] $Lock = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$LockPath = if ($Lock) { $Lock } else { Join-Path $RepoRoot 'third_party/soundtouch/soundtouch.lock' }
$Base = $BaseUrl.TrimEnd('/')
$ExpectPath = $ExpectWasm
$script:Failures = 0

function Write-Fail([string] $Message) { Write-Host "FAIL $Message"; $script:Failures++ }
function Write-Pass([string] $Message) { Write-Host "ok   $Message" }
function Get-LockValue([string] $Key) { (Select-String -LiteralPath $LockPath -Pattern "^$Key=(.*)$").Matches[0].Groups[1].Value.Trim() }
function Get-ByteSha256([byte[]] $Bytes) { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Bytes)).ToLowerInvariant() }

# Returns @{ Status; Type; Bytes } (Status 0 when the server cannot be reached).
function Get-Served([string] $Path) {
    try {
        $r = Invoke-WebRequest -Uri "$Base/$Path" -SkipHttpErrorCheck -UseBasicParsing
        $bytes = if ($r.Content -is [byte[]]) { $r.Content } else { [Text.Encoding]::UTF8.GetBytes($r.Content) }
        @{ Status = [int]$r.StatusCode; Type = "$($r.Headers['Content-Type'])"; Bytes = $bytes }
    }
    catch { @{ Status = 0; Type = '-'; Bytes = [byte[]]@() } }
}

function Test-Served([string] $Path, [string] $Type) {
    $r = Get-Served $Path
    if ($r.Status -eq 200 -and $r.Type.StartsWith($Type)) { Write-Pass "$Path ($($r.Status) $($r.Type))" }
    else { Write-Fail "${Path}: expected 200 $Type, got $($r.Status) $($r.Type)" }
    $r
}

function Test-Wasm {
    $r = Test-Served 'js/soundtouch/soundtouch.wasm' 'application/wasm'
    if (-not $ExpectPath) { return }
    $expected = Get-ByteSha256 ([IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $ExpectPath)))
    if ((Get-ByteSha256 $r.Bytes) -eq $expected) { Write-Pass "soundtouch.wasm equals $ExpectPath" } else { Write-Fail "soundtouch.wasm differs from $ExpectPath" }
}

function Test-Required([Collections.IDictionary] $Sums) {
    $archive = Get-LockValue 'SOUNDTOUCH_ARCHIVE_NAME'
    $names = 'COPYING.TXT', 'NOTICE.md', 'README.md', 'soundtouch.lock', $archive, 'include/stemstudio_soundtouch.h',
    'shim/stemstudio_soundtouch.cpp', 'scripts/build-soundtouch.sh', 'scripts/build-soundtouch.ps1'
    foreach ($name in $names) { if (-not $Sums.Contains($name)) { Write-Fail "SHA256SUMS does not list $name" } }
    if ($Sums[$archive] -ne (Get-LockValue 'SOUNDTOUCH_SHA256')) { Write-Fail "$archive is not the archive pinned in soundtouch.lock" }
}

function Test-ListedFile([string] $Name, [string] $Digest) {
    $r = Get-Served "third-party/soundtouch/$Name"
    if ($r.Status -ne 200) { Write-Fail "third-party/soundtouch/${Name}: HTTP $($r.Status)"; return }
    if ((Get-ByteSha256 $r.Bytes) -eq $Digest) { Write-Pass "third-party/soundtouch/$Name sha256" }
    else { Write-Fail "third-party/soundtouch/${Name}: SHA-256 differs from SHA256SUMS" }
}

function Test-Distribution {
    Test-Served 'third-party/soundtouch/index.html' 'text/html' | Out-Null
    $r = Get-Served 'third-party/soundtouch/SHA256SUMS'
    if ($r.Status -ne 200) { Write-Fail "third-party/soundtouch/SHA256SUMS: HTTP $($r.Status)"; return }
    $sums = [ordered]@{}
    foreach ($line in ([Text.Encoding]::UTF8.GetString($r.Bytes) -split "`r?`n" | Where-Object { $_ })) {
        $digest, $name = $line -split '  ', 2
        $sums[$name] = $digest
    }
    Test-Required $sums
    foreach ($name in $sums.Keys) { Test-ListedFile $name $sums[$name] }
}

if (-not (Test-Path -LiteralPath $LockPath)) { Write-Error "lock file not found: $LockPath" -ErrorAction Continue; exit 2 }
Write-Host "checking $Base"
Test-Wasm
Test-Distribution
if ($script:Failures -gt 0) { Write-Host "$script:Failures check(s) failed"; exit 1 }
Write-Host 'all soundtouch checks passed'
