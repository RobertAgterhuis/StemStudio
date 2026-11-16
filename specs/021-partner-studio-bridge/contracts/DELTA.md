# Contract delta: feature 021 (practice partner and studio bridge)

Feature 021 bumps exactly the contracts allocated to it in `docs/ROADMAP.md`: backend-api
**1.20.0**, worker-api **1.12.0**, audio-engine **2.9.0**, ui-test-ids **1.20.0**, generator-api
**1.2.0**. Every change is additive; no existing operation, schema, id or behaviour changes.
Features 012-020 are specified in parallel and are not merged on this branch, so the 021 documents
are **standalone deltas** (as 011 did); Phase 0 (tasks T001-T003) merges them into the full
documents on top of 1.19.0 / 1.11.0 / 2.8.0 / 1.19.0 / 1.1.0.

## Status (design, 2026-11-16)

| Contract | Base | 021 document | State |
|---|---|---|---|
| Backend API | 1.19.0 (020) | [backend-api.delta.openapi.yaml](backend-api.delta.openapi.yaml) `1.20.0-delta.1`, Redocly-valid OpenAPI 3.1 (0 errors; 5 documented warnings) | binding for the wave; merged unchanged at T001 |
| Worker API | 1.11.0 (020) | [worker-api.delta.openapi.yaml](worker-api.delta.openapi.yaml) `1.12.0-delta.1`, Redocly-valid (0 errors, 0 warnings) | binding; merged at T001 |
| Generator API | 1.1.0 (010) | [generator-api.delta.openapi.yaml](generator-api.delta.openapi.yaml) `1.2.0-delta.1`, Redocly-valid (0 errors, 0 warnings) | binding; merged at T001 |
| Audio engine | 2.8.0 (019) | [audio-engine.delta.md](audio-engine.delta.md) 2.9.0 | binding; `types.ts` at T006 |
| UI test ids | 1.19.0 (020) | [ui-test-ids.delta.md](ui-test-ids.delta.md) 1.20.0 | names binding; merged at T003 |
| DAWproject mapping | - | [dawproject-mapping.md](dawproject-mapping.md) 1.0.0 | binding (backend, qa golden files) |
| Amp catalogue | - | [amp-catalogue.schema.json](amp-catalogue.schema.json) v1 (JSON Schema 2020-12) | binding (catalogue file, backend loader) |
| render-vectors | 1.1.0 (019) | unchanged (parts are ordinary gain/pan inputs) | - |
| help-content | 1.0.0 | unchanged (021 topics use existing areas: mixer, mixes, tracks, glossary) | - |
| agent tool catalogue | (011 + later minors) | 3 entries requested for the next minor (FR-031) | request to the 011 owner |

Lint command: see [redocly.yaml](redocly.yaml).

## 1. Backend API 1.20.0 (summary; the OpenAPI document wins)

| Method + path | operationId | Notes |
|---|---|---|
| `GET /api/tracks/{trackId}/partner/context` | `getPartnerContext` | families, tempo, key, sections, availability, base-model download size |
| `POST /api/tracks/{trackId}/partner-parts` | `createPartnerGeneration` | `202 Generation(kind partner)`; `400 imitation`; `409 not-installed / gpu-required / track-not-ready / too-many-parts`; `422` |
| `POST /api/generations/{generationId}/takes/{index}/parts` | `addPartFromTake` | `202 Part(conforming)`; `409 generation-not-completed / not-a-partner-generation / part-exists / too-many-parts` |
| `GET /api/tracks/{trackId}/parts` | `listParts` | |
| `GET/PATCH/DELETE /api/tracks/{trackId}/parts/{partId}` | `getPart`, `renamePart`, `deletePart` | delete also removes the channel from every mix |
| `POST /api/tracks/{trackId}/parts/{partId}/retry` | `retryPart` | |
| `GET /api/tracks/{trackId}/parts/{partId}/audio` | `getPartAudio` | Range/ETag as stems |
| `GET /api/amp/catalogue` | `listAmpCatalogue` | |
| `POST /api/amp/catalogue/{entryId}/install` | `installCatalogueEntry` | `201`; `409 already-installed`; `422 checksum-mismatch / invalid-nam`; `502 catalogue-download-failed` |
| `GET/POST /api/amp/profiles` | `listAmpProfiles`, `uploadAmpProfile` | multipart `.nam` <= 16 MiB; `413`; `422 invalid-nam / unsupported-architecture` |
| `GET/PATCH/DELETE /api/amp/profiles/{profileId}`, `GET .../file` | `getAmpProfile`, `updateAmpProfile`, `deleteAmpProfile`, `getAmpProfileFile` | |
| `GET/POST /api/amp/irs`, `GET/PATCH/DELETE /api/amp/irs/{irId}`, `GET .../file` | `listCabinetIrs`, `uploadCabinetIr`, `getCabinetIr`, `renameCabinetIr`, `deleteCabinetIr`, `getCabinetIrFile` | WAV <= 2 s, <= 5 MiB |
| `GET/POST /api/amp/rigs`, `GET/PUT/DELETE /api/amp/rigs/{rigId}` | `listRigPresets`, `createRigPreset`, `getRigPreset`, `updateRigPreset`, `deleteRigPreset` | seeded presets `preset:<slug>` read-only (`409 preset-read-only`) |
| `PUT/GET/DELETE /api/tracks/{trackId}/takes/{takeId}/wet` | `putTakeWet`, `getTakeWetAudio`, `deleteTakeWet` | multipart file + `rig` |
| `PATCH /api/tracks/{trackId}/takes/{takeId}/playback` | `setTakePlayback` | `{playWet}` |
| `POST /api/assets/daw-projects` | `createDawProjectExport` | `202 Asset(kind dawProject)`; `409 insufficient-disk-space / track-not-ready / arrangement-mismatch / nothing-to-export` |

Enum and member additions at the merge: `GenerationKind.partner`, `Generation.partner`,
`GenerationTake.fit` + `partId`, `AssetKind.dawProject`, `AssetOrigin.dawExport`,
`AssetFileRole.dawproject` + `report`, `AssetFile.contentType` + `application/vnd.dawproject+zip`
+ `application/json`, `Provenance.dawExport`, `ChannelId` + `part-[0-9a-f]{8}`, 015 `Take.wet` +
`playWet`. `MixSettings` channel maps accept part channel ids (pattern only).

**015 path assumption**: the take operations assume 015's resource
`/api/tracks/{trackId}/takes/{takeId}`. If 015 chose another path, T001 moves the two 021
operations under 015's take resource and keeps their operationIds. Same for the take channel id
pattern (`take-[0-9a-f]{8}` assumed in `ChannelId`).

## 2. Worker API 1.12.0

`POST /v1/conform-jobs` (`createConformJob`), `GET/DELETE /v1/conform-jobs/{conformJobId}`,
`GET /v1/conform-jobs/{conformJobId}/file`; render lane; allow-list pattern
`tracks/<uuid>/parts/<uuid>.flac` for conform and render inputs. Takes are read through their 007
asset paths (existing pattern).

## 3. Generator API 1.2.0

`createGeneration` accepts `kind: partner` (multipart `params` + 1-6 `context`); `PartnerParams`;
`ModelId` + `acestep-v15-base`; `GeneratedTake.track`; `/health.kinds` may list `partner`
(acestep variant). Fake engine behaviour and the `quiet-part` fake mode are part of the contract
(E2E relies on them).

## 4. Audio engine 2.9.0

`addPart`, `removePart`, `loadNamRuntime`, `loadAmpProfile`, `loadCabinetIr`, `setRig`,
`setRigBypass`, `clearRig`, `getRigStatus`, `setTakeReamp`, callback `OnRigStatus`, recorder
option `captureWet` with `wet` result. See [audio-engine.delta.md](audio-engine.delta.md).

## 5. Runtime / compose (devops, not a contract document)

- Web app CSP: `script-src` must contain `'wasm-unsafe-eval'` (shared with 012's SoundTouch WASM).
- `wwwroot/js/nam-worklet.js` and `nam-engine.wasm` are build outputs of the engine (`npm run build`).
- API configuration: `StemStudio__Amp__CatalogueAllowedHosts` (defaults from the catalogue file),
  `StemStudio__Partner__IncludeChordsInCaption` (default true).
- E2E fake stack: fake generator (`STEMSTUDIO_GENERATOR=fake`, `STEMSTUDIO_FAKE_PART_OFFSET_MS=37`),
  a static fake catalogue host container serving `e2e/fixtures/amp/` with a test catalogue
  (`StemStudio__Amp__CatalogueFile=/fixtures/catalogue.e2e.json`), Chromium fake audio input
  (`--use-fake-device-for-media-stream --use-file-for-fake-audio-capture=<guitar-di fixture>`).
- CI: the NAM WASM rebuild job (`emscripten/emsdk` pinned image) runs on changes under
  `tools/nam-wasm/**` and weekly; it never blocks unrelated pushes.
