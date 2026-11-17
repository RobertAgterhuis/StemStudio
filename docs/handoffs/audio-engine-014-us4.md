# Hand-off: audio-engine, 014 HO:us4 (T080-T081, score follower)

Lane L1, branch `014-015-round-lane-l1-engine`, 2026-11-17. Contract 2.4.0 "Score follower"; `types.ts` unchanged.

## Commits

| Commit | Task |
|---|---|
| `7b861d4f` | T080 failing tests `tests/score-follow.test.ts` |
| `13a6b245` | T081 `src/score-follow/{follower,cursor,dom,scroll,validate}.ts` and the engine methods |
| (this hand-off commit) | `FrameSubscription` in `frame-loop.ts`, shared by karaoke and the follower (removes a jscpd clone) |

## Behaviour (for L5, frontend 014)

- `attachScoreFollower(elementId, view, options)` validates everything before it replaces an attached follower:
  - `Score host not found: <id>`
  - `Invalid score view: pages must be 1..200 | duplicate page <n> | bars must be 1..2000 | bar <i> is on unknown
    page <p> | bar <i> box outside 0..1 | bar <i> ends before it starts | bar <i> does not follow bar <j>`
  - `Invalid score follow options: scrollPauseMs must be 0..60000`

  The engine adds one `.score-highlight` per `[data-score-page="<n>"]` container. Each has `aria-hidden="true"`,
  `data-state="idle|current"`, an inline `position: absolute` and `pointer-events: none`, so bar clicks reach
  Blazor's images. Placement is in percent. Colours and outline come from the app CSS. A page without a container
  gets no highlight, but the host attributes still follow.
- Host attributes: `data-bar-index` (-1 for none), `data-bar-number`, `data-page` and `data-position-ms`, written on
  every drawn frame. `detachScoreFollower` removes them and the highlights.
- The clock is `transport.positionAt(heardTime(...))`, the karaoke clock without an offset. Tests cover 50, 75, 100
  and 120 % speed, loop wraps, seeks, pause and stop.
- Scrolling: `scrollIntoView({ block: 'center', inline: 'nearest', behavior })` once per system change (a new page,
  or `|y change| > 0.02`).
  - With reduced motion (default `matchMedia('(prefers-reduced-motion: reduce)')`) the behaviour is `auto`, and it
    scrolls only when the highlight is outside the host's box. This layout read happens once per system change,
    never per frame.
  - A `wheel`, a `touchmove`, or a `keydown` of PageUp/PageDown/Home/End/arrow keys inside the host pauses scrolling
    for `scrollPauseMs`.
  - `setScoreFollowOptions({ follow: true })` ends a pause and scrolls on the next frame. This is the "until Follow is
    pressed" part of FR-011.
- Frames: the follower draws while it is attached and the transport is playing. It draws one extra frame after
  attach, seek, pause, stop, loop change (`setLoop`), a live rate change and an option change. Style writes happen
  only on a bar change; the internal `stats` counter is tested.
- Loading a new track does not detach the follower (contract). `dispose()` detaches it.

## Tests and coverage (engine, full suite)

| Component | Tests | Lines | Branches | Functions | Statements |
|---|---|---|---|---|---|
| audio-engine | 749 passed | 99.80 % | 97.94 % | 99.79 % | 99.86 % |

`src/score-follow` on its own is about 97 % lines and 94 % branches. `tsc`, `eslint --max-warnings 0` and the build
are clean. `tools/quality/run_all.py`: PASS, 0 errors. The first run failed on two clones between karaoke and the
follower; the shared `FrameSubscription` fixed that. `dotnet format --verify-no-changes`: clean.

## Decisions

- D1: `setScoreFollowOptions` without an attached follower validates its options and otherwise does nothing.
- D2: the frame cost of SC-004 is measured by the CTO's performance trace. In Vitest, the "frame cost counter" is
  the draw/bar-change counter: no style writes and no layout reads between bar changes.

No deviations and no new dependency.
