# stemstudio-worker

Python 3.12 processing worker of StemStudio: ingest (yt-dlp, spotDL -> YouTube, uploads, ffmpeg),
stem separation (Demucs `htdemucs_6s`, fallback `htdemucs`) and beat analysis (librosa) behind a
FastAPI job API. Contract: `specs/001-stemstudio-mvp/contracts/worker-api.openapi.yaml`
(pydantic models in `src/stemstudio_worker/schemas.py`, ports in `src/stemstudio_worker/ports.py`).

```bash
uv sync                                   # Python 3.12 venv from uv.lock (CPU torch)
uv run pytest --cov                       # unit tests + coverage gate (90 %, branch)
uv run pytest -m integration              # real Demucs run (slow)
uv run ruff check . && uv run ruff format --check . && uv run mypy src tests
uv run uvicorn --factory stemstudio_worker.main:create_app --port 8000
```

Environment: `STEMSTUDIO_DATA_DIR` (default `data/worker`), `STEMSTUDIO_SEPARATOR`
(`demucs` | `fake`), `STEMSTUDIO_DEVICE` (`cpu` | `cuda` | `auto`). ffmpeg must be on `PATH`.
