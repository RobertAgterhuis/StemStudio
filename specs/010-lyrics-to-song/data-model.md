# Data Model: Own Lyrics to Song (Music + Voice)

Feature 010. Extends the 009 data model (`specs/009-style-transfer/data-model.md`); only additions
and changes are listed. Versions are assigned at feature start (contracts/DELTA.md).

## 1. Configuration (environment, additions)

| Variable | Service | Default | Meaning |
|---|---|---|---|
| `StemStudio__SingerBaseUrl` | api | unset | singer service URL (`http://singer:8010`); unset = voice features "not installed" |
| `STEMSTUDIO_GENERATOR_KINDS` | generator, singer | `restyle,song` (generator image) / `voiceConversion` (singer image) | job kinds this instance accepts; also listed in `/health.kinds` |
| `STEMSTUDIO_FAKE_GENERATOR_MODE` | generator, singer | `ready` | adds `unvoiced-sample` (voiced-ratio check fails) |
| `STEMSTUDIO_SVC_MAX_TARGET_SECONDS` | generator | `480` | longest vocal stem accepted for conversion |

## 2. Generator (Python)

### 2.1 Job kinds (additions to `CreateGenerationRequest`)

`kind: "song"`, `song: SongParams`:

| Field | Type | Rules |
|---|---|---|
| `caption` | str | 1-512 (built by the API from style + vocal options) |
| `lyrics` | str | 1-4,096 chars, LF line ends; `"[Instrumental]"` when instrumental |
| `instrumental` | bool | |
| `vocalLanguage` | str | ISO 639-1 from the ACE-Step list (`en`, `nl`, `de`, `fr`, `es`, ... 50+) or `unknown` |
| `bpm` | int? | 40-220 |
| `keyScale` | str? | `^[A-G](#|b)? (major|minor)$` |
| `timeSignature` | `"2"\|"3"\|"4"\|"6"`? | |
| `durationSeconds` | float | 10-480 |
| `inferenceSteps`, `shift`, `seeds` | as RestyleParams | |
| `thinking` | bool | default true (LM planner) |

No audio parts.

Song takes also report `profile: "planned" | "direct"` (which song profile ran).

`kind: "voiceConversion"` (**singer** service only), `voiceConversion: VoiceConversionParams` + multipart parts
`target` (vocal stem, FLAC/WAV, <= `STEMSTUDIO_SVC_MAX_TARGET_SECONDS`), `prompt` (voice sample,
FLAC/WAV, 10-60 s), `accompaniment` (0-5 other stems, summed and mixed with the converted vocal):

| Field | Type | Rules |
|---|---|---|
| `pitchShift` | int | -12..12; `0` + `autoShift` true = shift to the prompt's register |
| `autoShift` | bool | default true |
| `steps` | int | 8-64, default 32 |
| `cfg` | float | 1.0-6.0, default 3.0 |
| `vocalGainDb` | float | -12..6, default 0 |
| `profileName` | str | 1-80, written into `AI_VOICE_PROFILE` |

Takes of a voice conversion: 1 take (`index` 1) = the remix; plus a second file
`GET /v1/generations/{id}/takes/1/vocal` (converted vocal stem only, FLAC 24 kHz mono upsampled to
48 kHz stereo for import consistency).

### 2.2 Voice-sample check

`POST /v1/voice-samples/check` (multipart `sample`) -> `200 VoiceSampleCheck { checkId,
durationSeconds, voicedRatio, pcmSha256, ok, reason? }`. RMVPE runs on the CPU for this check
(short input, no lease needed; research.md S-05), so profiles can be created while a GPU job runs.
`ok` iff 10 <= duration <= 60 and `voicedRatio >= 0.5`. When `ok`, the normalised sample (48 kHz
mono FLAC) is kept for 10 minutes at `GET /v1/voice-samples/{checkId}/audio` (deleted after the
first download or the timeout); otherwise nothing is kept.

### 2.3 Ports (additions)

| Port | Methods | Implementations |
|---|---|---|
| `MusicEngine` | `+ song(request: EngineSong, on_progress) -> list[EngineTake]` | `AceStepEngine`, `FakeMusicEngine` |
| `VoiceConverter` | `load(on_progress)`, `convert(target: Path, prompt: Path, opts) -> np.ndarray`, `unload()` | `SoulXSvcConverter` (lazy import), `FakeVoiceConverter` |
| `PitchTracker` | `f0(path) -> np.ndarray` (Hz, 0 = unvoiced) | `RmvpePitchTracker`, `FakePitchTracker` |
| `GpuNeed` | `+ SONG_PLANNED` (DiT + pt LM, 15.7 GB), `SONG_DIRECT` (DiT only, 9.6 GB), `SONG_DIRECT_LOWMEM` (offload, 7.4 GB), `SVC_FP16` (6.0 GB) | research.md S-04/S-05 |

Model statuses add `acestep-5hz-lm-1.7b` (already in the bundle), `soulx-singer-svc`,
`rmvpe`, `whisper-base`.

### 2.4 Labels (FLAC Vorbis comments, in addition to 009)

`AI_VOICE=synthetic` (song with vocals) | `converted` (voice conversion) ; `AI_VOICE_PROFILE=<name>`
(conversion only); `LYRICS` is **not** embedded (kept in the API only).

## 3. Backend (.NET)

### 3.1 LyricsSheet (`StemStudio.Domain.Lyrics`)

| Member | Type | Rules |
|---|---|---|
| `Id` | Guid | |
| `Title` | string | 1-80 |
| `Language` | string | ISO 639-1 from the supported list, or `unknown` |
| `Text` | string | 1-4,096 after normalisation (LRC timestamps `[mm:ss.xx]` stripped, CRLF -> LF, trailing spaces trimmed, at most 2 consecutive blank lines) |
| `CreatedAt`, `UpdatedAt` | | |

`LyricsSections.Parse(text)` -> list of `(Marker, Lines)`; markers recognised case-insensitively:
`intro, verse, pre-chorus, chorus, bridge, outro, instrumental` (optionally numbered, `[verse 2]`).
Unknown `[...]` lines are kept as text. `LyricsSections.Propose(text)` -> text with markers:
blocks separated by blank lines; a block whose normalised text repeats becomes `[chorus]`, the
first block before the first chorus `[verse]`, etc. (test vectors in tests).
`LyricsDensity.WordsPerSecond(text, duration)`; warning when > 4.0.

### 3.2 Generation (changes)

- `GenerationKind` adds `Song`, `VoiceConversion`.
- Song fields: `LyricsSheetId?`, `LyricsSnapshot` (text, title, language), `StyleSnapshot?`,
  `FreeCaption?`, `VoiceOptions(LeadVoice: Female|Male|Any, Delivery?: string <= 40)`,
  `Instrumental`, `Bpm?`, `KeyScale?`, `TimeSignature?`, `DurationSeconds`.
- Voice conversion fields: `SourceGenerationId`, `SourceTakeIndex`, `VoiceProfileId?`,
  `VoiceProfileName` (snapshot), `ImportedTrackId` (the separated take), `PitchShift`, `AutoShift`.
- `GenerationTake.Vocal` = `None | Synthetic | Converted` + `VoiceProfileName?`; conversion takes
  have `VocalStemPath` (converted vocal only).
- Voice conversion lifecycle: `Queued` -> (`WaitingForStems`: the source take is imported and its
  separation not finished) -> submitted to the generator -> generator states -> terminal.
  `WaitingForStems` is an API-only status (generator never sees it).

### 3.3 VoiceProfile (`StemStudio.Domain.Voices`)

| Member | Type | Rules |
|---|---|---|
| `Id` | Guid | |
| `DisplayName` | string | 1-60, unique |
| `Consent` | `VoiceConsent` | required |
| `SampleSource` | `Recorded` \| `Uploaded` | |
| `SamplePath` | string | `api-data/voices/<id>/sample.flac` (normalised 48 kHz mono FLAC) |
| `SampleSha256` | string | SHA-256 of the decoded PCM (int16 LE, 48 kHz mono) |
| `DurationSeconds` | double | 10-60 |
| `VoicedRatio` | double | >= 0.5 |
| `CreatedAt` | | |

`VoiceConsent { StatementVersion: "consent.v1", StatementSha256, PersonName (1-80), ConsentedAt,
Confirmed: true }`. Statement text `consent.v1` (embedded resource, English, Dutch translation
shown alongside): "This is my own voice. I agree that StemStudio uses this recording on this
computer to convert generated vocals into my voice. I can delete it at any time."

Exact-copy guard: `IAudioHashIndex.Contains(sha256)` over the **file-byte** SHA-256 of every
stored stem, detail stem, take, take vocal, export and upload (index table `AudioHashes`, filled
when files are written; backfilled at start-up for existing files). The uploaded sample's raw
bytes are hashed before anything is stored. This catches a re-upload of a file StemStudio
produced or stored (the realistic shortcut); it is not a similarity check (documented).

Deletion: `VoiceProfileService.DeleteAsync(id, deleteTakes)` removes `api-data/voices/<id>/`,
asks the generator to delete any job copies (`DELETE /v1/generations/{id}` for queued/running
conversions with the profile), optionally deletes conversion takes, and sets
`Generation.VoiceProfileId = null` (name snapshot kept + "(deleted)").

### 3.4 Persistence

Tables `LyricsSheets`, `VoiceProfiles` (consent as owned columns), `AudioHashes (Sha256 PK, Kind,
OwnerId)`; `Generations` gains the song/conversion columns (JSON for snapshots). Migration
`AddLyricsAndVoices`.

### 3.5 Track origin

`TrackOrigin.Generated` adds `Vocal` and `LyricsSheetId?`; `TrackDto.origin` adds `vocal`
(`none|synthetic|converted`), `voiceProfileName?`, `lyricsSheetId?`.
