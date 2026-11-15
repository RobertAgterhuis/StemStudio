# Hand-off: frontend / 006 US2 (lyrics card: preview, alignment, paste)

Tasks 006:T046 (red) and T047 (green).

## What was built

- **Client** (backend 1.6.0): `SetLyricsAsync` (`PUT .../lyrics`), `AlignLyricsAsync` (`POST .../lyrics/align`),
  `CancelLyricsAlignmentAsync` (`POST .../lyrics/align/cancel`).
- **`Components/Lyrics/LyricsSection`** (+ `.Actions.cs`), on the track page of completed tracks (after the detail
  section). It inherits `PollingComponent` and polls while `searching`/`aligning`.
  - `lyrics-section` (`data-help-id="lyrics-sources"`), `lyrics-status` (`data-status` = wire value; text such as
    "Found, timed per word", "Waiting for alignment"), `lyrics-credit`.
  - The backend reason is shown as help text for notFound/disabled/instrumental (`.ss-lyrics-reason`).
  - `lyrics-error` holds **only** the reason (failed lookup or alignment, a refused action, a load error), so the
    E2E exact-text check works.
  - `lyrics-preview`: one `<p>` per line, for alignPending and failed-with-text.
  - `lyrics-copyright-notice`.
  - After a failed alignment while the vocals are not split: hint `.ss-lyrics-split-hint` "Split the vocals into
    detail stems and try again." (research P0-2).
- **`LyricsAlignment`**:
  - `lyrics-align` ("Align to the vocals"; "Try again" when failed with text; Expert only for found + line timing,
    feature `lyrics.refine`).
  - `lyrics-align-estimate`: the backend reason while alignPending (e.g. "Aligning takes about 7 minutes on the
    CPU."), else "Aligning takes about N seconds/minutes." from `alignment.estimateSeconds`.
  - While aligning: `lyrics-align-progress` (`RadzenProgressBar`, `aria-valuenow`), `lyrics-align-device` (003
    `DeviceBadge`, feature `lyrics.align-device`, Advanced+) and `lyrics-align-cancel`.
- **`LyricsPasteForm`**: `lyrics-paste-open` opens an inline form with `lyrics-paste-input` (textarea,
  `maxlength=20000`, focused), a character count and `lyrics-paste-submit`.
  - Client-side check (`Lyrics/LyricsPaste`): empty, more than 20,000 characters, control characters other than
    tab/CR/LF.
  - Refusals (client or 422/409) appear in `lyrics-paste-error` and keep the text. Paste is not offered while
    searching or aligning.
- **Help**: `tracks/lyrics-sources.md` (LRCLIB, what is sent, statuses, copyright) and `tracks/lyrics-align.md` (GPU
  automatic, CPU on click with the estimate, the 1.6 GB model, try again). `karaoke` links to both.
- **Legal notice**: the import page `legal-notice` adds the sentence "Lyrics are fetched from LRCLIB; they are shown
  in the app only, for your personal practice, and never exported." (marker `lyrics-sources`).
- **CSS**: `css/components/lyrics.css` (tokens only).

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T046 / T047 | 4a69ea4 (build fails) | 32ad025 |

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true
Passed! - Failed: 0, Passed: 1122, Total: 1122
| StemStudio.Web | 99.49% line | 92.92% branch | 99.89% method |
```

## Quality checks

- Build: 0 warnings. `dotnet format --verify-no-changes`: clean.
- `sig_check.py src/StemStudio.Web`: 0 errors. No new warning in production code.
- `css_tokens_check.py`: 0 errors.

## Dependencies added

None.

## Notes for qa

- Extra ids not in ui-test-ids 1.6.0: `lyrics-paste-error`, and `lyrics-align-device-fallback` (the `DeviceBadge`
  reason id).
- `lyrics-paste-submit` is a plain button. The text is sent as typed; the backend splits lines at line breaks.
- The lyrics card exists only for completed tracks.

## Next owner

frontend (006 US3: T053/T054); qa T048.
