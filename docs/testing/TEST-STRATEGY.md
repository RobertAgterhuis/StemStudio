# StemStudio test strategy

Owner: qa. Binding inputs: constitution I (test-first, >= 90 % line AND branch per component),
ADR-006 (tooling), ADR-008 (checkpoint hand-offs, SC-004 method, browsers), `tasks.md`.

## 1. Test pyramid

```text
                 E2E (Playwright, Chromium + Firefox, retries 0)     e2e/tests/*.spec.ts
             ----------------------------------------------------
            Contract tests (OpenAPI shapes, URL vectors, UI test ids)
        ------------------------------------------------------------
       Component / integration tests (WebApplicationFactory, bUnit,
       FastAPI TestClient, SQLite in-memory, one real-Demucs test)
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
| Fixture generator | qa | pytest | pytest-cov `--cov-branch`, `fail_under = 90` (`tests/fixtures/pyproject.toml`) | ruff, ruff format, mypy --strict |
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
- **Run policy for real Demucs**: not on every push. It runs in the integration wave (T123), in
  the manual/scheduled workflow `.github/workflows/e2e-real.yml` (devops) and before a release.
  The run records processing time of the 20 s fixture and a 4-minute song, the model used and
  model-cache reuse on a second start.

## 5. E2E scope (Playwright)

Runs against the real stack (`STEMSTUDIO_SEPARATOR=fake docker compose up -d --build --wait`), web
on `E2E_BASE_URL` (default `http://localhost:8080`), API on `E2E_API_URL` (default
`http://localhost:8081`). Selectors use `page.getByTestId(...)` with the ids of
`specs/001-stemstudio-mvp/contracts/ui-test-ids.md` only, wrapped in page objects (`e2e/pages/`).

| Spec | Covers |
|---|---|
| `e2e/tests/health.spec.ts` | `GET /health` of web and API (harness smoke test) |
| `e2e/tests/import.spec.ts` | notice; upload -> completed with 6 stems, model `fake`, BPM ~100; re-upload cached < 2 s (SC-006); rejects `.ogg`, `vimeo.com`, playlist URL with a reason and no new track (SC-007); `corrupt.mp3` -> failed + reason + `track-retry`; streamed upload of `upload_4mb.wav` (G1); `/tracks` -> track -> `open-mixer` |
| `e2e/tests/mixer.spec.ts` | strips per stem with defaults 80 % / pan 0; play after a user gesture; master meter rises above -60 dB; mute/solo change meters; loop 10 s -> 20 s accepted, B <= A refused; SC-004 timing (< 50 ms) for fader, mute, solo |
| `e2e/tests/main-flow.spec.ts` | constitution main flow: import -> completed -> mixer -> change fader, pan, mute, solo, metronome BPM + offset, loop -> save "Practice" -> reload -> load -> every control restored; save-as "Practice 2", duplicate, delete (SC-001, SC-005) |
| `e2e/tests/main-flow.real.spec.ts` (`@real`) | the main flow with real Demucs (`htdemucs_6s` or `htdemucs`) |

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
- **Exactly one** `@pytest.mark.integration` test exists in the worker (audited in T110).

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

## 9. Commands

```bash
# Fixtures (generator + its tests, from the repository root)
uv run --project worker python tests/fixtures/generate_fixtures.py
uv run --project worker pytest -c tests/fixtures/pyproject.toml --cov --cov-branch \
  --cov-config=tests/fixtures/pyproject.toml tests/fixtures/tests
uv run --project worker ruff check tests/fixtures
uv run --project worker ruff format --check tests/fixtures
uv run --project worker mypy --config-file tests/fixtures/pyproject.toml tests/fixtures

# E2E (in e2e/)
npm ci && npx playwright install --with-deps chromium firefox
npm run lint && npm run typecheck
npx playwright test --list
npm test                 # everything except @real (and @restart), Chromium + Firefox, retries 0
npm run test:restart     # after `docker compose restart api web` (T122)
npm run test:real        # real Demucs stack only
```
