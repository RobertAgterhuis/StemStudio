# Hand-off: frontend / 006 US3 (banner controls, search again, remove)

Tasks 006:T053 (red) and T054 (green).

## What was built

- **`karaoke-toggle`** (`aria-pressed`). Hiding the banner removes the host, the controls and the
  `karaoke-unavailable` notice, and calls `DetachKaraokeAsync`. Showing it again renders a fresh host and attaches
  it again.
- **`karaoke-size`** (Advanced+): native select `s`/`m`/`l`. The value goes to the host's `data-size` attribute, and
  the engine's `MutationObserver` re-measures.
- **`Lyrics/KaraokePreferences`** (scoped): stores the choices in `localStorage["stemstudio.karaoke"]` as
  `{"visible":bool,"size":"s|m|l"}` through `IJSRuntime`.
  - Read errors, unreadable JSON and unknown sizes fall back to the defaults (shown, `m`).
  - A blocked write is ignored.
  - The key sits next to the 004 preferences (not inside them), so the 004 preferences contract is unchanged.
- **Reduced motion**: when the app's own Preferences motion setting is "reduced", `attachKaraoke` gets
  `reducedMotion: true`. Otherwise the option is left out and the engine follows the OS live.
  `karaoke.css` also hides the ball under `html[data-motion=reduced]` and underlines the current word.
- **Contrast** (FR-011): the banner background is `--ss-color-raised`; word colours are ink (past), muted (future)
  and `--ss-stem-vocals` (current). A test checks that every text colour in `karaoke.css` is one of these tokens and
  that each is >= 4.5:1 on the banner in both themes.
- **Track page**:
  - `lyrics-search` for removed/notFound/failed/disabled/found. It polls the new search.
  - `lyrics-remove` (Advanced+, feature `lyrics.remove`), only when there is something to remove and nothing is
    running. It opens the shared confirm dialog ("... will not search for them again by itself ...").
- **Client**: `SearchLyricsAsync` (`POST .../lyrics/search`), `RemoveLyricsAsync` (`DELETE .../lyrics`).

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T053 / T054 | aed7237 (build fails) | 2eb4daa |

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true
Passed! - Failed: 0, Passed: 1156, Total: 1156
| StemStudio.Web | 99.5% line | 93.28% branch | 99.89% method |
```

## Quality checks

- Build: 0 warnings. `dotnet format --verify-no-changes`: clean.
- `sig_check.py src/StemStudio.Web`: 0 errors. No new warning in production code.
- `css_tokens_check.py`: 0 errors.

## Dependencies added

None.

## Notes for qa

- "Toggle persists across reload" reads `localStorage["stemstudio.karaoke"]`. A fresh storage state shows the banner.
- `searchLyrics` answers `searching`. With lookups off, the next poll (about 1 s later) shows `disabled`.

## Next owner

frontend (006 polish T060); qa T055.
