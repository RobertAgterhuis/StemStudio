# Hand-off: quality-gate / 006 foundational (006:T009, 006:T010)

Branch from `006-008-lyrics-sheet-music` at 62d107e (Phase 0). The 008 rules
(`quality-gate-008-foundational.md`) were built right after this one on the same branch.

## What was built

- **Worker rules** (`PYTHON_ALLOWED`): `lyrics` and `alignment` may import only `ports`,
  `schemas` and `config`. `jobs`, `api` and every other adapter importing either one fails the
  gate. Only `main.py` composes them.
- **Sub-module exception** (`PYTHON_SUBMODULES_ALLOWED`): `lyrics` may also import the shared
  egress modules `metadata.http`, `metadata.ratelimit` and `metadata.user_agent` (and names inside
  them). Every other `metadata` module is forbidden, including `from stemstudio_worker import
  metadata` and look-alikes such as `metadata.httpx`. The error names the imported module and
  lists the allowed ones, for example
  `forbidden dependency lyrics -> metadata.images (allowed: metadata.http, metadata.ratelimit, metadata.user_agent)`.
  `alignment -> metadata.http` stays forbidden.
- **Import parsing**: worker edges now carry the dotted module (`Edge.module`) and are recorded
  per imported name. One statement still produces at most one finding (findings are
  de-duplicated). Cycle detection is unchanged and works on top-level modules, so
  `metadata.http -> lyrics` reports both the forbidden edge and the cycle
  `lyrics -> metadata -> lyrics`.
- **Engine module rule** (`TS_MODULE_FORBIDDEN`, `read_ts_module_edges`): inside
  `src/audio-engine/src/`, `transport` and `mixer` never import `karaoke/`. A module is a
  top-level file or folder, so `transport.ts` and `transport/**` both count as `transport`. The
  rule covers static, type-only, `export ... from` and dynamic `import()` relative specifiers.
  Comments, npm packages, look-alike names (`./karaoke-text`) and test files are not edges.
  `karaoke -> transport` (reading the `TransportClock`) and `engine.ts`/`index.ts -> karaoke`
  are allowed.
- **Ownership** (`common.OWNERS`): `lyrics/` and `worker/tests/lyrics/` belong to audio-ingest.
  `alignment/`, `worker/tests/alignment/` and `worker/scripts/` belong to stem-separation
  (006 plan ownership map). The exception is `worker/scripts/install_music_font.py`, which stays
  with the architect (008 ownership map).
- **SIG report**: two new component rows, `worker/lyrics` and `worker/alignment`.

## Files changed

`tools/quality/{dependency_check,common,components}.py`, `tools/quality/README.md`,
`tools/quality/tests/{test_lyrics_rule.py (new), test_components.py}`,
`docs/quality/SIG-REPORT.md`, `specs/006-synced-lyrics-karaoke/tasks.md` (T009, T010 ticked).

## Tests and coverage

TDD: the failing tests were committed first in fa85fd7. At that point the new module failed at
collection (missing `PYTHON_SUBMODULES_ALLOWED`) and `test_components` had 1 failure. The
implementation followed in 5d693b8.

```text
uv run --directory tools/quality pytest --cov --cov-branch        (after 006)
367 passed
TOTAL 979 stmts, 0 missed, 200 branches, 0 partial -> 100.00 % line and branch
```

Final numbers after 008 on the same branch: 437 passed, 1037 stmts and 216 branches at 100 %
(see the 008 hand-off).

## Quality checks

```text
uv run --directory tools/quality ruff check .            All checks passed!
uv run --directory tools/quality ruff format --check .   all files formatted
uv run --directory tools/quality mypy                    Success: no issues found in 31 source files
python tools/quality/sig_check.py tools/quality          0 errors, 0 warnings
python tools/quality/dependency_check.py                 dependency_check: 0 errors, 0 warnings
uv run --project tools/quality python tools/quality/run_all.py --report docs/quality/SIG-REPORT.md
quality gate: 867 files, 6890 units, 50 clones, 1 exemptions, 0 errors, 210 warnings -> exit 0
```

The base is 62d107e, which includes the 007 backend merge, so these counts are higher than the
Phase 0 hand-off's (784 files, 163 warnings). No warning comes from a 006 file.

## Dependencies added

None.

## Open issues / requests

- **audio-ingest (006:T013+)**: `lyrics/**` may import `ports`, `schemas`, `config` and only
  `metadata.http`, `metadata.ratelimit` and `metadata.user_agent`. Any other `metadata` helper you
  need (for example title cleaning in `metadata.titles`) fails the gate. In that case ask the
  architect to move it to `ports`, or ask me to widen the allow-list, which needs a plan
  amendment.
- **stem-separation (006:T017+, T029+)**: `jobs/lyrics_*` and `api/lyrics_*` get the
  `LyricsLookup` and the `Aligner` from `main.py`. `alignment/**` gets the `DeviceRunner`
  injected and must not import `devices`.
- **audio-engine (006:T025+)**: keep `karaoke/` out of `transport.ts`/`mixer.ts`. Wire it in
  `engine.ts`. `karaoke` may import `transport` (read-only clock).
- `tasks.md`: T009/T010 are ticked. T063 (HO:polish) is left open on purpose.

## Next owner

quality-gate 006:T063 + 008:T078 (HO:polish) after the 006/008 owner branches merge. Then CTO
(merge; regenerate `docs/quality/SIG-REPORT.md` rather than hand-merging it).
