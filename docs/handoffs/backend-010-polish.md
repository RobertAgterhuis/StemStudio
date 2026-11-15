# Hand-off: backend / 010-us3, 010-us4 and 010-polish (T044-T049, T053-T054, T058)

## What was built

- **US3 voice profiles** (`VoiceProfileService`, `VoiceEndpoints`, `VoiceProblems`): order of checks of
  `POST /api/voice-profiles` = consent (400 `consent-required` / `consent-outdated` / `personName`), display name
  1-60 + source (400), unique name (409 `voice-name-taken`), singer ready (409 `voice-not-installed` /
  `voice-unavailable`), 20 MiB (413), exact-copy guard (422 `library-audio`), singer check (422 `too-short` /
  `too-long` / `unvoiced` with the singer's message), then the normalised sample `voices/<id>/sample.flac` and the
  consent record (version, statement SHA-256, person name, time, PCM and file SHA-256). Nothing is written before
  the last check passed (test). List/get, sample (Range) and `GET /api/consent/voice`. `convertedTakeCount` = completed
  conversions with the profile. `generation/status.voice` from the singer (`SingerAvailability`, 10 s cache).
- **Exact-copy guard** (`AudioHashIndex`): `AudioHashes` filled by scanning `tracks/`, `uploads/`, `generations/`,
  `assets/` at start-up and before each check (only new or changed files are hashed).
- **Voice conversions** (`VoiceConversionService`, `ConversionStemWaiter`, `GeneratorRouter`): song take with an AI
  voice only (409 `no-vocals`), profile 404, pitch -12..12 (400), singer ready (409); the take is imported when
  needed and the conversion waits in `waitingForStems`; once separated, `vocals` (target), the other stems
  (accompaniment) and the sample (prompt) go to the singer; conversion jobs are polled, collected (take + converted
  vocal `vocal-<n>.flac`, label `converted`, profile name) and deleted on the singer; `GET .../takes/{index}/vocal`.
- **Deletion** (`VoiceProfileDeletion`): active conversions are removed from the singer and cancelled, the sample
  folder is deleted, converted takes are deleted (`deleteTakes=true`) or kept as "<name> (profile deleted)"
  (qa expectation); a walk over the data directory finds no copy of the sample (test).
- **US4**: `Track.Origin`/`TrackDto.origin` carry `vocal`, `voiceProfileName`, `lyricsSheetId` for imported song and
  conversion takes.
- **Polish**: the language list is an embedded resource (`languages.v1.txt`, no clone of `ContractLimits`);
  `PendingSongVoiceOperations` and `PendingGenerationOperations` are both empty (every 009/010 id is served).

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T044/T045 | 945945d | 2de7f4d |
| T046/T047 | 4e28f63 | ea06ea5 |
| T048/T049 | 27aaeb2 | a0c4ab0 |
| T053/T054 | 161769c | dc9be8e |
| T058 (polish) | - | d8726bd |

## Tests and coverage

```text
dotnet test tests/StemStudio.Domain.Tests -p:CollectCoverage=true        751 passed   Domain       line 98.92 %  branch 94.52 %
dotnet test tests/StemStudio.Persistence.Tests -p:CollectCoverage=true    92 passed   Persistence  line 98.48 %  branch 95 %
dotnet test tests/StemStudio.Api.Tests -p:CollectCoverage=true --filter "FullyQualifiedName!~NewerContracts_ContainTheOlderOnes"
                                                                        1165 passed   Api          line 98.9 %   branch 92.9 %
                                                                                      Contracts    line 100 %    branch 100 %
dotnet test tests/StemStudio.Web.Tests                                    798 passed   (unchanged, not my component)
```

Unfiltered Api run: 1165 passed + the 2 pre-existing `GenerationContractTests.NewerContracts_...` failures (spec
drift from be286b4; architect request in backend-009-foundational).

## Quality checks

- `dotnet build StemStudio.slnx -c Release`: 0 warnings, 0 errors; `dotnet format --verify-no-changes`: clean.
- `dotnet-ef migrations has-pending-model-changes`: none (migrations `AddGenerativeStyles`, `AddLyricsAndVoices`).
- `sig_check.py src/StemStudio.{Domain,Persistence,Api}`: 0 errors, 52 warnings (16-25 NLOC or CCN 6-7 units in the
  new generation/voice services; none above the hard limits).
- `duplication.py`: 0 clones in production code. `dependency_check.py`: 0 errors, 0 warnings.

## Dependencies added

None.

## Deviations

- Exact-copy guard index is filled by scanning before each check instead of by every writer (covers every writer
  without touching 001-008 storage code); stale hashes of deleted files stay (stricter, never weaker).
- `ConvertedTakeCount` counts completed conversions (one take each).
- A conversion whose profile was deleted while waiting for stems fails with "The voice profile was deleted."

## Open issues / requests

- **architect**: be286b4 port (see backend-009-foundational).
- **devops**: set `StemStudio__SingerBaseUrl=http://singer:8010` next to `StemStudio__GeneratorBaseUrl` in the
  generative compose file.
- **frontend**: profile deletion label is "(profile deleted)"; `voice.state` now comes from the singer.

## Next owner

CTO integration wave (009 T081 + 010 T059).
