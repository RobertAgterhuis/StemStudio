# Hand-off: architect, 009/010 contract sync with the 005-008 reconciliation

## What drifted

The 005-008 reconciliation (commit be286b4, merge 57b5190) changed backend-api 1.7.0
(`specs/008-sheet-music/contracts/backend-api.openapi.yaml`) after the 009/010 Phase 0 had copied it
into 1.8.0 (009) and 1.9.0 (010). Backend entries that differed between 008 and 009/010:

- `paths`: `/api/tracks/{trackId}/lyrics/search`, `/lyrics/align`, `/lyrics/align/cancel` (all lyrics
  409 types in the descriptions), `/api/assets/scores` (`409 source-unavailable`, `503 WorkerUnavailable`).
- `components/responses`: `LyricsConflict` (409 types and examples), `ScoreConflict` (`source-unavailable`).

Other documents checked; no drift:

- Worker: 009/010 have no worker-api copy; the tests link the 008 worker document directly.
- Generator 1.0.0/1.1.0 and the 009/010 `ui-test-ids.md`: not touched by the reconciliation.
- Agent delta (011): standalone, it does not repeat 1.7.0 entries.
- `ExportConflict` and the other responses were already identical.

## Fix

The be286b4 hunks for the 008 backend document were applied unchanged to the 009 and 010 documents.
No version bump, because the change describes behaviour that is already built. 008 -> 009 now differs
only in `Track`, `AssetOrigin`, `Provenance`, `AudioFacts`. 009 -> 010 differs only in the named 010 entries.

## Files changed

- `specs/009-style-transfer/contracts/backend-api.openapi.yaml`
- `specs/010-lyrics-to-song/contracts/backend-api.openapi.yaml`

## Verification

- `npx @redocly/cli@2.78.0 lint` (009 and 010 `redocly.yaml`): valid. 4 and 6 warnings remain, all of them
  existing `operation-4xx-response` / `no-unused-components` warnings.
- `dotnet build StemStudio.slnx -warnaserror`: 0 warnings, 0 errors.
- `dotnet test StemStudio.slnx -p:CollectCoverage=true`: exit 0.
  - Domain: 555 passed, line 98.94 %, branch 95.82 %.
  - Agent: 48 passed, line 100 %, branch 100 %.
  - Persistence: 77 passed, line 98.4 %, branch 92.85 %.
  - Web: 798 passed, line 99.27 %, branch 92.56 %.
  - Api: 1068 passed, line 99.56 %, branch 95.12 %.
- Generator `uv run --frozen pytest -q`: 309 passed.
- Worker `uv run --frozen pytest -q`: 2378 passed, 3 skipped (cairo/font on Windows), 10 deselected.
- `tools/quality/run_all.py`: 1417 files, 0 errors, 357 warnings, exit 0.

## Dependencies added

None.

## Next owner

CTO: merge into `009-010-generative`.
