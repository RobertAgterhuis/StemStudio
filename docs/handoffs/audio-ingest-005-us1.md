# Hand-off: audio-ingest / 005 us1 (005:T022-T023)

The source artwork and the lookup names are now handed from the import source to the pipeline
(FR-001, research R-02/R-04). Foundational work is in
[audio-ingest-005-foundational.md](audio-ingest-005-foundational.md).

## What was built

- `metadata/titles.py`:
  - `clean_title(title)` removes the R-04 suffixes, bracketed or after ` - `: official (music /
    lyric) video, official audio, lyric(s) video, lyrics, audio, HD, HQ, 4K, remaster(ed)
    [year]. It also removes `feat.`/`ft.`/`featuring` parts. "Live" is kept. A title that would
    become empty is kept as it was.
  - `clean_artist(name)` removes ` - Topic`, `VEVO` and ` Official`.
  - `youtube_lookup_names(info)` tries yt-dlp `track` + `artist` first, then splits
    `"<artist> - <title>"` on the first `-` or en dash, then falls back to the raw title with the
    uploader (or channel) as artist. Values are cut to 500 characters.
  - `normalize_for_compare(text)` applies NFKC and casefold, maps `&` to and, removes Unicode
    punctuation and collapses whitespace. The matcher in 005:T031 builds on it.
- `metadata/source_artwork.py`:
  - `youtube_thumbnail(info)` picks the best `info["thumbnails"]` entry (plus the single
    `thumbnail` field) on `https://i.ytimg.com`: `maxresdefault` > `sddefault` > `hqdefault` >
    the widest other entry of at least 300 px.
  - `spotify_cover(song)` reads `Song.cover_url` on `https://i.scdn.co`.
  - `read_embedded(path)` uses tinytag to read the front cover (else any picture) and the
    title, artist and album. It never raises: unreadable files and WAV give empty
    `EmbeddedTags`.
  - `SourceDetailsReader` has three methods: `.youtube(fetched, info)`,
    `.spotify(source, song)` and `.upload(source, path)`.
- `ingest/details.py` (new): `ingest` may not import `metadata`, so the reader is **injected**.
  - The `SourceDetails` protocol is implemented structurally by `SourceDetailsReader`.
  - `NoSourceDetails` is the default.
  - `with_details(add, fallback)` is best effort: it logs only the exception type and returns
    the value without details, so it never fails an import (FR-002).
- Ingest hand-over:
  - `YtDlpFetcher(..., details=)` now sets `FetchedAudio.album` (yt-dlp `album`) and adds
    `artwork`, `lookup_title` and `lookup_artist`.
  - `SpotdlMetadataClient` reads `album_name` and `isrc` (canonical upper case, hyphens
    removed, validated) into `SpotifyTrack.album/isrc`.
  - `UrlSourceResolver(client, matcher, details)` sets `SourceInfo.album/isrc` and adds the
    Spotify cover and the cleaned lookup names (Spotify "X - Remastered" becomes "X").
  - `FileUploadIntake(max_upload_bytes, details)` adds the embedded cover, the tag
    title/artist (the lookup title falls back to the file title) and the album.
  - `create_ingest_adapters(settings, details=None)` wires the reader into all three.
- P0-2 is confirmed: spotDL 4.6.0 `Song` has `cover_url`, `isrc`, `album_name` and `year`
  (`spotdl/types/song.py`).
- SIG refactors made while I was there: `YtDlpFetcher._download`,
  `spotify._track_from_song`, `spotify._plausible`. The `pick_match` complexity warning is gone.

## Red -> green commits

| Task pair | Red | Green |
|---|---|---|
| 005:T022 / T023 | 47646a7 | 8743145 |

## Files changed

- `worker/src/stemstudio_worker/metadata/{titles,source_artwork}.py` (new)
- `worker/src/stemstudio_worker/ingest/details.py` (new);
  `ingest/{__init__,fetcher,resolver,spotify,upload}.py`
- `worker/tests/metadata/{test_titles,test_source_artwork,tagged}.py` (new);
  `worker/tests/ingest/test_source_details.py` (new)
- `specs/005-artwork-band-info/tasks.md` (ticked T022, T023), this note

## Tests and coverage

Ingest and metadata only:
`uv run pytest tests/ingest tests/metadata --cov=stemstudio_worker.ingest --cov=stemstudio_worker.metadata --cov-branch`

```text
ingest/{__init__,details,fetcher,limits,normalizer,resolver,spotify,text,upload,urls}.py  100 %
metadata/{http,images,ratelimit,source_artwork,titles,user_agent}.py                     100 %
TOTAL                                    977      0    156      0   100%
468 passed, 1 deselected
```

Whole worker: `uv run pytest --cov --cov-branch`: **1127 passed**, 3 deselected; TOTAL 4358
statements, 0 missed, 476 branches, 1 partial (the one partial already existed, outside my
paths).

All test inputs are built in the tests: ID3v2.3 MP3 and FLAC files with VORBIS_COMMENT and
PICTURE blocks (`tests/metadata/tagged.py`), images from Pillow, yt-dlp info dicts and a stub
`Song`. Nothing touches the network or a subprocess.

## Quality checks

- `ruff check src tests`: All checks passed! `ruff format`: clean. `mypy --strict src tests`:
  no issues in 171 files.
- `sig_check.py` on `metadata/**`, `ingest/**` and `tests/metadata/**`: 27 files, 270 units,
  **0 errors, 0 warnings**.
- Three older test-size warnings remain in `tests/ingest`: `test_fetcher.py:125`,
  `test_normalizer.py:246` and `test_spotify.py:132`. They are scheduled for 005:T051.
- `duplication.py` on my paths: 0 clones.
- Dependency direction: `ingest/**` and `metadata/**` import only `ports`, `schemas`,
  `config` and their own package (grep-checked).

## Dependencies added

None. tinytag 2.3.2 (MIT) was added in Phase 0.

## Open issues / requests

1. **stem-separation (005:T025, `main.py`)**: `default_adapters` must pass the reader, or no
   source artwork reaches the pipeline. `None` is the default and means no artwork. Wire it as
   `create_ingest_adapters(settings, SourceDetailsReader())` with
   `from stemstudio_worker.metadata.source_artwork import SourceDetailsReader`.
2. **stem-separation (005:T025)**: the job should prefer `SourceInfo.artwork` (the Spotify cover
   or embedded upload cover) over `FetchedAudio.artwork`. A Spotify job also downloads its
   YouTube match, so the fetcher offers a YouTube thumbnail as well.
   - `SourceMetadata.lookup_title/lookup_artist` should come from `FetchedAudio` for YouTube
     jobs and from `SourceInfo` for Spotify and upload jobs.
   - `album` should come from `SourceInfo.album`, else `FetchedAudio.album`.
   - `isrc` should come from `SourceInfo.isrc`.
3. **quality-gate (005:T006)**: the T006 rule "ingest does not import metadata" holds. The
   composition root is the only place where `metadata` and `ingest` meet.
4. **qa (005:T009/T030)**: the fake stack downloads nothing from YouTube or Spotify, so only the
   upload path (MP3/FLAC with an embedded cover, WAV without one) produces real source artwork
   there. For URL candidates, the fake normaliser of 005:T034 generates solid-colour images.

## Next owner

audio-ingest A continues with 005:T031 -> T034 (HO:us2).
