# Hand-off: stem-separation / 006 US2 (T040-T043)

## What was built

- `alignment/` (new package, composed only by `main.py`):
  - `audio_prep.VocalAudioLoader`: FLAC -> mono float32, soxr to 16 kHz; unreadable/missing ->
    `VOCAL_STEM_MISSING_TEXT`, peak < -60 dBFS -> `VOCAL_STEM_SILENT_TEXT`.
  - `mapping`: stable-ts words -> our tokens (whitespace words of the non-empty lines) **by
    character position** (NFC + casefold, whitespace ignored; stable-ts may split/join words);
    a text mismatch, a refine document with other lines, or a document over the limits ->
    `ALIGNMENT_FAILED_TEXT`. Times repaired into valid spans (starts never decrease, 1..3000 ms,
    inside the line; with `refine` the refined line times are kept and words clamped into them).
    `lowConfidence` = probability < 0.2 or raw length < 20 ms. `is_collapsed`: > 35 % of words
    < 20 ms or > 25 % of lines starting before the previous line ends (`ports.COLLAPSE_*`).
  - `stable_ts_aligner.StableTsAligner`: runs inside `DeviceRunner.run(work, WHISPER_GPU_NEED)`
    (load/align OOM propagates for the runner's CPU retry); weights missing -> stage
    `Downloading speech model (1.6 GB)`, `whisper._download` into
    **`STEMSTUDIO_MODEL_DIR/whisper`** (`download_root`, never `~/.cache/whisper`; devops note
    honoured), failure -> `SPEECH_MODEL_DOWNLOAD_TEXT`; `align(..., language, original_split=True,
    vad=False, progress_callback)` (stage `Aligning lyrics`, seek/total), no language hint ->
    Whisper language detection; collapse or `None` -> one retry with `vad=True`, second ->
    `ALIGNMENT_FAILED_TEXT`; `refine` -> `align_words` with the line segments. Models cached per
    device, `release()` from the idle hook (and on reclaim for any other GPU step).
  - `onsets` (qa SC-003 finding): every aligned word start moves to the nearest vocal onset
    (librosa onset strength, hop 10 ms, backtracked) from 50 ms before to 600 ms after it.
  - `whisper_runtime.WhisperRuntime` (lazy `whisper`/`stable_whisper` imports), `weights`
    (`WhisperWeights.cached()` = file present with the pinned size), `status.AlignmentStatus`
    (`Health.lyricsAlignment`: available = stable-ts + Whisper importable (fake stack: always),
    model `large-v3-turbo`/`small`, `modelCached`, `auto`, reason "The speech alignment packages
    are not installed."), `fake.FakeAligner` (qa rule: word *k* at 2000 + 500 *k* ms, 500 ms
    long, a line spans its words; `failed` mode fails), `create_aligner`, `create_vocal_loader`,
    `alignment_status`.
- `main.py`: default `LyricsParts` = `lyrics.create_lyrics_lookup(settings, <process-wide 005
  OnlineClient>)` (shared rate limiter; real LRCLIB, or the `STEMSTUDIO_FAKE_LYRICS` provider on
  the fake stack, `disabled` with lookups off), `create_aligner`, `create_vocal_loader`,
  `model_cached`/`health` from `alignment_status`; `_GpuModels` releases the detail pipeline's
  and the aligner's models when idle, and the aligner's on a reclaim for another GPU step.
- `/health.lyricsAlignment` reports the real state; `estimatedSeconds` uses the worker device and
  `modelCached`. Health literals in `tests/api/test_jobs_api.py` and `tests/test_smoke.py`
  updated (fake stack: available, cached, reason null).
- Integration test `tests/alignment/test_align_clip.py` (`@pytest.mark.integration`) on the qa
  clip (`align-clip.json` format: `file`, `plainText`, `lines[].words[].startMs`, `sc003`).

Red -> green: T040 `8e54af1` -> T041 `cbf15e3` (+ `a9d2104` fake rule / clip format);
T042 `db2ac62` -> T043 `feaec46` (+ `e5d5f00` shared online client); SC-003 fix `80c0ad2` ->
`3f4371c`.

## SC-003 measurement (qa finding)

CPU (this Windows machine, torch 2.15.0+cpu), Whisper large-v3-turbo, stable-ts 2.19.1,
qa CC0 sung clip (28.97 s, 22 words):

| Variant | Within 300 ms | Median error |
|---|---|---|
| default stable-ts options (qa's number reproduced) | 14/22 (63.6 %) | 170 ms |
| + onset snap (nearest onset in -50/+600 ms), as shipped | **20/22 (90.9 %)** | 40 ms |

`pytest -m integration tests/alignment/test_align_clip.py -s` -> `SC-003: 20/22 word starts
within 300 ms`, 1 passed (13.8 s with cached weights). The two misses: "Morning" (first word,
placed 680 ms late) and "for" (560 ms early, the nearest onset in the window is the second
syllable of "waiting"). Caveat: the clip is synthetic (clean onsets per note); a real singer's
legato onsets are weaker, so the gain may be smaller there; the window is small enough that a
correct start is not moved far. Re-measure on the GPU in T066.

## Files changed

- `worker/src/stemstudio_worker/alignment/{__init__,audio_prep,mapping,onsets,stable_ts_aligner,whisper_runtime,weights,status,fake}.py` (new)
- `worker/src/stemstudio_worker/{main,api/routes,jobs/lyrics_runner,jobs/lyrics_lane}.py`
- `worker/tests/alignment/**` (new), `worker/tests/api/{test_lyrics_composition}.py` (new),
  `worker/tests/api/{test_lyrics_routes,test_jobs_api}.py`, `worker/tests/test_smoke.py`,
  `worker/tests/jobs/test_lyrics_defaults.py`
- `specs/006-synced-lyrics-karaoke/tasks.md` (T040-T043 ticked)

## Tests and coverage

```text
uv run pytest -q --cov=stemstudio_worker --cov-branch
2237 passed, 8 deselected in 98.36s
TOTAL 8534 stmts, 0 missed, 998 branches, 0 partial -> 100 % line and branch (whole worker)
alignment/, jobs/, api/, main.py: 100 % / 100 %
```

## Quality checks

ruff check / format --check clean; mypy --strict (304 files) clean; `sig_check.py` (alignment,
jobs, api, main.py): 56 files, 448 units, 0 errors, 0 warnings; `run_all.py`: 981 files,
**0 errors**, 240 warnings.

## Dependencies added

None new in the lock. Direct imports of locked transitive packages: `soxr` 1.1.0 (LGPL-2.1+,
https://pypi.org/project/soxr/; via librosa) in `alignment/audio_prep.py`; `librosa` (already a
direct dependency) in `alignment/onsets.py`. Weights: Whisper large-v3-turbo (MIT), first use.

## Open issues / requests

- **architect**: promote `soxr` to a direct dependency and add a mypy override for `soxr`
  (now a local `# type: ignore[import-untyped]`).
- **audio-ingest / metadata owner**: expose the process-wide online client publicly (main imports
  `metadata._shared_client` to share the 005 rate limiter with the LRCLIB lookup).
- **backend**: aligned documents carry `source: user` unless `refine` names the record; keep the
  LRCLIB credit on the backend side for aligned LRCLIB plain records.
- **CTO (T066)**: re-run the SC-003 test on the GPU image.

## Next owner

backend/frontend (alignment flow); CTO for T066.
