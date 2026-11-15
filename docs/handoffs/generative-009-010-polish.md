# Hand-off: generative / 009 T074 + 010 T058 (polish) - generator service complete

The generator service implements `generator-api.openapi.yaml` 1.1.0 (it contains 009's 1.0.0)
for all three kinds and the voice-sample checks. Per-story details are in the other hand-offs:

- `generative-009-foundational-a.md`
- `generative-009-us1.md`, `generative-009-us6.md`, `generative-009-us5.md`
- `generative-010-us1.md`, `generative-010-us3.md`
- Agent B's audio and fake work: `generative-B-009-foundational.md`,
  `generative-B-010-foundational.md`

## What was built in polish

- Every unit is within the SIG limits: `sig_check generator/src` reports 0 errors and
  0 warnings. The refactors are in `ca04448` and `b4687c1`.
- Public API docstrings are complete. The runner docstring now states the OOM retry rule.
- The voice-sample hardening from the security review (`bead782` -> `bda2a92`).

## Tests and coverage

Windows, `cd generator; uv run --frozen pytest -q --cov --cov-branch`:

```text
502 passed, 4 deselected (integration)
TOTAL 3129 stmts, 27 missed, 350 branches, 6 partial
line 99.14 %, branch 98.29 %, combined 99.05 %
```

Partly uncovered on this host:

- `devices/lease.py` 86 %: the Linux `flock` branch, which cannot run on Windows.
- `main.py` 93 %: the real GPU wiring (needs torch).
- `engines/soulx_api.py`: the real imports, marked `pragma: no cover` (soulx image only).

Linux (throw-away `ghcr.io/astral-sh/uv:python3.12-bookworm-slim` with ffmpeg, no GPU):
502 passed, combined 99.02 %. The real-ffmpeg FLAC test ran. mypy --strict and ruff are clean.

Real-model integration tests, one per family, on the RTX 5070 Ti in throw-away containers. The
`stemstudio` compose containers were never touched, and the model volume was mounted read-only.

| Test | Result | NVML idle / peak (MiB) |
|---|---|---|
| `test_acestep_real.py` (10 s cover) | passed, 30.0 s cold | 2,264 / 13,139 (cover + song run) |
| `test_acestep_song_real.py` (10 s song, `thinking`) | passed, `profile=planned`, 29.2 s | same run; delta 10,875, which confirms `SONG_PLANNED` 12,288 |
| `test_soulx_real.py` (RMVPE sample check + 10 s SVC) | passed; voicedRatio 0.901; SVC 19.4 s | 2,434 / 6,141 |

## Quality checks

- `ruff check .`: all checks passed. `ruff format --check .`: clean. `mypy --strict src tests`:
  no issues in 110 files, on Windows and on Linux.
- `tools/quality/sig_check.py generator/src`: 54 files, 442 units, **0 errors, 0 warnings**.
  On `generator/tests`: 0 errors and 20 warnings (unit size in tests; one is Phase 0's
  `test_config.py`).
- `tools/quality/dependency_check.py`: 0 errors. One warning, `StemStudio.Agent` (011), not mine.
  The generator layer rule holds: `jobs`/`engines`/`devices`/`audio` import only `ports`,
  `schemas` and `config`; `api` also imports `jobs`; only `main.py` composes.
- `tools/quality/duplication.py`: jscpd exited 1 locally (tool problem). Not investigated;
  quality-gate/CTO to run it in the gate.

## Dependencies added

None in `pyproject.toml`/`uv.lock`. Runtime tools: ffmpeg (decoding of browser voice samples and
FLAC writing; already required by agent B).

## Deviations (summary)

1. **Probe texts**: the probe reason texts are identical to the worker's, including "This worker
   image ..." in `NO_CUDA_BUILD`.
2. **Ports moved/added (architect delegate)**:
   - Moved: `TakeLabels` and `WrittenFile`.
   - Added: `TakeWriter`, `StemMixer`/`MixedSource`, `SVC_SAMPLE_RATE`,
     `VoiceRemixer`/`RemixRequest`/`RemixedFiles`, `VoiceSampleAnalyzer`/`SampleFacts`.
   - Why: `jobs` and `api` reach audio code only through ports.
3. **OOM retry**: a kind without a low-memory profile (SVC) gets its one OOM retry with the same
   need, after unloading. Before, the job failed immediately.
4. **SoulX loading**: SoulX's `load_wav` needs `torchcodec`, which is not in the `soulx` extra.
   The adapter loads waveforms with librosa instead. `torch.load(weights_only=True)` is used for
   the pinned SVC checkpoint (upstream uses `False`).
5. **Voice-sample checks**:
   - ffmpeg decoding as decided by the CTO, hardened per the security review (container
     sniffing, explicit demuxer, `-protocol_whitelist file`, `-t 61`, pre-decode duration).
   - The `tooShort`/`tooLong` message texts are ours; the contract only shows `unvoiced`.
6. **T052 and US5**: T052 needed no code change, and parts of US5 already passed at red.
7. **Lease waits**: the lease wait has no limit (ADR-018 D3), and cancel ends it. Only the
   free-memory wait fails with `gpuBusy`.

## Open issues / requests

- **devops**:
  - The `generator` service needs `model-cache` **read-write**. ACE-Step rewrites
    `acestep-v15-turbo/modeling_*.py` inside the checkpoint directory when the package's model
    code differs (`_sync_model_code_if_needed`); a read-only mount fails with EROFS.
  - The singer image needs the SoulX source at `/opt/soulx-singer` with `soulxsinger/` and
    `preprocess/tools/f0_extraction.py` (as ADR-019). `librosa` is needed (already in the extra).
  - `torchcodec` is **not** needed.
- **architect** (contract wording, optional): the voice-sample description says "soundfile or
  torchaudio, never a shell". ffmpeg is now used from an argument list with a protocol and
  format whitelist.
- **qa/CTO**: the git-ignored voice fixtures (`generate_voice_fixtures.py`) must be generated
  for `tests/api/test_fake_fixture.py`'s pitch cases. Those cases are skipped otherwise.
- **CTO**: transformers 4.57 warns that `torch_dtype` is deprecated in favour of `dtype` (bf16 LM
  loader); it is harmless at the pinned version.
- Throw-away GPU volumes (`gena-venv-*`, `gena-uvcache`) were removed. The images
  `ghcr.io/astral-sh/uv:python3.12-bookworm-slim` and `alpine/git` stay cached.

## Next owner

CTO: integration wave (009 T081-T084, 010 T059-T062).
