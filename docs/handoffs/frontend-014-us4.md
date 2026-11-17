# Hand-off: frontend 014, HO:us4 (lane L5, round 014+015)

US4 "The score follows the playback". Merged L1's score follower (c0b56164, `audio-engine-014-us4.md`).

## Tasks (red -> green)

| Task | Commit |
|---|---|
| Wrapper: `AttachScoreFollowerAsync` / `SetScoreFollowOptionsAsync` / `DetachScoreFollowerAsync`, `LoopPassed` (`OnLoopPass` [JSInvokable], removed from the drift test's staged callbacks), score method names checked against `types.ts` | 2bb874ff |
| T082 failing tests `ScorePanelTests` | da0f4d65 |
| T083 `Components/Practice/ScorePanel.razor(.cs)`, `Mixer.Score.cs`, `mixer.score.toggle` / `mixer.score.follow` | 94672dfe |
| T085 help `mixer-score-follow`; tour `hands-free` gets the `score-panel` step | 68130055 |

`EngineMethods.cs` stays untouched (shared read-only): the three score method names live in
`Audio/AudioEngineInterop.Score.cs` (`AudioEngineInterop.ScoreMethods`).

## Behaviour

- `score-panel-toggle` (all levels, `aria-expanded`, key F) shows `score-panel` (`role="region"`, `aria-label="Score"`,
  `data-asset-id`, `data-view-status`). `score-select` options "Bass - <name>" (profile wire name capitalised).
  `score-empty` links to the song page (`tracks/<id>`), where 008's score dialog is.
- View loading: `getScoreView`; status `none` -> `createScoreView`; `score-preparing` while none/queued/rendering (004
  polling); failed -> `score-error` with the server text.
- Pages: `score-view` host (`id="score-view"`, `tabindex="0"`, `role="group"`, so scroll keys work and pause auto-scroll),
  one `score-page-<n>` container per page (`data-score-page`) with an `<img>` from the public API base URL and one
  absolutely positioned click target per bar (`.ss-score-bar`, in percent). The engine's `.score-highlight` (pointer
  events none) is styled as an outline plus a hatched fill (not colour alone).
- Attach after the pages rendered and the engine is ready; detach on another choice, on close (F) and on dispose.
  Engine refusals show in `score-error`.
- `score-follow-toggle` (`aria-pressed`, Shift+F): `setScoreFollowOptions({follow})`; turning it on ends a scroll pause.
  `reducedMotion` is true when the user chose Motion "Reduced", else left to the browser setting.
- Click a bar -> seek to its start. Shift+click (not Beginner) -> loop from the bar holding the loop start (when it is
  before the clicked bar) or the clicked bar, to the clicked bar's end; the loop is switched on.
- `score-transpose-note` while transpose != 0; `score-tempo-note` for manual-tempo scores.
- `score-bar-label` ("Bar 17 of 64", `aria-live="off"`, recomputed at most once per second of position);
  `score-read-bar` writes the text once into a `role="status"` region ("Not in a bar" outside the bars).

## Quality

| Component | Tests | Line | Branch |
|---|---|---|---|
| StemStudio.Web (Release, coverage) | 3363 passed (incl. L6's merged suites) | 99.48 % | 93.54 % |
| web-interop | 412 passed | 99.8 % | 98.29 % |

- Release build 0 warnings; format clean except L6's `RecordingApiClientTests.cs:103`; `run_all.py` PASS (0 errors,
  jscpd ran). SIG warnings: `ScorePanel.LoadAsync` 16 NLOC, `BarClickAsync` CCN 7.
- Hook C (`MixerState.ExternalSolo`) was delivered in a4aec34e.
