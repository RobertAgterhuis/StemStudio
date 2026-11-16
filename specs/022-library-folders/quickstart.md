# Quickstart and validation guide: Library by Artist and Album

## Running StemStudio

Unchanged: `docker compose up` (or the non-Docker path of the README). No new environment variable
is required; `StemStudio__LibraryBackfillBatchSize` (default 500) tunes the start-up filing. For the
end-to-end suite use the fake stack (`STEMSTUDIO_SEPARATOR=fake docker compose up -d --build --wait`,
with `STEMSTUDIO_FAKE_ONLINE=found` or `not-found` for band info).

## Using it

1. Import a song (for example a Spotify link), wait for the separation and the band info.
2. On the track page choose **Save to library** next to `bass`, or in the mixer **Save to library >
   What I hear**. The confirmation shows **Saved to Artist > Album > Song**; follow the link.
3. Open **Library**: artist folders with pictures. Open an artist (album covers as a mosaic), an
   album, a song; use the breadcrumb to go back. Switch grid/list with the two buttons.
4. Type in the search box: results appear while you type (artists, albums, songs, items), matched
   parts highlighted. Try an instrument ("snare", "lead vocals", "strings"), a name without accents
   ("motorhead"), two words ("metallica bass"). Press Escape to return to the folder you were in.
5. Advanced level: **Edit library info** on a song folder (or on one item) to change artist, album
   or song name, or tick **Automatic**; **All items** shows the 007 flat list with filters; sort
   folders by name, year, recently added, size or items; **Only in this folder** limits the search.
6. Press `/` on a library page to jump to the search box.

## API (what the 011 agent and scripts call)

```http
GET    /api/library/folders?sort=name                     # artists (ETag "lib-<revision>")
GET    /api/library/folders/{folderId}?sort=year          # artist -> albums, album -> songs, song -> items
GET    /api/library/search?q=snare&limit=20               # grouped hits with paths and highlights
GET    /api/library/search?q=bass&within={folderId}
GET    /api/tracks/{trackId}/library-info
PUT    /api/tracks/{trackId}/library-info   {"artist":null,"album":"Ace of Spades","song":null}
DELETE /api/tracks/{trackId}/library-info
PUT    /api/assets/{assetId}/library-info   {"artist":null,"album":null,"song":"Jazz take"}
DELETE /api/assets/{assetId}/library-info
```

Every `Asset` in any response now has `placement` (`artist`/`album`/`song` with `folderId`, `name`,
`source`; `year`; `filed`).

## Validation checklist (integration wave)

- [ ] Saving each item kind (stem, detail stem, What I hear, selected channels, stem pack, range,
      sheet music, restyled take, own-lyrics song) files it at once (SC-001); confirmation shows the path.
- [ ] Normalisation: "Metallica"/"metallica "/"METALLICA", "Motörhead"/"Motorhead", "The Beatles"/
      "Beatles", "Guns N’ Roses"/"Guns N' Roses", featuring tails, "Various Artists" match (SC-002).
- [ ] A pre-022 database fixture with 5,000 items is filed within 10 s; `library-filing-pending`
      shows and disappears (SC-003).
- [ ] Folder art: cover, mosaic of 2/3/4 album covers, placeholder; broken image -> placeholder, no
      layout shift; `srcset` uses the cover on a 2x screen (SC-005).
- [ ] Search at 5,000 items: p95 <= 0.4 s from last key to results (Playwright timing), API p95 <=
      50 ms (backend perf test) (SC-004).
- [ ] One search case per instrument family and per detail group passes (SC-006).
- [ ] Library info on a track moves all its items; on an item only that one; Automatic restores; a
      later band info match moves automatic items only.
- [ ] Deleting the last item removes the song/album/artist folders; a stale deep link shows
      `library-folder-missing`.
- [ ] Beginner: folders, art, breadcrumb, search and item actions only; Advanced adds sort, All
      items, scope and library info (R7-R9).
- [ ] axe: no serious/critical findings on root, artist, album, song, search results and dialog in
      light and dark themes, comfortable and compact (SC-008); screen reader smoke test (NVDA):
      tile names, breadcrumb landmark, result count announced once.
- [ ] Existing 007/008 E2E specs pass on `/library?view=all` after the page-object migration.
- [ ] Agent evaluation LIB-01..LIB-05 pass; the library context offers exactly 32 tools.
- [ ] 013 backup + restore of a 022 library keeps library info and placements; restore of a pre-022
      archive files every item after start.
- [ ] Manuals: user manual "Library" chapter (folders, art, search, library info) updated and PDFs
      rebuilt (`node tools/docs/check.mjs` green).
