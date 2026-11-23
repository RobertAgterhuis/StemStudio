#requires -Version 7.0
<#
.SYNOPSIS
    Runs every component's lint, test and coverage gate locally (the same commands as CI).

.DESCRIPTION
    .NET (build, format, tests with the per-component 90 % line+branch gate), worker and generator
    (ruff, mypy, pytest + coverage; the generator's base install only, no torch), audio engine and web-interop (lint, typecheck, coverage, build), contracts (Redocly, render vectors),
    secret scan (no Claude key or key value in the repo, feature 011), key volume checks (011 claude-key: tests + static compose check),
    quality gate (self-checks + SIG checks), public manuals check (docs/public up to date), website build + check (tools/site), fixture generator (checks + generation) and the E2E
    suite (lint, typecheck, list); performance budgets (Category=Perf, uninstrumented) and the 022/023 contracts
    (Redocly, merge_full.py --check, output schema, merge tests); feature 012: the 012 contract lint + superset check, the soundtouch
    emsdk pin check and, when soundtouch.wasm has been built, the engine's Node WASM suite (test:wasm). The E2E run itself needs the compose stack (see README / CI).
    Prints a summary and exits 1 when any step failed.

.PARAMETER ContinueOnError
    Runs all steps even after a failure (default: stop at the first failure).

.PARAMETER Affected
    Runs only the step groups a change needs (refactor proposal 001): scripts/affected_select.py maps the
    files changed since -From (default: the merge-base with main), uncommitted and untracked files
    included, through scripts/affected/components.json and narrows the .NET projects with dotnet-affected;
    it prints which groups and projects run or are skipped and why. An unmapped file, an always-build file
    (.claude/tipoca/affected/always-build.txt), a missing base or a tool error runs everything. For lane
    checkpoints only: integration and pre-merge runs stay FULL (no switch).

.PARAMETER From
    Base ref for -Affected (default: the merge-base of HEAD with main).

.PARAMETER Gpu
    Also starts a fake stack as compose project stemstudio-e2e twice, with a fake GPU
    (STEMSTUDIO_FAKE_DEVICE=gpu) and one that runs out of memory (gpu-oom), and runs the Playwright
    @gpu specs each time (feature 003). Needs ports
    8080/8081 free and the Playwright browsers installed; the project and its volumes are removed
    afterwards.

.PARAMETER AgentE2e
    Also starts a fake stack (STEMSTUDIO_AGENT_BACKEND=none) as compose project stemstudio-e2e and
    runs scripts/agent-e2e.ps1 fake, then none (feature 011, T059). Same prerequisites as -Gpu; the
    project and its volumes are removed afterwards.

.PARAMETER RecordingE2e
    Also generates the 015 capture fixtures, starts a fake stack with the e2e override (web
    StemStudio__Recording__TestLoopback=true) as compose project stemstudio-e2e and runs
    npm run test:recording (project recording, full Chromium build, E2E_RECORDING_LOOPBACK=1;
    feature 015, T010). Same prerequisites as -Gpu; the project and its volumes are removed afterwards.

.EXAMPLE
    ./scripts/test-all.ps1
.EXAMPLE
    ./scripts/test-all.ps1 -Gpu
.EXAMPLE
    ./scripts/test-all.ps1 -Affected -From origin/main
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '', Justification = 'Interactive developer script; console output is the UI.')]
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '', Justification = 'Private helpers that only change this script''s own throwaway containers or process environment.')]
[CmdletBinding()]
param([switch] $ContinueOnError, [switch] $Gpu, [switch] $AgentE2e, [switch] $RecordingE2e, [switch] $Affected, [string] $From)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$Redocly = '@redocly/cli@2.78.0'
# Round 014+015: the newest full backend document is 015's 1.14.0 (= 014's 1.13.0 + the recording
# delta); the worker document is 014's 1.8.0. Render vectors 1.1.0 (007's + the take cases) are 015's.
$Contracts = 'specs/015-play-along-recording/contracts'
# Feature 016: backend 1.15.0, worker 1.9.0, both deltas, merge_full.py and the chord spelling vectors 1.0.0.
$InstrumentContracts = 'specs/016-instrument-views/contracts'
$RenderVectors = 'specs/015-play-along-recording/contracts/generate_render_vectors.py'
$PracticeFlowContracts = 'specs/014-practice-flow/contracts'  # backend 1.13.0, worker 1.8.0 and both deltas
# Features 009/010 (generator 1.0.0/1.1.0, backend 1.8.0/1.9.0); 010's documents contain 009's.
$GenerativeContracts = @('specs/009-style-transfer/contracts', 'specs/010-lyrics-to-song/contracts')
# Feature 011 (backend 1.10.0 = 010's 1.9.0 + the agent delta); worker stays 008's, generator 010's.
$AgentContracts = 'specs/011-ai-composer-agent/contracts'
# 011 follow-up claude-key: full backend 1.21.0 (= 1.10.0 + claude-key delta.2) and the delta itself.
$ClaudeKeyContracts = 'specs/011-ai-composer-agent/followups/claude-key/contracts'
# Feature 012 (backend 1.11.0 = 1.21.0 + the practice settings); superset check needs PyYAML only.
$PracticeContracts = 'specs/012-practice-tempo/contracts'
# Feature 013 (backend 1.12.0 = 1.11.0 + library care, worker 1.7.0); backup fixture tools have their own pyproject.
$LibraryCareContracts = 'specs/013-library-care/contracts'
$BackupFx = 'tests/fixtures/backup/pyproject.toml'
$PyYamlPython = @('uv', 'run', '--no-project', '--with', 'pyyaml==6.0.3', 'python', '-I')
# Features 022 (backend 1.22.0 = 1.12.0 + library folders) and 023 (backend 1.23.0 = 1.22.0 + lyric assistant):
# full document + delta, merge_full.py (freshness, superset), 023 output schema check and merge tests.
$LibraryFoldersContracts = 'specs/022-library-folders/contracts'
$LyricAssistContracts = 'specs/023-lyric-writing-assistant/contracts'
$JsonSchemaPython = @('uv', 'run', '--no-project', '--with', 'jsonschema==4.26.0', 'python', '-I')  # as worker/uv.lock
$PytestPython = @('uv', 'run', '--no-project', '--with', 'pyyaml==6.0.3', '--with', 'pytest==9.2.0', 'python', '-m', 'pytest')
# SoundTouch module (012): built by scripts/build-soundtouch.ps1; test:wasm runs only when it exists.
$SoundTouchWasm = 'src/StemStudio.Web/wwwroot/js/soundtouch/soundtouch.wasm'
$HasSoundTouchWasm = Test-Path -LiteralPath (Join-Path $RepoRoot $SoundTouchWasm)
$ScriptsCfg = 'scripts/pyproject.toml'  # tested devops checks (key_volume_checks.py, build-soundtouch tests), quality-gate env
$KeyChecks = @('scripts/key_volume_checks.py', 'scripts/backup_folder_checks.py', 'scripts/api_image_size_checks.py', 'scripts/affected_select.py', 'scripts/loopback_ports_checks.py', 'scripts/tests')
$QualityTools = @('uv', 'run', '--project', 'tools/quality', '--frozen')
# Static key volume check (011 claude-key) for every compose combination CI validates.
$ComposeCombos = @(
    , @('docker-compose.yml')
    , @('docker-compose.yml', 'docker-compose.gpu.yml')
    , @('docker-compose.yml', 'docker-compose.gpu.yml', 'docker-compose.generative.yml')
    , @('docker-compose.yml', 'docker-compose.generative.yml', 'docker-compose.generative-fake.yml')
)
$AgentCoverage = 'tests/StemStudio.Agent.Tests/TestResults/coverage/coverage.cobertura.xml'
$AgentEvalCoverage = 'tests/StemStudio.AgentEval.Tests/TestResults/coverage/coverage.cobertura.xml'
$WorkerPython = @('uv', 'run', '--project', 'worker', '--frozen', 'python')
if ($From -and -not $Affected) { throw '-From needs -Affected' }

function Add-SelectionLine {
    <# Applies and prints one line of `affected_select.py --format lines` to the selection. #>
    param([hashtable] $Selection, [string] $Line)
    $kind, $key, $verb, $rest = $Line -split ' ', 4
    switch ($kind) {
        'mode' { $Selection.Full = $key -ne 'affected'; Write-Host "mode: $key" }
        'group' {
            if ($verb -eq 'run') { [void] $Selection.Groups.Add($key) }
            Write-Host ('  {0,-5} {1,-24} {2}' -f $verb, $key, $rest)
        }
        'dotnet' {
            if ($key -eq 'target') { $Selection.DotnetTarget = "$verb $rest".Trim(); Write-Host "  .NET build input: $($Selection.DotnetTarget)" }
            else { Write-Host "  .NET project: $verb" }
        }
        default { Write-Host "${kind}: $key $verb $rest" }
    }
}

function Get-AffectedSelection {
    <# Runs scripts/affected_select.py; Nothing = no changed file, Full = run every step (also when it fails). #>
    param([string] $Base)
    $selection = @{ Full = $true; Nothing = $false; Groups = [System.Collections.Generic.HashSet[string]]::new(); DotnetTarget = 'StemStudio.slnx' }
    if (-not $Affected) { return $selection }
    Write-Host "`n=== affected selection ===" -ForegroundColor Cyan
    $arguments = @('run', '--no-project', 'python', '-I', 'scripts/affected_select.py', '--format', 'lines') + $(if ($Base) { @('--from', $Base) } else { @() })
    Push-Location $RepoRoot
    try { $lines = & uv @arguments; $code = $LASTEXITCODE } finally { Pop-Location }
    $selection.Nothing = $code -eq 166
    if ($code -notin 0, 166) { Write-Host "affected_select.py failed (exit $code): running everything" -ForegroundColor Yellow; return $selection }
    foreach ($line in $lines) { Add-SelectionLine $selection $line }
    $selection
}

function Resolve-StepGroup {
    <# Gives every step the group of the last @{ Group = ... } marker above it and drops the markers. #>
    param([object[]] $Items)
    $group = ''
    foreach ($item in $Items) {
        if ($item.Count -eq 1 -and $item.ContainsKey('Group')) { $group = $item.Group; continue }
        $item.Group = $group
        $item
    }
}

$Selection = Get-AffectedSelection $From
if ($Selection.Nothing) { Write-Host 'Nothing changed since the base: no step runs.'; exit 0 }
$DotnetTarget = $Selection.DotnetTarget

# Each step: name, working dir (relative to the repo root), command. Every component is merged, so a
# missing folder fails the step instead of skipping it. Commands match .github/workflows/ci.yml.
$Fx = 'tests/fixtures/pyproject.toml'
$Steps = Resolve-StepGroup @(
    @{ Group = 'dotnet' }
    @{ Name = 'dotnet tool restore'; Dir = '.'; Cmd = @('dotnet', 'tool', 'restore') }
    @{ Name = 'dotnet build'; Dir = '.'; Cmd = @('dotnet', 'build', $DotnetTarget, '-c', 'Release') }
    @{ Name = 'dotnet format'; Dir = '.'; Cmd = @('dotnet', 'format', $DotnetTarget, '--verify-no-changes') }
    @{ Name = 'dotnet test + coverage gate'; Dir = '.'; Cmd = @('dotnet', 'test', $DotnetTarget, '-c', 'Release', '--no-build', '-p:CollectCoverage=true') }
    @{ Group = 'dotnet-agent-floor' }
    @{ Name = 'agent coverage floor (011)'; Dir = '.'; Cmd = $WorkerPython + @('scripts/coverage_floor.py', $AgentCoverage) }
    @{ Group = 'dotnet-agent-eval-floor' }
    @{ Name = 'agent-eval coverage floor (011)'; Dir = '.'; Cmd = $WorkerPython + @('scripts/coverage_floor.py', $AgentEvalCoverage) }
    # Timing asserts skip under coverlet: the performance budgets (022 SC-004) run uninstrumented (as in CI).
    @{ Group = 'dotnet-perf' }
    @{ Name = 'performance budgets, uninstrumented (Category=Perf)'; Dir = '.'; Cmd = @('dotnet', 'test', 'tests/StemStudio.Api.Tests', '-c', 'Release', '--no-build', '--filter', 'Category=Perf', '-p:RunSettingsFilePath=') }
    @{ Group = 'worker' }
    @{ Name = 'worker uv sync'; Dir = 'worker'; Cmd = @('uv', 'sync', '--frozen') }
    @{ Name = 'worker ruff check'; Dir = 'worker'; Cmd = @('uv', 'run', '--frozen', 'ruff', 'check', '.') }
    @{ Name = 'worker ruff format'; Dir = 'worker'; Cmd = @('uv', 'run', '--frozen', 'ruff', 'format', '--check', '.') }
    @{ Name = 'worker mypy'; Dir = 'worker'; Cmd = @('uv', 'run', '--frozen', 'mypy', 'src', 'tests') }
    @{ Name = 'worker pytest + coverage'; Dir = 'worker'; Cmd = @('uv', 'run', '--frozen', 'pytest', '--cov', '--cov-branch', '--cov-report=term', '--cov-fail-under=90') }
    @{ Group = 'generator' }
    @{ Name = 'generator uv sync'; Dir = 'generator'; Cmd = @('uv', 'sync', '--frozen') }
    @{ Name = 'generator ruff check'; Dir = 'generator'; Cmd = @('uv', 'run', '--frozen', 'ruff', 'check', '.') }
    @{ Name = 'generator ruff format'; Dir = 'generator'; Cmd = @('uv', 'run', '--frozen', 'ruff', 'format', '--check', '.') }
    @{ Name = 'generator mypy'; Dir = 'generator'; Cmd = @('uv', 'run', '--frozen', 'mypy', 'src', 'tests') }
    @{ Name = 'generator pytest + coverage'; Dir = 'generator'; Cmd = @('uv', 'run', '--frozen', 'pytest', '--cov', '--cov-branch', '--cov-report=term', '--cov-fail-under=90') }
    @{ Group = 'audio-engine' }
    @{ Name = 'engine npm ci'; Dir = 'src/audio-engine'; Cmd = @('npm', 'ci', '--no-audit', '--no-fund') }
    @{ Name = 'engine lint'; Dir = 'src/audio-engine'; Cmd = @('npm', 'run', 'lint') }
    @{ Name = 'engine typecheck'; Dir = 'src/audio-engine'; Cmd = @('npm', 'run', 'typecheck') }
    @{ Name = 'engine test:coverage'; Dir = 'src/audio-engine'; Cmd = @('npm', 'run', 'test:coverage') }
    @{ Name = 'engine build'; Dir = 'src/audio-engine'; Cmd = @('npm', 'run', 'build') }
    @{ Group = 'soundtouch' }
    if ($HasSoundTouchWasm) { @{ Name = 'engine test:wasm (012, real soundtouch.wasm)'; Dir = 'src/audio-engine'; Cmd = @('npm', 'run', 'test:wasm') } }
    @{ Name = 'soundtouch emsdk pin (012)'; Dir = '.'; Cmd = @('pwsh', '-NoProfile', '-File', 'scripts/build-soundtouch.ps1', '-CheckPins') }
    @{ Group = 'web-interop' }
    @{ Name = 'web-interop npm ci'; Dir = 'src/web-interop'; Cmd = @('npm', 'ci', '--no-audit', '--no-fund') }
    @{ Name = 'web-interop lint'; Dir = 'src/web-interop'; Cmd = @('npm', 'run', 'lint') }
    @{ Name = 'web-interop typecheck'; Dir = 'src/web-interop'; Cmd = @('npm', 'run', 'typecheck') }
    @{ Name = 'web-interop test:coverage'; Dir = 'src/web-interop'; Cmd = @('npm', 'run', 'test:coverage') }
    @{ Name = 'web-interop build'; Dir = 'src/web-interop'; Cmd = @('npm', 'run', 'build') }
    @{ Group = 'contracts' }
    @{ Name = 'contracts lint (015-play-along-recording)'; Dir = '.'; Cmd = @('npx', '--yes', $Redocly, 'lint', '--config', "$Contracts/redocly.yaml", "$Contracts/backend-api.openapi.yaml", "$Contracts/recording-api.openapi.yaml") }
    @{ Name = 'contracts lint (014-practice-flow)'; Dir = '.'; Cmd = @('npx', '--yes', $Redocly, 'lint', '--config', "$PracticeFlowContracts/redocly.yaml", "$PracticeFlowContracts/backend-api.openapi.yaml", "$PracticeFlowContracts/worker-api.openapi.yaml", "$PracticeFlowContracts/practice-api.openapi.yaml", "$PracticeFlowContracts/worker-practice.openapi.yaml") }
    foreach ($dir in $GenerativeContracts) {
        @{ Name = "contracts lint ($(Split-Path -Leaf (Split-Path -Parent $dir)))"; Dir = '.'; Cmd = @('npx', '--yes', $Redocly, 'lint', '--config', "$dir/redocly.yaml", "$dir/generator-api.openapi.yaml", "$dir/backend-api.openapi.yaml") }
    }
    @{ Name = 'contracts lint (011-ai-composer-agent)'; Dir = '.'; Cmd = @('npx', '--yes', $Redocly, 'lint', '--config', "$AgentContracts/redocly.yaml", "$AgentContracts/backend-api.openapi.yaml", "$AgentContracts/agent-api.openapi.yaml") }
    @{ Name = 'contracts lint (011 claude-key)'; Dir = '.'; Cmd = @('npx', '--yes', $Redocly, 'lint', '--config', "$ClaudeKeyContracts/redocly.yaml", "$ClaudeKeyContracts/backend-api.openapi.yaml", "$ClaudeKeyContracts/backend-api.claude-key.openapi.yaml") }
    @{ Name = 'contracts lint (012-practice-tempo)'; Dir = '.'; Cmd = @('npx', '--yes', $Redocly, 'lint', '--config', "$PracticeContracts/redocly.yaml", "$PracticeContracts/backend-api.openapi.yaml") }
    @{ Name = 'contracts superset (012 contains 1.21.0)'; Dir = '.'; Cmd = $PyYamlPython + @("$PracticeContracts/check_superset.py") }
    @{ Name = 'contracts lint (013-library-care)'; Dir = '.'; Cmd = @('npx', '--yes', $Redocly, 'lint', '--config', "$LibraryCareContracts/redocly.yaml", "$LibraryCareContracts/backend-api.openapi.yaml", "$LibraryCareContracts/backend-api.delta.openapi.yaml", "$LibraryCareContracts/worker-api.openapi.yaml") }
    @{ Name = 'contracts superset (013 contains 1.11.0)'; Dir = '.'; Cmd = $PyYamlPython + @("$LibraryCareContracts/check_superset.py") }
    @{ Name = 'contracts superset (013 contains its delta)'; Dir = '.'; Cmd = $PyYamlPython + @("$LibraryCareContracts/check_superset.py", "$LibraryCareContracts/backend-api.delta.openapi.yaml", "$LibraryCareContracts/backend-api.openapi.yaml") }
    @{ Name = 'contracts lint (022-library-folders)'; Dir = '.'; Cmd = @('npx', '--yes', $Redocly, 'lint', '--config', "$LibraryFoldersContracts/redocly.yaml", "$LibraryFoldersContracts/backend-api.openapi.yaml", "$LibraryFoldersContracts/backend-api.library-folders.openapi.yaml") }
    @{ Name = 'contracts lint (023-lyric-writing-assistant)'; Dir = '.'; Cmd = @('npx', '--yes', $Redocly, 'lint', '--config', "$LyricAssistContracts/redocly.yaml", "$LyricAssistContracts/backend-api.openapi.yaml", "$LyricAssistContracts/backend-api.delta.openapi.yaml") }
    @{ Name = 'contracts merge check (022 full 1.22.0)'; Dir = '.'; Cmd = $PyYamlPython + @("$LibraryFoldersContracts/merge_full.py", '--check') }
    @{ Name = 'contracts merge check (023 full 1.23.0)'; Dir = '.'; Cmd = $PyYamlPython + @("$LyricAssistContracts/merge_full.py", '--check') }
    @{ Name = 'contracts superset (014 contains 1.23.0)'; Dir = '.'; Cmd = $PyYamlPython + @("$PracticeFlowContracts/check_superset.py") }
    @{ Name = 'contracts superset (014 contains its delta)'; Dir = '.'; Cmd = $PyYamlPython + @("$PracticeFlowContracts/check_superset.py", "$PracticeFlowContracts/practice-api.openapi.yaml", "$PracticeFlowContracts/backend-api.openapi.yaml") }
    @{ Name = 'contracts merge check (014 full 1.13.0)'; Dir = '.'; Cmd = $PyYamlPython + @("$PracticeFlowContracts/merge_full.py", '--check') }
    @{ Name = 'contracts superset (015 contains 1.13.0)'; Dir = '.'; Cmd = $PyYamlPython + @("$Contracts/check_superset.py") }
    @{ Name = 'contracts superset (015 contains its delta)'; Dir = '.'; Cmd = $PyYamlPython + @("$Contracts/check_superset.py", "$Contracts/recording-api.openapi.yaml", "$Contracts/backend-api.openapi.yaml") }
    @{ Name = 'contracts merge check (015 full 1.14.0)'; Dir = '.'; Cmd = $PyYamlPython + @("$Contracts/merge_recording_delta.py", '--check') }
    @{ Name = 'contracts output schema (023 check_output_schema.py)'; Dir = '.'; Cmd = $JsonSchemaPython + @("$LyricAssistContracts/check_output_schema.py") }
    @{ Name = 'contracts merge tests (023 test_merge_full.py)'; Dir = '.'; Cmd = $PytestPython + @($LyricAssistContracts) }
    @{ Name = 'contracts lint (016-instrument-views)'; Dir = '.'; Cmd = @('npx', '--yes', $Redocly, 'lint', '--config', "$InstrumentContracts/redocly.yaml", "$InstrumentContracts/backend-api.openapi.yaml", "$InstrumentContracts/worker-api.openapi.yaml", "$InstrumentContracts/backend-api.delta.openapi.yaml", "$InstrumentContracts/worker-api.delta.openapi.yaml") }
    @{ Name = 'contracts merge check (016 full 1.15.0 / 1.9.0)'; Dir = '.'; Cmd = $PyYamlPython + @("$InstrumentContracts/merge_full.py", '--check') }
    @{ Name = 'chord spelling vectors check (016)'; Dir = '.'; Cmd = @('uv', 'run', '--no-project', 'python', '-I', "$InstrumentContracts/generate_chord_spelling_vectors.py", '--check') }
    @{ Name = 'chord spelling vector tests (016)'; Dir = '.'; Cmd = $PytestPython + @($InstrumentContracts) }
    @{ Group = 'scripts-tests' }
    @{ Name = 'split E2E jobs check (016 T144)'; Dir = '.'; Cmd = $PytestPython + @('-c', 'scripts/pyproject.toml', '-q', 'scripts/tests/test_ci_e2e_split.py') }
    @{ Group = 'secret-scan' }
    @{ Name = 'secret scan self-test'; Dir = '.'; Cmd = $WorkerPython + @('scripts/secret_scan.py', '--self-test') }
    @{ Name = 'secret scan'; Dir = '.'; Cmd = $WorkerPython + @('scripts/secret_scan.py') }
    @{ Group = 'contracts' }
    @{ Name = 'render vectors check'; Dir = '.'; Cmd = @('uv', 'run', '--project', 'worker', '--frozen', 'python', $RenderVectors, '--check') }
    @{ Group = 'quality-tools' }
    @{ Name = 'quality ruff check'; Dir = 'tools/quality'; Cmd = @('uv', 'run', '--frozen', 'ruff', 'check', '.') }
    @{ Name = 'quality ruff format'; Dir = 'tools/quality'; Cmd = @('uv', 'run', '--frozen', 'ruff', 'format', '--check', '.') }
    @{ Name = 'quality mypy'; Dir = 'tools/quality'; Cmd = @('uv', 'run', '--frozen', 'mypy') }
    @{ Name = 'quality pytest + coverage'; Dir = 'tools/quality'; Cmd = @('uv', 'run', '--frozen', 'pytest', '--cov', '--cov-branch', '--cov-report=term') }
    @{ Group = 'scripts-tests' }
    @{ Name = 'key volume checks ruff check'; Dir = '.'; Cmd = $QualityTools + @('ruff', 'check', '--config', $ScriptsCfg) + $KeyChecks }
    @{ Name = 'key volume checks ruff format'; Dir = '.'; Cmd = $QualityTools + @('ruff', 'format', '--config', $ScriptsCfg, '--check') + $KeyChecks }
    @{ Name = 'key volume checks mypy'; Dir = '.'; Cmd = $QualityTools + @('mypy', '--config-file', $ScriptsCfg) + $KeyChecks }
    @{ Name = 'key volume checks pytest + coverage'; Dir = '.'; Cmd = $QualityTools + @('pytest', '-c', $ScriptsCfg, '--cov', '--cov-branch', "--cov-config=$ScriptsCfg", '--cov-report=term', 'scripts/tests') }
    @{ Group = 'compose' }
    foreach ($combo in $ComposeCombos) {
        @{ Name = "key volume check ($($combo -join ' + '))"; Dir = '.'; Cmd = @('pwsh', '-NoProfile', '-Command', "& ./scripts/check-key-volume.ps1 -File $($combo -join ',')") }
    }
    foreach ($combo in $ComposeCombos + , @('docker-compose.yml', 'docker-compose.e2e.yml')) {
        @{ Name = "loopback ports check ($($combo -join ' + '))"; Dir = '.'; Cmd = @('pwsh', '-NoProfile', '-Command', "& ./scripts/check-loopback-ports.ps1 -File $($combo -join ',')") }
    }
    @{ Group = 'quality-gate' }
    @{ Name = 'quality gate (run_all)'; Dir = '.'; Cmd = @('uv', 'run', '--project', 'tools/quality', '--frozen', 'python', 'tools/quality/run_all.py') }
    @{ Group = 'docs-manuals' }
    @{ Name = 'docs manuals up to date'; Dir = '.'; Cmd = @('node', 'tools/docs/check.mjs') }
    @{ Group = 'site' }
    @{ Name = 'site npm ci (tools/docs markdown-it)'; Dir = 'tools/docs'; Cmd = @('npm', 'ci', '--no-audit', '--no-fund') }
    @{ Name = 'site unit tests'; Dir = '.'; Cmd = @('node', '--test', 'tools/site/test/site.test.mjs') }
    @{ Name = 'site build + check'; Dir = '.'; Cmd = @('node', 'tools/site/check.mjs') }
    @{ Group = 'fixtures' }
    @{ Name = 'fixtures ruff check'; Dir = '.'; Cmd = @('uv', 'run', '--project', 'worker', '--frozen', 'ruff', 'check', 'tests/fixtures') }
    @{ Name = 'fixtures ruff format'; Dir = '.'; Cmd = @('uv', 'run', '--project', 'worker', '--frozen', 'ruff', 'format', '--check', 'tests/fixtures') }
    @{ Name = 'fixtures mypy'; Dir = '.'; Cmd = @('uv', 'run', '--project', 'worker', '--frozen', 'mypy', '--config-file', $Fx, '--exclude', '^tests/fixtures/backup/', 'tests/fixtures') }
    @{ Name = 'fixtures pytest + coverage'; Dir = '.'; Cmd = @('uv', 'run', '--project', 'worker', '--frozen', 'pytest', '-c', $Fx, '--cov', '--cov-branch', "--cov-config=$Fx", 'tests/fixtures/tests') }
    @{ Name = 'backup fixtures ruff check (013)'; Dir = '.'; Cmd = @('uv', 'run', '--project', 'worker', '--frozen', 'ruff', 'check', '--config', $BackupFx, 'tests/fixtures/backup') }
    @{ Name = 'backup fixtures ruff format (013)'; Dir = '.'; Cmd = @('uv', 'run', '--project', 'worker', '--frozen', 'ruff', 'format', '--config', $BackupFx, '--check', 'tests/fixtures/backup') }
    @{ Name = 'backup fixtures mypy (013)'; Dir = '.'; Cmd = @('uv', 'run', '--project', 'worker', '--frozen', 'mypy', '--config-file', $BackupFx, 'tests/fixtures/backup') }
    @{ Name = 'backup fixtures pytest + coverage (013)'; Dir = '.'; Cmd = @('uv', 'run', '--project', 'worker', '--frozen', 'pytest', '-c', $BackupFx, '--cov', '--cov-branch', "--cov-config=$BackupFx", '--cov-report=term', 'tests/fixtures/backup/tests') }
    @{ Name = 'fixtures generate'; Dir = '.'; Cmd = @('uv', 'run', '--project', 'worker', '--frozen', 'python', 'tests/fixtures/generate_fixtures.py') }
    @{ Group = 'e2e-lint' }
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

# Optional (-AgentE2e): one fake stack, then the agent passes (fake backend, then none), then the 023 lyric passes; they
# recreate only the api of this project.
if ($AgentE2e) {
    $agentEnv = @{ STEMSTUDIO_SEPARATOR = 'fake'; STEMSTUDIO_AGENT_BACKEND = 'none'; COMPOSE_PROJECT_NAME = $E2eProject }
    $Steps += @{ Name = 'agent e2e: fake stack up'; Dir = '.'; Env = $agentEnv; Cmd = @('docker', 'compose', 'up', '-d', '--build', '--wait', '--wait-timeout', '300') }
    foreach ($backend in 'fake', 'none') {
        $Steps += @{ Name = "agent e2e: playwright agent.spec.ts ($backend)"; Dir = '.'; Env = $agentEnv; Cmd = @('pwsh', '-NoProfile', '-File', 'scripts/agent-e2e.ps1', $backend) }
    }
    # 023 lyric assistant passes (fake model with /lyric-scripts, then backend none); same api-only recreate.
    foreach ($backend in 'fake', 'none') {
        $Steps += @{ Name = "agent e2e: lyric assistant ($backend)"; Dir = '.'; Env = $agentEnv; Cmd = @('pwsh', '-NoProfile', '-File', 'scripts/lyric-e2e.ps1', $backend) }
    }
}
# Optional (-RecordingE2e, feature 015 T010): capture fixtures, a fake stack with the e2e override, the @recording specs.
if ($RecordingE2e) {
    $recordingEnv = @{ STEMSTUDIO_SEPARATOR = 'fake'; COMPOSE_PROJECT_NAME = $E2eProject; COMPOSE_FILE = 'docker-compose.yml;docker-compose.e2e.yml' }
    $Steps += @{ Name = 'recording e2e: capture fixtures'; Dir = 'tests/fixtures'; Cmd = @('uv', 'run', '--project', '../../worker', '--frozen', 'python', '-m', 'recording.generate_recording_fixtures') }
    $Steps += @{ Name = 'recording e2e: fake stack up (e2e override)'; Dir = '.'; Env = $recordingEnv; Cmd = @('docker', 'compose', 'up', '-d', '--build', '--wait', '--wait-timeout', '300') }
    $Steps += @{ Name = 'recording e2e: playwright @recording'; Dir = 'e2e'; Env = @{ E2E_RECORDING_LOOPBACK = '1' }; Cmd = @('npm', 'run', 'test:recording') }
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

if (-not $HasSoundTouchWasm) { Write-Host "engine test:wasm skipped: $SoundTouchWasm not built (pwsh scripts/build-soundtouch.ps1)" -ForegroundColor Yellow }
$summary = [System.Collections.Generic.List[object]]::new()
foreach ($step in $Steps) {
    if (-not ($Selection.Full -or -not $step.Group -or $Selection.Groups.Contains($step.Group))) {
        $summary.Add([pscustomobject]@{ Step = $step.Name; Result = "skipped (affected: $($step.Group) not selected)" })
        continue
    }
    $result = Invoke-Step $step
    $summary.Add([pscustomobject]@{ Step = $step.Name; Result = $result })
    if ($result -eq 'failed' -and -not $ContinueOnError) { break }
}
if ($Gpu -or $AgentE2e -or $RecordingE2e) { & docker compose -p $E2eProject -f docker-compose.yml -f docker-compose.e2e.yml down -v | Out-Host }
$summary | Format-Table -AutoSize | Out-String | Write-Host
if (-not $Selection.Full) { Write-Host 'Affected run (lane checkpoint): integration and pre-merge runs stay full.' -ForegroundColor Yellow }
exit ([int]($summary.Result -contains 'failed'))
