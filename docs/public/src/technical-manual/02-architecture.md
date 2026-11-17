# Architecture

StemStudio is a local, single-user application made of containers that talk over HTTP on a
private Docker network. There are no accounts, no authentication and no secrets in the core
product; it must not be exposed to the internet (ADR-002).

## Components

| Component | Technology | Port | Responsibility |
|---|---|---|---|
| Web (`src/StemStudio.Web`) | Blazor Web App, Interactive Server, Radzen | 8080 | All screens; SignalR to the browser; typed API client |
| Audio engine (`src/audio-engine`) | TypeScript, Web Audio API | in browser | Sample-locked multi-stem playback, gain, pan, mute/solo, meters, loop, metronome; from 012 the practice tempo (speed, transpose, speed trainer, count-in) |
| Time-stretch stage (012, `src/audio-engine/src/stretch`, `third_party/soundtouch`) | AudioWorklet + SoundTouch 2.4.1 compiled to `soundtouch.wasm` (LGPL-2.1) | in browser | Speed and pitch of the summed mix; separate, replaceable module (ADR-021) |
| Web interop (`src/web-interop`) | TypeScript | in browser | Preferences, first paint, theme, shortcuts, tours (driver.js), focus |
| API (`src/StemStudio.Api`) | ASP.NET Core 10 Minimal API, EF Core + SQLite | 8081 | System of record: tracks, stems, mixes; job synchronisation; from 013 library care (`LibraryCare/`: backup, verify, restore, storage overview, clean-up) |
| Domain (`src/StemStudio.Domain`) | .NET, no infrastructure | — | Aggregates and rules |
| Contracts (`src/StemStudio.Contracts`) | .NET DTOs | — | Shared wire types for Web and API |
| Persistence (`src/StemStudio.Persistence`) | EF Core, SQLite | — | DbContext and migrations |
| Worker (`worker/`) | Python 3.12, FastAPI | 8000 (internal) | Ingest (yt-dlp, spotDL, ffmpeg), Demucs separation, detail models, beat analysis |
| Generator (`generator/`) | Python 3.12, FastAPI, ACE-Step | 8010 (internal) | Style transfer and song generation (009, 010) |
| Singer (`generator/`, variant `soulx`) | Python, SoulX-Singer | 8010 (internal) | Voice-sample checks and voice conversion (010) |
| Agent (`src/StemStudio.Agent`) | .NET library hosted in the API | — | AI composer agent loop and tool registry (011) |

## Data flow

1. The browser talks to **Web** over SignalR. Web calls the **API** server-side through a
   typed `HttpClient`.
2. For an import the API creates a track and submits a job to the **worker**. A
   `BackgroundService` (JobSyncService) polls the worker about once per second and moves the
   track forward: queued → downloading → separating → analysing → completed (or failed).
3. When a job completes, the API copies the FLAC stems into its own data folder (ADR-004)
   and becomes their system of record.
4. The browser's **audio engine** fetches the stems directly from the API (CORS, HTTP Range,
   ETag) and plays them in one `AudioContext`: BufferSource → Gain → StereoPanner →
   Analyser → master Gain, all started at the same `when` (ADR-005).
5. Detail separation is a second worker job per track; standard jobs always have priority.
6. Practice tempo (012, ADR-021): at neutral settings (100 %, 0 semitones) nothing extra is
   loaded and the graph is unchanged. Otherwise the sources play at `playbackRate = speed`
   into the mix bus, which feeds one AudioWorklet (`js/time-stretch-processor.js`) that runs
   SoundTouch from `js/soundtouch/soundtouch.wasm` to restore the pitch (and transpose). The
   stage adds a fixed delay `D = 5120` frames plus a content offset `c(p) = round(-1100 *
   log2(p))` frames; the transport, metronome, karaoke clock and count-in are anchored to the
   heard latency `L(p) = (D + c(p)) / sampleRate` (66-147 ms), so clicks and the ball stay
   with the stretched music. Exports never apply the practice tempo.

Later features add worker lanes rather than new paths: a network-only lane for metadata (005;
LRCLIB lyrics lookups in 006), a render lane for exports (007), lyrics alignment on the device
lane (006) and a transcription lane (008).
Generative work runs in the separate generator and singer containers (009, 010) because of
their size and incompatible dependencies. The agent (011) runs inside the API and calls the
public API over loopback, so it has exactly the capabilities of a user.

### Library care (013, ADR-022)

Library jobs (backup, verify, restore) run one at a time in the API (`LibraryCare/Jobs`); their
state and the restore journal are JSON files under `<data>/library-care/`, outside the database
that a restore replaces. A **write gate** (`LibraryCare/Gate`) keeps the library consistent:
while a backup runs, deletes and clean-ups answer `409 backup-running` (play, mix and export
keep working); while a restore switches folders the API answers `503` with `Retry-After` for a
few seconds (maintenance mode). A backup takes an online SQLite snapshot, scrubs the excluded
categories from the copy and streams one ZIP (store for FLAC/JPEG, deflate otherwise) with a
SHA-256 per entry and the manifest last; archives are written to the bind-mounted backups
folder (`/backups`), never through the browser. A restore validates the whole archive
(data-model 3.4), extracts into `library-care/staging/<jobId>/`, checks and migrates the staged
database first, then switches folders through a journal that start-up recovery completes or
rolls back. A merge restore adds the missing items of the staged library to the live database
in one transaction instead of switching. The replaced library is kept in
`library-care/previous/` until it is undone or discarded. The worker reports its disk use
(`GET /v1/storage`), cleans its caches (`POST /v1/storage/cleanup`) and rebuilds a standard job
from the library stems on a cache miss (`POST /v1/jobs/rehydrate`, FR-019).

## Dependency rules

- .NET: Web → Contracts ← Api → Domain ← Persistence, and Api → Persistence. Web never
  references Domain, Persistence or Agent. From 011: Api → Agent → Contracts.
- Worker: adapters implement the `Protocol` ports in `ports.py`; heavy libraries sit behind
  those ports so unit tests use fakes.
- Generator: `api → jobs → ports ← {engines, devices, audio}`; only `main.py` composes.
- Audio engine (012): `stretch/processor.ts` imports only `stretch/{constants,params,fifo,abi}`
  (the worklet scope); `transport.ts`, `mixer.ts`, `metronome/**` and `karaoke/**` never import
  `practice/**` or `stretch/**`; `practice/**` never imports `karaoke/**`.
- `tools/quality/dependency_check.py` enforces the direction in CI.

## Contracts and versions

Every interface is a versioned contract under `specs/NNN-*/contracts/`. A feature bumps only
the contracts it changes; versions were allocated up front so features can be built in
parallel (ROADMAP, "Contract version allocation").

| Contract | Current (delivered) | Planned bumps |
|---|---|---|
| Backend API (`backend-api.openapi.yaml`) | 1.12.0 (005: 1.4.0, 007: 1.5.0, 006: 1.6.0, 008: 1.7.0, 009: 1.8.0, 010: 1.9.0, 011: 1.10.0 = 1.9.0 + `/api/agent/*`, claude-key: 1.21.0, 012: 1.11.0 = 1.21.0 + `practice` on mixes, 013: 1.12.0 = 1.11.0 + library care; labels identify merged deltas, not an order) | — |
| Worker API (`worker-api.openapi.yaml`) | 1.7.0 (005: 1.3.0, 007: 1.4.0, 006: 1.5.0, 008: 1.6.0, 013: 1.7.0 storage, clean-up and rehydration) | — |
| Backup archive (`backup-archive.md` + `backup-manifest.schema.json`) | 1.0.0 (013) | a new major only for an incompatible layout |
| Audio engine (`audio-engine.md`) | 2.3.0 (006: 2.1.0 karaoke clock and banner; 2.2.0 unused; 012: 2.3.0 `setPractice`, `getPracticeStatus`, `OnPracticeChanged`, time-stretch stage and WASM ABI 1) | — |
| UI test ids (`ui-test-ids.md`) | 1.12.0 (005: 1.4.0, 007: 1.5.0 + patch 1.5.1, 006: 1.6.0, 008: 1.7.0, 009/010: 1.8.0/1.9.0, 011: 1.10.0, claude-key: 1.21.0, 012: 1.11.0, 013: 1.12.0) | one minor per feature |
| Help content (`help-content.md` + schemas) | 1.0.0 (004) | additive topics per feature |
| Web interop (`web-interop.md`) | 1.0.0 (004) | — |
| Render vectors (`render-vectors.json`) | 1.0.0 (007) | — |
| Generator API (`generator-api.openapi.yaml`) | 1.1.0 (009: 1.0.0, 010: 1.1.0) | — |
| Agent tool catalogue (`tool-catalogue.md`, 57 tools), agent E2E script schema (`agent-script.schema.json`), evaluation set (`eval-set.md`) | catalogue 1.3.0 (011: 1.0.0; 012: `practice` read-only in `get_mix` / `mixer_get_state`), schema and evaluation set 1.0.0 | catalogue 1.1.0 (022), 1.2.0 (023) |

Conventions: camelCase JSON, lower-case enums, RFC 9457 problem details for errors, UTC ISO
8601 timestamps. CI lints the current OpenAPI documents with Redocly.
