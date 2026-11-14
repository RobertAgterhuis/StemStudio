#requires -Version 7.0
<#
.SYNOPSIS
    Runs every component's lint, test and coverage gate locally (the same commands as CI).

.DESCRIPTION
    .NET (build, format, tests with the per-component 90 % line+branch gate), worker (ruff, mypy,
    pytest with coverage), audio engine (lint, typecheck, coverage, build), contracts (Redocly),
    quality tools, fixture generator and the E2E lint. Components whose folder does not exist yet
    are skipped. Prints a summary and exits 1 when any step failed.

.PARAMETER ContinueOnError
    Runs all steps even after a failure (default: stop at the first failure).

.EXAMPLE
    ./scripts/test-all.ps1
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '', Justification = 'Interactive developer script; console output is the UI.')]
[CmdletBinding()]
param([switch] $ContinueOnError)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$Redocly = '@redocly/cli@2.78.0'
$Contracts = 'specs/001-stemstudio-mvp/contracts'

# Each step: component folder (relative to the repo; skipped when missing), working dir, command.
$Steps = @(
    @{ Name = 'dotnet tool restore'; Needs = 'StemStudio.slnx'; Dir = '.'; Cmd = @('dotnet', 'tool', 'restore') }
    @{ Name = 'dotnet build'; Needs = 'StemStudio.slnx'; Dir = '.'; Cmd = @('dotnet', 'build', 'StemStudio.slnx', '-c', 'Release') }
    @{ Name = 'dotnet format'; Needs = 'StemStudio.slnx'; Dir = '.'; Cmd = @('dotnet', 'format', 'StemStudio.slnx', '--verify-no-changes') }
    @{ Name = 'dotnet test + coverage gate'; Needs = 'StemStudio.slnx'; Dir = '.'; Cmd = @('dotnet', 'test', 'StemStudio.slnx', '-c', 'Release', '--no-build', '-p:CollectCoverage=true') }
    @{ Name = 'worker uv sync'; Needs = 'worker'; Dir = 'worker'; Cmd = @('uv', 'sync', '--frozen') }
    @{ Name = 'worker ruff check'; Needs = 'worker'; Dir = 'worker'; Cmd = @('uv', 'run', '--frozen', 'ruff', 'check', '.') }
    @{ Name = 'worker ruff format'; Needs = 'worker'; Dir = 'worker'; Cmd = @('uv', 'run', '--frozen', 'ruff', 'format', '--check', '.') }
    @{ Name = 'worker mypy'; Needs = 'worker'; Dir = 'worker'; Cmd = @('uv', 'run', '--frozen', 'mypy', 'src', 'tests') }
    @{ Name = 'worker pytest + coverage'; Needs = 'worker'; Dir = 'worker'; Cmd = @('uv', 'run', '--frozen', 'pytest', '--cov', '--cov-branch', '--cov-report=term', '--cov-fail-under=90') }
    @{ Name = 'engine npm ci'; Needs = 'src/audio-engine'; Dir = 'src/audio-engine'; Cmd = @('npm', 'ci', '--no-audit', '--no-fund') }
    @{ Name = 'engine lint'; Needs = 'src/audio-engine'; Dir = 'src/audio-engine'; Cmd = @('npm', 'run', 'lint') }
    @{ Name = 'engine typecheck'; Needs = 'src/audio-engine'; Dir = 'src/audio-engine'; Cmd = @('npm', 'run', 'typecheck') }
    @{ Name = 'engine test:coverage'; Needs = 'src/audio-engine'; Dir = 'src/audio-engine'; Cmd = @('npm', 'run', 'test:coverage') }
    @{ Name = 'engine build'; Needs = 'src/audio-engine'; Dir = 'src/audio-engine'; Cmd = @('npm', 'run', 'build') }
    @{ Name = 'contracts lint'; Needs = $Contracts; Dir = '.'; Cmd = @('npx', '--yes', $Redocly, 'lint', '--config', "$Contracts/redocly.yaml", "$Contracts/worker-api.openapi.yaml", "$Contracts/backend-api.openapi.yaml") }
    @{ Name = 'quality tools ruff'; Needs = 'tools/quality'; Dir = '.'; Cmd = @('uv', 'run', '--project', 'worker', '--frozen', 'ruff', 'check', 'tools/quality') }
    @{ Name = 'quality tools mypy'; Needs = 'tools/quality'; Dir = '.'; Cmd = @('uv', 'run', '--project', 'worker', '--frozen', '--with-requirements', 'tools/quality/requirements.txt', 'mypy', '--strict', '--config-file', 'tools/quality/pyproject.toml', 'tools/quality') }
    @{ Name = 'quality tools pytest'; Needs = 'tools/quality'; Dir = '.'; Cmd = @('uv', 'run', '--project', 'worker', '--frozen', '--with-requirements', 'tools/quality/requirements.txt', 'pytest', 'tools/quality/tests', '--cov=tools/quality', '--cov-branch', '--cov-config=tools/quality/pyproject.toml', '--cov-fail-under=90') }
    @{ Name = 'quality run_all'; Needs = 'tools/quality'; Dir = '.'; Cmd = @('uv', 'run', '--project', 'worker', '--frozen', '--with-requirements', 'tools/quality/requirements.txt', 'python', 'tools/quality/run_all.py') }
    @{ Name = 'fixtures ruff'; Needs = 'tests/fixtures'; Dir = '.'; Cmd = @('uv', 'run', '--project', 'worker', '--frozen', 'ruff', 'check', 'tests/fixtures') }
    @{ Name = 'fixtures mypy'; Needs = 'tests/fixtures'; Dir = '.'; Cmd = @('uv', 'run', '--project', 'worker', '--frozen', 'mypy', '--strict', 'tests/fixtures') }
    @{ Name = 'fixtures pytest'; Needs = 'tests/fixtures/tests'; Dir = '.'; Cmd = @('uv', 'run', '--project', 'worker', '--frozen', 'pytest', 'tests/fixtures/tests', '--cov=tests/fixtures', '--cov-branch', '--cov-fail-under=90') }
    @{ Name = 'e2e npm ci'; Needs = 'e2e'; Dir = 'e2e'; Cmd = @('npm', 'ci', '--no-audit', '--no-fund') }
    @{ Name = 'e2e lint'; Needs = 'e2e'; Dir = 'e2e'; Cmd = @('npm', 'run', 'lint') }
)

function Invoke-Step {
    <# Runs one step; returns 'passed', 'failed' or 'skipped'. #>
    param([hashtable] $Step)
    if (-not (Test-Path (Join-Path $RepoRoot $Step.Needs))) { return 'skipped' }
    Write-Host "`n=== $($Step.Name) ===" -ForegroundColor Cyan
    Push-Location (Join-Path $RepoRoot $Step.Dir)
    try {
        $command = $Step.Cmd[0]
        $arguments = @($Step.Cmd | Select-Object -Skip 1)
        & $command @arguments | Out-Host
        if ($LASTEXITCODE -eq 0) { 'passed' } else { 'failed' }
    }
    finally { Pop-Location }
}

$summary = [System.Collections.Generic.List[object]]::new()
foreach ($step in $Steps) {
    $result = Invoke-Step $step
    $summary.Add([pscustomobject]@{ Step = $step.Name; Result = $result })
    if ($result -eq 'failed' -and -not $ContinueOnError) { break }
}
$summary | Format-Table -AutoSize | Out-String | Write-Host
exit ([int]($summary.Result -contains 'failed'))
