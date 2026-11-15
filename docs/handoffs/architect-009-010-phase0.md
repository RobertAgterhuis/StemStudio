# Hand-off: architect / 009 + 010 Phase 0 (contracts, generator skeleton, contract code, wave plan)

009 T001 (architect part: ADR-018 Accepted, `.claude/agents/generative.md`) and T002-T006, 010
T001-T005, executed as **one skeleton** on top of `006-008-lyrics-sheet-music` (e2826e4: 004
complete, 005/007 backend + worker, 006/008 Phase 0 and most of their worker work), so 009 and 010
run as **one combined wave**. Constitution 1.3.0 (GPU-only generative services exception) and the
CTO decisions of 2026-11-15 (fourth container `singer`, consent wording) were already in place.

## What was built

### Contract versions (docs/ROADMAP.md, binding)

| Document | 009 (`specs/009-style-transfer/contracts/`) | 010 (`specs/010-lyrics-to-song/contracts/`) |
|---|---|---|
| generator-api | **1.0.0** (new): health, restyle generations, cancel, take audio (Range), delete | **1.1.0** = 1.0.0 + `song` (JSON body), `voiceConversion` (multipart `target`/`prompt`/`accompaniment`), take vocal, voice-sample check + download, `kinds`, `voiceSampleCheck`, `kindNotServed` |
| backend-api | **1.8.0** = 008's 1.7.0 + status, styles, restyles, generations, take audio/import, `Track.origin`, library origin `generation` | **1.9.0** = 1.8.0 + lyrics sheets, songs, voice profiles + consent, voice conversions, take vocal, `GenerationStatus.voice` |
| ui-test-ids | **1.8.0** | **1.9.0** |
| worker-api / audio engine | unchanged (1.6.0 / 2.1.0) | unchanged |
| redocly.yaml | yes | yes |

The code follows the **010** documents (generator `CONTRACT_VERSION = "1.1.0"`, Contracts DTOs =
backend 1.9.0). Subset rules are tested in both stacks: generator 1.0.0 -> 1.1.0 changes exactly
the listed entries (`generator/tests/contract/test_generator_contract.py`); backend 1.7.0 -> 1.8.0
changes only `Track`, `AssetOrigin`, `Provenance`, `AudioFacts`, and 1.8.0 -> 1.9.0 only the
generation enums/schemas, `TrackOrigin`, `GenerationStatus` and their examples
(`GenerationContractTests.NewerContracts_...`). ADR-018 and ADR-019 are **Accepted** with a
"Phase 0 outcome" section; each `contracts/DELTA.md` starts with "Phase 0 outcome". The
refinements every wave agent must know:

- **Shapes**: progress is an integer 0-100; takes are numbered from 1; a generation carries its
  kind's details in the member named by the kind (`restyle`, `song`, `voiceConversion`; the others
  absent); `Track.origin` and `Provenance.generation` are absent (never null) when not applicable
  (`JsonIgnore(WhenWritingNull)` in the DTOs); takes are 48 kHz FLAC (no 44.1 kHz copy), generator
  takes report `peakDbfs`, library items of takes are kind `audio`, origin `generation`.
- **Problems**: generator `code` extension (`invalidInput`, `gpuRequired`, `sourceTooShort`,
  `kindNotServed`); backend kebab-case `type` (`not-installed`, `gpu-required`, `track-not-ready`,
  `imitation`, `style-name-taken`, `preset-read-only`, `generation-not-completed`,
  `generation-not-retryable`, `generator-unavailable`, `consent-required`, `consent-outdated`,
  `voice-name-taken`, `voice-not-installed`, `voice-unavailable`, `no-vocals`, `too-short`,
  `too-long`, `unvoiced`, `library-audio`).
- **Names**: `GenerationState` = status of a generation, `GenerationAvailability` =
  `notInstalled`/`unavailable`/`ready`, `GenerationStatus` = body of `GET /api/generation/status`;
  lyrics-sheet operations `listLyricsSheets` ... `deleteLyricsSheet` (006 owns `getLyrics`),
  DTOs `LyricsSheetDto`, `LyricsInput`, `SectionProposalDto` in `StemStudio.Contracts.Lyrics`.
- **Help topics** without the `help.` prefix (`generation-gpu-required`, `styles-library`,
  `voice-consent`, ...; lists in ui-test-ids 1.8.0/1.9.0).
- **GPU gate**: `GpuGate.hold(needs, on_wait)` admits the first `GpuNeed` of an ordered tuple that
  fits (`RESTYLE_NEEDS`, `SONG_NEEDS`, `CONVERSION_NEEDS`); `GpuNeed.profile` = `standard` /
  `lowMemory`. Values (MiB): COVER_TURBO 13,926, COVER_TURBO_LOWMEM 8,602, **SONG_PLANNED 12,288**
  (re-measured, see below; requires the LM loaded directly in bf16), SONG_DIRECT 9,830,
  SONG_DIRECT_LOWMEM 7,578, SVC_FP16 6,144.

### Phase 0 measurements (RTX 5070 Ti, throw-away containers only; `stemstudio` untouched; 14.1-14.8 GB GPU free before each run)

| Item | Result |
|---|---|
| ACE-Step on Python 3.12 | its own lock installs on `python:3.12-slim` (`uv sync --frozen --no-dev --no-install-package gradio`): 410 s, 7.6 GB venv, torch `2.10.0+cu128`, arch list incl. `sm_120`; flash-attn not installed on 3.12 (3.11-only wheel; SDPA as in the spike) |
| gradio exclusion | `import acestep.handler, acestep.inference, acestep.llm_inference` succeeds without gradio -> dropped by `override-dependencies`; the training packages stay (`acestep.handler` imports `acestep.training.trainer`) |
| LM dtype (010 T003) | the LM already runs in bf16 by default; the 15.7 GB of the spike come from ACE-Step's loader (fp32 `from_pretrained` on the CPU, then `.to(cuda).to(bf16)`). Loaded directly in bf16: NVML peak 12,347 MiB over 1,539 idle -> need 11,832 -> **SONG_PLANNED 12,288 MiB**; 60 s song 13.3 s cold / 11.4 s warm (pt backend). Default loader: 15,360 MiB. Details: 010 research.md S-07 |
| Generator lock, both GPU extras | throw-away `uv:python3.12-bookworm-slim` container, `uv sync --frozen --no-dev --extra <x>` from **our** `generator/uv.lock`: `soulx` 329 s / 7.3 GB venv, then `import torch, transformers, librosa, omegaconf, accelerate`, `LlamaDecoderLayer`, `soulxsinger.models.soulxsinger_svc.SoulXSingerSVC` (source at `81aeb3a` on the path) and RMVPE loaded from `preprocess/tools/f0_extraction.py` by file path -> OK (torch 2.10.0+cu128, transformers 4.41.2); `acestep` 7.5 GB venv, `import acestep.handler, acestep.inference, acestep.llm_inference` -> OK (transformers 4.57.6, gradio absent); `stemstudio_generator.schemas` 1.1.0 importable in both. No GPU used; container removed |

### Pre-downloaded weights (owner request via the CTO, during Phase 0)

The weights are in the **existing named volume `stemstudio_model-cache`**: the `model-cache`
volume of the `stemstudio` compose project, which the worker already mounts at `/models`. The
generator and the singer mount it at `/models` too (`STEMSTUDIO_MODEL_DIR=/models`, 009
data-model 1). They were downloaded by a throw-away `python:3.12-slim` container with
`huggingface_hub==0.36.0`. The download was anonymous and used no token. No `stemstudio`
container was touched.

The paths are defined as constants in `ports.py`: `ACESTEP_CHECKPOINT_DIR`, `SOULX_SVC_FILE`,
`RMVPE_FILE` and `WHISPER_BASE_DIR`. Each directory also holds a `REVISION` file and a
`SHA256SUMS` file (sha256sum format, every file), so the services can check the pins at start-up
and skip the download.

| Path in the volume | Source @ revision | Size | Verified SHA-256 |
|---|---|---|---|
| `acestep/checkpoints/` (ACE-Step `checkpoint_dir`) | `ACE-Step/Ace-Step1.5` @ `19671f406d603126926c1b7e2adc169acbcade22` | 10,092,108,456 B | `acestep-v15-turbo/model.safetensors` `3f6e0797...e422d7` (matches 009 research S-01); `acestep-5Hz-lm-1.7B/model.safetensors` `f161689d...cd7db8` (matches); `vae/diffusion_pytorch_model.safetensors` `da17edb6...a99b0`, `Qwen3-Embedding-0.6B/model.safetensors` `0437e45c...e23fd`, `acestep-v15-turbo/silence_latent.pt` `a778e9dd...6358b` (recorded) |
| `soulx/model-svc.pt` | `Soul-AILab/SoulX-Singer` @ `40493ad90286056c7a9095035164434a79daa8c9` | together with RMVPE and whisper-base: 3,269,928,851 B | `38e72d9e15be6eef3cc937daaf81421a7b5b546816786530bb9aa346cee5d43c` (matches 010 research S-01) |
| `soulx/rmvpe/rmvpe.pt` | `Soul-AILab/SoulX-Singer-Preprocess` @ `83dc50289d22a81b1e9998f5b9e111aef7c1fdcd` | | `6d62215f4306e3ca278246188607209f09af3dc77ed4232efdd069798c4ec193` (matches) |
| `soulx/whisper-base/` (`model.safetensors`, configs, tokenizer) | `openai/whisper-base` @ `e37978b90ca9030d5170a5c07aadb050351a65bb` | | `model.safetensors` `07cadb9f25677c8d50df603e66a98fbd842cce45047139baeb16e6219a1e807b` (new record, for THIRD-PARTY-LICENSES) |

Total: 13.36 GB. Each of the four pinned hashes matched. `hf_hub_download` also leaves a
`.cache/huggingface` folder inside each download folder; the manifests leave it out.

**For generative**: the adapters must load from these paths (ACE-Step
`checkpoint_dir=/models/acestep/checkpoints`; whisper-base `from_pretrained` on the local
directory, not the hub id). They must also check `REVISION` and `SHA256SUMS` before reporting
`present: true`.

**For devops**: compose declares `model-cache` as the shared volume for `generator` and `singer`.
### Generator skeleton (`generator/`, 009 T003 + 010 T003)

- `pyproject.toml`: Python 3.12; base = fastapi, uvicorn, python-multipart, pydantic, soundfile,
  numpy, nvidia-ml-py (no torch: fake variant and CI are OSI-only and NVIDIA-free); extra
  **`acestep`** = `ace-step @ git+...@ca1e85fe9430179831e6bc6be790c332190a3866` + torch 2.10.0 /
  torchvision 0.25.0 / torchaudio 2.10.0 from the cu128 index + the model stack pinned to
  ACE-Step's own lock (transformers 4.57.6, diffusers 0.37.1, accelerate 1.12.0, torchcodec
  0.10.0, numba 0.63.1, ...); its vendored `nano-vllm` pinned as a git subdirectory source (uv does
  not read a git dependency's sources); extra **`soulx`** = torch/torchaudio 2.10.0+cu128,
  transformers 4.41.2, tokenizers 0.19.1, accelerate 1.11.0, librosa 0.11.0, omegaconf 2.3.0,
  scipy, tqdm; the extras conflict (`[tool.uv] conflicts`) and are Linux x86_64 only; `gradio`
  dropped; `exclude-newer = 2026-11-01` (every package >= 2 weeks old; the ACE-Step commit is
  2026-08-29, the SoulX commit 2026-05-29). `uv.lock`: 172 packages.
- **SoulX-Singer packaging decision (ADR-019 Phase 0 outcome)**: path install in the singer image,
  no build shim and no `third_party/` folder: the image fetches commit `81aeb3a`, copies
  `soulxsinger/` and `preprocess/tools/f0_extraction.py` to `/opt/soulx-singer` on `PYTHONPATH`;
  the adapter loads `f0_extraction.py` by file path (the package `__init__` imports heavy tools).
- `schemas.py` (1.1.0, every schema of the contract; cross-field rules: range 10-480 s, one
  member per kind, error iff failed, ready iff GPU, sample reason iff not ok; fixed texts),
  `ports.py` (`MusicEngine` with `restyle`/`song`, `VoiceConverter`, `PitchTracker`, `GpuGate`,
  `DeviceProbe`, `JobStore`, `Clock`, value types, errors with contract codes, `GpuNeed`s, pinned
  code commits and weights revisions with SHA-256), `config.py` (engine `acestep|soulx|fake`,
  kinds with per-engine defaults, fake modes incl. `unvoiced-sample`, dirs, lease file, waits,
  SVC target limit; constants). The fake engine, `main.py` and the service are the generative
  agent's (009 T020/T021, T033).
- Tests: `tests/contract/` (round trips of every generator example, enums, problems, subset
  rule, cross-field rules), `tests/test_config.py`, `tests/test_ports.py`.

### Worker (009 T004)

`WorkerSettings.gpu_lease_file` (`STEMSTUDIO_GPU_LEASE_FILE`, unset/blank = 003 behaviour) and
`gpu_lease_wait_seconds` (`STEMSTUDIO_GPU_LEASE_WAIT_SECONDS`, 900), `GPU_LEASE_BUSY_REASON`;
tests in `worker/tests/test_config.py`.

### .NET (009 T005 + 010 T004)

- `StemStudio.Contracts`: `Styles/*` (`StyleFields` -> `StyleInput`; `StyleDefinition` ->
  `StyleDto`, `StyleSnapshotDto`; 5 enums), `Generation/*` (status, restyle/song/voice-conversion
  requests and details, `GenerationDto`, take, error, provenance; 7 enums), `Lyrics/` sheet DTOs +
  `SectionMarkerDto` + `LyricsUploadForm`, `Voices/*` (`VoiceProfileDto`, `VoiceConsentDto`,
  `ConsentStatementDto`, `VoiceSampleSourceDto`, `VoiceProfileForm`), `Tracks/TrackOriginDto` +
  `TrackDto.Origin`, `AssetOriginDto.Generation`, `ProvenanceDto.Generation`, `ApiRoutes`
  (24 routes), `ContractLimits` (generation 10-480 s, takes, style limits, caption 512, lyrics
  4,096 / title 80 / upload 64 KiB / extensions / density 4.0, song BPM/key/time signatures,
  languages, sample 10-60 s / 20 MiB, names, `consent.v1`, pitch shift).
- Tests: `Contract/GenerationContractTests.cs`, `Contract/SongVoiceContractTests.cs`; helpers in
  `ContractJson.cs` (`Backend008/009`, `GeneratorContract`, `Generator009`, `RoundTripDeep`,
  `AssertEnumsMatch`, `IsGeneratorCopyOf`); **22 generator example copies** in
  `tests/StemStudio.Api.Tests/Generator/Examples/` (multipart examples as their `params` JSON,
  `*Params.json`).

### Agreed one-time edits outside the lane

- Backend tests (consequence of the version bump): `StemStudio.Api.Tests.csproj` (main backend
  document -> 010; 008/009 backend and 009/010 generator documents linked),
  `BackendContractTests.cs` (+31 operation ids), `BackendContractTests.StemsAndMixes.cs`
  (`PendingGenerationOperations` 15 ids - backend 009; `PendingSongVoiceOperations` 16 ids -
  backend 010), `LyricsContractTests.cs` (006 -> 008 subset test now uses the linked 008 document).
- **Pre-existing red steps on the base commit e2826e4** (reproduced there from a `git archive`
  copy; not caused by Phase 0), fixed mechanically so the gate is green, in a separate commit the
  CTO can drop: `worker/tests/engraving/scores.py` (4 `type: ignore[no-untyped-call]` on music21
  calls, 2 `cast(DictionaryObject, ...)` on pypdf objects), `worker/tests/engraving/test_fonts.py`
  (monkeypatch by dotted name instead of `fonts.shutil`/`fonts.subprocess`; audio-ingest's files),
  `tools/quality/tests/test_vendor_check.py` (import order, `ruff --fix`; quality-gate's file).
- No Web page, component or existing endpoint was changed.

## Files changed

- `specs/009-style-transfer/{contracts/(generator-api.openapi.yaml, backend-api.openapi.yaml, ui-test-ids.md, redocly.yaml) new, contracts/DELTA.md, tasks.md}`
- `specs/010-lyrics-to-song/{contracts/(generator-api.openapi.yaml, backend-api.openapi.yaml, ui-test-ids.md, redocly.yaml) new, contracts/DELTA.md, research.md (S-07), data-model.md (GpuNeed), tasks.md}`
- `docs/decisions/ADR-018-generative-service-and-style-library.md`, `docs/decisions/ADR-019-lyrics-to-song-and-voice-conversion.md`
- `.claude/agents/generative.md` (new, from the 009 plan draft)
- `generator/**` (new: `pyproject.toml`, `uv.lock`, `README.md`, `src/stemstudio_generator/{__init__,ports,schemas,config}.py`, `py.typed`, `tests/**`)
- `worker/src/stemstudio_worker/config.py`, `worker/tests/test_config.py`
- `src/StemStudio.Contracts/**`, `tests/StemStudio.Api.Tests/**` (see above)
- pre-existing fixes: `worker/tests/engraving/{scores,test_fonts}.py`, `tools/quality/tests/test_vendor_check.py`
- `docs/handoffs/architect-009-010-phase0.md`

## Tests and coverage

`scripts/test-all.ps1 -ContinueOnError` (after the commits below): **all 35 steps passed** (exit 0).

```text
StemStudio.Domain.Tests        389 passed   Domain        line 98.63 %  branch 96.39 %
StemStudio.Persistence.Tests    66 passed   Persistence   line 98.35 %  branch 92.85 %
StemStudio.Web.Tests           798 passed   Web           line 99.45 %  branch 92.11 %
StemStudio.Api.Tests           797 passed   Api           line 99.46 %  branch 95.75 %
                                            Contracts     line 100 %    branch 100 %   (incl. all new DTOs/routes)
worker pytest                  2180 passed, 3 skipped, 7 deselected; 7904 stmts / 908 branches, 100 %
worker ruff / format / mypy    clean (286 files; after the pre-existing fixes)
engine vitest / web-interop    287 / 232 passed, 100 %
quality tools pytest           100 % (1037 stmts / 216 branches); ruff clean
fixtures pytest                100 %
contracts lint (008 docs)      valid
e2e lint, typecheck, playwright --list (447 tests in 34 files)   passed
quality gate run_all           1130 files, 0 errors, 257 warnings (none in a Phase 0 file)
dotnet build -c Release        0 Warning(s), 0 Error(s);  dotnet format --verify-no-changes passed
```

Not yet in `test-all` (devops adds it, 009 T010), run separately:

```text
generator uv sync --frozen; pytest --cov --cov-branch     73 passed; 468 stmts / 26 branches, 100 % line and branch
generator ruff check / ruff format --check / mypy --strict   clean (11 files / 10 source files)
worker tests/test_config.py                                  11 passed (5 new); config.py sig_check 0 warnings
Redocly 2.78.0 lint 009 (generator 1.0.0 + backend 1.8.0)    valid, 5 warnings (known 4xx-less GETs)
Redocly 2.78.0 lint 010 (generator 1.1.0 + backend 1.9.0)    valid, 7 warnings (known 4xx-less GETs)
```

## Quality checks

- SIG: `sig_check` on `generator/src`: 0 errors, 0 warnings; `tools/quality/run_all.py`: 0
  errors, no warning in a Phase 0 file (style DTO duplication removed with the abstract
  `StyleFields`/`StyleDefinition` records; contract test helpers shared).
- Generator: ruff, ruff format, mypy --strict clean (10 files).
- Public API documented (docstrings, XML docs; TreatWarningsAsErrors passes).

## Dependencies added

Generator base and dev (resolved, all published before 2026-11-01):

| Name | Version | Licence | URL |
|---|---|---|---|
| fastapi | 0.162.0 | MIT | https://pypi.org/project/fastapi/ |
| uvicorn | 0.54.0 | BSD-3-Clause | https://pypi.org/project/uvicorn/ |
| python-multipart | 0.0.33 | Apache-2.0 | https://pypi.org/project/python-multipart/ |
| pydantic | 2.14.2 | MIT | https://pypi.org/project/pydantic/ |
| soundfile | 0.14.0 | BSD-3-Clause | https://pypi.org/project/soundfile/ |
| numpy | 2.5.4 (acestep extra: 2.3.5) | BSD-3-Clause | https://pypi.org/project/numpy/ |
| nvidia-ml-py | 13.615.71 | BSD-3-Clause | https://pypi.org/project/nvidia-ml-py/ |
| dev: pytest 9.2.0, pytest-cov 7.1.0, pytest-timeout 2.4.0, ruff 0.17.3, mypy 2.4.1, httpx 0.28.1, jsonschema 4.26.0, pyyaml 6.0.3, types-pyyaml, types-jsonschema | | MIT / BSD-3 / Apache-2.0 | PyPI |

GPU extras (only in the optional images; never in CI or the default stack):

| Name | Version | Licence | Note |
|---|---|---|---|
| ace-step | 1.5.0 @ `ca1e85f` (git) | MIT | `acestep` extra; verified LICENSE (009 research S-01) |
| nano-vllm | 0.2.0 (ACE-Step subdirectory, same commit) | MIT | vendored in ACE-Step |
| torch / torchvision / torchaudio | 2.10.0+cu128 / 0.25.0+cu128 / 2.10.0+cu128 | BSD-3 / BSD-3 / BSD-2 | cu128 index; bring NVIDIA CUDA 12.8 wheels (NVIDIA licence, system dependency per ADR-011) and triton 3.6.0 (MIT) |
| transformers / tokenizers | 4.57.6 / 0.22.2 (acestep); 4.41.2 / 0.19.1 (soulx) | Apache-2.0 | |
| diffusers 0.37.1, accelerate 1.12.0 (soulx 1.11.0), safetensors 0.7.0, huggingface-hub 0.36.0, peft 0.18.1, modelscope 1.34.0 | | Apache-2.0 | |
| torchcodec 0.10.0, torchao 0.16.0 | | BSD-3 | |
| vector-quantize-pytorch 1.27.20, einops 0.8.2, lightning 2.6.1, lycoris-lora 3.4.0 | | MIT / MIT / Apache-2.0 / Apache-2.0 | |
| numba 0.63.1, scipy 1.17.0 (soulx 1.18.1), librosa 0.11.0, omegaconf 2.3.0 | | BSD-2 / BSD-3 / ISC / BSD-3 | |
| matplotlib 3.10.8, tensorboard 2.20.0, pywavelets 1.9.0, typer-slim 0.21.2 | | Matplotlib (PSF-based) / Apache-2.0 / MIT / MIT | ACE-Step requirements, not used by the generator |
| SoulX-Singer source | commit `81aeb3a` (image build, not a Python dependency) | Apache-2.0 | ADR-019 Phase 0 outcome |

The complete transitive list of each image comes from `uv pip list` in the image (CTO, 009 T083 /
010 T061). Weights (first-use downloads, never committed, pinned in `ports.py`): ACE-Step 1.5
bundle (MIT, rev `19671f4`), SoulX-Singer `model-svc.pt` and RMVPE (Apache-2.0), whisper-base
(Apache-2.0). No NuGet or npm change.

## Combined 009 + 010 wave (execution plan)

Every agent starts from the Phase 0 commit in its own worktree. Task ids are written
`009:T0nn` / `010:T0nn`; hand-offs `docs/handoffs/<owner>-009-<checkpoint>.md` /
`<owner>-010-<checkpoint>.md`. All owners can start at once: every cross-owner dependency is
replaced by a fake (generator stub handler with the example copies, fake API client, fake engine,
tmp lock files); the only real hand-overs happen in the CTO's integration wave.

| Owner | Agents | Sequence | Why |
|---|---|---|---|
| generative (new role) | **2, partly parallel** | A (devices + jobs + api): 009:T012 -> T013 -> T014 -> T015 -> T016 -> T017 -> HO:foundational (009, devices/jobs part). B (audio + engines) **in parallel**: 009:T018 -> T019 -> T020 -> T021 -> 010:T010 -> T011 -> T012 -> T013 -> HO:foundational (009 audio/engines part + 010). Then **one agent (A, after merging B)**: 009:T032 -> T033 -> T034 -> T035 -> T036 -> HO:us1 -> T051 -> T052 -> HO:us6 -> T068 -> T069 -> HO:us5 -> 010:T022 -> T023 -> T024 -> T025 -> T026 -> HO:us1 -> T039 -> T040 -> T041 -> T042 -> T043 -> HO:us3 -> 009:T074 + 010:T058 (HO:polish) | A owns `devices/**`, `jobs/**`; B owns `audio/**`, `engines/fake.py`, `engines/profiles.py` (disjoint files). From US1 on, `api/**`, `jobs/runner.py`, `engines/acestep_engine.py`, `engines/fake.py` and `main.py` are edited by both features, so one agent. The song LM loader override (bf16 at `from_pretrained`) is part of 010:T022/T023 |
| stem-separation | 1 | 009:T022 -> T023 -> HO:foundational -> T075 (HO:polish) | independent; 010 has no stem-separation task |
| backend | 1, sequential (+1 optional for pure domain) | 009:T024 -> T025 -> T026 -> T027 -> T028 -> T029 -> T030 -> T031 -> HO:foundational -> T037 -> T038 -> T039 -> T040 -> T041 -> T042 -> HO:us1 -> T046 -> T047 -> HO:us2 -> T053 -> T054 -> HO:us6 -> T058 -> T059 -> HO:us3 -> T063 -> T064 -> HO:us4 -> T070 -> T071 -> HO:us5 -> T076 (HO:polish) -> 010:T018 -> T019 -> T020 -> T021 -> HO:foundational -> T027 -> T028 -> T029 -> T030 -> HO:us1 -> T034 -> T035 -> HO:us2 -> T044 -> T045 -> T046 -> T047 -> T048 -> T049 -> HO:us3 -> T053 -> T054 -> HO:us4 -> T058 (HO:polish). Optional second agent **in parallel**: 010:T014 -> T015 -> T016 -> T017 (`Domain/Lyrics/{LyricsSheet,LyricsNormaliser,LyricsSections,LyricsDensity}.cs`, `Domain/Voices/**`, `Domain/Generation/{SongRequest,VoiceConversionRequest,VocalLabel,SongCaptionBuilder}.cs` - new files only; `SongCaptionBuilder` needs 009's `StylePromptBuilder`, so T016/T017 start after 009:T025 is merged into its branch) | two migrations (`AddGenerativeStyles`, `AddLyricsAndVoices`) share the DbContext and the model snapshot; both features extend the generator client (two named clients in 010), the sync service, the composition root and `ApiFactory`. Un-stage `PendingGenerationOperations` / `PendingSongVoiceOperations` per story. `Domain/Lyrics/` already holds 006's synced-lyrics types: keep the 010 types in new files |
| frontend | 1, sequential | 009:T043 -> T044 -> HO:us1 -> T048 -> T049 -> HO:us2 -> T055 -> T056 -> HO:us6 -> T060 -> T061 -> HO:us3 -> T065 -> T066 -> HO:us4 -> T072 -> T073 -> HO:us5 -> T077 (HO:polish) -> 010:T031 -> T032 -> HO:us1 -> T036 -> T037 -> HO:us2 -> T050 -> T051 -> HO:us3 -> T055 -> HO:us4 -> T058 (HO:polish) | both edit `IStemStudioApiClient` + fake, the nav rail, the track page, the help topics; 010 reuses 009's take list, player and badges. 004 is complete on this branch, so nothing waits for a UI merge. New ids and help topics: ui-test-ids 1.8.0/1.9.0 |
| qa | 1, sequential | 009:T011 -> T045 -> HO:us1 -> T050 -> HO:us2 -> T057 -> HO:us6 -> T062 -> HO:us3 -> T067 -> HO:us4 -> T078 (HO:polish) -> 010:T009 -> T033 -> HO:us1 -> T038 -> HO:us2 -> T052 -> HO:us3 -> T056 -> HO:us4 -> T057 -> HO:us5 -> T058 (HO:polish) | shared `TEST-STRATEGY.md`, page objects and the `@generative` project; specs are authored against the fake stacks and executed by the CTO |
| devops | 1, sequential | 009:T009 -> T010 -> 010:T008 -> HO:foundational -> 009:T079 + 010:T058 (HO:polish) | one `docker/generator.Dockerfile` with `GENERATOR_VARIANT=fake|acestep|soulx`, one `docker-compose.generative.yml` (`generator` + `singer`), fake compose, CI generator job, `scripts/test-all.*` + `generative-smoke.*` |
| quality-gate | 1, sequential | 009:T007 -> T008 -> 010:T006 -> T007 -> HO:foundational -> 009:T080 + 010:T058 (HO:polish) | both extend `dependency_check.py`/`components.py`; the generator is **not** in the gate yet (`common.DEFAULT_ROOTS`, `components.py`, coverage) - T007/T008 add it |
| audio-engine, audio-ingest, beat-analysis | - | no tasks | |
| CTO | - | merge order: this Phase 0 -> owner branches (generative B before A's US1 work; backend optional domain branch into backend) -> 009:T081 + 010:T059 (merge, gates, Playwright default + `@generative` on the fake stacks) -> 009:T082 + 010:T060 (reference machine) -> 009:T083 + 010:T061 (licences, README) -> 009:T084 + 010:T062 (listening checks, analyze, STATUS, PR) | |

Parallel at the start: generative A + B, stem-separation, backend (+ optional domain agent),
frontend, qa, devops, quality-gate (8-9 agents, disjoint paths).

## Open issues / requests

- **devops**:
  - `generator.Dockerfile`: `uv sync --frozen --no-dev` + `--extra acestep` (variant `acestep`)
    or `--extra soulx` (variant `soulx`; plus the SoulX source at commit `81aeb3a` into
    `/opt/soulx-singer`, `PYTHONPATH`) or no extra (variant `fake`: no torch, apply the 003
    CPU-image guard). Use a multi-stage build and a uv cache mount: the naive 3.12 spike image was
    26.2 GB on disk (9.1 GB content). `STEMSTUDIO_GENERATOR_KINDS` per service; the singer reads
    the same `model-cache` volume (`/models`).
  - `scripts/test-all.{ps1,sh}` and CI: add the generator (uv sync, ruff, ruff format, mypy,
    pytest + 90 % gate) and lint `specs/010-lyrics-to-song/contracts/{backend,generator}-api`
    (+ `specs/009-style-transfer/contracts/generator-api`) next to the 008 worker document
    (`CONTRACTS_DIR` covers one folder today).
  - The 009/010 GPU spikes left BuildKit build cache entries on the machine (the spike image was
    deleted; `docker builder prune` was not run on purpose because the cache is shared).
- **quality-gate**: add `generator/` to `DEFAULT_ROOTS`, the component map and the generator layer
  rule (009 T007/T008, 010 T006/T007).
- **generative**: implement the LM bf16 loader override (or use `SONG_PLANNED` 15,360 MiB); keep
  ACE-Step's training packages (import-time dependency); `f0_extraction.py` by file path.
- **backend**: un-stage the 31 operation ids per story; `TrackDto.Origin`/`ProvenanceDto.Generation`
  stay null for imported tracks and exports; `AudioFactsDto.SampleRate` is 48000 for take items.
- **stem-separation / CTO**: `worker/uv.lock` contains `fastapi 0.163.0`, published 2026-11-14
  (less than two weeks before Phase 0; found while locking the generator, which resolves 0.162.0).
  Not changed here (worker lock is the stem-separation/devops lane).
- **CTO**: the separate "pre-existing fixes" commit (worker mypy, quality ruff) can be dropped if
  the owners fix them differently; THIRD-PARTY-LICENSES from the tables above and the images.

## Next owner

The combined wave above (start from the Phase 0 commit); CTO for the integration wave.
