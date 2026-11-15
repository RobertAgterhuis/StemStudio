# StemStudio test strategy

Owner: qa. Binding inputs: constitution I (test-first, >= 90 % line AND branch per component),
ADR-006 (tooling), ADR-008 (checkpoint hand-offs, SC-004 method, browsers), ADR-010 (detail
separation), `specs/001-stemstudio-mvp/tasks.md` and `specs/002-detail-separation/tasks.md`.

Feature 002 (detail separation) extends every section; its additions are marked **(002)**. The
binding contracts are now those in `specs/002-detail-separation/contracts/` (worker 1.1.0,
backend 1.2.0, engine 2.0.0, UI test ids 1.1.0).

## 1. Test pyramid

```text
                 E2E (Playwright, Chromium + Firefox, retries 0)     e2e/tests/*.spec.ts
             ----------------------------------------------------
            Contract tests (OpenAPI shapes, URL vectors, UI test ids)
        ------------------------------------------------------------
       Component / integration tests (WebApplicationFactory, bUnit,
       FastAPI TestClient, SQLite in-memory, one real-model test per
       model family: Demucs, detail models (002))
   --------------------------------------------------------------------
  Unit tests (xUnit v3, pytest, Vitest) with fakes for every port: the bulk
```

- **Unit** tests are the bulk: fast, deterministic, no network, no subprocess, no model, no real
  clock. Every heavy tool sits behind a port (`worker/src/stemstudio_worker/ports.py`, .NET
  interfaces, `EngineOptions` in the engine) and is replaced by a fake.
- **Component** tests exercise one deployable through its public surface (HTTP via
  `WebApplicationFactory` / `TestClient`, Razor components via bUnit) with the neighbours faked.
- **Contract** tests pin the committed contracts: backend and worker OpenAPI shapes (YamlDotNet +
  System.Text.Json, `jsonschema` + `pyyaml`), `source-url-vectors.json` (API and worker accept and
  reject the same forms), and the UI test-id contract (bUnit asserts the ids, Playwright uses them).
- **E2E** tests cover the constitution's main flow and the acceptance scenarios that only make
  sense in a real browser against the real stack. They are few and slow by design.

## 2. Per component: tools and gates

| Component | Owner(s) | Test tools | Coverage gate (line AND branch) | Lint gate |
|---|---|---|---|---|
| Domain, Persistence, Api, Contracts (.NET) | backend | xUnit v3, Shouldly, NSubstitute, WebApplicationFactory, FakeTimeProvider, SQLite in-memory | coverlet.msbuild `-p:CollectCoverage=true`, 90 % minimum per test project (`Directory.Build.props`); Contracts via Api.Tests | analyzers `latest-recommended` + `TreatWarningsAsErrors`, `dotnet format --verify-no-changes` |
| Web (Blazor + Radzen) | frontend | xUnit v3, bUnit (JSInterop mock), stub `HttpMessageHandler`, fake `IStemStudioApiClient` | same, `StemStudio.Web.Tests` | same |
| Worker (ingest, analysis, separation, jobs, api) | audio-ingest, beat-analysis, stem-separation | pytest, httpx `TestClient`, in-test fakes, `tmp_path` | pytest-cov `--cov-branch`, `fail_under = 90` per package | ruff (incl. C90/PLR limits), ruff format, mypy --strict |
| Audio engine (TS) | audio-engine | Vitest + jsdom, hand-written `AudioContext` mock, fake timers | v8 thresholds 90/90/90/90 | ESLint strict type-checked + jsdoc, `tsc --noEmit` |
| Fixture generators (`generate_fixtures.py`; `generate_detail_fixtures.py` (002)) | qa | pytest | pytest-cov `--cov-branch`, `fail_under = 90` over both modules (`tests/fixtures/pyproject.toml`) | ruff, ruff format, mypy --strict |
| E2E suite | qa | Playwright Test (TypeScript) | n/a (behavioural) | ESLint strict type-checked + eslint-plugin-playwright, `tsc --noEmit` |
| SIG metrics | quality-gate | `tools/quality/run_all.py` (lizard, jscpd, dependency direction) | own tests >= 90 % | unit <= 15 LOC (max 30), CC <= 5 (max 10), <= 4 params, no 6-line duplicates |

CI (devops, `.github/workflows/ci.yml`) runs exactly these commands and fails below the gates.

## 3. Fakes per port

| Port / boundary | Real implementation | Fake used in unit/component tests | Where the real one is exercised |
|---|---|---|---|
| `UrlResolver`, `AudioFetcher` (yt-dlp, spotDL) | audio-ingest | fake `yt_dlp` factory, fake spotDL metadata client + YouTube matcher | manual V3 only (network) |
| `AudioNormalizer` (ffmpeg/ffprobe) | audio-ingest | fake `CommandRunner` | E2E (compose) |
| `UploadIntake` | audio-ingest | real code on files in `tmp_path` | E2E upload specs |
| `BeatAnalyzer` (librosa) | beat-analysis | none needed: real librosa on synthetic signals generated in the test | E2E (`track-bpm` ~100) |
| `Separator` (Demucs) | stem-separation | fake Demucs loader/applier; `FakeSeparator` (`STEMSTUDIO_SEPARATOR=fake`) | the single `@pytest.mark.integration` test; `@real` E2E |
| Worker HTTP API | stem-separation | stub `HttpMessageHandler` returning the OpenAPI examples (backend) | E2E (compose) |
| Backend HTTP API | backend | fake `IStemStudioApiClient` / stub handler (frontend) | E2E (compose) |
| Audio engine interop | audio-engine | bUnit `JSInterop` (frontend) | E2E (real browser) |
| Web Audio, `fetch`, timers, rAF | browser | `AudioContext` mock, stubbed `fetch`, fake timers (engine) | E2E (real browser) |
| Clock | system | `FakeTimeProvider` (.NET), fake clock (worker), Vitest fake timers | n/a |
| **(002)** `DetailModel` / `DetailModelProvider` (MDX23C DrumSep, UVR KARA 2, MVSep Mega 53) | stem-separation (`detail/runtimes.py`, vendored MSST) | fake models returning scaled copies of the parent (`detail/fake.py`, T042); fake torch/onnx callables for the runtime wrappers (T048) | the detail `@pytest.mark.integration` test (T050); quickstart D1-D6 (T092) |
| **(002)** Model download (`detail/download.py`) | stem-separation | fake opener (T018): first use, SHA-256 mismatch, offline | first real detail phase (quickstart D5) |
| **(002)** Gating, folding, re-assignment, checks | stem-separation | none needed: pure numpy on synthetic signals generated in the tests (T012-T017) | integration test; `detail-manifest.json` fixtures (qa) |
| **(002)** Priority scheduler, detail runner | stem-separation | fake step functions, fake clock and sleeper (T022, T044, T065) | E2E `@detail` "progress and cancel", `@worker-restart` |
| **(002)** Worker detail API (`/v1/detail-jobs`) | stem-separation | stub `HttpMessageHandler` with the worker 1.1.0 examples (`Worker/Examples/detail-*.json`, backend T030) | E2E `@detail` (compose) |
| **(002)** Backend detail API (`startDetail`, `cancelDetail`, `getDetailStem`, `Track.detail`, mix `expanded`/`detailChannels`) | backend | fake `IStemStudioApiClient` (frontend T034-T080) | E2E `@detail` |
| **(002)** Engine groups (`loadTrack.groups`, `setExpanded`, `<parent>.<detail>` channels) | audio-engine | `AudioContext` mock and stubbed `fetch` (T058-T060); bUnit `JSInterop` for the Web (T062) | E2E `@detail` "mixer" (real browser) |

## 4. Fake versus real separator

- **Fake** (`STEMSTUDIO_SEPARATOR=fake`, model `fake`, 6 stems): the default for the E2E suite and
  CI. It produces six FLAC stems of the input's duration in well under a second, so the full
  pipeline (download/upload intake, ffmpeg normalisation, real librosa analysis, persistence, stem
  serving, browser decoding) is exercised quickly and deterministically.
- **Real Demucs** runs in exactly two places:
  1. the single worker test marked `@pytest.mark.integration` (`uv run pytest -m integration`),
     deselected by default, on a short synthetic clip;
  2. the E2E variant tagged `@real` (`e2e/tests/main-flow.real.spec.ts`, `npm run test:real`)
     against `docker compose` with `STEMSTUDIO_SEPARATOR=demucs`; it accepts model `htdemucs_6s`
     (6 stems) or the fallback `htdemucs` (4 stems) and has a long timeout (15 min).
- **(002) Real detail models** run in exactly one more marked test,
  `worker/tests/detail/test_detail_integration.py` (`@pytest.mark.integration`, T050): MDX23C
  DrumSep, UVR KARA 2 and MVSep Mega 53 on a generated 10 s band (the qa fixture
  `detail_10s_band.wav` or an equivalent generated in the test); every split parent must
  reconstruct <= -30 dB and the gates must hold (SC-002, SC-003). It downloads about 1.9 GB of
  weights on first use and needs >= 20 GB of memory for Docker. The fake separator
  (`STEMSTUDIO_SEPARATOR=fake`) also selects the fake detail models, so CI and the `@detail` E2E
  specs never download weights. The constitution's "exactly one real-model test" is read as
  "one per model family" (plan.md Complexity Tracking, ADR-010).
- **Run policy for real Demucs**: not on every push. It runs in the integration wave (T123), in
  the manual/scheduled workflow `.github/workflows/e2e-real.yml` (devops) and before a release.
  The run records processing time of the 20 s fixture and a 4-minute song, the model used and
  model-cache reuse on a second start. **(002)** The same workflow runs the detail integration
  test with the cached model volume and a 60 min timeout (devops T008); the CTO measures SC-001
  (detail phase of a 4-minute song <= 30 min) on the reference machine (T092).

## 5. E2E scope (Playwright)

Runs against the real stack (`STEMSTUDIO_SEPARATOR=fake docker compose up -d --build --wait`), web
on `E2E_BASE_URL` (default `http://localhost:8080`), API on `E2E_API_URL` (default
`http://localhost:8081`). Selectors use `page.getByTestId(...)` with the ids of
`specs/002-detail-separation/contracts/ui-test-ids.md` (1.1.0, a superset of 1.0.0) only, wrapped in page objects (`e2e/pages/`).

| Spec | Covers |
|---|---|
| `e2e/tests/health.spec.ts` | `GET /health` of web and API (harness smoke test) |
| `e2e/tests/import.spec.ts` | notice; upload -> completed with 6 stems, model `fake`, BPM ~100; re-upload cached < 2 s (SC-006); rejects `.ogg`, `vimeo.com`, playlist URL with a reason and no new track (SC-007); `corrupt.mp3` -> failed + reason + `track-retry`; streamed upload of `upload_4mb.wav` (G1); `/tracks` -> track -> `open-mixer` |
| `e2e/tests/mixer.spec.ts` | strips per stem with defaults 80 % / pan 0; play after a user gesture; master meter rises above -60 dB; mute/solo change meters; loop 10 s -> 20 s accepted, B <= A refused; SC-004 timing (< 50 ms) for fader, mute, solo |
| `e2e/tests/main-flow.spec.ts` | constitution main flow: import -> completed -> mixer -> change fader, pan, mute, solo, metronome BPM + offset, loop -> save "Practice" -> reload -> load -> every control restored; save-as "Practice 2", duplicate, delete (SC-001, SC-005) |
| `e2e/tests/main-flow.real.spec.ts` (`@real`) | the main flow with real Demucs (`htdemucs_6s` or `htdemucs`) |
| `e2e/tests/detail.spec.ts` "auto" (`@detail`, 002) | import `detail_10s_band.wav` -> standard completes -> the detail phase is queued/running with stage, progress and the long-duration notice -> the mixer plays the standard stems during the phase -> completed with the outcome of every parent equal to the fake table (drums/vocals/other split, guitar nothing-further, piano not-reliable, bass not-supported); limitation help (FR-017) |
| `detail.spec.ts` "mixer" (`@detail`, 002) | `expand-<parent>` only for split parents; expanding drums shows the grouped strips (defaults 80 % / 0) and the parent meter falls silent; muting the snare silences only `meter-drums-snare`; solo kick silences every other channel; detail controls reach the engine within 50 ms (SC-004); collapsing brings the parent back and the next expansion keeps the detail settings; 50 loop repetitions with a group expanded mid-playback keep every running source on one transport anchor within 1 ms (SC-004) |
| `detail.spec.ts` "progress and cancel" (`@detail`, 002) | delete refused while the phase is active (`track-delete-blocked`), cancel -> `cancelled` + Retry, then delete succeeds (FR-016); a new import's standard separation completes while another track's detail phase is still running (FR-011, SC-006) |
| `detail.spec.ts` "split further" (`@detail`, 002) | cancel the auto phase -> start it again from the track page -> completed -> the mixer offers expand (FR-009); delete and re-import a detail-separated source -> its detail phase completes from the worker cache within 5 s (FR-010, SC-007) |
| `detail.spec.ts` "mixes" (`@detail`, 002) | expand drums + mute snare -> save "No snare" -> reload -> load -> drums expanded, snare muted, other parents collapsed; duplicate keeps the detail settings; a mix created through the API in the 1.1.0 shape (no `expanded`/`detailChannels`) loads collapsed with its 001 settings (FR-015, SC-005) |
| `detail.spec.ts` "worker restart" (`@detail @worker-restart`, 002) | `docker compose restart worker` while a detail phase runs -> `failed` "interrupted by restart" with Retry -> the standard stems still play -> Retry completes (FR-019, US3 AS3) |

**(002) Stack and data for `@detail`.** The stack runs with `STEMSTUDIO_SEPARATOR=fake` and
`STEMSTUDIO_FAKE_DETAIL_STEP_SECONDS=5`, so a detail phase takes about 25 s and the specs can
observe `queued`/`running`, cancel it, import another track meanwhile and restart the worker
mid-phase. The suite reads the same value from `E2E_FAKE_DETAIL_STEP_SECONDS` (default 5) to size
its timeouts. The worker caches detail results per audio SHA-256 (FR-010), so specs that need a
*live* phase upload a per-run copy of `detail_10s_band.wav` with a few least-significant bits
changed (`e2e/support/unique-audio.ts`; inaudible, new hash, same fake outcomes); specs that only
need a completed detail phase reuse the plain fixture and are fast on reruns.

**(002) Worker restart.** The 001 rule "the suite never restarts containers" stays for `npm test`.
The single exception is the opt-in `@worker-restart` spec, which runs
`docker compose -f $E2E_COMPOSE_FILE restart worker` through `child_process.execFileSync`
(argument list, no shell) because the restart has to land inside a window of about 25 s; it is
excluded from `npm test` and run with `npm run test:worker-restart` on a host with Docker (T091).

**(002) Sample alignment (SC-004) in the browser.** `e2e/support/alignment-probe.ts` wraps
`AudioBufferSourceNode.prototype.start/stop` in an init script and records, per source, the
effective start time, the offset and the node (for `loop`, `loopStart`, `loopEnd`). For all
running stem sources (buffers >= 1 s, so metronome clicks are ignored) it computes the transport
anchor `offset - start` and the largest circular difference modulo the loop length. The check runs
after 50 repetitions of a 1 s loop with drums expanded mid-playback. It proves the scheduling
invariant of contract 2.0.0 (one shared `when`/`offset`) in a real browser; timing inside the
browser's native looping is the browser's own and is covered by the engine's Vitest suite (T060).

Persistence across an application restart (G2, SC-005): the integration wave (T122) saves a mix,
runs `docker compose restart api web`, and then runs the `@restart`-tagged check in
`main-flow.spec.ts`, which reloads and asserts every setting. The suite never restarts containers
itself (no Docker access from the browser tests), so the restart step is part of the run script.

**SC-004 method (ADR-008 #5).** An init script wraps `AudioParam` automation methods
(`setValueAtTime`, `setTargetAtTime`, `linearRampToValueAtTime`, `value` setter) and records
`performance.now()` per call in `window.__stemStudioE2E`. The test records `performance.now()`
immediately before the user action (dispatched in the page, so Playwright IPC latency is not
counted) and asserts that the first engine automation call after it happens within 50 ms. This
measures "user action -> audio engine applies the change", which is stricter than "engine method
called", and does not depend on the engine's private object graph.

**Not automated (manual, network-dependent)**: YouTube and Spotify imports (quickstart V3) need
live third-party services; they are a manual check in the integration wave (T124). Also manual:
worker-down 503 (V5), restart during separation (V6), audible metronome alignment (V9), SC-002
stem alignment after 50 loop repetitions (unit evidence from the engine plus a browser check).

## 6. Rules

- **retries = 0** everywhere (Playwright config, CI). A flaky test is a defect, never retried away.
- **Determinism**: fixtures are generated deterministically (fixed seeds, bit-exact encoders);
  E2E waits on observable state (`expect.poll`, `toHaveAttribute`), never on fixed sleeps.
  Interactive Server renders before its JS interop is wired: Blazor's `InputFile` attaches its
  `change` listener in a JS `init` call after the first render, so `ImportPage.uploadFile` waits
  for the `_blazorInputFileNextFileId` marker before `setInputFiles` (T122 found the race on
  Firefox: 1 of 36 failed once; 180/180 after the fix).
- **Idempotent suite**: uploads are cached by SHA-256, so a second run gets `200` instead of `202`
  for the same fixture. Specs accept either and assert the end state, not the first response.
  Mix names include a per-run suffix so reruns against the same data volume do not collide.
- **Test data ownership**: every owner keeps its own fixtures and fakes in its own test folder
  (e.g. `worker/tests/analysis/`, `tests/StemStudio.Api.Tests/Worker/Examples/`). `tests/fixtures/`
  is only for E2E and the generator; unit tests of other owners never read it (G6, O5).
- **Exactly one** `@pytest.mark.integration` test per real model family exists in the worker:
  the Demucs one (audited in 001 T110) and the detail one (002 T050, audited in 002 T086).

## 7. Fixture catalogue

Generated by `uv run --project worker python tests/fixtures/generate_fixtures.py` into
`tests/fixtures/audio/` (git-ignored); `tests/fixtures/manifest.json` (committed) records the
facts tests rely on. All audio is 44.1 kHz stereo 16-bit unless stated.

| File | Content | Known facts (manifest) | Used by |
|---|---|---|---|
| `click_120bpm_4-4_12s.wav` | clicks, accent (1.5 kHz) on beat 1, normal (1 kHz) otherwise, first beat at 0.5 s | 120 BPM, 4/4, beat times exact to the sample | metronome/analysis checks, manual V9 |
| `click_90bpm_3-4_12s.wav` | same, 3/4 | 90 BPM, 3/4 | analysis checks |
| `music_100bpm_20s.mp3` | bass + chord tones + kick/hat pattern + clicks | 100 BPM, 4/4, ~20 s | import, mixer, main flow |
| `music_100bpm_20s.flac` | same audio, FLAC | 100 BPM, 4/4, 20 s | FLAC upload path |
| `upload_4mb.wav` | music at 110 BPM, ~24 s, ~4.2 MB | 110 BPM, 4/4 | streamed upload (G1) |
| `silence_5s.wav` | digital silence | no beat grid | unreliable-grid path |
| `corrupt.mp3` | ID3 header followed by deterministic garbage | not decodable | failed job + retry |
| `unsupported.ogg` | Ogg signature with an invalid payload | rejected by extension | 415 reject |

An oversized file (200 MiB + 1 byte) is not generated: the 413 path is covered by backend
component tests and the manual V4 check.

**(002) Detail fixtures.** Generated by
`uv run --project worker python tests/fixtures/generate_detail_fixtures.py` into the same
git-ignored `tests/fixtures/audio/`; `tests/fixtures/detail-manifest.json` (committed) records,
per file, the level (dBFS, mean power of the mono mix-down) and the share of 2048/512 frames above
-40 dBFS, and per parent the energy (dB rel. parent) and active share (above
max(-50 dBFS, parent level - 25 dB)) of each component and the outcome the research.md D-04 gates
must give. All files are 10 s, 44.1 kHz stereo PCM16; every `*_sum.wav` is the exact 16-bit
sample sum of its component files (reconstruction exact, SC-002). The manifest also records the
D-04 constants it was derived with (`gating`).

| File | Content | Expected outcome (D-04) | Used by |
|---|---|---|---|
| `detail_silent_parent.wav` | Gaussian noise at -80 dBFS, 0 % active frames | `skipped-silent` (FR-003) | gate checks, SC-003 |
| `detail_bleed_parent.wav` | -80 dBFS noise + three 0.1 s bursts: 4.1 % active frames (< 8 %) | `skipped-silent` (bleed, FR-003) | gate checks, SC-003 |
| `detail_drums_{kick,snare,hihat}.wav`, `detail_drums_sum.wav` | 120 BPM kick (beats 1, 3), snare (2, 4), hihat (eighths) and their sum | `split` -> kick, snare, hihat, rest | gate and reconstruction checks, SC-002 |
| `detail_vocals_{lead,backing}.wav`, `detail_vocals_sum.wav` | lead melody with vibrato (centre), backing root + fifth every other bar (panned left), sum | `split` -> lead, backing, rest | lead/backing checks, SC-002 |
| `detail_instruments_{strings,brass}.wav`, `detail_instruments_sum.wav` (parent `other`) | sawtooth triads with a bowed attack, odd-harmonic brass stabs, sum | `split` -> strings, brass, rest | family checks, SC-002 |
| `detail_10s_band.wav` | all of the above plus a bass line, 120 BPM, peak 0.8 | real models: measured by T050; fake: the T042 table | detail integration test (T050), every `@detail` E2E spec |

Unit tests of other owners still generate their own synthetic signals (G6); these files are the
shared, documented inputs for the integration test, the E2E suite and manual threshold tuning.

## 8. Traceability: acceptance scenario -> tests

| Scenario | Unit / component / contract tests | E2E |
|---|---|---|
| US1-1 upload -> job + progress/stage | `ImportEndpointsTests.cs`, `JobSyncServiceTests.cs`, `test_upload_intake.py`, `test_runner.py`, `ImportPageTests.cs`, `TrackDetailPageTests.cs` | `import.spec.ts`, `main-flow.spec.ts` |
| US1-2 YouTube URL | `test_url_resolver.py`, `test_fetcher.py`, `test_normalizer.py`, `ImportEndpointsTests.cs`, `SourceKeyTests.cs` | manual V3 |
| US1-3 Spotify URL via YouTube match | `test_url_resolver.py`, `test_fetcher.py` | manual V3 |
| US1-4 six (or four) stems + BPM/beats/time signature | `test_fake_separator.py`, `test_demucs_separator.py`, integration test, `test_librosa_analyzer.py`, `test_reliability.py`, `TrackEndpointsTests.cs` | `import.spec.ts`, `main-flow.real.spec.ts` |
| US1-5 cache reuse | `test_cache.py`, `ImportEndpointsTests.cs`, `SourceKeyTests.cs` | `import.spec.ts` (SC-006) |
| US1-6 rejects | `source-url-vectors.json` tests (API + worker), `test_upload_intake.py`, `ImportEndpointsTests.cs`, `ImportPageTests.cs` | `import.spec.ts` (SC-007) |
| US1-7 legal notice | `ImportPageTests.cs` | `import.spec.ts` |
| US1-8 failure + retry | `test_runner.py`, `test_reconcile.py`, `TrackEndpointsTests.cs`, `TrackDetailPageTests.cs` | `import.spec.ts` (`corrupt.mp3`) |
| FR-018 track list -> mixer | `TrackListPageTests.cs` | `import.spec.ts` |
| US2-1 strips + defaults + meters | `MixerStateTests.cs`, `MixerPageTests.cs`, `mixer.test.ts`, `meters.test.ts` | `mixer.spec.ts` |
| US2-2 transport sample lock | `transport.test.ts`, `engine.test.ts` | `mixer.spec.ts` (play after gesture) |
| US2-3 solo/mute | `mixer.test.ts`, `MixerStateTests.cs` | `mixer.spec.ts` |
| US2-4 A-B loop | `loop.test.ts` | `mixer.spec.ts` |
| US2-5 invalid loop refused | `loop.test.ts`, `MixerStateTests.cs` (`LoopValidator`) | `mixer.spec.ts` |
| SC-004 < 50 ms | `mixer.test.ts` (ramp <= 10 ms) | `mixer.spec.ts` timing check |
| US3-1..4 metronome | `metronome.test.ts`, `MetronomePanelTests.cs` | `main-flow.spec.ts` (BPM + offset persisted); manual V9 |
| US4-1 save | `MixEndpointsTests.cs`, `MixTests.cs`, `SaveMixTests.cs` | `main-flow.spec.ts` |
| US4-2 load restores everything | `MixEndpointsTests.cs`, `MixLibraryPageTests.cs` | `main-flow.spec.ts` (+ restart, SC-005) |
| US4-3 edit / save-as | `MixEndpointsTests.cs`, `SaveMixTests.cs` | `main-flow.spec.ts` |
| US4-4 duplicate "(copy)" | `MixEndpointsTests.cs`, `MixTests.cs` | `main-flow.spec.ts` |
| US4-5 delete keeps track | `MixEndpointsTests.cs`, `MixLibraryPageTests.cs` | `main-flow.spec.ts` |
| US4-6 name taken | `MixEndpointsTests.cs`, `MixTests.cs`, `SaveMixTests.cs` | manual V10 |
| FR-011a unsaved indicator | `MixerStateTests.cs`, `MixerPageTests.cs` | `main-flow.spec.ts` (`unsaved-indicator`) |

### (002) Detail separation: acceptance scenario -> tests

| Scenario | Unit / component / contract tests | E2E |
|---|---|---|
| US1-1 standard stems playable during the phase (FR-001) | `DetailAutoQueueTests.cs`, `TrackDetailDetailTests.cs` | `detail.spec.ts` "auto" |
| US1-2 silent parent skipped (FR-003, SC-003) | `test_gating.py`, `test_steps.py` | manual D2 (the fake separator has no silent parent); fixtures `detail_silent_parent.wav`, `detail_bleed_parent.wav` |
| US1-3 inaudible sub-stem not offered (FR-004) | `test_gating.py`, `test_steps.py`, `test_fake.py` | `detail.spec.ts` "auto" (guitar nothing-further) |
| US1-4 sub-stems + rest = parent (FR-005, SC-002) | `test_checks.py`, `test_detail_integration.py` | manual D3 (`check_reconstruction.py`); fixtures `detail_*_sum.wav` |
| US1-5 implausible split not offered (FR-006) | `test_checks.py`, `test_fake.py` | `detail.spec.ts` "auto" (piano not-reliable) |
| US2-1 expand shows grouped strips, sound unchanged (FR-012) | `StemGroupTests.cs`, `MixerStateDetailTests.cs`, `groups.test.ts`, `gate.test.ts` | `detail.spec.ts` "mixer" |
| US2-2 detail mute/solo/pan/fader, same responsiveness (SC-004) | `groups.test.ts`, `StemGroupTests.cs` | `detail.spec.ts` "mixer" (50 ms probe) |
| US2-3 collapse keeps detail settings | `MixerStateDetailTests.cs`, `gate.test.ts` | `detail.spec.ts` "mixer" |
| US2-4 solo across levels (FR-013) | `gate.test.ts`, `MixerStateDetailTests.cs` | `detail.spec.ts` "mixer" (solo kick) |
| US2-5 sample-aligned play/seek/loop (FR-014, SC-004) | `groups.test.ts` (T058, T060), `transport.test.ts` | `detail.spec.ts` "mixer" (50 loops, alignment probe) |
| US3-1 stage, progress, long notice (FR-007) | `test_detail_runner.py`, `DetailAutoQueueTests.cs`, `TrackDetailDetailTests.cs` | `detail.spec.ts` "auto" |
| US3-2 failed -> Retry only the detail phase (FR-008) | `test_detail_runner.py`, `DetailEndpointTests.cs`, `DetailPhaseTests.cs` | `detail.spec.ts` "worker restart" (Retry), "progress and cancel" |
| US3-3 restart -> "interrupted by restart" (FR-019) | `test_detail_runner.py` (reconcile), `DetailAutoQueueTests.cs` (worker 404) | `detail.spec.ts` "worker restart" (`@worker-restart`) |
| US3-4 new import not delayed (FR-011, SC-006) | `test_scheduler.py`, T065 tests | `detail.spec.ts` "progress and cancel" (priority) |
| FR-016 delete refused while active; cancel | `DetailEndpointTests.cs`, `test_detail_service.py` | `detail.spec.ts` "progress and cancel" |
| US4-1 Split further (FR-009) | `DetailEndpointTests.cs`, T072/T074 tests | `detail.spec.ts` "split further" |
| US4-2 cached detail result (FR-010, SC-007) | `test_detail_service.py`, `test_detail_runner.py`, T072 tests | `detail.spec.ts` "split further" (re-import) |
| US5-1 save/load expansion + detail settings (FR-015, SC-005) | `MixDetailEndpointTests.cs`, `MixDetailTests.cs`, `DetailPersistenceTests.cs`, T079 tests | `detail.spec.ts` "mixes" |
| US5-2 old mixes load collapsed | `MixDetailEndpointTests.cs` (1.1.0 body), `DetailPersistenceTests.cs` (001 schema), T079 tests | `detail.spec.ts` "mixes" (API-created 1.1.0 mix) |
| US5-3 duplicate/edit copy detail settings | `MixDetailEndpointTests.cs`, `MixDetailTests.cs` | `detail.spec.ts` "mixes" (duplicate) |
| FR-017 limitations in the UI | `TrackDetailDetailTests.cs` | `detail.spec.ts` "auto" (`detail-limitation-help`) |
| FR-018 models downloaded on first use, never committed | `test_download.py`, `test_runtimes.py` | manual D5, D12 |

## 9. Commands

```bash
# Fixtures (generator + its tests, from the repository root)
uv run --project worker python tests/fixtures/generate_fixtures.py
uv run --project worker python tests/fixtures/generate_detail_fixtures.py   # (002)
uv run --project worker pytest -c tests/fixtures/pyproject.toml --cov --cov-branch \
  --cov-config=tests/fixtures/pyproject.toml tests/fixtures/tests
uv run --project worker ruff check tests/fixtures
uv run --project worker ruff format --check tests/fixtures
uv run --project worker mypy --config-file tests/fixtures/pyproject.toml tests/fixtures

# E2E (in e2e/)
npm ci && npx playwright install --with-deps chromium firefox
npm run lint && npm run typecheck
npx playwright test --list
npm run fixtures         # both generators
npm test                 # all but @real, @restart, @worker-restart; Chromium + Firefox, retries 0
npm run test:detail      # (002) only @detail, without @worker-restart
npm run test:worker-restart  # (002) needs Docker on the host (docker compose restart worker)
npm run test:restart     # after `docker compose restart api web` (T122)
npm run test:real        # real Demucs stack only
```
