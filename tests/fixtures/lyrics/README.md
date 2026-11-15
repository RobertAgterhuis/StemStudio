# Lyrics fixtures (feature 006)

Owner: qa (006:T012). Consumers: audio-ingest (`worker/tests/lyrics/`, 006:T013/T015/T027/T028),
stem-separation (`worker/tests/alignment/`, 006:T040-T042), backend (fake-run log scan, 006:T033),
the E2E specs `e2e/tests/karaoke.spec.ts` and `lyrics.spec.ts`, and the checks in
`tests/fixtures/tests/test_lyrics_fixtures.py` / `test_generate_lyrics_fixtures.py`.

| Path | What | Used by |
|---|---|---|
| `lrc/basic.lrc`, `lrc/edge-cases.lrc`, `lrc/negative-offset.lrc`, `lrc/no-stamps.lrc` | LRC files: metadata tags, `[mm:ss]` / `.x` / `.xx` / `.xxx` stamps, two stamps on one line, `[offset:+250]` and `[offset:-500]`, an empty stamp (break), a blank line, a CJK line, a right-to-left (Hebrew) line, padded text, a file without stamps | `test_lrc.py` |
| `lrc/expected.json` | the parse result every LRC file must give (start ms after the offset, text, breaks, usable) plus the rules it follows | `test_lrc.py`; checked by a reference reader in `test_lyrics_fixtures.py` |
| `lyricsfile/word-synced.yaml`, `instrumental.yaml`, `unsafe-tag.yaml` | LRCLIB `lyricsfile` YAML in the research.md F-03 shape; an instrumental record; a document with a `!!python/object` tag that `safe_load` must refuse | `test_lyricsfile.py` |
| `lrclib/get-*.json`, `lrclib/search-*.json`, `lrclib/get-not-json.txt` | LRCLIB `/api/get` and `/api/search` responses (status + body) in the shape of F-01/F-02, with placeholder text | `test_lrclib.py`, `test_select.py`, `test_lookup.py` (replay with `httpx.MockTransport`) |
| `fake-lyrics.json` | proposed behaviour table of the fake lyrics provider and fake aligner (`STEMSTUDIO_FAKE_LYRICS` = `word`, `line`, `plain`, `instrumental`, `not-found`, `failed`) and the word-synced document they return | `lyrics/fake.py` (006:T028) and `alignment/fake.py` (006:T041) should return exactly this; the E2E specs read it (`e2e/support/lyrics.ts`) |
| `align-clip.json` | exact word-start annotation of the sung alignment clip (generated, committed) | SC-003 integration test `worker/tests/alignment/test_align_clip.py` (006:T042, run in T066) |
| `sample-en.txt`, `sample-nl.txt`, `sample.lrc` (feature 010) | self-written own lyrics (CC0): English in four blocks with a repeated chorus (no markers, for the section helper), Dutch with `[verse]`/`[chorus]`, an LRC version with stamps and an empty-stamp break | E2E `song.spec.ts`, quickstart V3; checked by `test_generative_fixtures.py` |
| `own-lyrics-expected.json` (feature 010) | expected facts of the own lyrics: word count, density, proposed markers, the normalised LRC text | E2E `song.spec.ts`, `generative-support.spec.ts` |

## Rules the fixtures encode

- **LRC offset** (`lrc/expected.json`): the LRC convention, `[offset:+n]` shows the lyrics *n* ms
  sooner, so `startMs = stamp - n`. This is not the app's karaoke offset (backend contract:
  positive `offsetMs` = lyrics later). audio-ingest: please confirm in 006:T013 or report.
- **Word ends of `word-synced.yaml`** (research.md R-03): a word ends at the next word's start; the
  last word of a line at `min(line end_ms, start + 3000)`. Expected: line 1 `Fake` 10950-11400,
  `line` 11400-11900, `one` 11900-13200; line 2 `La` 14000-14500, `la` 14500-15000, `la`
  15000-16000; line 3 `A` 17500-17800, `very` -18200, `long` -18600, `held` -19000, `last`
  -19400, `word` 19400-**22400** (3 s cap, the line ends at 25000).
- **Search `expected`** maps our duration (s) to the chosen record id after step 3 of data-model
  2.3 (`abs(duration - ours) <= 2`, then word sync, then synced, then plain; ties: our album,
  then the lowest id), `plain:<id>` for step 4 (plain text within 15 s), `null` for nothing.
  `search-ties.json` keys are `<duration>|<our album>`. `get-by-duration.json` replays the F-01
  table (262/265/258 word sync, 270 and 285 line sync, 200 -> 404).
- **Fake stack timing** (`fake-lyrics.json`): fits the 20 s fixtures (`music_100bpm_20s.*`):
  lines at 2.0-5.6 s, 6.2-9.8 s and 16.0-17.8 s (a 6.2 s gap shows the countdown), 600 ms per
  word; fake alignment puts word *k* at 2000 + 500 *k* ms.

## Alignment clip (`align_clip_sung_29s.wav`, owner question O-2)

Generated, not committed: `uv run --project worker python tests/fixtures/generate_lyrics_fixtures.py`
writes the clip into `tests/fixtures/audio/` and rewrites `align-clip.json`.

- **What**: an isolated lead vocal (one voice, no accompaniment, as the aligner sees `vocals.lead`),
  28.97 s, 44.1 kHz stereo PCM16, 4 lines / 22 words, D major at 100 BPM, one syllable per note
  (eighth to whole notes), a 2.4 s rest between lines 2 and 3.
- **How**: the song (words and melody) is self-written by qa; the voice is the formant singing
  synthesiser `tests/fixtures/sung_voice.py` (Rosenberg glottal pulse with vibrato through five
  time-varying resonators, noise for fricatives and bursts). The annotation is exact by
  construction: a word starts at the first sample of its first phone.
- **Licence**: CC0 1.0 (qa dedicates lyrics, melody, annotation and the rendered audio to the
  public domain). No third-party recording, voice model or lyrics are involved
  (CTO-DECISIONS-2026-11-15: "choose a CC0/public-domain sung recording or synthesise one").
- **Known limitation**: research.md R-07 preferred a real sung recording because a synthetic voice
  is not real singing. The clip is intelligible to Whisper (turbo transcribed 18 of 22 words
  correctly, see below), but it remains easier and more regular than a human singer. A self-sung
  CC0 clip can replace it later with the same `align-clip.json` format.
- **qa measurement (2026-11-15, CPU, stable-ts 2.19.1 `align(..., language="en",
  original_split=True)`, default options, 16 kHz mono)**: Whisper `turbo`: 14 of 22 word starts
  (63.6 %) within 300 ms, median error 170 ms; Whisper `base`: 59.1 %, median 220 ms. The misses
  are systematic: the word after a held note (2-3 beats) is placed 340-580 ms early, i.e. inside
  the held vowel. That is below the SC-003 target (>= 80 % within 300 ms); see the qa hand-off
  `docs/handoffs/qa-006-pre-ui.md` (request to stem-separation 006:T040-T042).

## Provenance and licences

- **No lyric text of any song is committed.** Every lyric line is self-written ("Fake line one",
  "La la la", the clip's song, a self-written Japanese and Hebrew line). `test_lyrics_fixtures.py`
  asserts that all LRCLIB records carry only the placeholder text.
- Track names, artists, albums, durations and the record ids 546388, 10422352, 16019288,
  13446363, 3780737 and 3879894 are the facts recorded in research.md F-01/F-02 (no lyrics); every
  other id (`90001nn`-`90005nn`) and the albums "Fixture ..." are synthetic. The `lyricsfile`
  values inside the LRCLIB records use the placeholder lines.
