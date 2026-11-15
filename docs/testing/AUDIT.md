# Test evidence audit (T110)

Auditor: qa. Criteria (constitution I, ADR-006, ADR-008, TEST-STRATEGY.md section 6):
test-first commits (red commit before green), fakes instead of network/subprocess/model, exactly
one `@pytest.mark.integration` test (the real Demucs one), coverage >= 90 % line AND branch with
exact numbers, owners do not read `tests/fixtures/`.

**Scope of this pass (wave, 2026-11-14).** Only hand-offs already merged into
`001-stemstudio-mvp` could be audited: architect, audio-ingest (us1, polish), beat-analysis (us1,
polish), devops (foundational), quality-gate (foundational). backend, frontend, audio-engine and
stem-separation were still in their worktrees; they are re-audited after T119 (integration
wave) with the same criteria.

## Summary

| Owner | Red -> green | Fakes | Integration marker | Coverage (line / branch) | Verdict |
|---|---|---|---|---|---|
| audio-ingest | yes, 5 task pairs, red commits listed (`ImportError`, "5 failed") | yes: fake `YoutubeDL` factory, fake spotDL client/matcher, fake `CommandRunner`, fake modules via `sys.modules` | **finding F1**: one `integration` test (real ffmpeg) | 100 % (446/446) / 100 % (84/84), 201 passed | accept with F1 |
| beat-analysis | yes, 3 pairs (`b487cf0`->`ecdb1ef`, `63d1b07`->`9e43914`, `f2aa7da`->`500642d`) | not needed (real librosa on in-test synthetic signals, `tmp_path`) | none | 100 % (142/142) / 100 % (14/14), 41 passed | accept |
| quality-gate | yes (`f518964` "8 errors during collection" -> `1bd7528`) | fake subprocess for jscpd, synthetic samples | n/a | 100 % / 100 % (575 stmts, 116 branches), 119 passed | accept |
| devops | n/a (no measurable code; validation outputs) | n/a | n/a | n/a | accept, see F3 |
| architect | n/a (skeleton) | n/a | n/a | gates configured in `Directory.Build.props` | accept |
| backend, frontend, audio-engine, stem-separation | pending (not merged at audit time) | | | | re-audit after T119 |

`git grep tests/fixtures` over `worker/tests`, `tests/StemStudio.Api.Tests` and
`src/audio-engine/tests` on `001-stemstudio-mvp`: no matches; owners keep their own test data (G6).

## Findings

- **F1 (audio-ingest, constitution I "exactly one integration test")**:
  `worker/tests/ingest/test_normalizer.py:271` is marked `@pytest.mark.integration` (real ffmpeg on
  a `lavfi` tone). Together with stem-separation's T060 Demucs test this makes two markers. It is
  deselected by default, so the 90 % gate is unaffected, but the rule is explicit. Request: move the
  real-ffmpeg check to a different marker (e.g. `ffmpeg`, registered in `worker/pyproject.toml` by
  the architect) or drop it, since the E2E suite already exercises real ffmpeg on uploads
  (`import.spec.ts`). Owner: audio-ingest (+ architect for the marker).
- **F2 (audio-ingest / beat-analysis, informational)**: both report one deliberately failing case
  in the full worker run caused by a contract/vector question (recorded in their open issues). The
  CTO must resolve these before T120, otherwise the worker gate is red.
- **F3 (devops, CI command drift)**: CI runs `mypy --strict tests/fixtures` and
  `pytest tests/fixtures/tests --cov=tests/fixtures` without `tests/fixtures/pyproject.toml`.
  qa made the generator pass both forms (commit `b7fe6e0`: 100 % line and branch either way). The
  `e2e-lint` job runs `npm run lint` but not `npm run typecheck`; request: add it.
- **F4 (beat-analysis, informational)**: reliability thresholds were calibrated on synthetic
  signals; the E2E fixture `music_100bpm_20s.mp3` (bass, chords, kick/hat, accent clicks) gives the
  first real-pipeline data point in T122 (`track-bpm` within 3 of 100).

## Re-audit checklist (after T119)

For backend, frontend, audio-engine, stem-separation: red commit precedes green commit per task
pair; coverage commands from tasks.md give >= 90 % line and branch (exact numbers); worker
separation/jobs tests use fake Demucs loader/applier and fake clock; the only remaining
`@pytest.mark.integration` is the T060 Demucs test; bUnit tests assert the ui-test-ids.md ids
that `e2e/pages/` uses.

## Re-audit after T119 (integration wave, qa T122/T123, 2026-11-15)

Scope: backend, frontend, audio-engine, stem-separation (plus worker-integration) on the merged
`001-stemstudio-mvp` tree. Coverage was re-measured by qa, not copied from the hand-offs.

| Owner | Re-measured (qa) | Hand-off claim | Verdict |
|---|---|---|---|
| backend Domain | 173 passed; 97.73 % line / 97.72 % branch | same (backend-polish) | accept |
| backend Persistence | 21 passed; 99.03 % / 100 % | same; devops-polish reported 83.33 % branch | accept, see R2 |
| backend Api + Contracts | 220 passed; Api 99.88 % / 98.18 %, Contracts 100 % / 100 % | same | accept |
| frontend Web | 184 passed; 99.77 % / 94.36 % | same (frontend-polish) | accept |
| audio-engine | 121 passed; 100 % lines (432/432) / 100 % branches (147/147) | same | accept |
| worker (stem-separation + all) | 413 passed, 2 deselected; 100 % (1591 stmts) / 100 % (188 branches) | same (worker-integration) | accept, see R1 |

Commands: `dotnet test tests/StemStudio.{Domain,Persistence,Api,Web}.Tests -p:CollectCoverage=true`;
`cd worker && uv run pytest -q --cov=stemstudio_worker --cov-branch`;
`cd src/audio-engine && npm run test:coverage`.

- **R1 (audio-ingest + architect; F1 still open)**: `git grep mark.integration -- worker` still
  finds two markers: `worker/tests/ingest/test_normalizer.py:245` (real ffmpeg) and
  `worker/tests/separation/test_demucs_integration.py:27` (real Demucs, the intended one). The CTO
  accepted F1 in STATUS.md; the rule text in the constitution/TEST-STRATEGY still says "exactly
  one". Either register a separate `ffmpeg` marker or amend the rule text so the evidence and the
  rule agree.
- **R2 (devops/backend, measurement drift)**: devops-polish reports Persistence branch 83.33 %
  (`UnitOfWork.cs:31`, gate red), the per-project command gives 100 % on the same tree. The two
  scripts measure differently (`scripts/test-all.ps1` vs `-p:CollectCoverage=true` per project).
  Request: devops states which number the CI gate uses and makes both scripts identical, so a red
  gate cannot hide behind a green local run (or the reverse).
- **R3 (all four, test-first)**: a separate "add failing ... tests" commit exists for every test
  task, and the implementation commits (the next task id, e.g. T069 -> T070) come after it in
  `git log --reverse`; spot-checked T112 `3476275` (red) -> `9d267bb` (green). Test tasks: backend T031, T033, T069, T071, T073, T091, T102, T111; frontend T040/T042,
  T076-T080, T093, T095, T100, T104/T106, T112; audio-engine T082, T084, T086, T088, T090, T098;
  stem-separation T056, T058, T061, T063, T065, T067. audio-engine-foundational's mocks were
  written with their self-tests (no red commit) - test infrastructure only, accepted.
- **R4 (fakes and clock)**: no `time.sleep`, `time.time()` or `datetime.now` in
  `worker/tests/{separation,jobs,api}`; the API job sync and services use `FakeTimeProvider`
  (`CompositionTests`, `ImportServiceTests`, `MixServiceTests`); Demucs is faked in all but T060.
- **R5 (G6)**: no owner test reads `tests/fixtures/` (only a docstring in
  `worker/tests/separation/conftest.py` mentions the path, stating it is *not* read).
- **R6 (UI test ids)**: bUnit asserts the contract ids on every page that `e2e/pages/` uses
  (Import 12, TrackDetail 32, TrackList 10, MixLibrary 24 occurrences) and the full E2E suite
  passes against those ids on Chromium and Firefox (T122).
- **R7 (frontend, informational)**: no prerendering, so markup-only checks (legal notice without
  JS) cannot pass and `InputFile` needs a JS init before it accepts files (the T122 race, fixed in
  the page object). Not a defect against the spec; noted for future no-JS/accessibility audits.
- **R8 (stem-separation, informational)**: with the weights cached in the `model-cache` volume the
  worker still contacts the Hugging Face Hub on every process start (unauthenticated-request
  warning in the log; blob timestamp proves no re-download). An offline start therefore depends on
  huggingface_hub's offline fallback; consider `HF_HUB_OFFLINE=1` once the weights are present.
