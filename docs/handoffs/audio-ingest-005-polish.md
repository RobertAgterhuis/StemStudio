# Hand-off: audio-ingest / 005 polish (005:T051)

Last checkpoint of audio-ingest agent A for feature 005. Earlier checkpoints:
[foundational](audio-ingest-005-foundational.md), [us1](audio-ingest-005-us1.md),
[us2](audio-ingest-005-us2.md). Those notes hold the design, deviations and requests; this note
adds the polish and the final numbers.

## What was built

- **SIG**: the last three warnings in my paths are gone. They were old test-size warnings in
  `tests/ingest`:
  - `test_fetcher.py` progress events are now a module constant;
  - `test_normalizer.py` has the `_generated_tone` helper (integration test);
  - `test_spotify.py` has a shorter `fake_spotdl` fixture.
- **Docstrings**: every public module, class, function and constant in `metadata/**` and the
  changed `ingest/**` modules is documented (ruff `D` rules, Google style).
  `metadata/__init__.py` lists the factories and modules.
- **Live-service smoke test** `worker/tests/metadata/test_live_services.py` is
  `@pytest.mark.integration` and deselected by default, with 4 tests:
  - MusicBrainz search finds the Load studio take `c8361f8a…` / `e389b7df…`;
  - the Cover Art Archive front of Load is fetched;
  - Wikidata Q15920 and its Wikipedia summary are read;
  - a full lookup of Kevin MacLeod by ISRC `USUAN1400011` finds artist `8292bf44…`.

  The tests assert identifiers and shapes only, never third-party text. **I did not run them**:
  that is the CTO's job in 005:T062, with
  `cd worker && uv run pytest -m integration tests/metadata/test_live_services.py`.

## Red -> green commits (all of audio-ingest A, 005)

| Task pair | Red | Green |
|---|---|---|
| 005:T010 / T011 | a515055 | c86b0f4 |
| 005:T012 / T013 | ef14da0 | 02559f8 |
| 005:T022 / T023 | 47646a7 | 8743145 |
| 005:T031 / T032 | 828785b, cf4439d (qa set) | 4286a12, d62599a |
| 005:T033 / T034 | 99e630f | 30b7076 |
| 005:T051 | (refactor and integration test) | 6195dd0 |

Hand-offs: 94b7596 (foundational), c40eec1 (us1), 963c937 (us2), this commit (polish). The
merge of `005-007-artwork-export` (qa fixtures) is c2d26ac.

## Files changed (T051)

- `worker/tests/metadata/test_live_services.py` (new)
- `worker/tests/ingest/{test_fetcher,test_normalizer,test_spotify}.py`
- `specs/005-artwork-band-info/tasks.md` (ticked T051), this note

## Tests and coverage

- `cd worker && uv run pytest -q --cov --cov-branch` (whole worker): **1438 passed, 7
  deselected** (the integration tests). TOTAL 5308 statements, 0 missed, 612 branches,
  1 partial (it already existed and is outside my paths).
- My component:
  `uv run pytest tests/metadata tests/ingest --cov=stemstudio_worker.metadata --cov=stemstudio_worker.ingest --cov-branch`
  gives **TOTAL 1582 statements, 0 missed, 228 branches, 0 partial: 100 % line, 100 %
  branch**, with 654 passed and 5 deselected.
- `uv run pytest -m integration tests/ingest` (real ffmpeg on a generated tone): 1 passed.

## Quality checks

- `ruff check src tests`: All checks passed! `ruff format --check`: clean.
  `mypy --strict src tests`: no issues in 201 source files.
- `uv run --project tools/quality python tools/quality/run_all.py` (whole repo, after the merge
  of `005-007-artwork-export`): **717 files, 5738 units, 28 clones, 0 errors, 162 warnings, exit
  0**. None of the warnings is in `metadata/**`, `ingest/**` or their tests.
- `sig_check.py` on `metadata/**`, `ingest/**` and their tests: 0 errors, 0 warnings.
  `duplication.py`: 0 clones.

## Dependencies added

None in all of 005 audio-ingest A. Used from Phase 0: tinytag 2.3.2 (MIT), Pillow 13.0.0
(MIT-CMU), httpx 0.28.1 (BSD-3-Clause), rapidfuzz 3.14.6 (MIT).

## Open issues / requests (consolidated)

1. **stem-separation, `main.py`**: wire
   `create_ingest_adapters(settings, metadata.create_source_details(settings))`. Without it no
   source artwork or lookup names reach the pipeline.
   - Your loader already finds `create_image_normalizer` and `create_metadata_lookup`.
   - Prefer `SourceInfo.artwork` over `FetchedAudio.artwork` (Spotify and upload jobs).
   - Take the lookup names from `FetchedAudio` for YouTube and from `SourceInfo` otherwise.
   - Take `album` from `SourceInfo.album`, else `FetchedAudio.album`.
   - Optionally enforce the 60 s per-job budget in the lane.
2. **architect**: two wording changes.
   - ADR-014 D4: the ISRC step is the `/ws/2/isrc/{isrc}` lookup.
   - data-model 2.7: add the two new warning texts, "Wikidata could not be reached." and "The
     artist details could not be loaded.".
3. **CTO 005:T062**: run the live-service test above. If MusicBrainz answers 503 often with
   the generic User-Agent, set `STEMSTUDIO_CONTACT_URL` (owner question O-1).

## Next owner

CTO (005:T058 merge; T062 live services).
