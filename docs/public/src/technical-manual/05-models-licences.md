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
  agent: opt-in, off by default, text only, per-run limits and visible usage, never required. The
  key comes from `ANTHROPIC_API_KEY` or is entered in Settings and stored encrypted (constitution
  1.4.0, ADR-035; see "Claude key storage" in the installation chapter). Any other paid service
  needs its own amendment.
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
| Qwen 3.5 9B / 4B via Ollama | 011, 023 | Agent (local default / fallback); lyric assistant (9B only: the 4B model on the CPU gave no valid lyric output in the spike) | Apache-2.0; pulled by the user | 6.6 GB / 3.4 GB |

Rejected for licence reasons, among others: YuE (CC BY-NC weights), VoiceStudio (AGPL,
CC BY-NC weights), YourMT3+ (GPL), ADTOF (non-commercial), FluentAssertions 8 and
JsonSchema.Net 9.

## Dictionaries and word lists (023)

The lyric assistant's deterministic layer uses open data that is **vendored byte-identical in the
API image** (`src/StemStudio.Api/LyricAssist/Dictionaries/`, 7,740,587 bytes, about 7.4 MiB; the
tests check every SHA-256 and `PROVENANCE.md` records source, pin and licence). Nothing is
downloaded at run time and the files are not model weights.

| Data | Version (pin) | Use | Licence |
|---|---|---|---|
| OpenTaal Hunspell `nl` | 2.20.23, commit `1c22bc3d` (2022-11-29) | Dutch spelling and rhymes | BSD-3-Clause, chosen from "BSD-3-Clause and/or CC BY 3.0" |
| SCOWL Hunspell `en_US`, `en_GB-ise`, `en_GB-ize` | release `rel-2026.02.25`, size 60 | English spelling (a word is correct when the US or a British list accepts it) | SCOWL notice (MIT/HPND-style); affix files under Kuenning's Ispell BSD-style licence |
| CMU Pronouncing Dictionary | commit `74790861` (2025-10-24), 135,166 lines | English syllables and rhymes | BSD-2-Clause wording |
| WeCantSpell.Hunspell (NuGet, managed Hunspell port) | 7.0.1 | Spell checking over the lists above | MPL-1.1, chosen from MPL-1.1 / GPL-2.0 / LGPL-2.1; used unmodified |

The READMEs and licence files stay with the data. MPL-1.1 is file-level copyleft: it does not
affect StemStudio's MIT code because WeCantSpell's files are not modified. LanguageTool is not
used (ADR-037). The British list is the SCOWL build, not the LibreOffice en_GB dictionary.

## Accepted copyleft dependencies

Used unmodified as libraries or system tools: FFmpeg (LGPL/GPL, Debian), lameenc (LGPL-3,
via Demucs; direct from 007), soxr (LGPL-2.1, via librosa), the GPL/LGPL transitive
dependencies of spotDL (mutagen, unidecode, text-unidecode, pykakasi, spotapi; metadata only)
and, from 008, verovio (LGPL-3.0) and cairosvg (LGPL-3.0+). From 023, WeCantSpell.Hunspell 7.0.1 is used under MPL-1.1 (file-level copyleft, unmodified).

From 012, **SoundTouch 2.4.1 (LGPL-2.1)**, unmodified and pinned by SHA-256, is compiled with
StemStudio's ABI shim (LGPL-2.1-or-later) into the separate, replaceable
`js/soundtouch/soundtouch.wasm`. StemStudio's own engine and worklet stay MIT and contain no
SoundTouch code; every binary distribution ships the complete corresponding source at
`/third-party/soundtouch/index.html` (ADR-021). The emsdk 6.0.13 image (MIT/NCSA) is a
build tool only.

## Notable permissive components

yt-dlp (Unlicense), spotDL (MIT), Radzen Blazor (MIT), driver.js (MIT), Markdig (BSD-2),
Archivo font (OFL-1.1), Leipzig music font (OFL-1.1, 008), music21 (BSD-3, 008),
Microsoft.Extensions.AI and OllamaSharp (MIT, 011), Anthropic C# SDK (Apache-2.0, 011), the OpenTaal, SCOWL and CMU dictionaries (BSD-3-Clause, SCOWL notice and BSD-2-Clause, 023).

The documentation build (`tools/docs`) uses markdown-it (MIT) with its dependencies
(argparse PSF-2.0, entities BSD-2-Clause, linkify-it, mdurl, punycode.js and uc.micro MIT),
pdf-lib (MIT, with pako MIT/Zlib, tslib 0BSD and its MIT helper packages) and
playwright-core (Apache-2.0) with Playwright's Chromium.

## Attribution

StemStudio credits Ultimate Vocal Remover (UVR) and its developers, ZFTurbo's Music Source
Separation Training, the authors of the piano transcription model (CC BY 4.0), and shows
Wikipedia excerpts with CC BY-SA 4.0 attribution.
