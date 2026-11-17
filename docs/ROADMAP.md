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
| 016 | **Instrument views**: chords (keys/guitar/vocals), key detection, capo mode, tablature (guitar/bass), drum trainer, singing feedback, transposed parts for B-flat/E-flat/F instruments | S1, S4, S9, S10, S11, S12 | 008, 014, 015 |
| 017 | **Mixer effects and controllers**: EQ, compressor, reverb and a practice EQ per strip; MIDI controller mapping (Web MIDI) | C2, C3 | 012 |
| 018 | **Tablet and remote**: installable PWA with a music-stand layout, LAN remote control | C4 | 014 |
| 019 | **Arrangement editor**: cut, repeat and rearrange sections of stems into a practice version, export | C6 | 007, 014 |
| 021 | **Practice partner and studio bridge**: generate a missing part in key and tempo, amp/pedal simulation for guitar and bass, DAWproject export | C9, C10, C11 | 009, 015, 017, 019 |
| 020 | **Insights and handouts**: stem quality report with tips; one-click lesson PDF (lyrics, chords, sections, tempo, scores) | C7, C8 | 014, 016 |
| 022 | **Library by artist and album**: saved items filed automatically in Artist > Album > Song folders (in the app only) with folder art, live search over artist, album, song and stem/instrument, library info to correct names | S13 (owner request 2026-11-16) | 005, 007 (013/014 overlaps resolved in the 022 research) |
| 023 | **Lyric writing assistant**: on-demand help writing own lyrics (continue, alternatives, rhymes, syllables vs rhythm, titles) and spelling/grammar/flow checks in Dutch and English that always allow poetic licence and propose it; local dictionaries + the 011 agent backends | S13 | 010, 011 (+ claude-key) |

Cross-cutting, every feature: in-app help entries (004 framework), user-journey level awareness
(004), licence records (constitution IV), contract versions, >= 90 % coverage, SIG limits, E2E.

## Owner decisions recorded

- 2026-11-15: AI agent LLM backend is configurable: Ollama (default, local, open models) and Claude
  API (optional, paid per use, key only via environment variable, off when no key is set). This
  needs a constitution amendment for an optional, opt-in paid external service (feature 011).
- 2026-11-16: the Claude key can also be entered in Settings and stored encrypted in the database
  (environment variable still wins); constitution 1.4.0 ratified by the owner on 2026-11-16 (011
  follow-up claude-key; LAN access kept until 018, backups keep the ciphertext).
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
| 012 Practice tempo | 1.11.0 | - | 2.3.0 | 1.11.0 | agent tool catalogue 1.3.0 (integration: `practice` read-only in `get_mix` / `mixer_get_state`) |
| 013 Library care | 1.12.0 | 1.7.0 | - | 1.12.0 | backup-archive 1.0.0 |
| 014 Practice flow | 1.13.0 | 1.8.0 | 2.4.0 | 1.13.0 | web-interop 1.1.0 |
| 015 Play-along recording | 1.14.0 | - | 2.5.0 | 1.14.0 | render-vectors 1.1.0 |
| 016 Instrument views | 1.15.0 | 1.9.0 | 2.6.0 | 1.15.0 | |
| 017 Effects/controllers | 1.16.0 | - | 2.7.0 | 1.16.0 | |
| 018 Tablet/remote | 1.17.0 | - | - | 1.17.0 | remote-protocol 1.0.0 |
| 019 Arrangement editor | 1.18.0 | 1.10.0 | 2.8.0 | 1.18.0 | render-vectors 1.2.0 |
| 020 Insights/handouts | 1.19.0 | 1.11.0 | - | 1.19.0 | |
| 021 Partner/studio bridge | 1.20.0 | 1.12.0 | 2.9.0 | 1.20.0 | generator-api 1.2.0 |
| 011 follow-up claude-key (Claude key in Settings, encrypted; 2026-11-16) | 1.21.0 | - | - | 1.21.0 | agent tool catalogue unchanged (no key tool) |
| 022 Library by artist and album (owner request 2026-11-16) | 1.22.0 | - | - | 1.22.0 | agent tool catalogue 1.1.0 |
| 023 Lyric writing assistant | 1.23.0 | - | - | 1.23.0 | agent tool catalogue 1.2.0, agent eval set 1.1.0, lyric-assist-output 1.0.0 |

ADR numbers of the parallel 012-021 designs are renumbered by the CTO at merge, in feature order.

The 011 follow-up "claude-key" (owner decision 2026-11-16, design in
`specs/011-ai-composer-agent/followups/claude-key/`, ADR-035 accepted) takes the next free minors,
backend-api **1.21.0** and ui-test-ids **1.21.0**, as standalone deltas. Whatever is built first is
merged onto the full documents current at that time (as 013 does after 012); if the follow-up is
built before 012-021, its delta keeps the label 1.21.0 and the CTO merges the earlier-numbered
deltas onto it in feature order, so no allocated number is reused. Constitution 1.4.0 was
ratified by the owner on 2026-11-16.

**Full-document labels after an out-of-order merge (concrete rule, 012 Phase 0, 2026-11-16).** A
full document (backend-api, ui-test-ids) produced by merging a feature's delta carries **that
feature's allocated label**, also when a higher label is already merged: labels identify the set of
merged deltas, never an order. The backend document lists the merged labels in
`info.x-stemstudio-contains`. Consumers compare documents by containment (`check_superset.py`, the
contract tests), never by numeric order; the newest full document is the one of the feature merged
last. Sequence on `main`: 1.10.0 (011) -> 1.21.0 (011 claude-key) -> **1.11.0** (012 = 1.21.0 + the
012 delta) -> 1.12.0 (013 = 1.11.0 + 013) -> ... Audio-engine follows the same rule; 2.2.0 stays
unused (011 needed no engine change), so 012's engine document is 2.3.0.

Feature 022 (owner request 2026-11-16, design in `specs/022-library-folders/`, ADR-036 proposed)
takes the next free minors after the 011 follow-up "claude-key" (backend-api 1.21.0 and ui-test-ids
1.21.0, merged to `main` 2026-11-16): backend-api **1.22.0**, ui-test-ids **1.22.0** and
agent tool catalogue **1.1.0** (no other feature changes the catalogue), as standalone deltas merged
onto the newest full documents when 022 is built. Worker, generator and audio-engine contracts are
unchanged.

Build order: 003 -> 004 -> {005, 007} -> {006, 008} -> {009 -> 010} -> 011, then
{012, 013} -> {014, 015} -> 016 -> {017, 018, 019} -> {020, 021} (specs for 012-020 are written in
parallel now). Features in braces
are built in parallel; their contract edits are additive and merged by the CTO.

Feature 023 (lyric writing assistant, owner request 2026-11-16, design in
`specs/023-lyric-writing-assistant/`, ADR-037 accepted) reserves backend-api **1.23.0**, ui-test-ids
**1.23.0** and agent tool catalogue **1.2.0**. When 023 was designed, feature 022 (library by artist
and album, designed in parallel) had not allocated yet; 022 is expected to take **1.22.0** and catalogue
**1.1.0** (unused if 022 adds no tools; labels are never renumbered). 012-021 hold 1.11.0-1.20.0 and the
011 claude-key follow-up 1.21.0. The 023 delta is merged on the newest full document at build time.

Feature 012 (integration wave, 2026-11-16, CTO decision while the owner was unavailable) takes agent
tool catalogue **1.3.0**, the next free catalogue label after 1.1.0 (022) and 1.2.0 (023): additive,
the `get_mix` and `mixer_get_state` results show the practice tempo read-only (numbers only) and the
mix tools keep it unchanged. Tools that change the practice tempo remain a later catalogue minor.
022 and 023 merge their catalogue deltas onto 1.3.0 by containment (labels are never renumbered).
