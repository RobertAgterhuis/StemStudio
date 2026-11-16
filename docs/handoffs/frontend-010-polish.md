# Hand-off: frontend / 010 polish

Task **010:T058** (frontend part). Commits: tests c534bf6 (red: 1 failing) -> fix 5caa5f7. T058 is one task id for
six owners, so it is **not ticked** here; the CTO ticks it when every owner's polish hand-off is in.

## What was built

- Accessibility (bUnit): the consent panel, the recorder, the new-voice panel, the use-my-voice dialog and the song
  page use native controls only (keyboard) and every input/select/textarea has a label; the statement paragraphs carry
  `lang="en"` / `lang="nl"`; the consent checkbox is described by the statement; the timer is `role="timer"`, the
  recording result `role="status"`, unavailable notices `role="note"`; the voice delete choice is a labelled checkbox in
  a labelled dialog; Escape closes the use-my-voice and lyrics dialogs; the lyrics textarea is described by its counter.
- Bug found by the new tests: `FieldProblem.From` mapped every 409 without field errors to `name` (right for a taken
  style name, wrong for "generator not ready" on the song page). The conflict field is now passed by the form (style
  editor only).
- Edge cases: song form fallbacks and limits (tempo, takes, length), back to "New lyrics", free description without
  styles, song facts without style or music choices, instrumental facts, second recording releases the first, loading
  and empty states of `/voices`, `/lyrics`, `/songs/new`, use-my-voice profile choice and load failure.
- Help review of the 010 topics: `song-options`, `lyrics-format`, `lyrics-sections`, `song-language`,
  `lyrics-library`, `voice-profiles`, `voice-consent`, `voice-conversion` (+ the 009 topics they link to); the 004
  reference queries still return their topics first.

## Tests and coverage (009 + 010 complete)

```text
dotnet test tests/StemStudio.Web.Tests -p:CollectCoverage=true
Passed! - Failed: 0, Passed: 1450, Total: 1450   (1230 before 009/010)
| StemStudio.Web | 99.45% line | 94.7% branch | 99.92% method |

src/web-interop: npx vitest run --coverage
Tests 254 passed (9 files); 99.69 % statements, 97.95 % branches, 100 % functions, 99.68 % lines
```

## Quality checks

- `dotnet build StemStudio.slnx -c Release`: 0 warnings, 0 errors; `dotnet format --verify-no-changes`: clean.
- `src/web-interop`: `eslint .` clean, `tsc --noEmit` clean, `npm run build` ok.
- `uv run --project tools/quality python tools/quality/run_all.py`: 1673 files, **0 errors**, exit 0 (new-code warnings
  only: `SongForm.Request` 17 lines, `SongForm` density getter 16 lines, `StyleEditorForm.From` 16 lines; hard max 30).
- No raw colours (tokens only, `generation.css`, `shell.css`); no secrets.

## Red -> green summary (010)

| Task pair | Red | Green |
|---|---|---|
| T031 -> T032 | 67ea746 | 41c1d9d |
| T036 -> T037 | 009b03c | 0ceceb8 |
| T050 -> T051 | af1c94e | 0c3d6d7 |
| T055 | 49310d2 | efd35b0 |
| T058 (frontend) | c534bf6 | 5caa5f7 |

## Dependencies added

None (no NuGet or npm package; MediaRecorder and getUserMedia are browser APIs).

## Open issues / requests

- qa: shell visual baselines change (five more rail links after a divider; the bottom bar scrolls sideways).
- qa: extra ids beyond ui-test-ids 1.8.0/1.9.0 (not binding): `song-lyrics-saved`, `song-load-error`,
  `generation-voice-reason`, `voice-record-status`, `voice-cancel`, `voices-empty`, `voices-error`,
  `voices-action-error`, `take-voice-unavailable`, `take-voice-error`, `lyrics-cancel`, `lyrics-load-error`,
  `lyrics-action-error`, `generations-error`, `generations-empty`, `generation-action-error`,
  `generation-delete-cancel`, `style-delete-cancel`, `styles-error`, `styles-action-error`, `style-load-error`.
- architect: the recorder functions are additive exports of `web-interop.js` (contract 1.0.0 of 004 does not list
  them); document them in a web-interop contract revision if wanted.
- CTO: `wwwroot/js/web-interop.js` is a build output (`npm run build` in `src/web-interop`), as before.

## Next owner

qa executes the 009/010 specs on the fake stacks (009:T081 / 010:T059); CTO integration wave.
