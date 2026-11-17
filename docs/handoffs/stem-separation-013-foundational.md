# Hand-off: stem-separation / 013 foundational (T025-T030)

Branch `013-lane-worker` (from `013-library-care` 7253cf37). Worker API 1.7.0 storage tag
implemented against `schemas.py`/`ports.py` 1.7.0 (architect T007), red -> green per task pair.

## What was built

- **`storage/` (new package)**, may import `jobs.*`, never `separation`, `detail` or `api`:
  - `walk.py`: link-free tree size (`os.scandir`, no symlinks or junctions followed or counted).
  - `users.py`: cache users from the job files: standard jobs (`audioSha256`; failed and
    `cacheEvicted` jobs not attributed), detail jobs, lyrics `align` jobs (via their standard job).
    A non-terminal job of any of them puts the entry `inUse`.
  - `model_catalog.py`: known model ids per `/models` top-level folder (Demucs in `hf/` and
    `torch/`, detail models, Whisper, basic-pitch, piano CRNN), markers checked against
    `detail.registry` and the pinned weights in tests.
  - `meter.py` `DiskStorageMeter` (`StorageMeter`): volumes via injectable `disk_usage` (nearest
    existing folder), cache entries split into standard/detail/partial/artwork bytes, job files
    of all six job kinds, scratch (`work/`, `uploads/`, `artwork/`), model caches; report cached
    30 s, `invalidate()` after a clean-up or rehydration.
  - `janitor.py` `DiskCacheJanitor` (`CacheJanitor`): under the queue lock; `["*"]` or a list
    (duplicates once; `notFound`, `inUse` skips); removes the entry and its `by-source` pointers,
    marks referencing standard jobs `cacheEvicted`; scratch and old job files (rules below);
    freed bytes measured before/after, so exact.
  - `library.py` `FileLibraryStemReader` (`LibraryStemReader`): `DataPaths.library_stem` only
    (UUID + stem enum); no library / not a folder / other `OSError` -> `LibraryUnavailableError`,
    missing file -> `StemMissingError`.
  - `rehydrate.py` `Rehydrator`: copies the model's stems into `cache/by-audio/<newJobId>.partial`
    while hashing, key `sha256("library:" + ",".join(sorted("stem=sha")))`, places the entry with
    `ResultCache.store` (atomic rename), writes a completed `StoredJob` (`fromLibrary`, progress
    100, stage "Completed", every stem's FLAC header must be readable, duration = longest stem,
    clamped to 900 s). Idempotent per key (200); an evicted rehydrated job gets its entry back and
    loses the flag. Errors: `StemSetError`, `InvalidStemError` (both 422).
  - `services.py`: `StorageServices` + `create_storage_services(StorageWiring)`; one storage lock
    shared by janitor and rehydrator, the janitor additionally holds `PriorityScheduler.hold()`.
- **`api/storage.py`**: `GET /v1/storage`, `POST /v1/storage/cleanup` (400 on a bad body),
  `POST /v1/jobs/rehydrate` (201 + `Location`, 200 existing; malformed JSON 400; schema 422 with
  `errors`; stem set 422 `errors.stems`; 404 `type .../problems/stem-missing` + `stem`, detail
  `tracks/<id>/stems/<stem>.flac does not exist.`; 409 `type .../problems/library-unavailable`).
- **jobs**: `DataPaths(library=...)`, `library_stem`, `rehydrate_staging`, `source_pointers`;
  `StoredJob.from_library`, `cache_evicted`, `reusable` (used by `JobStore.find_active`);
  `QueueThread.hold()`; runtime builds `DataPaths` with `STEMSTUDIO_LIBRARY_DIR`.
- **api/routes.py**: `/result` and `/stems/{stem}` answer 404 for rehydrated jobs (artwork
  already 404: they have none). `POST /v1/detail-jobs` accepts a rehydrated `standardJobId`.
- **main.py**: storage router; `default_storage(runtime, settings)` -> `app.state.storage`.

Rules chosen (recorded in `docs/decisions/CTO-DECISIONS-2026-11-15.md`, 013 rows):
old job files = terminal standard jobs older than 7 days **without a live entry** (failed,
evicted, entry gone) plus their `jobs/<id>/` folder, and terminal detail jobs older than 7 days;
other lanes keep their own sweeps. Scratch = folders of terminal jobs at once, folders of no
known job after 1 h idle, expired user artwork via the artwork sweep. Rehydrated/evicted jobs are
never reused by `POST /v1/jobs`.

## Files changed

- `worker/src/stemstudio_worker/storage/{__init__,walk,users,model_catalog,meter,janitor,library,rehydrate,services}.py` (new)
- `worker/src/stemstudio_worker/api/storage.py` (new), `api/routes.py`, `main.py`
- `worker/src/stemstudio_worker/jobs/{paths,models,store,threads,runtime}.py`
- `worker/tests/storage/{builders,test_meter,test_model_catalog,test_janitor,test_rehydrate}.py` (new)
- `worker/tests/api/test_storage_routes.py`, `worker/tests/jobs/{test_paths_library,test_store_reuse,test_queue_hold}.py` (new)
- `specs/013-library-care/tasks.md` (T025-T030 ticked), `docs/decisions/CTO-DECISIONS-2026-11-15.md` (4 rows)

Commits: 7539ac7d (T025 red), 65fd89c5 (T026), ce3c3521 (T027 red), 5b70ec2a (T028),
055c38cf (T029 red), 1c241373 (T030).

## Tests and coverage

```text
cd worker
uv run pytest -q --cov=stemstudio_worker --cov-branch
  2523 passed, 3 skipped (libcairo), 10 deselected (integration)
  coverage line 100 % (10282/10282), branch 100 % (1184/1184); storage/ 100 % / 100 %
```

## Quality checks

```text
uv run ruff check src tests          All checks passed
uv run ruff format --check src tests clean
uv run mypy src tests (strict)       Success: no issues found in 375 source files
tools/quality sig_check.py worker/src worker/tests
  0 errors, 52 warnings (all pre-existing; none in the 013 files)
```

## Dependencies added

None (soundfile, numpy already direct dependencies).

## Docs impact

None user-visible from the worker itself; the backend/frontend show the storage numbers. Setup:
the worker reads stems for rehydration from the existing read-only `/library` mount (007).

## Open issues / requests

- **Residual race (accepted)**: a standard job that is running but has no `audioSha256` yet can
  hit, by audio hash, an entry the janitor removes in the same moment; its stems then answer 404
  (the queue lock stops new units, not the running one). Needs the same audio as an existing
  entry and a clean-up at that instant.
- **architect (editorial)**: data-model 7.2 says the rehydrated job has "progress 1.0, stage
  null"; `Job.progress` is 0..100 and `stage` is required, so it is 100 / "Completed". Data-model
  6.3 says job directories after 24 h; the contract says 7 days (implemented: 7 days).
- **quality-gate (T009c)**: dependency map entry for `stemstudio_worker.storage` (done in the
  quality-gate lane of this branch).
- **backend (T023)**: treat `404` from `/stems` and `409` from `POST /v1/detail-jobs` or lyrics
  alignment as a cache miss and call rehydrate once; a deleted job file (old job files) answers
  `404` on the job itself, which should also trigger rehydrate.

## Next owner

backend (T023 worker client, T031+), devops (T011), quality-gate (T009-T010).
