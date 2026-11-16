# StemStudio roadmap (post-MVP)

Owner request of 2026-11-15, ordered by the CTO so that each feature builds on the previous ones
and the whole list can be executed autonomously. Each item becomes its own Spec Kit feature
(`specs/NNN-*`) with spec, plan, tasks, a parallel agent wave and an integration wave.

Reference machine: Intel Core Ultra 9 285K, 127 GB RAM, NVIDIA GeForce RTX 5070 Ti (16 GB VRAM),
Windows 11 + Docker Desktop, Ollama installed.

| # | Feature | Why this position | Depends on |
|---|---|---|---|
| 002 | Detail separation (in progress) | Already running | 001 |
| 003 | **GPU acceleration**: NVIDIA GPU in the worker (Demucs, detail models, later generative models), automatic CPU fallback | Owner request 2026-11-15: right after 002. Makes 001/002 separation much faster; generative features (009, 010) and the agent are impractical on CPU. Kept out of 002 so 002 is validated on the CPU baseline first | 002 |
| 004 | **Design system and UX foundation**: own Radzen theme, modern distinctive UI, user journey for beginner / advanced / expert users, in-app help framework | Every later feature gets its UI in the new design; doing it later means redoing all screens | 002 |
| 005 | **Song artwork and band info**: cover image in the mixer and as thumbnail in the track library; artist/band information from open data sources | Small, enriches the library; metadata is reused by lyrics (006) and the agent (011) | 004 |
| 006 | **Synced lyrics with karaoke ball**: download time-synced lyrics when available online; a banner at the bottom of the mixer with a bouncing ball following the playback position | Needs track metadata (005) for lookup and the 004 UI | 005 |
| 007 | **Export isolated channels**: save any stem, detail stem or a selection of channels (as currently mixed) as an audio file in a personal export library for later use | Needed by sheet music (008), style transfer (009) and the agent (011) as inputs/outputs | 004 |
| 008 | **Sheet music per instrument**: transcribe a stem to notation and download it as PDF (and MusicXML/MIDI) | Uses stems/exports (007); GPU optional | 007 |
| 009 | **Style transfer with a style library**: convert an imported song to another music style chosen from a library of styles | Generative model on the GPU (003); results land in the export library (007) | 003, 007 |
| 010 | **Own lyrics to song**: upload your own lyrics and generate music plus a sung voice | Same generative stack as 009 | 003, 009 |
| 011 | **AI composer agent in the UI**: give instructions in natural language that the agent executes as StemStudio actions (import, separate, mix, export, transcribe, restyle, generate). LLM backend: local Ollama by default, Claude API optionally when an API key is configured via an environment variable | Orchestrates the capabilities of all previous features, so it comes last | 005-010 |

### Backlog features from [MoSCoW.md](MoSCoW.md) (owner, 2026-11-16)

Every instrument counts equally (MoSCoW principle). The 011 agent gets tools for these features in
a later agent-tool-catalogue minor version.

| # | Feature | MoSCoW items | Depends on |
|---|---|---|---|
| 012 | **Practice tempo**: speed 50-120 % without pitch change and transpose +-12 semitones (SoundTouch, LGPL, WASM), speed trainer loop, count-in/pre-roll | M1, M2, M3 | 001, 006 |
| 013 | **Library care**: backup and restore of the whole library, storage overview and clean-up | M4, M5 | 001-010 |
| 014 | **Practice flow**: song sections with one-click loops, score follows playback, setlists, keyboard and foot-pedal control, practice log | S2, S5, S6, S7, S8 | 008, 012 |
| 015 | **Play-along recording**: record yourself over the mix with latency compensation, takes, listen back, export | S3 | 007, 012 |
| 016 | **Instrument views**: chords (keys/guitar/vocals), tablature (guitar/bass), drum trainer, singing feedback, transposed parts for B-flat/E-flat/F instruments | S1, S4, S9, S10 | 008, 014, 015 |

Cross-cutting, every feature: in-app help entries (004 framework), user-journey level awareness
(004), licence records (constitution IV), contract versions, >= 90 % coverage, SIG limits, E2E.

## Owner decisions recorded

- 2026-11-15: AI agent LLM backend is configurable: Ollama (default, local, open models) and Claude
  API (optional, paid per use, key only via environment variable, off when no key is set). This
  needs a constitution amendment for an optional, opt-in paid external service (feature 011).
- 2026-11-15: Model weights exception (constitution 1.1.0, ADR-009) also applies to the generative
  and transcription models of 008-010.

## Contract version allocation (CTO, 2026-11-15)

Fixed up front so features can be built in parallel without version collisions. Each feature
bumps only the contracts listed for it; a feature that needs no change in a contract keeps the
previous version.

| Feature | backend-api | worker-api | audio-engine | ui-test-ids | other |
|---|---|---|---|---|---|
| 003 GPU | 1.3.0 | 1.2.0 | 2.0.0 | 1.2.0 | |
| 004 Design/UX | - | - | - | 1.3.0 | help-content 1.0.0, web-interop 1.0.0 |
| 005 Artwork/band info | 1.4.0 | 1.3.0 | - | 1.4.0 | |
| 007 Export library | 1.5.0 | 1.4.0 | - | 1.5.0 | render-vectors 1.0.0 |
| 006 Lyrics/karaoke | 1.6.0 | 1.5.0 | 2.1.0 | 1.6.0 | |
| 008 Sheet music | 1.7.0 | 1.6.0 | - | 1.7.0 | |
| 009 Style transfer | 1.8.0 | - | - | 1.8.0 | generator-api 1.0.0 |
| 010 Lyrics to song | 1.9.0 | - | - | 1.9.0 | generator-api 1.1.0 |
| 011 AI agent | 1.10.0 | - | 2.2.0 if needed | 1.10.0 | agent tool catalogue 1.0.0 |
| 012 Practice tempo | 1.11.0 | - | 2.3.0 | 1.11.0 | |
| 013 Library care | 1.12.0 | 1.7.0 | - | 1.12.0 | backup-archive 1.0.0 |
| 014 Practice flow | 1.13.0 | 1.8.0 | 2.4.0 | 1.13.0 | |
| 015 Play-along recording | 1.14.0 | - | 2.5.0 | 1.14.0 | |
| 016 Instrument views | 1.15.0 | 1.9.0 | 2.6.0 | 1.15.0 | |

Build order: 003 -> 004 -> {005, 007} -> {006, 008} -> {009 -> 010} -> 011, then
{012, 013} -> {014, 015} -> 016 (specs for 012-016 are written in parallel now). Features in braces
are built in parallel; their contract edits are additive and merged by the CTO.
