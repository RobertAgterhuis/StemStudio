# Data Model: Synced Lyrics with Karaoke Ball

Builds on the 001-005 data models. Contract changes: [contracts/DELTA.md](contracts/DELTA.md).
Design: ADR-015. Times are integer milliseconds from the start of the track's audio.

## 1. Configuration (worker environment)

| Variable | Values | Default | Meaning |
|---|---|---|---|
| `STEMSTUDIO_ONLINE_LOOKUPS` (005) | `on`, `off` | `on` | `off`: no LRCLIB request; lookups complete with status `disabled`; pasting + alignment still work |
| `STEMSTUDIO_ALIGN_MODEL` | `turbo`, `small` | `turbo` | Whisper model for alignment |
| `STEMSTUDIO_ALIGN_AUTO` | `gpu`, `always`, `never` | `gpu` | when alignment starts without a click: only on a GPU worker (default), always, or never |
| `STEMSTUDIO_FAKE_LYRICS` | `word`, `line`, `plain`, `instrumental`, `not-found`, `failed` | `word` | Only with `STEMSTUDIO_SEPARATOR=fake`: deterministic fake LRCLIB results with self-written text; fake alignment spreads words evenly after 2 s |

Model weights: `STEMSTUDIO_MODEL_DIR/whisper/large-v3-turbo.pt` (1,617,941,637 bytes, SHA-256 in
research.md F-05), downloaded at first alignment.

## 2. Worker entities

### 2.1 TimedLyrics (lyrics job result, stored by the backend)

| Field | Type | Rule |
|---|---|---|
| `syncLevel` | `word` \| `line` \| `plain` | best timing available in this document |
| `origin` | `source` \| `interpolated` \| `aligned` \| `none` | dominant word origin (`none` for plain) |
| `language` | BCP-47 primary tag \| null | LRCLIB `lyricsfile.metadata.language`, alignment detection, or null |
| `source` | `lrclib` \| `user` | |
| `sourceId` | string \| null | LRCLIB record id |
| `sourceDurationSeconds` | number \| null | duration of the LRCLIB record |
| `instrumental` | bool | LRCLIB `instrumental` |
| `lines` | Line[] (<= 400) | ordered by `startMs`; empty for instrumental |
| `plainText` | string (<= 20,000) | the text as received (normalised newlines); used for re-alignment |

`Line`: `startMs` (int >= 0, null when plain), `endMs` (int > start, null when plain), `text`
(1..500), `words` Word[] (1..40). `Word`: `text` (1..100), `startMs`, `endMs` (null when plain),
`origin` (`source`|`interpolated`|`aligned`), `lowConfidence` (bool, aligned only).

Invariants (validated in the worker and again in the backend): words inside their line,
non-decreasing starts across the document, `endMs > startMs`, `endMs - startMs <= 3000` for
words.

### 2.2 LyricsJob (`POST /v1/lyrics-jobs`, `GET /v1/lyrics-jobs/{id}`)

Request `CreateLyricsJobRequest`:

| Field | Type | Rule |
|---|---|---|
| `mode` | `lookup` \| `align` | |
| `title`, `artist`, `album` | strings (title required for lookup) | 005 cleaned lookup names |
| `durationSeconds` | number (> 0, <= 900) | required for lookup |
| `standardJobId` | 32 hex | required for align (vocal stem source) |
| `text` | string (1..20,000) | required for align |
| `language` | string \| null | align: hint; null = detect |

Job:

| Field | Type | Rule |
|---|---|---|
| `lyricsJobId` | 32 hex | |
| `mode` | as request | |
| `status` | `queued` \| `running` \| `completed` \| `failed` \| `cancelled` | |
| `stage` | string | "Searching lyrics", "Downloading speech model (1.6 GB)", "Aligning lyrics" |
| `progress` | 0..1 | |
| `device` | JobDevice \| null (003) | align only |
| `estimatedSeconds` | number \| null | align: estimate for the worker's device (research R-04) |
| `error` | string \| null | fixed texts 2.4 |
| `createdAt`, `updatedAt` | date-time | |

Lanes: `lookup` on the 005 network lane; `align` on the separation scheduler (priority standard >
align > detail parent step). Restart: queued/running jobs re-run from the start.
`DELETE /v1/lyrics-jobs/{id}` removes the job and its result (copyright minimisation).

### 2.3 Lookup selection (worker)

1. `/api/get` with album (if any) -> 2. `/api/get` without album -> 3. `/api/search`
   (`track_name`, `artist_name`) and pick: `abs(duration - ours) <= 2`, then `hasWordSync`, then
   `syncedLyrics`, then `plainLyrics`; tie -> equal album, then lowest id.
4. No record within 2 s but a record with `plainLyrics` within 15 s -> result `syncLevel=plain`
   (text only, for alignment) with `sourceDurationSeconds` set.
5. `instrumental: true` (chosen record) -> `instrumental = true`, no lines.
6. Nothing -> job completes with result `null` and outcome `not-found` (field `outcome` on the
   result envelope: `found` | `instrumental` | `not-found` | `disabled`).

### 2.4 Fixed worker texts

| Situation | Text |
|---|---|
| LRCLIB unreachable after retries | "Lyrics could not be retrieved; try again later." |
| Unusable lyrics data | "The lyrics found online could not be read." |
| Vocal stem missing in the worker cache | "The vocal stem of this track is no longer available in the worker; re-import the track." |
| Vocal stem silent | "The vocal stem is silent, so the lyrics cannot be aligned." |
| Model download failed | "The speech model could not be downloaded; try again when online." |
| Alignment error | "The lyrics could not be aligned to the vocals." |

## 3. Backend entities

### 3.1 TrackLyrics (table `TrackLyrics`, 1:1 Track, cascade delete)

| Field | Type | Rule |
|---|---|---|
| `TrackId` | uuid PK/FK | |
| `Status` | `none` \| `searching` \| `found` \| `instrumental` \| `notFound` \| `alignPending` \| `aligning` \| `failed` \| `disabled` \| `removed` | 3.2 |
| `Reason` | string \| null (<= 1000) | |
| `LyricsJobId` | 32 hex \| null | current worker job |
| `Document` | TimedLyrics (JSON) \| null | |
| `OffsetMs` | int (-5000..5000, multiple of 10) | default 0 |
| `AlignEstimateSeconds` | int \| null | from the worker job, shown on CPU |
| `AlignDevice` | JobDevice \| null | |
| `UpdatedAt` | date-time | |

### 3.2 Status transitions

```text
none ──(standard job reports durationSeconds; or first mixer open of an old track)──> searching
searching ──(found, syncLevel word|line)──> found
searching ──(found, syncLevel plain)──> alignPending ──(auto: worker GPU && standard completed)──> aligning
searching ──(instrumental)──> instrumental
searching ──(not-found)──> notFound
searching ──(disabled)──> disabled
alignPending | notFound | found | failed ──(user pastes text)──> alignPending (document = plain text, source user)
alignPending ──(user clicks Align, or auto rule)──> aligning
aligning ──(completed)──> found (origin aligned)
searching | aligning ──(worker failed / 404 after restart)──> failed
any ──(user removes)──> removed  (document cleared; no automatic lookup any more)
removed | notFound | failed | disabled ──(user searches again)──> searching
```

`found` with `syncLevel line` can be refined by alignment (P3, `POST .../lyrics/align`), which
keeps the line times as constraints (`align_words`).

### 3.3 DTOs (wire)

- `TrackDto.lyricsStatus`: status enum only (no text; used by the mixer to decide whether to
  fetch). Always present (`none` for old tracks).
- `LyricsDto` (`GET /api/tracks/{id}/lyrics`): `{status, reason, syncLevel, origin, language,
  source, credit, offsetMs, instrumental, lines[], alignment {estimateSeconds, device, progress}}`.
  `credit` is built by the backend: "Lyrics: LRCLIB" or "Lyrics: entered by you", plus "; word
  timing estimated" (interpolated) or "; timing: automatic alignment" (aligned).

## 4. Browser preferences (per user, `localStorage`, not on the server)

`stemstudio.karaoke.visible` (bool, default true), `stemstudio.karaoke.size` (`s`|`m`|`l`,
default `m`). Wrapped in try/catch; defaults apply when storage is unavailable.

## 5. Engine types (TypeScript, see DELTA.md 3)

`KaraokeLyrics` = `{ lines: { startMs, endMs, words: { text, startMs, endMs }[] }[] }` (the
render subset of TimedLyrics), `KaraokeOptions` = `{ offsetMs, reducedMotion?, countdownGapMs?:
5000 }`, `KaraokeState` = `'hidden' | 'countdown' | 'singing' | 'ended'`.
