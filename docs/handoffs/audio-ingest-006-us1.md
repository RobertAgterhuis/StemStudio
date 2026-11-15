# Hand-off: audio-ingest / 006 US1 (T027-T028)

## What was built

The `LyricsLookup` port implementation (`stemstudio_worker.lyrics`):

- `lrclib.py`: `LrclibClient(getter, limiter=None)` with `get(query, with_album)` and
  `search(query)`. It goes through the shared 005 `OnlineClient`, which handles the allow-list,
  the 005 User-Agent, `Retry-After`/back-off and the 10 MB cap. The client adds the LRCLIB
  parameters (`track_name`, `artist_name`, `album_name`, `duration` rounded to whole seconds)
  and its own spacing of at least 500 ms (`MIN_REQUEST_GAP_SECONDS`, a 005 `RateLimiter` keyed
  `lrclib.net`). A 404, a non-JSON body or an unexpected shape counts as not found. Unreadable
  search items are skipped. `OnlineRequestError`/`ServiceBusyError` raise
  `LyricsUnavailableError`. `LrclibRecord` is the lenient record model.
- `select.py`: `kind_of`, `select_record`, `Choice`. Records within 2 s are ranked
  word > line > plain > instrumental; ties go to the equal album (case-insensitive), then to the
  lowest id. Without a 2 s match, the closest record with `plainLyrics` within 15 s is used as
  `plain`.
- `lookup.py`:
  - `LrclibLookup(client)`: runs get+album, then get, then search, and stops at the first usable
    record. The gets need an artist, so with only a title it searches. That makes at most 3
    requests. It reports `LYRICS_STAGE_SEARCHING` at 0 and 1.
  - `result_for(choice)`: word documents come from the `lyricsfile` (the LRC lines are used when
    the file is unreadable); line documents are interpolated LRC; plain documents are text only.
    `instrumental` gives the empty contract document and `not-found` gives `lyrics: null`. The
    record's `plainLyrics` (LF line breaks, at most 20,000 characters, otherwise the line texts
    are joined) becomes `plainText`.
  - `DisabledLyricsLookup` and `create_lyrics_lookup(settings, getter)` for the composition
    root. Lookups off -> `disabled` with no request (this also wins over the fake stack);
    `separator=fake` -> `FakeLyricsLookup(settings.lyrics.fake_mode)`; otherwise
    `LrclibLookup`.
- `fake.py`: `FakeLyricsLookup(mode)` with self-written `FAKE_LINES`. Line *i* of *n* starts at
  `duration*(i+1)/(n+1)`. Modes: `word` (origin source, `en`), `line` (interpolated), `plain`,
  `instrumental`, `not-found`, `failed` (raises `LyricsUnavailableError`). `sourceId` is
  `fake-1`.
- `normalize.py` gained `normalize_newlines` and `instrumental_document`. `interpolate.py` gained
  `lrc_document`.
- No lyrics are logged: the package has no logging at all, and a test scans DEBUG log output of
  word, line and plain lookups for the fixture lines.

## Commits (red -> green)

| Task | Red | Green |
|---|---|---|
| T027 -> T028 (lrclib, select, lookup, fake) | 2ae3899 | 91d35b0 |

## Files changed

- `worker/src/stemstudio_worker/lyrics/{lrclib,select,lookup,fake}.py` (new), `normalize.py`,
  `interpolate.py`
- `worker/tests/lyrics/{records,test_lrclib,test_select,test_lookup,test_fake}.py` (new),
  `test_interpolate.py`

## Tests and coverage

```text
uv run --frozen pytest tests/lyrics --cov=stemstudio_worker.lyrics --cov-branch
  200 passed, 1 xfailed (the strict allow-list guard, see request 1)
  lyrics: 441 stmts, 60 branches, 100 % line and branch (all 9 modules 100 %)
uv run --frozen pytest --cov --cov-branch   (whole worker, at 91d35b0)
  1936 passed, 7 deselected, 1 xfailed; TOTAL 7109 stmts, 814 branches, 100 %
```

## Quality checks

```text
ruff check .            All checks passed!
ruff format --check .   252 files already formatted
mypy src tests          Success: no issues found in 255 source files
sig_check.py worker/src/stemstudio_worker/lyrics worker/tests/lyrics
  19 files, 255 units, 0 errors, 0 warnings, 0 exemptions
tools/quality/run_all.py   885 files, 0 errors -> exit 0
  (lyrics warnings: two test-fixture clones, removed in T056 f906905; "lyrics is not in the
  dependency map", see the polish hand-off)
```

## Dependencies added

None.

## Open issues / requests

1. **Blocking for production (CTO / owner of `worker/src/stemstudio_worker/metadata/http.py`)**:
   `lrclib.net` is not on the 005 `ALLOWED_HOSTS`, so the real `OnlineClient` refuses every
   LRCLIB URL with `HostNotAllowedError`. That error maps to `LyricsUnavailableError`, so every
   real lookup would fail with "Lyrics could not be retrieved". The fix is one line: add
   `"lrclib.net"` to `ALLOWED_HOSTS` (research R-02 already lists it as the only lyrics host).
   It is outside my lane, so I did not make it. Guard: `test_lrclib.py::test_the_005_client_allows_lrclib`
   is `xfail(strict=True)` only while the host is missing. Once it is added, the test runs as a
   normal passing test, and no edit to my tests is needed. The wiring tests (`TestThrough005Client`:
   User-Agent, `Retry-After`, busy -> unavailable, 404) already run against the real
   `OnlineClient` over `httpx.MockTransport`. They monkeypatch the allow-list to include
   `lrclib.net`, which simulates the fix.
2. **stem-separation (006:T029/T030 wiring)**: build the lookup once with
   `create_lyrics_lookup(settings, online_client)`, passing the process-wide 005 `OnlineClient`
   so its 1 request/s limiter is shared. Run `lookup` on the 005 network lane. Map
   `LyricsUnavailableError`/`LyricsUnusableError` messages (fixed texts) to `LyricsJob.error`.
   Deleting the worker's copy after the backend fetched it, and the alignment rule (`vocals.lead`
   first, collapse check plus one `vad=True` retry, `ALIGNMENT_FAILED_TEXT`), are stem-separation
   work (`jobs/`, `alignment/`). Nothing in `lyrics/` keeps a copy.
3. **qa (006:T012)**: once `tests/fixtures/lyrics/` exists, a test can compare `FAKE_LINES` and the
   fake modes with a fake-provider table, as 005 did. The current fake text is self-written.

## Next owner

stem-separation A (routes/lanes wiring, T029/T030); CTO for request 1.
