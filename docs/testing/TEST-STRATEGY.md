# StemStudio test strategy

Owner: qa. Binding inputs: constitution I (test-first, >= 90 % line AND branch per component),
ADR-006 (tooling), ADR-008 (checkpoint hand-offs, SC-004 method, browsers), ADR-010 (detail
separation), ADR-011/ADR-012 (GPU runtime exception, device policy),
`specs/001-stemstudio-mvp/tasks.md`, `specs/002-detail-separation/tasks.md` and
`specs/003-gpu-acceleration/tasks.md`.

Feature 002 (detail separation) extends every section; its additions are marked **(002)**. The
binding contracts are now those in `specs/002-detail-separation/contracts/` (worker 1.1.0,
backend 1.2.0, engine 2.0.0, UI test ids 1.1.0).

Feature 003 (GPU acceleration) adds the processing device; its additions are marked **(003)**.
The binding contracts are now those in `specs/003-gpu-acceleration/contracts/` (worker 1.2.0,
backend 1.3.0, engine 2.0.0 unchanged, UI test ids 1.2.0, all additive).

Feature 004 (design system and UX foundation) changes only the web front end; its additions are
marked **(004)** and collected in section 10. Binding contracts: `specs/004-design-system-ux/contracts/`
(UI test ids **1.3.0** with level rules R7-R9, help-content 1.0.0, web-interop 1.0.0); backend
1.3.0, worker 1.2.0 and engine 2.0.0 are unchanged (`DELTA.md`). ADR-013 holds the design
decisions.

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
| **(004)** Web interop (TS, `src/web-interop`) | frontend | Vitest + jsdom, fake driver.js factory | v8 thresholds 90/90/90/90 | ESLint strict type-checked + jsdoc, `tsc --noEmit` |
| **(004)** Design quality (visual, a11y, Lighthouse) | qa (specs), devops (CI jobs) | Playwright `toHaveScreenshot` (container only), `@axe-core/playwright` 4.14.0, `@lhci/cli` 0.15.1 | n/a (budgets: 0.1 % pixels, 0 axe violations, SC-003 timings) | same as the E2E suite |
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
| **(002)** Priority scheduler, detail runner | stem-separation | fake step functions, fake clock and sleeper (T022, T044, T065) | E2E `@detail` `detail-progress.spec.ts`, `@worker-restart` |
| **(002)** Worker detail API (`/v1/detail-jobs`) | stem-separation | stub `HttpMessageHandler` with the worker 1.1.0 examples (`Worker/Examples/detail-*.json`, backend T030) | E2E `@detail` (compose) |
| **(002)** Backend detail API (`startDetail`, `cancelDetail`, `getDetailStem`, `Track.detail`, mix `expanded`/`detailChannels`) | backend | fake `IStemStudioApiClient` (frontend T034-T080) | E2E `@detail` |
| **(003)** Device probe (`devices/probe.py`: torch CUDA build, `is_available`, arch list, test kernel) | stem-separation | fake torch adapter (T011) raising or returning per probe step of data-model 2.1 | `scripts/gpu-smoke.*` and quickstart G3/G4 on the reference machine (CTO, T052) |
| **(003)** NVML (`devices/nvml.py`, `nvidia-ml-py`) | stem-separation | fake NVML reader with scripted total/free MiB (T011, T013) | quickstart G3, G9 (busy GPU) |
| **(003)** `DeviceRunner` (lease, free-memory check, memory fraction, OOM retry on the CPU) | stem-separation (`devices/{lease,oom,runner}.py`) | fake torch memory hooks and fake step functions (T013); consumers (`separation/`, `detail/`, `jobs/`) get a fake runner returning `DeviceUse`s or raising (T023-T027) | quickstart G5-G10 (CTO) |
| **(003)** `FakeDeviceRunner` (`STEMSTUDIO_FAKE_DEVICE` = `cpu`, `gpu`, `gpu-oom`; only with `STEMSTUDIO_SEPARATOR=fake`) | stem-separation (`devices/fake.py`) | is itself the fake; unit-tested in T015 | E2E `@gpu` (`gpu.spec.ts`) and the default-suite `device.spec.ts` |
| **(003)** `merge_device` fold | architect (`ports.py`, Phase 0) | none needed: pure function, `worker/tests/test_device_contract.py` | every job record |
| **(003)** Worker device fields and health (`Job.device`, `DetailJob.device`, `deviceInfo`) | stem-separation | stub `HttpMessageHandler` with the worker 1.2.0 examples (backend T021) | E2E (compose) |
| **(003)** `GET /api/device`, `Track.device`, `DetailPhase.device` | backend | fake `IStemStudioApiClient` (frontend T031, T042) | E2E `device.spec.ts`, `gpu.spec.ts` |
| **(002)** Engine groups (`loadTrack.groups`, `setExpanded`, `<parent>.<detail>` channels) | audio-engine | `AudioContext` mock and stubbed `fetch` (T058-T060); bUnit `JSInterop` for the Web (T062) | E2E `detail-mixer.spec.ts` (real browser) |

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

- **(003) GPU path.** No test in the repository needs a GPU and CI has none. Every torch, NVML
  and onnxruntime call sits behind a small injectable adapter and is unit-tested with fakes
  (table in section 3); no new real-model test is added. The real GPU path (torch `+cu132`,
  onnxruntime CUDA provider, speed SC-001, outcome equality SC-003, peak VRAM, busy GPU) is
  validated by the CTO on the reference machine (RTX 5070 Ti) in T052 with quickstart G3-G10, and
  the CPU image is guarded against NVIDIA runtime wheels by `scripts/check-cpu-image.*` (CI job
  `cpu-image-guard`, quickstart G2).

## 5. E2E scope (Playwright)

Runs against the real stack (`STEMSTUDIO_SEPARATOR=fake docker compose up -d --build --wait`), web
on `E2E_BASE_URL` (default `http://localhost:8080`), API on `E2E_API_URL` (default
`http://localhost:8081`). Selectors use `page.getByTestId(...)` with the ids of
`specs/004-design-system-ux/contracts/ui-test-ids.md` (1.3.0, a superset of 1.2.0, 1.1.0 and 1.0.0) only, wrapped in page objects (`e2e/pages/`). **(004)** Every browser context starts at level Advanced (section 10.1), where every 1.2.0 id renders exactly as before (R7).

| Spec | Covers |
|---|---|
| `e2e/tests/health.spec.ts` | `GET /health` of web and API (harness smoke test) |
| `e2e/tests/import.spec.ts` | notice; upload -> completed with 6 stems, model `fake`, BPM ~100; re-upload cached < 2 s (SC-006); rejects `.ogg`, `vimeo.com`, playlist URL with a reason and no new track (SC-007); `corrupt.mp3` -> failed + reason + `track-retry`; streamed upload of `upload_4mb.wav` (G1); `/tracks` -> track -> `open-mixer` |
| `e2e/tests/mixer.spec.ts` | strips per stem with defaults 80 % / pan 0; play after a user gesture; master meter rises above -60 dB; mute/solo change meters; loop 10 s -> 20 s accepted, B <= A refused; SC-004 timing (< 50 ms) for fader, mute, solo |
| `e2e/tests/main-flow.spec.ts` | constitution main flow: import -> completed -> mixer -> change fader, pan, mute, solo, metronome BPM + offset, loop -> save "Practice" -> reload -> load -> every control restored; save-as "Practice 2", duplicate, delete (SC-001, SC-005) |
| `e2e/tests/main-flow.real.spec.ts` (`@real`) | the main flow with real Demucs (`htdemucs_6s` or `htdemucs`) |
| `e2e/tests/detail.spec.ts` "auto" (`@detail`, 002) | import `detail_10s_band.wav` -> standard completes -> the detail phase is queued/running with stage, progress and the long-duration notice -> the mixer plays the standard stems during the phase -> completed with the outcome of every parent equal to the fake table (drums/vocals/other split, guitar nothing-further, piano not-reliable, bass not-supported); limitation help (FR-017) |
| `e2e/tests/detail-mixer.spec.ts` (`@detail`, 002) | `expand-<parent>` only for split parents; expanding drums shows the grouped strips (defaults 80 % / 0) and the parent meter falls silent; muting the snare silences only `meter-drums-snare`; solo kick silences every other channel; detail controls reach the engine within 50 ms (SC-004); collapsing brings the parent back and the next expansion keeps the detail settings; 50 loop repetitions with a group expanded mid-playback keep every running source on one transport anchor within 1 ms (SC-004) |
| `detail-progress.spec.ts` (`@detail`, 002) | delete refused while the phase is active (`track-delete-blocked`), cancel -> `cancelled` + Retry, then delete succeeds (FR-016); a new import's standard separation completes while another track's detail phase is still running (FR-011, SC-006) |
| `detail-split-further.spec.ts` (`@detail`, 002) | cancel the auto phase -> start it again from the track page -> completed -> the mixer offers expand (FR-009); delete and re-import a detail-separated source -> its detail phase completes from the worker cache within 5 s (FR-010, SC-007) |
| `detail-mixes.spec.ts` (`@detail`, 002) | expand drums + mute snare -> save "No snare" -> reload -> load -> drums expanded, snare muted, other parents collapsed; duplicate keeps the detail settings; a mix created through the API in the 1.1.0 shape (no `expanded`/`detailChannels`) loads collapsed with its 001 settings (FR-015, SC-005) |
| `detail-worker-restart.spec.ts` (`@detail @worker-restart`, 002) | `docker compose restart worker` while a detail phase runs -> `failed` "interrupted by restart" with Retry -> the standard stems still play -> Retry completes (FR-019, US3 AS3) |
| `device.spec.ts` (`@device`, 003, default suite) | default CPU stack: `GET /api/device` is reachable with `kind=cpu` and no reason, and the import page shows `worker-device` `data-device="cpu"` without `worker-device-reason`; a fresh import shows `track-device` and `detail-device` with `data-device="cpu"`, `data-fallback="false"` and no fallback notice (US2-1, US3-1, FR-006) |
| `gpu.spec.ts` (`@gpu`, 003, own run) | fake GPU stack: `GET /api/device` and `worker-device` report `gpu` "GPU (Fake GPU)" without a reason, in both fake GPU modes (US3-1); with `STEMSTUDIO_FAKE_DEVICE=gpu` `track-device` / `detail-device` `data-device="gpu"` "GPU (Fake GPU)" without a fallback (US1-1); with `gpu-oom` the job completes and `track-device-fallback` / `detail-device-fallback` contain "ran out of memory" (US2-3, SC-004) |

**(003) Device mode for `@gpu`.** The `@gpu` specs need a stack started with
`STEMSTUDIO_SEPARATOR=fake`, `STEMSTUDIO_FAKE_DETAIL_STEP_SECONDS=5` and
`STEMSTUDIO_FAKE_DEVICE=gpu` or `gpu-oom` (worker environment, passed through by
`docker-compose.yml`, devops T008). The suite reads the same mode from `E2E_FAKE_DEVICE` (falling
back to `STEMSTUDIO_FAKE_DEVICE`, default `cpu`; `e2e/support/env.ts`) so every test runs only in
the mode it asserts: tests tagged `@fake-gpu` need `gpu`, tests tagged `@fake-gpu-oom` need
`gpu-oom` and are skipped with the reason as annotation in the other mode; the import-page test
runs in both. In mode `cpu` every `@gpu` test fails fast with a hint instead of passing vacuously.
`npm test` excludes `@gpu` (`--grep-invert`), so the default suite on the CPU stack is unchanged
apart from the new `@device` spec. A complete device run is two stack starts, each followed by
`npm run test:gpu`: one with `gpu`, one with `gpu-oom`. Results from the worker cache carry no
device (data-model 2.2), so every device spec uploads a per-run copy of `detail_10s_band.wav`
(`uniqueBand`) instead of reusing a fixture the stack may have seen.


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

### (003) GPU acceleration: acceptance scenario -> tests

| Scenario | Unit / component / contract tests | E2E / manual |
|---|---|---|
| US1-1 GPU mode runs on the GPU and the track page shows it (FR-002, FR-006) | `worker/tests/devices/test_probe.py`, `test_runner.py`, `test_factory.py`, `test_fake.py`; `worker/tests/separation/test_demucs_device.py`; `worker/tests/detail/test_device_models.py`; `worker/tests/jobs/test_job_device.py`; `JobDeviceTests.cs`, `JobDevicePersistenceTests.cs`, `WorkerDeviceClientTests.cs`, `DeviceSyncTests.cs`, `DeviceContractTests.cs`; `DeviceBadgeTests.cs` + track-page tests | `gpu.spec.ts` "device on the track page" (`@gpu @fake-gpu`); quickstart G3, G7 (CTO) |
| US1-2 >= 5x faster than the CPU baseline (SC-001) | none (measurement) | quickstart G5 on the reference machine (CTO, T052) |
| US1-3 same outcomes per parent, reconstruction passes (FR-007, SC-003) | 002 gating/check tests unchanged; `test_device_models.py` (same steps, only the device differs) | quickstart G6 (CTO, T052; `check_reconstruction.py`) |
| US2-1 default start unchanged on the CPU (FR-001, SC-002) | every 001/002 test unchanged; `worker/tests/api/test_device_fallback.py` (`STEMSTUDIO_DEVICE=cpu`: no probe, `CPU` without fallback); `scripts/check-cpu-image.*` (FR-008) | the whole default suite; `device.spec.ts` (`@device`, `data-device="cpu"`); quickstart G1, G2 |
| US2-2 GPU option without a usable GPU -> CPU with a reason (FR-003) | `test_probe.py` (each reason text), `test_factory.py`, `test_device_fallback.py` (`/health` reason), `DeviceEndpointTests.cs` | quickstart G4 (CTO); `worker-device-reason` is not reachable with the fake device (the fake GPU is always usable) |
| US2-3 out of GPU memory -> step retried once on the CPU, notice shown (FR-004, SC-004) | `test_runner.py` (torch and onnxruntime OOM, one CPU retry, non-OOM propagates), `test_fake.py` (`gpu-oom`), `test_device_fallback.py`; backend fallback-reason tests (T036); `DeviceBadgeTests.cs` (fallback) | `gpu.spec.ts` "fallback notice" (`@gpu @fake-gpu-oom`); quickstart G8, G9 |
| US3-1 device shown before importing and per job (FR-006) | `DeviceEndpointTests.cs`, frontend import-page device tests (T042); `DeviceBadgeTests.cs` | `gpu.spec.ts` "worker device on the import page" (`@gpu`) and "device on the track page" (`@fake-gpu`); `device.spec.ts` "the import page shows the CPU worker", "the track page shows the CPU for both jobs" (`@device`); quickstart G7 |
| FR-005 one GPU step at a time | `test_runner.py` (lease, two threads) | quickstart G10 (CTO) |
| FR-009 GPU compose override builds and starts | none (build) | `scripts/gpu-smoke.*`, quickstart G3 (CTO) |

### (002) Detail separation: acceptance scenario -> tests

| Scenario | Unit / component / contract tests | E2E |
|---|---|---|
| US1-1 standard stems playable during the phase (FR-001) | `DetailAutoQueueTests.cs`, `TrackDetailDetailTests.cs` | `detail.spec.ts` "auto" |
| US1-2 silent parent skipped (FR-003, SC-003) | `test_gating.py`, `test_steps.py` | manual D2 (the fake separator has no silent parent); fixtures `detail_silent_parent.wav`, `detail_bleed_parent.wav` |
| US1-3 inaudible sub-stem not offered (FR-004) | `test_gating.py`, `test_steps.py`, `test_fake.py` | `detail.spec.ts` "auto" (guitar nothing-further) |
| US1-4 sub-stems + rest = parent (FR-005, SC-002) | `test_checks.py`, `test_detail_integration.py` | manual D3 (`check_reconstruction.py`); fixtures `detail_*_sum.wav` |
| US1-5 implausible split not offered (FR-006) | `test_checks.py`, `test_fake.py` | `detail.spec.ts` "auto" (piano not-reliable) |
| US2-1 expand shows grouped strips, sound unchanged (FR-012) | `StemGroupTests.cs`, `MixerStateDetailTests.cs`, `groups.test.ts`, `gate.test.ts` | `detail-mixer.spec.ts` |
| US2-2 detail mute/solo/pan/fader, same responsiveness (SC-004) | `groups.test.ts`, `StemGroupTests.cs` | `detail-mixer.spec.ts` (50 ms probe) |
| US2-3 collapse keeps detail settings | `MixerStateDetailTests.cs`, `gate.test.ts` | `detail-mixer.spec.ts` |
| US2-4 solo across levels (FR-013) | `gate.test.ts`, `MixerStateDetailTests.cs` | `detail-mixer.spec.ts` (solo kick) |
| US2-5 sample-aligned play/seek/loop (FR-014, SC-004) | `groups.test.ts` (T058, T060), `transport.test.ts` | `detail-mixer.spec.ts` (50 loops, alignment probe) |
| US3-1 stage, progress, long notice (FR-007) | `test_detail_runner.py`, `DetailAutoQueueTests.cs`, `TrackDetailDetailTests.cs` | `detail.spec.ts` "auto" |
| US3-2 failed -> Retry only the detail phase (FR-008) | `test_detail_runner.py`, `DetailEndpointTests.cs`, `DetailPhaseTests.cs` | `detail-worker-restart.spec.ts` (Retry), `detail-progress.spec.ts` |
| US3-3 restart -> "interrupted by restart" (FR-019) | `test_detail_runner.py` (reconcile), `DetailAutoQueueTests.cs` (worker 404) | `detail-worker-restart.spec.ts` (`@worker-restart`) |
| US3-4 new import not delayed (FR-011, SC-006) | `test_scheduler.py`, T065 tests | `detail-progress.spec.ts` (priority) |
| FR-016 delete refused while active; cancel | `DetailEndpointTests.cs`, `test_detail_service.py` | `detail-progress.spec.ts` |
| US4-1 Split further (FR-009) | `DetailEndpointTests.cs`, T072/T074 tests | `detail-split-further.spec.ts` |
| US4-2 cached detail result (FR-010, SC-007) | `test_detail_service.py`, `test_detail_runner.py`, T072 tests | `detail-split-further.spec.ts` (re-import) |
| US5-1 save/load expansion + detail settings (FR-015, SC-005) | `MixDetailEndpointTests.cs`, `MixDetailTests.cs`, `DetailPersistenceTests.cs`, T079 tests | `detail-mixes.spec.ts` |
| US5-2 old mixes load collapsed | `MixDetailEndpointTests.cs` (1.1.0 body), `DetailPersistenceTests.cs` (001 schema), T079 tests | `detail-mixes.spec.ts` (API-created 1.1.0 mix) |
| US5-3 duplicate/edit copy detail settings | `MixDetailEndpointTests.cs`, `MixDetailTests.cs` | `detail-mixes.spec.ts` (duplicate) |
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
npm test                 # all but @real, @restart, @worker-restart, @gpu; Chromium + Firefox, retries 0
npm run test:gpu         # (003) only @gpu; stack with STEMSTUDIO_FAKE_DEVICE=gpu, then again with gpu-oom

# (003) the two @gpu runs (from the repository root; the same variable drives stack and suite)
export STEMSTUDIO_SEPARATOR=fake STEMSTUDIO_FAKE_DETAIL_STEP_SECONDS=5
STEMSTUDIO_FAKE_DEVICE=gpu docker compose up -d --build --wait
(cd e2e && STEMSTUDIO_FAKE_DEVICE=gpu npm run test:gpu)
STEMSTUDIO_FAKE_DEVICE=gpu-oom docker compose up -d --wait    # recreates the worker
(cd e2e && STEMSTUDIO_FAKE_DEVICE=gpu-oom npm run test:gpu)
npm run test:detail      # (002) only @detail, without @worker-restart
npm run test:worker-restart  # (002) needs Docker on the host (docker compose restart worker)
npm run test:restart     # after `docker compose restart api web` (T122)
npm run test:real        # real Demucs stack only

# (004) design system and UX (section 10); fake CPU stack unless stated
npm run test:legacy      # only the 1.0.0-1.2.0 specs (SC-006 evidence: the same set as before 004)
npm run test:ux          # only the @ux specs (journeys, levels, help, tours, keyboard, theme flash)
npm run test:a11y        # axe matrix, project "a11y" (Chromium)
npm run test:lighthouse  # project lighthouse-setup writes the URLs, then lhci autorun (lighthouserc.json)
# Visual regression: ONLY inside the Playwright container and on a FRESH stack (empty data volume).
# CI job "visual" uses scripts/playwright-container.sh (devops); by hand, from the repository root on Linux:
docker compose down -v && STEMSTUDIO_SEPARATOR=fake STEMSTUDIO_FAKE_DETAIL_STEP_SECONDS=5 docker compose up -d --build --wait
docker run --rm --network host --ipc=host -e E2E_VISUAL_CONTAINER=1 \
  -v "$PWD:/work" -w /work/e2e mcr.microsoft.com/playwright:v1.65.1-noble \
  sh -c "npm ci && npm run test:visual"                          # compare against the baselines
# On Windows/macOS hosts (Docker Desktop with host networking) add -v /work/e2e/node_modules so the
# container's npm ci does not overwrite the host's node_modules.
# Baselines (integration wave T067, then reviewed in T068): same, with
#   sh -c "npm ci && npm run test:visual -- --update-snapshots"
# -> e2e/visual/__screenshots__/screens.spec.ts/*.png
```

## 10. (004) Design system and UX foundation

Scope: the web front end only (spec 004, ADR-013). Unit and component tests (bUnit; Vitest for
`src/web-interop`) belong to the frontend agent; the qa suites below exercise the result in real
browsers against the fake stack. They are authored during the wave (they lint, typecheck and
list) and executed by the CTO in the integration wave (T067) after the frontend merge.

### 10.1 Levels in E2E (storage state per level)

`e2e/support/preferences.ts` builds the local-storage record `stemstudio.preferences`
(data-model.md 1.1, schema version 1) as a Playwright storage state for the web origin:

| State | Record | Used by |
|---|---|---|
| `advancedState` (config default) | `level: "advanced"`, `help.welcomeDismissed: true`, every known tour recorded as `completed` at its current version (no tour offers) | every 1.0.0-1.2.0 spec, unchanged (ui-test-ids R7, SC-006); the specs themselves were not edited |
| `freshState` | nothing stored (effective level Beginner, welcome panel shown) | `journey-beginner.spec.ts`, welcome cases |
| `levelState(level, options)` | any level / theme / density / motion; welcome and tours configurable | the level, help, tour, keyboard, visual and a11y matrices |
| `expertState` | Expert, welcome dismissed, no offers | `expert-keyboard.spec.ts` |

`tourVersions` in the same file must follow the `version` of each `Help/tours/<id>.json`: a bumped
tour version re-offers the tour and would add an offer chip to the 1.2.0 screens. Specs that need
another state use `test.use({ storageState: ... })` per file or describe block, or a second context
or page for cross-tab checks. The record carries a fixed timestamp, so it is deterministic.

The Playwright config has five projects: `chromium` and `firefox` (testDir `tests/`, the E2E suite;
`npm test` and the other behavioural scripts select exactly these two), `visual` (testDir
`visual/`), `a11y` (testDir `a11y/`) and `lighthouse-setup` (`lighthouse/*.setup.ts`). The new
behavioural specs in `tests/` are tagged `@ux`, so `npm run test:legacy` still runs exactly the
1.2.0 set.

Shared data (`e2e/support/ux-data.ts`, worker-scoped fixture `uxData` in `e2e/support/ux-test.ts`):
`detail_10s_band.wav` with a completed detail phase (track with detail stems, mixer collapsed and
expanded), `click_120bpm_4-4_12s.wav` with its detail phase cancelled (track without detail stems;
tour step skipping) and the mix "UX practice" of the band track (find-or-create). The screens of
the matrices are defined once in `e2e/support/screens.ts`; new page objects: `app-shell.ts`,
`help-panel.ts`, `tour-popover.ts`, `command-palette.ts`.

### 10.2 Visual regression rules (SC-005)

- `e2e/visual/screens.spec.ts`, Playwright `toHaveScreenshot`, project `visual` (Chromium,
  1440 x 900, device scale 1, `reducedMotion: reduce`, stored `motion: "reduced"`).
- **Container only**: baselines are rendered and compared only in
  `mcr.microsoft.com/playwright:v1.65.1-noble` (same version as `@playwright/test`); the spec skips
  itself with the reason unless it runs on Linux inside that image (`/ms-playwright` exists, as
  with devops' `scripts/playwright-container.sh`) or `E2E_VISUAL_CONTAINER=1` is set. Windows or macOS renderings are never
  committed. Path: `e2e/visual/__screenshots__/screens.spec.ts/<name>.png` (no platform suffix).
- **Fresh stack**: the run starts on an empty data volume (`docker compose down -v`, then up) and
  runs `test:visual` alone, so the track list and mix library contain only the seeded data.
- **Threshold**: `maxDiffPixelRatio: 0.001` (0.1 % of pixels), animations disabled, caret hidden,
  CSS scale; full-page captures.
- **Masks**: meters and dB readouts (`meter-*`), `transport-position`, `transport-state`, `toast`,
  and every `<time>` element (mix dates); GUIDs never appear as text. The `<time>` mask is the only
  non-test-id selector of the suite (a mask is cosmetic, not an interaction); it relies on the
  front end rendering dates in `<time>` (requested in the qa hand-off).
- **Matrix**: 7 screen states (import, tracks, track with and without detail stems, mixer collapsed
  and expanded, mixes) x 2 themes x 3 levels = 42, plus per theme: welcome panel, help panel open
  (mixer, docked), a tour step (`import-basics` step 1), 360 px views of Import, Tracks, Track,
  Mixes and the help sheet, and the mixer at 768 px: 60 baselines (>= 30 required).
- **Baseline review**: the CTO generates the first set in the integration wave (T067) with
  `--update-snapshots` in the container, and the owner approves it (T068). Afterwards a baseline
  changes only through the CI job's `update-baselines` dispatch; the new images are reviewed in
  the pull request like code. A failing comparison is never "fixed" by regenerating without review.
- `e2e/tests/theme-flash.spec.ts` (SC-004, `@ux`) runs in the normal suite: 20 reloads per theme
  (dark, light, and system with the OS dark and with the OS light); a 1 x 1 px screenshot taken at
  `domcontentloaded` must be closer to that theme's page background than to the other theme's
  (both read from the `body` background with `html[data-theme]` set), and `html[data-theme]` must
  already be set at that moment.

### 10.3 Accessibility scan matrix (SC-001)

`e2e/a11y/screens.a11y.spec.ts`, project `a11y` (Chromium, 1440 x 900), `@axe-core/playwright`
with tags `wcag2a`, `wcag2aa`, `wcag21a`, `wcag21aa`, `wcag22aa`; **0 violations**, no rule disabled
and no element excluded. Matrix: the 7 screen states x 3 levels x 2 themes (42 scans), plus per
level and theme the help panel open (mixer), the command palette open (import) and a tour step on
the mixer (`mixer-basics` at Beginner, `mixer-advanced` at Advanced, `expert-keyboard` at Expert),
plus the welcome panel (fresh storage, both themes): 62 scans. A failure lists rule id, impact,
help URL and target selectors. Keyboard-only walkthroughs are covered by `journey-beginner`,
`tours` and `expert-keyboard`.

`e2e/a11y/robustness.a11y.spec.ts` (same project, added in the T067 integration run) covers the
checks suggested by HO:frontend-004-polish: six screen states at 320 x 720 CSS px with 0 px
horizontal document overflow (WCAG 1.4.10; Import, Tracks, Track, Track without detail, Mixes,
Mixer), and `page.emulateMedia({ forcedColors: 'active' })` with the checked level segment and the
primary Import button keeping a label colour distinct from their fill, plus axe on Import, Track
and Mixer in forced colours (WCAG 1.4.11): 2 themes x 11 = 22 tests.

### 10.4 Lighthouse budgets (SC-003)

`npm run test:lighthouse` = project `lighthouse-setup` (`e2e/lighthouse/urls.setup.ts`: seeds the
shared data through the API and writes the five URLs plus Playwright's Chromium path to
`.state/lighthouse-urls.json`) followed by `node lighthouse/run-lighthouse.mts`, which runs
`lhci autorun --config=lighthouserc.json` with those URLs (argument list, no shell). Budgets in
`e2e/lighthouserc.json` (desktop preset, cold cache, 3 runs, median): FCP <= 1000 ms, LCP <= 1500
ms, CLS <= 0.05 for Import, Tracks, Track, Mixes and Mixer. Throttling method `devtools` (T067): the
desktop profile's RTT and bandwidth are applied in the browser and the paints are observed. The default
`simulate` (Lantern) cannot model Blazor Server's first render, which waits for the SignalR circuit
(no prerendering): it estimated FCP 2.0-2.1 s and Mixer LCP 4.4-4.6 s, while the same page measured
FCP 0.17-0.19 s and LCP 0.35-0.37 s with `devtools` or `provided` throttling. Reports go to `e2e/lighthouse-report/`
(CI artifact). Lighthouse resets storage, so it measures the first visit (Beginner with the
welcome panel). The byte budget (+<= 200 KB) and the performance trace (SC-011) are measured by
the frontend (T063).

### 10.5 Help-coverage tests (FR-025, FR-026, SC-009)

The build-breaking checks (markers <-> anchors, one screen topic per page, links, tour targets,
the 20 reference queries) are xUnit/bUnit tests of the frontend (`HelpCoverageTests.cs`, T050).
`e2e/tests/help.spec.ts` checks the same behaviour in the browser: V-H1 on every screen (help
button, rail item, `?` and F1 open the screen topic; F1 on a focused marked control opens its
topic; focus moves into the panel; Escape returns it), V-H2 tooltip with "Learn more", V-H3 the 20
reference queries (expected topic first), a related link and Back, V-H4 the level note and its
switch, V-H5 transport and strips usable beside the docked panel at 1280 px. V-H6 (unknown topic)
has no browser hook and stays a bUnit test.

### 10.6 Counting rule for SC-008

`e2e/support/keyboard.ts` counts every `press` (a chord such as `ControlOrMeta+K` is one press) and
every text entry (palette query, mix name) as one, the same way SC-008 treats typing the import
URL. The fake stack cannot import from a URL (no network), so the spec starts from the seeded,
completed band track and measures palette -> track -> mixer -> `→` `[` `→` `→` `]` `L` -> `Mod+S`
-> name -> Enter -> `G` `M` -> palette -> load (20 of the 25 allowed).

### 10.7 (004) acceptance scenario -> tests

| Scenario | Unit / component (frontend) | E2E / design suites (qa) |
|---|---|---|
| US1 tokens, themes, no flash (FR-001..FR-005, SC-002, SC-004) | token contrast test, `PreferencesState` tests, Vitest `first-paint`, `theme` | `tests/theme-flash.spec.ts`; `visual/screens.spec.ts` |
| US1 designed screens and states, 360 px, stem colours (FR-006..FR-009, SC-005) | bUnit design components (`StemRibbon`, `SplitProgress`, states) | `visual/screens.spec.ts` (60 baselines); `a11y/screens.a11y.spec.ts` |
| US1 performance (SC-003, SC-011) | frontend T063 (bytes, trace) | `lighthouserc.json` + `lighthouse/` |
| US2 welcome, Beginner journey, presets (FR-010, FR-011, FR-015, FR-016, SC-007) | `WelcomePanelTests`, `JourneyRailTests`, `PracticePresetsTests` | `tests/journey-beginner.spec.ts` |
| US2 level gates, persistence, cross-tab, state kept (FR-012..FR-014, FR-020, SC-010, R7-R9) | feature catalogue and gate tests, `PreferencesState` tests | `tests/journey-levels.spec.ts`; the whole 1.2.0 suite at Advanced (`npm run test:legacy`, SC-006) |
| US3 tooltips, panel, search, level note (FR-021..FR-024) | `HelpPanelTests`, `HelpHintTests`, `HelpContextTests`, help search tests | `tests/help.spec.ts` |
| US3 coverage (FR-025, FR-026, SC-009) | `HelpCoverageTests.cs` | `tests/help.spec.ts` (reference queries in the browser) |
| US4 tours (FR-027) | Vitest `tour.test.ts`, `TourOfferTests`, `TourServiceTests` | `tests/tours.spec.ts` |
| US5 palette, shortcuts, overlay, Expert controls (FR-017..FR-019, SC-008) | Vitest `shortcuts.test.ts`, `CommandPaletteTests`, `ShortcutOverlayTests`, mixer command tests | `tests/expert-keyboard.spec.ts` |
| Accessibility (SC-001) | bUnit roles and labels | `a11y/screens.a11y.spec.ts`; the keyboard-only flows above |

Suite inventory after T064 (`npx playwright test --list`): `chromium` + `firefox` 78 tests of
1.0.0-1.2.0 (unchanged) + 37 `@ux` tests each; `visual` 60; `a11y` 62 (84 after T067: + 22
robustness); `lighthouse-setup` 1 (then
`lhci autorun` over 5 URLs x 3 runs). Success criteria -> evidence: SC-001 `a11y/`; SC-003
`lighthouserc.json`; SC-004 `theme-flash.spec.ts`; SC-005 `visual/`; SC-006 `npm run test:legacy`;
SC-007 `journey-beginner.spec.ts`; SC-008 `expert-keyboard.spec.ts`; SC-009 `help.spec.ts` +
`HelpCoverageTests.cs`; SC-010 `journey-levels.spec.ts`; SC-002 and SC-011 frontend (token
contrast test, T063 trace).
