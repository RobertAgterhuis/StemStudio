# Hand-off: audio-ingest (agent B) / 007 foundational (007:T011-T020)

Branch from Phase 0 `d2e06a7`. Strict TDD: each task pair is a red commit (failing test) followed
by a green commit.

| Task | Red | Green |
|---|---|---|
| 007:T011/T012 mixing | `b2bcfca` | `0ae95b6` |
| 007:T013/T014 path guard | `71fb02b` | `c6dd268` (+ `a1592e3` SIG clone fix) |
| 007:T015/T016 reader | `06b7366` | `b5590bb` |
| 007:T017/T018 dither + encoders | `0966290` | `3d2b22d` |
| 007:T019/T020 executor + factories | `0890e74` | `42c5086` |

## What was built

`worker/src/stemstudio_worker/render/` (imports only `ports`, `schemas`, `config`):

- `mixing.py`: `pan_matrix(pan)` (Web Audio StereoPannerNode, stereo input; equals every
  `render-vectors.json` pan case to 1e-9) and `mix_block(blocks, inputs, master_gain)`
  (`sum(pan(x·gain)) · master`, float32). Pan 0 is computed with the `pan > 0` form (θ = 0), which
  is mathematically the same as the `pan <= 0` form but gives the **exact** identity matrix, so a
  centred unity input stays bit-identical.
- `paths.py`: `MountedLibrary(root)` implements `LibraryReader`. Allow-list first
  (`ports.is_allowed_library_path`; message `Input path is not allowed: <path>` = contract
  example), then the root must be set and listable (`os.scandir`, checked on every call; else
  `RenderError(LIBRARY_UNAVAILABLE_TEXT)`), then `resolve(strict=True)`; a real path outside the
  real root (symlink/junction) is `InputPathError`, a missing file or a directory is
  `InputNotFoundError(path)`. `status()` gives `LibraryHealth`.
- `reader.py`: `open_inputs([InputFile(name, file)])` (context manager, closes everything, also
  on a failure while opening) -> `OpenInputs.frames` (longest) and
  `blocks(FrameRange, block_frames)`; pads shorter inputs with silence, duplicates mono, rejects
  non-44.1 kHz ("Unsupported sample rate 48000 Hz in <path>"), more than 2 channels and unreadable
  files ("Cannot read <path>"). `frame_range(range, total)` rounds seconds to samples, clamps the
  end, and fails with "The range starts after the end of the inputs" when nothing is left.
- `dither.py`: `quantize(block, bits, dither)`, `TpdfDither(seed).noise()` (difference of two
  uniforms, +/- 1 LSB), `dither_seed(key)` (SHA-256 of the key), `full_scale(bits)`.
- `encoders.py`: `open_encoder(output, metadata, path)` -> `PcmEncoder` (FLAC/WAV, PCM_16/24,
  `title`/`artist`/`comment` with control characters stripped) or `Mp3Encoder` (lameenc CBR
  192/256/320, quality 2, no tags). Encoders take already quantised ints, so the written peak is
  exact.
- `executor.py`: `BlockRenderExecutor(library, block_seconds)` implements `RenderExecutor`.
  `validate()` resolves every input. `render()`: pass 1 mixes and measures the peak (progress
  0..0.5 after every block), silent -> `RenderError(SILENT_RENDER_TEXT)`, gain `s = 1` or
  `10^(-0.1/20)/peak`; pass 2 mixes again, scales, quantises (TPDF when `output.dither`, seeded
  from `out_dir.name`, i.e. the job id) and encodes (progress 0.5..1). Any exception in pass 2,
  including `RenderCancelledError` from `progress`, closes the encoder and deletes the partial
  file before re-raising. An output that cannot be created -> `RenderError("The export file
  could not be written")`. `RenderResult`: size, SHA-256 (streamed), duration, `peakDbfs` of the
  written integers (floor -150 dB if all quantise to 0), `appliedGainDb = 20·log10(s)`.
- `__init__.py`: `create_library_reader(settings)` and
  `create_render_executor(settings, library=None)`.

### For stem-separation (007:T021-T024, `/health`, routes)

```python
from stemstudio_worker.render import create_library_reader, create_render_executor
library = create_library_reader(settings)              # library.status() -> Health.library
executor = create_render_executor(settings, library)  # validate() at POST, render() in the lane
executor.render(request, data_dir / "render-jobs" / job_id, progress)  # out_dir must exist
```

`out_dir.name` must be the render job id (dither seed); the file is `out_dir/output.<ext>`.
`InputPathError` -> 400 `invalid-input-path`, `InputNotFoundError` -> 404 `input-not-found`,
other `RenderError` -> job `failed` with `str(error)`.

## Files changed

- `worker/src/stemstudio_worker/render/{__init__,mixing,paths,reader,dither,encoders,executor}.py` (new)
- `worker/tests/render/test_{mixing,paths,reader,encoders,executor}.py` (new)
- `specs/007-export-library/tasks.md` (T011-T020 ticked)
- `docs/handoffs/audio-ingest-007-foundational.md` (this note)

## Tests and coverage

```text
cd worker
uv run pytest tests/render --cov=stemstudio_worker.render --cov-branch
  119 passed;  render/  334 stmts 0 missed, 60 branches 0 partial -> 100 % line, 100 % branch
uv run pytest --cov --cov-branch                       (whole worker)
  993 passed, 3 deselected (integration); TOTAL 4200 stmts 0 missed, 468 branches 1 partial (pre-existing) -> 99.98 %
```

## Quality checks

```text
uv run ruff check src tests            All checks passed!
uv run ruff format --check src tests   158 files already formatted
uv run mypy src tests                  Success: no issues found in 164 source files (strict)
python tools/quality/sig_check.py worker/src/stemstudio_worker/render worker/tests/render
  12 files, 149 units, 0 errors, 0 warnings
python tools/quality/run_all.py
  652 files, 4957 units, 1 error, 162 warnings
```

The one `run_all` error is not in this lane and is unchanged by it:
`e2e/support/preferences.ts:12` duplicates `src/web-interop/src/types.ts:8` (16 lines). The
`render` package adds one warning: "render is not in the dependency map" (quality-gate
007:T006/T007 adds `"render": _ADAPTER`; `render/__init__.py` imports `config`, which `_ADAPTER`
allows).

## Dependencies added

None (numpy, soundfile and lameenc 1.9.0 were pinned in Phase 0).

## Deviations / open issues / requests

1. **Quantisation scale (architect, data-model.md 3)**: the data model says
   `round(y·(2^(b-1) - 1))`. That contradicts SC-004: libsndfile reads PCM as `int / 2^(b-1)`, so
   a raw 16-bit stem would come back with every sample shrunk by 1/32768 (32767 -> 32766, and
   -32768 never reachable). Implemented `clamp(round(y·2^(b-1)), -2^(b-1), 2^(b-1)-1)`, which is
   bit-identical for raw renders (tested end to end, including -32768 and 32767). Please update
   the data-model text.
2. **Pan 0**: data-model 3 assigns pan 0 to the `p <= 0` row; the code uses the `p > 0` form at 0
   (same values mathematically; exact identity in floating point). Suggest the table say
   `p < 0` / `p >= 0`. Vectors unaffected.
3. **Dither**: applied to every integer output when `output.dither` is true (16- and 24-bit, and
   the 16-bit PCM fed to LAME), as data-model 3 says; ADR-016 D4 only mentions 16-bit. The seed is
   `dither_seed(out_dir.name)` because `RenderExecutor.render` has no job-id parameter.
4. **MP3 length (T017 wording)**: lameenc writes no gapless (LAME/Xing) info that libsndfile uses,
   so the decoded file is longer than the input by the encoder delay plus frame padding (measured
   +1656 frames for 2 s). The test accepts `n <= decoded <= n + 2 frames` instead of +/- 1 frame.
5. **Extra readable errors** (not in the contract, only job `error` texts): "Cannot read <path>",
   "Unsupported channel count <n> in <path>", "The range starts after the end of the inputs",
   "The export file could not be written".
6. `peakDbfs` is floored at -150 when every written sample quantises to zero (peak > 0 but below
   half an LSB), because `-inf` is not valid JSON.

## Next owner

stem-separation (007:T021-T024 and the render routes/`/health`); quality-gate (dependency map
entry for `render`). audio-ingest B continues with 007:T064 (HO:polish).
