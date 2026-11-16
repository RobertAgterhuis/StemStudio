# Hand-off: generative / 010 US3 - own voice: sample checks and voice conversion (T039-T043)

## What was built

### Voice-sample checks (singer, CPU only, no GPU lease)

- **Endpoint** (`api/voice_samples.py`): `POST /v1/voice-samples/check` takes a multipart
  `sample` part, at most 20 MiB (otherwise `413`).
  - Container sniffing and decoding (`audio/samples.py`, see Security below), then
    normalisation to 48 kHz mono FLAC.
  - `pcmSha256` is computed over int16 LE PCM. It equals the SHA of the FLAC decoded as int16
    (tested).
  - The voiced ratio comes from the `PitchTracker` (RMVPE on the CPU, 10 ms frames).
- **Verdict**: duration is checked before voicing. `tooShort` (< 10 s), `tooLong` (> 60 s) and
  `unvoiced` (< 50 % voiced) each come with readable messages. The `unvoiced` message follows
  the contract example format.
- **Keeping the sample**: an `ok` sample is kept in `<data_dir>/samples` for exactly one
  download within 10 minutes (`api/samples.py`). Anything not registered is purged.
- **Refusals**: an unreadable sample gives `400` "The sample could not be read as audio.". The
  generator service (`voiceSampleCheck: false`) gives `409 kindNotServed`. Checks also work
  without a GPU.
- **Fake mode**: `unvoiced-sample` refuses every sample.

### SoulX-Singer-SVC (`engines/soulx_svc.py`, `engines/soulx_api.py`, `engines/rmvpe.py`)

- **Loading**: SoulX is imported lazily. `f0_extraction.py` is loaded by file path from the
  SoulX source root (ADR-019).
- **Model build**: the model is built once and kept in fp16 on CUDA (`model.half()`,
  `model.mel.float()`, `eval()`).
  - The checkpoint is SHA-256-verified, then loaded with **`torch.load(weights_only=True)`**.
    The upstream code uses `weights_only=False`; the pinned `model-svc.pt` loads with `True`.
  - While the model is built, the whisper-base hub id is mapped to the local pinned snapshot
    `soulx/whisper-base`. No hub access is made, and the patch is removed afterwards.
- **F0**: RMVPE `F0Extractor` on the SVC grid (24 kHz, hop 480), with `max_duration` set to the
  target limit + 1 s. Upstream defaults to 300 s, which would truncate long targets.
- **Checks before inference**:
  - Target longer than `STEMSTUDIO_SVC_MAX_TARGET_SECONDS`: `invalidInput`.
  - Target below 10 % voiced: `unvoicedTarget`.
  - Unreadable prompt: `promptInvalid`.
- **Inference**: `infer(...)` is called with `auto_shift`, `pitch_shift`, `n_steps`, `cfg`,
  `use_fp16=True` and seed 0.
- **Deviation**: waveforms are loaded with librosa, not SoulX's `load_wav`. That function calls
  `torchaudio.load`, which needs `torchcodec` with torchaudio 2.10, and `torchcodec` is not in
  the `soulx` extra. The result is the same: mono, resampled to 24 kHz, shape `(1, T)`.
- **Weights**: `soulx/` is shared by two pins (SVC and RMVPE) and uses one `REVISION` line per
  file (`<file> <repo> <revision>`). `WeightsDir(only_pinned=True)` checks its own lines,
  downloads only its pinned files and merges its lines into `REVISION`.

### voiceConversion kind (`jobs/conversion.py`, `api/intake.py`)

- **Intake**: exactly one `target`, exactly one `prompt` and 0-5 `accompaniment` parts. The
  target length is checked at intake (`400`).
- **Job**: needs `CONVERSION_NEEDS`. `convert`, then `VoiceRemixer.remix` (new port,
  `audio.pipeline.AudioVoiceRemixer`). Both the mix (the take) and the vocal stem are labelled
  `AI_VOICE=converted` and `AI_VOICE_PROFILE=<name>`.
  - The take is index 1, seed 0, `vocal: converted`, `hasVocalStem: true`.
  - `GET .../takes/{index}/vocal` serves the stem; it returns `404` for takes without one.
- **Cleanup**: prompt and target copies are deleted when the job ends, whatever the outcome.
- **Runner change (deviation)**: when a kind has no low-memory profile (SVC), the one OOM retry
  reuses the same need after unload and cache empty. Before, it failed at once. This matches
  agent B's fake: in mode `oom` the retry succeeds.
- **Ports (architect delegate)**: `SVC_SAMPLE_RATE`, `VoiceRemixer`/`RemixRequest`/
  `RemixedFiles`, `VoiceSampleAnalyzer`/`SampleFacts`.

### Security (CTO / automated review, fixed test-first: `bead782` red -> `bda2a92` green)

1. **Playlists and SSRF**:
   - The container is identified by magic bytes before anything decodes it. soundfile handles
     WAV, FLAC, MP3 and Ogg; ffmpeg handles WebM/Matroska, Ogg/Opus and MP4.
   - Anything else (HLS `.m3u8`, ffconcat, DASH manifests disguised with an audio extension) is
     refused as unreadable and **never reaches ffmpeg** (tested).
   - ffmpeg runs from an argument list with an explicit `-f matroska|ogg|mov` and
     `-protocol_whitelist file`.
2. **Decompression amplification**:
   - soundfile formats are measured with `soundfile.info` first. A sample longer than 61 s is
     reported `tooLong` **without decoding** (tested with a spy on `read_audio`).
   - ffmpeg decodes at most `-t 61` seconds; a longer stream then becomes `tooLong`.

## Red -> green

| Task | Red | Green |
|---|---|---|
| T039 + T041 | `978da33` | `6930dc7` (T040 + T042) |
| Security hardening | `bead782` | `bda2a92` |
| T043 | `add34c4` | see GPU measurement |

## GPU measurement (RTX 5070 Ti, throw-away container, read-only model volume with overlay)

`tests/integration/test_soulx_real.py`, run in a `soulx`-extra venv with the SoulX source at
`81aeb3a` copied to `/opt/soulx-singer`, as the image does:

- **Real RMVPE sample check (CPU)** of the qa fixture `own-voice-20s.wav`: **ok**,
  `voicedRatio` **0.901** (the qa manifest records 0.84 voiced share). It took 39.8 s,
  including the first RMVPE load and librosa on the CPU.
- **Real SVC conversion** of `sung-target-10s.wav` (target) with `own-voice-20s.wav` (prompt):
  **completed**. The output is 10.0 s, 48 kHz, with `AI_VOICE=converted`,
  `AI_VOICE_PROFILE=Fixture voice`, and `GENERATOR=SoulX-Singer-SVC (...)`. The `/vocal` stem
  returned 200. It took 19.4 s, including the model build.
- **Memory**: NVML idle was 2,434 MiB and the peak 6,141 MiB, so the delta is **3,707 MiB**.
  This is well below `SVC_FP16` 6,144 MiB, because the 10 s target is shorter than the
  research's 60 s case.
- **First attempt**: it failed at `torchaudio.load` (torchcodec missing). This led to the
  librosa loader deviation described above.

## Next owner

CTO; backend uses `pcmSha256`, `checkId` download, `hasVocalStem` and `/vocal`.
