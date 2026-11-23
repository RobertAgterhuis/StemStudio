# Hand-off: backend / 020 US4 setlist handouts (T081, T082)

| Task pair | Red | Green |
|---|---|---|
| T081/T082 | 4ef522e3 | 97bc5e88 |

- `createHandout` with `source.setlistId` and `getHandoutOptions?setlistId=` (query name unchanged): 404 unknown setlist,
  409 `setlist-empty`, 409 `too-many-songs` above `StemStudio:MaxSetlistHandoutSongs` (default 50), entries in 014
  running order (positions 1-based), setlist and entry notes (`note` "" -> null), a song that is not completed is
  included with what it has (FR-010; `track-not-ready` only for song handouts), entries whose track is gone are skipped.
- Document: `kind setlist`, title = setlist name, subtitle "Setlist - N songs", `SetlistBlock` (key from the chords in
  concert pitch, rounded tempo, length, notes); `setlist-notes` printed per song that has entry notes.
- Item: `trackId` null, `trackTitle` = setlist name, recipe with `setlistName` snapshot.
- Ingredient availability over a setlist = best song; each song prints only what it has. Setlist options carry no create
  actions (they are per song).
- Bookmarks/page layout (one bookmark per song with the track title, "Page n of N") are the worker's (audio-ingest).

## Final verification (all backend checkpoints of this lane; Domain/Persistence on 52be7bf8, Api on c4fee551)

| Component | Tests | Line | Branch |
|---|---|---|---|
| StemStudio.Domain | 4331 passed | 98.65 % | 94.12 % |
| StemStudio.Persistence | 185 passed | 99.5 % | 95.45 % |
| StemStudio.Api | 3683 passed, 4 skipped | 98.32 % | 90.81 % |
| (020 Api files only: Insights, Handouts, worker insights client) | | 98.9 % | 89.0 % |

- `dotnet build StemStudio.slnx -warnaserror`: 0 warnings, 0 errors; `dotnet format StemStudio.slnx --verify-no-changes`: clean.
- `tools/quality/run_all.py` on Domain, Persistence, Api and the 020 test folders: PASS, 0 errors (jscpd ran: 14 clones,
  none in production code; the 020 test clones are setup lines of the persistence and endpoint tests).
- Earlier full runs showed two failures, both handled: the 016 hook test (one-time edit, 046982e1) and one
  `LyricAssistEdgeTests.Stream_ClosesWhenTheAssistIsGone` timing flake (SSE, 9 s under load; passed in the next two runs).

## Integration list (not edited by this lane)

- Settings for the technical manual: `StemStudio:AutoQualityReport` (default true), `StemStudio:MaxSetlistHandoutSongs` (50).
- Copy-first migration test of AddQualityReport/AddPracticeNotes on the owner's database at deploy.
- No new dependencies, no licence rows. No STATUS/ROADMAP/decision-log edits.
