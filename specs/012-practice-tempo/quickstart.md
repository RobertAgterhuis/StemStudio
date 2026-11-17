# Quickstart and validation guide: Practice Tempo

Builds on the 001-011 quickstarts. Contracts: [contracts/DELTA.md](contracts/DELTA.md). Entities:
[data-model.md](data-model.md). Decisions: [research.md](research.md), ADR-021.

## Running StemStudio

Unchanged start commands and no new environment variables. The web image now contains the
SoundTouch module (built in a Docker stage). On the non-Docker path build it once:

```bash
scripts/build-soundtouch.sh        # or scripts/build-soundtouch.ps1; needs Docker (emscripten/emsdk:6.0.13, pinned by digest in third_party/soundtouch/soundtouch.lock)
cd src/audio-engine && npm ci && npm run build   # writes audio-engine.js and time-stretch-processor.js
```

Without the module the app runs normally; the practice controls show "Time stretch unavailable".
Nothing leaves the machine. The SoundTouch licence and source are served at
`http://localhost:8080/third-party/soundtouch/` (web port of the compose file).

## Using it

Mixer -> **Practice tempo** panel: speed slider (50-120 %), transpose (-12..+12), "Original speed
and key"; **Count-in** switch; at Advanced the **Speed trainer** (needs an A-B loop). Shortcuts:
`-` / `=` speed -5/+5 %, `,` / `.` transpose -1/+1, `r` reset, `c` count-in, `t` trainer.

## Validation (P1..P14)

| # | What | How | Expected |
|---|---|---|---|
| P1 | Unit, contract and engine tests | `scripts/test-all.ps1` (or `.sh`) | green; >= 90 % line and branch per component (engine, web-interop, API, Web); `npm run test:wasm` green when the module was built |
| P2 | Contracts | `npx @redocly/cli@2.78.0 lint --config specs/012-practice-tempo/contracts/redocly.yaml specs/012-practice-tempo/contracts/backend-api.openapi.yaml`; `python -I specs/012-practice-tempo/contracts/check_superset.py` | valid (9 warnings, all inherited from 1.21.0); `superset: yes` against 1.21.0 (default) and 1.9.0 |
| P3 | Speed, fake stack (US1-1, SC-003) | `STEMSTUDIO_SEPARATOR=fake docker compose up -d --build --wait`; Playwright `practice-tempo.spec.ts` "speed" (Chromium, Firefox) | at 50 % `transport-position` advances 5.0 +- 0.2 s in 10 s of wall time; at 120 % 12.0 +- 0.24 s; `practice-panel` `data-stretch="active"` |
| P4 | Pitch kept and transposed (US1-2, US1-3, SC-002) | `practice-tempo.spec.ts` "pitch" with `tone_440hz_20s.flac` and the output probe | FFT peak 440 Hz +- 1 % at 50/70/100/120 %; ratio 2^(n/12) +- 1 % for n = -12, -5, +2, +12, also at 50 % and 120 % |
| P5 | Live change latency, no dropouts (FR-003, SC-001) | "live" case: 100 changes while playing (slider steps every 100 ms), probe records the time of the frequency change after a transpose step | each change heard <= 250 ms after the input; `practice-stretch-status` (Expert) `data-underruns="0"` |
| P6 | Neutral = before 012 (FR-007, SC-007) | "neutral" case: open a mix created via the API without `practice`; record network requests | controls at 100 % / 0; zero requests for `time-stretch-processor.js` and `soundtouch.wasm`; existing mixer specs pass unchanged |
| P7 | Karaoke and metronome follow (FR-005, SC-003) | `karaoke.spec.ts` landing case re-run at 50/75/120 % (`STEMSTUDIO_FAKE_LYRICS=word`); click probe with the metronome on at 50 % | landing errors <= 50 ms; click spacing = 60 / bpm / 0.5 +- 1 ms |
| P8 | Trainer (US2, SC-006) | `practice-trainer.spec.ts`: loop 2.0-6.0 s, start 60, step 10, target 80, passes 1 | `trainer-status` `data-speed` sequence 60, 70, 80, 80 and `data-state="target"`; pause keeps the pass, stop resets to pass 1; without a loop `trainer-needs-loop` |
| P9 | Count-in (US3, SC-005) | `practice-count-in.spec.ts` on the 120 BPM click fixture: 1 bar, play; then 2 bars + every pass on a 4 s loop at 50 % | `countin-indicator` `data-beats-remaining` 4..1, music starts after 2.0 +- 0.05 s (1 bar at 120 BPM); at 50 % 2 bars = 8.0 +- 0.05 s before every pass; without grid `countin-unavailable` |
| P10 | Persistence (US1-5, US2-5, FR-014) | save a mix with practice, reload the page; duplicate it | settings restored; duplicate has the same `practice` |
| P11 | Unavailable module (US1-6, FR-009) | "unavailable" case: route `**/soundtouch.wasm` to 404 (and a second run with `AudioWorkletNode` deleted by an init script) | `practice-unavailable` with the reason; speed and transpose disabled; playback and count-in work |
| P12 | Export note (US1-7, FR-015) | `export.spec.ts` addition: speed 75 %, open the export dialog, export "What I hear" | `export-practice-note` shown; exported file duration equals the original length (oracle) |
| P13 | Levels, a11y, visuals (FR-016, FR-017) | `practice-levels.spec.ts` (Beginner / Advanced / Expert storage states), axe suite on the mixer in both themes, visual baselines of the mixer, keyboard-only walk through the panel and the shortcuts | ids per level as in ui-test-ids 1.11.0; no axe violations; announcements: trainer steps and count-in start only |
| P14 | Every instrument family (SC-008) | `practice-families.spec.ts`: for each of vocals, guitar, bass, piano (keys), drums, other: solo that stem, set 80 % and -2 st, play 4 s | output probe shows signal, position rate 0.8 +- 2 %, no engine error |

Reference machine (CTO, integration wave):

| # | What | Expected |
|---|---|---|
| R1 | Reference song with 6 stems + all detail stems expanded (20 channels), 50 % and +12 st for 10 minutes in Chromium and Firefox | `data-underruns="0"`; stage process time <= 35 % of the quantum budget (diagnostics build of P0-2) |
| R2 | Listening check at 85 %, 70 %, 50 % and +-7 semitones (vocals, guitar, drums soloed) | artefacts as described in help `practice-sound-quality`; note in `docs/STATUS.md` |
| R3 | LGPL bundle | `GET /third-party/soundtouch/` lists COPYING.TXT, NOTICE.md, the source archive (SHA-256 matches `soundtouch.lock`), shim and build script; replacing `soundtouch.wasm` with a rebuild from that archive works |

## Developer commands

```bash
cd src/audio-engine && npm run test:coverage      # engine incl. stretch/ and practice/
cd src/audio-engine && npm run test:wasm          # real SoundTouch in Node (after build-soundtouch)
cd src/web-interop && npm run test:coverage       # practice shortcuts
dotnet test --filter "FullyQualifiedName~Practice"
cd e2e && npx playwright test practice-
```
