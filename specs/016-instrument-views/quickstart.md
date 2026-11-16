# Quickstart and validation guide: Instrument Views

Builds on the 001-015 quickstarts, in particular [008](../008-sheet-music/quickstart.md) and
[006](../006-synced-lyrics-karaoke/quickstart.md). Contracts: [contracts/DELTA.md](contracts/DELTA.md)
(full documents from Phase 0). Entities: [data-model.md](data-model.md). Evidence:
[research.md](research.md). Design: ADR-031, ADR-032, ADR-033.

## Running StemStudio

Unchanged (`docker compose up --build`, optional GPU override). First chord detection downloads
the BTC checkpoint (12 MB; the Full vocabulary another 12 MB) into the model cache; offline, the
fast method is used. The fake stack (`STEMSTUDIO_SEPARATOR=fake`) uses the fake chords (with a key
change at bar 9), the fake drum hits and the fake reference melody; tablature, parts and engraving
are real. Singing feedback needs a microphone or audio interface set up in Recording settings
(015) and works best with headphones.

## Using it (per family)

1. **Harmony (keys, guitar, singers)**: mixer -> the Chords lane and chords above the karaoke
   words appear once chords are ready; track page -> lyrics card shows the chord sheet. Key next
   to the tempo; Advanced: Full vocabulary, edit a chord, ChordPro download.
2. **Guitar**: "Practise as Guitar" -> capo mode with suggestions; sheet music for the guitar stem
   -> "notation + tab", tuning, capo.
3. **Bass**: sheet music for `bass` -> tab with 4/5/6-string tunings; "MIDI" on any channel.
4. **Drums**: "Drumless" -> drums silent, metronome with count-in, hit lanes; split the drums
   first if asked; drum notation follows playback.
5. **Vocals**: "Practise as Vocals" -> Singing feedback -> analyse the melody once -> start and sing;
   scores per line in the banner, summary at the end; "Score this take" on a recorded take.
6. **Strings, brass, woodwinds**: sheet music for the detail stem -> pick your instrument (e.g. Alto
   sax in E-flat) and clef -> play from the score view, which follows playback.

API (what the 011 agent and tests call):

```bash
curl -s http://localhost:8081/api/tracks/<trackId>/chords
curl -s -X POST http://localhost:8081/api/tracks/<trackId>/chords -H "Content-Type: application/json" -d '{"vocabulary":"full"}'
curl -s "http://localhost:8081/api/tracks/<trackId>/key?transpose=2"
curl -s "http://localhost:8081/api/tracks/<trackId>/chords/capo-suggestions?transpose=3"
curl -s "http://localhost:8081/api/tracks/<trackId>/chords/sheet?capo=3"
curl -OJ "http://localhost:8081/api/tracks/<trackId>/chords/chordpro?capo=3"
curl -s http://localhost:8081/api/tracks/<trackId>/drum-hits
curl -s -X POST http://localhost:8081/api/tracks/<trackId>/vocal-pitch
curl -s http://localhost:8081/api/instruments/catalogue
curl -s -X POST http://localhost:8081/api/assets/scores -H "Content-Type: application/json" \
  -d '{"source":{"trackId":"<trackId>","channel":"guitar"},"tablature":{"tuning":"guitar-drop-d","capo":0,"layout":"notation-and-tab"}}'
curl -s -X POST http://localhost:8081/api/assets/scores -H "Content-Type: application/json" \
  -d '{"source":{"trackId":"<trackId>","channel":"other.brass"},"part":{"instrument":"alto-sax-eb","clef":"auto"}}'
```

## Validation (V1..V14)

| # | What | How | Expected |
|---|---|---|---|
| V1 | All automated tests | `scripts/test-all.ps1` (or `.sh`); once on the reference machine `uv run pytest -m integration worker/tests/harmony worker/tests/analysis` | green; coverage gates; synthetic chords >= 85 % (SC-001), key >= 90 % (SC-010) |
| V2 | Chords auto (US1) | import the reference song; time from "separation completed" to chords `ready` | <= 30 s on the CPU; lane shows current/next chord |
| V3 | Banner and sheet (US1) | play a verse; open the track page lyrics card | chord symbols above the right words; same chords in the sheet |
| V4 | Transpose and edits (US1) | transpose +2 (012); edit one chord (Advanced); re-detect | all views transposed; re-detect asks before overwriting the edit |
| V5 | Guitar tab (US2) | guitar stem, drop D, notation + tab | PDF with a TAB staff, fret numbers playable; MusicXML string/fret per note |
| V6 | Bass tab and MIDI (US2) | bass, 5-string, tab only; "MIDI" on `drums.kick` | 5-line tab; MIDI channel 10 note 36 |
| V7 | Drum trainer (US3, SC-003) | split drums; open the trainer; time to drum hits `ready` | <= 30 s; lanes in time with the hits by ear |
| V8 | Drumless (US3) | expand the drum strips; click Drumless; click again | all drum meters at -60 dB, metronome + count-in; previous settings restored |
| V9 | Singing feedback (US4, SC-004) | calibrate in 015; sing a verse with headphones | pitch line follows the voice; line scores appear; no dropouts |
| V10 | Octave and transpose (US4) | sing an octave lower; then transpose -2 and sing along | similar scores in both cases |
| V11 | Parts (US5, SC-005) | `other.brass` as alto sax and horn in F; open in the score view while playing | written key A major / G major for concert C; the highlighted bar matches the concert part |
| V12 | Song key (US7, SC-010) | a song with a key change; play past it; transpose +2; Practise as orchestral with alto sax | key next to the tempo, current-region key changes at the right bar, transposed and written keys shown |
| V13 | Capo mode (US8, SC-011) | a song in E-flat (or transpose +3); Practise as Guitar; apply the top suggestion | "Capo 3" badge, C/G/Am/F shapes in the lane and sheet; tab dialog prefilled with capo 3; no capo for bass/keys |
| V14 | Every family (SC-006, SC-009) | "Practise as" each family; first-time user reaches the view | minus-one mix correct; view opens in <= 3 clicks |