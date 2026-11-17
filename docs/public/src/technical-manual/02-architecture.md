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
| API (`src/StemStudio.Api`) | ASP.NET Core 10 Minimal API, EF Core + SQLite | 8081 | System of record: tracks, stems, mixes; job synchronisation; from 013 library care (`LibraryCare/`: backup, verify, restore, storage overview, clean-up); from 022 the library folders (`Library/`: filing, folder views, search, library info); from 023 the lyric assistant (`LyricAssist/`: runner, suggestions, vendored dictionaries) |
| Domain (`src/StemStudio.Domain`) | .NET, no infrastructure, no packages | — | Aggregates and rules; from 022 `Library/` (folder keys and ids, placement rules), from 023 `Lyrics/Assist/` (anchors, suggestion filter, syllables, rhymes) |
| Contracts (`src/StemStudio.Contracts`) | .NET DTOs | — | Shared wire types for Web and API |
| Persistence (`src/StemStudio.Persistence`) | EF Core, SQLite | — | DbContext and migrations |
| Worker (`worker/`) | Python 3.12, FastAPI | 8000 (internal) | Ingest (yt-dlp, spotDL, ffmpeg), Demucs separation, detail models, beat analysis |
| Generator (`generator/`) | Python 3.12, FastAPI, ACE-Step | 8010 (internal) | Style transfer and song generation (009, 010) |
| Singer (`generator/`, variant `soulx`) | Python, SoulX-Singer | 8010 (internal) | Voice-sample checks and voice conversion (010) |
| Agent (`src/StemStudio.Agent`) | .NET library hosted in the API | — | AI composer agent loop and tool registry (011); from 023 `LyricAssist/` (prompts, output parser, suggestion validator, model client) |

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

### Library by artist and album (022, ADR-036)

Folders are **derived, not stored**. Each library item (`Assets`) carries a placement: artist,
album and song with display name, normalised key and source (item, track, match, import,
fallback), the album year and the version of the filing rules (`FiledVersion`). A folder exists
while at least one item sits in it; its id is `ar`, `al` or `so` plus 16 hex characters of a
SHA-256 over the keys. Every creator of an item (export, stem pack, score, generated take) files
it in the same save through `ILibraryFiler`, so the 202 answer already carries `placement.filed`.
The band info applier, the track synchroniser and the library info service re-file a track's
items; names set by the user are never overwritten, and deleting a track never re-files.

A hosted service (`LibraryFilingBackfill`) files items below the current rules version: **2 s
after the host started**, in batches of `StemStudio:LibraryBackfillBatchSize` (default 500, one
transaction per batch), waiting for the 013 maintenance gate before each batch, and again after a
restore (the first library read triggers it when `pendingFiling > 0`; a failed run is repeated up
to five times). This is the data migration of existing items and of restored older backups.

Folder views and the search read one in-memory snapshot (`LibraryIndex`, rebuilt once per library
revision, a 16-hex stamp over counts and newest changes). `GET /api/library/folders[/{id}]` answers
with the ETag `"lib-<revision>"` and `304`; the search scans folded strings in memory (no SQL, no
`LIKE`). Folder art needs no new files: it references the 005 artwork URLs. Budgets measured at
5,000 items in Release: backfill about 1.1-1.6 s, folder views p95 about 11 ms, search p95 about
17 ms (CI step `Category=Perf`, see Quality gates).

### Lyric writing assistant (023, ADR-037)

An assist is one explicit request (`check`, `devices`, `alternatives`, `continue` or `titles`) on a
saved 010 lyrics sheet. `LyricAssistRunner` admits **one assist at a time** and pauses during library
maintenance; `LyricAssistExecutor` first runs the **deterministic layer** in process (WeCantSpell.Hunspell
over the vendored OpenTaal and SCOWL dictionaries, the licence lexicon, intentional marks and personal
words), then the **model layer** block by block (`SheetBlocks`: at most 8 lyric lines, neighbouring
short sections merged). The model layer is `StemStudio.Agent/LyricAssist`: the prompt builder (sheet
text only between random-boundary delimiters), the closed-schema output parser
(`lyric-assist-output` 1.0.0), the suggestion validator and `LyricModelClient` (Ollama with the
schema as `format`, or Claude with one forced tool). The Agent project stays Contracts-only; the
deterministic checks reach it through the port `ILyricVerification` (an API adapter over the Domain).

Everything the model returns is stored as **anchored suggestions** (line, UTF-16 offset, length,
original text) by `AssistSink`, after the backend re-checks every anchor against the saved text
and applies the sensitivity, mark and word filters (`SuggestionFilter`). The sheet text changes only
through accept, accept-all, undo or restore of stored suggestions; each change is one
`LyricsSheetRevision` (the last 50 are kept), and `LyricsSheet.Revision` is an optimistic
concurrency token (`409 revision-conflict`). Progress and results stream as Server-Sent Events
(`GET /api/lyric-assists/{id}/events`, resumable with `after`). The agent reaches the same service
through eight text-free tools over the loopback client (`ILyricAssistActions`).

When the backend is Ollama and the GPU is busy (a heavy job holds it), the factory does **not**
fall back to the smaller CPU model for lyrics (it produced no valid output in the spike): the
assist answers `model-unavailable` with a fix text, and `check` still runs the dictionary layer.
A GPU job that starts during an assist stops it with `backend-unavailable`.

## Dependency rules

- .NET: Web → Contracts ← Api → Domain ← Persistence, and Api → Persistence. Web never
  references Domain, Persistence or Agent. From 011: Api → Agent → Contracts.
- Worker: adapters implement the `Protocol` ports in `ports.py`; heavy libraries sit behind
  those ports so unit tests use fakes.
- Generator: `api → jobs → ports ← {engines, devices, audio}`; only `main.py` composes.
- Audio engine (012): `stretch/processor.ts` imports only `stretch/{constants,params,fifo,abi}`
  (the worklet scope); `transport.ts`, `mixer.ts`, `metronome/**` and `karaoke/**` never import
  `practice/**` or `stretch/**`; `practice/**` never imports `karaoke/**`.
- Library and lyric assistant (022, 023): `Domain/Library` and `Domain/Lyrics` use nothing outside
  Domain (no package, no `Microsoft.*`, not each other); `Api/Library` never references `Web`,
  `Agent` or `Api/LyricAssist`, and `Api/LyricAssist` never `Web` or `Api/Library`;
  `Agent/LyricAssist` references only Contracts (never `Api`, `Domain`, `Persistence`, `Web` or
  WeCantSpell); `Agent/Tools` may use only `Agent.Model` and `Agent.Ports`; the Web folders
  `Library` and `Components/Library` and `Components/Lyrics/Assist` call the API only through the
  typed client in `Web/Api` (no `HttpClient`, no WeCantSpell) and never each other.
- `tools/quality/dependency_check.py` enforces the direction in CI.

## Contracts and versions

Every interface is a versioned contract under `specs/NNN-*/contracts/`. A feature bumps only
the contracts it changes; versions were allocated up front so features can be built in
parallel (ROADMAP, "Contract version allocation").

| Contract | Current (delivered) | Planned bumps |
|---|---|---|
| Backend API (`backend-api.openapi.yaml`) | 1.23.0 = 1.11.0 + 013 (1.12.0) + 022 (1.22.0) + 023 (1.23.0); earlier labels: (005: 1.4.0, 007: 1.5.0, 006: 1.6.0, 008: 1.7.0, 009: 1.8.0, 010: 1.9.0, 011: 1.10.0 = 1.9.0 + `/api/agent/*`, claude-key: 1.21.0, 012: 1.11.0 = 1.21.0 + `practice` on mixes, 013: 1.12.0 = 1.11.0 + library care; labels identify merged deltas, not an order) | - |
| Worker API (`worker-api.openapi.yaml`) | 1.7.0 (005: 1.3.0, 007: 1.4.0, 006: 1.5.0, 008: 1.6.0, 013: 1.7.0 storage, clean-up and rehydration) | — |
| Backup archive (`backup-archive.md` + `backup-manifest.schema.json`) | 1.0.0 (013) | a new major only for an incompatible layout |
| Audio engine (`audio-engine.md`) | 2.3.0 (006: 2.1.0 karaoke clock and banner; 2.2.0 unused; 012: 2.3.0 `setPractice`, `getPracticeStatus`, `OnPracticeChanged`, time-stretch stage and WASM ABI 1) | — |
| UI test ids (`ui-test-ids.md`) | 1.23.0 (022: 1.22.0, 023: 1.23.0; earlier: 005: 1.4.0, 007: 1.5.0 + patch 1.5.1, 006: 1.6.0, 008: 1.7.0, 009/010: 1.8.0/1.9.0, 011: 1.10.0, claude-key: 1.21.0, 012: 1.11.0, 013: 1.12.0) | one minor per feature |
| Help content (`help-content.md` + schemas) | 1.0.0 (004) | additive topics per feature |
| Web interop (`web-interop.md`) | 1.2.0 (004: 1.0.0; 022: scope `library`, key `/` -> `library.search.focus`; 1.1.0 stays allocated to 014) | - |
| Render vectors (`render-vectors.json`) | 1.0.0 (007) | — |
| Generator API (`generator-api.openapi.yaml`) | 1.1.0 (009: 1.0.0, 010: 1.1.0) | — |
| Agent tool catalogue (`tool-catalogue.md`, 69 tools), agent E2E script schema (`agent-script.schema.json`), evaluation set (`eval-set.md`, 40 scored cases), lyric output schema (`lyric-assist-output.schema.json`) | catalogue 1.2.0 = 1.0.0 + 012 (1.3.0: `practice` read-only in `get_mix` / `mixer_get_state`) + 022 (1.1.0: four library tools) + 023 (1.2.0: eight lyric tools); evaluation set 1.1.0; script schema 1.0.0 (023 adds the `{{context.lyricsId}}` and `{{result.<tool>.ids:<category>}}` placeholders in its descriptions); lyric output schema 1.0.0 | - |

Conventions: camelCase JSON, lower-case enums, RFC 9457 problem details for errors, UTC ISO
8601 timestamps. CI lints the current OpenAPI documents with Redocly.
