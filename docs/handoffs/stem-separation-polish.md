# Hand-off: stem-separation / polish

## What was built
- T116: every public module, class and function in `separation/`, `jobs/`, `api/` and `main.py` has a
  Google-style docstring (enforced by ruff `D`, Google convention; clean). No functional changes since
  `stem-separation-us1.md`; that note holds the full description and red -> green table.

## Files changed
- `docs/handoffs/stem-separation-polish.md`, `specs/001-stemstudio-mvp/tasks.md` (T116 ticked).
- All stem-separation files (whole wave): `worker/src/stemstudio_worker/separation/{__init__,fake,flac_writer,demucs_separator,demucs_runtime}.py`,
  `worker/src/stemstudio_worker/jobs/{models,store,paths,progress,queue,cache,runner,service,reconcile,runtime}.py`,
  `worker/src/stemstudio_worker/api/{problems,dependencies,uploads,routes}.py`, `worker/src/stemstudio_worker/main.py`,
  `worker/tests/{separation,jobs,api}/**`.

## Tests and coverage
- Unit + contract: `uv run pytest tests/separation tests/jobs tests/api --cov=stemstudio_worker.separation
  --cov=stemstudio_worker.jobs --cov=stemstudio_worker.api --cov=stemstudio_worker.main --cov-branch`
  -> 116 passed, 1 deselected; 743 statements / 78 branches, **100 % line, 100 % branch**; ~4.6 s.
- Integration (`uv run pytest -m integration -s`): 1 passed. Cold: 6.2 s separation call including the
  first-time download of `htdemucs_6s` (53 MB, Hugging Face Hub `adefossez/HTDemucs-6s`, into `HF_HOME`);
  warm: 3.4 s for the 6 s synthetic clip on CPU (torch 2.15.0+cpu). Offline with `CI=true` it fails as
  required; offline locally it skips.

## Quality checks
ruff check: all passed; ruff format --check: 50 files formatted; mypy --strict: no issues in 50 files;
production units <= 15 LOC, CC <= 5, <= 4 parameters (see us1 note).

## Dependencies added
None.

## Open issues / requests
See `stem-separation-us1.md` items 1-7 (architect: placeholder test and Spotify-resolution contract note;
devops: model cache volumes and download behaviour).

## Next owner
CTO (review, merge in T119 order, integration wave T120/T122).
