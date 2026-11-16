# Quickstart: Play-along Recording

How to try and verify feature 015 once it is implemented. Prerequisites: the stack from the
README (`docker compose up`, web on http://localhost:8080, API on http://localhost:8081), a
completed track, Chrome or Edge, headphones; for exact timing a cable from an output of your
audio interface to one of its inputs.

## 1. First recording (Beginner)

1. Open a track's mixer and click **Record** in the toolbar (`mixer-record-panel-toggle`).
2. Read the note "Recordings stay on this computer" and click **Allow microphone**; accept the
   browser prompt.
3. Choose your input (USB microphone or interface), check that the input meter moves while you
   play or sing and that it does not show clip.
4. Choose what you record (Vocals, Guitar, Bass, Keys, Drums, Other) and keep "Mute the original
   part while recording" on.
5. Press **Record** (or `R`): one bar of count-in, then play along; press **Stop** (or `R`).
6. The take appears in the Takes panel and plays at once in the **Take** strip; "Saved" appears
   when the server has stored it.

## 2. Calibrate latency

1. In the recording panel click **Calibrate latency**.
2. Connect output to input with a cable (best), or hold a headphone cup against the microphone.
3. Click **Start**; within about 7 s the result shows e.g. "25.0 ms (8 of 8 clicks, spread 0.1
   ms)". It is saved for this input.
4. If it fails, follow the tip or choose **Enter manually**.

## 3. Loop takes and compare

1. Set an A-B loop around a hard passage, enable the loop, press **Record**.
2. Every pass becomes a take (`Pass 1`, `Pass 2`, ...); stop when done.
3. Select a pass, then use **My take / Original / Both** (or `C`) while it plays.
4. Star the best pass and choose **Delete the other passes**.

## 4. Export

- Take row -> **Export** -> **Take only** (trimmed to the take) or **Take + mix** (what you
  hear), format FLAC/WAV/MP3; the item appears in the Library (`/library`).

## 5. Verification commands (developers)

```powershell
# engine (Vitest, coverage >= 90 %)
cd src/audio-engine; npm ci; npm run lint; npm run typecheck; npm run test:coverage
# backend + web
dotnet build StemStudio.slnx -warnaserror
dotnet test StemStudio.slnx --settings tests/coverlet.runsettings
# worker regression (24-bit WAV -> FLAC bit-exact)
cd worker; uv run pytest tests/render/test_take_conversion.py
# contracts
npx @redocly/cli@2.78.0 lint --config specs/015-play-along-recording/contracts/redocly.yaml specs/015-play-along-recording/contracts/recording-api.openapi.yaml
# E2E (Chromium full build with the fake capture device; see TEST-STRATEGY section 19)
cd e2e; npx playwright install chromium; npm run test:recording
```

## 6. Expected limits (documented in help)

- Recording works at 100 % speed without transpose; playback of takes works at any setting.
- Browsers expose at most two input channels of an interface; set the input gain on the
  interface (StemStudio cannot change it).
- Chrome and Edge are supported for recording; Firefox may refuse devices that do not run at
  44.1 kHz.
- Monitoring through the browser is late by the round-trip latency; use direct monitoring on
  your interface or amp where possible.
