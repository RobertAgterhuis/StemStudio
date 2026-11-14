# Hand-off: stem-separation / us1

## What was built
- **Separation** (`separation/`): `FakeSeparator` (6 bit-identical FLAC copies of the mix, model `fake`);
  `DemucsSeparator` (`htdemucs_6s`, fallback `htdemucs` when loading fails, both fail -> `SeparationError`,
  model loaded once, device `cpu`/`cuda`/`auto`, progress clamped and non-decreasing, inference errors ->
  "Stem separation failed." with the cause only in the log); `demucs_runtime` (lazy torch/demucs imports,
  `get_model` + `apply_model` with CLI-style normalisation, segment callback -> fraction); `flac_writer`
  (soundfile, 44.1 kHz, 16-bit FLAC, no torchaudio); `create_separator(settings)`.
- **Jobs** (`jobs/`): `JobStore` (foundational); `JobRunner` (fetch or upload -> normalise -> separate ->
  analyse -> cache -> complete; stage texts and progress ranges per data-model 3.2; `WorkerError` message as
  `error`, else "Unexpected error" + log; `work/<id>` and `uploads/<id>` removed when terminal); `JobQueue`
  (one daemon thread, FIFO); `JobTracker`; `ResultCache` (`cache/by-audio/<sha>/{result.json,stems/}` written
  via a `.partial` dir and renamed, `cache/by-source/<key_>.json` pointer; source hit queued -> completed,
  audio hit downloading -> completed; damaged entries are misses); `JobService` (idempotent per source key
  under a lock, also for concurrent creates); `reconcile` (non-terminal -> failed "interrupted by restart",
  leftovers removed); `WorkerRuntime` (start/stop).
- **API** (`api/`, `main.py`): routes exactly per `worker-api.openapi.yaml` v1.0.1; problem+json for every
  error (also framework 404/405); JSON vs multipart dispatch on content type (else 415); uploads: extension
  check (415), early `Content-Length` check and streamed copy with exact 200 MiB limit (413), empty -> 400,
  intake rejection -> 415, duplicate upload -> 200 and the copy is discarded; stems served from the cache
  with `audio/flac` (stem not in the model's set or missing on disk -> 404, not completed -> 409).
  `create_app(settings=None, adapters=None)`: default adapters from the three T001 factories, reconcile and
  runner thread in the lifespan (so `/health` works without the lifespan).

## Red -> green evidence
| Task pair | Failing test commit | Implementation commit |
|---|---|---|
| T035 -> T036 | 5354406 | 8dd1d27 |
| T056 -> T057 | 69f2893 | 8724efb |
| T058 -> T059 | 6d86dea | 6642860 |
| T060 (integration test) | 497d438 | - (runs against T059) |
| T061 -> T062 | eab6439 | a6a0310 |
| T063 -> T064 | e472cf0 | 0249229 |
| T065 -> T066 | bef36b5 | 5de3f86 |
| T067 -> T068 | bac10ff | 647b8c8 |

## Tests and coverage
`uv run pytest tests/separation tests/jobs tests/api --cov=stemstudio_worker.separation
--cov=stemstudio_worker.jobs --cov=stemstudio_worker.api --cov=stemstudio_worker.main --cov-branch`:
**116 passed, 1 deselected**; 743 statements, 78 branches: **100 % line, 100 % branch** (every module 100/100).

Integration test (`uv run pytest -m integration -s`, `HF_HOME`/`TORCH_HOME` under `data/model-cache/`):
- first run: `model=htdemucs_6s elapsed=6.2s`, 1 passed in 7.75 s (includes the 53 MB weights download
  from the Hugging Face Hub, `adefossez/HTDemucs-6s`); warm run: `model=htdemucs_6s elapsed=3.4s`, 1 passed.
- K5 check: empty cache + unreachable proxy + `CI=true` -> **FAILED** ("Demucs model unavailable in CI:
  The separation models htdemucs_6s and htdemucs could not be loaded..."); same without `CI` -> SKIPPED.

Whole worker suite: 146 passed, 1 failed - `tests/test_ports.py::test_factory_stubs_are_placeholders[create_separator]`
(architect-owned skeleton test that asserts the placeholder; see Open issues 1).

## Quality checks
- `uv run ruff check .`: All checks passed. `uv run ruff format --check .`: 50 files already formatted.
- `uv run mypy src tests` (strict): Success, no issues in 50 source files.
- `tools/quality/sig_check.py` is not in this worktree; an AST check (code lines without blanks, comments and
  docstrings) shows every production unit <= 15 LOC and <= 4 parameters; ruff mccabe (max 5) and pylint
  limits (args 4, statements 15, branches 5) pass. Eight test functions are 16-22 LOC (below the hard max 30).

## Dependencies added
None (uses demucs, torch, soundfile, numpy, fastapi, python-multipart, jsonschema, pyyaml from the skeleton).

## Open issues / requests
1. **architect**: `tests/test_ports.py::test_factory_stubs_are_placeholders` still lists `create_separator`;
   drop it from the parametrization (it is now implemented). The other two factories follow when their
   owners merge.
2. **architect (contract)**: with the current ports, `SourceResolver.resolve` (Spotify metadata + YouTube
   match) runs synchronously inside `POST /v1/jobs`, because the source key is needed for idempotency.
   A `SourceUnavailableError` there (e.g. "No matching YouTube audio was found for this Spotify track.")
   is returned as **400** `title: Source unavailable` with that message, not as a failed job like the
   `FailedJob` example. Either document this in the 400 description, or split the port (key + metadata in
   `resolve`, YouTube matching in `fetch`). **backend**: the `POST /v1/jobs` timeout must allow spotDL latency.
3. Starlette's multipart parser spools a part before the handler sees it: oversized uploads are rejected
   early by `Content-Length` (> limit + 64 KiB) and exactly while copying; a chunked upload without
   `Content-Length` is spooled to a temp file before the 413.
4. **devops**: weights come from the HF Hub (53 MB for `htdemucs_6s`); when the hub is unreachable demucs
   falls back to `dl.fbaipublicfiles.com` via `torch.hub` into `TORCH_HOME` (observed). Persist both, and set
   `HF_HUB_DISABLE_SYMLINKS_WARNING=1` on Windows hosts. Shutdown waits at most 5 s for a running job; the job
   is then reconciled as interrupted at the next start.
5. `uvicorn --factory stemstudio_worker.main:create_app` fails at start-up in this worktree until the
   audio-ingest and beat-analysis factories are merged (expected; T120/T122).
6. Starlette's `TestClient` uses the transitive `httpx2` package, not the declared `httpx` dev dependency;
   the API tests type responses structurally. The architect may want to declare `httpx2` instead.
7. `jobs/cache.py` `ResultCache.store` landed with T062 (stems need a permanent home); T064 added lookups.

## Next owner
CTO: review and merge (T119 order: after audio-ingest and beat-analysis). stem-separation continues with T116.
