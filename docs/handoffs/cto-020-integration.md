# CTO hand-off: round 020 integration (2026-11-23)

Branch `020-insights-handouts` (worktree `.claude/worktrees/020-insights-handouts`), design head `ca82a055`. Not pushed,
not merged to `main` (the orchestrator does that). Logs: `.claude/scratchpad/020-round/integration/`.

## Merges (`--no-ff`, in order)

| Branch | Merge | Conflicts |
|---|---|---|
| `020-lane-qa-ops` | merged | none |
| `020-lane-beat` | merged | none |
| `020-lane-worker-io` | merged | none |
| `020-lane-backend` | `1ac8041e` | none |
| `020-lane-frontend` | `560aaed3` | `specs/020-insights-handouts/tasks.md` (ticks, union) |
| `main` `bf7df94e` | `d8b455f5` | none |

## Fixes in this integration (test-first; red commit before green)

| Commits | What |
|---|---|
| `c5cb7f5f` | Contracts: backend `1.19.0-delta.3`, worker `1.11.0-delta.3` (full documents rebuilt, `merge_full.py --check` superset yes, Redocly 2.78.0 valid with the 33 documented warnings). No `x-stemstudio-write-gate` on the 020 writes; `409 handouts-active`; `getHandoutOptions` `409 too-many-songs`; job limits named; warning codes `notation-omitted`, `artwork-omitted`; `CompletedHandoutJob.outline` in data-model 6.4 order |
| `818f4683` / `80f6de98` | API: `RequireGateOpen()` removed from requestQualityReport, createHandout, setPracticeNote (13 policy; maintenance 503 stays); `InsightsProblemTypes` (Contracts) with `handouts-active`; options answer 409 above `MaxSetlistHandoutSongs` and read nothing; `HandoutJobLimits` mirrors the worker's 8 MiB body, 500,000 characters (Unicode scalar values), 30,000 layout cells and 128 MiB notation (`NotationItem.SizeBytes`) and answers a readable 422 `document` before submitting; Domain/DTO warning enums |
| `cdcf97af` / `ce1f566b` (worker agent) | Worker warns `notation-omitted` (frame or byte budget) and `artwork-omitted` (not JPEG or > 40 MP); `87cea80b` fontTools 4.68.0 + threadpoolctl 3.8.0 direct pins; `07dcd6d5` `libharfbuzz-subset0` in the worker image; `f7094483`/`ac6a8af4` quality gate allows PIL in `handouts` (`insights` was already in the dependency map); `fc3dfaa3` mypy of the beat lane's insights tests |
| `b9704d30` / `4b8668fa` | Wording review: artefacts reason "(short dropouts in the sound)" (flicker not shipped); quality refusal without retry advice; worker-unavailable says "processing service" |
| `025778b5` / `b04ebc28` | `StemStudio:MaxSetlistHandoutSongs` validated at start (1-50) |
| `46c265f7`, `facc182b` | `LyricAssistEdgeTests.Stream_ClosesWhenTheAssistIsGone` flake: wait for the first SSE event, assert the end; FLAKES.md row |
| `ef167736`/`8bb2dbc7`, `426328b4`/`b3259823` | Found by the visual run: the mixer quality badge "Probably not in this song" turned into a circle with the text spilling out; control radius, `max-inline-size`, icon wraps first |
| `05e8902b` | 54 visual baselines regenerated (Playwright container v1.65.1, fresh stack) and inspected: track page (quality, handout, practice note cards), its dialogs, mixer (badges), setlist editor (handout button) |
| `58aabbe9` | 020 specs on their first real-backend run: one-click melodic is the C handout (only a B-flat part in the fixture, so notation is omitted); Beginners have no "All items" (open the song folder); one band copy per browser project for "Split further" |
| `c32acc97` | **Owner decision 2026-11-23**: Firefox removed from the E2E setup (project, scripts, CI matrix, browser install, Firefox-only smoke and skips, helpers); docs and decision log updated |
| `b1f998c0`, `c4cdf03f`, `0bde9017`, `400b0bda` (docs agent + CTO) | data-model 2.5/4.2-4.5/4.6, research R-02/R-03 (T108), licence rows (T106), help topics, user manual chapter 23, technical manual, decision log, STATUS/ROADMAP/CHANGELOG, backlog, LESSONS |
| `7212e17a` | Manuals rebuilt last (user 88 pages, technical 66 pages; `check.mjs` up to date) |

## Verification

| Check | Result |
|---|---|
| .NET (Release, coverage, mid-run) | Api 1 failed (the lyric-assist flake test, corrected in `facc182b`); final numbers in the `scripts/test-all.sh` row |
| worker (agent run) | 4216 passed, 29 skipped, 1 xfailed; 99.73 %; image `tests/handouts` + limits + input guard 140 passed, `handouts/**` 100/100 %; ruff, mypy --strict clean |
| contracts | `merge_full.py --check` superset yes; Redocly valid (33 documented warnings) |
| e2e lint / typecheck | clean |
| visual (container, fresh stack) | 160 passed, 6 skipped after the regenerated baselines |
| a11y (Chromium) | 213 passed, 22 skipped, 1 one-off (archive listed twice once; rerun 6/6) |
| E2E default, Chromium | 389 passed, 60 skipped, 1 failed (`library-care-cleanup:69`, contaminated by a mistaken filtered run on the same stack; rerun 7 passed) |
| E2E default, Firefox | stopped at 110/450 by the owner decision (no failures until then) |
| pass `insights` | 48 passed (Chromium + Firefox, before the decision); 24 Chromium |
| passes recording / practice-flow / instrument / fake-mic / lyric / agent / gpu / restart (Chromium) | 18 / 4+1s / 40 / 3+1s / 15 / 15 / 4+2s / 1+2s passed, 0 failed |
| Copy-first migration (owner DB copied read-only) | before: last `AddTrackAnalyses`, Tracks 13, Mixes 4, Assets 15; after `AddQualityReport` + `AddPracticeNotes`: same counts, `PracticeNotes` 0, `Tracks.QualityReport` and `Assets.Handout` present, `integrity_check` ok; rollback `MigrateAsync("20261118055631_AddTrackAnalyses")` keeps every count and the `IX_Assets_Folder` index; up again = after |
| `scripts/test-all.sh` (full, after the manual rebuild) | first run: 4 findings (quality-gate parameters + a cross-lane clone, site featureMap, ruff of the new no-Firefox test, a bUnit race), fixed in `242204c0`; rerun **exit 0, 90/90 steps**: Domain 4331, Persistence 195, Api 3739 + 4 skipped, Web 4045, Agent 995, AgentEval 178 (line/branch: Api 98.63/91.14, Web 99.1/92.52, Domain 98.73/93.85, Persistence 99.56/93.26, Agent 99.26/94.33, AgentEval 98/90.65); worker 4216 passed, 99.73 %; generator 99.05 %; `run_all.py` PASS (0 errors, 540 clones, none new in production); contracts lint and merge checks, secret scan, manuals up to date, site check, e2e lint/typecheck/list all passed |

Stack: compose project `e2e020i` (ports 39080/39081, images `e2e020i/*:local`, fake separator), retries 0, suites one
after another; torn down with volumes and images; the owner's `stemstudio` project untouched.

## Code-review fixes (branch `020-fix-cr`, backend; red `c296b227`, green `915af4f1`)

| # | Finding | Fix |
|---|---|---|
| 1 | Handout PDF above the 64 MiB download cap stayed Rendering and was downloaded every tick | `WorkerClient.MaxHandoutBytes` 192 MiB (128 MiB notation total + 64 MiB margin for the 8 MiB body, fonts, covers <= 200 KB each; streamed to disk); `WorkerResponseTooLargeException` fails the item with `HandoutJobApplier.TooLarge` |
| 2 | Score key from the first item of the whole song list, not transposed | `HandoutKeys` (Domain): chord key, else the first concert-pitch score (score/tab, no transposed part or drums) of the family notation, then of the song, written for the transposition; music21 tonics parsed (`B-`), spelled by the chord rule, unreadable tonic = no key |
| 3 | One bad quality job stopped the pass | `TrackPass` per item; a `DbUpdateConcurrencyException` detaches the track so later reports still save; only `WorkerUnavailableException` ends the pass |
| 4 | Setlist table in concert keys | Same key rule as the facts, in the handout's transposition |
| 5 | Retry outside the start lock and the cap | Retry under `HandoutGate`: stored status read again (`IAssetRepository.StoredStatusAsync`, a second click gets 409 `asset-not-failed`), counted against `MaxActivePerSource` (the failed item itself is not active) |
| 6 | Silent drops | > 8 notation items per song: planned `notation-omitted` warning (kept on the item, put before the renderer's, at most 50); `Guid.Empty` in `notationAssetIds` 422; setlist positions close up when a track is gone |
| 8 | Quality job left running when the track is deleted | `TrackDeletionService` removes a queued/running worker quality job, best effort (logged) |

Verification: Release build `-warnaserror` 0 warnings; `dotnet format --verify-no-changes` clean; .NET suites all passed
(Domain 4347, Persistence 196, Api 3758 + 4 skipped, Web 4045, Agent 995, AgentEval 178); line/branch Api 98.62/91.03,
Domain 98.73/93.91, Persistence 99.56/93.26, Web 99.1/92.52, Agent 99.26/94.33, AgentEval 98/90.65; `run_all.py` PASS
(0 errors, 543 clones, the 3 new ones in tests only). No UI texts changed; the worker is untouched.

For a decision: (a) a setlist song whose track is deleted meanwhile has no contract code (none of `notation-omitted`,
`artwork-omitted`, ... fits); it is left out without a warning, positions renumbered. A code such as `song-omitted` needs a
contract change. (b) Contract wording: `retryAsset` 409 should name `handouts-active` (and 422 `document`, already returned
before this fix); `HandoutWarning.code` says "the worker's warnings" but `notation-omitted` now also comes from the API.

## Open items

- Owner: MUSDB18-HQ for G1/G2; confirm the two setlist PDFs (014 stand PDF + 020 setlist handout).
- Backlog: 4-stem tracks get no quality report; stale save link after the band-info lookup (candidate
  `library?asset=<id>`); a family-neutral flicker measure.
- Watch (FLAKES.md): duplicate archive row in the backup list (once); `library-care-backup` write gate (Chromium passed).

## Time per phase (2026-11-23)

| Phase | Time |
|---|---|
| Read-in and merges | 16:50-17:05 |
| Contract and API fixes, wording, flake (worker and docs agents in parallel) | 17:05-17:55 |
| .NET coverage run, visual runs and the badge fix, baselines | 17:35-18:25 |
| E2E: insights, a11y, default Chromium (Firefox stopped by the owner decision) | 18:25-19:20 |
| Firefox removal, feature passes, reruns, teardown | 19:20-19:50 |
| Manual rebuild, `scripts/test-all.sh` x2 with fixes, hand-off | 19:50-20:40 |
