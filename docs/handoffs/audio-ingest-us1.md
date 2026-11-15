# Hand-off: audio-ingest / us1

Checkpoint hand-off per ADR-008 for T043-T050, T139 and T140 (owner sequence up to `HO:us1`).
Branch: `worktree-agent-abedf3add480cf058` (based on `001-stemstudio-mvp` at `fbf0ee3`).

## What was built

All four ingest ports from `ports.py` are implemented, and so is the agreed factory
`create_ingest_adapters(settings) -> IngestAdapters`.

| Port | Implementation | Notes |
|---|---|---|
| `SourceResolver` | `ingest.resolver.UrlSourceResolver` | Uses `ingest.urls.parse_source_url`. Only allow-listed hosts are accepted (`youtube.com`, `www.`/`m.`/`music.youtube.com`, `youtu.be`, `open.spotify.com`). Accepts http/https only. Rejects user info and explicit ports. Playlists, `list=`-only URLs, albums, artists, episodes and shows are rejected with actionable messages. A `watch?v=<id>&list=...` URL is accepted as the single video. Produces canonical URLs and `youtube:<id>` / `spotify:<id>` keys. Spotify goes through `SpotifyMetadataClient` (spotDL `Song.from_url`, metadata only) and then `YouTubeMatcher` (spotDL YouTube Music, then YouTube search). Title and artist always come from Spotify. The match URL is re-validated against the YouTube allow-list; anything else counts as "No matching YouTube audio was found for this Spotify track." Spotify audio is never requested (no DRM circumvention). |
| `UploadIntake` | `ingest.upload.FileUploadIntake` | Accepts `.mp3`/`.wav`/`.flac` (case-insensitive) and returns key `upload:<sha256 of stored bytes>`. The title is the last segment of the file name without its extension: control characters are removed, it is cut to 500 chars, and an empty result becomes "Untitled upload". The file name is never used as a path. As a second line of defence it also rejects empty files and files over `max_upload_bytes`. |
| `AudioFetcher` | `ingest.fetcher.YtDlpFetcher` | Uses the yt-dlp Python API through an injected `YoutubeDL` factory (default `create_youtube_dl`, imported lazily). Downloads `bestaudio/best` with `noplaylist`, and the output is `work_dir/source.<ext>`. The URL is re-validated before use. `extract_info(download=False)` runs first, so playlists, live streams and a reported duration over the limit are rejected before any media is downloaded. Progress fractions are non-decreasing and end at 1.0 (stage "Downloading audio"). Any library error becomes a generic `SourceUnavailableError`: the cause is suppressed and only the exception type is logged. |
| `AudioNormalizer` | `ingest.normalizer.FfmpegNormalizer` | Takes an injected `CommandRunner` (default `SubprocessRunner`: `subprocess.run` with an argument list, no shell, stdin `DEVNULL`, timeout). Runs ffprobe first, which must find an audio stream; it rejects a probed duration over 900 s before converting. ffmpeg then converts with `-map 0:a:0 -ac 2 -ar 44100 -c:a pcm_s16le -f wav` and `-t <limit+1>`, so endless inputs still stop. Paths are passed as absolute `file:` URLs, so a name cannot be read as an option or protocol. The written WAV is checked (44.1 kHz, stereo, 16-bit, non-empty) and its duration is checked again. Returns the SHA-256 of the WAV. |

The fixed error texts match data-model.md verbatim: "Audio is longer than 15 minutes" and
"No matching YouTube audio was found for this Spotify track.". Other texts are readable and never
contain tool output or library messages.

Security note (CTO request): every id check uses `re.fullmatch` (no `^...$`). Control characters
(`\x00-\x1f`, `\x7f`) anywhere in a URL are rejected before `urlsplit`. `urlsplit` would otherwise
silently drop tab, CR and LF, and `str.strip()` would remove a trailing `\n`. Only surrounding
spaces are trimmed now, which the vectors require. Tests cover a trailing `\n` and `\r\n`, a
leading `\n`, embedded `\n`, `\t`, `\r`, `\x00` and `\x7f`, and percent-encoded `%0A` in ids. All
are rejected. Upload titles drop control characters.

## Red-to-green evidence (commits)

| Task | Failing test commit | Implementation commit |
|---|---|---|
| T043/T044 URL resolver + spotDL adapters | `5412d59` | `3772e38`; hardening `3e056ae` (control chars) |
| T045/T046 fetcher | `7d7b8d0` | `3123f3f` |
| T047/T048 normaliser | `27276df` | `1ed7fc8` (also fixes a bug in the T047 fake WAV writer) |
| T049/T050 upload intake | `6bd97ea` | `47466fd` |
| T139/T140 factory | `7207f01` | `fa72317` |

Each test commit was red when committed: collection failed with `ImportError`, and T139 ran as
5 failed.

## Files changed

- `worker/src/stemstudio_worker/ingest/{__init__,urls,spotify,resolver,fetcher,normalizer,upload,limits}.py`
- `worker/tests/ingest/{test_url_resolver,test_spotify,test_fetcher,test_normalizer,test_upload_intake,test_factory}.py`
- `specs/001-stemstudio-mvp/tasks.md` (ticked T043-T050, T139 and T140 only)
- `docs/handoffs/audio-ingest-us1.md`

## Tests and coverage

`cd worker && uv run pytest tests/ingest --cov=stemstudio_worker.ingest --cov-branch --cov-report=term-missing`

```
Name                                         Stmts   Miss Branch BrPart  Cover   Missing
----------------------------------------------------------------------------------------
src\stemstudio_worker\ingest\__init__.py        10      0      0      0   100%
src\stemstudio_worker\ingest\fetcher.py         88      0     18      0   100%
src\stemstudio_worker\ingest\limits.py           7      0      0      0   100%
src\stemstudio_worker\ingest\normalizer.py      92      0     10      0   100%
src\stemstudio_worker\ingest\resolver.py        24      0      4      0   100%
src\stemstudio_worker\ingest\spotify.py         81      0     12      0   100%
src\stemstudio_worker\ingest\upload.py          43      0      6      0   100%
src\stemstudio_worker\ingest\urls.py           101      0     34      0   100%
----------------------------------------------------------------------------------------
TOTAL                                          446      0     84      0   100%
Required test coverage of 90.0% reached. Total coverage: 100.00%
201 passed, 1 deselected
```

Line coverage is 100 % (446/446) and branch coverage is 100 % (84/84). The unit tests use fakes
only: a fake `YoutubeDL` factory, a fake spotDL metadata client and matcher, a fake
`CommandRunner`, and fake `spotdl.*` / `yt_dlp` modules injected through
`monkeypatch.setitem(sys.modules, ...)` for the lazy-import glue. They use no network and no real
subprocess.

Opt-in integration test (real ffmpeg, a 2 s tone generated locally with `lavfi`):
`uv run pytest tests/ingest -m integration` gives `1 passed, 201 deselected`. It also confirms that
ffmpeg accepts the `file:` URLs on Windows.

## Quality checks

- `uv run ruff check`: All checks passed!
- `uv run ruff format --check`: 28 files already formatted
- `uv run mypy --strict src`: Success: no issues found in 17 source files (`mypy --strict src tests`: no issues in 27 files)
- SIG limits are enforced by the ruff config (`C901` max-complexity 5, `PLR0913` max-args 4, `PLR0915` max-statements 15, max-branches 5), and the package is clean. All units stay at or under 15 LOC. There are no duplicated blocks of 6 or more lines. `tools/quality/sig_check.py` does not exist yet (quality-gate). Dependencies point one way: `ingest` imports only `ports`, `schemas` and `config`, plus its own modules.

## Dependencies added

None. yt-dlp 2026.10.26 and spotdl 4.6.0 were already locked by the architect.

## Open issues / requests

1. **Architect / CTO:** `worker/tests/test_ports.py::test_factory_stubs_are_placeholders[create_ingest_adapters]`
   now fails as intended, because the stub is implemented. Drop `create_ingest_adapters` from that
   parametrisation. The full `uv run pytest` gives 231 passed, 1 failed (that case only).
2. **Contract gap (architect):** `SourceResolver.resolve` must not use the network for YouTube, and
   `AudioFetcher.fetch` returns only a `Path`. So the real YouTube title is never passed on, and
   the resolver returns the placeholder title `"YouTube video <id>"`. Proposal: let `fetch` return
   `(path, title, artist)`, or add an optional `title` to a fetch result type. yt-dlp already has
   `info["title"]` / `info["uploader"]` at no extra cost.
3. **Status mapping (stem-separation, API):** `FileUploadIntake` raises `InvalidSourceError` for
   three cases: an unsupported extension (should be 415), an empty file and a file over the limit
   (should be 400/413). The size check is defence in depth only, because the API enforces 413
   while streaming. If the API maps `InvalidSourceError` to 415 unconditionally, a subclass may be
   wanted. That would be a `ports.py` change, so I did not make it.
4. **devops:** YouTube downloads with current yt-dlp need a JavaScript runtime (deno) for
   signature solving. `yt-dlp-ejs` is installed, but the runtime is not. The worker image needs
   deno, plus `ffmpeg`/`ffprobe` on PATH. spotDL writes to `~/.spotdl` (temp/cache), so that
   directory must be writable in the container.
5. **CTO licence/ToS review (unchanged from architect note 3):** spotDL 4.6 uses unofficial
   Spotify web clients (spotipyfree/spotapi, GPL-3.0) for metadata. The client is initialised
   with spotDL's bundled public defaults (`DEFAULT_CONFIG`). No credentials are stored or passed by
   StemStudio.
6. Naming: tasks.md calls the upload class `UploadIntake`. It is `FileUploadIntake` here, to avoid
   shadowing the protocol of that name in `ports.py`.

## Next owner

- stem-separation: wire `create_ingest_adapters` in `main.create_app`. The job runner should catch
  `WorkerError` and use its message as the job `error`.
- CTO: review. Then architect for issues 1-3 and devops for issue 4.
