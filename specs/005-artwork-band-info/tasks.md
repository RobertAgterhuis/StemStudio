---

description: "Task list for feature 005 song artwork and band info (Phase 0 by the architect, one parallel wave of owner agents, then a CTO integration wave)"
---

# Tasks: Song Artwork and Band Info

**Input**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [quickstart.md](quickstart.md), [contracts/DELTA.md](contracts/DELTA.md),
constitution 1.2.0, ADR-014.

**Status**: Phase 0 **done** (2026-11-15, combined with 007 Phase 0 in one skeleton; hand-off
`docs/handoffs/architect-005-007-phase0.md`). Contract versions (docs/ROADMAP.md): worker 1.3.0,
backend 1.4.0, UI test ids 1.4.0 (on 004's 1.3.0). The code is at the 007 versions (worker 1.4.0,
backend 1.5.0), which contain the 005 documents unchanged (tested). The wave may start.

**Tests**: REQUIRED (constitution I). Within every owner sequence the test task comes before the
implementation task it covers (red -> green -> refactor); every owner reports >= 90 % line AND
branch coverage of its component in its hand-off. No task needs the network: online clients use
recorded, trimmed CC0 responses (`httpx.MockTransport`) and injected clock/sleeper; images are
generated in the tests. Real services are touched only by `@pytest.mark.integration` tests run by
the CTO (T062).

**Organization**: grouped by user story (US1..US3); the
[execution plan](#execution-plan-per-owner-parallel-wave) regroups the IDs per agent.

## Format: `[ID] [P?] [Story?] [owner] Description (FR/SC refs)`

- **[P]**: parallel with the other [P] tasks of the same phase (different files, no dependency on
  an incomplete task). Tasks of different owners are always parallel in the wave.
- **[owner]**: the agent from `.claude/agents/`; it writes only inside its owned paths (plan.md
  ownership map) plus `docs/handoffs/<owner>-005-<checkpoint>.md`.
- Paths are relative to the repository root. **CTO** = orchestrator.
- audio-engine and beat-analysis have no tasks in this feature.

## Checkpoint hand-offs (binding, ADR-008)

Each owner writes `docs/handoffs/<owner>-005-<checkpoint>.md` at `foundational` (if it has
foundational tasks), every story it takes part in (`us1`..`us3`) and `polish`, with red -> green
commit hashes per task pair, exact coverage numbers and lint/SIG output. Marker `HO:<checkpoint>`.

## Cross-owner rule (binding)

| Consumer | Needs | Contract | Replaced in tests by |
|---|---|---|---|
| stem-separation (`jobs/`, `api/`) | artwork candidates, normaliser, metadata lookup | `ports.py` `ArtworkCandidate`, `ImageNormalizer`, `MetadataLookup` (T001) | fakes returning fixed `ArtworkImages` / `MetadataResult` |
| audio-ingest (`metadata/`, `ingest/`) | the result models | `schemas.py` (T001) | n/a |
| backend | worker artwork, metadata jobs, normalise route | worker contract (T004; examples copied to `tests/StemStudio.Api.Tests/Worker/Examples/`, T003) | stub `HttpMessageHandler` |
| frontend | `Track.artwork`, `Track.metadata`, band info, artwork routes | backend contract + Contracts DTOs (T003, T004) | fake `IStemStudioApiClient` |
| qa | UI ids, fake provider | `ui-test-ids.md` (T004), `STEMSTUDIO_FAKE_ONLINE` (T001) | suite authored in the wave, executed in T059 |
| devops | env vars | data-model.md 1 | skeleton |

---

## Phase 0: Contract code and skeleton (architect, BEFORE the wave; done 2026-11-15)

- [x] T001 [architect] Worker contract code: `schemas.py` (version bump; `ArtworkSource`, `ArtworkInfo`, `SourceMetadata.{album,isrc,duration_seconds,lookup_title,lookup_artist}`, `Job.artwork`, `CreateMetadataJobRequest`, `MetadataJob`, `MetadataJobState`, `MetadataResult`, `RecordingMatch`, `ReleaseGroupInfo`, `ArtistInfo`, `ArtistMember`, `WikipediaSummary`, `ArtworkUpload`, `Health.online_lookups`); `ports.py` (`ArtworkCandidate`, `ArtworkImages`, `ImageNormalizer`, `MetadataQuery`, `MetadataLookup`, new optional fields on `SourceInfo`/`FetchedAudio`); `config.py` (`online_lookups`, `contact_url`, `wikipedia_languages`, `fake_online`); skeleton tests `worker/tests/test_metadata_contract.py`; worker contract tests switched to the 005 document (agreed one-time edit in stem-separation tests) (FR-001, FR-003, FR-010)
- [x] T002 [P] [architect] Worker dependencies: `tinytag==2.3.2` (new, MIT); Pillow, httpx (dev -> runtime), rapidfuzz as direct runtime dependencies at their locked versions; `uv lock`; mypy overrides for `tinytag`; licences in `docs/handoffs/architect-005-007-phase0.md`
- [x] T003 [P] [architect] .NET contract code (DELTA.md 2.3) + `ApiRoutes`; skeleton tests `tests/StemStudio.Api.Tests/Contract/ArtworkContractTests.cs`; test project links -> 005 contracts; worker example copies re-synced (+ `artwork`, metadata job examples); new operationIds staged in `PendingArtworkOperations` (agreed one-time edit in backend tests) (FR-006, FR-008)
- [x] T004 [P] [architect] Full contract documents: worker-api, backend-api (OpenAPI 3.1, Redocly lint clean), ui-test-ids, redocly config, from DELTA.md with the assigned versions; ADR-014 status Accepted
- [x] T005 [architect] Verify the skeleton: `dotnet build` 0 warnings, all .NET tests + coverage gates, worker `pytest --cov`/ruff/mypy, engine untouched, `tools/quality/run_all.py` 0 errors; hand-off `docs/handoffs/architect-005-007-phase0.md` (combined with 007)

**Checkpoint**: skeleton green; wave may start.

---

## Phase 1: Setup (per owner, no story)

- [x] T006 [P] [quality-gate] Failing tests in `tools/quality/tests/`: `worker/src/stemstudio_worker/metadata` imports only `ports`, `schemas`, `config`; `ingest`, `jobs`, `api` do not import `metadata` (only `main.py` composes it); `StemStudio.Api/Metadata` and `/Artwork` do not reference `StemStudio.Web` (plan.md dependency direction)
- [x] T007 [quality-gate] Implement the rules in `tools/quality/dependency_check.py`; re-run `run_all.py`; update `docs/quality/SIG-REPORT.md`
- [x] T008 [P] [devops] `docker-compose.yml`: worker env `STEMSTUDIO_ONLINE_LOOKUPS`, `STEMSTUDIO_CONTACT_URL`, `STEMSTUDIO_WIKIPEDIA_LANGUAGES`, `STEMSTUDIO_FAKE_ONLINE` (pass-through, documented defaults; no value baked in); `scripts/dev.*` likewise; CI `CONTRACTS_DIR` and `scripts/test-all.*` -> `specs/007-export-library/contracts` (combined 005+007 wave: the 007 documents contain 005's; one switch, done once in the 007 T008); CI asserts the worker image does not set `STEMSTUDIO_CONTACT_URL` (FR-010)
- [x] T009 [P] [qa] `tests/fixtures/metadata/`: `matching-cases.json` (>= 20 cases, research R-05; recorded CC0 MusicBrainz responses trimmed to the read fields), recorded Wikidata/Wikipedia/CAA JSON for the two reference artists (Wikipedia extracts replaced by self-written placeholder text so no CC BY-SA text is committed), README with provenance; update `docs/testing/TEST-STRATEGY.md` (traceability US1..US3)

---

## Phase 2: Foundational (blocking inside each owner)

### audio-ingest (tests first)

- [x] T010 [P] [audio-ingest] Failing tests `worker/tests/metadata/test_user_agent.py`, `test_http.py`, `test_ratelimit.py`: UA pattern with and without a valid `STEMSTUDIO_CONTACT_URL`; invalid values (http, `mailto:`, `@`, user-info, > 200 chars, whitespace) fall back and the warning does not contain the value; the client refuses non-https and non-allow-listed hosts, re-validates each of <= 3 redirects, caps bodies at 10 MB, sets 10 s timeouts; token bucket 1/s per host family with fake clock; 503/429 `Retry-After` (seconds and HTTP date, cap 30 s) and 2/4/8 s jittered back-off, 3 retries then a typed error (FR-010, FR-011, SC-005)
- [x] T011 [audio-ingest] Implement `metadata/{user_agent,http,ratelimit}.py` to pass T010
- [x] T012 [P] [audio-ingest] Failing tests `worker/tests/metadata/test_images.py` with generated images: JPEG/PNG/WebP in, two square JPEGs out (600/160, <= 200 KB/20 KB), EXIF orientation applied then stripped, alpha flattened, first frame of animations, centre crop of 500 x 499 and 16:9, > 40 MP and truncated files rejected with typed errors, SHA-256 of the cover bytes (FR-007)
- [x] T013 [audio-ingest] Implement `metadata/images.py` (`PillowImageNormalizer`) to pass T012

### stem-separation (tests first)

- [x] T014 [P] [stem-separation] Failing tests `worker/tests/jobs/test_metadata_lane.py` (fake lookup, fake clock): a single-thread lane separate from the separation scheduler; queued -> running -> completed/failed; persisted in `metadata-jobs/*.json`; restart re-runs queued/running jobs; a long fake separation does not delay a metadata job; results and artwork swept after 24 h (ADR-014 D3)
- [x] T015 [stem-separation] Implement `jobs/{metadata_lane,metadata_store}.py` to pass T014

### backend (tests first)

- [x] T016 [P] [backend] Failing tests `tests/StemStudio.Domain.Tests/Tracks/ArtworkPrecedenceTests.cs`, `TrackMetadataTests.cs`, `Artists/ArtistTests.cs`: precedence (data-model 3.1) incl. equal rank with a new hash, user picture kept over automatic offers, reset to automatic; metadata state machine 3.4 incl. retry/correction and invalid transitions; artist value limits (genres <= 8, members <= 12, summary fields) (FR-005, FR-009)
- [x] T017 [backend] Implement `Domain/Tracks/{TrackArtwork,ArtworkSource,ArtworkPrecedence,TrackMetadata}.cs`, `Track` members, `Domain/Artists/Artist.cs` to pass T016
- [x] T018 [P] [backend] Failing tests `tests/StemStudio.Persistence.Tests/ArtworkPersistenceTests.cs`: JSON round trips, `Artists` table, FK `SET NULL`, orphaned artist deleted with the last track, a database migrated from the 003 schema loads with `Metadata.Status = none`
- [x] T019 [backend] Implement configuration, `ArtistRepository` and migration `AddArtworkAndArtists` to pass T018
- [x] T020 [P] [backend] Failing tests `tests/StemStudio.Api.Tests/Worker/WorkerMetadataClientTests.cs` with the worker examples: `Job.artwork` and new `SourceMetadata` fields parsed (absent in 1.2.0 bodies -> null); create/get/result of metadata jobs; artwork downloads (`image/jpeg`, 404 -> null); `POST /v1/artwork` streaming and its 400/413/415/422 mapping
- [x] T021 [backend] Implement the worker client additions to pass T020

**Checkpoint**: HO:foundational for audio-ingest, stem-separation and backend.

---

## Phase 3: User Story 1 - Cover while mixing and in the library (P1) MVP

**Goal**: source artwork captured at import and shown in mixer, track page and library.
**Independent test**: quickstart A2 (fake stack), A9.

- [x] T022 [P] [US1] [audio-ingest] Failing tests `worker/tests/metadata/test_source_artwork.py` and ingest tests: YouTube thumbnail choice from an `info["thumbnails"]` fixture (largest `i.ytimg.com` >= 300 px, maxres > sd > hq); Spotify `Song.cover_url`, `isrc`, `album_name` read from a stub `Song`; upload `tinytag` front cover then `any` image, tag title/artist/album; WAV -> no candidate; `SourceInfo`/`FetchedAudio` carry candidate, album, ISRC, cleaned lookup names (research R-04 title cleaning cases) (FR-001)
- [x] T023 [US1] [audio-ingest] Implement `metadata/{source_artwork,titles}.py` and the hand-over in `ingest/{fetcher,spotify,upload,resolver}.py` to pass T022
- [x] T024 [P] [US1] [stem-separation] Failing tests `worker/tests/jobs/test_artwork_step.py`, `worker/tests/api/test_artwork_routes.py`: the standard job runs "Saving cover" after the download with the injected normaliser; any error (network, decode, timeout > 15 s) is logged and the job continues with `artwork = null`; `Job.artwork` set; `SourceMetadata.durationSeconds` set after normalisation; `GET /v1/jobs/{id}/artwork/{variant}` serves JPEG with ETag, 404 without; cached results keep their artwork (FR-001, FR-002, SC-006)
- [x] T025 [US1] [stem-separation] Implement in `jobs/{runner,progress}.py`, `api/routes.py`, `main.py` to pass T024
- [x] T026 [P] [US1] [backend] Failing tests `tests/StemStudio.Api.Tests/Artwork/ArtworkSyncTests.cs`, `ArtworkEndpointTests.cs`: a standard poll with a new `artwork` hash downloads both variants atomically and offers them; the same hash is not downloaded again; `TrackDto.artwork` URLs carry `v`; `GET /api/tracks/{id}/artwork/{variant}`: JPEG, ETag/If-None-Match 304, immutable header only with matching `v`; 404 without artwork; deleting a track deletes the files (FR-005..FR-007, FR-016)
- [x] T027 [US1] [backend] Implement `Api/Artwork/{ArtworkFileStore,ArtworkService,ArtworkEndpoints}.cs` and the sync hook in `Sync/TrackJobApplier.cs` to pass T026
- [x] T028 [P] [US1] [frontend] Failing bUnit tests: `TrackArtwork` renders `img` with `loading="lazy"`, `decoding="async"`, width/height and alt text "Cover of <title>", or `ArtworkPlaceholder` (initials, colour from a stable title hash, `aria-hidden` decoration + text alternative); `track-thumb-<id>`, `track-cover`, `mixer-cover` with `data-artwork-source`; broken image -> placeholder (`onerror`) (FR-006, US1-1..5)
- [x] T029 [US1] [frontend] Implement `Components/Shared/{TrackArtwork,ArtworkPlaceholder}.razor`, the thumbnail column in `Tracks.razor`, the cover on `TrackDetail` and in the mixer header (004 layout), help topic `artwork` to pass T028
- [x] T030 [US1] [qa] Author `e2e/tests/artwork.spec.ts` (fake stack `STEMSTUDIO_FAKE_ONLINE=not-found`): YouTube/Spotify/upload-with-cover/WAV sources and their `data-artwork-source`; thumbnail visible in the library; seed script `e2e/support/seed-artwork.ts` and a timing assertion for 100 rows (SC-001, SC-003)

**Checkpoint**: HO:us1.

---

## Phase 4: User Story 2 - Read about the band (P2)

**Goal**: metadata job, band info panel with attribution, correction.
**Independent test**: quickstart A3-A8.

- [x] T031 [P] [US2] [audio-ingest] Failing tests `worker/tests/metadata/test_musicbrainz.py`, `test_matcher.py`: ISRC query first; search query with `dur:[d-5000 TO d+5000]` and escaping of Lucene special characters; matcher over `matching-cases.json` gives the expected recording or not-found for every case (SC-004); release group with the earliest official album date; 503-then-200 handled via the T011 client (FR-003, FR-004)
- [x] T032 [US2] [audio-ingest] Implement `metadata/{musicbrainz,matcher}.py` to pass T031
- [x] T033 [P] [US2] [audio-ingest] Failing tests `test_coverart.py`, `test_wikidata.py`, `test_wikipedia.py`, `test_lookup.py`: CAA `front-500` redirect to archive.org followed and normalised (`source=coverartarchive`), 404 -> no artwork + no warning; Wikidata claims P136/P527 (+ P580/P582)/P571/P576/P495/P856 and a labels call for <= 50 ids; Wikipedia summary in the configured language order, `type != standard` dropped, attribution fields built (history URL, licence); `MetadataLookup` orchestration: partial failures become warnings, recording failure fails, lookups off -> `disabled` without requests, requests recorded by the fake transport contain only the allowed query fields and the UA (FR-008, FR-010, SC-005)
- [x] T034 [US2] [audio-ingest] Implement `metadata/{coverart,wikidata,wikipedia,lookup,fake}.py` (fake provider per `STEMSTUDIO_FAKE_ONLINE`) to pass T033
- [x] T035 [P] [US2] [stem-separation] Failing tests `worker/tests/api/test_metadata_routes.py`: create/get/result/artwork routes per contract (422, 404, 409), `Health.onlineLookups`, composition with the real lookup or the fake provider (FR-003, FR-011)
- [x] T036 [US2] [stem-separation] Implement `api/metadata_routes.py`, `main.py` wiring to pass T035
- [x] T037 [P] [US2] [backend] Failing tests `tests/StemStudio.Api.Tests/Metadata/MetadataSyncTests.cs`: `none -> pending` when the standard poll reports `durationSeconds` and on first `GET` of an old completed track (FR-012); synchronizer creates the job with the lookup names, polls, applies the result (artist upsert, 30-day refresh, `coverartarchive` artwork offered), maps failures/404 to `failed`, keeps state while the worker is unreachable (FR-003, FR-009)
- [x] T038 [US2] [backend] Implement `Api/Metadata/{MetadataSynchronizer,MetadataJobApplier,MetadataService}.cs` (registered in `JobSyncService`) to pass T037
- [x] T039 [P] [US2] [backend] Failing tests `BandInfoEndpointTests.cs`: `GET /api/tracks/{id}/band-info` for each status, sources list and licences, validates against the backend contract; `POST /api/tracks/{id}/metadata/lookup` with corrected names (stored, `correctedByUser`), empty body retry, 409 while searching, 422 limits (FR-008, FR-009)
- [x] T040 [US2] [backend] Implement `Api/Metadata/BandInfoEndpoints.cs` to pass T039
- [x] T041 [P] [US2] [frontend] Failing bUnit tests: `BandInfoPanel` per status (searching skeleton, found facts, notFound + correct, failed + retry, disabled + help link); members row only for groups; `WikipediaAttribution` renders title link, "Wikipedia contributors" history link, "CC BY-SA 4.0" licence link and "Excerpt, unmodified"; `band-info-sources`; `CorrectMetadataDialog` validation and submit; journey levels (beginner: name, type, years, summary; advanced/expert: all facts and sources); `mixer-album` text (FR-008, FR-009, FR-015)
- [x] T042 [US2] [frontend] Implement `Components/BandInfo/*`, `IStemStudioApiClient.{GetBandInfoAsync, LookupMetadataAsync}`, `band-info-open` on track page and mixer, help topics `band-info` and `online-lookups`, legal notice sentence (FR-014) to pass T041
- [x] T043 [US2] [qa] Author `e2e/tests/band-info.spec.ts` (fake stack, modes `found`, `not-found`, `failed`, `off`): panel content, attribution links, retry, correction flow updates the panel and cover, disabled notice

**Checkpoint**: HO:us2.

---

## Phase 5: User Story 3 - Choose my own picture (P3)

**Goal**: user image upload and reset.
**Independent test**: spec US3.

- [x] T044 [P] [US3] [stem-separation] Failing tests `worker/tests/api/test_artwork_upload.py`: `POST /v1/artwork` normalises via the injected normaliser, 400/413/415/422 per contract, files kept 1 h, `GET /v1/artwork/{id}/{variant}` (FR-013)
- [x] T045 [US3] [stem-separation] Implement `api/artwork_routes.py` to pass T044
- [x] T046 [P] [US3] [backend] Failing tests `UserArtworkEndpointTests.cs`: `PUT /api/tracks/{id}/artwork` streams to the worker (10 MiB limit enforced while streaming, 413), stores `user/` files, `Artwork.source = user`; worker errors mapped to problem details; 503 when the worker is down; `DELETE` returns to `AutoArtwork` (or none) and deletes `user/` files (FR-013)
- [x] T047 [US3] [backend] Implement in `Api/Artwork/*` to pass T046
- [x] T048 [P] [US3] [frontend] Failing bUnit tests: upload input (accept JPEG/PNG/WebP, client-side size check), progress, `artwork-error`, `artwork-reset` only when the source is `user`; advanced/expert levels only (FR-013, FR-015)
- [x] T049 [US3] [frontend] Implement the artwork actions on the track detail page to pass T048
- [x] T050 [US3] [qa] Author `artwork.spec.ts` "own picture": upload a generated PNG, check `data-artwork-source="user"` in the library, reset, invalid file error

**Checkpoint**: HO:us3.

---

## Phase 6: Polish (inside the wave, per owner)

- [x] T051 [P] [audio-ingest] Refactor to SIG limits, docstrings, `@pytest.mark.integration` live-service smoke `worker/tests/metadata/test_live_services.py` (one request per service, reference songs; deselected by default), HO:polish with coverage of `metadata`, `ingest`
- [x] T052 [P] [stem-separation] Refactor, docstrings, HO:polish with coverage of `jobs`, `api`
- [x] T053 [P] [backend] Refactor, XML docs, HO:polish with Domain/Persistence/Api coverage
- [x] T054 [P] [frontend] Accessibility (alt texts, focus order in the panel, contrast in both 004 themes, keyboard access to `band-info-open`), help topic review, HO:polish
- [x] T055 [P] [qa] Traceability matrix update, HO:polish
- [x] T056 [P] [devops] Validate compose/CI changes (`docker compose config -q`, `actionlint`), HO:polish
- [ ] T057 [P] [quality-gate] `run_all.py` on the whole repo; refactor requests per owner; HO:polish

---

## Integration wave (CTO, after all wave hand-offs are accepted)

- [ ] T058 [CTO] Merge the owner branches; full build, lint, unit + coverage gates green
- [ ] T059 [CTO] Playwright default suite + `artwork`/`band-info` specs on the fake stack (retries 0)
- [ ] T060 [CTO] `docs/THIRD-PARTY-LICENSES.md`: tinytag 2.3.2 (MIT); Pillow 13.0.0 (MIT-CMU), httpx 0.28.1 (BSD-3), rapidfuzz 3.14.6 (MIT) as direct dependencies; new section "Online data services" (MusicBrainz CC0 core data, Cover Art Archive no stated image licence, Wikidata CC0, Wikipedia CC BY-SA 4.0 with attribution) (FR-014)
- [ ] T061 [CTO] README: online lookups (what is sent, User-Agent, `STEMSTUDIO_CONTACT_URL`, how to switch off), personal-practice notice incl. pictures and band texts
- [ ] T062 [CTO] Real-service validation on the reference machine (quickstart A4, A5, A7, A8, A10; `pytest -m integration tests/metadata/test_live_services.py`); record in `docs/STATUS.md`
- [ ] T063 [CTO] `/speckit-analyze` consistency pass; update `docs/STATUS.md`; PR
- [ ] T063a [CTO] Update docs/public manuals and regenerate PDFs (constitution 1.3.1, last step of the integration wave, after `docs/STATUS.md` is updated): edit the chapters of this feature in `docs/public/src/user-manual/` and/or `docs/public/src/technical-manual/` (remove the *In development* marker, describe the delivered behaviour, setup, env vars, contracts and limits; add screenshots from the visual baselines where useful), run `cd tools/docs && npm ci && npm run browser && npm run build`, commit the sources, both PDFs and `docs/public/build-manifest.json` together; `node tools/docs/check.mjs` passes

---

## Execution plan per owner (parallel wave)

All owners start after Phase 0 from the Phase 0 commit, each in its own worktree. Order inside a
sequence is binding.

| Owner | Ordered task sequence | Owned paths (write) | May mock / fake |
|---|---|---|---|
| audio-ingest | T010 -> T011 -> T012 -> T013 -> HO:foundational -> T022 -> T023 -> HO:us1 -> T031 -> T032 -> T033 -> T034 -> HO:us2 -> T051 (HO:polish) | `worker/src/stemstudio_worker/{metadata,ingest}/**`, `worker/tests/{metadata,ingest}/**` | `httpx.MockTransport` with recorded responses, fake clock/sleeper, generated images, stub spotDL `Song`, yt-dlp info fixtures |
| stem-separation | T014 -> T015 -> HO:foundational -> T024 -> T025 -> HO:us1 -> T035 -> T036 -> HO:us2 -> T044 -> T045 -> HO:us3 -> T052 (HO:polish) | `worker/src/stemstudio_worker/{jobs,api}/**`, `main.py`, `worker/tests/{jobs,api}/**` | fake normaliser, fake lookup, fake clock |
| backend | T016 -> T017 -> T018 -> T019 -> T020 -> T021 -> HO:foundational -> T026 -> T027 -> HO:us1 -> T037 -> T038 -> T039 -> T040 -> HO:us2 -> T046 -> T047 -> HO:us3 -> T053 (HO:polish) | `src/StemStudio.{Domain,Persistence,Api}/**`, their tests | stub `HttpMessageHandler`, `FakeTimeProvider`, SQLite, temp data dir |
| frontend | T028 -> T029 -> HO:us1 -> T041 -> T042 -> HO:us2 -> T048 -> T049 -> HO:us3 -> T054 (HO:polish) | `src/StemStudio.Web/**`, `tests/StemStudio.Web.Tests/**` | fake `IStemStudioApiClient`, bUnit |
| qa | T009 -> T030 -> HO:us1 -> T043 -> HO:us2 -> T050 -> HO:us3 -> T055 (HO:polish) | `e2e/**`, `docs/testing/**`, `tests/fixtures/**` | fake stack (`STEMSTUDIO_FAKE_ONLINE`) |
| devops | T008 -> HO:foundational -> T056 (HO:polish) | `docker/**`, `docker-compose*.yml`, `.github/**`, `scripts/**` | skeleton |
| quality-gate | T006 -> T007 -> HO:foundational -> T057 (HO:polish) | `tools/quality/**`, `docs/quality/**` | sample files |
| architect | T001 -> T002 -> T003 -> T004 -> T005 (before the wave; later contract fixes on request) | `specs/**`, ADRs, skeleton files | n/a |
| CTO | T058 -> T059 -> T060 -> T061 -> T062 -> T063 | `README.md`, `docs/THIRD-PARTY-LICENSES.md`, `docs/STATUS.md`, merges | n/a |

Task count per owner: architect 5, audio-ingest 11, stem-separation 9, backend 15, frontend 7,
qa 5, devops 2, quality-gate 3, CTO 6 (total 63: T001-T063).

### Flagged cross-owner tasks (converted)

| Task | Would have needed | Converted to |
|---|---|---|
| T001 worker contract test switch | stem-separation test files | one-time edit by the architect, recorded in the hand-off |
| T003 backend contract staging | backend test files | operationIds staged by the architect; un-staged by the backend in T026/T039/T046 |
| T009 fixtures with Wikipedia text | committing CC BY-SA text | placeholder text; real text only in the integration test at run time |
| T060/T061 licences and README | CTO-owned files | consolidated by the CTO (AGENT-PROTOCOL rule 9) |

## Dependencies & Execution Order

Phase 0 -> wave (Setup + Foundational + stories per owner, parallel across owners) -> Integration
wave. Stories are independent at the contract level (fakes). US1 is the MVP of this feature and
can ship alone.

## Definition of Done checklist (mirrors the constitution)

- [ ] Red-green-refactor followed; failing test commit before implementation commit (I)
- [ ] All tests green; >= 90 % line AND branch per component, exact numbers in the hand-off (I)
- [ ] Online clients behind ports with recorded responses; no unit test touches the network (I)
- [ ] SIG limits respected; `run_all.py` passes incl. the `metadata` dependency rule (II)
- [ ] Implementations match the 005 worker, backend and UI-id contracts; contract tests pass (III)
- [ ] New dependencies OSI-licensed and recorded; Wikipedia attribution complete; pictures and band texts never exported; personal-practice notice extended (IV)
- [ ] No personal data in requests (test over recorded requests); lookups can be switched off; failures shown with reasons; nothing guessed (V)
- [ ] No secrets; analyzers/formatters/linters clean; public APIs documented
- [ ] Help topics `artwork`, `band-info`, `online-lookups` present (004 framework)
- [ ] docs/public user and/or technical manual updated for this feature and the PDFs regenerated; `node tools/docs/check.mjs` passes (constitution 1.3.1)
- [ ] Hand-off notes written; Conventional Commits with the Co-Authored-By trailer
