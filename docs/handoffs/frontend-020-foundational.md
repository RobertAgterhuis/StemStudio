# Hand-off: frontend / 020 HO:foundational (T026, T027)

Branch `020-lane-frontend` (from design head ca82a055). Not pushed, not merged.

## Tasks

| Task | Red | Green | Result |
|---|---|---|---|
| T026/T027 | d53645cf | 56cebd55 (+ test split for SIG) | Help areas `insights` ("Stem quality") and `handouts` ("Lesson handouts") in `TopicParser.AreaOrder` after `instruments` and in the help index; nine topics reviewed "020" (`stem-quality`, `stem-quality-tips`, `stem-quality-measures`, `stem-quality-reliability`, `handouts`, `handout-families`, `handout-transposition`, `handout-licence`, `practice-notes`); no G2 topic or entry; seven catalogue entries (help-topics.md); "See also" (`related`) from `track-screen`, `setlist-edit` and `library` |

Contract files written under the approved exceptions: 22e77306 `specs/020-insights-handouts/contracts/help-topic.schema.json`
(help-content 1.2.0 = 016's 1.1.0 + areas `insights`, `handouts`); a4918e43 `help-topics.md` feature id corrected.

## Tests and coverage

| Component | Command | Result | Coverage |
|---|---|---|---|
| StemStudio.Web | `dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true` | 3880/3880 | 99.1 % line, 92.4 % branch (gate 90/90) |

`dotnet build` 0 warnings; `dotnet format StemStudio.slnx --verify-no-changes` clean; `tools/quality/run_all.py` PASS
(0 errors; jscpd ran, no clone in a 020 file; the only 020 finding, a 24-line test, was split).

## Decisions

1. Q1 (orchestrator): help-content 1.2.0 schema in the 020 contracts folder; the test project copies it (as 016 did).
2. Q2 (orchestrator): the Mute tip opens `/tracks/{id}/mixer?mute=<channel>` with the stem muted, unsaved, no API call.
3. Q3 (orchestrator, confirmed): `open-detail` -> `/tracks/{id}/mixer?expand=<parent>`; ingredient create links go only
   to existing flows (score dialog, lyrics/chords card, mixer, detail section, practice notes editor).
4. Q4 (orchestrator): feature id `practice-notes.edit` -> `practice.notes-edit` (the 004 id pattern allows no hyphen in
   the first segment); help-topics.md corrected.
5. Topic anchors (`quality-panel`, `handout-menu`, ...) are added with the markup in HO:us1..us4: the 004 coverage
   suite requires every literal anchor to appear in the markup.
6. Search: the keywords "copyright" and "kick" are not used in the new topics, so the 004 reference queries keep their
   first hits (`legal-notice`, `detail-outcomes`).

## Deviations

None (the two contract edits are approved exceptions).

## Open items

None for this checkpoint. Next: US1 (T046/T047).
