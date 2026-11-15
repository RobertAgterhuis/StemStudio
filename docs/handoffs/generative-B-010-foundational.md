# Hand-off: generative (agent B) / 010 foundational (song and voice fakes, remix, normalisation)

010 T010-T013, on top of agent B's 009 work (`generative-B-009-foundational.md`), on the same
branch. Agent B did not touch `devices/`, `jobs/`, `api/` or `main.py`.

## What was built

### TDD trail (red -> green)

| Task | Red (failing tests) | Green (implementation) |
|---|---|---|
| T010 -> T011 fakes | `803c608` test: fake song engine, voice converter, pitch tracker | `28352c7` feat: `engines/fake.py` (song, `FakeVoiceConverter`), `engines/fake_pitch.py`, `profiles.song_vocal_label` |
| T012 -> T013 audio | `1050ae1` test: remix and sample normalisation | `f0158f0` feat: `audio/{resample,remix,normalise}.py` |

### Fakes (`engines/fake.py`, `engines/fake_pitch.py`)

- `FakeMusicEngine.song(request, on_progress)`: one take per seed, each
  `durationSeconds * 48000` frames (FLAC 48 kHz stereo 24-bit, untagged). Takes with vocals get a
  vibrato 'voice' tone on top of the music; instrumentals do not. A take is `planned` only when
  `request.planned` is set **and** the loaded need loads the LM (`SONG_PLANNED`). It then reports
  `PLANNING_STAGE` first. In every other case the take is `direct`, so `EngineTake.profile` shows
  what actually ran, also when the gate fell back to `SONG_DIRECT`. OOM modes as for restyle (see
  the 009 hand-off). `profiles.song_vocal_label(params)` returns `none` for an instrumental and
  `synthetic` otherwise.
- `FakeVoiceConverter(settings, sleep=time.sleep)` implements `VoiceConverter`: the singer
  models (all present), load/unload as the music fake, and `convert(request, on_progress)`. It
  returns the target's channel average, resampled to `SVC_SAMPLE_RATE` = 24 kHz mono (linear
  interpolation), scaled by 0.8, float32 and as long as the target. The output is deterministic.
  An unreadable prompt raises `PromptInvalidError`. An unreadable target raises
  `InvalidInputError`. A target below `TARGET_MIN_VOICED_RATIO` (0.1, judged by the fake pitch
  tracker) raises `UnvoicedTargetError(UNVOICED_TARGET_TEXT)`. Stage "Converting voice" (one fake
  step). In mode `oom` the first attempt per target path fails and the retry succeeds (SoulX has
  no low-memory profile); `oom-twice` fails every attempt; `no-gpu` refuses `load`.
- `FakePitchTracker(mode="ready")` implements `PitchTracker`: an autocorrelation F0 per 10 ms
  (100 frames/s, `ceil(samples / hop)` frames), searched between 60 and 1,000 Hz after the first
  zero crossing. A frame is voiced at a peak above 0.5 and RMS above 1e-3. A tone is voiced at its
  pitch (440 Hz within 2 %; 16/24/44.1/48 kHz tested); white noise and silence read as 0. It
  processes 256 frames per batch, so long targets stay within bounded memory. Mode
  `unvoiced-sample` returns all zeros, so every voice-sample check fails as `unvoiced`.
  `read_mono(path, error)` is shared by the fakes. The voice converter has its own tracker in mode
  `ready`, so mode `unvoiced-sample` does not break conversions.

### Audio (`audio/`)

- `resample.py`: `resample(samples (frames, ch), rate, target_rate)` uses numpy only (FFT per
  channel, at least a 0.1 s silent tail, padded so the output length is a whole number of frames).
  Output length is `round(frames * target / rate)`. Equal rates or an empty input return the input
  unchanged. Accuracy: 24k -> 48k tone within 2e-3, 44.1k -> 48k within 5e-3 (edges excluded).
- `remix.py`: `remix(vocal: AudioClip, accompaniment: Sequence[AudioClip], vocal_gain_db) ->
  RemixResult(mix, vocal, limited)`. The converted vocal (24 kHz mono) becomes 48 kHz stereo
  (`upsample_vocal`) and is aligned to the accompaniment length (`align_length`, silence pad or
  trim; without accompaniment the vocal's own length is used). The 0-5 accompaniment stems are
  summed at unity (`add_stems`: same rate, lengths within 1 frame) and resampled to 48 kHz, for
  example from the worker's 44.1 kHz stems. The vocal is added with `vocalGainDb` and the mix is
  peak-limited to `MIX_CEILING_DBFS` = -1 dBFS. `vocal` is the vocal stem for `getTakeVocal`,
  without gain and limited to the same ceiling. More than 5 stems raises `InvalidInputError`.
- `normalise.py`: `normalise_sample(clip)` returns 48 kHz mono float32 (channel average,
  band-limited resample). `pcm16(mono)` returns int16 little-endian bytes (x 32767, rounded,
  clipped, symmetric). `pcm_sha256(mono)` is `VoiceSampleCheck.pcmSha256` and the backend's
  `SampleSha256`. `write_sample_flac(path, mono)` writes 48 kHz mono 16-bit FLAC with exactly
  those int16 samples; decoding the file as int16 gives the same hash (tested).

## Files changed

- `generator/src/stemstudio_generator/engines/fake.py` (song, converter),
  `engines/fake_pitch.py` (new), `engines/profiles.py` (`song_vocal_label`)
- `generator/src/stemstudio_generator/audio/{resample,remix,normalise}.py` (new),
  `audio/mix.py` (`add_stems`/`STEREO` already in 009 T019)
- `generator/tests/engines/test_fake_song_svc.py`, `generator/tests/engines/fakes.py`,
  `generator/tests/audio/{test_remix,test_normalise}.py` (new)
- `docs/handoffs/generative-B-010-foundational.md`

## Tests and coverage

Run from `generator/` at `f260d2c`:

```text
uv run pytest -q --cov --cov-branch
171 passed in 9.59s
src\stemstudio_generator\audio\commands.py          18      0      0      0   100%
src\stemstudio_generator\audio\flac.py              72      0     10      0   100%
src\stemstudio_generator\audio\levels.py            20      0      4      0   100%
src\stemstudio_generator\audio\mix.py               78      0     12      0   100%
src\stemstudio_generator\audio\normalise.py         25      0      0      0   100%
src\stemstudio_generator\audio\remix.py             42      0      6      0   100%
src\stemstudio_generator\audio\resample.py          22      0      2      0   100%
src\stemstudio_generator\engines\fake.py           138      0     16      0   100%
src\stemstudio_generator\engines\fake_pitch.py      60      0      2      0   100%
src\stemstudio_generator\engines\profiles.py        43      0      0      0   100%
TOTAL                                              986      0     78      0   100%
Required test coverage of 90.0% reached. Total coverage: 100.00%
```

Generator line and branch coverage: **100 % / 100 %** (986 statements, 78 branches).

## Quality checks

- ruff check: all checks passed. ruff format --check: 34 files formatted. mypy --strict: no issues
  (16 source files).
- `sig_check generator/src generator/tests`: 0 errors, 2 warnings, both in the Phase 0 tests and
  not agent B's (see the 009 hand-off). `duplication.py`: 0 clones.

## Dependencies added

None. The resampler is numpy-only, so the base and fake image stays without scipy or librosa.

## Open issues / requests

- **agent A (010 T039+, voice-sample check route)**: the contract allows **WebM/Opus** samples
  from the browser recorder. libsndfile 1.2.2 (soundfile) decodes WAV, FLAC, MP3 and Ogg, but not
  WebM. Proposal: decode the upload with ffmpeg (argument list, `file:` URLs, as `FlacWriter`; it
  is already required in the image) to a temporary float WAV, then call `read_audio` ->
  `normalise_sample` -> `write_sample_flac` + `pcm_sha256`. The duration is
  `mono.size / SAMPLE_RATE`; the voiced ratio is `voiced_ratio(pitch_tracker.f0(<normalised
  file>))`. The contract text says "soundfile or torchaudio, never a shell"; ffmpeg from an
  argument list is not a shell, but the architect may want to confirm the wording.
- **agent A (conversion runner)**: `convert()` -> `remix(AudioClip(vocal[:, None],
  SVC_SAMPLE_RATE), [read_audio(p) for p in accompaniment], params.vocal_gain_db)`. Write both
  `mix` and `vocal` with `write_wav` + `FlacWriter.write`, using
  `TakeLabels(VOICE_GENERATOR_LABEL, id, 1, "converted", params.profile_name)` and
  `VOICE_TAKE_MODEL`. The take has `vocal: converted`, `hasVocalStem: true` and seed 0 (contract
  example). Song takes use `TakeLabels(MUSIC_GENERATOR_LABEL, id, i, song_vocal_label(params))`,
  `MUSIC_TAKE_MODEL` and `profile` from `EngineTake.profile`.
- **agent A (`main.py`)**: singer fake = `FakeVoiceConverter(settings)` +
  `FakePitchTracker(settings.fake_mode)` (the sample-check tracker takes the mode).
- **architect/CTO**: the boxes 010 T010-T013 in `specs/010-lyrics-to-song/tasks.md` are not
  ticked (outside this lane).

## Next owner

Agent A: merge this branch and continue with 009 US1 (T032+) and then 010 T022+. CTO for the
integration wave.
