# Hand-off: audio-ingest / 020 US4 (T083, T084)

Lane WORKER-IO, branch `020-lane-worker-io`.

## What was built

`handouts/templates/setlist.html.j2` and its use in `handout.html.j2`: cover (setlist name,
subtitle, "<family> handout, made on <date>", setlist notes with line breaks), running order
table (position, song, artist, key, tempo, length, notes), contents with page numbers
(`target-counter()` with dot leaders), then every song on a new page with its own entry notes
("Setlist notes") and the song layout of US3, "About this handout" once at the end. Bookmarks:
setlist name, one top-level entry per song, "About this handout".

## Commits

| Task | Red | Green |
|---|---|---|
| T083 / T084 | 9d968125 | f6cf14fc |

## Tests

`tests/handouts/test_setlist.py`: cover view and escaped entry notes (everywhere); with
WeasyPrint: cover texts, each song starts a page, contents page numbers equal the real pages,
top-level outline `[Friday set, Song 1, Song 2, Song 3, About this handout]`, 20-song render
within the 90 s budget (2.0 s without notation in the test). Coverage of `handouts` stays 100 %
line and branch (Linux image).

## Deviations

None.
