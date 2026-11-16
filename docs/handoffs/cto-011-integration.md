# Hand-off: CTO / 011 integration (T080)

Branch `011-composer-agent` (worktree `.claude/worktrees/agent-a5eae21fff586ccc4`), base `782665d5`. Nothing pushed,
nothing merged into `main`; `main-run` and the root checkout untouched. Privacy check before and after the merges:
`git log --format='%ae' main..HEAD | sort -u` (and `%ce` for the lane branches) shows only `ragterhuis@agterhuis.net`.

## What was built

### Step 1 - lane merges (`git merge --no-ff`, in the prescribed order)

| Lane | Lane tip | Merge commit | Conflicts |
|---|---|---|---|
| `011-lane-quality` | `3cf3a2c5` | `35bbbfe4` | none |
| `011-lane-devops` | `320efd88` | `850a1c32` | tasks.md (ticks) |
| `011-lane-backend` | `509026d9` | `1f7cc234` | none |
| `011-lane-agent` | `a0d16716` | `528fae97` | tasks.md (ticks) |
| `011-lane-frontend` | `a62999be` | `4f4439de` | tasks.md (ticks) |
| `011-lane-qa` | `1f716ea9` | `a6ce180c` | tasks.md (ticks) |

tasks.md: every tick kept; `3bd61601` restores the architect's T041 wording (the agent lane carried the older text).
No lane changed a contract or spec file other than tasks.md.

### Step 2 - switch-overs and fixes (test-first; red commit before each behaviour change)

| # | Item | Commits |
|---|---|---|
| 1 | `risk-check-failed`: `PolicyInput.RiskCheckFailed` (set from `PreparedCall.RiskCheckFailed`), `ToolPolicy` emits `AgentDecisionReasons.RiskCheckFailed` instead of `risk-heavy`; never plan-covered; US6 escalation test updated | `30cfda6d` (red), `811cd1d2` |
| 2 | `UndoEntry.ExpectedUpdatedAt`: `UndoRecorder` turns the provider's `$expect` into the field and stores the call without it; `UndoRun` reads it via `UndoGuard.For` (`Split` kept for entries stored with `$expect`, covered by a legacy test). Backend maps the nullable column; `AddAgent` regenerated (dotnet-ef 10.0.15, diff = the one column) and kept at id `20261116064402` (never released) | `57403308` (red), `97d872df`, `91ba97df` (red), `efd7ba70` |
| 3 | `ImitationGuard` reads `Resources/EmbeddedLexicon`; one-off check (order-insensitive, same counts) that the three lists equal `Policy/ImitationLexicon.cs`, then the copy and its `DATA_COPIES` entry deleted (mechanism and tests kept) | `a6d4a03b` |
| 4 | DI tests use `ServiceCollection.BuildServiceProvider` (ValidateOnBuild); `Fakes/TestServiceProvider.cs` deleted. `tests/StemStudio.AgentEval.Tests`: grader (every case of eval-set.json: ideal passes, forbidden / missing / 3 extra reads fail, every matcher), `FixtureIds`, the fixture ports and page, the backends, the entry point and a fake live run of all 33 cases | `72357602`, `51b2f26a` |
| 5 | Race fix: `ApprovalWaiter.Register` returns an `ApprovalWait`; `ApprovalGate` registers before publishing `approval.requested`; the harness's 250 ms delay removed (AgentEval tests 4 s -> 0.5 s) | `9142f0d6` (red), `58e79793`, `c3e57795` |
| 6 | Eval cases 17 and 27: "approval: required (tainted) -> approve" in eval-set.md and eval-set.json; harness test proves the change after a search is approved and executed; decision appended to `docs/decisions/CTO-DECISIONS-2026-11-15.md` | `7aa857e2` (red), `15f0871f` |
| 7 | Frontend label for `risk-check-failed` (bUnit). qa: no E2E/script asserts `risk-heavy` on a failed size check (the two `risk-heavy` assertions are statically heavy tools); `device` assertions already use `gpu`/`cpu` - no change needed | `79fc6efd` (red), `75d47ba8` |
| 8 | Sonar: `tools/agent-eval` in `sonar.sources`; the opencover glob already imports `tests/StemStudio.AgentEval.Tests/.../coverage.opencover.xml` (now asserted by a quality test); CI + `scripts/test-all.{ps1,sh}`: "agent-eval coverage floor (011)"; artefact glob documented; the harness is still never run in CI | `961458c4` (red), `e242baaf` |
| 9 | `Agent/Policy` rule is an allow-list (`DOTNET_FOLDER_ALLOWED`): inside `StemStudio.Agent` only `Model`, `Tools` and the data-only `Resources` (needed by item 3); plan.md, tools/quality/README.md and the decisions log updated | `1b5aac64` (red), `8480da3b` |

### Step 3 findings fixed on the merged tree (each lane was clean on its own branch)

| Finding | Fix | Commits |
|---|---|---|
| `dotnet format --verify-no-changes` failed: backend mappers with several initialisers on one line | formatted (5 backend units now 16-26 LOC: soft warnings, below 30) | `46dc952f` |
| Secret scan: the sentinel test keys contain the key prefix | built from parts, like `scripts/secret_scan.py` | `0cf71f05` |
| Quality gate: 10 duplication errors | agent clones removed (shared `ScriptedCall.ToContent`, `FreshMix`, `ToolDefinitions.TrackId`, using headers), `LoopbackClient` uses `HttpValidationProblemDetails`; the two Domain mirrors (`ImitationGuard`, `ProvenanceChecker`) are listed in a new `MIRRORS` rule (warning with reason; Agent may not reference Domain); `ToolPolicy.Reasons` split back to CCN <= 5 | `1cfbb899`, `46dc952f`, `cd285511` |

### Step 4 findings fixed (found by the E2E on the fake stack)

| Finding | Fix | Commits |
|---|---|---|
| The api image did not copy `src/StemStudio.Agent` (011 api did not build in Docker) | `docker/api.Dockerfile` | `02b0e99e` |
| `scripts/agent-e2e.ps1` passed the Playwright arguments to `npx.ps1` as one joined string | splat the arguments (in commit `0491f8f5`, together with the next fix) | `0491f8f5` |
| Mixer tool right after `open_mixer` failed ("mixer not open") with the zero-latency fake model | host waits up to 10 s for the mixer page after `open_mixer` | `d4f09e78` (red), `0491f8f5` |
| Badge showed a restored run's backend while the agent was unavailable | status backend while not ready | `02e11259` (red), `7aca7b3d` |
| `delete_mix` is not offered on the mixer page (catalogue page profiles), but `us2-delete-mix` called it there | script navigates to the mixes page first (catalogue is binding) | `83160e7b` |
| Delete card did not name the track (US2-1) | `delete_track` estimator: "Delete the track <title> ... This cannot be undone." | `cca88cfb` (red), `83160e7b` |
| New conversation replaced by the newest stored one loading in the background (Firefox) | selection counter + automatic open only while nothing was chosen (also during New) | `87025fa6`, `ad5f95ef`, `9b5e52a4` (red), `1ea38667` |
| Every upload was titled with its stored file hash (pre-existing since 001: the worker gets the hashed name and its title overwrote the original) | `MetadataMapper.ToSource(track, metadata)` keeps an upload's original-name title (artist still taken); a first domain-level attempt broke take imports and was reverted | `16e02257`/`46a2451c` (reverted), `7ed67ee9` |
| With real titles the US1 search taints the run, so the mute asks (owner decision: titles taint) | US1 E2E approves the tainted mute | `2bfac860` |
| Undo stayed enabled after the only command was undone (qa us5 request 2) | Undo needs a command not undone yet | `c632251f` (red), `099bf0b3` |
| `GET/PUT /api/agent/settings` returned the fake override, not the stored choice (qa request 3) | `EfAgentSettingsStore.GetStoredAsync` for the settings service; the runtime keeps the override | `79e01e83` (red), `a8c08683` |
| Agent restyle not shown on the track page until reload | `AgentRunNotifier`: generation lists reload when a run ends | `2e1c4d46` (red), `0babbd23` |

## Files changed

Lane merges (all lane files), plus: `src/StemStudio.Agent/{Policy,Runtime,Tools,Backends/Fakes}/*`, `src/StemStudio.Api/Agent/*`,
`src/StemStudio.Api/{Metadata/MetadataMapper.cs,Sync/*Applier.cs}`, `src/StemStudio.Persistence/{Agent/AgentEntities.cs,Migrations/*AddAgent*,Migrations/StemStudioDbContextModelSnapshot.cs}`,
`src/StemStudio.Web/{Agent/*,Components/Generation/GenerationList.razor.cs,Program.cs}`, `tools/agent-eval/*`,
`tests/StemStudio.{Agent,AgentEval,Api,Persistence,Web}.Tests/*`, `tools/quality/{dependency_check.py,duplication.py,README.md,tests/*}`,
`sonar-project.properties`, `.github/workflows/ci.yml`, `scripts/{test-all.ps1,test-all.sh,agent-e2e.ps1}`, `docker/api.Dockerfile`,
`e2e/{agent-scripts/us2-delete-mix.json,tests/agent.spec.ts,visual/__screenshots__/**}`, `specs/011-ai-composer-agent/{tasks.md,plan.md,evaluation/eval-set.md}`,
`tools/agent-eval/eval-set.json`, `docs/decisions/CTO-DECISIONS-2026-11-15.md`, `docs/quality/SIG-REPORT.md`, this note.

## Tests and coverage

`pwsh scripts/test-all.ps1 -ContinueOnError` on the final tree: **51 steps, all passed** (exit 0).

| Component | Tests | Line | Branch |
|---|---|---|---|
| StemStudio.Domain | 752 passed | 98.97 % | 94.52 % |
| StemStudio.Agent | 600 passed | 98.80 % | 93.22 % |
| StemStudio.AgentEval | 125 passed | 99.83 % | 92.57 % |
| StemStudio.Persistence | 99 passed | 98.76 % | 94.73 % |
| StemStudio.Web | 1712 passed | 99.56 % | 93.65 % |
| StemStudio.Api (+ Contracts 100/100) | 1435 passed | 98.91 % | 91.61 % |
| worker (pytest) | all passed | 100 % | 100 % |
| generator (pytest, base install) | all passed | 99.05 % total | (gate 90 % met) |
| audio-engine (Vitest) | 287 passed | gate met | gate met |
| web-interop (Vitest) | 254 passed | 99.69 % stmts | 97.95 % |
| tools/quality (pytest) | 597 passed | 100 % | 100 % |
| tests/fixtures (pytest) | 250 passed | 100 % | 100 % |

Coverage floors: agent 98.80/93.22, agent-eval 99.83/92.57 (met 90). `PendingAgentOperations` is empty
(`BackendContractTests.StemsAndMixes.cs`).

## Quality checks

- `dotnet build StemStudio.slnx -c Release`: 0 warnings, 0 errors; `dotnet format --verify-no-changes`: clean.
- `tools/quality/run_all.py`: 2111 files, 16452 units, 219 clones, 2 exemptions, **0 errors**, 742 warnings
  (`docs/quality/SIG-REPORT.md` regenerated). Rows: `Agent` 564 units, stars 5 / 5 / 3 (parameters 28.3 % at 3-4);
  `tools/agent-eval` 176 units, 5 / 5 / 4.
- Contracts lint (Redocly 2.78.0): 008, 009, 010 and 011 documents valid (011: 10 warnings, the known 8 + 2).
- Secret scan: self-test passed; 3438 files clean.

## E2E (fake stack, retries 0, chromium + firefox)

Owner's stack recorded before stopping: project `stemstudio`, files `main-run/docker-compose.yml`,
`docker-compose.gpu.yml`, `docker-compose.generative.yml` (web and worker created with the first two), containers
api `508fd0f`, web `3425d9c`, worker `a196183`, generator/singer `:local`, all healthy; 8080 and 8081 answered 200.
Stopped with `docker stop` (containers kept), E2E ran as project `stemstudio-e2e`.

| Run | Result |
|---|---|
| Default suite (`--grep-invert "@real\|@restart\|@worker-restart\|@gpu\|@agent"`, fresh stack, agent `none`) | **375 passed, 0 failed, 115 skipped** (34.7 min). Skips: generator-dependent specs (song 24, voice 24, generative 22, voice-recording 4, generation-status 4) and mode-dependent ones (lyrics 10, artwork 8, panner-law 8, band-info 6, karaoke 5) |
| `scripts/agent-e2e.ps1 fake` | us1-command 4/4, us2-delete-mix 2/2, us2-delete-track 2/2, us2-detail 2/2, us2-inject 2/2, us5-mixer-undo 2/2, us5-save-undo 2/2, unscripted @agent (settings) 2/2 - **18 passed, 0 failed** |
| `scripts/agent-e2e.ps1 none` | unavailable 2/2 |
| `@generative` agent tests (no devops mode exists; run by hand per qa-011-us6: compose + generative + generative-fake, `STEMSTUDIO_FAKE_GENERATOR_MODE=ready`, api recreated per script) | us6-restyle 2/2, us6-imitation 2/2 |
| Visual (Playwright v1.65.1-noble container, fresh stack, agent backend ollama unreachable as in CI) | baselines regenerated: 100 changed + 6 new `settings--{beginner,advanced,expert}--{dark,light}.png`; second fresh stack: **110 passed, 6 skipped** (restyle-dialog needs a generator) |
| a11y (same container and stack) | **142 passed, 6 skipped** |

Per-spec default results: every spec 0 failed (agent-support 18, artwork 14+8s, audio-check 12, band-info 8+6s, detail 2,
detail-mixer 10, detail-mixes 4, detail-progress 4, detail-split-further 4, device 4, expert-keyboard 10, export 20,
feature-levels 12, generation-status 8+4s, generative-support 18, health 4, help 46, import 20, journey-beginner 2,
journey-levels 16, karaoke 21+5s, library 4, lyrics 2+10s, lyrics-score-levels 12, lyrics-score-support 18, main-flow 2,
mixer 12, panner-law 8+8s, retry-delete 2, sheet-music 14, styles 20, theme-flash 8, tours 16).

Visual baseline changes reviewed side by side: the header "Agent" toggle, the rail "Settings" link, and upload titles
(the original file name instead of the 64-character hash, fix `7ed67ee9`), which shortens the track, mixer, export,
score and band-info screenshots. No other differences.

### Owner's stack restored

`docker compose -p stemstudio -f docker-compose.yml -f docker-compose.gpu.yml -f docker-compose.generative.yml start`
(from `main-run`) restarted the **same containers** (api `508fd0f`, web `3425d9c`, worker `a196183`, generator, singer;
same volumes); all healthy; `http://localhost:8080/` and `/tracks` 200, `8081/health` 200. The E2E builds had moved
the image tags `stemstudio/{api,web,worker}:local`; the original api/web images were no longer in the image store, so
`api:local` and `web:local` were rebuilt from `main-run` (`af28ea2d`) without touching the containers;
`worker:local` (CPU tag, unused by the GPU stack) now points at the 011 CPU build; `worker:local-gpu`,
`generator:local`, `singer:local` unchanged; the `*-fake` tags were removed.

## Dependencies added

None.

## Docs impact

As in the lane hand-offs (T084a), plus: the approval card's "risk-check-failed" text; the delete card names the track;
Undo is disabled when every command of the conversation was undone; uploads keep their file name as title;
`scripts/agent-e2e.ps1` works with the npx PowerShell shim.

## Open issues

1. **Decisions to confirm (owner/architect)**: Policy allow-list includes the data-only `Agent.Resources`;
   `MIRRORS` accepts the two agent mirrors of Domain logic as warnings; AddAgent kept its id (dev databases with the
   earlier AddAgent must be recreated); the upload-title fix changes 001 behaviour (titles were hashes in practice).
2. **devops**: no `agent-e2e` mode for the `@generative` agent tests (qa-011-us6 request); CI's e2e job still does
   not run them.
3. **backend**: 5 soft unit-size warnings in the formatted mappers (16-26 LOC); `MetadataMapper.ToQuery` still sends
   the hashed title as the lookup title of an upload.
4. **ai-agent**: `Agent` parameter stars 3 (28 % of units with 3-4 parameters).
5. **frontend**: Undo's "command left" count is message-based (a command without changes still enables Undo until
   the API answers 409).
6. Commit `0491f8f5` also carries the `agent-e2e.ps1` splatting fix (devops file) - noted here instead of a rewrite.
7. Next: **T081a/T081** (Ollama readiness and the reference-machine evaluation, `--backend ollama --model qwen3.5:9b --runs 3`),
   **T082** (owner-run Claude A14/A15), **T083** (third-party licences), **T084** (speckit-analyze, STATUS, PR),
   **T084a** (public manuals + PDFs). Not done here by instruction.

## Next owner

CTO (T081a/T081, T083, T084, T084a); owner (T082).
