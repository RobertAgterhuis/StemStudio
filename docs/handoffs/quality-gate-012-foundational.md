# Hand-off: quality-gate / 012 foundational (T008, T009)

Branch `012-lane-quality` (from `012-practice-tempo` 010c3e6d). Commits 097cf38c (red tests, T008)
and 3b631dc6 (implementation, T009). The owner was unavailable; the decisions below were taken by
quality-gate and are listed under "Deviations".

## What was built

### Dependency rules (`tools/quality/dependency_check.py`)

| Rule | Where | Severity |
|---|---|---|
| `transport`, `mixer`, `metronome/`, `karaoke/` -/- `practice/`, `stretch/` | `TS_MODULE_FORBIDDEN` (module level, type-only imports included) | error |
| `practice/` -/- `karaoke/` | `TS_MODULE_FORBIDDEN` | error |
| `stretch/processor.ts` imports only `stretch/{constants,params,fifo,abi,core}` (any import: value, type-only, dynamic, npm) | `TS_WORKLET`, `check_worklet` (file level) | error |
| `stretch/{constants,params,fifo,abi,core}` value-import only each other (`import type`/`export type` are erased by the build and may point anywhere) | `check_worklet` | error |

Message: `worklet scope: <file> must not import <module>`. The 006 karaoke rules are unchanged.
`run()` now calls `check_typescript(root)` (package edges, module edges, worklet scope).

### C++ shim measured, third-party and build output excluded

- `common.SOURCE_SUFFIXES` += `.cpp`, `.h` (lizard measures C++ with the same limits: size <= 15
  warn / > 30 error, CCN <= 5 / > 10, <= 4 parameters); `third_party` is a default root;
  `third_party/soundtouch/` is production code (hard limits, production clones fail).
- `EXCLUDED_PATHS` += `third_party/soundtouch/upstream/` (SoundTouch 2.4.1, LGPL-2.1, downloaded),
  `third_party/soundtouch/build/`, `src/StemStudio.Web/wwwroot/js/time-stretch-processor.js(.map)`,
  `src/StemStudio.Web/wwwroot/js/soundtouch/`, `src/StemStudio.Web/wwwroot/third-party/`.
- `.jscpd.json`: format `cpp` added; the same five paths ignored.
- Ownership (`OWNERS`): `third_party/soundtouch/shim/` -> audio-engine, `third_party/soundtouch/` ->
  architect (README, NOTICE, COPYING, lock, `include/`). New engine files fall under
  `src/audio-engine/` (audio-engine), new Web files under `src/StemStudio.Web/` (frontend), the
  build scripts under `scripts/` (devops); all asserted in tests.
- Component rows: `audio-engine/stretch`, `audio-engine/practice` (sub-rows of `audio-engine`),
  `soundtouch shim`.

### Sonar (`sonar-project.properties`)

- `sonar.sources` += `third_party/soundtouch` (shim and header stay in the analysis).
- `sonar.exclusions` and `sonar.cpd.exclusions` += `third_party/soundtouch/upstream/**`,
  `third_party/soundtouch/build/**`, `src/StemStudio.Web/wwwroot/js/time-stretch-processor.js`,
  `...time-stretch-processor.js.map`, `src/StemStudio.Web/wwwroot/js/soundtouch/**`,
  `src/StemStudio.Web/wwwroot/third-party/**` (architect T006 request + devops request).
- No pattern excludes the shim or `**/*.cpp` (asserted).

## Files changed

- `tools/quality/{common.py, components.py, dependency_check.py, .jscpd.json, README.md}`
- `tools/quality/tests/{test_practice_rules.py (new, 50 tests), test_lyrics_rule.py, test_components.py}`
- `sonar-project.properties`, `docs/quality/SIG-REPORT.md` (regenerated)
- `specs/012-practice-tempo/tasks.md` (T008, T009 ticked)

## Tests and coverage

```text
uv run --directory tools/quality pytest --cov --cov-branch     716 passed
  TOTAL 1370 statements, 282 branches: line 100 %, branch 100 % (every module 100 / 100)
uv run --directory tools/quality ruff check . / ruff format --check .   clean / 42 files formatted
uv run --directory tools/quality mypy                            no issues in 41 source files
uv run --project tools/quality python tools/quality/run_all.py --report docs/quality/SIG-REPORT.md
  quality gate: 2223 files, 17492 units, 252 clones, 3 exemptions, 0 errors, 816 warnings -> exit 0
```

Red step (097cf38c): the new test module failed at collection (`TS_WORKLET` missing); after the
first implementation 24 tests failed until the Sonar keys and the C++ fixture were right.

## Quality checks

`sig_check` on the changed modules: 104 units, 0 errors, 0 warnings (`_worklet_findings` and
`run` were split to stay <= 15 LOC / CCN <= 5). Duplication 1.02 %. Dependency direction PASS.

Preview against the audio-engine lane (`012-lane-engine` aa77db0d, read-only `git
archive` into the scratchpad): `dependency_check` 0 errors; the real shim
`third_party/soundtouch/shim/stemstudio_soundtouch.cpp` 14 units, all <= 15 LOC, CCN <= 5,
<= 3 parameters (0 warnings).

## Deviations (owner unavailable; decided by quality-gate)

1. **`stretch/core` added to the worklet allow-list.** tasks.md T008 lists
   `stretch/{constants,params,fifo,abi}`; the audio-engine lane split the WSOLA driver out of
   `processor.ts` into `stretch/core.ts` (imports only `abi`, `constants`, `fifo`). Keeping the
   processor small is the better SIG result, and the rule's purpose (no DOM or engine code in the
   worklet bundle) holds because every worklet module is itself restricted.
2. **Worklet rule is transitive for value imports.** The plan only constrains `processor.ts`;
   without restricting its imports, `core.ts` could pull engine code into the bundle. Type-only
   imports are allowed in the non-entry modules (`params.ts` reads `PracticeSettings` from
   `../types`); the entry stays strict.
3. **Header measured too.** `include/stemstudio_soundtouch.h` (architect) is in scope for lizard; it
   has no function bodies, so it yields no units, but any inline code added later is measured.
4. **No "Web loads WASM only via the engine" rule.** The plan names no such rule and the Web
   project never references `soundtouch.wasm` (it is loaded by the engine's worklet stage); a text
   rule would only guard a hypothetical. Revisit if a .cs/.razor file starts to reference it.
5. **C/C++ in Sonar.** The shim is in `sonar.sources`, but SonarQube analyses C/C++ only in a
   commercial edition and with a compilation database (build-wrapper or `compile_commands.json`);
   in Community edition it is skipped and measured by `tools/quality` only (comment in the file).

## Dependencies added

None.

## Docs impact

None user-visible. Technical manual (CTO): the quality gate also measures the C++ shim and
excludes the downloaded SoundTouch source.

## Open issues / requests

- **devops**: if CI ever runs a commercial SonarQube edition, either provide a compilation database
  for the shim (`em++` flags from `soundtouch.lock`) or set `sonar.cfamily...` accordingly; the
  current Community setup needs nothing.
- **audio-engine**: see `quality-gate-012-polish.md` (one hard error on the lane head).

## Next owner

audio-engine and frontend (requests in the polish note), then the CTO (integration, T070).
