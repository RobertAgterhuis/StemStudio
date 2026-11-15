# Hand-off: audio-ingest / 006 polish (T056)

## What was built

- SIG refactors during the wave. `finish` was split (`_meta_fields`), `_merge_crowded` got
  `_join`, `lyricsfile._line` got `_bounds`, `select_record` became `_timed_choice` +
  `_plain_choice`, and `kind_of` became table-driven. Every unit is now <= 15 LOC, CC <= 5 and
  <= 4 parameters.
- f906905 removed the two test clones that `run_all.py` reported. `test_lyricsfile.SONG` is now
  derived from `records.LYRICSFILE`, and `records.FakeClock` no longer mirrors
  `tests/metadata/fakes.FakeClock`.
- Docstrings (Google style) are on every public module, class and function of `lyrics/`, and
  ruff `D` is clean.
- Security review fixes are in place (details in `audio-ingest-006-foundational.md`):
  - `lyricsfile` refuses YAML anchors and aliases before `safe_load` ("billion laughs").
  - The LRC `[offset:]` takes at most 7 digits and is clamped to +-600,000 ms.

## Commits (whole 006 audio-ingest sequence)

| Task | Red | Green |
|---|---|---|
| T013 -> T014 | 534c022 | 49e7d18 |
| T015 -> T016 | a427fc7 | 32e84e9 |
| Security review | e3878db | 6d92d0e |
| HO:foundational | - | 9d89c7b |
| T027 -> T028 | 2ae3899 | 91d35b0 |
| T056 (refactor, tests only) | - | f906905 |

## Files changed

`worker/src/stemstudio_worker/lyrics/{__init__,normalize,lrc,lyricsfile,interpolate,lrclib,select,lookup,fake}.py`,
`worker/tests/lyrics/**`, `docs/handoffs/audio-ingest-006-{foundational,us1,polish}.md`.
No file outside the lane was touched.

## Tests and coverage

```text
uv run --frozen pytest tests/lyrics --cov=stemstudio_worker.lyrics --cov-branch   (f906905)
  200 passed, 1 xfailed; lyrics 441 stmts, 60 branches, 100 % line and branch
uv run --frozen pytest --cov --cov-branch   (whole worker, 91d35b0; T056 changed tests only)
  1936 passed, 7 deselected, 1 xfailed; TOTAL 7109 stmts, 814 branches, 100 %
```

## Quality checks

```text
ruff check / ruff format --check / mypy --strict (src + tests)   clean
sig_check.py lyrics src + tests   19 files, 255 units, 0 errors, 0 warnings
run_all.py   885 files, 7121 units, 0 errors, 211 warnings -> exit 0
  only lyrics warning left: "lyrics is not in the dependency map"
```

## Dependencies added

None.

## Open issues / requests

1. **CTO / 005 http owner (blocking for real lookups)**: add `"lrclib.net"` to
   `metadata/http.py` `ALLOWED_HOSTS`. Details are in `audio-ingest-006-us1.md`. The strict
   conditional xfail turns into a normal passing test once the host is added.
2. **quality-gate (006:T009)**: add `lyrics` to the `dependency_check.py` dependency map. It
   imports only `config`, `ports`, `schemas`, `metadata.http` (`LenientModel`, `OnlineClient`
   types, errors) and `metadata.ratelimit` (`RateLimiter`). `metadata` must not import `lyrics`.
3. **stem-separation**: wiring notes are in the US1 hand-off (`create_lyrics_lookup(settings,
   online_client)`, network lane, fixed error texts). The alignment rule and the deletion of the
   worker's copy belong to stem-separation.

## Next owner

CTO (integration wave); stem-separation A for the T029/T030 wiring.
