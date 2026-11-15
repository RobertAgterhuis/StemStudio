# ADR-003: Worker contract, job model and polling; Python ML stack

- **Status**: Accepted
- **Date**: 2026-11-14
- **Decider**: architect agent, on the CTO brief

## Context

Separation of a 4-minute song takes minutes on CPU. The spec requires background jobs with
status, progress, error, caching, one job at a time and restart reconciliation. Demucs, torch and
spotDL have tight version constraints on Python 3.12.

## Decision

- Worker HTTP API v1 (`contracts/worker-api.openapi.yaml`): `POST /v1/jobs` (JSON for URLs,
  multipart for uploads; idempotent per source key), `GET /v1/jobs/{id}`, `GET .../result`,
  `GET .../stems/{stem}`, `GET /health`. camelCase JSON, RFC 9457 errors. Pydantic models in
  `schemas.py` are the executable form of the contract.
- One in-process worker thread, FIFO queue, job state persisted as JSON files; non-terminal jobs
  become `failed` ("interrupted by restart") at start-up. Cache by source key and by SHA-256 of
  the normalised audio.
- The API polls every ~1 s from a `BackgroundService`; the browser polls the API. No push channel.
- `STEMSTUDIO_SEPARATOR=fake` copies the mix into N stems for CI/E2E.
- ML stack: **demucs 4.1.0** (not 4.0.1 as the brief assumed: 4.1.0 removes the torchaudio
  dependency and ships `demucs.api`), **torch 2.15.0 CPU wheels** from the PyTorch CPU index,
  **no torchaudio**; audio I/O through soundfile and ffmpeg. Agents load models with
  `demucs.pretrained.get_model` + `demucs.apply.apply_model`. spotdl 4.6.0 forces uvicorn 0.23.2.

## Consequences

- Simple, restart-safe and testable without a broker; at most ~1 s status latency.
- Model weights download on first use (Hugging Face Hub, fallback dl.fbaipublicfiles.com):
  Docker must persist `HF_HOME`/`TORCH_HOME`; offline first runs fail with a clear job error.
- Upgrading torch/demucs requires re-running the integration test and `uv lock`.
