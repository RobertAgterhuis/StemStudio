# Implementation Plan: Own Lyrics to Song (Music + Voice)

**Branch**: `010-lyrics-to-song` | **Date**: 2026-11-15 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification `/specs/010-lyrics-to-song/spec.md`; constitution (1.3.0 after
the 009 amendment); research `docs/research/roadmap-003-011.md` sections 8-9; feasibility spike
[research.md](research.md); ADR-018 (009), ADR-019 (this feature).

**Note**: Produced by the architect agent (`/speckit-plan` equivalent). 010 starts after 009 is
merged and reuses its generator service, GPU gate, job/take/library/import flows, style library
and text guard. Contract versions are assigned at feature start
([contracts/DELTA.md](contracts/DELTA.md)).

## Summary

Two additions to the 009 stack:

1. **Song from lyrics**: the API stores lyrics sheets (normalised text with section markers, a
   section-proposal helper, density warning), builds a caption from a 009 style plus vocal options
   (guarded), and creates a `song` generation; the generator runs ACE-Step 1.5 text2music with the
   1.7B language-model planner (turbo DiT), producing takes with an AI voice (or instrumental).
2. **Own voice**: the user creates a **voice profile** from an in-app recording or upload with a
   stored, versioned consent record; the **singer** service checks the sample (duration, voiced ratio). For a
   song take, "Use my voice" imports the take as a track (009 flow, worker separation on the GPU
   under the shared lease), then sends the vocal stem, the other stems and the sample to the
   singer service (`voiceConversion`), where **SoulX-Singer-SVC** (Apache-2.0) converts the vocal to the
   profile's timbre (RMVPE F0, auto pitch shift) and remixes it. Result: a new take labelled
   "AI voice converted to <name>", in the 007 library, importable as a track.

No VoiceStudio code is used (AGPL-3.0; no singing in its code). Score-driven singing synthesis
(SoulX-Singer SVS) is out of scope (ADR-019 D2). Spike: research.md S-04 (song), S-05 (SVC in
the ACE-Step environment), S-06 (pipeline).

## Technical Context

**Language/Version**: as 009 (C# 14/.NET 10; generator Python 3.12; browser recording via the
standard MediaRecorder API in a small JS interop module of the Web project, no new npm package).

**Primary Dependencies (new)**:
- singer (new service from the generator code base, extra `soulx`): `soulx-singer` code at git
  commit `81aeb3ae772c70093c3de74dc23c92d983801ae4` (Apache-2.0; no `pyproject.toml` upstream, so it
  is a pinned git dependency through a tiny build shim, or installed by path in the image; decided
  in Phase 0 T003), `torch` 2.10.0+cu128 + `torchaudio` 2.10.0 (BSD), **`transformers` 4.41.2**
  (Apache-2.0; SoulX fails with 4.57, research.md S-05), `librosa` 0.11 (ISC), `omegaconf` 2.3
  (BSD-3). The `acestep` and `soulx` extras are mutually exclusive lock groups (uv conflicts).
- weights: `Soul-AILab/SoulX-Singer` `model-svc.pt` (2.79 GB, Apache-2.0, revision
  `40493ad90286056c7a9095035164434a79daa8c9`), `Soul-AILab/SoulX-Singer-Preprocess`
  `rmvpe/rmvpe.pt` (0.18 GB, Apache-2.0, revision `83dc50289d22a81b1e9998f5b9e111aef7c1fdcd`),
  `openai/whisper-base` (0.29 GB safetensors, Apache-2.0, revision
  `e37978b90ca9030d5170a5c07aadb050351a65bb`); the ACE-Step 5Hz LM 1.7B is already in the 009
  bundle.
- .NET: none new. Web: none new (MediaRecorder via own TS/JS interop file in the Web project's
  `wwwroot/js/voice-recorder.js`, typed, tested with Vitest-free bUnit JS-interop mocks).

**Storage**: tables `LyricsSheets`, `VoiceProfiles`, `AudioHashes`; `Generations` columns for
song/conversion; migration `AddLyricsAndVoices`; files `api-data/voices/<id>/sample.flac`,
`api-data/generations/<id>/take-1-vocal.flac`.

**Testing**: as 009; new fakes `FakeVoiceConverter`, `FakePitchTracker`; one additional
`@pytest.mark.integration @pytest.mark.gpu` test runs SoulX-SVC on a 10 s fixture; an SC-006 test
walks the data directories after profile deletion; Playwright `@generative` specs for lyrics,
song, voice profile with consent (fake microphone via Playwright's `--use-fake-device-for-media-stream`),
conversion.

**Target Platform / Performance**: as 009; SC-001 (60 s song <= 90 s warm), SC-002 (60 s
conversion <= 2 min).

**Constraints**: consent before any profile is stored (FR-006); no library audio as a sample
(FR-007); voice data local only (FR-015); GPU-only generation (amendment 1.3.0).

## Constitution Check

| # | Principle / constraint | How the plan complies | Status |
|---|---|---|---|
| I | Test-first, >= 90 % | New ports `VoiceConverter`, `PitchTracker`, `MusicEngine.song` with fakes; domain tests for lyrics normalisation/sections/density, consent, exact-copy guard, deletion; bUnit for the recorder (JS interop mocked); Playwright with fake generator and fake microphone. | PASS |
| II | SIG 4 stars | New .NET folders `Lyrics/`, `Voices/`; generator `engines/soulx_svc.py`, `engines/rmvpe.py`, `audio/remix.py`; small units. | PASS |
| III | Contracts first | Generator 1.1.0, backend +minor, UI ids +minor (DELTA.md); ports/schemas/DTOs in Phase 0; ADR-019. | PASS |
| IV | Open source, lawful use | SoulX-Singer code + weights Apache-2.0 and whisper-base Apache-2.0 (verified LICENSE / HF `cardData.license`, research.md S-01); VoiceStudio **not** used (AGPL-3.0, research 9.2). Consent and own-voice-only rules (FR-006/007/010), AI labels (FR-011), local-only voice data (FR-015). GPU-only per amendment 1.3.0 (009). | PASS |
| V | Simplicity and honesty | Reuse of 009 flows; SVC instead of SVS (no score editor); exact-copy guard documented as not a similarity check; conversion quality limits and languages stated in help. | PASS |
| - | Tech & security | Uploads size/type-checked in the API and decoded only in the singer service (soundfile/torchaudio, no shell); consent validated before any file is written; consent text versioned; voice data never leaves the internal network; no secrets. | PASS |
| - | Workflow | Same owners as 009 incl. `generative`; hand-offs per checkpoint. | PASS |

**Post-design re-check**: PASS.

## Project Structure

### Documentation (this feature)

```text
specs/010-lyrics-to-song/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md, tasks.md
├── checklists/requirements.md
└── contracts/DELTA.md     # generator 1.0.0 -> 1.1.0, backend +minor, UI ids +minor
```

### Source Code (additions)

```text
generator/src/stemstudio_generator/
├── ports.py, schemas.py                      # + VoiceConverter, PitchTracker, song/conversion schemas (architect)
├── engines/acestep_engine.py                 # + song() with LM load/unload (generative)
├── engines/soulx_svc.py, engines/rmvpe.py    # lazy imports, fp16 on CUDA (generative)
├── audio/remix.py, audio/normalise.py        # converted vocal + accompaniment, 48 kHz mono sample (generative)
└── api/voice_samples.py                      # POST /v1/voice-samples/check (generative)

src/StemStudio.Contracts/{Lyrics,Voices}/*, Generation/* additions                     (architect)
src/StemStudio.Domain/Lyrics/{LyricsSheet,LyricsNormaliser,LyricsSections,LyricsDensity}.cs (backend)
src/StemStudio.Domain/Voices/{VoiceProfile,VoiceConsent,ConsentStatement}.cs + consent.v1.{en,nl}.txt (backend)
src/StemStudio.Domain/Generation/{SongRequest,VoiceConversionRequest,VocalLabel}.cs     (backend)
src/StemStudio.Persistence/Migrations/*_AddLyricsAndVoices.cs, AudioHashes backfill   (backend)
src/StemStudio.Api/{Lyrics,Voices}/*Endpoints.cs, Generation/{SongService,VoiceConversionService,
    ConversionStemWaiter}.cs, Audio/AudioHashIndex.cs                                  (backend)
src/StemStudio.Web/Components/Pages/{NewSong,LyricsList,VoiceProfiles}.razor,
    Shared/{LyricsEditor,VoiceRecorder,ConsentPanel,UseMyVoiceDialog,VocalBadge}.razor,
    wwwroot/js/voice-recorder.js, Help entries                                         (frontend)
e2e/tests/{song,voice}.spec.ts                                                         (qa)
docker-compose.generative.yml (+ service `singer`), generator.Dockerfile (variant `soulx`)  (devops)
```

**Dependency direction**: unchanged from 009; `Api.Voices -> Domain.Voices`, `Api.Generation ->
Api.Voices` (read profile + sample path) one way; `Api.Audio.AudioHashIndex` is used by
storage writers (stems, takes, exports, uploads) and by `Api.Voices`.

### Ownership map

Same as 009 (`specs/009-style-transfer/plan.md`); no new paths outside those owners. The
`generative` agent role (draft in the 009 plan) is required.

## Key flows

1. **Lyrics**: `POST /api/lyrics` / `upload` -> `LyricsNormaliser` -> validation -> stored;
   `POST /api/lyrics/sections` -> `LyricsSections.Propose` (pure).
2. **Song**: `POST /api/songs` -> lyrics snapshot + style snapshot + vocal options ->
   `SongCaptionBuilder` (009 builder + `"female lead vocal"`/`"male lead vocal"` + delivery,
   guarded) -> `Generation(kind=song)` -> generator `song` job: lease + NVML -> profile by free
   memory (planned DiT + pt LM 15.7 GB, direct DiT-only 9.6 GB, direct low-memory 7.4 GB; research.md
   S-04) -> `generate_music(text2music, thinking = planned)` -> takes (`vocal=synthetic`, or
   `none` when instrumental) -> 009 sync/library/import.
3. **Voice profile**: browser records (MediaRecorder, WebM/Opus) or user uploads -> `POST
   /api/voice-profiles` (consent fields required; checked first, before any file is written) ->
   API hashes the uploaded bytes and checks `AudioHashes` (byte-identical to a stored stem, take,
   export or upload -> `422 libraryAudio`) -> API forwards the sample to the singer service
   `POST /v1/voice-samples/check` -> the singer decodes, resamples to 48 kHz mono, measures
   duration and voiced ratio, keeps the normalised FLAC for 10 minutes and returns
   `{ checkId, durationSeconds, voicedRatio, pcmSha256, ok, reason? }` -> if `ok`, the API
   downloads `GET /v1/voice-samples/{checkId}/audio` (then the singer deletes it) and stores the
   sample + consent record.
4. **Use my voice**: `POST .../voice-conversions` -> if the take is not imported: 009 import (track
   + worker separation) -> status `waitingForStems` until the track is completed -> API sends
   `target` = vocals stem, `accompaniment` = other five stems, `prompt` = profile sample ->
   singer: lease + NVML (`SVC_FP16`) -> RMVPE F0 (prompt, target) -> voiced check -> SVC
   (segments by F0, 32 steps, cfg 3, auto shift) -> resample 24 kHz -> 48 kHz, mix with the
   accompaniment (unity, peak-limited at -1 dBFS) -> FLAC with labels -> 009 sync stores the take +
   vocal stem.
5. **Delete profile**: cancel queued/running conversions with the profile -> delete
   `api-data/voices/<id>/` -> optionally delete conversion takes (files + library entries) ->
   null the profile id on generations.

## Complexity Tracking

| Deviation / refinement | Why needed | Simpler alternative rejected because |
|---|---|---|
| A fourth container `singer` (same code base, second image variant) | SoulX-SVC needs transformers 4.41.x, ACE-Step needs >= 4.51 (< 4.58); measured incompatibility (research.md S-05) | Patching SoulX's model code for 4.57 changes upstream code; two transformers versions in one image via `PYTHONPATH` worked in the spike but is fragile and untestable |
| Voice conversion requires a separation run first | SVC converts a dry vocal; the generated song is a full mix | Asking ACE-Step for separate vocal/instrumental output is not supported by the turbo model; the worker separation is fast on the GPU and also produces the mixer stems |
| `AudioHashes` index across stored audio | FR-007 exact-copy guard | Computing hashes on demand over all files would be slow and racy |
| Browser microphone recording (JS interop) | own-voice capture without extra tools | Upload-only would push users to reuse existing recordings, against the own-voice rule |
