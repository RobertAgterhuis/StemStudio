# Hand-off: stem-separation (lane L3) / 016 HO:us1

Branch `016-round-lane-l3`, not pushed, not merged. Tasks T061, T062, T063 (all ticked).

## What was built

| Task | Commits | Result |
|---|---|---|
| T061 | 11d4ee29 | Route, health and composition tests of the `instrument-analysis` lane, responses checked against worker 1.9.0 (`tests/api/contract.py`) |
| T062 | dd685fd1 | `jobs/instrument_analysis_service.py` (`LaneJobService` on its own lane `instrument-analysis`; stages `loading-model`/`preparing` -> `analysing` -> `finishing`; path guard at create, 400/404), `api/instrument_analysis_routes.py` (POST 202 + Location, GET, idempotent DELETE), `/health.instrumentAnalysis` (`chordEngine`, `pitchEngine`, BTC `models`) and the lane in `/health.lanes`; skeleton expectations in `tests/test_smoke.py` and `tests/api/test_jobs_api.py` updated. The same commit also holds the drum-hits and vocal-pitch kinds (see HO:us3, HO:us4) |
| T063 | b57d57ab, 0a9a425e, 764b2efb | `tests/harmony/test_chords_integration.py` (`@pytest.mark.integration`): real BTC weights through the 008 model store (SHA-256), `weights_only=True` + `BTC_SAFE_GLOBALS`, 8 threads; skips readably when the weights cannot be downloaded |

## T063 measurements (this PC, CPU, 8 threads)

| Measure | Result | Target |
|---|---|---|
| BTC majmin forward time | **0.0022 s per audio second** | budget 0.2 (Phase 0 estimate 0.007) |
| Raw BTC majmin frames on the 24-triad set | 81.4 % | >= 70 % |
| Chart majmin time, BTC + L2 `BeatSnapPostProcessor` (SC-001) | **85.4 %** | >= 85 % |
| Key test set, L2 `HmmKeyEstimator` (SC-010) | **17/17** | >= 90 % |

The chart and key rows were measured with L2's `harmony/{labels,postprocess,chroma_hmm,key}.py` (branch
`016-round-lane-l2`) copied into the worktree for that one run and removed afterwards (never committed). On this
branch alone the test prints "harmony.postprocess / harmony.key not installed" and checks only BTC. SC-001 passes
with 0.4 points of margin: if T139 finds it below 85 % on the reference machine, the tuning constants are in
`harmony/postprocess.py` (L2).

## Hook requests (lane plan, L2 -> L3): delivered

`main.create_instrument_analysis_parts` builds the L2 parts by module path (`jobs` may not import adapters; until
the merge the modules are not on this branch): `harmony.chroma_hmm:ChromaHmmRecognizer()` (the BTC fallback and the
`chroma-hmm` engine), `harmony.postprocess:BeatSnapPostProcessor()`, `harmony.key:HmmKeyEstimator()` +
`harmony.key.apply_key`, `analysis.vocal_pitch:LibrosaPitchAnalyzer(engine=...)`,
`analysis_inputs.mixdown:SoundfileHarmonicMixer(allowed_root=library_dir)` - the signatures of L2's
HO:foundational. Smoke with L2's files overlaid (not committed): every part is the real class (no `MissingPart`),
both BTC weights `ready`, a C major triad gives `C:maj` with key C major through BTC and through chroma-hmm, and a
220 Hz tone gives pYIN frames at 5699 cents.

## Tests and quality

Worker suite, ruff, mypy: see HO:us4 (one run for the US1-US4 checkpoints, since all four were in by then).

Security self-review: request bodies are pydantic-validated by `kind` (422); every input path passes the library
path guard at create (400 outside the allow-list, 404 missing) and is resolved again at run time; the job id of
`DELETE` is checked against 32 hex characters; BTC weights load only with `weights_only=True`.

## Decisions / deviations / requests

None beyond HO:foundational.
