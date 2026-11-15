# Data Model: Sheet Music per Instrument

Builds on the 001-003 data models and on [007's data model](../007-export-library/data-model.md)
(assets, lanes, library mount). Wire shapes: [contracts/DELTA.md](contracts/DELTA.md). Design:
[ADR-017](../../docs/decisions/ADR-017-sheet-music-transcription-and-engraving.md).

## 1. Configuration

| Where | Name | Default | Meaning |
|---|---|---|---|
| worker | `STEMSTUDIO_TRANSCRIBER` | `real` (`fake` when `STEMSTUDIO_SEPARATOR=fake`) | `fake` = deterministic notes per profile (2.6); notation and engraving stay real |
| worker | `STEMSTUDIO_TRANSCRIBE_THREADS` | `8` | torch/onnxruntime intra-op threads in the `transcribe` lane |
| worker image | Leipzig font | installed | `/usr/local/share/fonts/verovio/Leipzig.ttf` (OFL-1.1; TrueType outlines, 119,356 B, SHA-256 `773c72dc...584a4b` for verovio 6.4.0 + fontTools 4.68.0; the image build prints the current value), converted at build from the pinned Verovio wheel by `worker/scripts/install_music_font.py` (research.md S-08, P0); `fc-list` must list "Leipzig" |
| API | `StemStudio:ScoreEstimates` | see 4.5 | seconds per audio second per (profile engine, device), used for the shown estimate |

## 2. Worker

### 2.1 Instrument profiles (constants, `notation/profiles.py`)

| Profile | Staves / clefs | Pitch range (MIDI) | Single voice | Engine (quality) | Default resolution | Max notes per voice per bar |
|---|---|---|---|---|---|---|
| `vocals` | 1, treble | 40-84 | yes | basic-pitch | 1/8 | 32 |
| `bass` | 1, bass 8vb (written an octave higher) | 28-67 | yes | basic-pitch | 1/16 | 32 |
| `guitar` | 1, treble 8vb | 40-88 | no | basic-pitch | 1/16 | 32 |
| `keys` | 2, grand staff (split 60) | 21-108 | no | basic-pitch | 1/16 | 32 |
| `piano` | 2, grand staff (split 60) | 21-108 | no | piano CRNN (`accurate`) / basic-pitch (`fast`) | 1/16 | 32 |
| `drums` | 1, percussion (5 lines), voices: 1 hands up, 2 feet down | n/a | n/a | drum onsets | 1/16 | 32 |

Suggested profile per channel: `vocals*` -> vocals, `bass` -> bass, `guitar*` -> guitar,
`piano` -> piano, `piano.*` and `other*` -> keys, `drums` -> drums, `drums.<piece>` -> not
offered in 008 (single drum pieces: later). Any pitched profile may be chosen for a pitched
source.

basic-pitch parameters: onset threshold 0.6, frame threshold 0.4, minimum note length 127.7 ms,
no pitch bends in the output (research.md S-03). Grand-staff split: notes >= 60 on the treble
staff, < 60 on the bass staff (no hysteresis in 008).

### 2.2 Note event (between transcription and notation)

`NoteEvent { start: float s, end: float s, pitch: int 0..127, velocity: float 0..1 }` for pitched
profiles; `DrumHit { time: float s, piece: kick|snare|toms|hihat|cymbals, strength: float 0..1 }`
for drums. Times are relative to the input's start (range start subtracted).

### 2.3 Cleaning (binding order)

1. Drop pitches outside the profile range.
2. Single-voice profiles: sort by `(start, -velocity)`; within 60 ms of the previous kept onset keep
   only the louder note.
3. Quantise (2.4); for single-voice profiles each note ends at the next note's onset at most.
   Polyphonic staves (guitar, keys, piano) are written as one chord per staff and onset: a chord
   lasts until its longest note ends or until the next onset on that staff, whichever comes
   first (a note held under a later onset is cut; no voice splitting in 008, ADR-017 D5).
4. Drop notes shorter than one grid step after quantisation; merge duplicates (same pitch and
   onset slot).
5. Cap: per voice per bar keep the `max` loudest onsets; the rest is dropped and counted.
6. Warnings (2.7) from the counts.

Drums: per piece, drop hits whose linear onset peak (the absolute sample peak of the stem from
25 ms before to 30 ms after the onset) is below 10 % of the piece's 99th-percentile peak (bleed
gate, tuned in Phase 0; `strength` = peak / that percentile, clamped to 0..1), then quantise; simultaneous hits of the hands voice form
one chord; kick is voice 2.

### 2.4 Time base (binding)

Input: `TempoGrid` = either the beat grid (`beats[]`, `downbeats[]`, `beatsPerBar`, `bpm`) or a
manual grid (`bpm`, `beatsPerBar`, `firstDownbeatSeconds`) from which beats are generated at a
constant period over the input length. With a range or a source asset, the API shifts the grid so
that time 0 is the input's start (beats before 0 are kept for interpolation).

- `period` = median of beat differences (manual: `60 / bpm`).
- `b(t)` = `interp(t, beats, 0..n-1)` for `beats[0] <= t <= beats[n-1]`; else linear extrapolation
  with `period`.
- `k0` = index of the beat nearest to `downbeats[0]` (manual: the generated beat at
  `firstDownbeatSeconds`); `q(t) = b(t) - k0` in quarter notes.
- Snap: `round(q·d)/d` with `d = 4` (1/16) or `2` (1/8).
- Bar `m` (1-based) starts at `q = (m-1)·beatsPerBar`; notes with `q < 0` go to a pickup bar of
  `ceil(-min q / beatsPerBar)·beatsPerBar` beats, shortened to the used length.
  When the notes reach back more than one bar, the meter phase is kept: the pickup (measure 0)
  is only the partial bar, the whole bars before the first downbeat stay whole and measure 1 is
  the first whole bar (`bar_layout`). With at most one bar of lead-in both readings agree.
- Time signature `"<beatsPerBar>/4"`; tempo mark `round(bpm)` quarter = BPM.

### 2.5 Key and spelling

- `KeyEstimate { tonic, mode (major|minor), confidence 0..1 }` from music21
  `analyze('key')` over the cleaned notes (weights = durations); confidence = the correlation
  coefficient clamped to 0..1. Fewer than 8 notes -> C major with confidence 0 and a warning.
- Override: `keyOverride` = one of the 30 MusicXML key names (`C`, `G`, ..., `C#`, `F`, ...,
  `Cb`, each `major`/`minor`).
- Spelling: every pitch whose step is not in the key's scale is spelled with the key's
  accidental direction (sharps for sharp keys and C/a, flats for flat keys).

### 2.6 Fake transcriber

Per profile, deterministic notes on every beat of the grid: pitched profiles play the profile's
scale (C major inside the profile range), one note per beat; piano adds a C-major triad in the
bass staff every bar; drums play kick on beats 1 and 3, snare on 2 and 4, hi-hat on every eighth.
It honours ranges, so E2E tests check bars and files on real output.

### 2.7 Warnings (codes and texts, stored on the score)

| Code | Text (en) | When |
|---|---|---|
| `manual-tempo` | "Tempo and bars come from the values you entered." | manual grid |
| `low-key-confidence` | "The key is uncertain (confidence <n> %)." | confidence < 0.6 |
| `notes-cleaned` | "<n> very short or out-of-range notes were removed." | removed > 10 % |
| `dense-capped` | "<n> notes were left out in very busy bars." | capped > 0 |
| `detail-bleed` | "Detail stems can contain other instruments; expect extra notes." | source is a detail stem |
| `cpu-piano` | "The piano model ran on the CPU." | accurate piano on the CPU |
| `few-notes` | "Very few notes were detected." | < 8 notes |

No notes at all -> the job fails with "No notes were detected in this audio."

### 2.8 TranscriptionJobRequest (`POST /v1/transcription-jobs`)

| Field | Type | Rule |
|---|---|---|
| `profile` | profile name | 2.1 |
| `quality` | `accurate`, `fast` | only meaningful for `piano`; others ignore it |
| `input` | `{path}` | 007 path rules; required for pitched profiles |
| `drumInputs` | `{kick, snare, toms, hihat, cymbals}` paths | required for `drums` (each a `tracks/<id>/detail/drums/<piece>.flac` path) |
| `range` | `{startSeconds, endSeconds}` \| null | as 007 |
| `grid` | `TempoGrid` (2.4) | required; beats 2..10,000 and downbeats 1..10,000, strictly ascending, each -900..900 s (negative = before the input start, kept for interpolation, 2.4) |
| `resolution` | `sixteenth`, `eighth` | default per profile |
| `keyOverride` | key name \| null | 2.5 |
| `titles` | `{title, subtitle, instrumentName}` | each <= 200 chars; title = asset name, subtitle = track title/artist |

### 2.9 TranscriptionJob

As `RenderJob` (007 data-model 2.4) plus `stage` (`loading-model`, `transcribing`, `notating`,
`engraving`), progress ranges 0-0.05 load, 0.05-0.80 transcribe (per 10 s segment / per piece),
0.80-0.90 notation, 0.90-1.0 engraving; `device` (`JobDevice`, 003); `result`:
`{ files: [{role: pdf|musicxml|midi, fileName, contentType, sizeBytes, sha256}], summary:
ScoreSummary }`.

`ScoreSummary`: `profile`, `engine` (`basic-pitch`, `piano-crnn`, `drum-onsets`, `fake`),
`quality`, `key` (`{tonic, mode, confidence, source: estimated|override}` or null for drums),
`bpm` (int), `timeSignature`, `bars`, `notes`, `pages`, `resolution`, `warnings[{code, text}]`,
`attribution` (string or null).

Files: `transcription-jobs/<id>/score.pdf` (`application/pdf`), `score.musicxml`
(`application/vnd.recordare.musicxml+xml`), `score.mid` (`audio/midi`). Persistence, restart and
sweep rules as 007 render jobs; `GET /v1/transcription-jobs/{id}/files/{role}`.

### 2.10 Models (model manager, as 002)

| Model id | Source | SHA-256 | Size | Licence |
|---|---|---|---|---|
| `basic-pitch-icassp2022` | `https://github.com/spotify/basic-pitch/raw/v0.4.0/basic_pitch/saved_models/icassp_2022/nmp.onnx` (Phase 0 verifies the tag URL) | `2c3c1d144bfa61ad236e92e169c13535c880469a12a047d4e73451f2c059a0ec` | 230,444 B | Apache-2.0 |
| `piano-crnn-note-pedal` | `https://zenodo.org/records/4034264/files/CRNN_note_F1%3D0.9677_pedal_F1%3D0.9186.pth` | `c3fa9730725bf4a762f1c14bc80cd5986eacda01b026f5a4a2525cd607876141` | 171,966,578 B | CC BY 4.0 |

`PIANO_GPU_NEED = GpuNeed("Piano transcription", 1280)` (measured NVML peak 952 MiB + 20 %, Phase 0);
basic-pitch and drum onsets: CPU only. Weights constants: `ports.BASIC_PITCH_WEIGHTS`, `ports.PIANO_WEIGHTS`.

## 3. Engraving (binding)

- MusicXML 4.0 from music21 (`score.write('musicxml')`), metadata: title, subtitle (composer
  field: "<track title> - <artist>"), part name = instrument name, software "StemStudio".
- Verovio options: `pageWidth 2100`, `pageHeight 2970` (A4 at 10 units/mm), `scale 40`,
  `adjustPageHeight false`, `footer none`, `breaks auto`; one SVG per page.
- PDF: CairoSVG per page, merged with pypdf; document info title = asset name, producer
  "StemStudio", subject = attribution text when set.
- Glyph check (test): every SVG has no `font-family` other than `Leipzig` and the text fonts, and
  the PDF's fonts include the Leipzig font when a tempo mark exists.

## 4. Backend

### 4.1 Asset extensions (007 `Assets` table)

- `Kind = score`; `Files` roles `pdf`, `musicxml`, `midi`; `Audio = null`; new JSON column
  `Score` (`ScoreFacts` = the worker `ScoreSummary` + `device`).
- `Provenance.origin = transcription`; `sourceAssetIds` = the source audio asset when used;
  `trackId` = the source track (direct or through the audio asset).
- Download file names: `<name>.pdf`, `<name>.musicxml`, `<name>.mid`.

### 4.2 ScoreRecipe (normalised request; stored and returned)

| Field | Type | Rule |
|---|---|---|
| `source` | `AudioSource` (007) | track must be completed; a channel must exist (kept detail stems of split parents only); an asset must be `ready` and `kind = audio` |
| `profile` | profile name \| null | null = suggested (2.1); `drums` only for `{trackId, channel: drums}` with drums `split` (else 409 `drums-not-split`) |
| `quality` | `accurate` \| `fast` \| null | null = `accurate` on a GPU worker (`GET /api/device`), else `fast` (piano only) |
| `resolution` | `sixteenth` \| `eighth` \| null | null = profile default |
| `keyOverride` | key name \| null | 2.5 |
| `tempo` | `{bpm 30..300, beatsPerBar 1..12, firstDownbeatSeconds >= 0}` \| null | required when the grid is unreliable/absent (422 `tempo-required`); when given, it overrides the grid |
| `range` | `{startSeconds, endSeconds}` \| null | as 007, >= 2 s for scores |
| `name` | string \| null | default `"<track title> - <channel> (sheet music)"` |

### 4.3 Grid resolution (API)

| Source | Grid sent to the worker |
|---|---|
| `{trackId, channel}`, grid reliable, no `tempo` | track beat grid; shifted by `range.start` if set |
| audio asset with provenance track + recipe range | the track's grid shifted by the asset's range start (+ the score's own range start) |
| `tempo` given | manual grid from `tempo` |
| otherwise | 422 `tempo-required` |

### 4.4 Inputs

Pitched: the channel's path (007 rule 7) or `assets/<id>/audio.<ext>`. Drums:
`tracks/<id>/detail/drums/{kick,snare,toms,hihat,cymbals}.flac` (all five must be kept stems of
the split; missing pieces are sent as silence by omitting them, with a warning).

### 4.5 Options and estimates (`GET /api/scores/options`)

For a source: suggested profile, allowed profiles, whether drums need splitting, grid status
(`reliable`, `unreliable`, `none`), default quality, and per engine an estimate
`seconds = duration · factor(engine, device) + overhead`. Defaults (from research.md S-02/S-04,
refined by CTO T080 from the integration-run measurements on the reference machine): basic-pitch
CPU 0.05 s/s (measured 0.011-0.019), piano CRNN CPU 0.45 s/s (measured 0.367 with 8 threads), piano
CRNN GPU 0.2 s/s (Phase 0: 0.075, integration run: 0.184), drum onsets 0.02 s/s; overhead 5 s (+ 60 s
when the piano model is not downloaded yet). Phase 0 defaults were 0.10 / 1.7 / 0.08.

### 4.6 Limits (`ContractLimits` additions)

| Constant | Value |
|---|---|
| `MinScoreRangeSeconds` | 2 |
| `ScoreProfiles` | vocals, bass, guitar, keys, piano, drums |
| `ScoreResolutions` | sixteenth, eighth |
