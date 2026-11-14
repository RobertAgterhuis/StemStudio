# ADR-001: Clarification decisions for the MVP specification

- **Status**: Accepted
- **Date**: 2026-11-14
- **Decider**: CTO (orchestrator), autonomous operating mode

## Context

`/speckit-clarify` normally asks a human up to five questions. The project runs in fully
autonomous mode, so the CTO answers the highest-impact questions and records them here and in
`specs/001-stemstudio-mvp/spec.md` (section *Clarifications*).

## Decisions

| # | Question | Decision | Rationale | Rejected alternatives |
|---|----------|----------|-----------|-----------------------|
| 1 | Stem storage/delivery format | FLAC 44.1 kHz stereo 16-bit | Lossless, no MP3 encoder delay/padding (which would break sample-accurate sync between stems), decodes natively in all modern browsers, about 50% of WAV size | MP3 (padding offsets, lossy), WAV (2x size), Opus (Safari gaps) |
| 2 | Cache identity | YouTube video ID / Spotify track ID / SHA-256 of upload | Stable, cheap to compute, independent of URL variations (`youtu.be` vs `watch?v=`) | Full URL (fragile), title match (ambiguous) |
| 3 | Jobs at restart | In-flight jobs are marked `failed` ("interrupted by restart"), retryable; completed results persist | Simple and honest; resuming Demucs mid-run is not supported by the model | Transparent resume (complex), silent loss |
| 4 | Level meter | Post-fader peak in dBFS, -60..0 dB, clip indicator at >= 0 dBFS | Matches what musicians expect from a DAW channel strip | RMS/LUFS (more costly, less intuitive for clipping) |
| 5 | Unsaved changes | No autosave; dirty indicator plus confirm on leave | Avoids overwriting a saved mix by accident; explicit save/save-as keeps the edit semantics clear | Autosave (surprising overwrites) |

## Consequences

- The worker converts Demucs output to FLAC; the audio engine decodes FLAC via `decodeAudioData`.
- The backend stores a `SourceKey` (`youtube:<id>`, `spotify:<id>`, `upload:<sha256>`) with a
  unique index.
- At start-up the backend and the worker reconcile non-terminal jobs to `failed`.
