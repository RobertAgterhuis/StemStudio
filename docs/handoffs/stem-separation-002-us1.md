# Hand-off: stem-separation / 002-us1 (automatic detail stems)

## What was built

- `detail/steps.py` `DetailPipeline`: planning reads each standard stem once; `bass` ->
  `not-supported`, gated parents -> `skipped-silent` (both checkpointed at once); steps drums,
  vocals and the instrument group (one model call on the sum of the active other/guitar/piano,
  families re-assigned by power masks; a single active parent runs directly); 4-stem fallback
  works on vocals/drums/other. `RegistrySource` maps drums -> MDX23C DrumSep, vocals -> UVR KARA 2,
  instruments -> Mega 53 with the D-04 check policies. Progress weights 0.20/0.05/0.74 (+ 0.01
  finalise in the runner), renormalised over the steps that run.
- `detail/split.py` + `detail/checkpoint.py`: per parent: sub-stem gate -> `nothing-further` /
  model residual or double count -> `not-reliable` -> rest on the 16-bit grid -> FLACs ->
  reconstruction check of the written files -> `split` (files discarded when not reliable);
  written into `<parent>.tmp/` and renamed; checkpoints carry the `separatorVersion`.
- `detail/fake.py`: `STEMSTUDIO_SEPARATOR=fake` -> per-parent fake models (no sum run, model id
  `fake-detail`, no downloads), outcome table drums/vocals/other split, guitar nothing-further,
  piano not-reliable, bass not-supported; `fake_detail_step_seconds` per model call in 4 chunks.
- `detail/__init__.py`: `create_detail_adapters` (fake provider or `RealModelProvider`) and
  `create_detail_pipeline` (the factory the composition root uses).
- `detail/demix.py` (MSST overlap-add, only kept outputs accumulated: 8 for Mega 53),
  `detail/mdx_net.py` (KARA 2 STFT wrapper, compensation 1.065, `lead = input - backing`),
  `detail/runtimes.py` (verified download, `torch.load(weights_only=True)`, ONNX CPU session,
  lazy imports: fake mode never imports onnxruntime or the vendored code).
- `detail/vendor/msst/`: `bs_roformer.py`, `attend.py`, `mdx23c_tfc_tdf_v3.py` from ZFTurbo MSST
  commit `1f700c386e0dd763fe0a57242bb9420dce987354` with header, MIT `LICENSE` and `NOTICE`
  (beartype removed, relative import, `prefer_target_instrument` replaced), `configs.py` with
  the two YAML configs as constants (their SHA-256 in the header).
- `jobs/detail_runner.py`: units start (cache hit completes at once, SC-007; standard stems
  missing -> fixed text) -> one unit per step ("Splitting drums (1 of 3)", `currentParent`) ->
  finalise (`result.json` written into `detail.partial`, renamed to `detail/` in one step);
  errors -> fixed texts (memory, ENOSPC, download texts, `DetailSeparationError` text, else
  "Unexpected error" + log); cancel honoured at the next progress report.
  `jobs/detail_progress.py` (monotonic tracker, terminal jobs immutable), `jobs/detail_cache.py`
  (lookup per `separatorVersion`, URL rebinding), `jobs/detail_ports.py` (pipeline protocols, so
  `jobs` never imports `detail`), `reconcile_detail_jobs` (non-terminal -> "interrupted by
  restart", checkpoints kept, `*.tmp` removed).
- `api/detail_routes.py`: `/v1/detail-jobs` (200/202 + `Location`, 400/404/409), `/{id}`,
  `/{id}/cancel`, `/{id}/result`, `/{id}/stems/{parent}/{name}` (FLAC, 404 for names not
  offered, 409 not completed); shared `api/responses.py`. `main.create_app(..., detail_pipeline)`
  wires `WorkerRuntime.start(settings, adapters, pipeline)` (detail store, service, runner on the
  priority scheduler).

## Red -> green commits

| Tasks | Red | Green |
|---|---|---|
| T040 / T041 steps | `f8a9214` | `ef42702` (+ `0c95009` branch test) |
| T042 / T043 fake models, factories | `5f564c5` | `52fe51f` |
| T044 / T045 runner, reconcile | `e9a6e7f` | `96d345a` (+ refactor `110f7ef`) |
| T046 / T047 routes, wiring | `bebd848` | `0dac1b8` |
| T048 / T049 runtimes, vendored code | `b9a74d9` | `9111c73` |
| T050 integration test | - | `11df75f` |
| SIG refactor | - | `7399f54` |

## Files changed

- `worker/src/stemstudio_worker/detail/{steps,split,checkpoint,fake,demix,mdx_net,runtimes,__init__}.py`,
  `detail/vendor/{__init__.py, msst/{__init__,attend,bs_roformer,mdx23c_tfc_tdf_v3,configs}.py, msst/LICENSE, msst/NOTICE}`
- `worker/src/stemstudio_worker/jobs/{detail_runner,detail_progress,detail_cache,detail_ports}.py`,
  `jobs/{reconcile,runtime,detail_service}.py`
- `worker/src/stemstudio_worker/api/{detail_routes,responses}.py`, `api/routes.py`, `main.py`
- tests: `worker/tests/detail/{models,test_steps,test_fake,test_runtimes,test_detail_integration}.py`,
  `worker/tests/jobs/{detail_fakes,test_detail_runner}.py`,
  `worker/tests/api/{test_detail_routes,conftest,contract,test_app}.py`

## Tests and coverage

```text
cd worker && uv run pytest --cov
633 passed, 3 deselected; total line 99.93 % (2987/2989), branch 99.13 % (341/344)
stem-separation component (detail, jobs, api, separation, main; vendor omitted per ADR-010):
line 99.90 % (2098/2100), branch 98.75 % (237/240)

uv run pytest -m integration tests/detail/test_detail_integration.py -s
1 passed in 46.7 s (models cached; first run downloaded 1,859,359,312 B and verified SHA-256)
detail wall time 45.8 s: drums 10.1 s, vocals 3.0 s, guitar+other (one Mega 53 sum run) 32.6 s
on the 10 s synthetic band: drums split into kick/snare/toms/hihat + rest (reconstruction
-160 dB), vocals/guitar/other "nothing further" (single synthetic voices), piano skipped, bass
not supported; every split parent <= -30 dB.
```

The API contract tests now validate against `specs/002-detail-separation/contracts/worker-api.openapi.yaml`
(T046); all 001 contract tests stay green against 1.1.0.

## Quality checks

```text
ruff check / ruff format --check / mypy --strict (src + tests): clean
uv run --project tools/quality python tools/quality/run_all.py
  quality gate: 415 files, 3121 units, 20 clones, 17 errors, 130 warnings -> exit 1
  all 17 errors are in worker/src/stemstudio_worker/detail/vendor/msst/** (vendored MIT code,
  excluded from SIG by plan.md Complexity Tracking; the exclusion is quality-gate T006/T007).
  Non-vendor worker code: 0 errors; new soft warnings only demix (17 LOC) and main.create_app
  (16 LOC).
```

## Dependencies added

No new packages (onnxruntime, rotary-embedding-torch were added in T002). Vendored code:

| Name | Version | Licence | URL |
|---|---|---|---|
| ZFTurbo Music-Source-Separation-Training (bs_roformer, attend, mdx23c_tfc_tdf_v3) | commit 1f700c386e0dd763fe0a57242bb9420dce987354 | MIT | https://github.com/ZFTurbo/Music-Source-Separation-Training |
| MDX-Net STFT wrapper (own code after python-audio-separator) | - | MIT (credit) | https://github.com/nomadkaraoke/python-audio-separator |

Model weights (downloaded at first use, never committed): research.md D-07 rows, unchanged.

## Deviations / open issues / requests

1. **Out-of-lane edit (architect file)**: `worker/tests/test_detail_contract.py::test_detail_factory_is_a_stub_until_the_wave`
   asserted `NotImplementedError`; it is replaced by `test_detail_factory_returns_a_model_provider`
   (3 lines) in `9111c73`, because implementing T043/T049 necessarily breaks it. Please confirm.
2. **quality-gate**: exclude `detail/vendor/**` from `sig_check`, `duplication` and the
   dependency check, and add `detail` to the dependency map (T006/T007). Until then `run_all.py`
   exits 1 on vendored code only.
3. **Integration fixture**: T050 generates its own 10 s synthetic standard stems in the test
   (the qa fixture `detail_10s_band.wav` from T010 is a mix and not in this worktree; running
   Demucs first belongs to the other integration test). qa can swap the input when T010 lands.
4. `result.json` is written into `detail.partial/` and the directory is renamed to `detail/` in
   one step (instead of renaming first and writing `result.json` last), so a result never
   exists without its stems; same guarantee as data-model 2.3.
5. Fake mode runs the instrument parents one by one (`fake-detail`), because the fake standard
   separator copies the mix into every stem; the real registry source sums them (ADR-010).
6. Stage "Downloading detail model" is not reported separately: the first `get()` of a model
   downloads it inside the parent step (stage stays "Splitting <parent> (i of n)").
7. `torch.backends.cuda.sdp_kernel` FutureWarning comes from the vendored `attend.py` (kept
   verbatim).

## Next owner

stem-separation continues with US3 (T065, T066). backend can integrate against the worker API
1.1.0 implementation; the fake worker (`STEMSTUDIO_SEPARATOR=fake`) serves detail jobs.
