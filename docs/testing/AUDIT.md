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
