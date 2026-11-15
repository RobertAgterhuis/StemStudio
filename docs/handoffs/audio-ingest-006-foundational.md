# Hand-off: audio-ingest / 006 foundational (T013-T016)

Agent A of audio-ingest for feature 006, on `006-008-lyrics-sheet-music` from 62d107e (Phase 0).
Owned paths: `worker/src/stemstudio_worker/lyrics/**`, `worker/tests/lyrics/**`.

## What was built

Package `stemstudio_worker.lyrics` (no logging of lyric text anywhere):

- `normalize.py`: limits (1 MB input, 400 lines, 40 words and 500 characters per line, 100 per
  word -> `LyricsUnusableError`), `check_input` (size + LF line breaks), `split_words`
  (whitespace; CJK/kana/Hangul per character, punctuation joins the previous CJK word),
  `timed_line`, `plain_lines`, `plain_document`, `DocumentMeta` and `finish` (builds the
  contract `TimedLyrics`; any broken data-model 2.1 invariant -> `LyricsUnusableError`; the
  language is reduced to its primary subtag, unknown values dropped).
- `lrc.py`: `parse_lrc` -> `LrcLine(start_ms, text)`; `[mm:ss]`, `.x/.xx/.xxx` and `:` fractions,
  several stamps per line, `[offset:]` (positive = earlier, floored at 0), tags and unstamped rows
  ignored, empty stamped lines = breaks; stable sort by start.
- `lyricsfile.py`: `parse_lyricsfile(text, meta)` -> word-synced document (origin `source`);
  word end = next start / line end, capped at 3000 ms, at least 1 ms; the line widens to hold
  its words; a timed line without words is interpolated from its text; `metadata.language`.
- `interpolate.py`: `vowel_weight` (vowel groups after NFKD accent stripping, Latin/Greek/
  Cyrillic, minimum 1), `line_end_ms` (`min(next start, start + 60 ms * chars + 1500 ms)`),
  `interpolate_line`, `interpolate_lrc` (breaks end the previous line; a line with less than
  1 ms per weight unit before the next start, e.g. equal stamps, is merged into the next).
  Reproduces the contract examples `LineSyncedLyricsResult` and `WordSyncedLyricsResult` exactly.

### Security review fixes (CTO request, test-first)

1. `lyricsfile.py`: before `yaml.safe_load`, the YAML parse events are scanned and any anchor or
   alias is refused (`LyricsUnusableError`), so alias amplification ("billion laughs") cannot
   defeat the 1 MB cap. Tests: alias, anchor without alias, a 9-level alias bomb that must fail
   in < 1 s.
2. `lrc.py`: `[offset:]` accepts at most 7 digits (longer -> ignored, offset 0; no `int()`
   digit-limit error) and is clamped to +-600,000 ms (`MAX_OFFSET_MS`). Tests: 5000-digit offset,
   +-9,999,999.

## Commits (red -> green)

| Task | Red | Green |
|---|---|---|
| T013 -> T014 (lrc, lyricsfile, normalize) | 534c022 | 49e7d18 |
| T015 -> T016 (interpolate) | a427fc7 | 32e84e9 |
| Security review (YAML aliases, LRC offset) | e3878db | 6d92d0e |

## Files changed

- `worker/src/stemstudio_worker/lyrics/{__init__,normalize,lrc,lyricsfile,interpolate}.py`
- `worker/tests/lyrics/{__init__,test_normalize,test_lrc,test_lyricsfile,test_interpolate}.py`

## Tests and coverage

```text
uv run --frozen pytest tests/lyrics --cov=stemstudio_worker.lyrics --cov-branch
  119 passed; lyrics 230 stmts, 28 branches, 100 % line and branch
uv run --frozen pytest --cov --cov-branch   (whole worker, at 32e84e9)
  1855 passed, 7 deselected; TOTAL 6898 stmts, 782 branches, 100 %
```

## Quality checks

```text
ruff check .            All checks passed!
ruff format --check .   243 files already formatted
mypy src tests          Success: no issues found in 246 source files
tools/quality/sig_check.py worker/src/stemstudio_worker/lyrics worker/tests/lyrics
  sig_check: 10 files, 127 units, 0 errors, 0 warnings, 0 exemptions
```

## Dependencies added

None (PyYAML 6.0.3 was already made a runtime dependency in Phase 0).

## Open issues / requests

- See the US1 hand-off for the 005 allow-list request (`lrclib.net`).

## Next owner

audio-ingest A continues with 006:T027/T028 (HO:us1). Consumers (stem-separation, backend) need
nothing from this checkpoint except the document shapes already fixed by the contract.
