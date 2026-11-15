# Contract delta: feature 004 against 003

Final version numbers are confirmed when the feature starts (Phase 0, T003); the numbers below
are the proposal.

| Contract | 003 version | 004 | Change |
|---|---|---|---|
| Backend API (`specs/003-gpu-acceleration/contracts/backend-api.openapi.yaml`) | 1.3.0 | **1.3.0, unchanged** | none (see "Why no preferences endpoint") |
| Worker API (`specs/003-gpu-acceleration/contracts/worker-api.openapi.yaml`) | 1.2.0 | **1.2.0, unchanged** | none |
| Audio engine (`specs/002-detail-separation/contracts/audio-engine.md`) | 2.0.0 | **2.0.0, unchanged** | none; new meter styles read the existing `--meter-level`, `data-peak-db`, `data-clip` |
| UI test ids ([ui-test-ids.md](ui-test-ids.md)) | 1.2.0 | **1.3.0** (additive) | adopted ids, new ids, level rules R7-R9; no 1.2.0 id renamed or removed |
| Help content ([help-content.md](help-content.md), schemas) | - | **1.0.0** (new) | topic and tour formats, automated checks |
| Web interop ([web-interop.md](web-interop.md)) | - | **1.0.0** (new) | `first-paint.js` and the `web-interop.js` module surface |

## Why no preferences endpoint

Preferences (level, theme, density, motion, help progress) are stored in the browser's local
storage (ADR-013 D4, research.md R-05): the theme must be applied before the first paint, which
only a synchronous read of local storage can do; StemStudio has a single local user; and each
Playwright test controls its preferences through `storageState`. A server endpoint would add a
second source of truth, a migration and a contract bump without user benefit today.

If a later feature needs the level on the server (for example the agent of feature 011 adapting
its explanations), the preferred option is that the UI sends the level with the request of that
feature. Should a stored server copy become necessary, the endpoint would be an additive backend
change; proposed shape, **not part of 004**:

```yaml
# backend-api 1.x (future, additive) - NOT implemented in 004
/api/preferences:
  get:  { operationId: getPreferences, responses: { "200": { $ref: UserPreferences } } }
  put:  { operationId: putPreferences, requestBody: UserPreferences, responses: { "200": UserPreferences, "400": ValidationProblem } }
# UserPreferences = data-model.md 1.1 without help progress
```

## Compatibility rules for 004 implementers

- The backend, worker and audio engine are not changed in 004; any need that appears during the
  wave is reported to the architect (AGENT-PROTOCOL rule 2), not implemented.
- Contract tests keep using the 003 OpenAPI documents (CI `CONTRACTS_DIR` stays
  `specs/003-gpu-acceleration/contracts`); only the UI-test-id document and the new 004 contracts
  move to `specs/004-design-system-ux/contracts`.
