# Hand-off: audio-engine / 002-us2

## What was built

Stem groups of engine contract 2.0.0 (FR-012..FR-014, SC-004), tasks T058-T061:

- `src/groups.ts`:
  - `validateGroups(stems, groups)` runs in `loadTrack` before any fetch and rejects
    `Unknown group parent "x"` (a parent that is not a loaded, splittable stem; `bass` is never
    splittable), `Duplicate group "x"` and `Duplicate stem "drums.kick"`.
  - `sourcesOf` / `groupChannels` map detail stems to `<parent>.<detail>` channel ids.
  - `StemGroups.setExpanded(parent, expanded)` rejects `No detail group for "x"` for a parent
    without a group. The first expansion fetches and decodes that group only, exactly once:
    concurrent calls share the download and a cached load serves every later expansion.
    Collapsing needs no download and resolves at once.
  - Load failures and superseded loads: a failed load rejects with the loader's message
    (`Failed to load stem "drums.snare": HTTP 404`), keeps the parent collapsed and allows a
    retry. A new track that arrives while a group is still loading rejects that expansion with
    `Track changed while loading the detail stems of "drums"` and drops the stale buffers. After
    `dispose()` the call rejects with `Audio engine has been disposed`.
  - Collapsing while a group loads wins: the load finishes and the buffers are attached, but the
    parent stays audible.
- Swap: one context time `at = max(currentTime + 0.01, shared when)` is used both for starting
  the new detail sources and for every gate ramp (`Mixer.setExpanded`, <= 10 ms ramps). At
  default settings the parent fades out while the detail channels fade in on the same time
  constant, so the sum stays equal to the parent.
- `transport.ts`:
  - `addBuffers(buffers, at)` adds decoded detail buffers. While playing it starts them with
    `start(at, offsetAt(at))` on the shared anchor, wrapped into an active loop and with the loop
    properties copied.
  - `startTimeAfter(delay)` returns the swap time. `offsetAt(time)` is shared with `position()`.
  - After the load, play, seek, loop changes, pause, stop and the end of the track treat detail
    sources exactly like standard sources (one shared `when`). `load()` drops the detail buffers.
- `stem-loader.ts`:
  - `loadStems` is generic over the name (stem name or channel id), so its error messages name
    detail stems as `drums.snare`.
  - `assertUnique` is exported. New types: `ChannelBuffers` and `NamedSource`.
- `engine.ts`:
  - `setChannel(ChannelId)` and `setExpanded` are wired.
  - `loadTrack` validates and resets the groups and builds the detail channels, which start
    hidden and are not fetched.
  - `attachMeter('drums.snare', ...)` works through the mixer analyser lookup. Because the meter
    tap sits after the gate, a hidden channel's meter falls to -60 dB.
- `meters.ts`: TSDoc only. Meter lookup already resolves any `MeterTarget`, so no code change
  was needed.

Red -> green:

| Tasks | Red | Green |
|---|---|---|
| T058 / T059 groups | `4b419f3` | `ff6bfe3` |
| T060 / T061 SC-004 with groups | `d54c497` (green on first run, see below) | no code change needed |

T060 passed on its first run. T059 already starts groups from the shared transport anchor, so
there was no red state to reach. As evidence that the test detects drift, I ran a temporary
mutation (offset of new detail sources + 2 ms) and reverted it afterwards. With the mutation, the
50-repetition test and three T058 tests failed.

## Files changed

- `src/audio-engine/src/{groups.ts (new), engine.ts, transport.ts, stem-loader.ts, meters.ts}`
- `src/audio-engine/tests/{groups.test.ts (new), engine.test.ts (T004 skeleton test removed), transport.test.ts (types)}`
- `src/audio-engine/tests/mocks/{audio-context.ts (MockAudioBuffer.sourceBytes), fetch.ts (hold(prefix))}`
- `specs/002-detail-separation/tasks.md` (T058-T061 ticked)

## Tests and coverage

`npm run test:coverage` (Vitest 5.1.0, v8):

```text
Test Files  9 passed (9)
     Tests  157 passed (157)
Statements   : 100% ( 558/558 )
Branches     : 100% ( 188/188 )
Functions    : 100% ( 201/201 )
Lines        : 100% ( 527/527 )
```

The SC-004 test runs a loop of 1.37-3.91 s and expands the group in the 4th repetition. After
each of the next 50 repetitions, the browser playheads of vocals, kick, snare and rest and of the
hidden drums parent lie within 1 ms of each other and of `getPosition()`.

## Quality checks

- `npx tsc --noEmit` and `npx eslint .`: clean.
- `npm run build`: OK (`audio-engine.js` 41.70 kB, gzip 12.54 kB).
- `uv run --project tools/quality python tools/quality/run_all.py src/audio-engine`:
  `32 files, 607 units, 0 clones, 0 exemptions, 0 errors, 13 warnings -> exit 0`. All warnings
  are test-unit sizes from 001, plus the existing `detail` dependency-map note.

## Dependencies added

None.

## Open issues / requests

1. Minor edge case: a `setChannel` in the <= 10 ms between a `setExpanded` call and its swap time
   cancels the scheduled swap ramp. The new gates then start at once instead of at the swap time.
   The worst case is a gap of up to 10 ms on the parent right after play. This is within the
   ramp tolerance, so I left it unchanged.
2. Unknown detail ids still report `Unknown stem "drums.toms"` (same wording as v1 for standard
   stems).
3. Frontend: call `setExpanded` after `loadTrack` for every parent in `MixSettingsDto.Expanded`,
   then re-apply `setChannel` for the detail channels. A new track starts with every parent
   collapsed.

## Next owner

frontend (T062/T063 group strips) and qa (T064). audio-engine continues with T085 (polish).
