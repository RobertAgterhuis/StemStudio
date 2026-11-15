# Hand-off: generative (agent B) / 009 foundational (audio + fake engine + load profiles)

009 T018-T021, done in parallel with agent A (009 T012-T017, `devices/**`, `jobs/**`). Branch
`worktree-agent-a4452d37385a6b145`, started from the Phase 0 merge `6accc47`. Agent B did not
touch `devices/`, `jobs/`, `api/` or `main.py`. The 010 part is in
`generative-B-010-foundational.md`.

## What was built

### TDD trail (red -> green)

| Task | Red (failing tests) | Green (implementation) |
|---|---|---|
| T018 -> T019 audio | `1e5bd3f` test: stem summing, range cut, labelled FLAC | `5f0a02c` feat: `audio/{levels,mix,commands,flac}.py` |
| T020 -> T021 fake engine | `39ce853` test: fake music engine, modes, load profiles | `526afbf` feat: `engines/{fake,profiles}.py` |
| extra test | - | `f260d2c` test: `SubprocessRunner` success path covered without ffmpeg on the PATH |

### `audio/` (imports only `ports`, `schemas` and `config`)

- `levels.py`: `peak`, `peak_dbfs` (floor `SILENCE_DBFS = -120`, never above 0, as `peakDbfs`
  requires), `db_to_gain`, `limit_peak(samples, ceiling_dbfs) -> (samples, limited)`. The limiter
  is a plain gain change, so it adds no distortion.
- `mix.py`: `AudioClip(samples (frames, channels) float32, sample_rate)`. `read_audio(path)`
  reads WAV/FLAC with soundfile and raises `InvalidInputError` on any failure.
  `sum_stems(clips) -> MixResult(clip, clipped)` sums 1-6 stems at unity: mono is spread to
  stereo, more than 2 channels is refused, lengths may differ by 1 frame (trimmed to the
  shortest), and different sample rates are refused (`invalidInput`). When the sum clips, it is
  limited to 0 dBFS and `clipped=True` (-> `CLIPPED_WARNING`). `add_stems` is the same sum without
  the count check and without the limiter (the remix uses it). `cut_range(clip, start, end)`
  allows an end 1 frame past the source. A later end raises `InvalidInputError` "The range ends
  after the end of the source.". `write_wav` writes 32-bit float WAV, the engine input
  (`EngineRestyle.source`).
- `commands.py`: `CommandRunner` protocol and `SubprocessRunner` (argument list, no shell, no
  stdin, timeout; a missing program gives `returncode -1`).
- `flac.py`: `TakeLabels(generator, generation_id, take_index, voice=None, voice_profile=None)`,
  `vorbis_comments(labels)` (fixed order `AI_GENERATED=true`, `GENERATOR`,
  `STEMSTUDIO_GENERATION`, `STEMSTUDIO_TAKE`, `COMMENT=AI-generated with StemStudio`, then
  `AI_VOICE` for `synthetic`/`converted` and `AI_VOICE_PROFILE`; control characters become
  spaces; lyrics are never written). `ffmpeg_flac_args(...)` builds `-map_metadata -1 -ar 48000
  -ac 2 -c:a flac -sample_fmt s32 -bits_per_raw_sample 24` plus `-metadata K=V`, with absolute
  `file:` URLs. `FlacWriter(runner).write(source, dest, labels) -> WrittenFile(path, sha256,
  bytes, duration_seconds, peak_dbfs)`: these are the facts of `GeneratedTake`. On an ffmpeg
  failure it raises `GeneratorError` (`engineError`) "The take could not be written as FLAC.",
  and the tool output goes to the log only. Also `file_sha256` (streamed) and `describe_file`.

### `engines/`

- `profiles.py`: `LoadProfile(need, cpu_offload, language_model, song_profile)` for each of the
  six `GpuNeed`s in `ports.py`; the constants stay there as Phase 0 put them. `cpu_offload` is
  true exactly when the need is `lowMemory`. The LM is loaded only for `SONG_PLANNED`.
  `load_profile(need)` raises `ValueError` for an unknown need. Helpers:
  `song_needs(thinking=)` (without `SONG_PLANNED` when `thinking` is false),
  `low_memory_needs(needs)` (the candidates for the one OOM retry; empty for `CONVERSION_NEEDS`).
  Model catalogue: `ACESTEP_MODELS` / `SOULX_MODELS` (`ModelFile(id, bytes, revision)`, ids and
  sizes as the contract examples) and `model_statuses(models, present)`. Take labels:
  `MUSIC_TAKE_MODEL` / `VOICE_TAKE_MODEL` (`TakeModel`), `MUSIC_GENERATOR_LABEL` =
  `ACE-Step 1.5 (<weights revision>)`, `VOICE_GENERATOR_LABEL` = `SoulX-Singer-SVC (<revision>)`.
- `fake.py`: `FakeMusicEngine(settings, sleep=time.sleep)` implements `MusicEngine`.
  `required_models()` returns the ACE-Step bundle with every model present. `load(use, ...)`
  reports "Loading model" 0 -> 1 and keeps the grant (`loaded_profile`); `unload()` forgets it.
  `restyle` writes one take per seed, each as long as the summed source, to
  `out_dir/take-<i>.flac`: FLAC 48 kHz stereo 24-bit, untagged. The signal is seeded noise plus
  a seed-dependent tone (`fake_take_signal`), deterministic per seed. Each take is one fake step
  of `fake_step_seconds`, split into `TICKS_PER_STEP = 4` progress callbacks with the stage
  "Generating take i of n" and overall progress that only goes up. A `Cancelled` raised from the
  callback stops the engine before any file is written. Using the engine without `load()` raises
  `GeneratorError` "The engine model is not loaded.".

### Fake modes (all modes that `config.py` parses)

| Mode | Fake engine behaviour |
|---|---|
| `ready` | normal |
| `no-gpu` | `load` raises `GpuRequiredError` (`NO_GPU_TEXT`) |
| `oom` | generating while the loaded profile is `standard` raises `GpuOutOfMemoryError`; after a reload with a `lowMemory` need it succeeds |
| `oom-twice` | every attempt raises `GpuOutOfMemoryError` |
| `busy` | engine normal; `fake_free_memory_mib("busy") = 4,096 MiB` (< `SVC_FP16` 6,144) for the fake gate; other modes 14,960 MiB of `FAKE_TOTAL_MEMORY_MIB` 16,303 |
| `slow` | each step `SLOW_FACTOR` = 10x longer |
| `unvoiced-sample` | engine normal (see 010: the pitch tracker hears no voice) |

## Files changed

- `generator/src/stemstudio_generator/audio/{__init__,levels,mix,commands,flac}.py` (new)
- `generator/src/stemstudio_generator/engines/{__init__,profiles,fake}.py` (new)
- `generator/tests/signals.py`, `generator/tests/audio/{__init__,test_mix,test_flac}.py`,
  `generator/tests/engines/{__init__,fakes,test_profiles,test_fake}.py` (new)
- `docs/handoffs/generative-B-009-foundational.md`

## Tests and coverage

Run from `generator/` at the 010 tip (`f260d2c`, which includes this checkpoint):

```text
uv run pytest -q --cov --cov-branch
171 passed in 9.59s
TOTAL  986 stmts, 0 miss, 78 branches, 0 partial  100%   (Required test coverage of 90.0% reached)
audio/commands.py 18/0  audio/flac.py 72/10br  audio/levels.py 20/4br  audio/mix.py 78/12br     all 100 %
engines/fake.py 138/16br  engines/profiles.py 43/0br                                          all 100 %
```

At the 009 green commit `526afbf`: 132 passed, 789 stmts / 64 branches, 99 % (one line, the fake
'voice' branch, was covered later by the 010 song tests). The real-ffmpeg test
(`test_real_ffmpeg_writes_24_bit_48k_stereo_with_the_tags`) runs when `ffmpeg` and `ffprobe` are
on the PATH. It ran here (ffmpeg 9.0) and checked 24-bit, 48 kHz, stereo and the tags with
ffprobe. Without ffmpeg it is skipped, and coverage does not depend on it.

## Quality checks

- `uv run ruff check .`: all checks passed. `uv run ruff format --check .`: 34 files already
  formatted. `uv run mypy` (strict): no issues in 16 source files.
- `python tools/quality/sig_check.py generator/src generator/tests`: 33 files, 262 units,
  **0 errors**, 2 warnings. Both warnings are in Phase 0 test files that agent B did not touch:
  `tests/contract/test_generator_contract.py:124`, `tests/test_config.py:31`.
- `python tools/quality/duplication.py generator/src generator/tests`: 0 clones.

## Dependencies added

None. Only numpy, soundfile and the standard library are used. **ffmpeg** is a runtime tool
(LGPL/GPL build of the image's distribution, a system dependency as in the worker).

## Open issues / requests

- **devops**: every generator image variant (`fake`, `acestep`, `soulx`) needs `ffmpeg` on the
  PATH. `FlacWriter` writes the labelled take with it. CI has ffmpeg for the real-ffmpeg test
  (it skips without it).
- **agent A (runner/api)**, binding details:
  - `FlacWriter.write(source, dest, labels)`: `dest` must differ from `source`, because ffmpeg
    cannot write in place. Write the engine's untagged `take-<i>.flac` to the final labelled path,
    then delete the untagged file.
  - Restyle input: `read_audio` each `stem` part -> `sum_stems` -> `cut_range` for `mode=range`
    (the whole sum for `inPlace`) -> `write_wav` -> `EngineRestyle.source`. If
    `MixResult.clipped` is set, put `CLIPPED_WARNING` into `warnings`. `cut_range` raises
    `InvalidInputError`; if this should be the `400 sourceTooShort` problem at request time, check
    the range against `AudioClip.duration_seconds` in the route.
  - The fake engine needs `load()` before `restyle`/`song`, and `unload()` when the queue is
    empty. OOM retry: `low_memory_needs(needs)` -> `gate.hold(...)` -> `engine.load(use)` -> the
    same call again. In mode `oom` the fake succeeds only once a `lowMemory` need is loaded.
  - Dependency rule: `jobs` may not import `engines`. `song_needs` / `low_memory_needs` are
    one-liners: re-implement them in `jobs`, inject them from `main.py`, or ask the architect to
    move them to `ports.py`.
  - Fake composition in `main.py` (agent A's): `FakeMusicEngine(settings)`.
    `fake_free_memory_mib(settings.fake_mode)` and `FAKE_TOTAL_MEMORY_MIB` feed agent A's fake
    NVML/gate (`devices/fake.py`), so `busy` works. `no-gpu` needs agent A's fake probe to report
    `kind: cpu` with the 003 reason text. The engine refuses `load` too.
- **architect/CTO**: the boxes 009 T018-T021 in `specs/009-style-transfer/tasks.md` are not
  ticked, because `specs/` is outside this lane.

## Next owner

Agent A merges this branch before 009 US1 (T032+). CTO for the integration wave.
