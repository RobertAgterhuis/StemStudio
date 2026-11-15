# StemStudio roadmap (post-MVP)

Owner request of 2026-11-15, ordered by the CTO so that each feature builds on the previous ones
and the whole list can be executed autonomously. Each item becomes its own Spec Kit feature
(`specs/NNN-*`) with spec, plan, tasks, a parallel agent wave and an integration wave.

Reference machine: Intel Core Ultra 9 285K, 127 GB RAM, NVIDIA GeForce RTX 5070 Ti (16 GB VRAM),
Windows 11 + Docker Desktop, Ollama installed.

| # | Feature | Why this position | Depends on |
|---|---|---|---|
| 002 | Detail separation (in progress) | Already running | 001 |
| 003 | **Design system and UX foundation**: own Radzen theme, modern distinctive UI, user journey for beginner / advanced / expert users, in-app help framework | Every later feature gets its UI in the new design; doing it later means redoing all screens | 002 |
| 004 | **GPU acceleration**: NVIDIA GPU in the worker (Demucs, detail models, later generative models), automatic CPU fallback | Generative features (009, 010) and the agent are impractical on CPU; also makes 001/002 separation much faster | 002 |
| 005 | **Song artwork and band info**: cover image in the mixer and as thumbnail in the track library; artist/band information from open data sources | Small, enriches the library; metadata is reused by lyrics (006) and the agent (011) | 003 |
| 006 | **Synced lyrics with karaoke ball**: download time-synced lyrics when available online; a banner at the bottom of the mixer with a bouncing ball following the playback position | Needs track metadata (005) for lookup and the 003 UI | 005 |
| 007 | **Export isolated channels**: save any stem, detail stem or a selection of channels (as currently mixed) as an audio file in a personal export library for later use | Needed by sheet music (008), style transfer (009) and the agent (011) as inputs/outputs | 003 |
| 008 | **Sheet music per instrument**: transcribe a stem to notation and download it as PDF (and MusicXML/MIDI) | Uses stems/exports (007); GPU optional | 007 |
| 009 | **Style transfer with a style library**: convert an imported song to another music style chosen from a library of styles | Generative model on the GPU (004); results land in the export library (007) | 004, 007 |
| 010 | **Own lyrics to song**: upload your own lyrics and generate music plus a sung voice | Same generative stack as 009 | 004, 009 |
| 011 | **AI composer agent in the UI**: give instructions in natural language that the agent executes as StemStudio actions (import, separate, mix, export, transcribe, restyle, generate). LLM backend: local Ollama by default, Claude API optionally when an API key is configured via an environment variable | Orchestrates the capabilities of all previous features, so it comes last | 005-010 |

Cross-cutting, every feature: in-app help entries (003 framework), user-journey level awareness
(003), licence records (constitution IV), contract versions, >= 90 % coverage, SIG limits, E2E.

## Owner decisions recorded

- 2026-11-15: AI agent LLM backend is configurable: Ollama (default, local, open models) and Claude
  API (optional, paid per use, key only via environment variable, off when no key is set). This
  needs a constitution amendment for an optional, opt-in paid external service (feature 011).
- 2026-11-15: Model weights exception (constitution 1.1.0, ADR-009) also applies to the generative
  and transcription models of 008-010.
