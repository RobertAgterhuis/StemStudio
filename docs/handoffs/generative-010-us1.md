# Hand-off: generative / 010 US1 - songs from own lyrics (T022-T026)

## What was built

- **`engines/acestep_song.py`**:
  - `song_values` builds the `text2music` parameters: caption, lyrics, `instrumental`,
    `vocal_language`, `duration`, steps and shift.
  - Planned profile: `thinking=True` and `use_cot_metas=True`; BPM, key and time signature come
    from the request, and the planner fills any gap.
  - Direct profile (DiT only): `thinking=False`, with the request's values or 120 BPM in 4/4.
  - `use_cot_caption` and `use_cot_language` are always off, so the backend's caption and the
    chosen language are kept.
- **bf16 LM loader (research S-07, `SONG_PLANNED` 12,288 MiB)**:
  - `bf16_llm(api)` subclasses ACE-Step's `LLMHandler` at runtime.
  - Its `_load_pytorch_model` calls `AutoModelForCausalLM.from_pretrained(..., torch_dtype=bf16)`
    and then `.to(cuda)`, replacing ACE-Step's fp32-then-cast loader. No upstream file changes.
  - `init_lm` initialises the LM with `backend="pt"`, on CUDA, `offload_to_cpu=False`,
    `dtype=bfloat16`. The LM on the CPU is never used.
- **`AceStepEngine`**:
  - The DiT stays loaded across needs with the same offload setting.
  - The LM is loaded only for `SONG_PLANNED`, dropped for any other need, and released together
    with the DiT on `unload`.
  - `song()` is planned only when the request asks for it **and** the LM is loaded.
  - Takes report the profile that ran (`planned`/`direct`).
  - The stage reads `PLANNING_STAGE` while ACE-Step's progress is below 0.5 (the LM phase).
- **`jobs/song.py`**:
  - `SongHandler` needs `SONG_NEEDS`; without `thinking` the needs drop `SONG_PLANNED`.
  - Takes with vocals get `vocal: synthetic` and the `AI_VOICE=synthetic` tag.
  - Instrumentals get `vocal: none` and no voice tag.
  - OOM retry: `SONG_DIRECT_LOWMEM`.
- **API**: a JSON body with `kind: song` (no files). The singer refuses it with
  `409 kindNotServed` ("This service does not generate songs; it serves voiceConversion.").
- **`main.py`**: restyle and song share one ACE-Step engine instance.

## Red -> green

| Task | Red | Green |
|---|---|---|
| T022 + T024 | `8d9a7d6` (+ `fa1fee7` completes the fake loader) | `0bcef10` (T023 + T025) |
| T026 | `06ef683` | ran green on the GPU |

## GPU measurement (RTX 5070 Ti, throw-away container, read-only model volume with overlay)

- **Run**: `tests/integration/test_acestep_song_real.py` **passed**. A 10 s song from two lyric
  lines, seed 42, `thinking=True`.
- **Profile**: the take reported **`profile=planned`**, meaning the bf16 LM fit next to the
  desktop (about 2.3 GB in use).
- **Time**: the call took 29.2 s, including the LM load, after the cover in the same process.
  The output is 10.0 s, 48 kHz, with `AI_VOICE=synthetic`.
- **Memory**: NVML peak over the cover + song run was 13,139 MiB against 2,264 MiB idle, so the
  delta is **10,875 MiB**. With the 1 GiB margin that is 11,899 MiB, below `SONG_PLANNED`
  12,288 MiB, which confirms the Phase 0 re-measure.
- transformers 4.57 warns that `torch_dtype` is deprecated in favour of `dtype`. It still works.

## Tests and coverage

See `generative-009-010-polish.md`.

## Next owner

CTO (SC-003 listening check). backend/frontend use `vocal`/`profile` from the takes.
