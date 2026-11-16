# Models and licences

Constitution principle IV allows only OSI-approved open-source code and libraries. Two
recorded exceptions apply; everything is listed in `docs/THIRD-PARTY-LICENSES.md`, which the
CTO consolidates from every hand-off.

## Exceptions

- **ADR-009, model weights.** Model weights whose licence is non-commercial or unstated are
  allowed for **local, personal, non-commercial use only**. They are downloaded on first use
  into the `model-cache` volume, verified with SHA-256 and never committed or baked into an
  image. Commercial use and redistributing images or volumes with weights are not covered.
  The owner extended this exception to the transcription and generative models of 008 to
  010. Code stays OSI-only: `python-audio-separator` is not installed because of its
  CC BY-NC dependency `diffq`; the MIT ZFTurbo MSST code is vendored instead.
- **ADR-011, GPU vendor runtime.** The free but proprietary NVIDIA CUDA runtime is allowed as a
  system dependency of the optional GPU images only.
- **Optional paid AI service (constitution 1.3.0, ADR-020).** Anthropic's Claude API for the
  agent: opt-in, off by default, key only from the environment, text only, per-run limits and
  visible usage, never required. Any other paid service needs its own amendment. The key rule is
  superseded by constitution 1.4.0 (ADR-035) once the claude-key follow-up ships: the key may then
  also be entered in Settings and stored encrypted.
- **GPU-only generative services (constitution 1.3.0, ADR-018).** The generator may require a
  GPU because generative models are impractical on a CPU.

## Models

| Model | Feature | Use | Licence | Size |
|---|---|---|---|---|
| Demucs `htdemucs_6s` (fallback `htdemucs`) | 001 | Six-stem separation | Weights unclear, likely CC BY-NC (ADR-009) | a few hundred MB |
| MDX23C DrumSep 6-stem | 002 | Drum pieces | No licence stated (ADR-009) | 438 MB |
| UVR MDX-Net KARA 2 (ONNX) | 002 | Lead and backing vocals | MIT per UVR README | 53 MB |
| MVSep Mega 53 BS-RoFormer | 002 | Instrument families | No licence stated (ADR-009) | 1.37 GB |
| Whisper large-v3-turbo | 006 | Lyrics alignment | MIT | 1.6 GB |
| basic-pitch ICASSP 2022 (ONNX) | 008 | Melodic transcription | Apache-2.0 | 0.2 MB |
| Piano CRNN note+pedal | 008 | Piano transcription | CC BY 4.0, attribution in UI | 172 MB |
| ACE-Step 1.5 (DiT, VAE, Qwen3 embedding, 5 Hz LM) | 009, 010 | Restyle and song generation | MIT; embedding Apache-2.0 | about 10 GB |
| SoulX-Singer SVC, RMVPE, whisper-base | 010 | Voice conversion | Apache-2.0 | about 3.3 GB |
| Qwen 3.5 9B / 4B via Ollama | 011 | Agent (local default / fallback) | Apache-2.0; pulled by the user | 6.6 GB / 3.4 GB |

Rejected for licence reasons, among others: YuE (CC BY-NC weights), VoiceStudio (AGPL,
CC BY-NC weights), YourMT3+ (GPL), ADTOF (non-commercial), FluentAssertions 8 and
JsonSchema.Net 9.

## Accepted copyleft dependencies

Used unmodified as libraries or system tools: FFmpeg (LGPL/GPL, Debian), lameenc (LGPL-3,
via Demucs; direct from 007), soxr (LGPL-2.1, via librosa), the GPL/LGPL transitive
dependencies of spotDL (mutagen, unidecode, text-unidecode, pykakasi, spotapi; metadata only)
and, from 008, verovio (LGPL-3.0) and cairosvg (LGPL-3.0+).

## Notable permissive components

yt-dlp (Unlicense), spotDL (MIT), Radzen Blazor (MIT), driver.js (MIT), Markdig (BSD-2),
Archivo font (OFL-1.1), Leipzig music font (OFL-1.1, 008), music21 (BSD-3, 008),
Microsoft.Extensions.AI and OllamaSharp (MIT, 011), Anthropic C# SDK (Apache-2.0, 011).

The documentation build (`tools/docs`) uses markdown-it (MIT) with its dependencies
(argparse PSF-2.0, entities BSD-2-Clause, linkify-it, mdurl, punycode.js and uc.micro MIT),
pdf-lib (MIT, with pako MIT/Zlib, tslib 0BSD and its MIT helper packages) and
playwright-core (Apache-2.0) with Playwright's Chromium.

## Attribution

StemStudio credits Ultimate Vocal Remover (UVR) and its developers, ZFTurbo's Music Source
Separation Training, the authors of the piano transcription model (CC BY 4.0), and shows
Wikipedia excerpts with CC BY-SA 4.0 attribution.
