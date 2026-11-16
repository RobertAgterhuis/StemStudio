# Data Model: Practice Partner and Studio Bridge (feature 021)

Binding for backend (domain, persistence, API), generative (generator), stem-separation /
beat-analysis (worker conform job), audio-engine and frontend. Wire shapes are in the contract
deltas under [contracts/](contracts/); on a difference the OpenAPI documents win. Every change is
**additive** to the 009/010 (generation), 007 (assets), 014 (sections), 015 (takes), 016 (key and
chords), 017 (effects) and 019 (arrangement) models.

## 1. Storage layout (API data directory, ADR-004/ADR-016)

```text
<DataDir>/
├── tracks/<trackId>/parts/<partId>.flac          # conformed part, 44.1 kHz stereo FLAC 24-bit (new)
├── tracks/<trackId>/takes/<takeId>/...           # 015 take folder; 021 adds wet.flac + rig.json
├── amp/profiles/<profileId>.nam                  # installed or uploaded NAM capture (new)
├── amp/irs/<irId>.wav                            # cabinet impulse response (new)
├── assets/<assetId>/<name>.flac                  # 009 takes registered in the library (partner takes: 48 kHz FLAC)
└── assets/<assetId>/<name>.dawproject            # DAW export (new asset kind)
```

The worker library allow-list (ADR-016 D5, worker-api 1.4.0 patterns) gains
`tracks/<uuid>/parts/<uuid>.flac` (worker-api 1.12.0) so renders and the conform job can read parts;
partner takes are read through their 009 library asset path `assets/<uuid>/<name>.flac`, which is
already allowed. Take files of 015 are assumed to be in the
allow-list already (015); `tracks/<uuid>/takes/<uuid>/wet.flac` and `dry.flac` are added if 015 did
not add them (merge note in DELTA.md).

## 2. Practice partner

### 2.1 Generation kind `partner` (backend `Generations` table, 009)

New enum value `GenerationKind.Partner` (`partner` on the wire, backend 1.20.0 and generator 1.2.0).
New owned record `PartnerDetails` (columns prefixed `Partner_` on `Generations`):

| Field | Type | Rule |
|---|---|---|
| `TrackId` | Guid | the source track; must be `ready` with stems |
| `Family` | `PartFamily` | section 2.2 |
| `Mode` | `add` \| `alternative` | |
| `ReplacesChannel` | ChannelId? | required iff `alternative`; a standard or detail channel that exists |
| `ContextChannels` | ChannelId[] (1-6 stems) | resolved by the API: all audible standard stems except `ReplacesChannel` (its parent when it is a detail channel) |
| `RangeStartSeconds`, `RangeEndSeconds` | double | 10-480 s long, inside the track; whole song = 0..min(duration, 480) |
| `SectionIds` | Guid[] (0-32) | 014 sections the range was taken from (snapshot of labels in `SectionLabels`) |
| `Bpm` | double 40-220 | default: beat grid median |
| `TimeSignature` | `2`/`3`/`4`/`6` | default: beat grid |
| `Key` | string? | `^[A-G](#\|b)? (major\|minor)$`; default: 016 key at range start |
| `ChordsText` | string? <= 160 | built by `ChordProgressionText` (2.4); snapshot |
| `StyleHint` | string? <= 200 | passes `StyleTextGuard` (ADR-018 D8) |
| `Caption` | string <= 512 | final caption sent to the generator (2.3) |
| `InferenceSteps` | int 30-60 | default 50 (Expert only) |
| `GuidanceScale` | double 1-15 | default 7.0 (Expert only; S-01 may change the default) |

Takes: the 009 `GenerationTake` gains `Fit` (`FitReport?`, null until conformed/checked) and
`PartId` (Guid?, set when added to the mixer). `vocal` = `none` for every family except
`backingVocals`, which is `synthetic` (010 label rules).

### 2.2 Part families

| `PartFamily` (wire) | ACE-Step `track_name` | Caption fragment (English, fixed) | Default strip name | Pitched | Default pan |
|---|---|---|---|---|---|
| `drums` | `drums` | `drum kit part, steady groove that follows the song` | Drums (AI) | no | 0 |
| `percussion` | `percussion` | `hand percussion part, shakers and congas` | Percussion (AI) | no | 0.2 |
| `bass` | `bass` | `bass guitar part locked to the kick drum, following the chords` | Bass (AI) | yes | 0 |
| `guitar` | `guitar` | `rhythm guitar part, strummed and picked chords` | Guitar (AI) | yes | -0.3 |
| `keys` | `keyboard` | `piano and keyboard accompaniment, chords and simple voicings` | Keys (AI) | yes | 0.3 |
| `synth` | `synth` | `synthesizer pad and arpeggio part` | Synth (AI) | yes | 0.2 |
| `strings` | `strings` | `string section accompaniment, sustained chords` | Strings (AI) | yes | -0.2 |
| `brass` | `brass` | `brass section part, short punchy stabs and lines` | Brass (AI) | yes | 0.2 |
| `woodwinds` | `woodwinds` | `woodwind part, flute and saxophone lines` | Woodwinds (AI) | yes | -0.2 |
| `backingVocals` | `backing_vocals` | `wordless backing vocals, oohs and aahs, harmony` | Backing vocals (AI) | yes | 0 |

`fx` and lead `vocals` are not offered (spec Clarifications). Default `ReplacesChannel` suggestion
per family: drums -> `drums`, bass -> `bass`, guitar -> `guitar`, keys -> `piano`, backingVocals ->
`vocals.backing` if the 002 detail stem exists; others have none (mode `add` suggested).

### 2.3 `PartnerCaptionBuilder` (domain, pure)

`caption = join("; ", [familyFragment, "key: " + Key?, Bpm + " bpm", TimeSignature + "/4 time"
(as "3/4", "6/8" for 6), "section: " + labels?, "chords: " + ChordsText?, StyleHint?])`, trimmed to
512 characters by dropping, in this order, the chords part, then the section part (never the family
fragment or the hint). The hint is checked by `StyleTextGuard` **before** building; the whole
caption is checked again (defence in depth). Test vectors in `tests/StemStudio.Domain.Tests/Partner/`.
The flag `IncludeChords` (config `StemStudio__Partner__IncludeChordsInCaption`, default true) is set
from spike S-02.

### 2.4 `ChordProgressionText` (domain, pure)

Input: 016 chord timeline (beats with chord symbols) and the beat grid, range. One symbol per bar
(the chord that covers the most beats of the bar; `N` for no chord), bars joined by spaces, groups
of 4 bars by ` | `; an immediately repeated group of 4 is written once with `x2`, `x3`, ...;
truncated at a bar boundary to 160 characters with a trailing `...`. Example:
`Am F C G | Am F C G` -> `Am F C G x2`.

### 2.5 Fit report (`FitReport`, from the worker conform job)

| Field | Type | Meaning |
|---|---|---|
| `offsetMs` | double -250..250 | shift applied so the part's onsets lock to the grid (positive = part was late) |
| `offsetConfidence` | double >= 0 | cross-correlation peak / second peak |
| `driftMsPer30s` | double | slope of the residual beat error |
| `beatLocked` | bool | confidence >= 2 and \|drift\| <= 20 |
| `keyAgreement` | `agrees` \| `relative` \| `differs` \| `unknown` | vs 016 key (`unknown` without key) |
| `detectedKey` | string? | |
| `chordAgreement` | double 0-1 \| null | share of beats matching the 016 chord; null for drums/percussion or without chords |
| `loudnessDbfs` | double | RMS of non-silent frames |
| `gainDb` | double -12..12 | applied gain to match the reference stem |
| `veryQuiet` | bool | loudness < -45 dBFS |
| `verdict` | `good` \| `fair` \| `poor` | research R-04 |
| `notes` | string[] (0-5) | readable reasons, e.g. "No chord reference (run chord detection first)" |

Recommended take = best verdict, then highest `chordAgreement`, then highest confidence.

### 2.6 Part (`TrackParts` table, new)

| Column | Type | Rule |
|---|---|---|
| `Id` | Guid | |
| `TrackId` | Guid FK (cascade delete) | |
| `ChannelId` | string | `part-` + first 8 hex of `Id`; unique per track |
| `Name` | string 1-60 | default from 2.2; unique per track (case-insensitive) |
| `Family` | `PartFamily` | |
| `GenerationId`, `TakeIndex` | Guid, int | source take (kept when the generation is deleted: set null) |
| `ReplacesChannel` | ChannelId? | |
| `FileName` | string | `parts/<id>.flac` relative to the track folder |
| `Sha256`, `SizeBytes`, `DurationSeconds` | | duration = track duration (padded) |
| `OffsetMs`, `GainDb` | double | applied by the conform job (snapshot) |
| `Fit` | `FitReport` (JSON column) | |
| `Status` | `conforming` \| `ready` \| `failed` | |
| `Error` | string? | |
| `CreatedAt` | DateTimeOffset | |

At most 16 parts per track (`ContractLimits.MaxPartsPerTrack`). Deleting a part removes its file
and its channel entry from every mix of the track (mix channel maps keep no dangling ids).

**ChannelId extension** (backend `ChannelId` pattern and engine `ChannelId` type): 021 adds the
alternative `^part-[0-9a-f]{8}$`. Parts are top-level channels in the gate rule (like a collapsed
stem: they have no detail children); `render-vectors` gets no new case because a part behaves
exactly like a stem input (gain + pan); the backend `RenderPlanner` accepts part channels and maps
them to `tracks/<id>/parts/<partId>.flac`.

### 2.7 State machine

```text
Generation(partner): queued -> waitingForGpu? -> downloadingModel? -> generating -> completed | failed | cancelled   (009)
After completed: per take, the API starts a worker conform job in "check" mode (fit only, no file kept)
"Add to mixer":  Part(conforming) --worker conform (write)--> Part(ready) | Part(failed, retryable)
```

The fit check of all takes runs automatically after the generation completes (CPU, light lane,
about 2 s per minute of audio); "Add to mixer" runs conform again in `write` mode for the chosen take
(so unused takes never produce part files).

## 3. Amp and pedal simulation

### 3.1 Amp profile (`AmpProfiles` table, new)

| Column | Type | Rule |
|---|---|---|
| `Id` | Guid | |
| `Name` | string 1-80 | from `metadata.name` or the file name; unique (case-insensitive) |
| `Source` | `catalogue` \| `upload` | |
| `CatalogueEntryId` | string? | set for catalogue installs; unique |
| `GearType` | `amp` \| `pedal` \| `ampCab` \| `ampPedal` \| `ampPedalCab` \| `preamp` \| `studio` \| `unknown` | from `metadata.gear_type` (NAM spellings mapped) |
| `Family` | `guitar` \| `bass` \| `any` | catalogue value; uploads `any` unless the user picks |
| `Architecture` | `WaveNet` \| `LSTM` \| `ConvNet` \| `Linear` \| `A2` | allow-list (S-01 of the WASM build confirms A2) |
| `SizeClass` | `nano` \| `feather` \| `lite` \| `standard` \| `custom` | from the WaveNet config (channels/layers) |
| `SampleRate` | int | `sample_rate` or 48,000 when absent (NAM default) |
| `ModeledBy`, `Licence`, `Attribution`, `SourceUrl` | string? | catalogue values; uploads: optional user text <= 200 |
| `FileName`, `Sha256`, `SizeBytes` | | <= 16 MiB |
| `InputLevelDbu`, `OutputLevelDbu` | double? | from metadata (calibration, shown to Experts) |
| `CreatedAt` | DateTimeOffset | |

Validation of an upload (`NamFileValidator`, domain, pure over a JSON document): UTF-8 JSON object;
`version` matches `^\d+\.\d+\.\d+$`; `architecture` in the allow-list; `config` is an object;
`weights` is a non-empty array of finite numbers (count <= 4,000,000); optional `sample_rate` in
{44100, 48000, 88200, 96000}; nesting depth <= 32 (DoS guard). Errors are readable
(`"Not a NAM model file (missing 'weights')"`). The WASM core is the final judge at load time.

### 3.2 Cabinet IR (`CabinetIrs` table, new)

`Id`, `Name` (1-80, unique), `Channels` (1-2), `SampleRate` (44,100 or 48,000), `DurationSeconds`
(<= 2.0), `FileName`, `Sha256`, `SizeBytes` (<= 5 MiB), `Source` (`upload`), `CreatedAt`.
`WavHeaderReader` (domain, pure): RIFF/WAVE, `fmt ` PCM (16/24/32-bit) or IEEE float (32-bit),
`data` chunk; rejects other codecs, extensible formats other than PCM/float, and lengths above 2 s.

### 3.3 Catalogue (`Amp/catalogue.v1.json`, embedded in `StemStudio.Domain`, read-only)

JSON Schema: [contracts/amp-catalogue.schema.json](contracts/amp-catalogue.schema.json). Entry fields:
`id` (`^[a-z0-9]+(-[a-z0-9]+)*$`), `title`, `family`, `gearType`, `architecture`, `sizeClass`,
`modeledBy` (creator), `licence` (SPDX id or exact licence name), `licenceUrl`, `attribution`
(text to show), `sourceUrl` (page), `downloadUrl` (HTTPS, host in `allowedHosts`), `sha256`,
`sizeBytes`, `sampleRate`, `notes`. Top level: `version: 1`, `reviewed` (date), `allowedHosts`.
Admission rules in ADR-0212 D4; the catalogue file and `docs/THIRD-PARTY-LICENSES.md` are updated
together. Installing: the API downloads `downloadUrl` (no redirects to other hosts, 30 s timeout,
at most `sizeBytes` + 1 byte), checks SHA-256, validates the file (3.1) and stores the profile.

### 3.4 Built-in amps and cabinets (engine constants, no files)

| id | Family | Description |
|---|---|---|
| `builtin:guitar-clean` | guitar | low gain, bright tone stack |
| `builtin:guitar-crunch` | guitar | medium gain, mid push |
| `builtin:guitar-lead` | guitar | high gain, gate on by default |
| `builtin:bass-drive` | bass | low-mid drive with a clean blend |
| `builtin:cab-guitar`, `builtin:cab-bass` | | synthesised cabinet IRs (research R-10) |

### 3.5 Rig settings (`RigSettings`, JSON; shared by presets, takes and the engine)

```json
{
  "version": 1,
  "gate":   { "enabled": true, "thresholdDb": -60, "releaseMs": 80 },
  "inputGainDb": 0,
  "pedal":  { "profileId": "uuid-or-null", "enabled": true, "levelDb": 0 },
  "amp":    { "kind": "builtin|profile|none", "id": "builtin:guitar-crunch|<uuid>", "levelDb": 0,
              "tone": { "bass": 0, "mid": 0, "treble": 0 }, "drive": 0.5 },
  "cabinet": { "kind": "builtin|ir|none", "id": "builtin:cab-guitar|<uuid>", "mix": 1.0 },
  "outputLevelDb": -6,
  "effects": { "...": "017 StripEffects settings, unchanged shape" }
}
```

Ranges: `thresholdDb` -90..0, `releaseMs` 10-500, gains -24..+24 dB, tone -12..+12 dB (built-in
amps only; ignored for captures), `drive` 0-1 (built-in only), `mix` 0-1. `effects` reuses the
017 type as-is (no copy of its schema). Unknown profile/IR ids at load -> the engine substitutes
`builtin:guitar-clean` / `none` and reports `missing` (FR-019).

### 3.6 Rig preset (`RigPresets` table, new)

`Id`, `Name` (1-60, unique case-insensitive), `Family` (`guitar`|`bass`|`any`), `Settings`
(`RigSettings` JSON), `CreatedAt`, `UpdatedAt`. Five read-only seeded presets (one per built-in amp
plus "Bass clean": `builtin:bass-drive` with drive 0.1), ids `preset:<name>`. Deleting a profile
or IR leaves presets in place; on read the API marks them `missingRefs: [..]`.

### 3.7 Take additions (015 `Takes` table)

| Column | Type | Rule |
|---|---|---|
| `DryFileName` | string | 015's take file (the dry signal) - unchanged meaning |
| `WetFileName` | string? | `takes/<takeId>/wet.flac`, same length as dry (+-0 frames) |
| `Rig` | `RigSettings`? (JSON) | snapshot when the wet file was recorded or printed |
| `PlayWet` | bool | default true when a wet file exists |

"Print" (re-amp to file) uploads a new wet file for the take (replaces the previous one; the
previous one is deleted after the new one is stored). Upload rule: duration within 1 ms of the dry
file, same sample rate (44.1 kHz), FLAC or WAV, <= 300 MiB.

## 4. DAW export

### 4.1 Asset additions (007)

- `AssetKind` + `dawProject`; `AssetOrigin` + `dawExport`; `AssetFileRole` + `dawproject`
  (content type `application/vnd.dawproject+zip`, extension `.dawproject`) and `report`
  (`application/json`, the export report).
- `Provenance.dawExport` member (absent on other kinds, as 009's `restyle` member rule):
  `DawExportRecipe` + `DawExportSummary`.

### 4.2 `DawExportRecipe` (closed, normalised request)

| Field | Type | Default |
|---|---|---|
| `trackId` | Guid | required |
| `mixId` | Guid? | null = unity mix (volume 1, centre, nothing muted) |
| `arrangementId` | Guid? | null = whole song (019) |
| `channels` | ChannelId[]? (1-80) | null = every standard stem + every part + every take of the track; detail stems only when listed |
| `includeTakes` | bool | true |
| `audioFormat` | `wav` \| `flac` | `wav` |
| `bitDepth` | 16 \| 24 | 24 |
| `tempoMode` | `beatGrid` \| `constant` | `beatGrid` |
| `includeEffects` | bool | true |
| `name` | string 1-120? | "<artist> - <title> (DAW)" |

### 4.3 Export report (`report.json` and `DawExportSummary`)

```json
{
  "dawprojectVersion": "1.0",
  "tracks": 9, "clips": 54, "markers": 7, "tempoPoints": 96,
  "durationSeconds": 262.1, "sizeBytes": 435224576,
  "mapped":   [{ "channel": "bass", "items": ["volume", "pan", "mute", "solo", "eq", "compressor"] }],
  "notMapped": [{ "channel": "guitar", "setting": "reverb", "reason": "DAWproject has no built-in reverb" },
                { "channel": "take-3f2a9c1d", "setting": "amp", "reason": "Amp captures cannot be carried over; the wet audio is included and the dry DI track is muted" }],
  "skipped":  [{ "channel": "drums.kick", "reason": "Detail stem not ready" }],
  "aiGeneratedTracks": ["part-1a2b3c4d"],
  "validation": { "schema": "Project.xsd + MetaData.xsd (DAWproject 1.0, commit <sha>)", "valid": true }
}
```

### 4.4 Package layout

```text
<name>.dawproject (ZIP)
├── project.xml
├── metadata.xml
└── audio/<nn>-<channel-slug>.wav|.flac      # nn = track order 01..80
```

Track order: standard stems in engine order (vocals, drums, bass, guitar, piano, other), each
followed by its listed detail stems, then parts (creation order), then takes (dry track after its
wet track). Colours: the 004 stem palette as `#RRGGBB`. Mapping rules: [contracts/dawproject-mapping.md](contracts/dawproject-mapping.md).

### 4.5 State machine

`queued -> rendering (one 007 raw render per channel, light lane) -> packaging (API: XML, ZIP,
XSD validation) -> ready | failed`. `AssetStatus` gains no value: `packaging` is reported as
`rendering` with `progress` 90-99 and stage text "Packaging DAW project" (no enum change).

## 5. Limits (`ContractLimits` additions)

| Constant | Value |
|---|---|
| `PartnerRangeMinSeconds` / `PartnerRangeMaxSeconds` | 10 / 480 |
| `PartnerStyleHintMaxLength` | 200 |
| `PartnerChordsTextMaxLength` | 160 |
| `PartnerTakesMax` | 2 |
| `MaxPartsPerTrack` | 16 |
| `PartNameMaxLength` | 60 |
| `PartConformMaxOffsetMs` | 250 |
| `NamFileMaxBytes` | 16 MiB |
| `NamWeightsMaxCount` | 4,000,000 |
| `CabinetIrMaxBytes` / `CabinetIrMaxSeconds` | 5 MiB / 2.0 |
| `AmpProfilesMax` / `CabinetIrsMax` / `RigPresetsMax` | 200 / 100 / 100 |
| `WetTakeMaxBytes` | 300 MiB |
| `DawExportMaxChannels` | 80 |

## 6. Persistence migration `AddPartnerAmpDaw` (backend)

New tables `TrackParts`, `AmpProfiles`, `CabinetIrs`, `RigPresets`; new columns on `Generations`
(`Partner_*`), `GenerationTakes` (`FitJson`, `PartId`), `Takes` (`WetFileName`, `RigJson`,
`PlayWet`); enum conversions extended (`GenerationKind`, `AssetKind`, `AssetOrigin`,
`AssetFileRole`). Seed: rig presets (3.6). Down migration not provided (SQLite, project rule).

## 7. Generator model (1.2.0)

`GenerationKind` + `partner`; `PartnerParams` (`trackName` = ACE-Step track name from 2.2, `caption`,
`bpm`, `keyscale?`, `timeSignature`, `rangeStartSeconds`, `rangeEndSeconds`, `inferenceSteps`,
`guidanceScale`, `seeds`); request multipart: `params` + 1-6 `context` stems (FLAC/WAV, same rules
as restyle `stem` parts). `ModelId` + `acestep-v15-base`. GpuNeeds `LEGO_BASE`, `LEGO_BASE_LOWMEM`
(`PARTNER_NEEDS`, provisional 14,336 / 9,216 MiB until S-01). Take: `GeneratedTake.track`
(the ACE-Step track name) and `vocal` as 2.1. FLAC tags: ADR-018 D10 tags with
`GENERATOR=ACE-Step 1.5 base (<revision>)` plus `STEMSTUDIO_PART_FAMILY`.

## 8. Worker model (1.12.0)

Job kind `conform` in the light lane (ADR-016 D6): input `{source: library path of a take asset or a part
file, mode: check|write, beatGrid: {bpm, beats[], downbeats[]}, trackDurationSeconds, rangeStartSeconds,
key?, chords?: [{startSeconds, endSeconds, symbol}], pitched, referenceLoudnessDbfs?}`; output
`ConformResult {fit: FitReport, file?: output file (write mode: 44.1 kHz stereo FLAC 24-bit,
track length)}`. The API copies the output into `tracks/<id>/parts/<partId>.flac` (hashing copy,
ADR-016 D5) and deletes the worker job.

## 9. User levels (004 journeys) and feature catalogue ids

| Feature id | Beginner | Advanced | Expert |
|---|---|---|---|
| `partner.generate` | family, add/alternative, whole song, generate, takes list with verdict badge | + sections, style hint, takes 1-2, BPM/key/time signature edit, fit details | + inference steps, guidance, seeds, model/device/profile details |
| `partner.parts` | add to mixer, delete | + rename, re-add another take | + offset/gain details |
| `amp.chain` | on/off, rig picker (presets), input/output level, latency badge | + gate, pedal, cabinet, tone, save rig, catalogue | + latency breakdown, CPU load, model architecture/size class, resampler info |
| `amp.profiles` | (hidden; presets only) | catalogue install, upload `.nam`/IR, rename, delete | + metadata (levels, architecture, SHA-256) |
| `amp.takes` | play wet | + re-amp toggle, print | + rig snapshot view |
| `export.dawproject` | export with defaults | + channels, mix, arrangement, format, bit depth, tempo mode | + report details (mapped/not mapped list) |

## 10. Help topics (004 help-content 1.0.0, existing areas only)

| id | area | levels | anchors |
|---|---|---|---|
| `partner` | mixer | all | `partner-open`, `partner-dialog` |
| `partner-families` | mixer | all | `partner-family` |
| `partner-fit` | mixer | all | `partner-fit-*` |
| `partner-parts` | mixer | all | `part-strip-*` |
| `partner-limits` | mixer | all | `partner-limits` |
| `amp-chain` | mixer | all | `amp-toggle`, `amp-panel` |
| `amp-latency` | mixer | all | `amp-latency` |
| `amp-profiles` | mixes | advanced, expert | `amp-profiles-page` |
| `amp-licences` | mixes | advanced, expert | `amp-catalogue-*` |
| `amp-own-files` | mixes | advanced, expert | `amp-upload` |
| `amp-reamp` | mixer | advanced, expert | `take-reamp-*` |
| `daw-export` | tracks | all | `daw-export-*` |
| `daw-compatibility` | tracks | all | `daw-compat` |
| `daw-report` | tracks | advanced, expert | `daw-report` |
| `nam`, `impulse-response`, `di-signal`, `dawproject` | glossary | all | - |
