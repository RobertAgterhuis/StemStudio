# Hand-off: stem-separation / 005 US3 (T044, T045)

## What was built

- `api/artwork_routes.py`: `POST /v1/artwork` (multipart part `file`, at most 10 MiB =
  10,485,760 bytes enforced while streaming and on the declared `Content-Length`) -> the injected
  `ImageNormalizer` with `ArtworkCandidate("user", data=...)` on a thread -> 201 `ArtworkUpload`
  + `Location: /v1/artwork/<id>/cover`. Errors (problem+json, contract titles): 400 "Not an
  image" (`NotAnImageError`, any other `ArtworkError`), 400 "Invalid request" (no part `file`,
  not multipart, empty file, malformed multipart), 413 "Picture too large", 415 "Unsupported
  picture type", 422 "Picture too large" (> 40 MP). `GET /v1/artwork/{id}/{variant}`: JPEG with
  `ETag`; 404 for unknown/invalid ids and variants.
- `jobs/user_artwork.py` `UserArtworkStore`: `artwork/<id>/{cover,thumb}.jpg` + `info.json`
  (info + creation time), written atomically; swept after one hour by the runtime sweeper
  (damaged entries too).
- `api/multipart.py` `parse_limited_form(request, limit, too_large)`: the streaming size limit
  moved out of `api/uploads.py`, shared by audio and picture uploads (behaviour of `/v1/jobs`
  unchanged).
- `jobs/artwork_files.write_artwork(..., extra)` writes `info.json` inside the atomic rename.
- `Lanes` now also carries `artwork` (the store) and `normalizer`.

Red -> green: T044 `23733fa` -> T045 `bba1f2d`.

## Files changed

- `worker/src/stemstudio_worker/api/{artwork_routes,multipart}.py`, `jobs/user_artwork.py` (new)
- `worker/src/stemstudio_worker/api/uploads.py`, `jobs/{artwork_files,artwork,paths,runtime}.py`, `main.py`
- `worker/tests/api/test_artwork_upload.py`, `worker/tests/jobs/test_user_artwork.py` (new)

## Tests and coverage

```text
uv run pytest -q --cov --cov-branch            985 passed, 3 deselected
TOTAL 4518 stmts, 0 missed, 498 branches, 1 partial (pre-existing scheduler.py 142->exit)
jobs + api + main.py: 1883 stmts, 0 missed, 246 branches, 1 partial -> line 100 %, branch 99.6 %
```

## Quality checks

ruff check / format --check clean; mypy --strict (172 files) clean; sig_check (jobs, api, main +
tests): 0 errors, 19 warnings (pre-existing).

## Dependencies added

None.

## Open issues / requests

- **Deviation (architect to confirm)**: a worker without an image normaliser (no `metadata`
  package) answers `POST /v1/artwork` with **503** "Pictures unavailable"; the contract declares
  no 503 for this route. After the 005 merge the normaliser always exists, so this only matters
  for a partial build; the backend already maps worker 5xx to its 503.

## Next owner

stem-separation continues with 005:T052 (polish).
