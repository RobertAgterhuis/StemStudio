# ADR-034: DAW export as DAWproject 1.0 in the asset library (feature 021, C11)

- **Status**: Proposed (architect, 2026-11-16; Accepted with the 021 plan; S-05 adds the observed
  DAW behaviour).
- **Numbering**: feature-scoped, see ADR-025.
- **Context documents**: `specs/021-partner-studio-bridge/{spec.md, research.md (R-12..R-15),
  data-model.md 4, contracts/dawproject-mapping.md}`, ADR-016 (asset library, render in the
  worker), the 014 (sections), 017 (effects) and 019 (arrangement) designs.

## Context

MoSCoW C11: export to a DAW as DAWproject (open format) with stems, tempo, sections and the
arrangement, landing in the 007 export library. DAWproject (bitwig/dawproject, MIT) is at version
1.0, stable, with `Project.xsd` and `MetaData.xsd`; supported by Bitwig Studio, Studio One, Cubase,
Cubasis, VST Live and n-Track; not by Ableton Live.

## Decisions

### D1 Format and version

DAWproject **1.0** (`Project version="1.0"`), ZIP container `.dawproject` with `project.xml`,
`metadata.xml`, `audio/`. The XSDs are vendored as embedded resources of `StemStudio.Domain`
(`DawProject/Schemas/{Project.xsd, MetaData.xsd, LICENSE}`, pinned by commit + SHA-256). Content
type `application/vnd.dawproject+zip` (unregistered vendor type, documented).

### D2 Who does what

- The **API** (domain `DawProjectBuilder`, pure; `DawExportService`, orchestration) resolves
  channels, mix, effects, sections, beat grid and arrangement into a `DawProjectModel`, writes XML
  with `XmlWriter`, validates both XML files against the XSDs with `XmlSchemaSet` before writing
  the ZIP, and stores the result as an asset (kind `dawProject`, origin `dawExport`, roles
  `dawproject` + `report`).
- The **worker** only renders each channel in 007 `raw` mode (unity gain, centre) to WAV 24-bit
  (default) or WAV 16 / FLAC: existing render jobs, no worker contract change for C11.
- No new NuGet or npm dependency (BCL `System.Xml`, `System.IO.Compression`).

### D3 Timing: seconds lanes, tempo map from the beat grid, no warping

Lanes use `timeUnit="seconds"`; clips carry no `Warps` and no stretch algorithm, so every DAW plays
the audio exactly as rendered. The tempo map is a `TempoAutomation` with one point per bar (hold),
`Transport/Tempo` = first bar; option `constant` writes the median BPM only. Sections become
`Markers`. The 019 arrangement becomes clips referencing the same audio files with `playStart`
offsets (no re-render).

### D4 Audio format

WAV (PCM 24-bit, 44.1 kHz stereo) by default because the format reference only shows WAV and every
listed DAW reads it; WAV 16-bit and FLAC are Advanced options with a compatibility warning.

### D5 Mix mapping and the report

Volume (linear), pan (normalised 0..1, centre 0.5), mute, solo map to channel parameters; 017 EQ and
compressor map to the DAWproject built-in `Equalizer` and `Compressor`; reverb, practice EQ, amp
captures, the metronome, karaoke and loops are not mappable and are listed in `report.json`
(binding table: contracts/dawproject-mapping.md). Master volume maps to a master channel. AI parts
keep an "AI-generated" comment.

### D6 Validation is a gate

An export whose XML fails XSD validation fails with the first validation message (no invalid file
ever becomes `ready`). Unit tests validate golden projects; a property-style test validates random
arrangements; CI runs the same validator.

## Consequences

- Large files (24-bit WAV of every channel); the 007 free-disk guard uses the size estimate x 2 +
  1 GiB.
- Ableton users get the 007 stem pack instead (help says so).
- DAW-specific import behaviour (e.g. whether a DAW reads tempo automation or the built-in EQ) is
  observed in S-05 and documented, not guaranteed.

## Alternatives rejected

| Alternative | Why not |
|---|---|
| AAF / OMF | binary, legacy, no open validated schema, no OSI .NET writer |
| Ableton `.als` | proprietary undocumented format |
| Reaper `.rpp` only | one DAW; DAWproject reaches six and Reaper via a converter |
| MIDI file with markers + stems folder | no mix settings, no arrangement, no single file |
| Building the project in the worker | the API owns mixes, arrangement, sections and mapping (ADR-016 D3) |
| Bitwig's Java reference library | needs a JVM in the API image |
| Lanes in beats with warp markers | DAWs would time-stretch audio on tempo changes; seconds lanes keep it exact |
