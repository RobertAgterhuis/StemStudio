# Hand-off: backend / 006-us2 (T044-T045)

## What was built

- **Automatic rule** (`Lyrics/LyricsAligner.cs`, data-model 3.2):
  - Plain lookup results and pasted text become `alignPending` with the rule pending (`TrackLyrics.AutoAlign`).
  - Once the standard separation is completed, the synchroniser (or the paste request) reads the worker
    `/health`. When `lyricsAlignment.auto` allows it (`always`, or `gpu` on a GPU worker), it starts the alignment
    at once.
  - Otherwise it stores the estimate for the worker's device as `alignment.estimateSeconds` and as the reason, e.g.
    "Aligning takes about 3 minutes on the CPU." The estimate uses the worker formula on the track duration, plus
    140 s when `modelCached` is false.
  - The rule is evaluated once. An older worker without `lyricsAlignment` counts as `gpu` with no cached model. An
    unreachable or refusing worker keeps the rule pending.
  - The rule does not look at `lyricsAlignment.available`: data-model 3.2 only names the device and the setting. An
    unavailable stack fails the job with the worker's text.
- **Endpoints**:
  - `PUT /api/tracks/{id}/lyrics` (202): the text becomes a plain user document and the rule is applied at once.
    The response is `alignPending` with the estimate or `aligning`. 422 on empty or oversized text, control
    characters other than LF/CR/tab, a line over 500 characters, over 40 words, or a bad language; 409
    `lyrics-busy` while searching or aligning.
  - `POST .../align` (202): from alignPending, failed with text, or found line-synced; a line-synced document is
    sent as `refine`. It returns 409 `lyrics-no-text`, `lyrics-timed`, `lyrics-busy`, `track-not-ready`, or
    `lyrics-align-rejected` with the worker's detail, and 503 when the worker is unreachable. Nothing is saved on
    an error.
  - `POST .../align/cancel` (200): back to alignPending, or to found when a line-synced document was refined. The
    worker job is removed with `DELETE`, which cancels it first. 409 `lyrics-not-aligning`.
- **Sync of alignments**:
  - Queued and running jobs report progress 0..100, the device (an invalid device is logged and dropped) and the
    estimate. The creation response already carries the estimate.
  - Completed jobs become `found` with origin aligned ("...; timing: automatic alignment") and the worker copy is
    deleted.
  - Failed or cancelled jobs become `failed` with the worker text; the text is kept for another try.
  - A rejected submission fails with the worker detail.
- `setLyrics`, `alignLyrics` and `cancelLyricsAlignment` are un-staged. `PendingLyricsOperations` is now
  `removeLyrics, searchLyrics`.

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T044/T045 | e2b707a | 28a4886 |

## Tests and coverage

```text
StemStudio.Api.Tests   842 passed   Api line 99.49 %  branch 96.1 %   Contracts 100 / 100
```

Domain and Persistence are unchanged since HO:us1.

## Quality checks

- Build: 0 warnings. `dotnet format --verify-no-changes`: clean.
- `sig_check.py src/StemStudio.{Domain,Persistence,Api}`: 0 errors, 0 warnings.

## Dependencies added

None.

## Open issues / requests

- **architect**: the new 409 type `lyrics-align-rejected` (worker 409 at creation, e.g. "vocal stem no longer
  available") belongs with the types requested in HO:us1.
- **frontend**: after a failed alignment the reason is the worker's fixed text, e.g. `ALIGNMENT_FAILED_TEXT` or
  the silent-stem text. The hint "Split the vocals into detail stems and try again" is the frontend's (T047).

## Next owner

backend (006 US3: T049/T050); frontend T046/T047 can use paste/align/cancel.
