# Data Model: Practice Flow (feature 014)

Binding for the wave. Names in `code` are contract names (JSON camelCase on HTTP, snake_case in
Python). Contracts: [contracts/DELTA.md](contracts/DELTA.md). Decisions: ADR-023.

## 1. Configuration

| Variable | Component | Values | Default |
|---|---|---|---|
| `STEMSTUDIO_STRUCTURE_ANALYZER` | worker | `real`, `fake` | unset = `fake` when `STEMSTUDIO_SEPARATOR=fake`, else `real` |
| `StemStudio:Practice:SectionDetectOnComplete` | API | bool | `true` |
| `StemStudio:Practice:SetlistPdfTimeoutSeconds` | API | 5..120 | 30 |

No secrets. No network access is needed by any 014 job.

## 2. Worker

### 2.1 Section job (`POST /v1/section-jobs`, lane `practice`)

Request `SectionJobRequest`:

| Field | Type | Rule |
|---|---|---|
| `stems` | `{vocals, drums, bass, other, guitar?, piano?}` of `LibraryPath` | the track's standard stems (6-stem model; `guitar`/`piano` absent for `htdemucs`); each path matches `tracks/<uuid>/stems/<name>.flac` |
| `details` | list of `{channel: "<parent>.<detail>", path: LibraryPath}`, 0..30 | kept detail stems of split parents (`rest` excluded); used only for the solo instrument |
| `grid` | `BeatGrid` (worker 1.0 schema: bpm, beats, downbeats, beatsPerBar, confidence, reliable) or null | the track's grid, unshifted |
| `durationSeconds` | number 1..900 | track duration |
| `maxSections` | int 1..64 | default 64 |

Result `SectionResult`: `analyzer {name, version}` (real: `stems-novelty` / `1.0.0`; fake:
`fake` / `1.0.0`), `snap` (`downbeat` \| `beat` \| `time`), `sections[]` of `DetectedSection`
`{kind, label, startSeconds, endSeconds, cluster ("A".."Z"), instrument (channel id or null),
confidence 0..1}`, `warnings[]` of `{code, message}` with codes `grid-unreliable`, `no-grid`,
`few-repetitions`, `no-vocals`, `merged-to-limit`. Sections cover `[0, durationSeconds]`
without gaps (the first starts at 0, the last ends at the duration; a lead-in before the first
downbeat belongs to the first section).

Job states reuse `RenderJobStatus` (`queued`, `running`, `completed`, `failed`, `cancelled`) with
`progress` 0..1 and `error` text; jobs are kept 24 h (sweep as 007) and fail with "interrupted by
restart" after a worker restart.

### 2.2 Analyser `stems-novelty` 1.0.0 (beat-analysis)

1. Load each stem mono at 22,050 Hz (soundfile + librosa resample); mix = sum of the standard
   stems.
2. Frames: bars from `grid.downbeats` when `reliable`; else beats grouped by `beatsPerBar` when
   beats exist (`snap = beat`); else fixed 2 s frames (`snap = time`). The lead-in before the first
   downbeat is one frame.
3. Features per frame: chroma CQT (12), MFCC (20, normalised per coefficient), mix RMS, vocal
   activity ratio, per-stem RMS share; `stack_memory(n_steps=2)`; L2-normalised columns.
4. Self-similarity `S = F^T F`; Foote novelty with a Gaussian-tapered checkerboard kernel of half
   size 4 frames (frames = bars); peak picking `pre_max = post_max = 3`, `pre_avg = post_avg = 2`,
   `delta = 0`, `wait = 3` (>= 4 bars apart). Thresholds are tuned in Phase 0 part 2 (T010) on
   the synthetic set and frozen as constants with tests.
5. Vocal switches: per frame `active = mean(vocals RMS > 0.1 x p95(vocals RMS)) > 0.3`; a switch
   that holds >= 2 frames is a boundary.
6. Boundaries = union (dedupe within 1 frame, the novelty one wins), then merge segments shorter
   than 4 frames (first/last: 2) into the more similar neighbour (cosine of mean features); then,
   while more than `maxSections`, merge the shortest (`merged-to-limit`).
7. Clusters: agglomerative clustering (average linkage, cosine) on segment means with distance
   threshold 0.35 -> letters in order of first appearance.
8. Kinds and labels: section 2.3. Confidence = 0.5 x normalised novelty peak (1.0 for a vocal
   switch) + 0.5 x cluster margin (distance to the second-nearest cluster).
9. Times: frame boundaries -> seconds (downbeat/beat times or frame x 2 s, rounded to 0.1 s); the
   first section starts at 0, the last ends at `durationSeconds`.

### 2.3 Labelling thresholds (binding, data in `analysis/structure/labels.py`)

| Rule | Condition |
|---|---|
| vocal segment | vocal ratio >= 0.5 |
| low-vocal segment | vocal ratio < 0.25 (between: decided by the neighbouring kinds, verse by default) |
| intro / outro | first / last segment and low-vocal |
| chorus | vocal, cluster count >= 2, highest mean mix RMS among such clusters (ties: later first occurrence loses) |
| verse | vocal, cluster count >= 2, not chorus; also the default for vocal segments |
| bridge | vocal, cluster count = 1, starts after the first chorus |
| solo | low-vocal, not first/last, one non-drum source >= 40 % of non-drum energy and >= 1.5 x its song median; sources: standard stems except drums and vocals, plus detail stems (instrument = detail channel when a detail stem wins inside its parent) |
| instrumental | other low-vocal middle segments |
| other | `few-repetitions` (no cluster occurs twice) for non-intro/outro segments, labelled "Part A", "Part B" by cluster |
| `no-vocals` warning | p95 of the vocals RMS < 0.01 of the mix's p95; then rule "vocal" is never true |

Labels: kind display name ("Intro", "Verse", "Pre-chorus", "Chorus", "Bridge", "Solo",
"Instrumental", "Outro") plus a number per kind when the kind occurs more than once; solos:
"<instrument> solo" with the 004 display name of the channel ("Guitar", "Electric guitar",
"Woodwinds", ...).

### 2.4 Fake analyser (E2E, fake stack)

Deterministic from `durationSeconds` and the grid: splits the song into 8-bar sections (8 x 2 s
without a grid) with the cycle `intro (4 bars), verse, chorus, verse, chorus, solo (guitar),
chorus, outro`, last section to the end; confidence 0.9; no warnings unless the grid is unreliable
(`grid-unreliable`). It reads no audio (paths are still checked by the path guard).

### 2.5 Score view job (`POST /v1/score-view-jobs`, lane `practice`)

Request `ScoreViewJobRequest`: `musicxml` (`LibraryPath`, pattern `assets/<uuid>/score.musicxml`,
new allow-list entry), `grid` (`TempoGrid` of 008: the grid the score was made with, times relative
to the score's input start), `durationSeconds` (input length 1..900).

Processing (audio-ingest, `engraving/score_view.py`):

1. Verovio with the 008 options (`VEROVIO_OPTIONS`); pass A plain SVG per page; pass B with
   `svgBoundingBoxes: true` for boxes only.
2. Bar boxes: for the *k*-th `g.measure` in document order across pages: union of its
   `g.staff.bounding-box > rect` (x, y, w, h), plus the `page-margin` translate; as fractions
   `x / 21000`, `y / 29700`, rounded to 4 decimals. Page index = page of that measure.
3. Bar times (008 data-model 2.4 time base): measure *k* (1-based, the pickup counts as measure 1
   when present, number 0) starts at its downbeat in the grid; the pickup starts at
   `downbeats[0] - pickupBeats x beatPeriod`; the last bar ends at `durationSeconds`. Manual tempo:
   generated grid as in 008. Result times are relative to the input start (the API shifts).
4. Font: insert `<style>@font-face{font-family:'Leipzig';src:url(data:font/woff2;base64,...)}</style>`
   as the first child of the outer `<svg>` (font from Verovio's `data/Leipzig.css`).
5. Output files `page-001.svg` ... (max 200 pages), `bars.json`.

Result `ScoreViewResult`: `pages [{index, file, widthPx 840, heightPx 1188}]`, `bars [{index,
number, page, x, y, width, height, startSeconds, endSeconds}]` (1..2,000 bars, ascending,
contiguous), `engine {name: "verovio", version}`.

### 2.6 Setlist document job (`POST /v1/setlist-document-jobs`, lane `practice`)

Request `SetlistDocumentRequest`: `title` (1..80), `note` (0..2,000), `generatedAt`
(date-time, printed in the footer), `options {includeLyrics, includeSections, includeNotes,
pageBreakPerSong}`, `songs[]` (1..100) of `{position, title (1..200), artist?, bpm?, beatsPerBar?,
key? (text, e.g. "A minor"), durationSeconds, mixName?, note?, sections? [{label, kind,
startSeconds}], lyrics? {lines: [string], shortened: bool}}`. Lyrics: at most 10,000 characters
per song after the API cut (`shortened = true` prints "Lyrics shortened").

Layout (audio-ingest, `documents/setlist_pdf.py`, ReportLab Platypus, A4 portrait, margins 18 mm,
DejaVu Sans 10/13 pt, headings DejaVu Sans Bold):

1. Overview page(s): title, note, a table "# | Title | Artist | Tempo | Length | Note (first 80
   characters)", total length.
2. Per song (new page when `pageBreakPerSong`, default true): heading "<position>. <title>", facts
   line ("Artist - 120 BPM, 4/4 - 3:42 - Mix: Band night"), note, sections as a two-column list
   ("0:12 Verse 1"), lyrics as paragraphs (empty line = stanza break), or "No lyrics".
3. Footer: "<setlist title> - page n of m - made with StemStudio on <date>".
4. Glyph check: characters not covered by DejaVu Sans are replaced by U+FFFD and the overview gets
   the note "Some characters could not be shown".

Result: one file `setlist.pdf` (`application/pdf`), `pages` count.

### 2.7 Ports (architect, Phase 0 part 2)

```python
class StructureAnalyzer(Protocol):
    name: str; version: str
    def analyze(self, request: SectionJobRequest, progress: RenderProgress) -> SectionResult: ...

class ScoreViewRenderer(Protocol):
    def render(self, musicxml: bytes, grid: TempoGrid, duration_seconds: float) -> ScoreView: ...
    # ScoreView(pages: tuple[str, ...], bars: tuple[BarBox, ...], engine_version: str)

class SetlistDocumentRenderer(Protocol):
    def render(self, request: SetlistDocumentRequest) -> bytes: ...
```

Errors: `StructureError` (readable message, job `failed`), `ScoreViewError` ("The score view could
not be made."), `DocumentError`.

### 2.8 Lane and stores

Lane `practice`: one worker thread, FIFO, kinds `section`, `score-view`, `setlist-document`;
`Health.lanes.practice {queued, running}`. Stores: `section-jobs/<id>.json`,
`score-view-jobs/<id>/{job.json, page-NNN.svg, bars.json}`, `setlist-document-jobs/<id>/{job.json,
setlist.pdf}`; same restart/delete/24 h rules as 007 render jobs.

## 3. Backend

### 3.1 `TrackSections` (one row per track)

| Column | Type | Rule |
|---|---|---|
| `TrackId` | uuid, PK, FK -> Tracks (cascade) | |
| `Status` | `none` \| `queued` \| `detecting` \| `ready` \| `failed` | |
| `Origin` | `detected` \| `edited` | `edited` after the first successful PUT |
| `Revision` | int >= 0 | +1 on every replace (detected or edited) |
| `Analyzer` | text (`stems-novelty/1.0.0`) or null | |
| `Snap` | `downbeat` \| `beat` \| `time` or null | |
| `WarningsJson` | JSON array of `{code, message}` | |
| `WorkerJobId` | text or null | while queued/detecting |
| `Error` | text <= 500 or null | when failed |
| `UpdatedAt` | timestamp (UTC) | |

### 3.2 `Sections`

| Column | Type | Rule |
|---|---|---|
| `Id` | uuid PK | kept across edits; new sections get a server id |
| `TrackId` | uuid FK -> TrackSections (cascade) | index (TrackId, Position) unique |
| `Position` | int 0..63 | order by time |
| `Kind` | enum of 9 kinds | |
| `Label` | text 1..40, trimmed | not unique |
| `StartSeconds`, `EndSeconds` | double | `0 <= start < end <= duration`, `end - start >= 1.0`; sections sorted and non-overlapping (gaps allowed after edits) |
| `Instrument` | channel id or null | only for `solo`; must be a channel of the track (standard or kept detail stem) |
| `Confidence` | double 0..1 or null | null for user sections |
| `Origin` | `detected` \| `user` | a detected section becomes `user` when its kind, label or bounds change |

Validation of `PUT /api/tracks/{id}/sections` (422 `validation`, field errors): 1..64 sections;
the rules above; ids that are given must belong to this track; `revision` must equal the stored
revision (409 `sections-changed`). Snap is not enforced by the API (the UI snaps; the API accepts
any time to keep manual "free" boundaries for rubato songs).

### 3.3 Detection triggers and sync

- After the 001 job sync stores a completed separation and `SectionDetectOnComplete` is true:
  create `TrackSections(status queued)` if absent or `none`, submit the section job. A failed
  submission (worker down) leaves `status none`; the mixer's open triggers it again.
- `POST /api/tracks/{id}/sections/detect {overwrite}`: 409 `track-not-ready` (not completed),
  409 `sections-edited` when `Origin = edited` and `overwrite` is false, 409 `detection-running`
  when queued/detecting; else 202 `SectionSet` (status queued).
- The existing job-sync background service (one more poller kind, `SectionJobApplier`) polls the
  worker every 2 s while jobs exist: on `completed` replace sections (new ids), `Origin detected`,
  `Revision + 1`, `Status ready`; on `failed` `Status failed` with the worker's text; deletes the
  worker job after applying.
- Re-separation (retry) of a track keeps its sections.

### 3.4 Mix loop and sections

`MixSettings.loop` (`LoopRegion`) gains `sectionId: uuid | null` (default null). Rules: the API
accepts any id; on read, an id that no longer exists in `Sections` is returned as null (the loop
itself stays). The UI sets it on a section loop and clears it when A or B is moved by hand.

### 3.5 `ScoreViews` (one row per score asset)

| Column | Type | Rule |
|---|---|---|
| `AssetId` | uuid PK, FK -> Assets (cascade) | asset `kind = score` |
| `Status` | `queued` \| `rendering` \| `ready` \| `failed` | |
| `TrackId` | uuid or null | the source track of the score (provenance) |
| `OffsetSeconds` | double >= 0 | track time of the score's input start: 008 range start + source audio asset's range start |
| `PageCount` | int 0..200 | |
| `BarsJson` | JSON (bar map with **track** times = worker times + offset) | |
| `WorkerJobId`, `Error`, `UpdatedAt` | | |

Files: `assets/<assetId>/view/page-NNN.svg`. Triggers: when the 008 asset sync marks a score
asset `ready` (new), and on `POST /api/assets/{assetId}/score-view` (older assets; 202; idempotent
while queued/rendering; 409 `not-a-score` for other kinds; 409 `score-not-ready`). The 007 asset
deletion removes the row and the files (cascade + folder delete). The score's tempo grid is
re-resolved with the 008 `TempoGridResolver` from the stored `ScoreRecipe`.

Score list for a track (`GET /api/tracks/{trackId}/scores`): ready score assets whose provenance
`trackId` equals the track (direct or through a source audio asset), newest first, with
`profile`, `name`, `createdAt`, `scoreView.status`.

### 3.6 `Setlists` and `SetlistEntries`

| Table / column | Type | Rule |
|---|---|---|
| `Setlists.Id` | uuid PK | |
| `Name` | text 1..80 trimmed | |
| `Note` | text 0..2,000 | |
| `CreatedAt`, `UpdatedAt` | timestamps | |
| `SetlistEntries.Id` | uuid PK | kept across `PUT entries` when given |
| `SetlistId` | FK (cascade) | |
| `Position` | int 0..99 | unique per setlist |
| `TrackId` | FK -> Tracks (**cascade**) | track must exist (404 `track-not-found` in the field errors as 422) |
| `MixId` | FK -> Mixes (**set null**) or null | must belong to `TrackId` (422 `mix-track-mismatch`) |
| `Note` | text 0..1,000 | |
| `Setlists.RemovedEntries` | int >= 0 | incremented by a trigger-free domain event when a track delete removes entries; reset to 0 by `GET /api/setlists/{id}?acknowledgeRemoved=true` |

The same track may appear more than once (encore). `PUT /api/setlists/{id}/entries` replaces the
whole ordered list (0..100 entries).

### 3.7 Practice tables

`PracticeSettings` (single row, Id = 1): `Enabled` bool (default true), `NoticeAcknowledged`
bool (default false), `UpdatedAt`.

`PracticeSessions`:

| Column | Type | Rule |
|---|---|---|
| `Id` | uuid PK (made by the browser) | upsert key |
| `TrackId` | FK -> Tracks (cascade) | index (TrackId, StartedAt) |
| `MixId` | FK -> Mixes (set null) or null | |
| `StartedAt`, `EndedAt` | UTC timestamps | `StartedAt <= EndedAt`, `EndedAt - StartedAt <= 12 h`, `StartedAt` not more than 5 min in the future |
| `PlayedSeconds` | double 0..43,200 | `<= (EndedAt - StartedAt) + 5` |
| `LoopPasses` | int >= 0 | |
| `MaxSpeedPercent` | double 50..120 or null | highest playback speed in the session |

`PracticeSectionTimes` (owned by a session, replaced on every upsert):

| Column | Type | Rule |
|---|---|---|
| `SessionId` | FK (cascade) | |
| `SectionId` | uuid or null (no FK: survives re-detection) | |
| `Label`, `Kind` | snapshot at recording time | `Kind` one of the 9 kinds or `none` (no section) |
| `PlayedSeconds` | double >= 0 | sum over rows <= session `PlayedSeconds` + 1 |
| `LoopPasses` | int >= 0 | |
| `TrainerReachedPercent` | double 50..120 or null | highest speed of a completed loop pass of this section with the trainer on |

Writes are refused with 409 `practice-log-off` while `Enabled` is false. An upsert for an
existing id replaces the session and its section rows (the browser sends cumulative values).

### 3.8 Practice summary (computed on read)

`GET /api/practice/summary?trackId&days=1..365 (30)&utcOffsetMinutes=-840..840 (0)`:

- `today`, `last7Days`, `total` seconds; `sessions` count; `lastPractisedAt`.
- `days[]`: one entry per local day in the window (`date` = `yyyy-MM-dd` in local time =
  UTC + offset), `seconds` (sessions split at local midnight proportionally).
- `sections[]`: grouped by `SectionId` (null grouped by label + kind), with current label when the
  section still exists, `seconds`, `loopPasses`, `bestTrainerPercent`, `lastPractisedAt`.
- `tempo[]`: per day and section the highest `TrainerReachedPercent` (only non-null).

`GET /api/practice/overview?utcOffsetMinutes` lists per track: title, artist, total seconds,
last-7-days seconds, last practised, sorted by last practised.

### 3.9 Migrations

One EF Core migration `AddPracticeFlow` (backend): `TrackSections`, `Sections`, `ScoreViews`,
`Setlists`, `SetlistEntries`, `PracticeSettings` (seeded), `PracticeSessions`,
`PracticeSectionTimes`; the mix loop `sectionId` lives in the existing mix settings JSON (no
column). 013's backup covers the database file, so no 013 change is needed; the restore version
check accepts the new migration (013 compares migration ids).

## 4. Engine (audio-engine 2.4.0, summary)

`ScoreViewMap {pages: [{url, widthPx, heightPx}], bars: [{number, page, x, y, width, height,
startSeconds, endSeconds}]}`; `ScoreFollowOptions {follow: bool, reducedMotion?, scrollPauseMs
(default 4000), leadInSeconds (default 0)}`; `LoopPassInfo {start, end, pass, speedPercent}`.
Full rules: [contracts/audio-engine.delta.md](contracts/audio-engine.delta.md).

## 5. Frontend state

### 5.1 Browser preferences (web-interop 1.1.0, `stemstudio.preferences` schemaVersion 2)

```json
{
  "schemaVersion": 2,
  "...": "every schemaVersion 1 field unchanged",
  "shortcuts": {
    "singleKey": true,
    "pedalMode": false,
    "pedalPreset": "play-section-start",
    "overrides": { "mixer.section.next": [{ "key": "n" }, { "key": "pagedown" }] }
  }
}
```

- `overrides`: command id -> 0..2 chords `{key (KeyboardEvent.key lower-cased, 1..20 chars),
  mod?: bool, shift?: bool, layer?: "keys"|"pedal"}`; an empty list unbinds the command.
- Reading a schemaVersion 1 record yields defaults for `shortcuts`; invalid or conflicting
  overrides are dropped (and reported once). Max record size stays 16 KB.
- Presets (`pedalPreset`): `play-section-start` (PageDown = `mixer.play-pause`, PageUp =
  `mixer.section.restart`), `next-previous-section`, `next-previous-song`, `faster-slower`,
  `custom`.

### 5.2 Command ids (binding names; default keys in spec FR-020)

`mixer.play-pause`, `mixer.stop`, `mixer.seek:-5`, `mixer.seek:5`, `mixer.seek:-1`, `mixer.seek:1`,
`mixer.loop.a`, `mixer.loop.b`, `mixer.loop.toggle`, `mixer.loop.section`, `mixer.section.next`,
`mixer.section.previous`, `mixer.section.restart`, `mixer.speed.down`, `mixer.speed.up`,
`mixer.speed.reset`, `mixer.transpose.down`, `mixer.transpose.up`, `mixer.trainer.toggle`,
`mixer.count-in.toggle`, `mixer.metronome.toggle`, `mixer.channel.select:1..9`,
`mixer.channel.mute`, `mixer.channel.solo`, `mixer.score.toggle`, `mixer.score.follow`,
`mixer.save`, `mixer.save-as`, `setlist.next`, `setlist.previous`, `help.open`, `palette.open`,
`shortcuts.open`, `go.import`, `go.tracks`, `go.mixes`, and the 015 commands `mixer.record.toggle`
(R) and `mixer.compare.cycle` (C). The 004 ids keep their meaning. `mixer.speed.reset` resets
speed **and** transpose (012 decision).

Catalogue entry (`keymap-defaults.json`, the single source for C# and TS): `{command, scope,
owner: "001"|"004"|"012"|"014"|"015", group, defaults: [chord], reserved: bool}`. `reserved: true`
(015 commands until 015 ships) keeps the default chords out of reach of other bindings and of
overrides, and hides the row in the overview and settings. A command whose owner feature has not
registered a handler is never dispatched. 016 adds its commands to the same file.

### 5.3 Section navigation (pure C#, `StemStudio.Web/Practice/SectionNavigator`)

- Current section = the last section with `start <= position` (or none before the first).
- Next = first section with `start > position + 0.05 s`; previous = if `position - current.start
  > 2 s` then current start else the one before; restart = current start (or 0).
- Loop section = `LoopRegion {start, end, enabled: true, sectionId}`; when a loop is active and the
  position is inside it, "next section" moves the loop to the next section (practice the song part
  by part) - documented in the help topic.

### 5.4 Practice recorder (pure C#, `StemStudio.Web/Practice/PracticeRecorder`)

States: `idle` -> (`playing` event) `running` -> (`paused`/`stopped`) `holding` -> (`playing`
within 10 min) `running` | (10 min, page leave, track change) `closed`. Per tick it adds
`now - lastTick` (TimeProvider) to the session and to the section of the last position; loop pass
events increment passes and update `TrainerReachedPercent` when the trainer is on and the pass
covered the whole section (loop `sectionId` set). Flush: every 30 s while running, on `holding`,
on `closed`; a 409 `practice-log-off` stops the recorder; other errors keep the buffer and retry
on the next flush.

## 6. Help topics and tours (004 framework, existing areas)

| Area | Topic id | Level | Anchors (`data-help-id`) |
|---|---|---|---|
| mixer | `mixer-sections` | all | `section-lane`, `section-*` |
| mixer | `mixer-sections-edit` | advanced, expert | `section-editor*` |
| mixer | `mixer-sections-detection` | advanced, expert | `section-status` (how detection works, limits, re-detect) |
| mixer | `mixer-score-follow` | all | `score-panel*` |
| mixer | `mixer-practice-log` | all | `practice-panel*` |
| mixes | `setlists` (screen topic `/setlists`) | all | `setlist-*` |
| mixes | `setlist-play` | all | `setlist-bar*` |
| mixes | `setlist-pdf` | advanced, expert | `setlist-pdf*` |
| start | `shortcuts` (reviewed "014": now points at the live overview) | all | `shortcut-overlay` |
| start | `shortcuts-custom` | advanced, expert | `shortcut-settings*` |
| start | `foot-pedals` | advanced, expert | `pedal-*` |
| start | `practice-privacy` (screen topic `/practice`) | all | `practice-page*` |
| glossary | `section`, `speed-trainer-region` | all | |

Tours: `practice-section` v1 (Beginner, mixer: `section-lane`, `section-loop`, `practice-panel`),
`hands-free` v1 (Advanced, mixer: `shortcut-overlay`, `pedal-setup`, `score-panel`).
