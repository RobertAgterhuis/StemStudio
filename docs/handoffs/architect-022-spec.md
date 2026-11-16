# Hand-off: architect / 022-spec (library by artist and album, design round)

## What was built

Design only (no product code) for feature 022, owner request 2026-11-16: saved items filed
automatically into Artist > Album > Song folders in the app, folder art, live search over artist,
album, song and stem/instrument.

- Spec Kit artefacts in `specs/022-library-folders/`: spec (US1-US5, FR-001..FR-038, SC-001..SC-008),
  requirements checklist, research (R-01..R-13, spike S-01), data-model, plan (constitution check
  PASS, ownership map, complexity tracking), quickstart with validation checklist, tasks (79 tasks
  incl. 1 conditional devops task, 28 red-green pairs, lane plan for max 4 parallel agents).
- Contracts (standalone deltas): backend-api **1.22.0-delta.1**, ui-test-ids **1.22.0** delta, agent
  tool catalogue **1.1.0** delta, `DELTA.md` with the merge rule, `redocly.yaml`.
- ADR-036 (Proposed). MoSCoW S13 (Should) and ROADMAP rows (feature table, version allocation, note).

Key decisions: folders derived from a placement stored per item (no folder tables, never empty);
per-level name precedence item > track > 005 match > import names > fallback; normalised keys
(case, accents, special letters, quotes/dashes, whitespace, leading "The", featuring); filing at
creation, re-filing on band info/library info changes, start-up backfill by filing-rules version
(also covers 013 restores); folder art reuses the 005 pictures (song = track, album = best by 005
precedence, artist = mosaic of up to 4 album covers, else lettered placeholder; no artist photos);
server-side live search over an in-memory index (spike: build 21 ms, search p95 < 1 ms at 5,000
items), 200 ms debounce; `/library` opens folders, the 007 flat list stays at `/library?view=all`.

## Files changed

- `specs/022-library-folders/{spec,plan,research,data-model,quickstart,tasks}.md`
- `specs/022-library-folders/checklists/requirements.md`
- `specs/022-library-folders/contracts/{DELTA.md,backend-api.library-folders.openapi.yaml,redocly.yaml,ui-test-ids.delta.md,tool-catalogue.delta.md}`
- `docs/decisions/ADR-036-library-by-artist-and-album.md`
- `docs/MoSCoW.md`, `docs/ROADMAP.md`
- `docs/handoffs/architect-022-spec.md`

## Tests and coverage

No code; no tests to run. Contract validation:
`npx @redocly/cli@2.78.0 lint --config specs/022-library-folders/contracts/redocly.yaml specs/022-library-folders/contracts/backend-api.library-folders.openapi.yaml`
-> "Your API description is valid", 0 errors, 0 warnings (OpenAPI 3.1; external `$ref`s to the
011 document resolve).

Spike S-01 (scratchpad file app, `dotnet run -c Release`, .NET SDK 10.0.401): fold+build 5,000
items x 5 fields 21.0 ms, 708 KiB folded text, 12 queries x 50 runs median 0.12-0.40 ms, p95 <= 0.89 ms.

## Quality checks

Markdown only; no analyzers apply. SIG limits are planned into the component split (plan.md).

## Dependencies added

None.

## Docs impact

User-visible: the Library page opens folders by artist, album and song with pictures; live search;
"Edit library info" (Advanced); "All items" view (Advanced); save confirmations name the folder.
User manual "Library" chapter and the technical manual (API, start-up filing) need updates in the
integration wave (T093).

## Open issues / requests

1. **CTO**: accept ADR-036 and the UI-test-id treatment: `/library` defaults to the folder view and
   `library-search` becomes visible at Beginner; handled as a 1.22.0 minor with a three-file test
   migration (T022, frontend test in T046/T052) instead of a major bump. Confirm or ask for a major.
2. **CTO**: version allocation assumes the claude-key follow-up keeps 1.21.0 (its row lives on branch
   `011-claude-key-spec`, not yet on `main`; constitution 1.4.0 also lives there). ROADMAP merge of
   the two branches will touch neighbouring lines.
3. **CTO**: ADR number 036 assumes ADR-035 (claude-key) - renumber at merge if needed.
4. Owner questions (genuine product choices, defaults already designed): see the final report.

## Next owner

CTO (review, owner questions), then architect for Phase 0 part 2 (T002-T005) when 022 is scheduled.
