# Hand-off: backend / 010-foundational (T014-T021)

## What was built

- **Domain/Lyrics** (new files next to 006's types): `LyricsSheet` (+ `LyricsContent`, `LyricsSnapshot`),
  `LyricsRules` (title 1-80, the 51 contract languages, text 1-4,096 after normalisation), `LyricsNormaliser`
  (LRC timestamps and tag lines removed, CR/CRLF -> LF, trailing spaces, at most two blank lines, outer blank
  lines trimmed; the qa `sample.lrc` expectation is a test vector), `LyricsSections.Parse/Propose/Marker`
  (repeated block -> chorus, verses numbered from 2, existing markers kept), `LyricsDensity` (words outside marker
  lines per second; warning above 4).
- **Domain/Voices**: `ConsentStatement` (`consent.v1`, embedded EN/NL, SHA-256 of the English text),
  `ConsentRules` (order: confirmed -> current version -> person name 1-80), `VoiceConsent`, `VoiceProfile`
  (name 1-60 unique, sample 10-60 s, voiced >= 0.5, PCM and file hashes), `AudioHash` (+ kinds).
- **Domain/Generations**: `LeadVoice`, `VocalLabel`, `SongDetails`, `VoiceConversionDetails`,
  `SongCaptionBuilder` (009 caption, or free caption, or "pop", plus lead voice and delivery; "instrumental" once;
  <= 512; guard on `freeCaption`/`delivery`), `SongRules`, `Generation.CreateSong/CreateVoiceConversion/
  WaitForStems/DetachVoiceProfile` (name snapshot + " (profile deleted)", qa expectation), take `Vocal`,
  `VoiceProfileName`, `VocalFilePath`; `TrackOrigin.Vocal/VoiceProfileName/LyricsSheetId`.
- **Persistence**: `LyricsSheets`, `VoiceProfiles` (consent JSON column), `AudioHashes` (SHA-256 key, idempotent
  add, removal per owner), `Generations.Song/VoiceConversion` JSON + `VoiceProfileId` column/index; repositories;
  migration **`AddLyricsAndVoices`** (`has-pending-model-changes`: none).
- **Api/Generator**: song (JSON), voice conversion (multipart), sample check + checked-sample and take-vocal
  downloads; `ISingerClient`/`SingerClient` on `StemStudio:SingerBaseUrl` (second typed client).

## Red -> green

| Pair | Red | Green |
|---|---|---|
| T014/T015 | c79b611 | 638858d |
| T016/T017 | 116a774 | 93c5a7f |
| T018/T019 | a409812 | 8cfbf74 |
| T020/T021 | 166ffdb | 37abb23 |

## Tests and coverage

Domain 751 passed, Persistence 92 passed, Api generator-client tests 92 passed (component numbers in the next
hand-offs).

## Deviations

- Consent stored as a JSON column instead of owned columns.
- The AudioHashes start-up backfill reads files and is implemented in the Api (US3, T045), not in Persistence.
- The embedded consent files need `WithCulture="false"` (".en"/".nl" would otherwise become satellite cultures).

## Dependencies added

None.

## Next owner

backend (010 US1).
