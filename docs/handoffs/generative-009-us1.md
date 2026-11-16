# Hand-off: generative / 009 US1 - restyle through the generator API (T032-T036)

## What was built

- **Ports (small move as the architect's delegate, agreed with the CTO)**:
  - `TakeLabels` and `WrittenFile` moved from `audio/flac.py` to `ports.py`; `audio.flac` still
    imports them.
  - New ports: `TakeWriter` (`FlacWriter` satisfies it), `StemMixer` and `MixedSource`.
  - Why: `jobs` and `api` reach the audio helpers only through ports that `main.py` injects
    (quality-gate layer rule).
- **`api/`** (`app`, `context`, `routes`, `uploads`, `intake`, `problems`):
  - `GET /health`.
  - `POST /v1/generations`. Multipart restyle: a `params` part (file or plain field) plus 1-6
    `stem` parts. Request order: content type, request validation, `kindNotServed`,
    `gpuRequired`, per-kind intake (save parts, `413`/`415`, `StemMixer.measure`, `422
    sourceTooShort`), then queue.
  - `GET`, `POST .../cancel`, `DELETE`.
  - `GET .../takes/{index}`: Starlette `FileResponse` with single byte ranges, `416` with
    `Content-Range: bytes */size`, and `ETag` set to the quoted SHA-256.
  - RFC 9457 problems carry the contract `code`. Request-validation errors and unknown paths are
    rendered as problems too.
- **`jobs/files.py`** (`inputs/`, `work/`, `takes/`). **`jobs/restyle.py`**:
  - `RestyleHandler` sums the stems in `prepare`, before the GPU is requested, so CPU work never
    holds the lease.
  - It runs the engine and labels each take through `TakeOutput`.
  - `cleanup` deletes the inputs and the work files.
  - `JobHandler` gained `prepare` and `cleanup`. The runner calls `cleanup` whatever the outcome.
- **`audio/pipeline.py`**: `AudioStemMixer` runs `read_audio` -> `sum_stems` -> `cut_range`
  (range mode only) -> `write_wav`. `CLIPPED_WARNING` is added when the sum clipped.
- **`engines/acestep_engine.py`**, the ACE-Step 1.5 adapter, plus `acestep_api.py`:
  - ACE-Step is imported lazily. The turbo DiT loads from `<model_dir>/acestep/checkpoints`
    (`project_root=<model_dir>/acestep`).
  - The low-memory profile offloads the DiT, VAE and text encoder to the CPU.
  - Restyles never initialise the LM.
  - Restyle maps to `task_type="cover"`, `src_audio`, caption, `audio_cover_strength`, steps and
    shift; config is `batch_size` = number of takes, fixed `seeds`, FLAC.
  - Progress is mapped to our stage texts. A `Cancelled` raised in the callback is remembered
    even when ACE-Step's progress thread swallows it.
  - An OOM or the "Insufficient free VRAM" guard becomes `outOfMemory`. Other failures get a
    generic text; the raw text only goes to the log.
  - `unload` empties the CUDA cache.
- **`engines/weights.py`**:
  - Weights count as present only when `REVISION` and `SHA256SUMS` match the pin.
  - The pinned files are hashed once per process.
  - A missing download runs file by file at the pinned revision with byte progress
    (`downloadingModel`).
- **`main.py`**: the composition root. Fake engine: real gate over a fake NVML that uses
  `fake_free_memory_mib`. GPU engines: torch/NVML probe, file-lease gate and torch memory cap. At
  start-up it reconciles jobs, resumes the queue and starts the queue thread.
- **qa conformance**: `tests/api/test_fake_fixture.py` checks the fake stack against
  `tests/fixtures/generative/fake-generator.json`.
  - Every mode in it is a configurable mode.
  - The restyle terminal state, error code and message pattern match each mode.
  - `no-gpu` reports a GPU/CUDA reason.
  - `STEMSTUDIO_TAKE` is the take index.
  - The fake pitch tracker hears `own-voice-20s.wav` (>= 0.5) and not `unvoiced-20s.wav`. This is
    skipped when the git-ignored voice fixtures have not been generated.

## Red -> green

| Task | Red | Green |
|---|---|---|
| T032 -> T033 | `8c9ac03` | `673274a` (ports move `981f0cf`) |
| T034 -> T035 | `3c2ae1e` | `2bafcf4` |
| T036 | `78a1a58` (real GPU test and qa conformance) | ran green on the GPU, see below |

## GPU measurement (RTX 5070 Ti, throw-away container, `stemstudio` untouched)

- **Setup**: `ghcr.io/astral-sh/uv:python3.12-bookworm-slim`, `uv sync --frozen --extra
  acestep`, ffmpeg. The model volume was mounted **read-only**, behind a writable overlay: weight
  files symlinked, small files copied.
- **Run**: `tests/integration/test_acestep_real.py` **passed**. A 10 s cover gives a 10 s FLAC at
  48 kHz, 24-bit, stereo, with all the tags. The call took 30.0 s (cold, including DiT load).
- **Memory**: NVML idle was 2,264 MiB and the peak over the whole run (cover + song) was
  13,139 MiB.

**Finding for devops/CTO**: ACE-Step's `initialize_service` rewrites its model code files inside
the checkpoint directory (`_sync_model_code_if_needed`, e.g.
`acestep-v15-turbo/modeling_acestep_v15_turbo.py`) when they differ from the installed package.
With a read-only `/models` the load fails with `EROFS`.

- The `generator` service therefore needs `model-cache` **read-write**, as devops planned.
- Our weights check only hashes the pinned weight files, so these code-file rewrites do not
  invalidate the cache.

## Tests and coverage

See the polish hand-off (`generative-009-010-polish.md`) for the final numbers. At the T036 commit
the suite had 399 passed, about 98-99 % line and branch coverage, and ruff, format, mypy --strict
and sig_check (src) were all clean.

## Open issues / requests

- **devops**: mount `model-cache` read-write for `generator` (see above); ffmpeg in every image
  variant (agent B).

## Next owner

CTO (integration). generative continues with US6/US5.
