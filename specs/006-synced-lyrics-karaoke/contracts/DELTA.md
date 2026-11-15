# Contract deltas: feature 006 (Synced Lyrics with Karaoke Ball)

Status: **delta only**, on top of the 005 contracts. Final version numbers are assigned when 006
starts: every contract gets the next **minor** version over the version current at that time
(all changes are additive). Phase 0 writes the full documents into
`specs/006-synced-lyrics-karaoke/contracts/` (worker-api, backend-api, audio-engine.md,
ui-test-ids.md, redocly.yaml), Redocly-lint clean (OpenAPI 3.1), and `types.ts`.

## 1. Worker API (005 version -> next minor)

### 1.1 New paths

| Method + path | operationId | Body / response | Errors |
|---|---|---|---|
| `POST /v1/lyrics-jobs` | `createLyricsJob` | `CreateLyricsJobRequest` (data-model 2.2) -> 202 `LyricsJob` | 404 `standardJobId` unknown (align), 409 standard job not completed (align), 422 validation |
| `GET /v1/lyrics-jobs/{lyricsJobId}` | `getLyricsJob` | `LyricsJob` | 404 |
| `GET /v1/lyrics-jobs/{lyricsJobId}/result` | `getLyricsResult` | `LyricsResult {outcome: found\|instrumental\|not-found\|disabled, lyrics: TimedLyrics \| null}`; `Cache-Control: no-store` | 404, 409 not completed |
| `POST /v1/lyrics-jobs/{lyricsJobId}/cancel` | `cancelLyricsJob` | 200 `LyricsJob` (queued/running align jobs; lookups finish too fast to cancel: 409) | 404, 409 |
| `DELETE /v1/lyrics-jobs/{lyricsJobId}` | `deleteLyricsJob` | 204 (job + result removed) | 404 |

### 1.2 Schemas

`LyricsJobMode (lookup|align)`, `LyricsJobState (queued|running|completed|failed|cancelled)`,
`CreateLyricsJobRequest`, `LyricsJob` (incl. `device: JobDevice|null`, `estimatedSeconds`),
`LyricsResult`, `LyricsOutcome`, `TimedLyrics`, `LyricsLine`, `LyricsWord`, `WordOrigin
(source|interpolated|aligned)`, `SyncLevel (word|line|plain)` per data-model 2.1-2.2.
`Health` gains `lyricsAlignment {available: bool, model: string, modelCached: bool,
auto: gpu|always|never, reason: string|null}` (`auto` = `STEMSTUDIO_ALIGN_AUTO`, read by the
backend's auto-align rule).

### 1.3 Python contract code (Phase 0)

`schemas.py` models; `ports.py`: `LyricsQuery`, `LyricsLookup` protocol (`lookup(query, progress)
-> LyricsResult`), `Aligner` protocol (`align(request: AlignRequest, device: str, progress) ->
TimedLyrics`), `AlignRequest` (audio array 16 kHz mono, text, language), `WHISPER_GPU_NEED`
placeholder (value set after Phase 0 P0-2); `config.py`: `align_model`, `align_auto`,
`fake_lyrics`.

## 2. Backend API (005 version -> next minor)

### 2.1 Changed schemas

```yaml
Track:
  lyricsStatus: { enum: [none, searching, found, instrumental, notFound, alignPending, aligning, failed, disabled, removed] }
```

### 2.2 New paths (all `Cache-Control: no-store`)

| Method + path | operationId | Body / response | Errors |
|---|---|---|---|
| `GET /api/tracks/{trackId}/lyrics` | `getLyrics` | `Lyrics` (data-model 3.3) | 404 track |
| `PUT /api/tracks/{trackId}/lyrics` | `setLyrics` | `SetLyricsRequest {text (1..20000), language?}` -> 202 `Lyrics` (status alignPending or aligning) | 404, 409 while aligning, 422 limits |
| `DELETE /api/tracks/{trackId}/lyrics` | `removeLyrics` | 200 `Lyrics` (status removed) | 404 |
| `POST /api/tracks/{trackId}/lyrics/search` | `searchLyrics` | 202 `Lyrics` (status searching) | 404, 409 while searching/aligning (with lookups off the response has status `disabled`) |
| `POST /api/tracks/{trackId}/lyrics/align` | `alignLyrics` | 202 `Lyrics` (status aligning) | 404, 409 no text / already aligning / standard stems not ready, 503 worker unavailable |
| `POST /api/tracks/{trackId}/lyrics/align/cancel` | `cancelLyricsAlignment` | 200 `Lyrics` (back to alignPending) | 404, 409 |
| `PUT /api/tracks/{trackId}/lyrics/offset` | `setLyricsOffset` | `{offsetMs: -5000..5000, multipleOf 10}` -> 200 `Lyrics` | 404, 409 no lyrics, 422 |

Schemas: `Lyrics`, `LyricsLine`, `LyricsWord`, `LyricsStatus`, `SyncLevel`, `WordOrigin`,
`LyricsAlignment {estimateSeconds?, device?, progress?}`, `SetLyricsRequest`,
`LyricsOffsetRequest`. Lyrics are never part of `GET /api/tracks` or `GET /api/tracks/{id}`.

### 2.3 .NET contract code (Phase 0)

`StemStudio.Contracts/Lyrics/{LyricsDto, LyricsLineDto, LyricsWordDto, LyricsStatusDto,
SyncLevelDto, WordOriginDto, LyricsAlignmentDto, SetLyricsRequest, LyricsOffsetRequest}.cs`,
`TrackDto.LyricsStatus`, `ApiRoutes.{Lyrics, LyricsSearch, LyricsAlign, LyricsAlignCancel,
LyricsOffset}`, `ContractLimits.{MaxLyricsChars = 20000, MaxLyricsOffsetMs = 5000}`.

## 3. Audio engine (2.0.0 -> next minor; additive)

### 3.1 Types (`types.ts`)

```ts
export interface KaraokeWord { readonly text: string; readonly startMs: number; readonly endMs: number }
export interface KaraokeLine { readonly startMs: number; readonly endMs: number; readonly words: readonly KaraokeWord[] }
export interface KaraokeLyrics { readonly lines: readonly KaraokeLine[] }
export interface KaraokeOptions {
  readonly offsetMs: number;            // -5000..5000; positive = lyrics later
  readonly reducedMotion?: boolean;     // default: matchMedia('(prefers-reduced-motion: reduce)')
  readonly countdownGapMs?: number;     // default 5000
}
export type KaraokeState = 'hidden' | 'countdown' | 'singing' | 'ended';
export interface AudioEngine {
  // ... 2.0.0 members unchanged ...
  /** Builds the banner DOM inside `elementId` and starts drawing on the shared animation frame. Replaces an attached karaoke. */
  attachKaraoke(elementId: string, lyrics: KaraokeLyrics, options: KaraokeOptions): void;
  /** Changes the offset; takes effect on the next frame. */
  setKaraokeOffset(offsetMs: number): void;
  /** Removes the karaoke DOM and stops drawing it. */
  detachKaraoke(): void;
}
```

### 3.2 Behaviour (contract text for audio-engine.md)

- **Host ownership**: the engine owns all children of the host element (Blazor renders it empty
  and never re-renders its content). Text is set with `textContent` only.
- **DOM produced**: `.karaoke-line[data-slot=previous|current|next]` with
  `.karaoke-word[data-state=past|current|future]`, `.karaoke-ball`, `.karaoke-countdown`; host
  attributes `data-state` (KaraokeState), `data-line-index`, `data-word-index`,
  `data-position-ms` (rounded, for tests). Styling comes from the app's CSS (004 tokens).
- **Clock**: `positionMs = round(1000 * (transportPosition(audioNow) - outputLatency)) - offsetMs`
  where `audioNow` comes from `ctx.getOutputTimestamp()` extrapolated with `performance.now()`
  (fallback `ctx.currentTime`), and `transportPosition` is the ADR-005 formula
  `offset + (audioNow - when)` wrapped into the loop. Paused/stopped: the stored position.
  Never accumulated from frame deltas.
- **Frame loop**: drawn on the engine's shared `requestAnimationFrame` loop (also used by the
  meters) while attached, playing and `document.visibilityState === 'visible'`; one frame after
  every seek, pause, stop, loop change, offset change and resize. No timers.
- **Ball**: lands on word *i* at `startMs(i)`; parabola to word *i+1* (research R-05); only
  `style.transform` is written per frame; positions are measured after build and on resize
  (`ResizeObserver`). Reduced motion: ball hidden, current word highlighted.
- **States**: `countdown` before the first line (from `firstLine.startMs - 3000`) and inside gaps
  `> countdownGapMs`; `ended` after the last line's end; `hidden` when `lines` is empty.
- **Errors**: unknown element id -> `Error("Karaoke host not found: <id>")`; invalid lyrics
  (non-increasing starts, word outside its line) -> `Error("Invalid karaoke lyrics: ...")`;
  calls after `dispose()` throw like other methods.
- **Testing**: Vitest with the existing `AudioContext` mock extended by `getOutputTimestamp` and
  `outputLatency`, a fake rAF and stubbed `getBoundingClientRect`; coverage >= 90 %.

## 4. UI test ids (005 version -> next minor)

| Screen | `data-testid` |
|---|---|
| Mixer (`/tracks/{id}/mixer`) | `karaoke-banner` (the host; engine writes `data-state`, `data-line-index`, `data-word-index`, `data-position-ms`; Blazor sets `data-sync` = `word`/`line`/`aligned`), the ball is engine-created (class `.karaoke-ball` inside the host, no test id), `karaoke-credit` (Blazor, below/next to the host: credit text), `karaoke-toggle` (button, `aria-pressed`), `karaoke-size` (select s/m/l), `karaoke-offset` (slider, `aria-valuenow` ms), `karaoke-offset-reset`, `karaoke-unavailable` (shown when status is not found/aligning/failed with the reason and a link to the track page) |
| Track detail (`/tracks/{id}`) | `lyrics-section`, `lyrics-status` (`data-status` = lyrics status), `lyrics-credit`, `lyrics-search`, `lyrics-remove`, `lyrics-paste-open`, `lyrics-paste-input`, `lyrics-paste-submit`, `lyrics-align` (alignPending), `lyrics-align-estimate`, `lyrics-align-progress` (`aria-valuenow` 0-100), `lyrics-align-cancel`, `lyrics-align-device` (reuses the 003 device badge rules), `lyrics-error`, `lyrics-preview` (static, unsynced lyrics view when status alignPending/failed with text), `lyrics-copyright-notice` |
| Help topics (004 framework) | `help-karaoke`, `help-lyrics-sources`, `help-lyrics-align`, `help-karaoke-offset` |

Word elements are engine-created and are asserted through the host attributes and the
`.karaoke-word[data-state]` class selector inside `karaoke-banner` (documented exception to the
"test ids only" rule, like the meters' `data-peak-db`).

## 5. Configuration (devops)

Worker env: `STEMSTUDIO_ALIGN_MODEL` (`turbo`), `STEMSTUDIO_ALIGN_AUTO` (`gpu`),
`STEMSTUDIO_FAKE_LYRICS` (fake stack only). Model cache: Whisper weights in
`STEMSTUDIO_MODEL_DIR/whisper` (existing model-cache volume; `XDG_CACHE_HOME` not used: the
adapter passes `download_root`). CPU image: built from `uv.lock` (triton excluded by the
override); GPU image: unchanged build arguments. CI `CONTRACTS_DIR` -> 006.
