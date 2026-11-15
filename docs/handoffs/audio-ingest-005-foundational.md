# Hand-off: audio-ingest / 005 foundational (005:T010-T013)

Audio-ingest agent A of the combined 005 + 007 wave, on top of the Phase 0 commit d2e06a7.

## What was built

New worker package `stemstudio_worker.metadata` (imports only `ports`, `schemas`, `config`):

- `metadata/user_agent.py`: `build_user_agent(contact_url, version=None)` gives
  `StemStudio/<worker version> ( <contact> )`. The contact is `STEMSTUDIO_CONTACT_URL` when it
  is a safe https URL (host present, no user-info, no `@` anywhere, not `mailto:`, <= 200 chars,
  no whitespace or control characters). Otherwise it is the generic token
  `local-practice-tool; contact not configured`. An invalid value is logged as a warning, and the
  log line does not contain the value. `worker_version()` reads the package version (`0.1.0`).
- `metadata/ratelimit.py`: `RateLimiter` is a token bucket with one request per second per host
  family (`musicbrainz` = musicbrainz.org + coverartarchive.org, `wikimedia` = wikidata.org +
  *.wikipedia.org, `archive.org`, every other host its own family). It is thread-safe and the
  clock and sleeper are injected. `retry_after_seconds(value, now)` reads seconds or an HTTP date
  and caps the wait at 30 s. `backoff_seconds(attempt, jitter)` gives 2, 4, 8 s, +-20 %.
  `MAX_RETRIES = 3`.
- `metadata/http.py`: `OnlineClient(user_agent, transport=None, pacing=None)` with
  `get(url, params) -> OnlineResponse | None`. Rules:
  - https only, on the allow-list `musicbrainz.org`, `coverartarchive.org`, `archive.org`,
    `*.archive.org`, `www.wikidata.org`, `*.wikipedia.org`, `i.ytimg.com`, `i.scdn.co`. The
    default port is required and user-info is refused.
  - Up to 3 redirects, each target re-validated (relative locations are resolved first).
  - Bodies are capped at 10 MB, both by the declared Content-Length and while streaming.
  - Timeouts are 10 s (connect, read, write, pool).
  - 503/429 answers honour `Retry-After`. Otherwise the client backs off. After 3 retries it
    raises `ServiceBusyError`.
  - 404 returns `None`. Any other status, a network failure or too many redirects raises
    `OnlineRequestError`. Refused hosts raise `HostNotAllowedError` and send nothing.

  `RequestPacing(limiter, sleep, now, jitter)` holds the injected time sources.
  `create_online_client(settings.online)` builds the process-wide client. Share one instance so
  the rate limit holds.
- `metadata/images.py`: `PillowImageNormalizer(client=None)` implements `ports.ImageNormalizer`.
  - Candidates are URLs (fetched through the `OnlineClient`, so the allow-list applies) or
    embedded bytes.
  - Accepted formats are JPEG, PNG and WebP. Anything else raises `UnsupportedImageTypeError`.
  - The pixel count is checked from the header before decoding: more than 40 MP raises
    `ImageTooLargeError`. Truncated or garbage data raises `NotAnImageError`, and a failed
    download raises `ArtworkFetchError`.
  - Only the first frame of an animation is used. The EXIF orientation is applied, then EXIF,
    ICC and text data are dropped. Alpha is flattened on black.
  - The picture is centre-cropped to a square and resized with LANCZOS to 600 px (q 85) and
    160 px (q 80). The quality is lowered in steps of 10 (to 20 at least) when a JPEG would
    exceed 200 KB or 20 KB.
  - `sha256` is computed over the cover bytes. Polyglot payloads do not survive the re-encode.

## Red -> green commits

| Task pair | Red (failing tests) | Green (implementation) |
|---|---|---|
| 005:T010 / T011 | a515055 | c86b0f4 |
| 005:T012 / T013 | ef14da0 | 02559f8 |

## Files changed

- `worker/src/stemstudio_worker/metadata/{__init__,user_agent,ratelimit,http,images}.py` (new)
- `worker/tests/metadata/{__init__,fakes,images,test_user_agent,test_ratelimit,test_http,test_images}.py` (new)
- `specs/005-artwork-band-info/tasks.md` (ticked T010-T013)
- `docs/handoffs/audio-ingest-005-foundational.md`

## Tests and coverage

`cd worker && uv run pytest -q --cov --cov-branch` (whole worker):

```text
src\stemstudio_worker\metadata\__init__.py        0      0      0      0   100%
src\stemstudio_worker\metadata\http.py          141      0     24      0   100%
src\stemstudio_worker\metadata\images.py         99      0     12      0   100%
src\stemstudio_worker\metadata\ratelimit.py      52      0      8      0   100%
src\stemstudio_worker\metadata\user_agent.py     33      0      6      0   100%
ingest/** unchanged                                                    100%
TOTAL                                          4191      0    458      1    99%
1016 passed, 3 deselected in 45.61s
```

`metadata` line coverage is 100 % (325/325) and branch coverage is 100 % (50/50), from 142
metadata tests. No test uses the network: they use `httpx.MockTransport`, a fake clock and
sleeper, and images generated in the test.

## Quality checks

- `uv run ruff check`: All checks passed! `uv run ruff format --check`: 160 files already
  formatted. `uv run mypy --strict src tests`: no issues in 164 source files.
- `sig_check.py worker/src/stemstudio_worker/metadata worker/tests/metadata`: 12 files,
  136 units, **0 errors, 0 warnings**.
- `run_all.py` (whole repo): 652 files, 4944 units, 29 clones, **1 error, 162 warnings**. Neither
  the error nor the warnings are in my files:
  - The error `e2e/support/preferences.ts:12 [duplication] ... src/web-interop/src/types.ts:8` is
    in qa/audio-engine files, which I did not touch.
  - `metadata:0: warning [dependency] metadata is not in the dependency map` waits for
    quality-gate 005:T006/T007.
  - The four ingest warnings already existed (`spotify.py pick_match` complexity 6, and three
    test unit sizes). I will clear them when I touch `ingest/` in 005:T023/T051.

## Dependencies added

None. httpx 0.28.1, Pillow 13.0.0 and rapidfuzz 3.14.6 were already direct dependencies from
Phase 0.

## Open issues / requests

- **stem-separation (005:T024/T025, main.py)**: build one process-wide client with
  `metadata.http.create_online_client(settings.online)` and pass it to
  `metadata.images.PillowImageNormalizer(client)`. This normaliser also fetches YouTube and
  Spotify URL candidates. Those downloads are not gated by `STEMSTUDIO_ONLINE_LOOKUPS`, because
  source artwork comes with the import (data-model 1). A convenience factory follows with
  005:T034.
- **quality-gate (005:T006/T007)**: add `"metadata": _ADAPTER` to `PYTHON_ALLOWED`.

## Next owner

audio-ingest A continues with 005:T022 -> T023 (HO:us1).
