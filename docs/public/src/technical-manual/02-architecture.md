# Architecture

StemStudio is a local, single-user application made of containers that talk over HTTP on a
private Docker network. There are no accounts, no authentication and no secrets in the core
product; it must not be exposed to the internet (ADR-002).

## Components

| Component | Technology | Port | Responsibility |
|---|---|---|---|
| Web (`src/StemStudio.Web`) | Blazor Web App, Interactive Server, Radzen | 8080 | All screens; SignalR to the browser; typed API client |
| Audio engine (`src/audio-engine`) | TypeScript, Web Audio API | in browser | Sample-locked multi-stem playback, gain, pan, mute/solo, meters, loop, metronome |
| Web interop (`src/web-interop`) | TypeScript | in browser | Preferences, first paint, theme, shortcuts, tours (driver.js), focus |
| API (`src/StemStudio.Api`) | ASP.NET Core 10 Minimal API, EF Core + SQLite | 8081 | System of record: tracks, stems, mixes; job synchronisation |
| Domain (`src/StemStudio.Domain`) | .NET, no infrastructure | — | Aggregates and rules |
| Contracts (`src/StemStudio.Contracts`) | .NET DTOs | — | Shared wire types for Web and API |
| Persistence (`src/StemStudio.Persistence`) | EF Core, SQLite | — | DbContext and migrations |
| Worker (`worker/`) | Python 3.12, FastAPI | 8000 (internal) | Ingest (yt-dlp, spotDL, ffmpeg), Demucs separation, detail models, beat analysis |
| Generator (`generator/`) <span class="planned">In development</span> | Python 3.12, FastAPI, ACE-Step | 8010 (internal) | Style transfer and song generation (009, 010) |
| Singer <span class="planned">In development</span> | Python, SoulX-Singer | 8010 (internal) | Voice-sample checks and voice conversion (010) |
| Agent (`src/StemStudio.Agent`) <span class="planned">In development</span> | .NET library hosted in the API | — | AI composer agent loop and tool registry (011) |

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

Later features add worker lanes rather than new paths: a network-only lane for metadata (005;
LRCLIB lyrics lookups in 006), a render lane for exports (007), lyrics alignment on the device
lane (006) and a transcription lane (008).
Generative work runs in the separate generator and singer containers (009, 010) because of
their size and incompatible dependencies. The agent (011) runs inside the API and calls the
public API over loopback, so it has exactly the capabilities of a user.

## Dependency rules

- .NET: Web → Contracts ← Api → Domain ← Persistence, and Api → Persistence. Web never
  references Domain, Persistence or Agent. From 011: Api → Agent → Contracts.
- Worker: adapters implement the `Protocol` ports in `ports.py`; heavy libraries sit behind
  those ports so unit tests use fakes.
- Generator: `api → jobs → ports ← {engines, devices, audio}`; only `main.py` composes.
- `tools/quality/dependency_check.py` enforces the direction in CI.

## Contracts and versions

Every interface is a versioned contract under `specs/NNN-*/contracts/`. A feature bumps only
the contracts it changes; versions were allocated up front so features can be built in
parallel (ROADMAP, "Contract version allocation").

| Contract | Current (delivered) | Planned bumps |
|---|---|---|
| Backend API (`backend-api.openapi.yaml`) | 1.7.0 (005: 1.4.0, 007: 1.5.0, 006: 1.6.0, 008: 1.7.0) | 1.8.0 (009), 1.9.0 (010), 1.10.0 (011) |
| Worker API (`worker-api.openapi.yaml`) | 1.6.0 (005: 1.3.0, 007: 1.4.0, 006: 1.5.0, 008: 1.6.0) | — |
| Audio engine (`audio-engine.md`) | 2.1.0 (006: karaoke clock and banner) | 2.2.0 if needed (011) |
| UI test ids (`ui-test-ids.md`) | 1.7.0 (005: 1.4.0, 007: 1.5.0 + patch 1.5.1, 006: 1.6.0, 008: 1.7.0) | one minor per feature, up to 1.10.0 (011) |
| Help content (`help-content.md` + schemas) | 1.0.0 (004) | additive topics per feature |
| Web interop (`web-interop.md`) | 1.0.0 (004) | — |
| Render vectors (`render-vectors.json`) | 1.0.0 (007) | — |
| Generator API (`generator-api.openapi.yaml`) | — | 1.0.0 (009), 1.1.0 (010) |
| Agent tool catalogue (`tool-catalogue.md`) | draft 0.1.0 | 1.0.0 (011) |

Conventions: camelCase JSON, lower-case enums, RFC 9457 problem details for errors, UTC ISO
8601 timestamps. CI lints the current OpenAPI documents with Redocly.
