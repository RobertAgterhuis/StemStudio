# StemStudio feature backlog (MoSCoW)

**Status:** proposal by the CTO, 2026-11-16, for the owner to prioritise.
**Scope:** features *after* the current roadmap (001–011, see [ROADMAP.md](ROADMAP.md)).
Nothing here is planned or specified yet; each accepted item becomes its own Spec Kit feature
(`specs/NNN-*`) and follows the constitution (open source only, local and single-user, ≥ 90 %
coverage, no DRM circumvention, consent for voices).

StemStudio is a practice tool: you import a song, split it into stems, and play along. The
proposals below are ranked by how much they help someone **practise an instrument or voice with
real songs**.

**Principle: every instrument counts equally** (owner, 2026-11-16). StemStudio is for every
musician (vocals, guitar, bass, keys, drums, and the other instruments the detail models can
isolate). So:

- features that help *every* instrument come first;
- an instrument-specific view is never built alone: each family gets its own equivalent in the
  same feature (see the "instrument views" group below);
- acceptance tests cover at least one case per instrument family.

## Must have

These close the biggest gaps for a practice tool.

| # | Feature | Why | Notes / size |
|---|---|---|---|
| M1 | **Slow down without changing pitch** (50–120 % speed) and **transpose** (±12 semitones) for the whole mix, live in the mixer | The most used function in every practice app. The metronome already follows the tempo, but playback speed is fixed today. | Engine work: a time-stretch/pitch-shift AudioWorklet based on **SoundTouch (LGPL-2.1) compiled to WASM** (owner decision 2026-11-16; Rubber Band is GPL and would force the MIT project to GPL). Must stay sample-locked across stems. M–L |
| M2 | **Speed trainer**: an A-B loop that starts slow and speeds up by n % after each clean pass, up to the target tempo | Turns the loop into a real exercise. Depends on M1. | Mixer setting saved in the mix. S |
| M3 | **Count-in and pre-roll** before play and before every loop pass (1–2 bars, metronome sound) | You need time to get your hands back on the instrument. | Uses the existing beat grid and metronome. S |
| M4 | **Backup and restore** of the whole library (database, stems, mixes, lyrics, scores, styles, voice profiles) as one archive | Everything lives in Docker volumes today; losing a volume loses hours of GPU work. | API + worker job; restore checks versions. Voice profiles stay subject to consent rules. M |
| M5 | **Storage overview and clean-up**: disk use per track (stems, detail stems, exports, generations) and per model cache, with safe "remove derived files" actions | Detail stems and generations grow fast; the owner wants a clean machine. | S–M |

## Should have

Important, with clear value, but the product works without them.

| # | Feature | Why | Notes / size |
|---|---|---|---|
| S1 | **Chord detection with a synced chord chart** (chord symbols above the lyrics and in a "chords" lane in the mixer) | Guitarists and pianists practise from chords more often than from sheet music. | Open-source chord recognition model (check licences per ADR-009 rules); reuse the 006 banner timing. M |
| S2 | **Song sections** (intro / verse / chorus / bridge) detected automatically, editable, with one-click loops | Faster than dragging A-B markers; sections feed M2 and the AI composer (011). | Structure analysis on the beat grid (librosa-based or an open model). M |
| S3 | **Play-along recording**: record yourself (microphone or audio interface) over the mix with latency compensation, then listen back or export | Hearing yourself against the original is how you improve. | Web Audio + MediaRecorder; latency calibration screen; stored as a take next to the track. M–L |
| S4 | **Guitar and bass tablature** next to standard notation (extends 008), plus **MIDI download** per instrument where it is missing | Many players do not read notation. | Fret mapping from the 008 transcription; tuning option. M |
| S5 | **Score follows playback**: the 008 sheet music scrolls and highlights the current bar while the song plays | Links reading and listening, and works with M1/M2. | Bar times from the beat grid; Verovio SVG highlighting. M |
| S6 | **Setlists**: ordered lists of tracks or mixes for a rehearsal or gig, with notes per song and one-click "next" | Bands and teachers practise in sets. | Backend entity + UI; export as PDF with lyrics/chords. S–M |
| S7 | **Keyboard and foot-pedal control**: complete shortcuts (play, loop, speed ±, next section) and USB/Bluetooth page-turner pedals, which act as keyboards | Hands are on the instrument. | Mostly frontend; shortcut overview in help. S |
| S8 | **Practice log**: time practised per song and section, tempo reached in the speed trainer, simple progress chart | Motivation and insight for beginners (004 journeys). | Local only, no tracking outside the machine. S–M |
| S9 | **Singing feedback**: pitch line of your voice against the lead vocal, per line score in the karaoke banner | Fun and useful for singers; builds on 006 and S3. | Real-time pitch tracking (e.g. an open-source pitch model or YIN in an AudioWorklet). M |
| S10 | **Drum trainer**: drum notation with hit lanes (kick/snare/hi-hat from 002 detail stems) and a "drumless" practice mix in one click | Drummers are a large group of stem-practice users. | Builds on 002 + 008. M |

## Could have

Nice ideas; pick them when there is room.

| # | Feature | Why | Notes / size |
|---|---|---|---|
| C2 | **Per-channel effects**: EQ, compressor and reverb per strip, plus a "practice EQ" preset that cuts a band's frequency range | Makes isolated stems sound better; helps hear a part. | Web Audio nodes; saved in mixes. M |
| C3 | **MIDI controller support** (Web MIDI): faders, mute/solo and transport on a hardware controller | Hands-on mixing. | Mapping screen; Chromium only. S–M |
| C4 | **Tablet layout and installable app (PWA)** for the music stand, controlled over the home network | Practising usually happens away from the desk. | Responsive work exists (320 px); add PWA manifest and a LAN-safe "remote" mode. M |
| C6 | **Remix and arrangement editor**: cut, repeat and rearrange sections of stems into a new practice version (e.g. "chorus 4×") | Practice versions and band arrangements. | Non-destructive edit list per mix; export via 007. L |
| C7 | **Stem quality report**: a bleed and artefact score per stem with a tip ("try the detail model", "use the GPU model") | Explains why a stem sounds bad. | Uses existing separation metrics. S–M |
| C8 | **Teacher handouts**: one-click PDF with lyrics, chords, sections, tempo and score per instrument for a lesson | Re-uses 006/008/S1/S2 output. | Builds on the docs PDF pipeline. S |

## Won't have (this time)

Explicitly out of scope, to protect the constitution and keep the product focused.

| # | Feature | Reason |
|---|---|---|
| W1 | Publishing or uploading to streaming services, or any commercial distribution of generated songs | Personal, non-commercial practice tool; model weight licences (ADR-009) do not cover it. |
| W2 | Multi-user cloud hosting, accounts and sharing over the internet | Local single-user design; would need authentication, multi-tenancy and new licence checks. |
| W3 | Cloning the voice of a real person without their recorded consent, or imitating a named artist | Consent and ethics rules of 010 stay non-negotiable. |
| W4 | Downloading from sources that need DRM circumvention (Spotify audio, Apple Music, Tidal) | Constitution: DRM is never circumvented; Spotify stays metadata plus a YouTube match. |
| W5 | Features that need a paid service or paid model to work | Open-source-only principle; the optional Claude backend of 011 is the single, opt-in exception. |
| W6 | A native desktop or mobile app (Electron, MAUI) | The Blazor web app plus C4 covers the use case at a fraction of the cost. |
| W7 | Dutch user interface (localisation) | Owner decision 2026-11-16: the UI, help and manuals stay English only. |

## Suggested order

1. **M1 → M2 → M3** as one "practice tempo" feature: the largest daily win, for every instrument.
2. **M4 + M5** together as "library care".
3. **S2 + S5 + S7 + S8** (sections, score follows playback, hands-free control, practice log):
   useful for all instruments.
4. **S3** (play-along recording): the base for feedback on every instrument.
5. **Instrument views**, built as one feature so no family waits for another:
   - harmony (keys, guitar, and singers who accompany themselves): **S1** chords;
   - guitar and bass: **S4** tablature;
   - drums: **S10** drum trainer;
   - vocals: **S9** singing feedback;
   - other instruments (strings, brass, woodwinds): playing from the 008 score with S5, plus a
     transposed part for B♭/E♭/F instruments in the score export.
6. Could items as filler.

## Decisions

- 2026-11-16: no Dutch UI (W7).
- 2026-11-16: every instrument counts equally; C1 and C5 moved to Should (S9, S10) so vocals and drums
  get their own views next to chords and tablature.
- 2026-11-16: M1 uses SoundTouch (LGPL-2.1), not Rubber Band (GPL), so StemStudio stays MIT. Any change to SoundTouch itself stays LGPL and is published with its source; the licence goes into THIRD-PARTY-LICENSES.
