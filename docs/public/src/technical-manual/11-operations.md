# Operations

## Health

| Check | Expected |
|---|---|
| `docker compose ps` | worker, api and web *healthy* |
| `http://localhost:8080/health`, `http://localhost:8081/health` | `ok` |
| Worker `/health` (inside the network) | status, separator, device information, contract version |
| `GET /api/device` | Processing device and reason |
| `GET /api/agent/status` (011) | Agent backend, model, `ready` or the reason and fix; Ollama itself: `curl http://localhost:11434/api/tags` |
| `GET /api/lyric-assist/status` (023) | Dictionary layer (`available`) and model layer (ready, or the reason and fix, for example *"The GPU is busy ..."*); the model layer mirrors the agent status |
| `GET /api/library/folders` (022) | `pendingFiling` is `0` once every item is filed; above 0 the library shows *Sorting your library (n left)* |
| `scripts/check-soundtouch-served.sh [BASE_URL]` (012) | `soundtouch.wasm` served as `application/wasm` with the expected SHA-256 and the complete LGPL folder `/third-party/soundtouch/` |
| Worker `/health` `lanes.practice`, `structure` (014) | Queued and running practice jobs; the section analyser (`stems-novelty` or `fake`) and its version |
| Worker `/health` `lanes.insights`, `insights` (020) | Queued and running quality and handout jobs; the analyser (`real` or `fake`) and its version, WeasyPrint and Pango versions, fonts |
| `scripts/check-worker-pdf.{sh,ps1} [--build] [image]` (020) | WeasyPrint imports, Pango 1.44 or newer, DejaVu Sans listed |
| `scripts/check-worker-fonts.{sh,ps1} [--build] [image]` (008, 014) | Leipzig and DejaVu Sans listed by `fc-list`; `verovio`, `cairosvg`, `music21` and `reportlab` import |
| Windows without Docker | `scripts/dev.ps1 -SmokeTest` waits for all three health endpoints |

When the worker is down, imports answer 503 and existing tracks and mixes keep working.
After a restart during processing, affected tracks show *interrupted by restart* and can be
retried.

## Volumes and backups

| Volume | Contents | Back up? |
|---|---|---|
| `api-data` | SQLite database, uploads, stems, mixes, artwork, library, generations, voices | **Yes**, this is the user's data |
| `worker-data` | Job files, result cache, temporary work | No (rebuilt from the library stems; 013 rehydration) |
| `model-cache` | Downloaded model weights | Optional (downloaded again on demand) |
| `web-keys` | ASP.NET data-protection keys | Optional |
| `generator-data`, `singer-data`, `gpu-lease` (009, 010) | Generator work files, lease file | No |

From 013 use **Library care** in the app instead: it backs up the running library consistently
(online database snapshot and a write gate) into the backups folder, verifies archives and
restores them (empty library, replace with undo, or add what is missing). The manual volume copy
below remains a fallback for disaster recovery.

At start-up the API first finishes or rolls back an interrupted restore from its journal, then
marks library jobs that were running as failed (*interrupted by restart*), then applies
migrations. A previous library kept by a replace restore uses as much space as the library;
discard it in the app when it is no longer needed.

Manual fallback: back up `api-data` while the stack is stopped, for example:

```
docker compose stop
docker run --rm -v stemstudio_api-data:/data -v "$PWD":/backup alpine \
  tar czf /backup/stemstudio-api-data.tgz -C /data .
docker compose start
```

Restore by extracting the archive into an empty `stemstudio_api-data` volume before
starting the stack. Do not copy the SQLite database while the API is running.

## Model cache

Models are downloaded on first use into `model-cache` and verified with SHA-256. To keep the
cache on a specific disk, start with `docker/compose.model-cache-bind.yml` and
`STEMSTUDIO_MODEL_CACHE_DIR=/absolute/path` (writable by uid 10001). Approximate sizes:
separation models 2 GB, Whisper 1.6 GB (006), piano model 172 MB (008), ACE-Step about
10 GB (009) and voice models about 3.3 GB (010). Removing the volume only costs a new download.

## Upgrades

1. Pull the new version of the repository.
2. Read the release notes in `docs/STATUS.md` and this manual's feature status.
3. Rebuild and start: `docker compose up --build` (add the GPU and generative overrides you
   use). Database migrations run automatically when the API starts.
4. Check health and import a short song.

YouTube downloads depend on yt-dlp keeping up with YouTube; rebuilding the worker image picks
up the pinned newer version when the project updates it.

## Logs and troubleshooting

- `docker compose logs -f api worker` shows job progress and errors.
- GPU problems: `scripts/gpu-smoke.ps1` (or `.sh`) and the reason on the import screen. Do
  not install a Linux NVIDIA driver inside WSL.
- Detail separation needs at least 20 GB of Docker memory and an online first run.
- Lyrics text and the API key are never written to the logs by design.
- Agent (011): "Local AI not reachable" means Ollama is not running or `STEMSTUDIO_OLLAMA_URL`
  is wrong; "not pulled" names the `ollama pull` command; an API restart marks a running agent
  command `interrupted` (its jobs keep running; there is no "Continue" yet, the user sends the
  command again). Development databases that applied an earlier, unreleased 011 `AddAgent`
  migration must be recreated (`docker compose down -v` deletes all data).
- Library (022): items under *Unknown artist* or in the wrong folder get their names from the band
  info or the import; the user fixes them with library info. The sorting banner that never goes away
  means the backfill keeps failing: check `docker compose logs api` for `database is locked` or a
  running library job.
- Lyric assistant (023): *model-unavailable* names the cause (backend off, Ollama not reachable, GPU
  busy); `claude-notice-required` means the notice was not acknowledged; `assist-active` means
  another check is still running (cancel it in the pane); a refused sheet (`not-own-lyrics`) matched
  library lyrics, see the own-work confirmation in the user manual.

## Practice flow (014)

- **Sections** are detected on the worker's `practice` lane right after each separation (switch
  `STEMSTUDIO_SECTION_DETECT_ON_COMPLETE`); tracks separated before 014 get them when their mixer
  is first opened. When the worker is unreachable the status stays `none` and the next mixer open
  tries again; a refused job is stored as `failed` with the reason and the UI offers **Try again**.
  A job takes a few seconds on the CPU; the job sync polls the worker every 2 s while jobs exist.
- **Score views** are made when a score item becomes ready and on first open for older items. A
  restore of a backup brings the scores but not their `view/` pages; they are made again lazily.
- **Setlist PDFs** need a running worker: `503 worker-unavailable` means it cannot be reached,
  `503 document-failed` that the document job failed or produced an unusable PDF (for example
  above 16 MiB), `504 document-timeout` that it took longer than
  `StemStudio__Practice__SetlistPdfTimeoutSeconds` (default 30 s). The target for a 20-song setlist
  with lyrics is under 10 s (014 SC-008).
- **Fonts**: a worker image without DejaVu Sans cannot make setlist PDFs and one without Leipzig
  renders scores without music glyphs; check with `scripts/check-worker-fonts.{sh,ps1}`.
- **Practice log**: nothing to operate. It lives in the database (part of every backup), is never
  sent anywhere and is deleted by the user on the Practice page.

## Insights and handouts (020)

- **Quality reports** are switched off by default (`STEMSTUDIO_QUALITY_REPORT_ENABLED`, api and
  web). Switched on, they run on the worker's `insights` lane after every six-stem separation and
  detail split (`StemStudio__AutoQualityReport`); a 5-minute song with 18 stems takes about 13 s
  on the reference CPU with 4 threads (`STEMSTUDIO_INSIGHTS_THREADS`; budget 30 s). A report made
  with an older analyzer version shows as outdated and is only remade on request. Every label is a
  rough indication: gate G1 on real multitrack music (MUSDB18-HQ, 25 tracks) failed on all eight
  stem type and measure pairs, so vocals, drums, bass and other show only High (analyzer 1.0.1;
  reports made with 1.0.0 show as outdated).
- **Handouts** share that lane: a song handout takes under a second, a 20-song setlist about 3 s
  in the worker image. A handout over its limits is refused, fails or leaves items out with a
  warning (chapter 8); `503 worker-unavailable` means the worker cannot be reached. Check the image
  with `scripts/check-worker-pdf.{sh,ps1}` (WeasyPrint, Pango 1.44 or newer, DejaVu Sans).
- **Backups** include the quality reports, the practice notes and the handout items (exports
  class, regenerable with **Make again**).

## Play-along recording (015)

- **Where it works**: only in Chrome or Edge (Firefox with 44.1 kHz devices) on the StemStudio
  computer itself, at `http://localhost:8080` or `http://127.0.0.1:8080`. A user on another device
  sees *Not on this computer*; this is by design, not a fault.
- **Storage**: a stereo take uploads about 15.9 MB per minute (24-bit WAV), stored as FLAC under
  `assets/<assetId>/` with a transient `capture.wav`. Uploads need 1 GiB plus twice the upload
  free on the data drive (`409 insufficient-disk-space`); raise or lower the upload cap with
  `STEMSTUDIO_RECORDING_MAX_UPLOAD_BYTES` (compose sets the API and web values together). The
  storage overview of Library care lists takes as recordings; backups include them unless the
  recordings part is left out.
- **A take stays at *Saving***: the worker's render lane is busy or down; the take shows an error
  with **Try again** when the render fails (the capture is kept for that). `capture-missing` means
  the capture file is gone; record again.
- **Out of time**: ask the user to calibrate the latency again (the calibration is stored per
  input device label) and to use direct monitoring instead of browser monitoring.
- **Test stacks**: `docker-compose.e2e.yml` sets `StemStudio__Recording__TestLoopback=true`, a
  digital loopback in place of a microphone; never set it in a normal installation.

## Library folders (022)

Items are filed when they are saved. Existing items (after the upgrade to 022, and after a restore
of an older backup) are filed by a background step that starts **2 s after the API started** and,
after a restore, at the first library read; it works in batches of
`StemStudio:LibraryBackfillBatchSize` (default 500) and waits while a library job holds the
maintenance gate. It is idempotent and takes about 1-2 s for 5,000 items. The step repeats a failed
run up to five times, 1 s apart, then waits for the next library read, so no operator action is
needed. In the UI the library shows *Sorting your library (n items left)* meanwhile. A wrong or
missing name is corrected by the user with library info; there is nothing to repair in the data
folder, because folders are not stored. A restore replaces the database, so filing and library info
come back with it (older archives are filed after the restore).

## Lyric assistant (023)

- **Nothing to install.** The dictionaries are inside the API image (`/app/LyricAssist/Dictionaries`,
  7.7 MB); the model layer uses the agent backend (Ollama on the host, `qwen3.5:9b`). Without any
  model the spelling check, the syllable counts and the rhyme finder still work.
- **GPU busy.** While a heavy job holds the GPU (separation, generation) Ollama unloads the 9B model
  and the lyric assistant reports `model-unavailable` with the fix *"The GPU is busy ... The spelling
  check still works"*; there is no CPU fallback for lyrics. A GPU job that starts during an assist
  stops it with `backend-unavailable`; start the check again when the job has finished.
- **Claude.** Only when Claude is the selected backend (Settings) and the user pressed **I agree** in
  the lyric notice (`GET`/`PUT /api/lyric-assist/settings`). Per assist 20,000 input and 4,000 output
  tokens are allowed (`StemStudio__LyricAssist__MaxInputTokensPerAssist`, `…MaxOutputTokensPerAssist`);
  past the cap the assist fails with `budget-exhausted` and the pane shows the usage. Anthropic is
  contacted only by an assist the user started on that backend. The lyric assistant never switches
  backend by itself.
- **One assist at a time.** An assist that was running when the API stopped is marked failed
  (*interrupted by restart*) at start-up; its stored suggestions stay.
- **Test stacks.** With `STEMSTUDIO_AGENT_BACKEND=fake`, `STEMSTUDIO_LYRIC_ASSIST_FAKE_SCRIPT` names
  a script file or folder (`docker-compose.e2e.yml` mounts `e2e/lyric-scripts/` read-only at
  `/lyric-scripts`); `scripts/lyric-e2e.{ps1,sh} fake|none` run the E2E passes.
- **Logs.** Sheet text, suggestions and the Claude key are never logged; the runner logs the
  exception type of a failed block only.

## Interrupted library switch

If the API stops at start-up with *"A library switch could not be undone at start-up ... Free the
file and start StemStudio again."*, a restore or undo could not put every library item back, and
the API refuses to serve a half-switched library. Nothing was deleted. To recover:

1. Stop the stack (`docker compose stop`) and back up the `api-data` volume (see above).
2. Open the volume (for example `docker run --rm -it -v stemstudio_api-data:/data alpine sh`) and
   read the open journal in `/data/library-care/journal/<id>.json`. Each `moveAside` step names a
   live item (`from`) and where it was put (`to`, under `library-care/previous/<id>/`).
3. For each item that is still at its `to` path: move away whatever now sits at the live `from`
   path (it is the file that blocked the rollback; keep it until the library works), then move the
   item back to `from`.
4. Start the stack. Recovery finds every item back, closes the journal as rolled back and serves
   the library. Check the track count in the app, then remove the moved-away file.

Do not delete anything under `library-care/previous/` while a journal is open.
