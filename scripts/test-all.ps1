#requires -Version 7.0
<#
.SYNOPSIS
    Runs every component's lint, test and coverage gate locally (the same commands as CI).

.DESCRIPTION
    .NET (build, format, tests with the per-component 90 % line+branch gate), worker (ruff, mypy,
    pytest with coverage), audio engine and web-interop (lint, typecheck, coverage, build), contracts (Redocly),
    quality gate (self-checks + SIG checks), fixture generator (checks + generation) and the E2E
    suite (lint, typecheck, list). The E2E run itself needs the compose stack (see README / CI).
    Prints a summary and exits 1 when any step failed.

.PARAMETER ContinueOnError
    Runs all steps even after a failure (default: stop at the first failure).

.PARAMETER Gpu
    Also starts a fake stack as compose project stemstudio-e2e twice, with a fake GPU
    (STEMSTUDIO_FAKE_DEVICE=gpu) and one that runs out of memory (gpu-oom), and runs the Playwright
    @gpu specs each time (feature 003). Needs ports
    8080/8081 free and the Playwright browsers installed; the project and its volumes are removed
    afterwards.

.EXAMPLE
    ./scripts/test-all.ps1
.EXAMPLE
    ./scripts/test-all.ps1 -Gpu
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '', Justification = 'Interactive developer script; console output is the UI.')]
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '', Justification = 'Private helpers that only change this script''s own throwaway containers or process environment.')]
[CmdletBinding()]
param([switch] $ContinueOnError, [switch] $Gpu)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$Redocly = '@redocly/cli@2.78.0'
$Contracts = 'specs/003-gpu-acceleration/contracts'

# Each step: name, working dir (relative to the repo root), command. Every component is merged, so a
# missing folder fails the step instead of skipping it. Commands match .github/workflows/ci.yml.
$Fx = 'tests/fixtures/pyproject.toml'
$Steps = @(
    @{ Name = 'dotnet tool restore'; Dir = '.'; Cmd = @('dotnet', 'tool', 'restore') }
    @{ Name = 'dotnet build'; Dir = '.'; Cmd = @('dotnet', 'build', 'StemStudio.slnx', '-c', 'Release') }
    @{ Name = 'dotnet format'; Dir = '.'; Cmd = @('dotnet', 'format', 'StemStudio.slnx', '--verify-no-changes') }
    @{ Name = 'dotnet test + coverage gate'; Dir = '.'; Cmd = @('dotnet', 'test', 'StemStudio.slnx', '-c', 'Release', '--no-build', '-p:CollectCoverage=true') }
    @{ Name = 'worker uv sync'; Dir = 'worker'; Cmd = @('uv', 'sync', '--frozen') }
    @{ Name = 'worker ruff check'; Dir = 'worker'; Cmd = @('uv', 'run', '--frozen', 'ruff', 'check', '.') }
    @{ Name = 'worker ruff format'; Dir = 'worker'; Cmd = @('uv', 'run', '--frozen', 'ruff', 'format', '--check', '.') }
    @{ Name = 'worker mypy'; Dir = 'worker'; Cmd = @('uv', 'run', '--frozen', 'mypy', 'src', 'tests') }
    @{ Name = 'worker pytest + coverage'; Dir = 'worker'; Cmd = @('uv', 'run', '--frozen', 'pytest', '--cov', '--cov-branch', '--cov-report=term', '--cov-fail-under=90') }
    @{ Name = 'engine npm ci'; Dir = 'src/audio-engine'; Cmd = @('npm', 'ci', '--no-audit', '--no-fund') }
    @{ Name = 'engine lint'; Dir = 'src/audio-engine'; Cmd = @('npm', 'run', 'lint') }
    @{ Name = 'engine typecheck'; Dir = 'src/audio-engine'; Cmd = @('npm', 'run', 'typecheck') }
    @{ Name = 'engine test:coverage'; Dir = 'src/audio-engine'; Cmd = @('npm', 'run', 'test:coverage') }
    @{ Name = 'engine build'; Dir = 'src/audio-engine'; Cmd = @('npm', 'run', 'build') }
    @{ Name = 'web-interop npm ci'; Dir = 'src/web-interop'; Cmd = @('npm', 'ci', '--no-audit', '--no-fund') }
    @{ Name = 'web-interop lint'; Dir = 'src/web-interop'; Cmd = @('npm', 'run', 'lint') }
    @{ Name = 'web-interop typecheck'; Dir = 'src/web-interop'; Cmd = @('npm', 'run', 'typecheck') }
    @{ Name = 'web-interop test:coverage'; Dir = 'src/web-interop'; Cmd = @('npm', 'run', 'test:coverage') }
    @{ Name = 'web-interop build'; Dir = 'src/web-interop'; Cmd = @('npm', 'run', 'build') }
    @{ Name = 'contracts lint'; Dir = '.'; Cmd = @('npx', '--yes', $Redocly, 'lint', '--config', "$Contracts/redocly.yaml", "$Contracts/worker-api.openapi.yaml", "$Contracts/backend-api.openapi.yaml") }
    @{ Name = 'quality ruff check'; Dir = 'tools/quality'; Cmd = @('uv', 'run', '--frozen', 'ruff', 'check', '.') }
    @{ Name = 'quality ruff format'; Dir = 'tools/quality'; Cmd = @('uv', 'run', '--frozen', 'ruff', 'format', '--check', '.') }
    @{ Name = 'quality mypy'; Dir = 'tools/quality'; Cmd = @('uv', 'run', '--frozen', 'mypy') }
    @{ Name = 'quality pytest + coverage'; Dir = 'tools/quality'; Cmd = @('uv', 'run', '--frozen', 'pytest', '--cov', '--cov-branch', '--cov-report=term') }
    @{ Name = 'quality gate (run_all)'; Dir = '.'; Cmd = @('uv', 'run', '--project', 'tools/quality', '--frozen', 'python', 'tools/quality/run_all.py') }
    @{ Name = 'fixtures ruff check'; Dir = '.'; Cmd = @('uv', 'run', '--project', 'worker', '--frozen', 'ruff', 'check', 'tests/fixtures') }
    @{ Name = 'fixtures ruff format'; Dir = '.'; Cmd = @('uv', 'run', '--project', 'worker', '--frozen', 'ruff', 'format', '--check', 'tests/fixtures') }
    @{ Name = 'fixtures mypy'; Dir = '.'; Cmd = @('uv', 'run', '--project', 'worker', '--frozen', 'mypy', '--config-file', $Fx, 'tests/fixtures') }
    @{ Name = 'fixtures pytest + coverage'; Dir = '.'; Cmd = @('uv', 'run', '--project', 'worker', '--frozen', 'pytest', '-c', $Fx, '--cov', '--cov-branch', "--cov-config=$Fx", 'tests/fixtures/tests') }
    @{ Name = 'fixtures generate'; Dir = '.'; Cmd = @('uv', 'run', '--project', 'worker', '--frozen', 'python', 'tests/fixtures/generate_fixtures.py') }
    @{ Name = 'e2e npm ci'; Dir = 'e2e'; Cmd = @('npm', 'ci', '--no-audit', '--no-fund') }
    @{ Name = 'e2e lint'; Dir = 'e2e'; Cmd = @('npm', 'run', 'lint') }
    @{ Name = 'e2e typecheck'; Dir = 'e2e'; Cmd = @('npm', 'run', 'typecheck') }
    @{ Name = 'e2e list'; Dir = 'e2e'; Cmd = @('npx', 'playwright', 'test', '--list') }
)
# Optional (-Gpu): two passes of the fake stack (fake GPU, then a fake GPU that runs out of memory),
# each followed by the @gpu specs (003); the mode goes to compose (worker env) and to Playwright.
$E2eProject = 'stemstudio-e2e'
if ($Gpu) {
    foreach ($mode in 'gpu', 'gpu-oom') {
        $gpuEnv = @{ STEMSTUDIO_SEPARATOR = 'fake'; STEMSTUDIO_FAKE_DEVICE = $mode }
        $Steps += @{ Name = "gpu e2e: fake stack ($mode) up"; Dir = '.'; Env = $gpuEnv; Cmd = @('docker', 'compose', '-p', $E2eProject, 'up', '-d', '--build', '--wait', '--wait-timeout', '300') }
        $Steps += @{ Name = "gpu e2e: playwright @gpu ($mode)"; Dir = 'e2e'; Env = $gpuEnv; Cmd = @('npm', 'run', 'test:gpu') }
    }
}

function Set-StepEnvironment {
    <# Sets the given process environment variables; returns their previous values. #>
    param([hashtable] $Variables)
    $previous = @{}
    foreach ($name in $Variables.Keys) {
        $previous[$name] = [Environment]::GetEnvironmentVariable($name)
        [Environment]::SetEnvironmentVariable($name, $Variables[$name])
    }
    $previous
}

function Invoke-Step {
    <# Runs one step; returns 'passed' or 'failed' (a missing working directory fails). #>
    param([hashtable] $Step)
    if (-not (Test-Path (Join-Path $RepoRoot $Step.Dir))) { return 'failed' }
    Write-Host "`n=== $($Step.Name) ===" -ForegroundColor Cyan
    Push-Location (Join-Path $RepoRoot $Step.Dir)
    $previous = if ($Step.ContainsKey('Env')) { Set-StepEnvironment $Step.Env } else { @{} }
    try {
        $command = $Step.Cmd[0]
        $arguments = @($Step.Cmd | Select-Object -Skip 1)
        & $command @arguments | Out-Host
        if ($LASTEXITCODE -eq 0) { 'passed' } else { 'failed' }
    }
    finally {
        Set-StepEnvironment $previous | Out-Null
        Pop-Location
    }
}

$summary = [System.Collections.Generic.List[object]]::new()
foreach ($step in $Steps) {
    $result = Invoke-Step $step
    $summary.Add([pscustomobject]@{ Step = $step.Name; Result = $result })
    if ($result -eq 'failed' -and -not $ContinueOnError) { break }
}
if ($Gpu) { & docker compose -p $E2eProject down -v | Out-Host }
$summary | Format-Table -AutoSize | Out-String | Write-Host
exit ([int]($summary.Result -contains 'failed'))
