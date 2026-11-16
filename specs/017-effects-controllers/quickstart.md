# Quickstart and validation guide: Mixer Effects and Controllers (017)

Builds on the 001-016 quickstarts. Contracts: [DELTA.md](DELTA.md). Entities:
[data-model.md](data-model.md). Design: [ADR-026](../../docs/decisions/ADR-026-mixer-effects-and-midi-controllers.md).

## Running StemStudio

Unchanged: `docker compose up --build`. New settings (defaults fit; environment variables only):

| Setting | Default | Meaning |
|---|---|---|
| `StemStudio__MaxPrerenderBytes` | 320000000 | largest accepted pre-render upload |
| `StemStudio__PrerenderTtlMinutes` | 60 | unused pre-renders are deleted after this |

Use Chrome or Edge on `http://localhost:...` for MIDI (a secure context). From another machine
over plain http, MIDI is unavailable (the app says why); effects work everywhere.

## Using it

1. **Practice EQ (any level)**: mixer -> a strip's **Practice EQ** -> *Focus on <family>* or
   *Make room for <family>* -> *Off* to remove.
2. **Effects (Advanced)**: strip **FX** -> EQ / compressor / reverb send, **Presets**, **Bypass**;
   master section: **Reverb** kind and return, **Bypass all** to compare.
3. **Save** the mix as usual; reload: effects are back. Old mixes open without effects.
4. **Export**: mixer -> **Save to library** -> "What I hear" -> "Include effects (as you hear
   them)" stays on -> **Save**. Progress shows the render, then the library item says "with
   effects".
5. **Controller (Advanced, Chrome/Edge)**: **Controllers** (navigation) -> **Connect controller**
   -> allow MIDI. In the mixer turn on **MIDI learn**, click a fader, move a hardware fader;
   repeat for mute, play, speed. Bank buttons move the slots to the next 8 strips.

## API checks

```bash
# a mix with a practice EQ on bass and a reverb send on vocals
curl -s -X PUT http://localhost:8081/api/mixes/<mixId> -H "Content-Type: application/json" -d @mix-with-effects.json

# controller profiles
curl -s http://localhost:8081/api/controller-profiles

# pre-render (normally the browser does this) and an export with effects
curl -s -X POST http://localhost:8081/api/tracks/<trackId>/prerenders -H "Content-Type: audio/wav" --data-binary @render.wav
curl -s -X POST http://localhost:8081/api/assets/audio-exports -H "Content-Type: application/json" \
  -d '{"trackId":"<trackId>","mode":"mix","channels":null,"mixId":"<mixId>","effects":"apply","prerenderId":"<prerenderId>","format":{"format":"flac","bitDepth":24}}'
```

## Validation (maps to the success criteria)

| Check | How | Expected |
|---|---|---|
| SC-001 practice EQ per family | `npm test -- presets` (engine) and `npx playwright test effects-practice-eq` | every family's test bands >= 12 dB lower |
| SC-002 old mixes | `dotnet test --filter ExportPlannerLegacy` | identical plans/bytes to 007 |
| SC-003 export parity | `npx playwright test effects-export` | decoded FLAC-24 within 1 LSB of a second offline render |
| SC-004 MIDI latency | `npx playwright test midi-control` | p95 < 50 ms |
| SC-005 CPU budget | `npx playwright test effects-budget`; 5-minute manual playback of the worst-case mix on the reference machine (no underruns in `chrome://media-internals`) | <= 10 s for 30 s offline; 0 dropouts |
| SC-006 bind in a minute | qa walkthrough | < 1 min |
| SC-007 no MIDI support | `npx playwright test midi-fallback` (also Firefox project) | 0 prompts, 0 console errors, reason shown |
| SC-008 a11y | `npx playwright test e2e/a11y/effects e2e/a11y/controllers` | no serious/critical axe findings |
| Contracts | `npx @redocly/cli@2.78.0 lint --config specs/017-effects-controllers/contracts/redocly.yaml specs/017-effects-controllers/contracts/backend-api.openapi.yaml` | valid |
| Coverage | `scripts/test-all.ps1` | >= 90 % line and branch per component |

## Troubleshooting

- "Your browser does not support MIDI controllers": use Chrome or Edge.
- "MIDI is blocked for this site": site settings -> MIDI devices -> Allow, then **Try again**.
- Export with effects is slow or the tab runs out of memory on a very long song: export the loop
  region, or export without effects.
- An export made by the AI agent says "without effects": the agent cannot render effects; export
  from the mixer instead.
