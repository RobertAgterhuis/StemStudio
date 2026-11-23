# Data Model: Insights and Handouts (feature 020)

Builds on the 001-019 data models. Wire shapes: [contracts/](contracts/) (standalone additive
deltas; on any difference the OpenAPI documents win). Design:
[ADR-029](../../docs/decisions/ADR-029-stem-quality-report-and-handouts.md). Sections marked
**(G2)** exist only if gate G2 passes (research.md R-06); **Phase 0 (2026-11-23): G2 is pending
(no dataset), so every (G2) part is deferred and not in the 1.11.0/1.19.0 contracts** (DELTA.md
"Phase 0 outcome").

## 1. Configuration

| Where | Name | Default | Meaning |
|---|---|---|---|
| worker | `STEMSTUDIO_QUALITY_ANALYZER` | unset = `fake` when `STEMSTUDIO_SEPARATOR=fake`, else `real` | `real` (signal measures) or `fake` (deterministic per channel, 2.6) |
| worker | `STEMSTUDIO_INSIGHTS_THREADS` | `4` (1..16) | intra-op thread cap of the `insights` lane (numpy/scipy BLAS threads) |
| worker | `STEMSTUDIO_RESEPARATOR` **(G2, deferred)** | as the analyzer | `real` (`vocals-hq` pipeline) or `fake` (copies the inputs, applies -1 dB, 2 s) |
| API, web | `StemStudio:QualityReport:Enabled` | `false` | the stem quality report as a whole (owner decision 2026-11-23, G1 failed; ADR-029 D8). Off: no automatic report, `requestQualityReport` 503, `getQualityReport` status `none`, `Track.quality` absent, no quality UI |
| API | `StemStudio:AutoQualityReport` | `true` | while the report is on: start a report after every standard, detail and high-quality separation |
| API | `StemStudio:MaxSetlistHandoutSongs` | `50` | FR-009 limit |

No secrets. The handout renderer never uses the network (FR-018).

## 2. Worker entities (`schemas.py`, Phase 0)

### 2.1 Lane

New lane **`insights`** (one job at a time, FIFO, threads capped): quality jobs and handout
jobs. Separations, renders (007) and transcriptions (008) keep their own lanes, so exports keep
their 15-second target and handouts never wait behind a separation. `Health.lanes.insights =
{queued, running}`. Re-separation jobs **(G2)** run in the separation queue (they are
separations and use the 003 device policy).

### 2.2 Library paths (per-job allow-lists, 007 2.1)

Quality jobs accept only the stem and detail stem patterns of 007 (`ports.QUALITY_PATH_PATTERNS`).
Handout jobs accept only (`ports.HANDOUT_PATH_PATTERNS`):

```text
assets/<uuid>/[a-z][a-z0-9-]{0,31}\.pdf                 # score/tab/drum/transposed PDFs (008/016)
tracks/<uuid>/artwork/(auto|user)/cover\.jpg            # 005 artwork
```

The general allow-list of render and transcription jobs (`ports.LIBRARY_PATH_PATTERNS`) is
unchanged, so a PDF or JPEG can never reach a render job (Phase 0 refinement).

Same resolution rule: `Path(root, path).resolve(strict=True)` inside `root.resolve()`, else
`400 invalid-input-path`; missing -> `404 input-not-found` (checked at job creation).

### 2.3 QualityJobRequest (`POST /v1/quality-jobs`)

| Field | Type | Rule |
|---|---|---|
| `stems` | `QualityInput[]` | 6..64; each of the six standard stems exactly once (they form the mix); every channel once; `kind` matches the channel (`standard` = no dot, `rest` = `<parent>.rest`, else `detail`) |
| `detailParents` | `DetailParentInput[]` | 0..5; per split parent the 002 metrics |
| `titles` | `{title, artist?}` | for logs only, <= 200 chars each |

**QualityInput**: `channel` (ChannelId: `vocals` or `drums.kick`), `path` (library path of the
FLAC), `kind` (`standard` \| `detail` \| `rest`).
**DetailParentInput**: `parent` (ParentStemName), `modelResidualDb` (number \| null),
`reconstructionErrorDb` (number \| null).

### 2.4 QualityJob and result

`qualityJobId` (32 hex), `status` (`RenderJobStatus`), `stage` (`loading` \| `analysing` \|
`rating` \| null), `progress` 0..1, `error` (<= 500 \| null), `result` (`QualityResult` \| null),
`createdAt`, `updatedAt`. Kept 24 h after the end; `queued`/`running` -> `failed` "interrupted by
restart" at start-up; `DELETE` cancels and removes (idempotent `204`).

**QualityResult**: `analyzerVersion` (string, e.g. `"1.0.0"`), `durationSeconds`,
`stems: StemQuality[]` (same order as the request).

**StemQuality**

| Field | Type | Rule |
|---|---|---|
| `channel` | ChannelId | from the request |
| `rating` | `good` \| `fair` \| `poor` \| `not-present` \| `unknown` | 4.5 |
| `ratingReason` | `QualityReasonCode` | one of 4.6 |
| `activeSeconds` | number >= 0 | frames above the activity floor x hop |
| `activeShare` | number 0..1 | active frames / all frames (002 gate statistic) |
| `levelDb` | number | energy relative to the mix (sum of the six stems), floored at -120 |
| `bleed` | `BleedEstimate` | |
| `artefacts` | `ArtefactEstimate` | |

**BleedEstimate**: `level` (`low` \| `medium` \| `high` \| `unknown`), `index` 0..1 (leakage
share after the guard), `mainSource` (StemName \| null: always a standard stem), `coOccurrence` (bool:
the guard capped the level), `reliability` (`validated` \| `indicative`).
**ArtefactEstimate**: `level` (as bleed), `index` 0..1, `kinds` (subset of `flicker`,
`dropouts`, `model-residual`, `rest-share`), `reliability`.

### 2.5 Handout jobs (`POST /v1/handout-jobs`)

The API assembles a complete, resolved **HandoutDocument**; the worker only lays it out and
renders it. No database, no lookups, no network in the worker.

**HandoutJobRequest**: `document` (HandoutDocument), `paper` (`a4` \| `letter`).

**HandoutDocument**

| Field | Type | Rule |
|---|---|---|
| `kind` | `song` \| `setlist` | |
| `family` | HandoutFamily (6.1) | |
| `familyLabel` | string <= 60 | e.g. "Melodic instruments in B-flat" |
| `title` | string 1..300 | song title or setlist name |
| `subtitle` | string <= 300 \| null | artist, or "Setlist - 12 songs" |
| `generatedOn` | date (`YYYY-MM-DD`) | from the API clock (deterministic tests) |
| `notice` | `{footer, full}` | fixed texts 6.6; the worker rejects a document whose texts differ (422) |
| `setlist` | `SetlistBlock` \| null | set iff `kind = setlist` |
| `songs` | `HandoutSong[]` | 1 for `song`; 1..50 for `setlist` |
| `attributions` | string[] (<= 20, each <= 300) | last page |
| `appVersion` | string <= 40 | last page |

**SetlistBlock**: `name`, `notes` (<= 4,000 \| null), `entries[]` (`position`, `title`,
`artist`, `key`, `tempoBpm`, `durationSeconds`, `notes`). 014 removes the entries of deleted
tracks, so every entry has a song (Phase 0; the planned `available` flag is dropped).

**HandoutSong**

| Field | Type | Rule |
|---|---|---|
| `title`, `artist` | string | |
| `artworkPath` | library path \| null | 2.2 |
| `facts` | `SongFacts` | `tempoBpm` (int \| null), `timeSignature` (`"4/4"` \| null), `key` (text \| null, e.g. "A minor"), `keySource` (`chords` \| `score` \| null), `durationSeconds`, `transpositionNote` (text \| null) |
| `sections` | `HandoutSection[]` (<= 64) | `name` (e.g. "Chorus 2"), `startSeconds`, `endSeconds`, `startBar` (int \| null), `bars` (int \| null), `chordBars` (string[][] \| null: per bar the chord symbols), `practiceTempoPercent` (int \| null, from 014's log) |
| `lyrics` | `HandoutLyrics` \| null | `lines[]` (`text` <= 500, empty = instrumental chord row; `section` (index \| null), `chords[]` (`symbol`, `charIndex`)), `chordPlacement` (`word` \| `line` \| `none`; the 016 chord sheet rule), `source` (text) |
| `chordSymbols` | string[] (<= 64) | distinct symbols in order of appearance (legend line) |
| `notation` | `HandoutNotation[]` (<= 8) | `title` (e.g. "Bass - tablature"), `kind` (`score` \| `tab` \| `drums` \| `transposed`), `pdfPath` (library path) |
| `practiceNotes` | string <= 4,000 \| null | plain text, line breaks kept |
| `entryNotes` | string <= 4,000 \| null | setlist entry notes |
| `include` | IngredientCode[] | what the renderer prints (already filtered by the API) |

**HandoutJob**: `handoutJobId`, `status`, `stage` (`layout` \| `notation` \| `writing` \| null),
`progress` 0..1, `error`, `result` (`HandoutResult` \| null), times. Lifecycle as 2.4.
**HandoutResult**: `fileName` (`handout.pdf`), `contentType` (`application/pdf`), `sizeBytes`,
`sha256`, `pages`, `outline` (string[]: bookmark titles in order), `warnings`
(`HandoutWarning[]`: `code` (`missing-glyphs` \| `notation-scaled` \| `notation-unreadable` \|
`notation-omitted` \| `artwork-omitted`; the last two additive at integration, orchestrator 16:14), `text`). File: `GET /v1/handout-jobs/{id}/file` (409 `handout-not-completed`).

Error texts: "A notation file could not be read: <title>" (the page is replaced by a note, warning
`notation-unreadable`, job still completes); "The handout could not be written" (fail).

**Job limits** (security fixes, orchestrator 16:14; the API mirrors the request limits in
`ContractLimits` and refuses before it submits, `422` on the field `document`):

| Limit | Value | When it is exceeded |
|---|---|---|
| request body | 8 MiB | refused (`422`) |
| text of the document | 500,000 characters | refused (`422`) |
| layout cells (sections, bars, chords, legend chords, lyric lines and their chords, setlist entries) | 30,000 | refused (`422`) |
| notation PDFs of one request, together | 128 MiB | refused (`422`) |
| notation pages per handout (frames) | 600 | further items become a note page, warning `notation-omitted`: "A notation file was left out because the handout reached its size limit: <title>" |
| notation bytes per handout while rendering | 128 MiB | as above |
| parsing a notation PDF (pypdf) | 200,000 tokens per content stream, 16 MiB per decoded stream; at most 50,000 resource objects per page; resources stripped to drawing keys (no actions, files or metadata) | the file counts as unreadable (`notation-unreadable`) |
| artwork | JPEG only, at most 40 megapixels | left out, warning `artwork-omitted`: "The cover picture was left out because it is not a JPEG or is larger than 40 megapixels: <song>" |
| render time | 240 s | the job fails with "The handout could not be written" |

The API caps the handouts that are made at the same time at **3 per song or setlist** (`409
handouts-active`: "Up to 3 handouts of a song or setlist are made at a time; wait until one is
ready."; a Retry is not counted) and a setlist handout at `StemStudio:MaxSetlistHandoutSongs`
songs (`409 too-many-songs` on create and on the options: "A setlist handout can hold up to 50
songs.").

**Write gate** (orchestrator 17:05, correcting 16:05): requesting a quality report, creating a
handout and setting a practice note are **not** gated during a backup or restore. The project
policy (013 data-model 5.2) gates only operations that delete or replace library files; database
edits, job starts and new assets are allowed. The maintenance answer (`503` while a restore
switches the library) still covers them.

### 2.6 Fake analyzer (E2E determinism)

`fake` returns, per channel, a fixed `StemQuality` (no audio read beyond the existence check):
`vocals` fair (bleed medium from `drums`), `drums` good, `bass` good, `guitar` poor (artefacts
high, flicker), `piano` not-present, `other` fair (bleed medium from `guitar`); detail stems
good, `rest` fair (`rest-share`). `analyzerVersion = "fake-1"`. Duration 0.5 s per job.

### 2.7 Re-separation jobs **(G2, deferred)** (`POST /v1/reseparation-jobs`)

Request: `pipeline` (`vocals-hq`), `inputs` (the six standard stem paths by StemName). Job:
`reseparationJobId`, `status`, `stage` (`loading-model` \| `vocals` \| `instruments` \|
`writing` \| null), `progress`, `error`, `device` (JobDevice, 003), `result` (`{pipeline,
models[], stems[{name, path, sizeBytes}]}`). Stems: `GET /v1/reseparation-jobs/{id}/stems/{stem}`.
Cache: `cache/by-audio/<sha of the summed input>/vocals-hq/`. `Health.separationPipelines =
{ "vocals-hq": "ready" | "not-downloaded" }`.

## 3. Worker modules (ownership in plan.md)

```text
insights/   representation.py (mel spectrograms, mix), bleed.py (B2 + guard, B3), artefacts.py
            (A1, A2), detail.py (A3), rating.py (levels, rating, reason), thresholds.py
            (constants + ANALYZER_VERSION, reliability table from G1), analyzer.py (QualityAnalyzer
            port implementation), fake.py
handouts/   document.py (validation of notice texts, paging plan), templates/*.html.j2,
            styles/handout.css, assets/ (tokens.css, Archivo, manifest.json), fetcher.py
            (allow-list url_fetcher), layout.py (WeasyPrint), notation.py (pypdf frame merge),
            renderer.py (HandoutRenderer port implementation)
separation/hq/ (G2) roformer_vocals.py, pipeline.py, fake.py
jobs/quality_*.py, jobs/handout_*.py, api/insights_routes.py, api/handout_routes.py
```

## 4. Quality measures (binding algorithm; constants calibrated in Phase 0)

### 4.1 Representation

Each FLAC is read in blocks, mixed to mono, resampled to 22,050 Hz (soxr), STFT n_fft 2048,
hop 512, Hann; 64 mel bands 30 Hz-11 kHz; power `P(t,b)`; `L = 10 log10(P + 1e-12)`. The mix
`M` is the sum of the six standard stems in the time domain before the STFT. Activity: a frame is
active when the broadband level is above `max(-50 dBFS, stem peak - 40 dB)` (002 gate family).

### 4.2 Bleed (B2 with the ratio-floor guard)

For stem i and every other standard stem j (detail stems: every other *standard* stem and the
siblings in the same parent; a sibling counts as the parent stem, so `mainSource` is always a
standard stem), per band b over j's active frames:

- `g_b = max(0, <P_i, P_j> / <P_j, P_j>)` (closed-form non-negative scalar least squares);
- `floor_b` = the 5th percentile of `P_i / P_j` over j's **loud cells** (j active and
  `L_j >= peak_b(j) - 20 dB`): the ratio floor. Bleed is a constant-ratio copy, so it shows in
  every loud cell of j; joint playing does not. This replaces the quiet-frame fit `g_quiet` of
  the plan (orchestrator 2026-11-23 14:40, Q1: Spearman 0.95 against 0.81 on V1, same cost);
- `g*_b = min(g_b, floor_b, 1)`: the gain is capped at 1 (`thresholds.MAX_GAIN`, orchestrator
  15:33, Q3), so a source never counts louder in i than it is itself;
- leakage `E_ij = sum_b g*_b sum_t P_j(t,b)`; share `s_ij = E_ij / sum_tb P_i(t,b)`, clipped to
  0..1. `index = max_j s_ij`, `mainSource = argmax`. The index is an energy share, so it grows
  with the square of the bleed amplitude.

Constants (`insights/thresholds.py`, `ANALYZER_VERSION` 1.0.0): `LOUD_RANGE_DB` 20,
`FLOOR_PERCENTILE` 5, `MAX_GAIN` 1, `CO_OCCURRENCE_RATIO` 2; levels T1 **0.0065**, T2 **0.053**
(V1 2026-11-23, seeds 7/11/23: Spearman 0.94/0.97/0.91, levels correct 93/87/87 %).

**Known limit** (orchestrator 15:33, Q3 (a)): instruments that sustain together on the same grid
(the dense V1 control song, every family all the time) can read as bleed: 1 of 6 stems was
falsely High (guitar 0.080 from bass). The control test stays a strict `xfail` until G1 on real
music decides; the labels stay `indicative`.

### 4.3 Co-occurrence guard

Pairs {drums, bass}, {guitar, other}, {piano, other}, {guitar, piano}: when the share of the
full fit (`g`) divided by the share of the guarded fit (`g*`) is above 2 the level is capped at `medium` and
`coOccurrence = true` (reason `bleed-together`). On V1 the playing-together control (kick and
bass on the same onsets) never reached High.

### 4.4 Artefacts

**A2 dropouts only** (orchestrator 15:33): A1 flicker is **not shipped** and does not feed the
level (on V1 it stayed flat after the fixed injection and the 1 h redesign; its scale differs about
tenfold per family, so no family-neutral thresholds exist). The kind `flicker` stays in the
contract enum (the fake analyzer still emits it, 2.6) but the signal analyzer never reports it.

A2 works on the 23 ms block envelope and judges **every family against its own envelope**
(orchestrator 14:40, Q2: each family counts equally, not only drums). A hole is a run of blocks
that:

- lies more than 30 dB below the median of its +-0.5 s neighbourhood (43 blocks);
- lasts at most 13 blocks (0.3 s);
- **falls abruptly**: the maximum of the two blocks before it minus its first block is at least
  25 dB;
- **recovers abruptly**: the maximum of the two blocks after it minus its last block is at least
  25 dB (natural decays fall slowly and are not holes).

Share = hole blocks / active frames; `x = share / max(mix share, 1e-3)`; `index = x / (x + 50)`.
Levels T1 0.35, T2 **0.62** (V1: Spearman 0.96, 100 % correct; clean stems of every family 0).
Kind `dropouts` when the level is at least `medium`. Detail stems add `model-residual` when the
parent's `modelResidualDb > -20` and `rest-share` when the parent's `rest` energy share > 0.25
(rest stems themselves get `rest-share` from the same value).

### 4.5 Levels and rating

`low` < T1 <= `medium` < T2 <= `high` per dimension (T1/T2 calibrated on V1: bleed 0.0065/0.053,
artefacts 0.35/0.62; the plan's initial guesses were 0.10/0.30 and 0.35/0.65). A dimension that was **measured and failed** G1 reports `unknown`
instead of `low`/`medium` (only `high` is shown). A dimension not measured yet (Phase 0: G1 not
run) is `indicative` and reports all levels; guitar, piano, detail and rest stems are always
`indicative`. Rating, in order: `activeSeconds < 10` -> `unknown`;
B3 (`activeShare < 0.08` or `levelDb < -30`) **and** bleed index >= 0.5 -> `not-present`; else
the worse dimension (`high` -> `poor`, `medium` -> `fair`, `low` -> `good`, both `unknown` ->
`unknown`).

### 4.6 Reason codes and texts (UI and API, fixed)

| Code | Text |
|---|---|
| `clean` | "Little bleed and few artefacts." |
| `bleed` | "Some sound of {source} is in this stem." (medium) / "Much of {source} is in this stem." (high) |
| `bleed-together` | "May be bleed of {source}, or the two instruments playing together." |
| `artefacts` | "The stem has separation artefacts (short dropouts in the sound)." |
| `bleed-and-artefacts` | "Bleed of {source} and separation artefacts." |
| `not-present` | "This instrument is probably not in this song; what you hear is spill from other instruments." |
| `too-short` | "Too little audio to rate." |

`{source}` = the stem's display name in lower case ("drums").

## 5. Backend entities

### 5.1 Track (extended)

| Field | Type | Rule |
|---|---|---|
| `QualityReport` | JSON `QualityReport` \| null | null = never made; migration `AddQualityReport` |
| `StemSet` **(G2, deferred)** | JSON `{Pipeline, Models[], SeparatedAt}` | null = `standard` with the 001 model; migration `AddStemSet` |
| `Reseparation` **(G2, deferred)** | JSON phase `{Status, Stage, Progress, Error, WorkerJobId, Pipeline, RequestedAt, UpdatedAt}` \| null | like `DetailPhase` (002 3.1) |

**QualityReport**: `Status` (`queued` \| `running` \| `ready` \| `failed`), `Progress` 0..100,
`Error`, `WorkerJobId`, `AnalyzerVersion`, `MeasuredAt`, `StemsVersion` (hash of the stem files'
sizes + mtimes at request time), `Stems` (list of the worker's `StemQuality`), `Previous` (map
ChannelId -> rating of the last ready report, kept when a new report becomes ready).
Derived on read: `outdated` = analyzer version differs from the worker's current
(`/health.insights.analyzerVersion`) or `StemsVersion` differs from the current files.

Rules: one active report per track (a new request while queued/running -> 409
`quality-running`); a failed run keeps the last ready stems and previous map; deleting a track
removes the report with it; requests need `Track.Status == Completed` (409 `track-not-ready`).

### 5.2 PracticeNote (table `PracticeNotes`)

`TrackId` (FK, cascade delete), `Family` (HandoutFamily), `Text` (1..4,000, trimmed, control
characters except `\n` removed), `UpdatedAt`. Primary key (`TrackId`, `Family`). Empty text
deletes the row. Migration `AddPracticeNotes`.

### 5.3 Tips (backend domain `Insights/TipRules`, pure)

Input: the `StemQuality`, the track state (detail phase status and outcomes, stem set pipeline,
whether the `vocals-hq` pipeline is available on this worker, mix context for "mute"), the
device estimate. Rules are evaluated in this order; the first two that apply are returned.

| # | Code | When | Text | Action |
|---|---|---|---|---|
| 1 | `not-in-song` | rating `not-present` | "Mute this stem; the instrument is probably not in this song." | `mute` (channel) |
| 2 | `use-detail-stem` | parent with outcome `split` and a kept child rated better than the parent | "Use the {child} detail stem instead." | `open-detail` (parent, child) |
| 3 | `rerun-high-quality` **(G2, deferred: never produced in 1.19.0)** | (channel `vocals` with rating `fair`/`poor`) or (`bleed.mainSource = vocals` and bleed `medium`/`high`); pipeline `standard`; pipeline available | "Re-separate with the high-quality vocal model ({estimate} on this computer)." | `reseparate` (`vocals-hq`) |
| 4 | `try-detail-separation` | channel in {vocals, drums, guitar, piano, other}; detail phase none, failed or cancelled; rating `fair`/`poor` | "Split further (detail separation) may pull apart the instruments mixed into this stem." | `split-detail` |
| 5 | `listen-to-rest` | detail stem with artefact kind `rest-share` | "Part of this sound went to the 'rest' stem; listen to it too." | `open-detail` (parent, `rest`) |
| 6 | `hard-to-isolate` | rating `poor` (or `fair` for guitar, piano, other and their detail stems) and none of 2-4 applied | "{Instrument} is hard to isolate with today's models; this is close to the best StemStudio can do." | none |
| 7 | `clean` | rating `good` | "Clean enough to practise with." | none |

`{estimate}` = "about N minutes" from the 003 device and the measured s/min (research
"Phase 0 measurements"). There is deliberately **no** "run on the GPU" tip (FR-006).

### 5.4 Asset (007 4.1, extended)

- `Kind` += `handout`; `AssetFile.role` `pdf` (existing role, file `handout.pdf`).
- `Provenance.origin` += `handout`; `Provenance.handoutRecipe` (`HandoutRecipe` \| null, null
  for all other kinds; `recipe` and `scoreRecipe` are null for handouts); `sourceAssetIds` = the
  notation items used; `trackId`/`trackTitle` = the song (song handouts) or null/setlist name.
- `Asset.handout` (`HandoutFacts` \| null): `kind`, `family`, `transposition`, `paper`, `pages`,
  `songs` (count), `included` (IngredientCode[]), `omitted` (`{code, reason}`[]: reasons
  `not-available`, `not-selected`, `no-timing`), `warnings` (worker warnings), `outline`.

**HandoutRecipe**: `kind`, `trackId` \| `setlistId`, `setlistName` (snapshot), `family`,
`include` (IngredientCode[] \| null = defaults), `notationAssetIds` (uuid[] \| null = defaults),
`transposition` (`c` \| `b-flat` \| `e-flat` \| `f` \| null), `paper`. "Make again" posts the
recipe unchanged (data is re-read, so the new handout reflects current lyrics, chords, notes).

Default name: "<Title> - <Family label> handout" (song), "<Setlist> - <Family label> handout"
(setlist), trimmed to 120 characters.

## 6. Handout content

### 6.1 Families

`overview` (Band overview), `vocals`, `guitar`, `bass`, `keys`, `drums`, `melodic` (Melodic
instruments; transposition `c` default, `b-flat`, `e-flat`, `f`).

Notation per family (newest ready `score` item of the track whose `ScoreFacts` match; shapes
reconciled in DELTA.md "Phase 0 outcome" P0-4): vocals -> profile `vocals`; guitar -> profile
`guitar` with `tablature` (tab) and without (score); bass -> profile `bass` with and without
`tablature`; keys -> profile `piano` or `keys`; drums -> profile `drums` (008/016 drum notation);
melodic -> a part whose `part.instrument` matches the transposition (`b-flat` = `*-bb`, `e-flat` =
`*-eb`, `f` = `*-f`), for `c` a profile `strings`/`brass`/`woodwinds` item or a `concert` part,
else the C score of the stem the user picks (`other` or a melodic detail stem).

### 6.2 Ingredients (IngredientCode) and defaults per family

| Ingredient | overview | vocals | guitar | bass | keys | drums | melodic |
|---|---|---|---|---|---|---|---|
| `facts` (tempo, metre, key, duration, artwork) | D | D | D | D | D | D | D |
| `form-chart` (sections, bars, practice tempo) | D | D | D | D | D | D | D |
| `chords-over-lyrics` | D | D | D | O | D | - | O |
| `lyrics` (plain; or first line per section as cues for drums) | (in above) | (in above) | O | O | O | O (cues) | O |
| `chord-chart` (per section, per bar) | D | O | D | D | D | - | D |
| `notation` | - | D | D | D | D | D | D |
| `practice-notes` | D | D | D | D | D | D | D |
| `practice-progress` (014 log, tempo reached per section) | O | O | O | O | O | O | O |

D = included by default when available; O = optional (Advanced dialog); - = not offered. Every
family has facts, form chart, notation-or-chords and notes, so no family gets less (MoSCoW
principle). `setlist-notes` is always included in setlist handouts.

### 6.3 Assembly (API, `Handouts/HandoutAssembler`, pure parts)

1. Load track facts (beat grid -> tempo rounded, `beatsPerBar/4`), key (016 `Track.key` /
   chord chart key, else the newest score's key, else null), sections (014 `SectionSet`), chords
   (016 chord chart), lyrics (006), notation items, practice notes, practice log (014
   `PracticeSummary.sections[].bestTrainerPercent`), artwork (005).
2. Transpose (melodic, not `c`): spell with 016's `ChordSpeller` and `ChordDisplay(transpose, 0)`
   with transpose +2 (B-flat), +9 (E-flat), +7 (F); key via `KeyFormatter`; `transpositionNote`
   e.g. "Part in B-flat (sounds a major second lower)".
3. Bars: section `startBar` = index of the first downbeat at/after its start (1-based, pickup =
   0); `bars` = downbeats inside; `chordBars` per bar from chords active at the bar's beats.
4. Chords over lyrics: 016's `ChordSheetBuilder` (research.md R-08 Phase 0 note): rows map to
   `LyricLine` (`text`, chords with `charIndex`), instrumental rows have empty text.
5. Filter by the effective `include` list; record omissions with reasons.
6. Notice and attributions (6.6); `generatedOn` from `TimeProvider`.

### 6.4 Layout (worker)

A4 or Letter, margins 18/18/20 mm (as `tools/docs`). Song: page 1 header band with artwork
(40 mm), title, artist, family label and the facts row; then form chart (table: section, bars,
time, chords per bar), chords over lyrics (chord row above the text row in a monospaced-width
layout of the Archivo font, chords bold in the accent token), chord legend, practice notes and
progress; then one frame page per notation page; last page "About this handout" (song handouts:
after the notation; setlist: once at the end). Setlist: cover (name, notes, generated on),
running order table, contents with page numbers (`target-counter`), then each song starting on a
new page with the song layout. Bookmarks: song title > section headings ("Form", "Lyrics and
chords", "Notation - Bass tablature", "Practice notes").

### 6.5 Footer and header

Header (pages after the first of a song): song title - family label. Footer left: "For personal
practice and teaching only"; right: "Page n of N". Generated with CSS `@page` margin boxes on
every page including frame pages.

### 6.6 Fixed texts (the worker validates them)

- `notice.footer`: "For personal practice and teaching only"
- `notice.full`: "This handout was made with StemStudio for your own practice and lessons. The
  song, its lyrics and its music belong to their rights holders. Do not publish, sell or share it
  beyond your lesson. Chords, notation and timings were made automatically and may contain
  errors."
- Attributions (only those that apply): lyrics source line from 006 (e.g. "Lyrics: LRCLIB"),
  "Chords detected automatically (StemStudio)", 008 piano model attribution (CC BY 4.0) when a
  piano score is included, 016 model attributions as 016 defines them, "Artwork: <source>" (005).

## 7. State machines

Quality report: `(none) -> queued -> running -> ready | failed`; `ready|failed -> queued` on a
new request or an automatic trigger. Handout asset: 007 asset states (`queued -> rendering ->
ready | failed`). Re-separation **(G2)**: `(none) -> queued -> running -> completed | failed |
cancelled`; on `completed` the API, in one transaction after the files are in place: writes the
new stems (temp + rename per file), removes `detail/` and sets `DetailPhase = null` when the
request had `discardDetail`, prunes `Expanded`/`DetailChannels` of the track's mixes, sets
`StemSet`, and triggers a quality report (FR-007). On failure or cancel nothing changes.

## 8. Assumed shapes of 014 and 016 (reconciled in Phase 0: DELTA.md "Phase 0 outcome" P0-4 has the merged shapes)

| Id | Assumption | Used for |
|---|---|---|
| A-014-1 | `GET /api/tracks/{id}/sections` -> ordered sections `{id, kind, label, startSeconds, endSeconds}` | form chart |
| A-014-2 | Setlist `{id, name, notes, entries[{id, position, trackId, mixId?, notes}]}` | setlist handouts |
| A-014-3 | Practice log: best tempo percent per (track, section) | `practice-progress` |
| A-016-1 | Chord track per track: `{status, key, chords[{startSeconds, endSeconds, symbol}]}` with symbols in a parseable `root[accidental]quality[/bass]` form | chords, key |
| A-016-2 | Tablature, drum notation and transposed parts are `score` assets with a `pdf` file and facts naming `notation` (`standard`/`tab`/`drums`) and `transposition` | notation selection |

The assembler reads them through one port per source (`ISectionSource`, `ISetlistSource`,
`IPracticeLogSource`, `IChordSource`, `INotationCatalog`) so a shape difference changes one
adapter only.
