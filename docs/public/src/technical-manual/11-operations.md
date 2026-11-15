# Operations

## Health

| Check | Expected |
|---|---|
| `docker compose ps` | worker, api and web *healthy* |
| `http://localhost:8080/health`, `http://localhost:8081/health` | `ok` |
| Worker `/health` (inside the network) | status, separator, device information, contract version |
| `GET /api/device` | Processing device and reason |
| Windows without Docker | `scripts/dev.ps1 -SmokeTest` waits for all three health endpoints |

When the worker is down, imports answer 503 and existing tracks and mixes keep working.
After a restart during processing, affected tracks show *interrupted by restart* and can be
retried.

## Volumes and backups

| Volume | Contents | Back up? |
|---|---|---|
| `api-data` | SQLite database, uploads, stems, mixes, artwork, library, generations, voices | **Yes**, this is the user's data |
| `worker-data` | Job files, result cache, temporary work | Optional (cache can be rebuilt) |
| `model-cache` | Downloaded model weights | Optional (downloaded again on demand) |
| `web-keys` | ASP.NET data-protection keys | Optional |
| `generator-data`, `singer-data`, `gpu-lease` (009, 010) | Generator work files, lease file | No |

Back up `api-data` while the stack is stopped, for example:

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
