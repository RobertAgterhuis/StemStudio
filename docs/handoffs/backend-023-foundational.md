# Hand-off backend-023: foundational (T012-T017)

Lane backend-023, branch `r22-lane-be023`, 2026-11-17.

## Commits

| Commit | Tasks |
|---|---|
| db670f05 | T012/T013 Domain `Lyrics/Assist/`: `SheetBlocks`, `AnchorResolver` + `LineDiff` (line LCS), `SuggestionApplier`, `IntentionalMark` + `IntentionalMarkMatcher`, entities `AssistRun`, `LyricSuggestion`, `LyricsSheetRevision`, `LyricWord`, `LyricAssistSettings`, `AssistEvent`, records `OwnWorkOverride`, `RhythmTarget`; `LyricsSheet` (partial) gains revision, last cause, sensitivity, rhythm target, marks, own-work override, assisted lines |
| 12705e0b | T014/T015 Persistence: configurations, `AddLyricAssist` migration + regenerated snapshot, repositories (`ILyricAssistRepository`, `ILyricSuggestionRepository`, `ILyricsRevisionRepository`, `ILyricWordRepository`) |
| f6a2bb5d | T016/T017 `PUT /api/lyrics/{id}`: revision per save, `expectedRevision` -> 409 `revision-conflict` (`currentRevision`), revisions stored, open suggestions re-anchored (stale on a language change), assisted lines pruned; sheet responses carry `revision` and `assist`; delete cancels active assists (cascade deletes the rest) |
| 31609223, 1af31bbb + follow-up | merges of the round branch (afa126df, GET lyric-assist settings) and the ai-agent seam (88c6af4a); temporary seam copy removed |

## Tests and coverage (full own suites, `-p:CollectCoverage=true`)

| Component | Tests | Line | Branch |
|---|---|---|---|
| StemStudio.Domain | 1,004 passed | 98.79 % | 93.93 % |
| StemStudio.Persistence | 129 passed | 98.86 % | 96.29 % |
| StemStudio.Api | 2,476 passed, 3 failed, 2 skipped | not reported (run failed) | - |

The 3 Api failures (`AgentEndpointsTests.Executor_*` x2, `ClaudeKeyUnreadableTests.OtherKeyRing_*`) are timing flakes of 011 tests
under load (I built Domain in parallel); re-run alone: 11/11 green. Api coverage is reported at the US1 checkpoint (the assist
endpoints are not served yet, so the API gate is expected below 90 % until T028/T030).

## Decisions (interim, for CTO review)

1. **Revision on every save** (contract `LyricsSheet.revision`: "incremented by every save"), also when nothing changed.
2. **Block range** starts at a leading section marker (fixture `zomeravond` blocks `[1,5]`); markers and blank lines do not count
   towards the 8 lyric lines.
3. **Insertion-point anchors** (`length = 0`, continuations): kept while their line survives (moved with the line diff, `start` =
   new line end), else stale. `original` is not compared for them.
4. **Sensitivity default** in the migration via `HasDefaultValue(Balanced).HasSentinel(-1)` so `Loose` (enum 0) is stored, and the
   default survives the CTO's re-generation of the migration.
5. **Revisions are stored lazily**: create stores revision 1; every change first stores the current revision when missing (sheets
   from before 023 get their revision-1 row at their first change), then the new one; retention 50.
6. Accepting an `operation: none` suggestion marks it accepted without a text change.

## Contract gaps found

- `updateLyricsSheet` declares no `409` response although `LyricsInput.expectedRevision` says a mismatch returns
  `409 revision-conflict`. Implemented as described; the 409 response is not checked by `ContractAssert` until the contract adds it.

## Open / next

US1 (T025-T030) in progress: deterministic layer (licence lexicon, `DictionaryCheck`, `SuggestionFilter`, `HunspellSpellChecker`).

## Security self-review (this checkpoint)

Problem details never echo sheet text (test `StaleExpectedRevision_Is409RevisionConflict`); all persisted texts are length-capped
in the Domain (proposal 400, explanation 300, failure 500, marks per scope); JSON columns hold only our own records.
