# Quickstart and validation guide: Practice Flow

Builds on the 001-013 quickstarts (in particular [008](../008-sheet-music/quickstart.md) for sheet
music). Contracts: [contracts/DELTA.md](contracts/DELTA.md) (full documents from Phase 0 part 2).
Entities: [data-model.md](data-model.md). Measurements: [research.md](research.md). Design:
[ADR-023](../../docs/decisions/ADR-023-practice-flow.md).

## Running StemStudio

Unchanged (`docker compose up --build`). Nothing is downloaded for 014: section detection is
signal processing on the CPU, score views use the installed Verovio, setlist PDFs use ReportLab and
the DejaVu font in the worker image. The fake stack (`STEMSTUDIO_SEPARATOR=fake`) uses the fake
structure analyser (fixed 8-bar sections, data-model 2.4); score views and PDFs are real.

## Using it

1. **Sections**: import a song -> when separation finishes, open the mixer: the section lane shows
   Intro, Verse 1, Chorus 1, ... Click a section to loop it; N / Shift+N for next/previous;
   Backspace to the section start. Advanced: **Edit sections** (split at the playhead, merge,
   rename, change kind, move boundaries) -> **Done**.
2. **Speed trainer** (012): loop a section, switch the trainer on (T): the section is the region.
3. **Score follow**: make sheet music for a stem (008) -> mixer -> **Score** (F) -> pick the part ->
   play: the current bar is highlighted and the page scrolls. Click a bar to jump there.
4. **Setlists**: rail -> **Setlists** -> **New setlist** -> **Add songs** (optionally a mix and a
   note per song) -> **Play** (setlist bar: previous/next, Ctrl/Cmd+Right) -> **PDF** (lyrics,
   sections, notes).
5. **Shortcuts and pedals**: Ctrl/Cmd+/ shows every shortcut. Preferences -> **Single-key
   shortcuts** on/off (accessibility). Advanced: **Shortcut settings** -> change keys, or
   **Pedals** -> preset or **Learn** (press the pedal) -> **Pedal mode** on.
6. **Practice log**: mixer -> **Practice** panel (today, week, per section, tempo reached); rail ->
   **Practice** for all songs; switch off or delete there. Nothing leaves the computer.

## API (what the 011 agent and scripts call)

```bash
curl -s http://localhost:8081/api/tracks/<trackId>/sections
curl -s -X POST http://localhost:8081/api/tracks/<trackId>/sections/detect -H "Content-Type: application/json" -d '{"overwrite":true}'
curl -s http://localhost:8081/api/tracks/<trackId>/scores
curl -s http://localhost:8081/api/assets/<assetId>/score-view
curl -s -X POST http://localhost:8081/api/setlists -H "Content-Type: application/json" -d '{"name":"Friday rehearsal"}'
curl -s -X PUT http://localhost:8081/api/setlists/<setlistId>/entries -H "Content-Type: application/json" \
  -d '{"entries":[{"trackId":"<trackId>","note":"Capo 2"}]}'
curl -s -o set.pdf "http://localhost:8081/api/setlists/<setlistId>/pdf?lyrics=true"
curl -s "http://localhost:8081/api/practice/summary?trackId=<trackId>&days=30&utcOffsetMinutes=60"
curl -s -X PUT http://localhost:8081/api/practice/settings -H "Content-Type: application/json" -d '{"enabled":false}'
curl -s -X DELETE "http://localhost:8081/api/practice/sessions"
```

## Validation checklist (integration wave)

| Check | How | Pass |
|---|---|---|
| Sections appear | fake stack, import fixture -> mixer within 30 s | lane `data-status="ready"`, 8 sections |
| One-click loop | click `section-<id>` | `loop-toggle` on, loop = section bounds, `data-looped="true"` |
| Edit + concurrency | edit in two tabs | second save shows `section-editor-error` "changed in another window" |
| Real analyser (SC-002) | `uv run pytest worker/tests/analysis/structure -k synthetic` | recall >= 0.80, precision >= 0.75, solos 5/6 |
| Score follow (SC-003) | E2E per family (vocals, bass, guitar, piano, drums) at 50/100/120 % | `score-view[data-bar-number]` = expected bar |
| Frame cost (SC-004) | Chromium performance trace, 5-page score + karaoke | <= 2 ms per frame |
| Setlist PDF (SC-008) | 20-song setlist with lyrics | < 10 s, pypdf finds titles and lyrics in order |
| Keyboard (SC-006) | E2E every FR-020 command; axe-core on mixer, setlists, practice, shortcut settings | no serious/critical |
| Screen reader | NVDA browse mode on the mixer (manual, CTO) | H/B/K navigation unaffected |
| Pedals | E2E sends PageDown/PageUp with pedal mode on, focus on a mute button | play toggles, section restart |
| Practice log (SC-007, SC-009) | fake clock unit tests; E2E request log | totals +-2 s per 10 min; no request to another host |
| Off and delete | switch off -> play -> summary unchanged; delete -> zeros | 409 `practice-log-off` in the network log |
