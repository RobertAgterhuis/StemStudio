# ADR-025: Practice partner part generation with ACE-Step 1.5 base (feature 021, C9)

- **Status**: Proposed (architect, 2026-11-16; becomes Accepted when the CTO approves the 021 plan;
  the Phase 0 spike S-01 may amend D3 values).
- **Numbering**: feature-scoped (`021` + index) because features 012-020 write ADRs in parallel and
  sequential numbers would collide; the CTO may renumber at the merge.
- **Context documents**: `specs/021-partner-studio-bridge/{spec.md, research.md (R-01..R-05),
  data-model.md 2, 7, 8}`, ADR-009, ADR-016, ADR-018, ADR-019, constitution 1.3.1 principle IV.

## Context

MoSCoW C9 asks for a generated missing or alternative part (bass, drums, accompaniment, other
families) in the song's key and tempo, as an extra mixer strip. Every instrument family counts
equally. The 009/010 generator runs ACE-Step 1.5 **turbo**, which supports text2music, cover and
repaint only. Research R-01/R-02 found that ACE-Step 1.5 **base** supports `lego` (generate one
named track from audio context) for 12 track classes under MIT code and weights, and that no other
open model generates a single part conditioned on the song's audio for all families.

## Decisions

### D1 Model: `acestep-v15-base` with the `lego` task, in the existing generator service

- New generator job kind `partner` (generator-api 1.2.0). Input: the context stems (multipart, as
  restyle), the ACE-Step `track_name`, the caption, `bpm`, `keyscale`, `timesignature`, the range
  (`repainting_start/end`), 50 steps, CFG guidance, seeds, 1-2 takes.
- The base DiT is a second weights pin (MIT, Hugging Face revision + SHA-256 in
  THIRD-PARTY-LICENSES, downloaded on first partner job, about 4 GB). The generator keeps at most
  one DiT resident and swaps turbo/base on demand (the 009 idle-release rule still applies).
- If the lego output contains more than the requested track (S-01), the engine runs `extract` for
  that track on the result in the same lease hold.
- The XL base (4B) is not offered (VRAM, as ADR-018 D6).

### D2 Conditioning

Audio context = all audible standard stems (mode `add`) or all except the replaced one (mode
`alternative`), summed at unity by the generator. Tempo, time signature and key are model
parameters; the chord progression (016, compact text) and the section label (014) go into the
caption; the family has a fixed caption fragment; the optional user hint passes the ADR-018 D8
guard. The caption builder is pure domain code in the API (ADR-018 D7 pattern); the generator
receives only the final caption.

### D3 GPU needs (provisional) and job rules

`PARTNER_NEEDS = (LEGO_BASE 14,336 MiB, LEGO_BASE_LOWMEM 9,216 MiB lowMemory)` until S-01 measures
them (ADR-018 D4 rule: NVML peak above idle + 1 GiB, rounded up to 512 MiB). All 009 job rules
apply unchanged: cross-container file lease, free-memory profile choice, waiting, one OOM retry with
the low-memory profile, model download as a phase, cancel, retry. GPU-only (constitution IV
exception); without a ready generator the partner controls are disabled with the reason.

### D4 Fit check and conform in the worker (worker-api 1.12.0)

A CPU job `conform` in the worker's light lane measures timing offset (onset cross-correlation with
the beat grid, +-250 ms), drift, key and chord agreement, loudness, and (mode `write`) writes the
conformed 44.1 kHz FLAC (resampled, shifted, placed at the range start, padded/trimmed to the track
length, gain-matched within +-12 dB). Every take is checked automatically; only the take added to
the mixer is written. The worker already has librosa and soxr; the generator stays GPU-only and
model-level.

### D5 Parts as first-class channels

A part is a track-owned channel `part-<8 hex>` (backend `TrackParts`, engine 2.9.0 `addPart`),
top-level in the gate rule, saved in mixes, rendered by 007, exported to DAWproject. Mode
`alternative` mutes the replaced stem in the current mix. Parts are deleted with their track; the
library takes stay (007 rule).

### D6 Labels and ethics

AI-generated labels everywhere (strip, library, take details, FLAC tags `AI_GENERATED`,
`GENERATOR=ACE-Step 1.5 base (<revision>)`, `STEMSTUDIO_GENERATION`, `STEMSTUDIO_PART_FAMILY`,
DAWproject track comment). No artist imitation (guard). Backing vocals are wordless synthetic
voices (`vocal: synthetic`); lead vocals with words stay in 010; no voice profile is used.

## Consequences

- About 4 GB more weights for the generator on first partner use; a DiT swap (seconds) when a
  restyle/song job and a partner job alternate.
- Quality of lego is not published (R-01). SC-002/SC-003 are checked by the fit check and an owner
  listening test; limits are documented in help (constitution V).
- The worker gains one CPU job kind; the generator gains one kind; the backend gains parts.
- CI never runs ACE-Step; the fake generator returns a deterministic synthetic part (a click/bass
  tone pattern on the requested BPM, offset by a configurable number of ms so the conform path is
  exercised).

## Alternatives rejected

| Alternative | Why not |
|---|---|
| ACE-Step turbo (009/010 model) | no lego/extract/complete support (README DiT table) |
| ACE-Step XL base (4B) | >= 20 GB VRAM without offload; 16 GB reference card |
| MusicGen-Stem | only bass/drums/"other"; weights licence of the release not found; would need a fifth image (audiocraft torch pin) |
| MusicGen / JASCO / Stable Audio Open | not conditioned on the song's stems (full mixes or loops); NC or non-OSI community licences add nothing over ACE-Step |
| Text2music of a full arrangement, then extract the family | two full generations, the result does not follow the song's own groove and form |
| Symbolic accompaniment (Anticipatory Music Transformer) + SoundFont | CPU-capable but depends on MIDI transcription quality and sounds like General MIDI; backlog "CPU partner" |
| Rule-based pattern partner | not what C9 asks (AI session musician); backlog idea for a CPU fallback |
| Fit check in the generator or in .NET | generator is GPU-only and model-level; .NET has no DSP stack |
