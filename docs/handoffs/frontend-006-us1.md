# Hand-off: frontend / 006 US1 (karaoke banner in the mixer)

Tasks 006:T037 (red) and T038 (green).

## What was built

- **Engine 2.1.0 interop** (`Audio/Karaoke.cs`, `IAudioEngine`, `AudioEngineInterop`, `EngineMethods`):
  `AttachKaraokeAsync(elementId, KaraokeLyrics, KaraokeOptions)`, `SetKaraokeOffsetAsync(int)`,
  `DetachKaraokeAsync()` (no-op without an engine). `KaraokeOptions.ReducedMotion` is left out of the JSON when
  null, so the engine follows the OS preference live. The karaoke records print no lyric text in `ToString()` (FR-010).
- **Client** (backend 1.6.0): `GetLyricsAsync` (`GET .../lyrics`), `SetLyricsOffsetAsync` (`PUT .../lyrics/offset`).
- **`Lyrics/`**: `KaraokeMapper` (only `found` + word/line documents whose every line and word has times),
  `LyricsText` (`data-status` values, `data-sync` = `aligned`/`word`/`line`, Expert timing origin, mixer notices),
  `OffsetInput` (snap to 10 ms, clamp ±5000, 50 ms arrow-key step).
- **`Components/Mixer/Karaoke/KaraokeBanner`** (+ `.Offset.cs`):
  - Host `<div id="karaoke-host" @key data-testid="karaoke-banner" data-sync data-help-id="karaoke" aria-live="off">`
    rendered empty.
  - Attached in `OnAfterRenderAsync` only once `EngineReady` (after `loadTrack`). A new document re-attaches;
    dispose detaches (an engine that is already gone is tolerated). Attach errors are shown in `.ss-karaoke-error`.
  - `karaoke-credit`, and `karaoke-unavailable` (`data-status`, readable reason, link `tracks/<id>`) instead of
    the host when there are no timed lyrics.
  - Polls `GET .../lyrics` about once per second while `searching`/`aligning` (`PollLoop`).
  - `karaoke-offset`: native range -5000..5000 step 10, `aria-valuenow` in ms. It is applied to the engine at once
    and saved debounced (500 ms, `TimeProvider`); a pending value is flushed on dispose. Arrow keys move it by 50 ms;
    `karaoke-offset-reset` sets 0.
- **Mixer layout**: `.ss-mixer-dock` (sticky at the bottom) holds the banner and then the transport, so the banner
  runs along the bottom of the screen and never covers the transport. The dock is the console's last child.
- **Levels** (catalogue): `karaoke.size`, `karaoke.offset` (Advanced+), `karaoke.timing-origin` (Expert,
  `.ss-karaoke-origin`). Beginner sees the banner, the credit and `karaoke-toggle`.
- **Help**: `mixer/karaoke.md`, `mixer/karaoke-offset.md` (feature `karaoke.offset`).
- **CSS**: `css/components/karaoke.css` (tokens only), registered in `App.razor`:
  - host `position: relative`;
  - `.karaoke-ball` absolute at left/top 0, centred with negative margins (never `transform`);
  - `data-size` s/m/l, `.karaoke-word[data-state]`, countdown dots by `data-count`, host states;
  - reduced motion: the ball is hidden and the current word is underlined.

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T037 / T038 | 2baf69b (build fails) | f17ebb9 |

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true
Passed! - Failed: 0, Passed: 1075, Total: 1075
| StemStudio.Web | 99.47% line | 92.66% branch | 99.88% method |
```

The only uncovered new branches are the hidden-banner paths (`Visible == false`), which US3 (T053/T054) covers.

## Quality checks

- `dotnet build` 0 warnings.
- `dotnet format StemStudio.slnx --verify-no-changes`: clean.
- `sig_check.py src/StemStudio.Web tests/StemStudio.Web.Tests`: 0 errors. No new warning in production code.
- `css_tokens_check.py`: 0 errors.

## Dependencies added

None.

## Notes for qa

- `transport-seek` still takes seconds. `karaoke-offset` is a native `input[type=range]`, so `setNumericValue`
  assigns the value directly.
- `karaoke-unavailable` replaces the host, so the host count is 0. There is no `karaoke-unavailable` while the
  banner is hidden (US3).

## Next owner

frontend (006 US2: T046/T047); qa T039.
