# Data Model: Instrument Views

Builds on the 001-015 data models, in particular [008](../008-sheet-music/data-model.md)
(profiles, notation, scores), [006](../006-synced-lyrics-karaoke/data-model.md) (lyrics, banner),
[002](../002-detail-separation/data-model.md) (detail stems), 003 (device policy), 012 (transpose,
count-in), 014 (sections, score-follows-playback) and 015 (input, latency, takes). Wire shapes:
[contracts/DELTA.md](contracts/DELTA.md). Design: ADR-031, ADR-032, ADR-033. Times are integer
milliseconds from the start of the track's audio unless stated otherwise.

## 1. Configuration

| Where | Name | Default | Meaning |
|---|---|---|---|
| worker | `STEMSTUDIO_CHORD_ENGINE` | `btc` (`fake` when `STEMSTUDIO_SEPARATOR=fake`) | `btc`, `chroma-hmm` or `fake`; `btc` falls back to `chroma-hmm` per job when the weights are missing and cannot be downloaded (result `engine` says which ran) |
| worker | `STEMSTUDIO_PITCH_ENGINE` | `pyin` (`fake` with the fake separator) | `pyin`, `yin` (fast fallback, research R-06) or `fake` |
| worker | `STEMSTUDIO_ANALYSIS_THREADS` | `4` | torch/numpy threads of the `instrument-analysis` lane |
| API | `StemStudio:InstrumentViews:AutoChords` | `true` | start chord detection when the standard separation completes |
| API | `StemStudio:InstrumentViews:AutoDrumHits` | `true` | start drum hit detection when the drums detail split completes |
| API | `StemStudio:InstrumentViews:Estimates` | see 7.7 | seconds per audio second per (kind, engine, device) for shown estimates (7.7) |

Model weights (model manager, as 002/008; downloaded at first use, never committed):

| Model id | Source (commit `2682317b`) | SHA-256 | Bytes | Licence |
|---|---|---|---|---|
| `btc-majmin` | `https://raw.githubusercontent.com/jayg996/BTC-ISMIR19/2682317be668032e6e4b269ded36adaa2ad57df0/test/btc_model.pt` | `71c2c5db17e8c43b8a9a9da5db36ef2d667158c07a214eba16344c154c00bf54` | 12,154,754 | MIT |
| `btc-large-voca` | `.../test/btc_model_large_voca.pt` | `1673d23f8f9a55ae7f9e8b80a51da616debb22675b8d8b67ea6ce0ef37b0ab51` | 12,229,576 | MIT |

`BTC_GPU_NEED = GpuNeed("Chord recognition (BTC)", 512)` (Phase 0: no GPU run in the design phase; the
T140 NVML peak + 20 % replaces it). Constants `ports.BTC_MAJMIN_WEIGHTS`, `ports.BTC_LARGE_VOCA_WEIGHTS`,
`ports.BTC_SAFE_GLOBALS` (the three numpy names `torch.load(weights_only=True)` may resolve, research R-03).

## 2. Chords

### 2.1 Vocabulary and labels

Labels use Harte syntax (`<root>:<quality>[/<bass interval>]`, `N` = no chord, `X` = unknown).

| Vocabulary | Qualities | Classes |
|---|---|---|
| `simple` | `maj`, `min` | 25 (12 x 2 + N) |
| `full` | `maj`, `min`, `dim`, `aug`, `min6`, `maj6`, `min7`, `minmaj7`, `maj7`, `7`, `dim7`, `hdim7`, `sus2`, `sus4`; inversions as `/3`, `/5`, `/b7`, `/7` bass intervals (from the bass stem's lowest pitch class, 2.3) | 170 + inversions |

Display suffix per quality: `maj` "", `min` "m", `dim` "dim", `aug` "aug", `min6` "m6", `maj6`
"6", `min7` "m7", `minmaj7` "m(maj7)", `maj7` "maj7", `7` "7", `dim7` "dim7", `hdim7` "m7b5",
`sus2` "sus2", `sus4` "sus4"; slash bass "/<note>". Example `A:min7/b7` -> "Am7/G".

### 2.2 Chord segment (worker result, stored, sent to the UI)

| Field | Type | Rule |
|---|---|---|
| `startMs`, `endMs` | int | `0 <= start < end <= duration`; segments contiguous and ordered, covering the whole track (`N` fills gaps) |
| `label` | string | Harte, as 2.1 |
| `root` | int 0..11 \| null | pitch class (C = 0); null for `N`/`X` |
| `quality` | enum 2.1 \| null | |
| `bass` | int 0..11 \| null | pitch class of the bass when not the root |
| `confidence` | number 0..1 | mean posterior (BTC) or mean normalised similarity (chroma-hmm) over the segment |
| `edited` | bool | backend only: set by FR-015 edits |

At most 4,000 segments per track.

### 2.3 Pipeline (worker, binding order)

1. Mix the harmonic stems (research R-04) to mono 22,050 Hz.
2. Engine: BTC (vocabulary `simple` -> `btc-majmin`, `full` -> `btc-large-voca`; through the
   `DeviceRunner` with `BTC_GPU_NEED`) or chroma-hmm (`simple` only; `full` requested with
   chroma-hmm -> `simple` with warning `vocabulary-reduced`). Output: per-frame class posteriors
   (or one-hot) at 92.88 ms.
3. Beat snapping when the grid is reliable (R-04), else 0.5 s minimum merge.
4. Slash bass (`full` only): per segment, the strongest pitch class of the `bass` stem's chroma;
   when it is the third, fifth or seventh of the chord, the inversion is added.
5. Key regions and global key (research R-13, section 2.8) -> `key`, `keyRegions`, `preferFlats`
   (of the global key).
6. Merge consecutive equal labels; if every segment is `N` the result has `noChords = true` and
   `key = null`, `keyRegions = []`.

Progress: 0-0.05 load/mix, 0.05-0.80 model, 0.80-0.90 key, 0.90-1.0 post-processing.

### 2.4 ChordChart (result document)

`{ vocabulary, engine (btc|chroma-hmm|fake), key, keyRegions[], preferFlats, beatSnapped (bool),
noChords (bool), segments[], warnings[{code, text}] }`. Warning codes (additional):
`key-uncertain` ("The key is uncertain (confidence <n> %)." when the global confidence < 0.5).
Other codes: `vocabulary-reduced` ("The fast method knows
only major and minor chords."), `grid-unreliable` ("Chord changes are not aligned to beats
because the beat grid is uncertain."), `fallback-engine` ("The chord model was not available;
the fast method was used."), `few-chords` ("Very few chords were detected.").

### 2.5 Display transposition and spelling (backend C# and engine TS, shared vectors)

`displayRoot = (root + transposeSemitones - capo) mod 12`, same for `bass`. Spelling uses the
key of the region the chord starts in (2.8; fallback the global key, then sharps), moved by the
same amount: `(region.tonic + transposeSemitones - capo) mod 12`, with the R-04 flat-key rule:
sharps names `C C# D D# E F F# G G# A A# B`, flat names `C Db D Eb E F Gb G Ab A Bb B`; F# major
and D# minor spell with sharps, Gb major and Eb minor with flats (both 6 accidentals; the rule
picks flats for tonic 6 major / 3 minor). `transposeSemitones` = the current 012 transpose
(-12..12), `capo` 0..12 (capo mode, guitar family only; 0 otherwise). Key names use the same
rule (`displayKey`). Vectors: `contracts/chord-spelling-vectors.json` (Phase 0, at least 200
spelling cases incl. every quality, slash chords and region keys, plus the capo section of 2.9),
consumed by `ChordSpellingTests` (backend) and `chord-spelling.test.ts` (engine).

### 2.6 Chord sheet (backend, for the lyrics card and ChordPro)

Built by `ChordSheetBuilder` from the lyrics document (006) and the chord chart:

- For each lyric line: the chords whose start falls in `[line.startMs, nextLine.startMs)`, each
  anchored at the character index of the word containing its start (word start <= chord start
  < next word start), or at index 0 with `lead = true` when it starts before the first word.
- Chords between the last line and the end, or in gaps of more than one bar without words, form
  instrumental rows (`text = ""`), at most 8 chords per row.
- Without lyrics (status not `found`, or plain text): rows of 4 bars (from the beat grid) or 8
  chords.
- `GET /api/tracks/{id}/chords/sheet?transpose&capo` returns `{transpose, capo, rows[{text,
  chords[{charIndex, symbol, lead}]}]}`; `.../chords/chordpro` returns the same as ChordPro
  (`{title: }`, `{artist: }`, `{capo: n}` when > 0, `{key: }`, `[Am]` inline chords; instrumental
  rows as `[C] [G] ...`), `text/plain; charset=utf-8`, attachment `<title>.cho`.

### 2.7 Fake chords

Research R-11, plus a fake key: C major (confidence 0.9) for bars 1-8 and D major from bar 9
(the fake progression moves to `D:maj, A:maj, B:min, G:maj`), so E2E sees one key change. The
capo E2E case uses a 012 transpose of +3 (the song then sounds in E-flat major) instead of a
second fake variant: the shortcut is capo 3 and the ranking follows research R-14.

### 2.8 Key (scope addition S11)

`AnalysisKey {tonic 0..11, mode major|minor, confidence 0..1, alternative {tonic, mode} | null}`
(global key); `KeyRegion {startMs, endMs, tonic, mode, confidence}` contiguous over the non-`N`
part of the song, ordered, each >= 8 bars (or 16 s), at most 50. Method: research R-13.

Display (backend `KeyFormatter`, engine `displayKey`): "<Tonic> major|minor" spelled by 2.5;
with transpose `t != 0`: "<transposed key> (original <key>, +t)"; confidence < 0.5 appends
"(uncertain)"; Advanced shows "<n> %" and the alternative ("or A minor"). Written key for a
transposing instrument (4.2): concert tonic + interval semitones mod 12, spelled by 2.5 (e.g.
concert C major, alto sax M6 -> A major).

`Track` summary (backend): `key {tonic, mode, confidence, display, regions (count)} | null` taken
from the chords analysis when `ready` (null otherwise), so the track page and the mixer show it
next to the BPM without loading the chart.

### 2.9 Capo mode and suggestions (scope addition S12)

- Capo applies only to the guitar family (catalogue family `guitar`; 6-string tunings). The
  chosen capo `c` (0..12) and `showSounding` (Advanced) are browser preferences (section 8) per
  track; the engine gets them through `setChordDisplay`; the sheet/ChordPro through `?capo=`.
- Shapes: 2.5 with `capo = c`; the badge reads "Capo <c>" when `c > 0`; with `showSounding` the
  sounding symbol (2.5 with `capo = 0`) is shown in small after the shape ("C (Eb)").
- Suggestions: `GET /api/tracks/{id}/chords/capo-suggestions?transpose=t` -> `{transpose,
  shortcut: {capo, reason: original-shapes} | null, suggestions: [{capo, shapeKey (display),
  openShare 0..1, fewOpenShapes (bool)}] (<= 3, best first)}` by research R-14 (`CapoAdvisor`).
  `409 chords-not-ready` when chords are not ready.
- Tablature: the score dialog prefills `score-tab-capo` with `c` for guitar (FR-078).
- Vectors: section `capo` of `chord-spelling-vectors.json` - `{root, quality, bass, transpose,
  capo, shape}` for every root x quality x capo 0..12 at transpose 0 and samples at -12..12; and
  `capoFixtures` - four progressions with expected suggestion lists.

## 3. Singing feedback

### 3.1 Reference melody (worker result `VocalPitch`)

| Field | Type | Rule |
|---|---|---|
| `hopMs` | int | 10 |
| `frames` | int[] | centi-semitones (MIDI x 100, e.g. A4 = 6900), 0 = unvoiced; length = ceil(duration / 10 ms) <= 90,000 |
| `source` | `vocals.lead` \| `vocals` | which stem was used |
| `engine` | `pyin` \| `yin` \| `fake` | |
| `voicedRatio` | 0..1 | share of voiced frames |
| `range` | `{lowCents, highCents}` \| null | 5th/95th percentile of voiced frames; null when none |
| `warnings` | `[{code, text}]` | `few-voiced` ("Very little sung melody was found."), `detail-bleed` (008 text, source is a detail stem) |

Progress: 0-0.1 load/resample, 0.1-0.95 pitch, 0.95-1.0 packing. A silent stem fails with "The
vocal stem is silent, so no melody can be shown." (`NoMelodyError`).

### 3.2 Live tracker (engine, AudioWorklet `pitch-tracker`)

Research R-05 parameters (binding). Messages worklet -> main: `{ t: number (context time of the
window centre), cents: number | null, clarity: number 0..1 }` every 10 ms; main -> worklet:
`{ type: 'stop' }`. Main thread: 5-frame median over voiced values, `null` breaks the line.

### 3.3 Alignment

`songMs(frame) = round(1000 * transportPosition(frame.t - roundTripSeconds))`, the karaoke-clock
formula of 006 with the round trip instead of the output latency; `roundTripSeconds` = the 015
calibration Blazor passes in the request (`roundTripMs`, `latencyMethod` `loopback`/`manual`) or the open
input's `InputInfo.estimatedLatencySamples` (`estimated`); the engine stores no calibration (Phase 0).

### 3.4 Scoring (binding)

- Units: lyric lines (006 lines with `startMs`/`endMs`) when lyrics are `found` and line/word
  synced; otherwise phrases = 014 sections; otherwise blocks of 4 bars of the beat grid; otherwise
  10 s blocks.
- For each 10 ms reference frame `r` inside the unit and voiced: target `= r + 100 *
  transposeSemitones`; the singer value `s` = the median-filtered singer pitch at the same song
  time (nearest frame within 15 ms, else missing). Error `e = s - target`, folded:
  `e' = ((e + 600) mod 1200) - 600` (octave-independent). Hit when `s` exists and
  `|e'| <= toleranceCents`.
- `score = round(100 * hits / voicedReferenceFrames)`; units with fewer than 20 voiced reference
  frames (0.2 s) get `null`. Loops: a unit is scored again on every pass (latest pass shown).
  Seeking into a unit discards the partial unit (no score).
- Tolerance: 100 (Beginner), 50 (Advanced), 25 (Expert) cents; adjustable 10..200 in steps of 5 at
  Advanced+.
- Summary on stop or track end: `{ unitsScored, average (rounded mean of scored units), best
  {index, score}, toleranceCents, latencySource }`. Not sent to the server (FR-047).

### 3.5 Pitch view

Window: 4 s back, 2 s ahead of the current song time; vertical range = reference `range` +-3
semitones (min 1 octave), folded singer values drawn in the octave nearest to the target;
reference = horizontal bars per run of equal (to 50 cents) voiced frames; singer = polyline;
the "now" line at 2/3 of the width. Reduced motion: no scrolling; the window pages every 6 s.

## 4. Notation extensions (008 pipeline)

### 4.1 Tuning catalogue (strings low to high, MIDI)

| Id | Instrument | Strings | Pitches | Label |
|---|---|---|---|---|
| `guitar-standard` | guitar | 6 | 40 45 50 55 59 64 | Standard (E A D G B E) |
| `guitar-drop-d` | guitar | 6 | 38 45 50 55 59 64 | Drop D |
| `guitar-half-step-down` | guitar | 6 | 39 44 49 54 58 63 | Half step down (Eb) |
| `guitar-full-step-down` | guitar | 6 | 38 43 48 53 57 62 | Full step down (D) |
| `guitar-dadgad` | guitar | 6 | 38 45 50 55 57 62 | DADGAD |
| `guitar-open-g` | guitar | 6 | 38 43 50 55 59 62 | Open G |
| `guitar-open-d` | guitar | 6 | 38 45 50 54 57 62 | Open D |
| `bass-4-standard` | bass | 4 | 28 33 38 43 | 4-string (E A D G) |
| `bass-4-drop-d` | bass | 4 | 26 33 38 43 | 4-string drop D |
| `bass-4-half-step-down` | bass | 4 | 27 32 37 42 | 4-string half step down |
| `bass-5-standard` | bass | 5 | 23 28 33 38 43 | 5-string (B E A D G) |
| `bass-6-standard` | bass | 6 | 23 28 33 38 43 48 | 6-string (B E A D G C) |

Pitches are sounding pitches (the 008 guitar/bass parts are written an octave higher; tab
numbers relate to sounding pitch). Capo `0..12` for guitar tunings only (bass: 422 `capo-not-
allowed`). Frets are counted from the capo (`fret 0` = the capo) and `fret + capo <= 24`.

### 4.2 Written instrument catalogue (sounding -> written interval)

| Id | Name (part name) | Key | Interval (written = sounding +) | Default clef | Sounding range (MIDI) |
|---|---|---|---|---|---|
| `concert` | (profile's instrument name) | C | P1 | profile | profile |
| `trumpet-bb` | Trumpet in B-flat | Bb | M2 | treble | 52-82 |
| `clarinet-bb` | Clarinet in B-flat | Bb | M2 | treble | 50-91 |
| `soprano-sax-bb` | Soprano Saxophone in B-flat | Bb | M2 | treble | 56-87 |
| `tenor-sax-bb` | Tenor Saxophone in B-flat | Bb | M9 | treble | 44-75 |
| `bass-clarinet-bb` | Bass Clarinet in B-flat | Bb | M9 | treble | 38-77 |
| `alto-sax-eb` | Alto Saxophone in E-flat | Eb | M6 | treble | 49-80 |
| `baritone-sax-eb` | Baritone Saxophone in E-flat | Eb | M13 | treble | 36-68 |
| `clarinet-eb` | Clarinet in E-flat | Eb | -m3 | treble | 53-91 |
| `horn-f` | Horn in F | F | P5 | treble | 34-77 |
| `english-horn-f` | English Horn in F | F | P5 | treble | 52-81 |
| `flute` | Flute | C | P1 | treble | 60-96 |
| `oboe` | Oboe | C | P1 | treble | 58-91 |
| `violin` | Violin | C | P1 | treble | 55-100 |
| `viola` | Viola | C | P1 | alto | 48-88 |
| `cello` | Cello | C | P1 | bass | 36-76 |
| `double-bass` | Double Bass | C | P8 | bass | 28-67 |
| `trombone` | Trombone | C | P1 | bass | 40-72 |
| `bassoon` | Bassoon | C | P1 | bass | 34-75 |
| `tuba` | Tuba | C | P1 | bass | 28-65 |

`-m3` = written a minor third lower. Key signatures beyond 7 accidentals are respelled (R-09).
Every instrument may be chosen for every pitched profile (a singer may want an alto-sax part).
The written pitch range is the sounding range plus the interval; notes outside the sounding range
raise `part-out-of-range` (FR-053).

### 4.3 Clefs

`auto` (instrument default, else the profile's 008 clef), `treble`, `bass`, `alto`, `tenor`,
`treble-8vb`, `bass-8vb`. Grand-staff profiles (`keys`, `piano`) ignore the clef choice (422
`clef-not-allowed` when set); `drums` accepts neither tablature, part nor clef.

### 4.4 New profiles (extends 008 2.1)

| Profile | Staves / clef | Range (MIDI) | Single voice | Engine | Default resolution | Suggested for |
|---|---|---|---|---|---|---|
| `strings` | 1, treble (auto clef via instrument) | 28-103 | yes (the loudest line of a section) | basic-pitch | 1/16 | `*.strings` |
| `brass` | 1, treble | 28-84 | yes | basic-pitch | 1/16 | `*.brass` |
| `woodwinds` | 1, treble | 34-98 | yes | basic-pitch | 1/16 | `*.woodwinds` |

The 008 suggestion `other.* -> keys` and `piano.* -> keys` keeps applying to the other detail stems; `<parent>.strings|brass|woodwinds` now suggest the new profiles. Section stems
reduced to one line raise the new warning `section-reduced` ("This stem may hold a whole section;
the part shows its loudest line.").

### 4.5 Tablature (`notation/tablature.py`)

Input: the cleaned, quantised events of the guitar/bass profile (008 2.3 steps 1-5) and
`TablatureSettings {tuning, capo, layout (notation-and-tab|tab-only)}`.

1. Range fit: a pitch below the lowest open string (+capo) is raised by octaves, above the top
   string's fret 24 lowered by octaves; counted -> `tab-octave-moved` ("<n> notes were moved by an
   octave to fit the tuning.").
2. Chords with more pitches than strings: keep the loudest `strings` pitches -> `tab-notes-dropped`
   ("<n> chord notes were left out because there are not enough strings.").
3. Viterbi over events with the R-08 cost; ties broken by lower string index (higher-pitched
   string first is never preferred), then lower fret.
4. Chords whose best assignment spans > 4 fretted frets keep it and raise `tab-wide-stretch`
   ("<n> chords need a wide stretch.").
5. Output per note `(string 1 = highest-pitched string, fret)` as MusicXML technical elements;
   the part gets a second staff (`notation-and-tab`) or only the TAB staff (`tab-only`) with
   `<staff-lines>` = string count, `<staff-tuning>` per line and `<capo>` when > 0.

`TablatureSummary {tuning, strings, capo, layout, octaveMoved, notesDropped, wideStretches}` in
`ScoreSummary.tablature`.

### 4.6 Parts (`notation/parts.py`)

`PartSettings {instrument (4.2 id), clef (4.3)}`; applied after key estimation: the score is
transposed by the interval, the key signature with it, accidentals respelled for the written key,
part name and `<transpose>` set, clef set. `PartSummary {instrument, name, interval,
writtenKey, clef, outOfRange}` in `ScoreSummary.part`. With `concert` and `auto` the output is
byte-identical to 008 (regression test).

### 4.7 Chord symbols in scores (FR-017)

`TranscriptionJobRequest.chords` = the chord segments (2.2, display already spelled for the key at
concert pitch); each segment start is quantised with the 008 time base and inserted as a
`harmony.ChordSymbol` on the top staff (MusicXML `<harmony>`); `N` segments are skipped;
transposed parts transpose the symbols with the part. Allowed for `vocals`, `guitar`, `keys`,
`piano`, `strings`, `brass`, `woodwinds` (not `bass`, `drums`).

### 4.8 Drum MIDI and single pieces

Drum MIDI uses channel 10 and the R-10 map. A source `drums.<piece>` (a single drum detail stem)
may use the `drums` profile with only that piece (the `missing-drum-piece` warnings of the other
pieces are not raised in that case).

## 5. Drum trainer

### 5.1 DrumHits (worker result)

`{ hits: [{timeMs, piece (kick|snare|toms|hihat|cymbals), strength 0..1}] (<= 40,000, time
order), pieces: [the pieces that had an input], engine (drum-onsets|fake) }`.

### 5.2 Hit lanes (engine)

Lane order top to bottom: cymbals, hihat, toms, snare, kick (drum notation order). "Now" line at
20 % of the width; look-ahead `lookAheadMs` 4000 (2000..8000 at Advanced); marks: rounded
rectangles, height 40-100 % of the lane by strength; hits within 30 ms in the past flash.
Reduced motion: no scrolling; the window pages per bar (beat grid) or per `lookAheadMs`.

### 5.3 Family rule (presets, "Drumless", "Practise as")

The channels of a family (002 parents `other`, `guitar` and `piano` may each hold any of the
instrument detail stems, so a detail channel belongs to the family of its **detail name**, and a
`rest` detail to the family of its parent):

| Family | Standard stems | Detail names (any parent) |
|---|---|---|
| `vocals` | `vocals` | `lead`, `backing` |
| `drums` | `drums` | `kick`, `snare`, `toms`, `hihat`, `cymbals`, `percussion` |
| `bass` | `bass` | - |
| `guitar` | `guitar` | `acoustic-guitar`, `electric-guitar` |
| `keys` | `piano` | `keys`, `synth` |
| `orchestral` (strings/brass/woodwinds) | `other` while it is not split | `strings`, `brass`, `woodwinds` |

Applying a
minus-one mix mutes every channel of the family whether its group is expanded or not (mute flags
of hidden channels are kept, 002 rule, so the result holds after expand/collapse), unmutes and
unsolos every other channel, keeps volumes and pans.

"Drumless" = minus-one `drums` + metronome on + count-in one bar (012) + open the drum trainer;
second click restores the stored previous mute/solo flags of the drum family and the previous
metronome/count-in settings. "Guide piece" (Advanced) = solo off for all, unmute only
`drums.<piece>`.

## 6. Journey levels (004)

| Option | Beginner | Advanced | Expert |
|---|---|---|---|
| Chords lane, banner chords, chord sheet, capo display | shown | shown | shown |
| Chord vocabulary `full`, chord edit, ChordPro download | - | shown | shown |
| Chord engine choice (`btc`/`chroma-hmm`), confidence per segment | - | - | shown |
| Key next to the tempo, current region, transposed key, written key (orchestral) | shown | shown | shown |
| Key confidence in %, relative alternative, region list with bars | - | shown | shown |
| Capo mode (guitar family): capo, badge, suggestions, "original shapes" shortcut | shown | shown | shown |
| Sounding symbol next to the shape | - | shown | shown |
| Tab layout + tuning (guitar standard / bass 4-string preselected) | shown | shown | shown |
| Capo, all tunings, string count | - | shown | shown |
| Hit lanes, Drumless | shown | shown | shown |
| Look-ahead, guide piece | - | shown | shown |
| Singing feedback, line scores, summary | shown (100 cents) | shown (50) | shown (25) |
| Tolerance slider, latency source text | - | shown | shown |
| Raw pitch readout (Hz, cents) | - | - | shown |
| Written instrument picker | shown | shown | shown |
| Clef choice, "include chords" | - | shown | shown |
| "Practise as ..." picker | shown | shown | shown |

Feature-catalogue ids (004 design-spec 5): `chords.view`, `chords.full`, `chords.edit`,
`chords.export`, `chords.engine`, `tab.basic`, `tab.tunings`, `drums.trainer`, `drums.guide`,
`singing.feedback`, `singing.tolerance`, `singing.raw`, `parts.instrument`, `parts.clef`,
`scores.chords`, `practise-as`, `key.view`, `key.details`, `capo.mode`, `capo.sounding`.

## 7. Backend

### 7.1 TrackAnalyses (table, 1:n Track, cascade delete; unique `(TrackId, Kind)`)

| Field | Type | Rule |
|---|---|---|
| `TrackId` | uuid FK | |
| `Kind` | `chords` \| `drumHits` \| `vocalPitch` | |
| `Status` | `none` \| `queued` \| `running` \| `ready` \| `failed` \| `unavailable` \| `stale` | 7.2 |
| `Reason` | string \| null (<= 1000) | readable reason for `failed`/`unavailable`/`stale` |
| `JobId` | 32 hex \| null | current worker job |
| `Progress` | 0..1 | |
| `Options` | JSON | chords `{vocabulary, engine}`; others `{}` |
| `Document` | JSON \| null | ChordChart (2.4) with `edited` flags, DrumHits (5.1) or VocalPitch (3.1) |
| `Engine` | string \| null | engine that produced the document |
| `Device` | JobDevice \| null | 003 |
| `SourceFingerprint` | string \| null | SHA-256 over the sorted `(channel, sha256)` of the input stems |
| `EstimateSeconds` | int \| null | shown while queued/running; on the wire also for `none` and for `unavailable` when the inputs exist (the estimate a run would get, not stored; integration 2026-11-22, backend review M3) |
| `UpdatedAt` | date-time | |

### 7.2 Status transitions

```text
none ──(auto rule 7.3, or POST)──> queued ──(worker running)──> running ──(completed)──> ready
queued | running ──(worker failed / 404 after restart)──> failed
none | failed | ready | stale ──(POST)──> queued      (POST on ready chords with edits needs ?overwriteEdits=true, else 409 edits-exist)
any ──(prerequisite missing: drums not split, no vocals stem, no harmonic stem)──> unavailable
ready ──(source fingerprint changes: re-separation, detail split redone)──> stale ──(auto rule 7.3)──> queued
any ──(DELETE)──> none (document cleared)
```

### 7.3 Auto rules

- Chords: when the standard job completes and `AutoChords`; when stale and `AutoChords`.
- Drum hits: when the drums detail split completes (outcome `split`) and `AutoDrumHits`; when stale.
- Vocal pitch: never automatic; `POST` from the singing panel's first open (the UI asks with the
  estimate shown when it is above 60 s).
- Old tracks (status `none`): the mixer's views call `POST` on first open (chords, drum hits).

### 7.4 Inputs

Chords: the cached standard stems `bass`, `guitar`, `piano`, `other` that exist (007 path rules);
drum hits: the five `tracks/<id>/detail/drums/<piece>.flac` paths (as 008 4.4); vocal pitch:
`tracks/<id>/detail/vocals/lead.flac` when vocals are split with a kept `lead` stem, else the
`vocals` stem. Grid: the track's beat grid when reliable, else none (chords only).

### 7.5 DTOs (wire)

- `TrackDto` (additive, optional on the wire: absent = every status `none` / no key): `instrumentViews
  {chords, drumHits, vocalPitch}` = statuses only; `key` = the 2.8 summary or null. All 016 additions to
  existing DTOs (score request, recipe, options, facts) are optional too (Phase 0, DELTA.md).
- `GET /api/tracks/{id}/key?transpose&instrument` -> `{status (of the chords analysis), key,
  display, regions [{startMs, endMs, startBar, tonic, mode, confidence, display}], writtenKey
  (display or null for `concert`)}`; `GET /api/tracks/{id}/chords/capo-suggestions?transpose`
  -> 2.9.
- `TrackAnalysisDto<T>`: `{kind, status, reason, progress, engine, device, estimateSeconds,
  updatedAt, limits (text, FR-064), document: T | null}` with T = `ChordChart` (+ `edited` on
  segments), `DrumHits`, `VocalPitch`.
- `ScoreOptions` (additive): `tablature {available, defaultTuning, tunings[], capoAllowed}`,
  `parts {instruments[], clefs[], defaultInstrument}`, `chords {available (ready chords and an
  allowed profile)}`.
- `CreateScoreRequest` (additive): `tablature {tuning, capo, layout} | null`, `part {instrument,
  clef} | null`, `includeChords: bool` (default false).
- `ScoreFacts` (additive): `tablature: TablatureSummary | null`, `part: PartSummary | null`,
  `chordsIncluded: bool`.
- `ScoreRecipe` (additive, provenance): `tablature`, `part`, `includeChords`.
- `listAssets` (additive query): `trackId` + `channel` filter (the MIDI action looks up a ready
  score of that channel; newest first).
- `GET /api/instruments/catalogue` (the `concert` row has name "", `defaultClef` `auto`, `lowMidi` 0,
  `highMidi` 127 = the profile's own clef and range) -> `{tunings (4.1), instruments (4.2), clefs (4.3),
  families (5.3 with their channel patterns and view ids), tolerances {beginner 100, advanced 50,
  expert 25, min 10, max 200, step 5}}` (static; cached by the UI).

### 7.6 Limits (`ContractLimits` additions)

| Constant | Value |
|---|---|
| `MaxChordSegments` | 4000 |
| `MaxDrumHits` | 40000 |
| `MaxPitchFrames` | 90000 |
| `ChordLabelMaxLength` | 32 |
| `CapoMax` | 12 |
| `TransposeDisplayRange` | -12..12 |
| `TabMaxFret` | 24 |
| `ToleranceCents` | 10..200, step 5 |

### 7.7 Estimates

`seconds = duration x factor(kind, engine, device) + overhead` (5 s; + 15 s when the BTC weights
are not downloaded yet). Defaults until the CTO's reference-machine run (T140) refines them:
chords BTC CPU 0.03 s/s (forward pass measured in T063), BTC GPU 0.01, chroma-hmm 0.015 (measured
0.012); drum hits 0.02; vocal pitch pyin 0.035 (measured 0.033), yin 0.005 (measured 0.004). Phase 0
measurements: research R-03, R-04, R-07 outcomes.

## 8. Browser preferences (`localStorage`, try/catch, defaults when unavailable)

`stemstudio.views.family` (last "Practise as" family, offered not applied), `stemstudio.chords.capo`
(per track id map `{capo 0..12, showSounding}`; capo mode, guitar family only),
`stemstudio.views.instrument` (written instrument of the orchestral family, for the written key), `stemstudio.chords.lane` (bool, default true),
`stemstudio.drums.lookAheadMs`, `stemstudio.singing.toleranceCents`,
`stemstudio.drumless.previous.<trackId>` (stored flags for the restore, cleared on restore).

## 9. Engine types (TypeScript, see contracts/audio-engine.delta.md)

`ChordSymbolEvent {startMs, endMs, root|null, quality|null, bass|null}`, `ChordChartData
{segments, keyTonic, keyMode, keyRegions [{startMs, endMs, tonic, mode}]}`, `ChordDisplay {capo,
showSounding?}` (transpose comes from the engine's 012 state), `ChordLaneOptions {barsBehind 1, barsAhead 6, reducedMotion?}`,
`DrumHitEvent {timeMs, piece, strength}`, `HitLaneOptions {lookAheadMs, reducedMotion?}`,
`PitchContour {hopMs, frames: Int16Array | number[]}`, `SingingUnit {index, startMs, endMs}`,
`SingingFeedbackRequest {pitchViewElementId, reference, units, toleranceCents, roundTripMs?:
number | null, latencyMethod?}`, `SingingUnitScore {index, score|null, pass}`, `SingingSummary`
(`latencySource` = 015 `LatencyMethod`), `TakeScoreRequest {takeUrl, startSample, reference, units,
toleranceCents}`, `RoundTripLatency {ms, source}`,
`SingingState = 'idle' | 'starting' | 'running' | 'stopped' | 'failed'`. Binding: `types.ts` 2.6.0.