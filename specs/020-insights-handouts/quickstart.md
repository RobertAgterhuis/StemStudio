# Quickstart and validation guide: Insights and Handouts

Builds on the 001-019 quickstarts. Contracts: [DELTA.md](DELTA.md) and [contracts/](contracts/).
Entities: [data-model.md](data-model.md). Measurements: [research.md](research.md). Design:
[ADR-029](../../docs/decisions/ADR-029-stem-quality-report-and-handouts.md).

## Running StemStudio

Unchanged (`docker compose up --build`, optional GPU override). The worker image now contains
WeasyPrint with Pango, HarfBuzz, fontconfig, the Archivo font and DejaVu Sans. The fake stack
(`STEMSTUDIO_SEPARATOR=fake`) uses the fake quality analyzer (fixed ratings per stem,
data-model 2.6) and, if G2 shipped, the fake re-separator; handouts are rendered for real.

The stem quality report is **switched off by default** (owner decision 2026-11-23, G1 failed): set
`STEMSTUDIO_QUALITY_REPORT_ENABLED=true` (api and web; `docker-compose.e2e.yml` does this for the test stacks)
before the steps below that use it. While it is off, `POST /api/tracks/<trackId>/quality` answers `503` and the track
page has no **Stem quality** card.

Check the worker: `curl -s http://localhost:8000/health` shows `insights.analyzerVersion`,
`insights.renderer.weasyprint`, `insights.fonts.archivo = true` and `lanes.insights`.

## Using it

1. Import and separate a song. The track page shows **Stem quality** within a minute: a rating
   per stem, a reason and up to two tips. Advanced: bleed and artefact levels; Expert: indices,
   reliability labels, analyzer version.
2. Follow a tip: **Split further** starts the detail separation; afterwards the report is made
   again and detail stems get their own ratings.
3. Existing track: **Check stem quality** (or **Check again** for an outdated report).
4. **Lesson handout** -> pick a family -> the toast links to the library item; open the PDF.
   Advanced: **Customise...** for ingredients, notation, transposition (Melodic instruments) and
   paper.
5. Write **Practice notes** per family on the track page; they appear in that family's handout.
6. Setlist page -> **Setlist handout** -> family.
7. (G2, deferred in Phase 0: skip) Tip **Re-separate with the high-quality vocal model** -> confirm -> stems are replaced;
   each rating shows "was ...".

API (what the 011 agent will call):

```bash
curl -s http://localhost:8081/api/tracks/<trackId>/quality
curl -s -X POST http://localhost:8081/api/tracks/<trackId>/quality
curl -s "http://localhost:8081/api/handouts/options?trackId=<trackId>&family=bass"
curl -s -X POST http://localhost:8081/api/assets/handouts -H "Content-Type: application/json" \
  -d '{"source":{"trackId":"<trackId>"},"family":"bass"}'
curl -s -X POST http://localhost:8081/api/assets/handouts -H "Content-Type: application/json" \
  -d '{"source":{"trackId":"<trackId>"},"family":"melodic","transposition":"b-flat","paper":"letter"}'
curl -s -X PUT http://localhost:8081/api/tracks/<trackId>/practice-notes/drums \
  -H "Content-Type: application/json" -d '{"text":"Count the fill in bar 16."}'
curl -s -X POST http://localhost:8081/api/assets/handouts -H "Content-Type: application/json" \
  -d '{"source":{"setlistId":"<setlistId>"},"family":"overview"}'
```

## Validation checklist (integration wave, CTO)

| # | Check | Expected |
|---|---|---|
| V1 | Fake stack: separate the fixture song | report ready <= 60 s; ratings vocals fair, drums good, bass good, guitar poor, piano not-present, other fair; disclaimer shown |
| V2 | Tips | other: "Split further..." with a working button; piano: "Mute this stem..."; guitar: "hard to isolate"; no tip mentions the GPU |
| V3 | Real stack, reference song (CPU) | analysis <= 30 s (log), ratings plausible by ear; record in research.md |
| V4 | Seven families on the per-family fixture song | seven ready handout items; each `handout.included` has the family defaults of data-model 6.2 that exist; footer on every page; last page notice |
| V5 | Melodic B-flat | chords transposed (+2 semitones), facts line "Part in B-flat ..." |
| V6 | Setlist of 3 (and 20 for timing) | running order, contents with page numbers, one bookmark per song; 20 songs <= 90 s |
| V7 | Security | a crafted document with `pdfPath: assets/../x.pdf` -> 400; an `<img src="http://...">` in practice notes appears as text, no request leaves the worker (network capture on the compose network) |
| V8 | Accessibility | axe scan clean on the quality panel and handout dialog; keyboard-only: menu, dialog, tip buttons; screen reader announces ratings with text |
| V9 | Manuals | user manual chapter "Stem quality and lesson handouts", technical manual (lane, env vars, Pango/fonts); `node tools/docs/check.mjs` passes |
| V10 (G2, deferred: skip) | Re-separation on the GPU and the CPU | timing vs estimate; ratings "was ..." shown; failure leaves stems unchanged |
