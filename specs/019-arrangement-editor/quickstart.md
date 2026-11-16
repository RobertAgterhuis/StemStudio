# Quickstart and validation guide: Arrangement Editor

Builds on the 001-018 quickstarts. Contracts: [contracts/DELTA.md](contracts/DELTA.md) (full
documents from Phase 0). Entities and law: [data-model.md](data-model.md). Design:
[ADR-028](../../docs/decisions/ADR-028-arrangement-timeline-model.md).

## Running StemStudio

Unchanged: `docker compose up --build` (CPU) or with the GPU override. No new service, volume,
environment variable or dependency. The fake stack (`docker-compose` fake mode used by E2E)
serves a reference track with a reliable beat grid (bars every 2 s) and 014 sections; the
arrangement E2E relies on both.

## Using it

1. Set the level to **Advanced** (top bar). Open a completed song -> **Mixer** -> tab **Arrange**.
2. **New from sections** (or **New from whole song**). Each section becomes a region on bar lines.
3. Click the chorus region -> inspector -> **Repeat** 4. Press **Play**: the chorus plays four
   times without a gap; the lane shows the region being played.
4. Select the second verse -> untick **guitar** (or any channel: vocals, drums, bass, piano,
   other, detail stems when split). The guitar is silent only there.
5. Select bars on the song strip (click/drag, or Left/Right + Shift, or the "From bar" / "To bar"
   fields) -> **Add region**. Move it with **Move left/right** or Alt+Left/Right; **Split** at the
   playhead bar; **Duplicate**; **Remove**. **Undo**/**Redo** (Ctrl+Z / Ctrl+Shift+Z).
6. Name it, **Save**. **Save mix** in the mixer to remember it with the mix.
7. **Save to library** -> keep **Use arrangement "<name>"** ticked -> format -> **Save**. The item
   says "..., arranged (<name>)".
8. **Song order** in the arrangement selector switches back to the original song at any time.

Expert level adds the crossfade length (0-50 ms), snap mode (bars/free) and seconds fields.

## API (also what the 011 agent will call in a later catalogue minor)

```bash
# create from sections
curl -s -X POST http://localhost:8081/api/tracks/<trackId>/arrangements \
  -H "Content-Type: application/json" -d '{"name":"Chorus drill","seed":"sections"}'

# validate an unsaved document and get its timeline
curl -s -X POST http://localhost:8081/api/tracks/<trackId>/arrangements/resolve \
  -H "Content-Type: application/json" \
  -d '{"snap":"bars","crossfadeMs":10,"regions":[{"startSeconds":8.5,"endSeconds":16.5,"repeat":1,"mutes":[]},{"startSeconds":16.5,"endSeconds":24.5,"repeat":4,"mutes":["guitar"]}]}'

# save (optimistic concurrency)
curl -s -X PUT http://localhost:8081/api/arrangements/<id> -H "Content-Type: application/json" \
  -d '{"name":"Chorus drill","revision":1,"document":{...}}'

# export what I hear with the saved arrangement
curl -s -X POST http://localhost:8081/api/assets/audio-exports -H "Content-Type: application/json" \
  -d '{"trackId":"<trackId>","mode":"mix","channels":null,"mixId":"<mixId>","arrangementId":"<id>","format":{"format":"mp3"}}'
```

## Validation scenarios (map to spec acceptance scenarios)

| # | Scenario | Expected | Spec |
|---|---|---|---|
| A1 | New from sections on the reference song | one region per section, bar-snapped, arranged length = song length (+/- 1 ms) | US1 AS1 |
| A2 | Chorus x4, play across all boundaries with the metronome on | no gap/click, accents on beat 1, lane highlight follows | US1 AS2-4 |
| A3 | Song without sections | "New from sections" disabled with reason; whole song works | US1 AS5 |
| A4 | Split bars 1-16 at bar 9, compare a render before/after | bit-identical (contiguous boundary) | US2 AS2 |
| A5 | Move, duplicate, remove, then 5x undo, 2x redo | document states match the history | US2 AS3-4 |
| A6 | 65th region, repeat 17, 31-minute arrangement | refused, message names the limit | US2 AS5 |
| A7 | Mute vocals / drums / bass / guitar / piano / other / `drums.snare` in different regions | each silent only in its region, fades at edges; lyrics still shown with vocals muted | US3, SC-008 |
| A8 | Save, save mix, reload, open the mix | arrangement active and plays | US4 AS1-2 |
| A9 | Edit in two tabs, save both | second gets the conflict dialog | US4 AS4 |
| A10 | Delete an arrangement used by two mixes | confirmation names them; mixes play song order | US4 AS5 |
| A11 | Export WAV with the arrangement | length = arranged length (+/- 1 ms), contents "arranged (<name>)", audio matches vectors | US5, SC-004 |
| A12 | Export with a range (second chorus repeat) | file = that part of the arranged version | US5 AS2 |
| A13 | Keyboard only: build intro, chorus x4, outro without vocals | possible without a mouse, every change announced, axe 0 violations | US6, SC-007 |
| A14 | Level Beginner with a mix that has an arrangement | Arrange tab hidden, chip "Arranged: <name>", "Play song order" works | US7 AS1 |
| A15 | Level Expert | crossfade, snap, seconds fields shown | US7 AS3 |
| A16 | Re-analyse/reprocess a track after saving | arrangement marked "bar lines changed", sounds the same, "Snap to new bar lines" offered | FR-017 |
| A17 | Record (015) while arranged | record button disabled with the reason | FR-015 |

## Developer checks

```bash
python specs/019-arrangement-editor/contracts/generate_render_vectors.py --check
npx @redocly/cli@2.78.0 lint --config specs/019-arrangement-editor/contracts/redocly.yaml \
  specs/019-arrangement-editor/contracts/worker-api.delta.openapi.yaml \
  specs/019-arrangement-editor/contracts/backend-api.delta.openapi.yaml
# after Phase 0: dotnet build / dotnet test, worker pytest, engine npm test, e2e --grep @arrangement
```
