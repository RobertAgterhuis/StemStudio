# Hand-off: audio-engine / 016 us3 (T088-T089)

Lane L1, branch `016-round-lane-l1`. Contract: audio-engine 2.6.0 (`types.ts` unchanged).

## What was built

| Task | Red | Green | Result |
|---|---|---|---|
| T088/T089 | `65fe31b8` | `94eb21cf` (+ shared `ViewController` refactor in the HO commit) | `attachHitLanes`, `setHitLaneOptions`, `detachHitLanes`: `src/drums/{validate,hit-view,hit-lanes}.ts`, `src/views/canvas-surface.ts` (canvas sizing x `devicePixelRatio`, CSS palette, counted `Painter`), `src/timeline/view-controller.ts` (base of the chord lane and hit lane controllers) |

Behaviour (for L5 and qa):

- Host gets one `<canvas aria-hidden="true">` (Blazor sets `role="img"` + `aria-label` on the host). Lanes top to bottom:
  cymbals, hihat, toms, snare, kick (only `pieces` when given, lane height = host height / lane count). Now line at 20 %;
  window = `lookAheadMs` after it and a quarter of it before. Marks 8 px wide rounded rectangles, 40-100 % of the lane by
  strength, vertically centred; hits 0..30 ms behind the position are drawn in the flash colour.
- Colours from host CSS custom properties, read on attach/resize (not per frame): `--hit-lane-mark` (fallback `#3b82f6`),
  `--hit-lane-flash` (`#f59e0b`), `--hit-lane-now` (`#e5e7eb`). L5 should set them from the theme tokens.
- Reduced motion (explicit or media query, followed at runtime and through `setHitLaneOptions({reducedMotion})`): the
  window pages per bar of the beat grid (per `lookAheadMs` without one); the now line moves inside the page.
- Host attributes: `data-position-ms`, `data-next-hit-ms` / `data-next-hit-piece` (first drawn hit after the position,
  `''` none), `data-visible-hits`, `data-look-ahead-ms`; all removed on detach.
- `lookAheadMs` is clamped to 2000..8000; an unknown piece in the options throws `Invalid drum hits: unknown piece <p>`.
  Errors: `Hit lane host not found: <id>`, `Invalid drum hits: hits missing | more than 40000 hits | unknown piece <p> |
  strength outside 0..1 | times decreasing`; validation before replacing.

## Tests and coverage

`npx vitest run --coverage`: 64 files, 1259 tests passed. Engine total 99.73 % statements, 98.01 % branches, 99.59 %
functions, 99.75 % lines; `src/drums/**`, `src/timeline/**`, `src/views/**` 100 % lines (views 90 % branches: the
`devicePixelRatio`/`defaultView` fallbacks). Frame budget: a hit lane frame = visible hits + at most 9 primitives.

## Quality checks

`tsc`, `eslint` clean; `npm run build` ok; `run_all.py` PASS 0 errors, 442 clones (jscpd ran; a 36-line clone between the
two controllers was removed by `ViewController`); `dotnet format --verify-no-changes` exit 0.

## Security self-review

Hits are validated (count, piece whitelist, strength range, finite non-decreasing times); nothing is written as markup.

## Decisions

1. Canvas `aria-hidden="true"`; the accessible image is the Blazor host.
2. Clamp of `lookAheadMs` (no option error in the contract); unknown pieces rejected with the drum-hits message.

## Deviations / dependencies / blockers

None / none / none.

## Docs impact

None beyond the user-manual chapter.

## Next owner

L5 (drum trainer CSS custom properties). L1 continues with T097-T104 (singing feedback).
