# Hand-off: stem-separation + devops + quality-gate (lane L3) / 016 HO:polish

Branch `016-round-lane-l3` (worktree `.claude/worktrees/r16-l3`), not pushed, not merged. Tasks T132
(stem-separation), T137 (devops), T138 (quality-gate). With this, every L3 row of the lane plan is ticked.

## T132 - refactor, docstrings, coverage (stem-separation)

| Change | Commit |
|---|---|
| `main.create_instrument_analysis_parts` split (`_pitch`, `_drums`), `WorkerParts.start` uses `_GpuModels.hooks`, `_GpuModels.reclaim` table-driven (CC 6 -> 4) | 9c00538a |
| `jobs/runtime.Lanes.start` uses `_analysis` and `_sweeping`; `transcription_pipeline._job_warnings` split (`_missing_piece_warnings`) - both had grown over the soft limits with 016 | 9c00538a |
| 016 test units within the soft limits (`test_btc._imports`, the T063 test, `test_views_fake`, the store test helpers) | 9c00538a |
| 19 mypy findings in the 016 test files fixed (misplaced `type: ignore` after formatting, `str` vs the `ChordEngine`/`DrumPiece` literals) | 5befc586 |

`sig_check` on `main.py`, `jobs/`, `api/`, `views_fake.py`, `harmony/btc.py` and every 016 worker test file:
**0 errors, 0 warnings**. Public functions and classes have docstrings (ruff `D` rules clean). Vendored-source record:
`harmony/vendor/btc/NOTICE` names BTC-ISMIR19 commit `2682317be668032e6e4b269ded36adaa2ad57df0` (2020-05-23), MIT
`LICENSE` beside it; `vendor_check` passes.

## T137 - compose, CI, image size (devops)

- `docker compose config -q`: default, `+gpu`, `+e2e`, `+generative`, `+generative-fake`, `+e2e+generative-fake` all
  valid; the worker gets `STEMSTUDIO_ANALYSIS_THREADS`, `STEMSTUDIO_CHORD_ENGINE`, `STEMSTUDIO_PITCH_ENGINE` (empty =
  worker defaults). No stack was started (lane rule).
- CI: every workflow and composite action parses (PyYAML); `scripts/tests` 125 passed with PyYAML (incl.
  `test_ci_e2e_split.py`: the union of `e2e` + `e2e-passes` equals the 015 set plus `instrument` and `fake-mic`, every
  job <= 90 min); `scripts/ci-e2e-pass.sh --dry-run instrument` -> `npm run test:instrument`, `fake-mic` -> `npm run
  test:fake-mic`. No `actionlint` on this PC (not installed; not added as a dependency).
- Fake-mic projects: `e2e/playwright.config.ts` projects `fake-mic-on-pitch` / `-semitone` / `-silence` feed
  `tests/fixtures/audio/{sing-on-pitch,sing-semitone-off,silence}.wav`; these are exactly the names and the default
  output folder of L2's `tests/fixtures/generate_instrument_fixtures.py` (checked on `016-round-lane-l2`), and the
  `fake-stack` action runs every `tests/fixtures/generate_*.py`, so CI generates them before the pass; the
  `e2e-passes` job checks they exist. `npx tsc --noEmit` and `eslint --max-warnings 0 playwright.config.ts` clean.
  `playwright test --list` finds 0 specs here (L5's specs are on its branch); L5's `instrument-chords.spec.ts` carries
  `@instrument` as agreed.
- Image size delta (estimated from the sources, no image build in the lane): worker **+~0.12 MB** (vendored BTC 68 KB
  + 016 worker modules ~47 KB; no new Python package, `uv.lock` unchanged); the BTC weights (12.2 MB + 12.2 MB) are
  downloaded at first use into the models volume (`/models/harmony`), never into the image. Web: + the pitch worklet
  module (L1's build output; the Dockerfile only gained a comment). T139/T140 can record the measured delta with
  `docker image ls` after the integration build.

## T138 - quality gate on the whole repo (quality-gate)

`run_all.py` (this branch): **PASS, 0 errors**, 1851 warnings (-4 after T132), 3649 files, 29,145 units; jscpd ran
(its own output: `duplication: 448 clones, 0 in production code, 1.08% duplicated lines`). `docs/quality/SIG-REPORT.md`
regenerated.

The other lanes' 016 files (`sig_check` over each lane branch's added/changed files in its worktree, read-only):
**0 errors** in every lane; production soft-limit warnings L1 3, L2 18 (incl. web files that are on L2's branch
after its mid-wave merge, owned by L5), L4 48, L5 15. Refactor requests (soft limits only, not blocking):

| Owner | Units (file:line, finding) |
|---|---|
| L1 audio-engine | `engine.ts` `createParts` 17 LOC, `loadTrack` 16 LOC; `recording/capture.ts` `isBlock` 24 LOC |
| L2 beat-analysis/qa | none of L2's own 016 Python production files is over a soft limit |
| L4 backend | (first 20 of 48) `InstrumentViews/AnalysisKindStrategies.cs:114` (size + CC) and `:152` (CC); `AnalysisResultMapper.cs:34,63,110` (size), `:83` (CC); `AnalysisEndpoints.cs:45` (CC); `ChordService.cs:55` `EditAsync` (size), `:114` `FileName` (CC); `AnalysisJobApplier.cs:73`, `AnalysisEstimator.cs:32`, `InstrumentViewMapper.cs:61`, `CatalogueMapper.cs:12` (size); `ApiComposition.cs` `AddStemStudioApi`/`MapEndpoints` grew (24/27 LOC) |
| L5 frontend | `ChordPanel.razor.cs:145` and `:199` (size + CC), `:170` (size); `MidiAction.razor.cs` (size; CC on L2's copy); `Mixer.InstrumentViews.cs:16` `OnChordsL...` (CC); `ScoreForm.cs:91` `Request` (CC); `ChordSheet.razor.cs:83`, `KaraokeBanner.Chords.cs:25`, `TopicParser.cs:32` (22 LOC), `ChordLabels.cs:15`, `ChordSheetLayout.cs:17`, `InstrumentCatalogueCache.cs:21`, `InstrumentFamilies.cs:24` (size); `Components/Scores/ScoreTabFields.razor:40` `@code` block 41 lines (razor-code rule, on L2's copy) |

The full lists are in `.claude/scratchpad/016-round/l3/lanes-sig.txt` (main checkout, git-ignored). The lane
branches move, so T139's `run_all.py` on the merged tree is the binding number.

## Tests and coverage

| Component | Command | Result | Coverage |
|---|---|---|---|
| Worker | `uv run --frozen pytest --cov --cov-branch` (worker/) | 2987 passed, 3 skipped (fonts/cairo), 11 deselected (integration) | **100 % line, 100 % branch** (12,862 statements, 1,446 branches; vendor omitted; after the security fixes) |
| T063 integration | `pytest -m integration tests/harmony/test_chords_integration.py` | 1 passed; 0.0022-0.0070 s per audio second (8 threads, varies with load), frames 81.4 %; with L2's modules overlaid chart 85.4 % (SC-001), keys 17/17 (SC-010) | - |
| scripts | `pytest -c scripts/pyproject.toml scripts/tests` (+PyYAML) | 125 passed | - |
| e2e | `tsc --noEmit`, `eslint playwright.config.ts` | clean | - |

`ruff check`, `ruff format --check`, `mypy src tests` (worker): clean. `dotnet format`: no .NET file changed in this
lane.

Security self-review (whole lane diff since HO:foundational): no new input surface beyond the analysis routes
(pydantic by `kind`, path guard at create and at run, 32-hex job ids, contract caps on hits/frames/segments, clamps
on hit time and strength); BTC checkpoints only with `weights_only=True` and the three allow-listed globals; no
subprocess or shell in the worker parts; `ci-e2e-pass.sh` evaluates only its own constant commands after checking
the pass name.

## Security fixes (orchestrator's independent review, findings 3-5)

| # | Finding | Test (red first) | Fix |
|---|---|---|---|
| 3 | LOW: the weights download (`transcription/models.py`, shared by 008 and `ChordModelStore`) had no size cap | 319807c9: `test_a_download_larger_than_the_pinned_size_is_aborted` (5 MB stream after the pinned bytes -> `TranscriptionError`, no `.part`, at most `size_bytes` + one 1 MB block read); `..._of_exactly_the_pinned_size_is_kept` | 7b008db4: `_HashingWriter` counts bytes and raises `OSError` past `WeightsFile.size_bytes` (handled like any failed download) |
| 4 | LOW: `ready()` re-hashed both 12 MB BTC checkpoints on every `/health` and job | 319807c9: `test_ready_hashes_an_unchanged_file_once` (counting `_file_digest`), `..._again_after_the_file_changed`; plus `test_an_unreadable_cache_entry_is_not_ready` | 7b008db4: digest cached per path keyed by `(st_size, st_mtime_ns)`; `ensure()` drops the entry when it replaces the file; 9cae7cc6 keeps `ensure` within 15 LOC |
| 5 | LOW test gap: no real-torch test with a `__reduce__` payload | 319807c9: `test_the_real_torch_refuses_a_checkpoint_with_a_reduce_payload` (`os.system` writing a marker file; `pickle.UnpicklingError`, marker absent) | none needed (passes against the `weights_only=True` loading) |

`transcription/models.py` and `tests/transcription/test_models.py` are 008 files, edited additively with the
orchestrator's permission. Observation (not changed, other owner): `detail/download.py` (002 model downloads) has
its own `_fetch` without a size cap either.

## L2 request: `harmony` and `analysis_inputs` in the dependency map

Already on this branch since T010 (4eb51ea3, tested in `tools/quality/tests/test_instrument_rules.py`): `harmony`,
`analysis_inputs`, `views_fake` are adapters in `PYTHON_ALLOWED`, with the scoped rules for the harmony core and
`harmony/btc.py`. L2 sees the "not in the dependency map" warnings because its branch has the old `tools/quality`.
Running this branch's `dependency_check.py` on L2's worktree: **0 errors, 0 warnings** (incl. no `notation` ->
`harmony` edge). No change needed; the warnings disappear at the merge.

## Decisions

1. Simultaneous drum hits keep the kit order (`DrumPiece` literal order) instead of the alphabetical order (bug fix,
   T085).
2. T084's "missing pieces listed in `pieces`" read as the contract says: `pieces` = the pieces that had an input.
3. The L2 hooks stay module-path lookups until the merge (HO:foundational decision 2); verified with L2's files
   overlaid. After the merge they can become direct imports (no behaviour change).

## Deviations

None from contracts or scope.

## Requests for the orchestrator

- T139: SC-001 passed with 0.4 points of margin (85.4 %); re-check on the reference machine.
- T140: record the measured image size delta after the integration build (estimate above).
- Relay the refactor requests above to L1, L4, L5 (soft limits only).

## Dependencies added

None.

## Mid-wave fix - the fake drum split delivers a hi-hat (research R-11)

The mid-wave checkpoint on the real stack showed the fake drum hits with kick, snare and toms but no hi-hat: the
fake detail split (`detail/fake.py`, `FAKE_SCALES["drums"]`) had no `hihat` output and `FakeDrumTranscriber` only
plays the pieces it is given. Orchestrator decision: make the fake consistent with R-11.

- Red: `worker/tests/api/test_fake_drum_split.py` (fake drum split -> WAVs -> `FakeDrumHitsTranscriber`: a `hihat`
  stem exists; hi-hats on every eighth from 0.5 s, kick on 1 and 3, snare on 2 and 4).
- Green: `FAKE_SCALES["drums"]` is now `kick 0.5, snare 0.25, toms 0.15, hihat 0.1` (still sums to 1.0, so the
  residual and double-count checks behave as before). The real pipeline (MDX23C DrumSep folding) is unchanged.
- **Cross-owner edit (test infrastructure, orchestrator-approved):** `worker/src/stemstudio_worker/detail/fake.py`
  and its test `worker/tests/detail/test_fake.py` (stem list `kick, snare, toms, hihat, rest`; new scales).
- Worker: 2989 passed, 3 skipped, 11 deselected; **100 % line, 100 % branch** (12,862 statements, 1,446 branches);
  ruff check/format clean, mypy clean (221 files).

### E2E specs that depend on the fake drum split (not edited; for the QA/E2E owner)

- `e2e/support/detail.ts:32` - doc comment still says `kick 0.5x, snare 0.3x, toms 0.2x`.
- `e2e/support/detail.ts:38` - `drums.stems` must become `['kick', 'snare', 'toms', 'hihat', 'rest']`; used by
  `e2e/tests/detail.spec.ts:60` (exact `toEqual` of the outcome table), `e2e/tests/detail-mixer.spec.ts:10`,
  `e2e/tests/detail-mixes.spec.ts:40` and `keptStems`/`expectFakeOutcomesShown` (`e2e/support/detail.ts:83,89`).
- `e2e/tests/journey-levels.spec.ts:17,29` - hard-coded drum details `kick, snare, toms` (a subset; passes only if
  `setExpanded` does not require the exact list - please check).
- `e2e/tests/sheet-music.spec.ts:98-99,111` - comment "no hi-hat or cymbals" and `expect(crossNoteheads(xml)).toBe(0)`:
  with a hi-hat the fake drum score gets x noteheads, so this assertion will fail and should become `> 0`.
- Unaffected: `e2e/tests/lyrics-score-support.spec.ts:63` (inline MusicXML fixture), `e2e/support/api.ts:28`.
