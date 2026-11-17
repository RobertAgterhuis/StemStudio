#requires -Version 7.0
<#
.SYNOPSIS
    Builds soundtouch.wasm (SoundTouch 2.4.1, LGPL-2.1, + the StemStudio shim; feature 012, ADR-021)
    exactly as pinned in third_party/soundtouch/soundtouch.lock.

.DESCRIPTION
    1. Downloads SOUNDTOUCH_URL into third_party/soundtouch/upstream/ (skipped when already there)
       and verifies SOUNDTOUCH_SHA256; a mismatching file is deleted and the build stops.
    2. Compiles with em++ (not emcc: C++ link) using EMCC_FLAGS over SOUNDTOUCH_SOURCES and
       shim/stemstudio_soundtouch.cpp, adding -ffile-prefix-map for the source and shim directories.
       Default: inside EMSDK_IMAGE@EMSDK_DIGEST with --network none (the container runs
       scripts/build-soundtouch.sh --local, the same compile as CI and the web image).
       -Local: with the em++ on PATH (a local Emscripten SDK of the lock's version; non-Docker path).
    3. Checks the module (no imports, every ABI export, st_abi_version() == ABI_VERSION) when node
       is available, writes OUTPUT (git-ignored) and prints its SHA-256.
    The LGPL distribution folder (-Package in the bash script) is produced by
    scripts/build-soundtouch.sh --package DIR (web image build).

.PARAMETER Local
    Compile with the em++ on PATH instead of the pinned Docker image.

.PARAMETER VerifyReproducible
    Docker build twice in fresh containers; fails unless both SHA-256 match.

.PARAMETER FetchOnly
    Download and verify the source archive only.

.PARAMETER CheckPins
    Fails unless docker/web.Dockerfile pins the lock's EMSDK_IMAGE@EMSDK_DIGEST.

.PARAMETER PrintConfig
    Prints the parsed lock values and exits.

.PARAMETER Lock
    Lock file (default third_party/soundtouch/soundtouch.lock); upstream/, build/, shim/ and
    include/ are taken from the lock's directory.

.PARAMETER ShimDir
    Directory with stemstudio_soundtouch.cpp (default <lock dir>/shim).

.PARAMETER Out
    Output file (default: OUTPUT of the lock, relative to the repository root).

.EXAMPLE
    pwsh scripts/build-soundtouch.ps1
.EXAMPLE
    pwsh scripts/build-soundtouch.ps1 -VerifyReproducible
.EXAMPLE
    pwsh scripts/build-soundtouch.ps1 -Local   # emsdk 6.0.13 activated in this shell (emsdk_env.ps1)
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '', Justification = 'Interactive developer script; console output is the UI.')]
[CmdletBinding()]
param(
    [switch] $Local,
    [switch] $VerifyReproducible,
    [switch] $FetchOnly,
    [switch] $CheckPins,
    [switch] $PrintConfig,
    [string] $Lock = '',
    [string] $ShimDir = '',
    [string] $Out = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$RequiredKeys = @('SOUNDTOUCH_VERSION', 'SOUNDTOUCH_URL', 'SOUNDTOUCH_SHA256', 'SOUNDTOUCH_ARCHIVE_NAME',
    'SOUNDTOUCH_ARCHIVE_ROOT', 'EMSDK_IMAGE', 'EMSDK_DIGEST', 'SOUNDTOUCH_SOURCES', 'EMCC_FLAGS', 'OUTPUT', 'ABI_VERSION')
$ShimFile = 'stemstudio_soundtouch.cpp'
$LockPath = [IO.Path]::GetFullPath($(if ($Lock) { $Lock } else { Join-Path $RepoRoot 'third_party/soundtouch/soundtouch.lock' }))
$ShimPath = if ($ShimDir) { [IO.Path]::GetFullPath($ShimDir) } else { '' }
$OutPath = if ($Out) { [IO.Path]::GetFullPath($Out) } else { '' }
$Switches = [ordered]@{ PrintConfig = $PrintConfig; CheckPins = $CheckPins; FetchOnly = $FetchOnly; Local = $Local; VerifyReproducible = $VerifyReproducible }
$Mode = @($Switches.Keys | Where-Object { $Switches[$_].IsPresent }) + @('Docker') | Select-Object -First 1
$AbiExports = 'memory _initialize st_abi_version st_create st_destroy st_set_pitch st_set_setting st_initial_latency st_put st_receive st_clear st_malloc st_free'

# Plain KEY=value lines; '#' starts a comment line; no quotes, no expansion (lock contract).
function Read-Lock([string] $Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "lock file not found: $Path" }
    $cfg = @{}
    foreach ($line in Get-Content -LiteralPath $Path) {
        if ($line -eq '' -or $line.StartsWith('#')) { continue }
        if ($line -notmatch '^([A-Z][A-Z0-9_]*)=(.*)$') { throw "invalid lock line: $line" }
        $cfg[$Matches[1]] = $Matches[2]
    }
    foreach ($key in $RequiredKeys) {
        if (-not $cfg[$key]) { throw "lock key missing or empty: $key ($Path)" }
    }
    $cfg
}

function Resolve-BuildPath([hashtable] $Cfg) {
    $lockDir = Split-Path -Parent $LockPath
    @{
        LockDir = $lockDir
        Shim    = if ($ShimPath) { $ShimPath } else { Join-Path $lockDir 'shim' }
        Out     = if ($OutPath) { $OutPath } else { Join-Path $RepoRoot $Cfg.OUTPUT }
        Archive = Join-Path $lockDir "upstream/$($Cfg.SOUNDTOUCH_ARCHIVE_NAME)"
        Build   = Join-Path $lockDir 'build'
    }
}

function Get-Sha256([string] $Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }

function Assert-Archive([hashtable] $Cfg, [hashtable] $P) {
    $actual = Get-Sha256 $P.Archive
    if ($actual -ne $Cfg.SOUNDTOUCH_SHA256) {
        Remove-Item -LiteralPath $P.Archive -Force
        throw "SHA-256 mismatch for $($Cfg.SOUNDTOUCH_ARCHIVE_NAME): expected $($Cfg.SOUNDTOUCH_SHA256), got $actual (file deleted)"
    }
    Write-Host "verified $($Cfg.SOUNDTOUCH_ARCHIVE_NAME) sha256=$actual"
}

function Get-Archive([hashtable] $Cfg, [hashtable] $P) {
    if (-not (Test-Path -LiteralPath $P.Archive)) {
        New-Item -ItemType Directory -Force (Split-Path -Parent $P.Archive) | Out-Null
        Write-Host "downloading $($Cfg.SOUNDTOUCH_URL)"
        $part = "$($P.Archive).part"
        Invoke-WebRequest -Uri $Cfg.SOUNDTOUCH_URL -OutFile $part -MaximumRetryCount 3 -SslProtocol Tls12, Tls13
        Move-Item -LiteralPath $part -Destination $P.Archive -Force
    }
    Assert-Archive $Cfg $P
}

function Assert-Shim([hashtable] $P) {
    $file = Join-Path $P.Shim $ShimFile
    if (-not (Test-Path -LiteralPath $file)) { throw "shim not found: $file (audio-engine, 012 T020)" }
}

function Get-RepoRelative([string] $Path) {
    $rel = [IO.Path]::GetRelativePath($RepoRoot, $Path)
    if ($rel.StartsWith('..') -or [IO.Path]::IsPathRooted($rel)) { return $null }
    $rel.Replace('\', '/')
}

# Runs scripts/build-soundtouch.sh --local inside the pinned image, offline; $RelOut is inside /work.
function Invoke-DockerBuild([hashtable] $Cfg, [hashtable] $P, [string] $RelOut) {
    $lockRel = Get-RepoRelative $LockPath
    if (-not $lockRel) { throw "Docker mode needs the lock inside the repository: $LockPath" }
    $dockerArgs = @('run', '--rm', '--network', 'none', '-v', "${RepoRoot}:/work", '-w', '/work')
    $shimRel = Get-RepoRelative $P.Shim
    $shim = if ($shimRel) { "/work/$shimRel" } else { '/shim' }
    if (-not $shimRel) { $dockerArgs += @('-v', "$($P.Shim):/shim:ro") }
    $dockerArgs += @("$($Cfg.EMSDK_IMAGE)@$($Cfg.EMSDK_DIGEST)", 'bash', 'scripts/build-soundtouch.sh', '--local',
        '--lock', "/work/$lockRel", '--shim-dir', $shim, '--out', "/work/$RelOut")
    & docker @dockerArgs
    if ($LASTEXITCODE -ne 0) { throw "docker build failed (exit $LASTEXITCODE)" }
}

function Copy-Output([string] $From, [string] $To) {
    New-Item -ItemType Directory -Force (Split-Path -Parent $To) | Out-Null
    Copy-Item -LiteralPath $From -Destination $To -Force
    Write-Host "wrote $To ($((Get-Item -LiteralPath $To).Length) bytes) sha256=$(Get-Sha256 $To)"
}

function Invoke-DockerMode([hashtable] $Cfg, [hashtable] $P) {
    $rel = Get-RepoRelative $P.Out
    if ($rel) { Invoke-DockerBuild $Cfg $P $rel; return }
    Invoke-DockerBuild $Cfg $P $Cfg.OUTPUT
    Copy-Output (Join-Path $RepoRoot $Cfg.OUTPUT) $P.Out
}

function Test-Reproducible([hashtable] $Cfg, [hashtable] $P) {
    $rel = Get-RepoRelative $P.Build
    Invoke-DockerBuild $Cfg $P "$rel/repro/first.wasm"
    Invoke-DockerBuild $Cfg $P "$rel/repro/second.wasm"
    $first = Get-Sha256 (Join-Path $P.Build 'repro/first.wasm')
    $second = Get-Sha256 (Join-Path $P.Build 'repro/second.wasm')
    if ($first -ne $second) { throw "not reproducible: first build $first, second build $second" }
    Write-Host "reproducible: both builds sha256=$first"
    Copy-Output (Join-Path $P.Build 'repro/first.wasm') $P.Out
}

# Fresh extraction for every build: no stale or locally edited upstream file is compiled.
function Expand-Source([hashtable] $Cfg, [hashtable] $P) {
    $src = Join-Path $P.Build 'src'
    if (Test-Path -LiteralPath $src) { Remove-Item -LiteralPath $src -Recurse -Force }
    New-Item -ItemType Directory -Force $src | Out-Null
    & tar -xzf $P.Archive -C $src
    if ($LASTEXITCODE -ne 0) { throw "tar failed (exit $LASTEXITCODE)" }
    $root = Join-Path $src $Cfg.SOUNDTOUCH_ARCHIVE_ROOT
    if (-not (Test-Path -LiteralPath (Join-Path $root 'source/SoundTouch'))) { throw "unexpected archive layout: $root" }
    $root
}

function Invoke-Compile([hashtable] $Cfg, [hashtable] $P, [string] $SrcRoot) {
    $srcDir = Join-Path $SrcRoot 'source/SoundTouch'
    $sources = $Cfg.SOUNDTOUCH_SOURCES -split ' ' | ForEach-Object { Join-Path $srcDir $_ }
    $wasm = Join-Path $P.Build 'out/soundtouch.wasm'
    New-Item -ItemType Directory -Force (Split-Path -Parent $wasm) | Out-Null
    $emArgs = @($Cfg.EMCC_FLAGS -split ' ') + @("-ffile-prefix-map=$srcDir=.", "-ffile-prefix-map=$($P.Shim)=.",
        '-I', (Join-Path $SrcRoot 'include'), '-I', (Join-Path $P.LockDir 'include')) + $sources + @((Join-Path $P.Shim $ShimFile), '-o', $wasm)
    & em++ @emArgs
    if ($LASTEXITCODE -ne 0) { throw "em++ failed (exit $LASTEXITCODE)" }
    $wasm
}

# Instantiates the module without imports and checks the ABI.
function Test-Module([hashtable] $Cfg, [string] $Wasm) {
    if (-not (Get-Command node -ErrorAction SilentlyContinue)) { Write-Warning 'node not found, module check skipped'; return }
    $js = 'const [f,a,n]=process.argv.slice(1);const m=new WebAssembly.Module(require("fs").readFileSync(f));' +
    'if(WebAssembly.Module.imports(m).length)throw new Error("module has imports");const e=new WebAssembly.Instance(m,{}).exports;' +
    'const miss=n.split(" ").filter(x=>!(x in e));if(miss.length)throw new Error("missing exports: "+miss);' +
    'e._initialize();if(e.st_abi_version()!==Number(a))throw new Error("st_abi_version() != "+a);console.log("module ok: no imports, ABI "+a)'
    & node -e $js $Wasm $Cfg.ABI_VERSION $AbiExports
    if ($LASTEXITCODE -ne 0) { throw 'module check failed' }
}

function Invoke-LocalMode([hashtable] $Cfg, [hashtable] $P) {
    Write-Host "em++ $((& em++ --version) | Select-Object -First 1)"
    $wasm = Invoke-Compile $Cfg $P (Expand-Source $Cfg $P)
    Test-Module $Cfg $wasm
    Copy-Output $wasm $P.Out
}

function Test-Pin([hashtable] $Cfg) {
    $pin = "$($Cfg.EMSDK_IMAGE)@$($Cfg.EMSDK_DIGEST)"
    $dockerfile = Join-Path $RepoRoot 'docker/web.Dockerfile'
    if (-not (Select-String -LiteralPath $dockerfile -SimpleMatch "ARG EMSDK_IMAGE=$pin" -Quiet)) {
        throw "docker/web.Dockerfile must pin 'ARG EMSDK_IMAGE=$pin' (soundtouch.lock)"
    }
    Write-Host "pins ok: docker/web.Dockerfile uses $pin"
}

function Write-Config([hashtable] $Cfg, [hashtable] $P) {
    $RequiredKeys | ForEach-Object { Write-Output "$_=$($Cfg[$_])" }
    Write-Output "ARCHIVE=$($P.Archive)"
    Write-Output "SHIM_DIR=$($P.Shim)"
    Write-Output "OUT=$($P.Out)"
}

function Invoke-Build([hashtable] $Cfg, [hashtable] $P) {
    Assert-Shim $P
    Get-Archive $Cfg $P
    switch ($Mode) {
        'Local' { Invoke-LocalMode $Cfg $P }
        'VerifyReproducible' { Test-Reproducible $Cfg $P }
        default { Invoke-DockerMode $Cfg $P }
    }
}

function Invoke-Main {
    $cfg = Read-Lock $LockPath
    $paths = Resolve-BuildPath $cfg
    switch ($Mode) {
        'PrintConfig' { Write-Config $cfg $paths }
        'CheckPins' { Test-Pin $cfg }
        'FetchOnly' { Get-Archive $cfg $paths }
        default { Invoke-Build $cfg $paths }
    }
}

try {
    Invoke-Main
}
catch {
    Write-Error "build-soundtouch: $($_.Exception.Message)" -ErrorAction Continue
    exit 1
}
