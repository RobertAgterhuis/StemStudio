# Hand-off: audio-engine / 007 T046 (checkpoint HO:us2)

## What was built

`src/audio-engine/tests/render-vectors.test.ts` is a test-only Vitest consumer of
`specs/007-export-library/contracts/render-vectors.json` (1.0.0). It reads the file straight from the spec folder, so there is no copy. No engine source changed.

- **Audible gate cases** (mode `mix`, `channels = null`): `no-solo`, `solo-standard`,
  `solo-hidden-ignored`, `mute-and-solo`, `expanded-muted-detail`, `collapsed-details-hidden`,
  `nothing-audible` and `four-stem-model`. Each one runs through the engine's real code:
  1. `gate.ts` `gatesOf` + `isHidden` give gate 1 exactly for the expected inputs and 0 for every other channel.
  2. A real `Mixer` on the mock context gets `setExpanded`, `setMaster` and `setChannel` for every channel. A channel without an entry gets the mixer default: volume 0.8, pan 0. The ramp target of every channel gain equals the vector gain (0 when the channel is absent). The panner target equals the vector pan, and master equals `masterGain`.
  3. `expected.error = nothing-to-export` holds if and only if the engine is silent on every channel.
- **Selection cases** (`explicit-selection`, `raw-single`, `raw-detail`, `gain-zero-dropped`): the
  selection, raw and gain-0-drop rules belong to the planner, and the engine has no equivalent. The test
  checks that the channel strip (`Channel.configure` + `apply(1)`) of every listed input gives the vector
  gain and pan (raw: volume 1, pan 0).
- **Classification guard**: every vector must be audible, explicit-mix or raw. The audible list is pinned,
  so a new vector cannot be skipped silently.
- **Pan cases** (7): the engine passes the pan to `StereoPannerNode.pan` unchanged. Each matrix equals
  the Web Audio StereoPannerNode stereo formula (research.md E-05) within 1e-6. qa T048 checks the
  browser itself.
- Mutation check: making `anyAudibleSolo` count hidden solos fails 6 tests
  (`solo-hidden-ignored`, `collapsed-details-hidden`). The mutation was reverted.

**Result: the engine agrees with every vector (12 gate, 7 pan). No contract defect.**

## Files changed

- `src/audio-engine/tests/render-vectors.test.ts` (new)
- `specs/007-export-library/tasks.md` (T046 ticked)
- `docs/handoffs/audio-engine-007-us2.md` (this note)

## Tests and coverage

`npm ci; npx vitest run --coverage` in `src/audio-engine`:

```text
Test Files  10 passed (10)
     Tests  204 passed (204)        (44 of them in render-vectors.test.ts)
Statements   : 100% ( 565/565 )
Branches     : 100% ( 188/188 )
Functions    : 100% ( 203/203 )
Lines        : 100% ( 534/534 )
```

## Quality checks

- `npx eslint .`: 0 problems. `npx tsc --noEmit`: 0 errors.
- `uv run --project tools/quality python tools/quality/sig_check.py src/audio-engine`:
  `33 files, 667 units, 0 errors, 12 warnings, 0 exemptions`. All warnings were already there; none is in the new file.
- `tools/quality/duplication.py`: no clone involves `src/audio-engine`. It reports one error that was already there,
  outside this lane: `e2e/support/preferences.ts:12` duplicates 16 lines of `src/web-interop/src/types.ts:8`
  (qa / web-interop owners).

## Dependencies added

None.

## Open issues / requests

- None for the contract. Note for the architect: the engine has no explicit-selection, raw or gain-0-drop
  logic, by design (it is planner-only). Here those vectors are covered only through the channel strip.

## Next owner

qa (T047 / T048), CTO (HO:us2 merge).
