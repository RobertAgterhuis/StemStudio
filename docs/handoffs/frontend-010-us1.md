# Hand-off: frontend / 010 US1 (own lyrics to a song)

Tasks **010:T031 -> T032** (ticked). Commits: red 67ea746 -> green 41c1d9d. Builds on the 009 UI (generation list,
take player, AI badge, status banner).

## What was built

- Typed client: `ListLyricsSheetsAsync(q)`, `GetLyricsSheetAsync`, `CreateLyricsSheetAsync`,
  `UploadLyricsSheetAsync(file, title?, language?)` (multipart `file`, optional `title`/`language` parts),
  `UpdateLyricsSheetAsync`, `DeleteLyricsSheetAsync`, `ProposeSectionsAsync`, `CreateSongAsync`.
- `/songs/new` (`Components/Songs/NewSongPage`, `?lyricsId=` preselects a sheet):
  - `LyricsEditor`: `song-lyrics-select` (native select, "New lyrics" first, option label = sheet title),
    `song-lyrics-text` (textarea, marker `lyrics-format`), `song-lyrics-count` ("n / 4096", raw length),
    `song-lyrics-upload` (InputFile, accept `.txt,.lrc,.md`, checked before upload: extension and 64 KiB; the upload
    saves a sheet and puts its normalised text in the editor), `song-sections-propose` (marker `lyrics-sections`;
    replaces the text), `song-lyrics-save` (creates "Untitled lyrics" or the title, then updates the selected sheet;
    confirmation `song-lyrics-saved`), `song-density-warning` (words without markers / length > 4.0; generating
    stays possible).
  - `SongOptions`: `song-title-input`, `song-language-select` (51 codes with English names, "English" preselected,
    marker `song-language`), `song-style-select` (library styles; "Free description" option at Advanced),
    `song-lead-voice` (values `female|male|any`), `song-instrumental`, `song-duration` (`<input type=range>` 10-480,
    `aria-valuenow` seconds, `aria-valuetext` m:ss, default 120).
  - Advanced (`song.options`): `song-free-caption` (enabled only with "Free description"), `song-delivery-input`,
    `SongMusicOptions` with `song-bpm`, `song-key` ("Automatic" + 24 keys), `song-time-signature`
    ("Automatic", 2/4, 3/4, 4/4, 6/8 -> values `2|3|4|6`), `song-takes`.
  - `song-personal-use-notice` (personal use, AI voice label, own lyrics, no imitation), `song-error` (`data-field`:
    `lyricsText` for empty or > 4096 characters, API field errors such as `delivery`/`freeCaption`),
    `song-generate` (disabled with `aria-describedby="generation-status-reason"` when the generator is not ready),
    section `song-generations` (generation list of kind `song`).
  - A saved sheet whose text is unchanged is sent as `lyricsId`, otherwise as `lyricsText`.
- Takes: `VocalBadge` `take-vocal-badge-<id>-<n>` with `data-vocal` (`none` -> "No vocals", `synthetic` -> "AI voice",
  `converted` -> "AI voice: <profile>"); take details of songs (`SongFacts`): lyrics title and language, style or free
  description, lead voice and delivery, tempo/key/beats per bar, length and the lyrics text.
- `GenerationTitles`: songs by their lyrics title, conversions "In the voice <name>".
- Help topics (area `mixes`): `song-options` (screen topic of `/songs/new`), `lyrics-format`, `lyrics-sections`,
  `song-language`. Nav `nav-songs` (`songs/new`). Shared `TakesInput` (restyle + song).

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -p:CollectCoverage=true
Passed! - Failed: 0, Passed: 1379, Total: 1379
| StemStudio.Web | 99.27% line | 93.47% branch |
```

## Quality checks

Build 0 warnings; format clean; `run_all.py`: 0 errors (one clone found and removed via `TakesInput`).

## Dependencies added

None.

## Open issues / requests

- qa: the upload of a lyrics file on the song page **saves it as a sheet** (that is what `POST /api/lyrics/upload`
  does); `song.spec.ts` deletes the sheet it created through the API, the UI upload creates a second one with the same
  file name as title.
- qa: `song-lyrics-saved` is an extra status id (not in ui-test-ids 1.9.0).

## Next owner

frontend continues with 010 US2 (T036/T037).
