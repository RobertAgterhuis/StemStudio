# Hand-off: architect / 012 Phase 0 (spikes, contracts, contract code, skeleton)

012 tasks T001-T007 on branch `012-practice-tempo` (from `main` f243e237). First of the 012-021
builds. The owner was unavailable (coordinator instruction 2026-11-16 21:52): every product or
technical choice below was decided by the architect for the best result for a musician practising
at home and is recorded in ADR-021 "Phase 0 outcome" (P1-P8) and in the section "Decisions taken".

## What was built

### Contract versions (binding for the wave)

| Contract | Version | Document | Base |
|---|---|---|---|
| backend-api | **1.11.0** | `specs/012-practice-tempo/contracts/backend-api.openapi.yaml` | full **1.21.0** on `main` (011 + claude-key) + the 012 additions unchanged; `info.x-stemstudio-contains` = 1.1.0..1.10.0, 1.21.0, 1.11.0 |
| ui-test-ids | **1.11.0** | `specs/012-practice-tempo/contracts/ui-test-ids.md` (full, new) | 1.21.0 + `ui-test-ids.delta.md` unchanged |
| audio-engine | **2.3.0** | `contracts/audio-engine.md`, `src/audio-engine/src/types.ts` | 2.1.0 (2.2.0 never published by 011, stays unused) |
| WASM ABI | **1** | audio-engine.md "WASM ABI", `third_party/soundtouch/include/stemstudio_soundtouch.h` | new |
| worker-api, help-content, web-interop, render-vectors | unchanged | | |

**Version labels (ROADMAP rule made concrete, `docs/ROADMAP.md`)**: a full document produced by
merging a feature's delta carries that feature's allocated label even when a higher label is
already merged; labels identify merged delta sets, never an order; consumers compare by
containment (`check_superset.py`, contract tests). Order of full documents on `main`: 1.10.0 ->
1.21.0 -> **1.11.0** -> 1.12.0 (013) -> ... 013 merges its delta onto this 1.11.0.

### Spike results (details: `specs/012-practice-tempo/research.md` "Phase 0 results")

Reference machine (Core Ultra 9 285K, Windows 11). Builds with `docker run --rm --network none` in
the pinned emsdk image; browser page on a local Node server with a random port; no compose project,
nothing left running; the owner's `stemstudio` stack was not touched.

| Item | Result |
|---|---|
| P0-1 build | SoundTouch 2.4.1 (unmodified) + throw-away shim -> standalone reactor WASM, **30,168 B**, **no imports**; two fresh builds **bit-identical** |
| Licence | `COPYING.TXT` = LGPL-2.1; release tarball SHA-256 `e07abf20ce8f95850c280132e1f61ad400fc1f4011b7fac698a503de6aab6733` (surina.net; Codeberg archive has identical content) |
| SIMD128 | bit-identical output, no speed-up -> **off** |
| Initial latency / max output lag | 4,852 / 4,736 frames over p = 0.42..4.0, also with 120 jumps 0.42 <-> 4 -> **`STRETCH_DELAY_FRAMES = 5120` (116.1 ms)** |
| Without `SOUNDTOUCH_PREVENT_CLICK_AT_RATE_CROSSOVER` | lag drift up to 205,608 frames (4.6 s) after 120 crossings of p = 1 -> flag mandatory |
| Pitch accuracy / identity | <= 0.017 % error at all factors; identity max error 3.2e-6 |
| Throughput (Node, one core) | 85x (p = 4) .. 369x real time |
| Content offset | WSOLA mean +32 ms (p 0.42) .. -40 ms (p 4) -> compensated by `c(p) = round(-1100 * log2(p))` frames |
| Pitch-change timing | midpoint rule: wrong-pitch time 12-23 ms (70-81 ms when applied at the input frame) |
| P0-2 Chromium 159 | 20 channels at 50 % / pitch 2 for 600.7 s: **0 underruns**; stage 0.019 ms per quantum (**0.9 %** of 2.9 ms); change latency 87 / 116 / 151 ms (min / median / max) |
| P0-2 Firefox 159 | 600.6 s: **0 underruns**; stage 0.017 ms (0.9 %); change latency 93 / 119 / 160 ms |
| Exit criteria | all met; fallback soundtouchjs not needed; ADR-021 **Accepted** |

### Contract refinements from the spikes (binding; DELTA.md 5, audio-engine.md)

1. `STRETCH_DELAY_FRAMES = 5120`; `STRETCH_CONTENT_FRAMES_PER_OCTAVE = -1100`; heard latency
   `L(p) = (D + c(p)) / sampleRate` for source starts, re-anchoring and `PracticeStatus.latencyMs`.
2. Live change: rate automation and pitch message at `U = now + 0.02`, heard from
   `T = U + D / sampleRate`, anchor `{when: T + c(p)/sr, offset: positionOld(T + c(pOld)/sr), rate}`.
3. The processor applies a pitch message when `outFrames + (inFrames - outFrames) / 2 >= atFrame - firstFrame`.
4. SoundTouch settings fixed (quick seek 0, AA filter 1 with 64 taps, overlap 8 ms); the module has
   no imports, the host calls `_initialize` once; memory fixed at 4 MiB.
5. Node WASM suite adds "no drift over 100 jumps" and "mean content offset at 0.5 / 2 within 25 ms of c(p)".

### Code and skeleton

- `src/StemStudio.Contracts/Mixes/{PracticeSettingsDto, SpeedTrainerSettingsDto, CountInSettingsDto,
  PracticeLimits}.cs` (records with `Default`; limits and the target message), `MixSettingsDto.Practice`
  (init, default `PracticeSettingsDto.Default`).
- `src/audio-engine/src/types.ts` 2.3.0 (practice types, `StretchState`, `TrainerState`,
  `PracticeStatus`, `TimeStretchOptions`, `TimeStretchStage`, `EngineOptions.timeStretch` /
  `timeStretchFactory`, `setPractice`, `getPracticeStatus`, callback `OnPracticeChanged`);
  `index.ts` `ENGINE_CONTRACT_VERSION = '2.3.0'`; `engine.ts` stubs (`PRACTICE_NOT_IMPLEMENTED`, after
  the disposed check); `tests/practice-contract.test.ts`.
- `third_party/soundtouch/{README.md, NOTICE.md, COPYING.TXT, soundtouch.lock, .gitignore,
  include/stemstudio_soundtouch.h}`; `src/StemStudio.Web/wwwroot/js/.gitignore` ignores
  `time-stretch-processor.js(.map)` and `soundtouch/`.
- Tests: `tests/StemStudio.Api.Tests/Contract/PracticeContractTests.cs` (21: 1.11.0 = 1.21.0 + only
  the 012 entries, defaults and ranges against the schema, example round trip, pre-1.11.0 mixes load
  the defaults), `PracticeUiTestIdsTests.cs` (5: merge rule of ui-test-ids 1.11.0).

### Staging (PendingOperations style) and agreed one-time edits outside the lane

- No backend operation is added, so the `Pending*Operations` lists are unchanged.
- `tests/StemStudio.Web.Tests/Audio/EngineContractDriftTests.cs`: `PendingCallbacks = ["OnPracticeChanged"]`
  (in types.ts, not yet `[JSInvokable]`); the test fails once the frontend implements the callback
  until the name is removed (frontend T029/T030).
- `src/StemStudio.Web/Audio/EngineMethods.cs`: `ContractVersion = "2.3.0"` (one constant; precedent 006).
- `tests/StemStudio.Api.Tests/StemStudio.Api.Tests.csproj`: `Contract/backend-api.openapi.yaml` ->
  012 document; 1.21.0 linked as `Contract/011-claude-key/backend-api.openapi.yaml`; 012 ui-test-ids
  linked as `Contract/012/`. `ContractJson.Backend1210`; `ClaudeKeyContractTests` reads 1.21.0
  through it (assertions unchanged).
- Engine: version literal 2.3.0 in `tests/engine.test.ts` and `tests/karaoke-contract.test.ts`.

## Decisions taken (owner unavailable; ADR-021 "Phase 0 outcome" P1-P8 and ROADMAP)

1. emsdk **6.0.13** instead of 6.0.7 (newest 6.0 patch at least two weeks old; 7.0.0 too new).
2. Pin the **surina.net release tarball**, not the generated Codeberg archive.
3. **`SOUNDTOUCH_PREVENT_CLICK_AT_RATE_CROSSOVER`** mandatory (build switch; source unmodified).
4. **No WASM SIMD**.
5. **D = 5120 frames (116 ms)**; SC-001 (250 ms) unchanged.
6. **Content-offset compensation** `c(p)`, so clicks, karaoke and position stay with the stretched
   music (up to 40 ms apart otherwise).
7. **Midpoint rule** for applying pitch changes (avoids an audible wrong-pitch chirp on every speed change).
8. Version labels: the merged documents carry 012's labels (backend 1.11.0, ui-test-ids 1.11.0); the
   concrete rule is in `docs/ROADMAP.md`; engine 2.2.0 stays unused.

## Files changed

- `specs/012-practice-tempo/{research, plan, data-model, quickstart, spec, tasks}.md`,
  `contracts/{backend-api.openapi.yaml, ui-test-ids.md (new), ui-test-ids.delta.md, audio-engine.md,
  DELTA.md, check_superset.py}`
- `docs/decisions/ADR-021-practice-tempo-time-stretch.md`, `docs/ROADMAP.md`
- `third_party/soundtouch/**` (new), `src/StemStudio.Web/wwwroot/js/.gitignore`
- `src/StemStudio.Contracts/Mixes/*`, `src/StemStudio.Web/Audio/EngineMethods.cs`
- `src/audio-engine/src/{types,index,engine}.ts`, `src/audio-engine/tests/{engine,karaoke-contract,practice-contract}.test.ts`
- `tests/StemStudio.Api.Tests/{StemStudio.Api.Tests.csproj, Contract/(ContractJson, ClaudeKeyContractTests,
  PracticeContractTests, PracticeUiTestIdsTests).cs}`, `tests/StemStudio.Web.Tests/Audio/EngineContractDriftTests.cs`

## Tests and coverage

```text
dotnet build StemStudio.slnx -c Release --no-incremental        0 Warning(s), 0 Error(s)
dotnet format StemStudio.slnx --verify-no-changes               passed
dotnet test StemStudio.slnx -c Release -p:CollectCoverage=true   all passed, gates met
  Domain.Tests        752 passed   Domain        line 98.97 %  branch 94.52 %
  Persistence.Tests   114 passed   Persistence   line 98.91 %  branch 95 %
  Web.Tests          1875 passed   Web           line 99.58 %  branch 93.64 %
  Api.Tests          1682 passed, 2 skipped      Api line 98.96 %  branch 92.04 %; Contracts 100 % / 100 %
  Agent.Tests         721 passed   Agent         line 98.91 %  branch 94.18 %
  AgentEval.Tests     134 passed   AgentEval     line 99.83 %  branch 92.85 %
engine npm ci / lint / typecheck                                clean
engine vitest --coverage                                        21 files, 291 passed; 100 % statements 927/927,
                                                                branches 357/357, functions 311/311, lines 890/890
web-interop                                                     not touched in Phase 0
Redocly 2.78.0 lint, 012 backend-api 1.11.0                     valid, 9 warnings (operation-4xx-response, identical to 1.21.0)
check_superset.py vs 1.21.0 / 1.9.0                             superset: yes / yes (vs 1.10.0: exactly the 6 entries 1.21.0 itself changed)
tools/quality/run_all.py                                        2221 files, 0 errors (817 warnings; none in a new Phase 0 file)
scripts/secret_scan.py                                          3638 files clean
```

## Quality checks

SIG: new units <= 15 LOC, CC <= 5, <= 4 parameters (records excepted); the first draft of
`Defaults_MatchTheSchemaDefaults` (16 LOC) and an 11-line clone with `ClaudeKeyUiTestIdsTests` were
refactored away. Public API documented (XML docs, TSDoc).

## Dependencies added

| Name | Version | Licence | URL | Note |
|---|---|---|---|---|
| SoundTouch | 2.4.1 (2026-03-29) | LGPL-2.1 | https://www.surina.net/soundtouch/ , https://codeberg.org/soundtouch/soundtouch | compiled into the separate `soundtouch.wasm`; source shipped in binary distributions; not in git |
| Emscripten SDK Docker image | 6.0.13 (2026-10-19), digest in `soundtouch.lock` | MIT / University of Illinois NCSA | https://hub.docker.com/r/emscripten/emsdk | build tool only, not distributed |

No NuGet, npm or Python package added.

## Docs impact

None user-visible yet (Phase 0). For the integration wave: technical manual - SoundTouch module,
build step `scripts/build-soundtouch.*` (emsdk 6.0.13 by digest), replaceable module (LGPL), heard
latency `L(p)` 66-147 ms and the content-offset compensation, CSP note `'wasm-unsafe-eval'`.

## Open issues / requests

- **devops (T010/T011)**: use `soundtouch.lock` exactly (`em++`, not `emcc`; `-ffile-prefix-map` for
  the source and shim directories); the image is already pulled on the reference machine; add the
  Redocly lint of the 012 document to `scripts/test-all.*` / CI (`CONTRACTS_DIR` 012 for the backend).
- **quality-gate (T009)**: Sonar exclusions `third_party/soundtouch/upstream/**`,
  `src/StemStudio.Web/wwwroot/js/time-stretch-processor.js*`, `src/StemStudio.Web/wwwroot/js/soundtouch/**`.
- **audio-engine**: implement the shim from the header (`_initialize` comes from Emscripten); until
  T010 exists build with `docker run --rm --network none` and the lock flags.
- **frontend**: remove `OnPracticeChanged` from `PendingCallbacks` when T030 adds the `[JSInvokable]`.
- **CTO**: THIRD-PARTY-LICENSES rows above (T072); `docs/STATUS.md`; `stemstudio-api-1`/`web-1` show
  a restart about 21:30 that did not come from this work (no compose command was run).

## Lane start list (wave of at most 4 parallel agents, all from the Phase 0 commit)

| Order | Lane | Start | Why |
|---|---|---|---|
| 1 | **audio-engine** | now | critical path (23 tasks); T013-T018 and T021-T024 need nothing else; T019/T020 can use `docker run` with the lock until T010 lands |
| 1 | **devops** | now | short (3 tasks); T010 unblocks audio-engine T019/T020 and CI `test:wasm` |
| 1 | **backend** | now | independent (7 tasks); DTOs and the 1.11.0 document are in place |
| 1 | **frontend** (incl. the web-interop lane) | now | 20 tasks; depends only on types.ts 2.3.0, the DTOs and ui-test-ids 1.11.0 |
| 2 | **quality-gate** | in devops' slot when devops finishes | T008/T009 dependency rules should land before audio-engine HO:us1 |
| 3 | **qa** | in backend's slot when backend finishes | T012 probes and fixture first, then the specs per story (run by the CTO in T070) |
| - | CTO | after all HO:polish | T069-T075 |

## Next owner

The wave above; the CTO for the integration wave.
