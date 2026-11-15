# Quickstart and validation guide: Detail Separation

Builds on [001 quickstart](../001-stemstudio-mvp/quickstart.md) (prerequisites, build, run).
Contracts: [contracts/](contracts/). Entities and states: [data-model.md](data-model.md).
Thresholds and measurements: [research.md](research.md).

## What changes for running StemStudio

- First real detail phase downloads about 1.9 GB of model weights (MDX23C DrumSep 438 MB, UVR
  KARA 2 53 MB, MVSep Mega 53-stem 1.37 GB) into the `model-cache` volume
  (`STEMSTUDIO_MODEL_DIR=/models`); later runs reuse them. Weights are never in the repository
  or the image (constitution IV, model weights exception).
- Memory: the worker needs about 16 GB RAM for a 4-minute song during the instrument step
  (Mega 53-stem); Docker Desktop must allow at least 20 GB (`docker info` -> Total Memory). The
  reference machine has 62 GB.
- `STEMSTUDIO_SEPARATOR=fake` fakes the detail phase too (deterministic, no downloads;
  `STEMSTUDIO_FAKE_DETAIL_STEP_SECONDS` default 1 s per parent step so E2E can observe progress
  and cancel).

```bash
# Worker: unit tests use fakes; one marked integration test runs the real detail models on a
# 10 s fixture (downloads weights on first run)
cd worker && uv run pytest --cov && uv run pytest -m integration
# Synthetic gating fixtures (qa)
uv run --project worker python tests/fixtures/generate_detail_fixtures.py
# Contracts
npx @redocly/cli@2.78.0 lint --config specs/002-detail-separation/contracts/redocly.yaml \
  specs/002-detail-separation/contracts/worker-api.openapi.yaml \
  specs/002-detail-separation/contracts/backend-api.openapi.yaml
```

## Validation scenarios

Unless noted, run with `docker compose up` (real models) for D1-D6 and D12, and with
`STEMSTUDIO_SEPARATOR=fake` for the automated E2E variants (Playwright tag `@detail`).

| # | Story / req | Steps | Expected |
|---|---|---|---|
| D1 | US1, FR-001, SC-001 | Import a 4-minute band song (e.g. the reference song "Hero of the Day", 4:22). Open the track page when the standard stems are ready. | Mixer usable immediately; detail section shows `queued` then `running` with "Splitting <parent> (i of n)", progress updates at least every 10 s, the long-duration notice is visible. Phase completes within the SC-001 target (research.md D-03: <= 30 min on the reference machine). |
| D2 | US1 AS2, FR-003, SC-003 | Import "Monkeys Spinning Monkeys" (no vocals, no drums). | `vocals` and `drums` show "Skipped: (almost) silent"; `bass` "Not split"; `guitar`/`other` are split or "nothing further". |
| D3 | US1 AS3-AS5, FR-004..006, SC-002 | For every split parent of D1/D2: download the detail stems and run `uv run python worker/scripts/check_reconstruction.py <trackDir>` (stem-separation task). | Every offered detail stem is above the audibility gate; `sum(detail stems) - parent` <= -30 dB relative to the parent for 100 % of split parents; a parent with fewer than 2 kept sub-stems is not split. |
| D4 | US2, FR-012..014, SC-004 | Open the D1 track in the mixer, play, expand drums, mute `snare`, solo `kick`, collapse, expand again; loop A-B 8 s and let it repeat 50 times. | Expanding at default settings sounds identical; only the snare meter falls silent when muted; with solo only kick plays (other stems silent); collapsing restores the parent with its own settings and the detail settings return on the next expansion; meters stay aligned (E2E: sample offset <= 1 ms after 50 loops). |
| D5 | US3 AS1-AS2, FR-007, FR-008 | Disconnect the network before the first real detail phase (empty model cache), import a song. | Detail phase `failed` "Detail model could not be downloaded; try again when online."; standard stems play; Retry after reconnecting succeeds. |
| D6 | US3 AS3, FR-019 | While a detail phase runs, `docker compose restart worker`. | Detail status `failed` "interrupted by restart" with Retry; standard stems intact; Retry resumes from finished parent checkpoints (stage skips them). |
| D7 | US3 AS4, FR-011, SC-006 | Queue detail phases for 3 tracks (Split further), then import a new song. | The new import's standard separation starts within 5 s after the current parent step finishes (worker log + track page). E2E variant with fake separator. |
| D8 | US4, FR-009 | Open a track imported before feature 002. | `detail-split-further` visible; clicking it queues the phase; the mixer offers expand buttons afterwards. |
| D9 | US4 AS2, FR-010, SC-007 | Delete the D1 track, re-import the same URL; or Split further on a second track with the same audio. | Detail phase completes within 5 s from the worker cache. |
| D10 | US5, FR-015, SC-005 | Expand drums, mute snare, save "No snare", reload, load it; then load a mix saved before 002. | Drums expanded, snare muted; the old mix loads with everything collapsed and identical settings. Duplicate/rename keep detail settings. |
| D11 | FR-016 | Try to delete a track while its detail phase is queued or running; then Cancel and delete. | Delete refused (409, reason shown at `track-delete-blocked`); after Cancel the delete succeeds and `detail/` is removed. |
| D12 | FR-017, FR-018, constitution IV | Read the track page help, README and `docs/THIRD-PARTY-LICENSES.md`. | Limitations text (quality varies, not all instruments, takes long); every detail model listed with licence status, URL and SHA-256; "not for commercial use or redistribution" notice. |
| D13 | Edge: 4-stem fallback | Force `htdemucs` (worker setting) and import. | Detail phase works on vocals, drums, other only. |
| D14 | Edge: long song | Import a 12-minute song. | Long-duration notice shown; phase completes (no timeout); memory within limits (research.md D-06). |
