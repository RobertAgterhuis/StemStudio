# Operations

## Health

| Check | Expected |
|---|---|
| `docker compose ps` | worker, api and web *healthy* |
| `http://localhost:8080/health`, `http://localhost:8081/health` | `ok` |
| Worker `/health` (inside the network) | status, separator, device information, contract version |
| `GET /api/device` | Processing device and reason |
| `GET /api/agent/status` (011) | Agent backend, model, `ready` or the reason and fix; Ollama itself: `curl http://localhost:11434/api/tags` |
| `scripts/check-soundtouch-served.sh [BASE_URL]` (012) | `soundtouch.wasm` served as `application/wasm` with the expected SHA-256 and the complete LGPL folder `/third-party/soundtouch/` |
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
