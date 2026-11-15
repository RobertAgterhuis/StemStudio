# Hand-off: stem-separation / 007 polish (T065) + 005 wiring after the audio-ingest merge

## What was built

- T065: module/unit docstrings of `jobs/runtime.py` and `jobs/render_service.py` (`c544513`);
  the queue-thread clone refactor was already done in 005:T052 (`QueueThread`), so the render
  lane adds no duplication (`run_all.py` 0 errors).
- **Merged `005-007-artwork-export` again** (`baf79dc`: audio-ingest 005 `metadata/` and
  `ingest/`; conflict only in `specs/005-artwork-band-info/tasks.md` checkboxes, resolved by
  keeping both owners' ticks).
- **Wiring as the CTO asked** (red `062816a` -> green `dc95bc4`):
  - `default_adapters` passes `metadata.create_source_details(settings)` to
    `create_ingest_adapters(settings, details=...)`.
  - `create_image_normalizer(settings)` and `create_metadata_lookup(settings)` are imported
    statically (one shared rate-limited client inside `metadata`); the dynamic
    `optional_factory` lookup of 005:T025/T036 is removed now that the package exists.
  - Artwork order: candidates are ranked user > spotify > embedded > coverartarchive >
    youtube, i.e. `SourceInfo.artwork` (Spotify cover / embedded cover) always before
    `FetchedAudio.artwork` (the YouTube thumbnail a Spotify job's download also offers); the
    next one is tried if the first fails. Lookup names for YouTube jobs: `FetchedAudio`, else
    the `SourceInfo` values (`jobs/models.fetched_metadata`); Spotify/upload keep `SourceInfo`.
  - Fake stack check: `test_the_fake_stack_normaliser_gives_jobs_a_picture` and
    `test_the_fake_stack_lookup_finds_the_song` run the real `metadata` fakes through the app.

## Summary of feature 007 (stem-separation)

| Task | Red | Green |
|---|---|---|
| T021/T022 render lane + queue health | `ff1d5a8` | `5ea32b0` |
| T023/T024 render job store | `595b995` | `1180fe1` |
| T033/T034 render routes, `/health` library + lanes, composition | `7328168` | `efed344` |
| T065 polish | - | `c544513` |

## Tests and coverage (merged tree, after the wiring)

```text
uv run pytest -q --cov --cov-branch            1596 passed, 7 deselected (integration)
TOTAL 6167 stmts, 0 missed, 718 branches, 0 partial -> 100 % line, 100 % branch (whole worker)
jobs + api + main.py: 2090 stmts, 0 missed, 262 branches, 0 partial -> 100 % / 100 %
```

## Quality checks

ruff check / format --check clean; mypy --strict (230 files) clean; sig_check (jobs, api,
main + tests): 0 errors, 19 warnings (pre-existing); `run_all.py` (whole repo, 746 files):
**0 errors**, 163 warnings.

## Dependencies added

None.

## Open issues / requests

- architect: `POST /v1/artwork` answers 503 when a runtime has no normaliser (only possible for a
  runtime built without `OnlineParts`, not via `create_app` any more); keep or drop at your call.
- CTO: this branch already contains the audio-ingest 005 and 007 merges; merging it into
  `005-007-artwork-export` should be conflict-free apart from tasks.md ticks.

## Next owner

CTO (integration waves 005:T058 / 007:T071); backend consumes the worker routes.
