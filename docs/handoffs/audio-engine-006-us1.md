# Hand-off: audio-engine / 006 US1 (T035, T036)

## What was built

`src/karaoke/` (only `engine.ts` imports it; `transport.ts`/`mixer.ts` do not):

| File | Role |
|---|---|
| `timeline.ts` | `buildTimeline` (validation, flattened words), `phaseAt` (hidden/countdown/singing/ended + dots), `validateCountdownGap`, `countdownDots` |
| `cursor.ts` | `Cursor` (O(1) forward, binary search after any jump), `lastAtOrBefore` |
| `dom.ts` | `buildBanner`, `fillSlot` (textContent only), `writeData` (writes only on change), `clearBanner` |
| `layout.ts` | `measureSlots` (word centre x / top y relative to the host, current line height), `LayoutWatcher` (ResizeObserver on host + slot words) |
| `ball.ts` | `hopProgress`, `hopHeight`, `ballAt`, `ballTransform` |
| `view.ts` | `KaraokeView`: one attached banner, `draw()` per frame |
| `index.ts` | `Karaoke` controller (TransportListener): attach/setOffset/redraw/detach |

Engine: `attachKaraoke`, `setKaraokeOffset`, `detachKaraoke` implemented (Phase 0 stubs and
`KARAOKE_NOT_IMPLEMENTED` removed); `setLoop` redraws one frame; `dispose` detaches.

Red -> green: `c44348d` (failing tests) -> `9af3d9c` (implementation).

### Behaviour details the frontend/qa need (contract interpretations)

- **Slots**: previous = line L-1 (all `past`), current = line max(L, 0), next = L+1 (all
  `future`). Before the first line the current slot already shows line 0 (all `future`) and
  the host has `data-line-index="-1"`.
- **Current word**: the last started word of the current line stays `current` until the next
  word starts or the line's `endMs` passes (then all its words are `past`, `data-word-index="-1"`).
- **Countdown**: `data-state="countdown"` for the whole time before the first line;
  `.karaoke-countdown[data-count]` is `0` until 3 s before, then `3`, `2`, `1` (one per second).
  Gaps: `countdown` from 3 s before the next line when the gap (`next.startMs - line.endMs`)
  exceeds `countdownGapMs` (default 5000). The countdown holds three `.karaoke-dot` children;
  CSS lights the first `data-count` dots.
- **Ball**: `.karaoke-ball` gets `style.transform = translate3d(<x>px, <y>px, 0px)` where `x` is
  the word centre and `y` the word top relative to the host's border box, minus the arc. CSS
  must position the ball at `left: 0; top: 0` (absolute, host `position: relative`) and centre
  it with negative margins, not with `transform` (the engine overwrites `transform`). The
  short-hop scaling uses the hop duration (start of word i to start of word i+1).
- **Layout**: geometry is read after the build (attach) and in `ResizeObserver` callbacks
  (host + the words in the slots), never in the rAF callback. After a line change the new words
  are observed; the browser delivers the observation after layout in the same rendering step,
  where the engine measures and places the ball before paint. Without `ResizeObserver` the slots
  are measured right after they are filled.
- **Validation** (`Invalid karaoke lyrics: <reason>`): lines not an array, > 400 lines, a line
  with no words or > 40 words, non-finite times, `endMs <= startMs`, line starts decreasing, word
  starts decreasing (also across lines), a word outside its line, a word without string text.
  Options: `countdownGapMs < 0` -> `Invalid karaoke options: ...`. Everything is validated
  before an attached banner is replaced (a failing attach keeps the old banner).
- `setKaraokeOffset` without an attached banner validates and stores the offset (the next
  `attachKaraoke` sets its own).
- `detachKaraoke` removes the children and the engine's `data-state`, `data-line-index`,
  `data-word-index`, `data-position-ms`; Blazor's `data-sync` stays.
- `options.reducedMotion` is accepted but takes effect with T052 (US3).

## Files changed

- `src/audio-engine/src/karaoke/{timeline,cursor,dom,layout,ball,view,index}.ts` (new), `src/engine.ts`
- `src/audio-engine/tests/karaoke/{timeline,cursor,dom,layout,ball,attach}.test.ts` (new),
  `tests/karaoke-contract.test.ts` (stub test replaced), `tests/mocks/{karaoke-rig,layout}.ts` (new)
- `specs/006-synced-lyrics-karaoke/tasks.md` (T035, T036 ticked)

## Tests and coverage

```text
npm run test:coverage   19 files, 280 passed
Statements 100% (895/895)  Branches 100% (347/347)  Functions 100% (302/302)  Lines 100% (857/857)
```

Landing: 0 ms error at every word start on the mock clock; with 16.7 +- 5 ms jittered frames
every word becomes current within its frame interval. No `getBoundingClientRect` call in
14,000 ms of frames; steady frames mutate only the ball `style` and `data-position-ms`.

## Quality checks

- `npm run lint`, `npm run typecheck` clean; `npm run build` OK (65.4 kB, gzip 19.4 kB).
- `sig_check.py src/audio-engine/src`: 0 errors, 0 warnings. `dependency_check.py`: 0 errors.
  `duplication.py`: 0 clones in production code.
- No `setTimeout`/`setInterval` in `frame-loop.ts` or `karaoke/`; no interop per frame (tested).

## Dependencies added

None.

## Open issues / requests

- **architect**: please fold the interpretations above (current slot before the first line,
  countdown before the first line with `data-count`, `.karaoke-dot`, `0px` in the transform,
  word-start ordering across lines) into `audio-engine.md`; and the latency-before-wrap note from
  `audio-engine-006-foundational.md`.
- **frontend (T037/T038)**: host `position: relative`; ball positioned with `left/top: 0` and
  margins; style `.karaoke-word[data-state]`, `.karaoke-countdown[data-count] .karaoke-dot`,
  host `[data-state=hidden|ended]`. Call `attachKaraoke` after `loadTrack` (loading a new track
  does not redraw the banner by itself).
- **qa (T039)**: `data-position-ms` is the karaoke clock (offset applied).

## Next owner

frontend (006 T037/T038), qa (T039); audio-engine continues with T051/T052.
