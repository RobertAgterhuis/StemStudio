# Hand-off backend-023: all stories (US1-US6) and polish

Lane backend-023, branch `r22-lane-be023`, 2026-11-17. Follows `backend-023-foundational.md`. All 24 backend rows of
`specs/023-lyric-writing-assistant/tasks.md` are ticked; `PendingLyricAssistOperations` is empty (all 28 operations of 1.23.0
are served).

## Commits (after the foundational checkpoint)

| Commit | Tasks |
|---|---|
| 76dc93f2 | T025/T026 licence lexicon (`licence-lexicon-{nl,en}.txt`), `DictionaryCheck`, `SuggestionFilter`, `HunspellSpellChecker` |
| 5ea93199, 4b418473 | T027/T028 assist endpoints, `LyricAssistRunner` (one assist globally), `LyricAssistExecutor`, `AssistSink`, events + SSE, start-up recovery, security review 4/5 |
| 24c19f15 | T029/T030 suggestions, accept/reject/intentional, marks, words, undo, revisions, sheet settings |
| 3f144067 | T038/T039, T045/T046, T047/T048 syllables, rhymes, language guess, corpus index, `ILyricVerification` adapter, `AddLyricAssistAgent` |
| 8724a1ba | T061/T062 status + settings (GET/PUT), Claude notice, no-outbound proof, fake stack test |
| (T074/T075 commit), dccb37e2 | own-work override; T056/T057 `LoopbackLyricAssistActions`, song snapshot `assistedLineCount` |
| refactor + 16047c6f + c38666e4 | SIG/duplication/formatter, process-wide data cache, sink anchor re-check, edge-case tests |
| merges | 022-023-round (afa126df, c46233d0, 5f282f51), ai-agent seam 88c6af4a and async factory 4d05cf52 |

## Tests and coverage

See the table in the final report (numbers of the last full run). Gold lists: Dutch syllable counter and English counter pass the
SC-007 thresholds (>= 97 % / >= 95 %), all 91 rhyme vectors pass, rhymes answer within 500 ms on the real word lists (warm).

## Decisions (interim, for CTO review)

1. Revision on every save (contract wording); block range starts at a leading marker; insertion-point anchors follow their line.
2. Sensitivity filter applies to `check` only; devices and writing kinds are explicit requests (not filtered by sensitivity).
   Intentional marks apply to `check` and `devices`, not to writing help for a marked line.
3. A recognised licence (lexicon word) is never an `error`; a model correction whose whole anchor is lexicon words is dropped.
4. Sound spelling needs a run of 3+ equal letters (`zooooo`, `yeahhh`) or an interjection, so Dutch double vowels (`vergeeten`) stay misses.
5. Own-lyrics guard (CTO security decision): **8 matching lines always refuse** besides the spec rule (>= 3 and >= 25 %);
   verbatim check also over the model's `explanation` (defence in depth in the sink). Deviation from R-07, tightened.
6. Dictionary layer bounded: words > 40 characters skipped, 200 misses per check, `Suggest` cached per word, cancellable.
7. The sink re-checks every model anchor against the checked text (`invalidAnchor`) and caps model/backend names.
8. Suggestions are listed in text order (title, then line, offset), not creation order.
9. Dutch rhyme candidates are OpenTaal **stems** only (`nl.dic` lists `kracht`, not the inflected `lacht`); English from CMUdict.
   Rhyme ranking checks at most 400 shortest candidates per kind (bounded work).
10. Parsed dictionaries/CMUdict/word lists are cached per process (`VendoredDataCache`); this also removed the CPU contention that
    made 011 timing tests flaky in the Api suite.
11. `Origin` of assists started through the agent loopback is `editor`: `CreateLyricAssistRequest` has no origin/run member
    (contract gap below).

## Contract gaps / requested edits (nobody-owned files)

- `CreateLyricAssistRequest` has no way to mark an agent-started assist (`origin: agent`, `agentRunId`); data-model 3 wants it.
  Proposal: an optional `agentRunId` set only by the loopback (or a trusted header). Until then the loopback assists show `editor`.
- SSE stream concurrency cap (security checklist): the contract declares no 429/503 for `streamLyricAssistEvents`; implemented
  instead: idle timeout (30 min without events, client reconnects with `after`), keep-alive, close for a deleted assist, bounded `after`.
- `ServedOpenApiDocument` / `Contract_EveryOperationHasAnOperationId` now consistent (5f282f51); nothing else requested.
- THIRD-PARTY-LICENSES unchanged by this lane (dictionaries were vendored by the architect; no new package).

## New dependencies

None (WeCantSpell.Hunspell was pinned in Phase 0).

## Security self-review

Untrusted model output: schema-validated by the assistant, then in the sink anchor re-check, `SuggestionFilter`, verbatim check
over proposal and explanation, length caps (proposal 400, explanation 300, note 300, model 100), at most 400 suggestions per assist;
text changes only through accept/undo/restore of stored suggestions of the same sheet (hostile-output test). Prompt injection:
sheet text only reaches the assistant through `LyricAssistJob`; agent loopback results carry no text (test asserts it). Own lyrics:
sheet guard before every assist (with the 8-line rule), override covers one track only and never verbatim/imitation. Problem details
never echo sheet text (track titles only). Claude notice checked server-side before any Claude model call. No secrets or lyric text
in logs (runner logs the exception type only). Every route sits behind the global cross-site guard and the 013 maintenance
middleware; the runner waits on `LibraryGate` before a queued assist and between blocks. Inputs bounded (65,536 raw text, 200
instruction, 100 ids, 40-character words, limit 60).

## Open / next

- T068 (ai-agent) measures the model layer; nothing in this lane waits for it.
- Integration: re-add the `AddLyricAssist` migration after 022's (CTO step); `LyricsSheetConfiguration` uses `HasSentinel` so the
  `Balanced` default survives the regeneration.
