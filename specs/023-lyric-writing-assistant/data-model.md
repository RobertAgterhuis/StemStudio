# Data Model: Lyric Writing Assistant (feature 023)

Extends the 010 lyrics sheet (`specs/010-lyrics-to-song/data-model.md` 3.1) and the 011 agent
model (`specs/011-ai-composer-agent/data-model.md`). Only additions and changes are listed. EF
Core migration `AddLyricAssist` (backend). Domain rules are pure and live in
`StemStudio.Domain/Lyrics/Assist/` (backend owns), the model-facing parts in
`StemStudio.Agent/LyricAssist/` (ai-agent owns).

## 1. Configuration (environment, additions)

| Variable | Default | Meaning |
|---|---|---|
| `StemStudio__LyricAssist__MaxInputTokensPerAssist` | `20000` | Claude per-assist cap (FR-026) |
| `StemStudio__LyricAssist__MaxOutputTokensPerAssist` | `4000` | Claude per-assist cap |
| `StemStudio__LyricAssist__BlockLines` | `8` | lines per model block (R-05) |
| `StemStudio__LyricAssist__BlockTimeoutSeconds` | `60` (local), `90` (Claude) | per block; a timed-out block is skipped with a note |
| `STEMSTUDIO_LYRIC_ASSIST_FAKE_SCRIPT` | unset | test/E2E: scripted assistant model output (file under `e2e/lyric-scripts/`), only with `STEMSTUDIO_AGENT_BACKEND=fake` |

## 2. LyricsSheet (010, extended)

| Member | Type | Rules |
|---|---|---|
| `Revision` | int | starts at 1 (existing rows: 1 by migration); +1 on every text or title change (user save, accept, undo) |
| `Sensitivity` | `Loose` \| `Balanced` \| `Strict` | default `Balanced` |
| `RhythmTarget?` | owned `{Bpm 40-220, BeatsPerBar 2-7, BarsPerLine 1-8, SyllablesPerBeat 0.5-4.0 (step 0.25)}` | optional; all four required when set |
| `IntentionalMarks` | owned list `IntentionalMark` | max 200 per sheet |
| `AssistedLines` | owned list `{LineText (normalised, <= 400), SuggestionId}` | max 400; computed on accept; pruned on every save to lines still present (a line edited by the user is `user` again, FR-024) |

`LyricsInput` (010) gets an optional `expectedRevision`; when present and different from the
stored revision the save is refused `409 revision-conflict`. When absent the 010 behaviour is
unchanged (last write wins), so existing clients keep working.

### 2.1 IntentionalMark

| Field | Type | Rules |
|---|---|---|
| `Id` | Guid | |
| `Scope` | `Word` \| `Phrase` \| `Line` | |
| `Text` | string | `Word` 1-40 chars without spaces; `Phrase` 1-200; `Line` 1-400; stored normalised (NFC, trimmed, single spaces); matching is case-sensitive for `Word` (a capitalised form is a separate mark) and exact for `Phrase`/`Line` |
| `Note?` | string <= 120 | optional user note |
| `CreatedAt` | | |

Respected (FR-011): a suggestion whose anchor text equals or lies inside a marked text occurrence
is dropped by `SuggestionFilter`. Marks whose text no longer occurs remain listed as "not in the
text" (the user may remove them) and become active again if the text returns.

### 2.2 LyricsSheetRevision

| Field | Type | Rules |
|---|---|---|
| `SheetId`, `Revision` | composite key | |
| `Title`, `Text` | snapshot | |
| `Cause` | `UserSave` \| `Accept` \| `Undo` \| `Created` | |
| `AcceptGroupId?` | Guid | the accept action (one suggestion or accept-all) that produced this revision |
| `CreatedAt` | | |

Retention: the last 50 revisions per sheet; deleted with the sheet.

**Undo rule** (FR-007): `POST /api/lyrics/{id}/undo` with `expectedRevision` = current revision.
Allowed when the current revision's cause is `Accept` (the newest assistant change); it restores
the previous revision's title and text as a new revision with cause `Undo`, reopens the
suggestions of that accept group (status back to `open`, re-anchored) and removes their
`AssistedLines`. When the current revision is a `UserSave` after the accept, undo is refused
`409 undo-blocked-by-edit` with the number of changed lines; the Expert revision list can restore
any revision explicitly (`POST /api/lyrics/{id}/revisions/{revision}/restore`, cause `Undo`).

## 3. Assist

| Field | Type | Rules |
|---|---|---|
| `Id` | Guid | |
| `SheetId`, `SheetRevision` | | the revision checked |
| `Kind` | `Check` \| `Devices` \| `Alternatives` \| `Continue` \| `Titles` | |
| `Categories` | set of `Spelling`, `Grammar`, `Flow`, `Licence`, `Device` | `Check` default {Spelling, Grammar, Flow, Licence}; `Devices` default {Device, Licence}; writing kinds: fixed (`Alternative`/`Continuation`/`Title`, see 4) |
| `LineFrom?`, `LineTo?` | int 1-based, inclusive | line range; `Alternatives` needs one line (`LineFrom = LineTo`); `Continue` uses `LineTo` as the insertion point |
| `Count?` | int | alternatives 1-5 (3), continuation lines 1-4 (2), titles 1-5 (5) |
| `Instruction?` | string <= 200 | writing kinds and devices only; imitation guard (FR-022) |
| `Sensitivity` | copy of the sheet's | |
| `Language` | `nl` \| `en` | copy of the sheet's |
| `Status` | `Queued` -> `Running` -> `Completed` \| `Failed` \| `Cancelled` | at most one non-terminal assist globally (409 `assist-active`); `Running` assists are `Failed` ("interrupted by restart") at API start-up |
| `Layers` | list `{Layer: Dictionary\|Rules\|Model, Name (e.g. "OpenTaal 2.20.x", "qwen3.5:9b"), Available: bool, Reason?}` | shown in the pane |
| `Backend?`, `Model?`, `UsingFallback` | | null when no model layer ran |
| `BlocksTotal`, `BlocksDone`, `BlocksSkipped` | ints | progress |
| `Counts` | `{Spelling, Grammar, Flow, Licence, Device, Alternative, Continuation, Title}` | shown suggestions |
| `Discarded` | `{InvalidAnchor, InvalidSchema, FailedVerification, Filtered, Verbatim}` | Expert log; `Verbatim` also shown to all levels ("withheld") |
| `Usage?` | `{InputTokens, OutputTokens, CachedInputTokens}` | Claude only |
| `Origin` | `Editor` \| `Agent` | |
| `AgentRunId?`, `AgentStepId?` | Guid | when started by the agent |
| `FailureCode?`, `FailureMessage?` | | `backend-unavailable`, `budget-exhausted`, `refusal`, `timeout`, `interrupted`, `internal` |
| `CreatedAt`, `StartedAt?`, `EndedAt?` | | |

Assists and their suggestions of a sheet are kept until the sheet is deleted; per sheet only the
newest 20 assists are kept (older ones with no open suggestions are deleted).

Synchronous refusals (no assist row is created): `language-unsupported`, `not-own-lyrics`,
`imitation`, `claude-notice-required`, `model-unavailable` (writing kinds and devices when no model
layer is usable; `Check` still runs with the dictionary layer), `revision-conflict`.

## 4. Suggestion

| Field | Type | Rules |
|---|---|---|
| `Id` | Guid | |
| `AssistId`, `SheetId` | | |
| `Category` | `Spelling` \| `Grammar` \| `Flow` \| `Licence` \| `Device` \| `Alternative` \| `Continuation` \| `Title` | |
| `Type` | string, from the closed list below | |
| `Severity` | `Error` \| `Improvement` \| `Idea` | corrections `Error`/`Improvement`; licence notes, devices and writing help `Idea` |
| `Anchor` | `{Line (1-based in the normalised text), Start (UTF-16 offset in the line), Length, Original (<= 400)}` | `Title`: `Line = 0`, `Original` = current title; `Continuation`: `Length = 0` at the end of `Line` |
| `Operation` | `Replace` \| `InsertAfter` \| `SetTitle` \| `None` | `None` = information only (recognised licence note) |
| `Proposal?` | string <= 400 (insertions <= 4 lines, LF-separated) | null for `None` |
| `GroupId?` | Guid | alternatives of one line share a group; accepting one supersedes the others |
| `Explanation` | string 1-300 | in the sheet's language, plain text |
| `Source` | `Dictionary` \| `Rules` \| `Model` | |
| `Model?` | string | model name for `Model` |
| `SyllablesBefore?`, `SyllablesAfter?` | int | verified (R-04) |
| `RhymeWith?` | `{Line, Word}` | for rhyme claims, verified |
| `Status` | `Open` \| `Accepted` \| `Rejected` \| `Intentional` \| `Stale` \| `Superseded` | |
| `AcceptGroupId?`, `DecidedAt?` | | |

**Types** (closed list, contract `SuggestionType`):
`spelling.typo`, `spelling.unknown-word`, `spelling.capitalisation`, `grammar.agreement`,
`grammar.verb-form` (incl. Dutch d/t), `grammar.word-order`, `grammar.homophone`, `grammar.other`,
`flow.stress`, `flow.awkward`, `flow.repetition`, `flow.clarity`, `flow.syllable-count`,
`licence.elision`, `licence.inversion`, `licence.archaism`, `licence.sound-spelling`,
`licence.slang`, `licence.code-switch`, `device.end-rhyme`, `device.internal-rhyme`,
`device.assonance`, `device.alliteration`, `device.imagery`, `device.syllable-fit`,
`device.rhythm`, `write.alternative`, `write.continuation`, `write.title`.

**Re-anchoring** (`AnchorResolver`, pure, FR-008): on every sheet save, for each open suggestion:
if `Original` is at `(Line, Start)` in the new text keep it; else search the unique occurrence of
`Original` on the same line, then on the lines within +-2 (line moved by insert/delete, located
with a line LCS diff between revisions); found -> update `Line/Start`; else `Stale`.

**Accept** (`SuggestionApplier`, pure): applies `Replace`/`InsertAfter`/`SetTitle` for a set of
suggestions on one revision, in descending anchor order so offsets stay valid; overlapping
suggestions in one request are refused (`409 overlapping-suggestions`, listing ids); the result
must pass the 010 normaliser and the 4,096 limit (`422 too-long`). Accepting an alternative
supersedes its group.

## 5. Personal word list

| Entity | Fields | Rules |
|---|---|---|
| `LyricWord` | `Id`, `Language` (`nl`\|`en`), `Word` (1-40, no spaces, NFC), `CreatedAt` | unique (Language, Word); max 5,000 per language; case-sensitive like Hunspell personal words (a lower-case entry also accepts the capitalised sentence-start form) |

## 6. Agent (011 model, additions)

- `PageContext` gains `LyricsId?` (the open sheet in `/lyrics/{id}`); `SendMessageRequest.context`
  gains `lyricsId` (optional).
- `RunStep.DecisionReasons` gains `lyrics-edit` (always on `apply_lyric_suggestions`).
- `JobRef.Phase` / `wait_for_job.phase` gains `lyricAssist` (`jobId` = assist id; polling
  `GET /api/lyric-assists/{id}`).
- `AgentSettings` unchanged. The lyric-assistant Claude notice is stored in `LyricAssistSettings`
  (section 7), so the 011 settings schema does not change.
- Policy (data-model 011 section 6) gains one rule before the risk rules:
  `if tool == apply_lyric_suggestions -> reasons += lyrics-edit` (approval always; never covered by
  a `plan_heavy` grant; not `fresh`).

## 7. LyricAssistSettings (one row)

| Field | Type | Rules |
|---|---|---|
| `ClaudeNoticeAcknowledgedAt?` | timestamp | set by `PUT /api/lyric-assist/settings` with `acknowledgeClaudeNotice: true`; the notice text version `lyric-claude-notice.v1` is stored with it; a new notice version requires a new acknowledgement |
| `ClaudeNoticeVersion?` | string | |

## 8. Song snapshot (010, extended)

`LyricsSnapshot` gains `AssistedLineCount` (int >= 0, from the sheet's `AssistedLines` at the time
of `POST /api/songs` with `lyricsId`; 0 for inline text). Take details show "Lyrics: n lines
AI-assisted" when > 0 (FR-024). File metadata is unchanged.

## 9. State transitions

```text
Assist:      Queued -> Running -> Completed | Failed | Cancelled
             any non-terminal -> Cancelled (POST cancel, sheet deleted) | Failed (API start-up: interrupted)
Suggestion:  Open -> Accepted | Rejected | Intentional | Stale | Superseded
             Accepted -> Open (undo of its accept group)
             Stale -> Open (text restored by undo/restore and re-anchored)
             Rejected and Intentional are final for that suggestion; a later check may propose
             the same change again only when it is not covered by an intentional mark
             (rejections are not remembered across checks: owner may ask, YAGNI)
```

## 10. Events (`GET /api/lyric-assists/{id}/events`, SSE)

Same framing as 011 (`id: <seq>`, `event: <type>`, `data: <JSON>`, `: keep-alive` every 15 s,
replay with `after` / `Last-Event-ID`; stored in `LyricAssistEvents (AssistId, Seq)`, payload
<= 16 KB; the stream closes after a terminal event).

| `event` | `data` |
|---|---|
| `assist.started` | `{assistId, layers, backend, model, usingFallback, blocksTotal, estimatedSeconds}` |
| `assist.progress` | `{blocksDone, blocksSkipped, blocksTotal}` |
| `suggestion.added` | `LyricSuggestion` |
| `assist.note` | `{code: block-skipped \| fallback-model \| quality-note \| verbatim-withheld, message}` |
| `usage.updated` | `{inputTokens, outputTokens, cachedInputTokens}` (Claude) |
| `assist.completed` | `{counts, discarded, durationMs}` |
| `assist.failed` | `{code, message, fix?}` |
| `assist.cancelled` | `{assistId}` |
