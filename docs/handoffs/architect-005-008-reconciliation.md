# Hand-off: architect / 005-008 contract and doc reconciliation

Branch: worktree from the tip of `006-008-lyrics-sheet-music` (9bcb074; all 005-008 backend and
worker work merged). `src/StemStudio.Web` and `e2e` were not touched (the 005/007 frontend is on
another branch). The implementations are the source of truth. Every deviation and request below
was checked against the code before it was folded in.

## What was built

### Accepted deviations and requests

| # | Source hand-off | Item | Where it is folded in |
|---|---|---|---|
| 1 | backend-006-us1/us2/polish | 409 types `lyrics-timed`, `lyrics-not-aligning`, `lyrics-align-rejected`, `track-not-ready` (lyrics). `LyricsConflict` now lists all seven types, with examples that carry the texts from `LyricsProblems.cs`. The `lyrics-busy` example text now reads "searched or aligned", as implemented. `alignLyrics` and `cancelLyricsAlignment` name their types. | backend 1.6.0 (006) and 1.7.0 (008), mirrored unchanged |
| 2 | backend-006-us3/polish | `searchLyrics` with lookups off answers `searching`, and the next `GET` says `disabled`. The contract sentence is corrected. No worker health flag is added. | backend 1.6.0 and 1.7.0 |
| 3 | backend-006-us1/polish | Contracts lyrics records hide the lyric text in `ToString()`. Redacted `PrintMembers` on `LyricsDto` (counts lines), `LyricsLineDto` (counts words), `LyricsWordDto` (times only) and `SetLyricsRequest` (text length), the same way as the domain records. TDD: red commit 57823da, then green 7003393. | `src/StemStudio.Contracts/Lyrics/*.cs`, `tests/StemStudio.Api.Tests/Contract/LyricsRedactionTests.cs` |
| 4 | backend-008-us1/polish | `createScore` declares `503 WorkerUnavailable`. `ScoreConflict` lists `source-unavailable` (worker 400/404 at creation), with an example. | backend 1.7.0 (008) |
| 5 | audio-ingest-007 #1 | Quantisation `clamp(round(y·2^(b-1)), -2^(b-1), 2^(b-1)-1)`. This is libsndfile's read scale, so raw renders stay bit-identical (SC-004). | 007 data-model 3 |
| 6 | audio-ingest-007 #2 | Pan table rows are now `p < 0` / `p >= 0` (same values; the `p >= 0` form is exact at 0). The vectors are unchanged. | 007 data-model 3 |
| 7 | audio-ingest-007 #3 | TPDF dither applies to every integer output (16/24-bit, and the 16-bit PCM fed to LAME). The seed is the first 8 bytes of `sha256(out_dir.name)` (= render job id). | 007 data-model 3, ADR-016 D4 |
| 8 | audio-ingest-007 #4 | MP3 decoded length tolerance is `n .. n + 2 frames`, because lameenc writes no gapless header. | 007 tasks.md T017 |
| 9 | audio-ingest-007 #5/#6 | Four extra readable job `error` texts. `peakDbfs` is floored at -150 when every sample quantises to zero. | 007 data-model 2.4 |
| 10 | stem-separation-005-us3/polish | **Accepted**: `POST /v1/artwork` returns 503 "Pictures unavailable" when the worker has no image normaliser. This only happens in a partial build, nothing is stored, and the backend already maps worker 5xx to its own 503. | worker 1.3.0 (005), mirrored unchanged into 1.4.0 (007), 1.5.0 (006) and 1.6.0 (008) |
| 11 | audio-ingest-005-us2/polish | ADR-014 D4: the ISRC step is the exact `/ws/2/isrc/<code>` lookup. data-model 2.7 gains the warnings "Wikidata could not be reached." and "The artist details could not be loaded." | ADR-014, 005 data-model 2.7 |
| 12 | beat-analysis-008 (1) | A pickup longer than a bar keeps the meter phase: measure 0 is the partial bar and measure 1 is the first whole bar (`bar_layout`). The worker `BeatGridTempo.downbeats` description is adjusted to match. | 008 data-model 2.4, worker 1.6.0 |
| 13 | beat-analysis-008 (2) | Polyphonic staves use one chord per staff and onset. A chord is cut at the next onset on that staff. | 008 data-model 2.3 |
| 14 | beat-analysis-008-us4 (3) | The drum bleed gate uses the linear onset peak (absolute peak from -25 ms to +30 ms), not the dB onset strength. `strength` = peak / p99. | 008 data-model 2.3 |
| 15 | (CTO brief) | data-model 2.8: beats and downbeats may be negative (-900..900 s), as the worker contract and `GridTime` already allow. The beats item count is now 2..10,000. | 008 data-model 2.8 |
| 16 | audio-engine-006-foundational/us1/polish | `audio-engine.md` 2.1.0, editorial only:<br>- removed the Phase 0 skeleton paragraph and `KARAOKE_NOT_IMPLEMENTED`;<br>- latency is subtracted before the loop wrap (`transportPosition(audioNow - outputLatency)`); paused = stored position minus offset;<br>- slot rules before the first line;<br>- countdown for the whole lead-in, `data-count` 0 then 3/2/1, three `.karaoke-dot`;<br>- `translate3d(<x>px, <y>px, 0px)` and the CSS rule (`left/top: 0`, margins, never `transform`);<br>- hop duration for short hops;<br>- the full validation list, including `Invalid karaoke options` and word starts across lines;<br>- `MutationObserver` on `class`/`data-size`; attach after `loadTrack`;<br>- `data-position-ms` includes the offset.<br>Checked against `karaoke/timeline.ts` and `dom.ts`. | 006 `contracts/audio-engine.md`, 006 DELTA line on the skeleton |
| 17 | devops-008-foundational + CTO font fix (e6a3dff) | Leipzig font facts. Measured in a throw-away `python:3.12-slim` container (verovio 6.4.0, fonttools 4.68.0, brotli 1.2.1; no `stemstudio` compose project touched). Two runs gave the same result: **119,356 B, SHA-256 `773c72dc26eb633b28d054f9880d19921ab2a1bbd28940a8df0441e377584a4b`**. The docs keep the note that the image build prints the current value. The Phase 0 value (119,416 B, `9606f129...`) is kept as history in research.md. | 008 data-model 1, research.md S-08 row, ADR-017 |

Each feature's `contracts/DELTA.md` (005-008) now ends with a "Reconciliation" section. No
contract version was bumped: every change documents behaviour that is already implemented.

### Rejected / not changed

- No deviation was rejected.
- Not changed: the 008 options example `seconds: 515` (piano accurate on the CPU). The data-model
  4.5 formula gives 262 s · 1.7 + 5 + 60 = 510 s. The CTO refines `StemStudio:ScoreEstimates` on
  the reference machine (T080), and the example should follow those defaults then.
- Not changed: `worker/scripts/install_music_font.py`. Its docstring already says that the build
  prints the current SHA-256.

## Files changed

- `specs/006-synced-lyrics-karaoke/contracts/{backend-api.openapi.yaml, worker-api.openapi.yaml, audio-engine.md, DELTA.md}`
- `specs/008-sheet-music/contracts/{backend-api.openapi.yaml, worker-api.openapi.yaml, DELTA.md}`, `specs/008-sheet-music/{data-model.md, research.md}`
- `specs/005-artwork-band-info/contracts/{worker-api.openapi.yaml, DELTA.md}`, `specs/005-artwork-band-info/data-model.md`
- `specs/007-export-library/contracts/{worker-api.openapi.yaml, DELTA.md}`, `specs/007-export-library/{data-model.md, tasks.md}`
- `docs/decisions/ADR-014-artwork-and-band-info.md`, `ADR-016-asset-library-and-export-rendering.md`, `ADR-017-sheet-music-transcription-and-engraving.md`
- `src/StemStudio.Contracts/Lyrics/{LyricsDto, LyricsLineDto, LyricsWordDto, SetLyricsRequest}.cs`
- `tests/StemStudio.Api.Tests/Contract/LyricsRedactionTests.cs` (new, 4 tests)

## Tests and coverage

- Red: `dotnet test tests/StemStudio.Api.Tests --filter LyricsRedactionTests` -> Failed 4 / Passed 0 (commit 57823da).
- `dotnet build StemStudio.slnx -warnaserror` -> 0 warnings, 0 errors.
- `dotnet test StemStudio.slnx -p:CollectCoverage=true` (gate 90 % line + branch), exit 0:
  - Domain: 555 passed, line 98.94 %, branch 95.82 %
  - Persistence: 77 passed, line 98.4 %, branch 92.85 %
  - Web: 798 passed, line 99.27 %, branch 92.56 %
  - Api: 927 passed, line 99.56 %, branch 95.09 %
  - `StemStudio.Contracts` module: line 100 %, branch 100 % (all four records 100 %)
- The "newer contains older" tests are green (backend `LyricsContractTests`, `ArtworkContractTests`; worker contract tests).
- Worker: `uv run --frozen pytest -q` -> 2357 passed, 3 skipped (cairo/font engraving tests on Windows), 10 deselected, exit 0.

## Quality checks

- `npx @redocly/cli@2.78.0 lint` with each feature's `redocly.yaml` reports "valid" for:
  - worker 005, 006, 007 and 008;
  - backend 006 and 008.

  The remaining warnings are the existing `operation-4xx-response` / `no-unused-components` ones.
  None comes from these changes.
- `uv run --project tools/quality python tools/quality/run_all.py` -> 1192 files, 0 errors, 336
  warnings, exit 0. No warning names a changed file.
- `dotnet format --verify-no-changes` on the changed C# files -> exit 0.

## Dependencies added

None. The throw-away font measurement used `python:3.12-slim` with verovio 6.4.0, fonttools 4.68.0
and brotli 1.2.1, which are the devops build-stage pins. Nothing was added to the repo.

## Open issues / requests

- **CTO (T080)**: once the `StemStudio:ScoreEstimates` defaults are refined, align the 008 options
  example (515 s vs the 510 s the formula gives).
- **frontend**: the lyrics 409 types and the karaoke DOM and CSS rules are now in the contracts.
  See backend `LyricsConflict` and `audio-engine.md` "Slots", "Ball" and "Countdown".

## Next owner

CTO: merge into `006-008-lyrics-sheet-music`. The frontend can build against the updated 006/008
documents.
