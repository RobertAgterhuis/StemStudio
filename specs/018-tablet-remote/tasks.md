---

description: "Task list for feature 018 Tablet and remote (Phase 0 by the architect at feature start, one parallel wave of owner agents, then a CTO integration wave)"
---

# Tasks: Tablet and Remote

**Input**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [quickstart.md](quickstart.md), [contracts/](contracts/)
(DELTA.md, remote-protocol.md, OpenAPI, schema, ui-test-ids delta), constitution 1.3.1, ADR-027.

**Tests**: REQUIRED (constitution I). Within every owner sequence the test task comes before the
implementation task it covers (red -> green -> refactor); every owner reports >= 90 % line AND
branch coverage of its component in its hand-off. No task needs a real LAN, a real certificate
authority or a real tablet; those are covered in the integration wave (T118-T120).

**Organization**: grouped by user story (US1..US6); the
[execution plan](#execution-plan-per-owner-parallel-wave) regroups the IDs per agent.

## Format: `[ID] [P?] [Story?] [owner] Description (FR/SC/SV refs)`

- **[P]**: parallel with the other [P] tasks of the same phase (different files, no dependency on
  an incomplete task). Tasks of different owners are always parallel in the wave.
- **[owner]**: an agent from `.claude/agents/`. Each owner writes only inside its owned paths
  (plan.md ownership map) plus `docs/handoffs/<owner>-018-<checkpoint>.md`.
- **[sec]**: security-relevant; the test must name the SV id it covers (plan.md "Security
  verification"); the quality-gate agent reviews these tests in its polish checkpoint.
- Paths are relative to the repository root. **CTO** = orchestrator.
- audio-engine, worker agents, generative and ai-agent have no tasks in this feature.

## Checkpoint hand-offs (binding, ADR-008)

Each owner writes `docs/handoffs/<owner>-018-<checkpoint>.md` at `foundational`, every story it
takes part in (`us1`..`us6`) and `polish`, with red -> green commit hashes per task pair, exact
coverage numbers and lint/SIG output. Marker `HO:<checkpoint>` in the execution plan.

## Cross-owner rule (binding)

| Consumer | Needs | Contract | Replaced in tests by |
|---|---|---|---|
| frontend (web) | `/api/remote/*` | backend-api 1.17.0 + `StemStudio.Contracts/Remote` DTOs (T005) | fake `IStemStudioApiClient` remote methods |
| frontend (remote client) | remote surface | remote-protocol 1.0.0 (+ `src/remote-client/src/types.ts`, T006) | fake WebSocket + recorded example messages (`contracts/remote-examples`) |
| frontend (mixer bridge) | 014 command catalogue, 012 tempo, 016 chords | DELTA.md 8 (reconciled in T002) | substitutes of the mixer handlers |
| qa | both UIs | ui-test-ids 1.17.0, remote-protocol 1.0.0 | fake stack (`STEMSTUDIO_SEPARATOR=fake`) + `docker-compose.lan.yml` in `lan-http` on 127.0.0.1 |
| devops | env vars, compose, CI | DELTA.md 7 | - |

---

## Phase 0: Contracts and skeleton (architect, at feature start, BEFORE the wave)

- [ ] T001 [architect] Merge contracts on the versions current at start: `contracts/backend-api.openapi.yaml` **1.17.0** = 017's 1.16.0 + `backend-api.remote.openapi.yaml` unchanged; `contracts/ui-test-ids.md` **1.17.0** = 1.16.0 + `ui-test-ids.delta.md`; Redocly lint of the full document + `remote-lan.openapi.yaml` valid; update `redocly.yaml` and CI `CONTRACTS_DIR` request to devops
- [ ] T002 [architect] Reconcile DELTA.md 8 with the delivered 012/014/016 contracts: section, setlist, score-map, chord and sheet-asset shapes; fill the "014 command id" column of remote-protocol.md 4 (names of remote commands unchanged); record in research.md section 1 (F-09..)
- [ ] T003 [architect] Verification spike on the reference machine (with the CTO; research R-03/R-04/K-01): Docker Desktop publishes `<LAN-IP>:8443` reachable from a second device and `127.0.0.1:8080` unreachable from it; firewall prompt scope; the playwright container (`--network host`) still reaches `127.0.0.1`-bound ports (else document `STEMSTUDIO_BIND_ADDRESS=0.0.0.0` for local E2E only); an mkcert-issued certificate for an IP SAN is trusted by iPadOS Safari and Android Chrome after root install; record results in research.md (F-10..)
- [ ] T004 [P] [architect] Contract checks: extend `specs/018-tablet-remote/contracts/check_examples.py` (exists since the spec: jsonschema Draft 2020-12 with format checks; 20 valid + 8 invalid examples pass) with examples for every one of the 24 commands and every `ack` reason; hand the script to devops for CI (DELTA.md 7)
- [ ] T005 [P] [architect] Skeleton .NET: `src/StemStudio.Contracts/Remote/*.cs` DTOs and enums of DELTA.md 2 with XML docs, `ApiRoutes.Remote*`; contract tests `tests/StemStudio.Api.Tests/Contract/RemoteContractTests.cs` (DTO <-> schema, routes, kebab-case enum names); `IWebInterop` signature additions of DELTA.md 4 with no-op test doubles; `src/web-interop/src/types.ts` additions
- [ ] T006 [P] [architect] Skeleton TS: `src/remote-client/` package (package.json with the web-interop toolchain pins + `pdfjs-dist` 6.5.438, tsconfig, vite config emitting to `src/StemStudio.Web/wwwroot/remote/`, vitest config with >= 90 % thresholds, eslint config), `src/types.ts` (protocol types of remote-messages.schema.json, TSDoc), a placeholder `index.html` without inline script, one skeleton test; `Directory.Packages.props` `QRCoder` 1.8.1
- [ ] T007 [architect] Verify the skeleton: `dotnet build` 0 warnings, all tests + coverage gates green, `npm test` in `src/remote-client` and `src/web-interop`, `tools/quality/run_all.py` 0 errors; hand-off `docs/handoffs/architect-018-phase0.md`

**Checkpoint**: skeleton green, contracts merged; the wave may start.

---

## Phase 1: Setup (per owner, no story)

- [ ] T008 [P] [quality-gate] Failing tests in `tools/quality/tests/`: `src/remote-client` is included in TS unit-size/complexity and duplication checks; `StemStudio.Web/Remote` must not reference `StemStudio.Web/Components` except through `IRemotePlayerBridge`/`INavigatorBridge`; `src/remote-client` must not import `src/web-interop` or `src/audio-engine`
- [ ] T009 [quality-gate] Implement the rules in `tools/quality/{components,dependency_check,duplication}.py`; Sonar sources/exclusions for `src/remote-client`; re-run `run_all.py`; update `docs/quality/SIG-REPORT.md`
- [ ] T010 [P] [sec] [devops] Failing check `scripts/compose_binding_check.py` (+ CI step): `docker compose config` of the base file has 8080/8081 on `127.0.0.1`; `-f docker-compose.lan.yml` without `STEMSTUDIO_LAN_ADDRESS` fails; with it, only the remote port is added and only on that address; no port uses `0.0.0.0` (SV-05)
- [ ] T011 [devops] `docker-compose.yml` bind address + `AllowedHosts` env + header comments; new `docker-compose.lan.yml` (DELTA.md 7); `.gitignore` certificate patterns; `scripts/lan-check.{ps1,sh}`; `scripts/dev.{ps1,sh}` remote variables for the non-Docker path (FR-010, FR-011)
- [ ] T012 [devops] CI: build/test/coverage gate for `src/remote-client`; Redocly lint of the 018 OpenAPI files; schema example check (T004 script); compose binding check (T010); E2E job "remote" (`docker-compose.lan.yml`, `STEMSTUDIO_LAN_ADDRESS=127.0.0.1`, `lan-http`, port 8090)
- [ ] T013 [devops] CI job "remote-https-smoke": generate a throwaway CA + leaf for `127.0.0.1` with `openssl` inside the job (never committed, deleted at the end), start `lan-https`, run the qa smoke project (T066); fail when any certificate file is staged in git
- [ ] T014 [P] [qa] Update `docs/testing/TEST-STRATEGY.md`: two-context E2E pattern, insecure-context emulation (`--host-resolver-rules`), security suite SV-01..SV-22 traceability, latency probe, manual device checks; traceability US1..US6 -> test files
- [ ] T015 [qa] `e2e/support/remote-context.ts` (second context on the remote origin, pairing helper that drives both contexts, `RemotePage` page object with `rc-*` ids) and `e2e/pages/stand.ts`; Playwright projects `remote` (lan-http) and `remote-https-smoke`

---

## Phase 2: Foundational (blocking inside each owner)

### backend (tests first)

- [ ] T016 [P] [sec] [backend] Failing tests `tests/StemStudio.Domain.Tests/Remote/`: `PairingCode` (6 digits from an injected RNG, zero-padded, equality in constant time), `CheckNumber` (data-model 4 formula, 3 digits), `DeviceName` (1-40, allowed Unicode categories, trims, rejects control/format chars and `<`-only names are allowed as text), `DeviceKey` (32 bytes, base64url 43 chars, SHA-256 hash, constant-time compare), `PollSecret` (16 bytes, 22 chars)
- [ ] T017 [backend] Implement `src/StemStudio.Domain/Remote/{PairingCode,CheckNumber,DeviceName,DeviceKey,PollSecret}.cs` to pass T016
- [ ] T018 [P] [backend] Failing tests `tests/StemStudio.Persistence.Tests/Remote/`: migration `AddRemoteControl` creates `RemoteSettings`, `RemoteDevices` (unique `KeyHash`), `RemoteAuditEvents`; stores: settings default when missing, device CRUD, find by key hash, 8-device count, idle-expiry query, audit append with 500-row retention and `auth-refused` coalescing per minute
- [ ] T019 [backend] Implement `src/StemStudio.Persistence/Remote/*` (entities, configurations, migration, `RemoteSettingsStore`, `RemoteDeviceStore`, `RemoteAuditStore`) to pass T018
- [ ] T020 [P] [sec] [backend] Failing tests `tests/StemStudio.Api.Tests/Remote/PairingRegistryTests.cs` (fake `TimeProvider`): every transition of data-model 4 (open -> requested -> allowed -> delivered; reject; 5 wrong -> stopped; timeout; new window cancels old; 60 s uncollected key removes the device; second redeem refused; poll with wrong secret 404; key wiped after delivery) (SV-09, SV-12)
- [ ] T021 [backend] Implement `src/StemStudio.Api/Remote/PairingRegistry.cs` (+ small state classes) to pass T020
- [ ] T022 [P] [sec] [backend] Failing tests `PairingRateLimiterTests.cs`: 20 per rolling minute, `Retry-After` seconds, recovery (SV-10)
- [ ] T023 [backend] Implement `PairingRateLimiter.cs` to pass T022
- [ ] T024 [P] [sec] [backend] Failing tests `RemoteRequestRulesTests.cs`: endpoint filter on the `remote` group -> non-JSON body 415, foreign `Origin` 403 `origin-refused`, no `Origin` allowed, `WebOrigin` allowed (SV-14)
- [ ] T025 [backend] Implement `RemoteRequestRules.cs` (endpoint filter) to pass T024
- [ ] T026 [P] [sec] [backend] Failing tests `HostFilteringTests.cs`: API answers 400 for `Host: evil.example`, 200 for `localhost`, `127.0.0.1`, `[::1]`, `api` (SV-04)
- [ ] T027 [backend] Configure `AllowedHosts` default and host filtering in `ApiComposition` + `appsettings.json` to pass T026

### frontend (tests first)

- [ ] T028 [P] [sec] [frontend] Failing tests `tests/StemStudio.Web.Tests/Remote/RemoteOptionsTests.cs`: data-model 1.1 validation table (modes, ports, private/loopback/link-local addresses, public addresses refused, host-name suffix rules, alias limit), `RemoteDeployment` problems (SV-21)
- [ ] T029 [frontend] Implement `Remote/{RemoteOptions,RemoteDeployment,AddressRules}.cs` to pass T028
- [ ] T030 [P] [sec] [frontend] Failing tests `RemoteGateTests.cs` (TestServer, classifier override): remote port -> path allow-list (SV-02 list -> 404 before routing), host check 400, origin check 403 for POST/upgrade, security headers and CSP on every response (SV-18), main port -> `/remote/v1/*` 404 (SV-03), non-private peer refused outside containers; request log contains path templates only (SV-13)
- [ ] T031 [frontend] Implement `Remote/{ListenerClassifier,RemoteGate,RemoteSecurityHeaders,RemoteEndpointFilter}.cs` and register the gate first in `Program.cs` (explicit `UseRouting` after it); main `AllowedHosts` default to pass T030
- [ ] T032 [P] [sec] [frontend] Failing tests `RemoteListenerTests.cs`: mode off -> one endpoint (SV-01); `lan-http` -> two endpoints; `lan-https` with valid / expired / host-mismatched / missing / encrypted-with-wrong-password PEM (test certificates generated in the test with `CertificateRequest`) -> endpoint or `problem` without any HTTP fallback (SV-20)
- [ ] T033 [frontend] Implement `Remote/RemoteListener.cs` (Kestrel endpoint configuration from options, `X509Certificate2.CreateFromPemFile`/`CreateFromEncryptedPemFile`, expiry warning) to pass T032
- [ ] T034 [P] [frontend] Failing tests `RemoteFamiliesTests.cs`: every stem and detail-stem name of the backend catalogue maps to the remote-protocol 6 family; order vocals, guitar, bass, keys, drums, other; unknown -> other
- [ ] T035 [frontend] Implement `Remote/RemoteFamilies.cs` to pass T034
- [ ] T036 [P] [frontend] Failing Vitest `src/remote-client/tests/{store,clock,backoff}.test.ts`: storage record of data-model 8 (validation, blocked storage -> memory only), server-clock offset (median of 5 pongs), display position interpolation incl. loop wrap and duration clamp, backoff 1-2-4-8-10 reset on welcome
- [ ] T037 [frontend] Implement `src/remote-client/src/{store,clock,backoff}.ts` to pass T036
- [ ] T038 [P] [frontend] Failing Vitest `connection.test.ts` with a fake WebSocket: subprotocol, `hello` first, `welcome`/`state` handling, every close code of remote-protocol 3.3 -> client state and storage effect, no command replay after reconnect, oversize guard on send
- [ ] T039 [frontend] Implement `src/remote-client/src/connection.ts` to pass T038

**Checkpoint HO:foundational** (backend, frontend).

---

## Phase 3: User Story 1 - Music-stand layout on the computer (P1) MVP

- [ ] T040 [P] [US1] [frontend] Failing bUnit tests `Components/Mixer/Stand/StandViewTests.cs`: toggle keeps engine and mixer state; transport buttons and their commands go through the existing handlers; part picker lists exactly the families with content in fixed order; content tabs per kind; current lyric/chord/section/page follow a fake position; text size (Advanced+); `remote-other-tab` notice; ids of ui-test-ids 1.17.0 (FR-001..FR-004)
- [ ] T041 [US1] [frontend] Implement `Components/Mixer/Stand/{StandView,StandTransport,StandContent,PartPicker}.razor(.cs)` + `css/components/stand.css` (004 tokens, >= 64 px transport, landscape/portrait grid, 320 px reflow) to pass T040
- [ ] T042 [P] [US1] [frontend] Failing Vitest `src/web-interop/tests/wakelock.test.ts` + `orientation.test.ts`: acquire, unsupported, refused, release, re-acquire on `visibilitychange`, callback states; orientation events
- [ ] T043 [US1] [frontend] Implement `src/web-interop/src/{wakelock,orientation}.ts` + C# `WebInterop` methods to pass T042
- [ ] T044 [P] [US1] [frontend] Failing bUnit tests `WakeLockSwitchTests.cs`: shows `on`/`off`/`unsupported`, releases on exit and dispose, remembers the preference (FR-005)
- [ ] T045 [US1] [frontend] Implement `Components/Mixer/Stand/WakeLockSwitch.razor` + stand preferences fields (data-model 8) to pass T044
- [ ] T046 [P] [US1] [frontend] Help: `topics/mixer/stand-view.md` + anchors; feature catalogue `stand.text-size`; failing help-content tests first (markers, anchors, links) then the topic
- [ ] T047 [P] [US1] [qa] E2E `e2e/tests/stand-view.spec.ts`: toggle, play/next section/loop, one case per family (vocals lyrics, guitar chords, bass score, keys score, drums drum score, other sections only) (SC-007), wake-lock state in Chromium (SC-008), sizes 1280x800/1024x768/768x1024/390x844 and 320 px no horizontal scroll
- [ ] T048 [P] [US1] [qa] a11y `e2e/a11y/stand.spec.ts` (axe 0 serious/critical, keyboard path, focus visible, reduced motion) and visual baselines of the stand layout (SC-006)

**Checkpoint HO:us1** (frontend, qa).

---

## Phase 4: User Story 2 - Pair a phone or tablet safely (P1)

### backend

- [ ] T049 [P] [US2] [sec] [backend] Failing endpoint tests `tests/StemStudio.Api.Tests/Remote/RemoteSettingsEndpointsTests.cs`: GET defaults, PUT audit `switch-changed`, PUT off cancels the open window
- [ ] T050 [US2] [backend] Implement `Api/Remote/RemoteEndpoints.Settings.cs` + `RemoteSettingsService` to pass T049
- [ ] T051 [P] [US2] [sec] [backend] Failing endpoint tests `RemotePairingEndpointsTests.cs`: start (201, switch off 409, 8 devices 409), current (code only while open, 404 30 s after final), cancel idempotent, allow/reject (409 states), redeem (202, wrong/unknown/used/expired byte-identical 404 - SV-11, 422 name, 429), poll (once, 404 after), every audit event of data-model 5 without secrets (SV-13)
- [ ] T052 [US2] [backend] Implement `RemoteEndpoints.Pairing.cs` + `RemotePairingService` to pass T051
- [ ] T053 [P] [US2] [sec] [backend] Failing endpoint tests `RemoteDeviceEndpointsTests.cs`: list (newest first, limit 8), rename (422), remove/remove all (audit), authenticate (200 updates last use at most once per minute, 401 unknown, 30-day idle -> 401 + deleted + audit, 409 when off), daily expiry sweep (hosted service, fake time), audit list `limit`
- [ ] T054 [US2] [backend] Implement `RemoteEndpoints.Devices.cs`, `RemoteDeviceService`, `RemoteAuditService`, `RemoteExpirySweep` to pass T053
- [ ] T055 [US2] [backend] Contract conformance: OpenAPI document of the API contains the 15 operations with the contract's operationIds and schemas (extend the existing contract test harness); log-capture test over all remote endpoints: no key, code, secret or check number in any log line (SV-13)

### frontend

- [ ] T056 [P] [US2] [frontend] Failing tests `Api/StemStudioApiClientRemoteTests.cs`: one method per operation, problem mapping of the new codes
- [ ] T057 [US2] [frontend] Implement `IStemStudioApiClient.Remote.cs` / `StemStudioApiClient.Remote.cs` to pass T056
- [ ] T058 [P] [US2] [sec] [frontend] Failing tests `Remote/PairingProxyEndpointsTests.cs` + `DeviceAuthenticatorTests.cs`: `info` shape (no device data), redeem/poll forwarding with transport, `X-Pairing-Secret` header only, `no-store`, switch off -> 403 `remote-disabled`; authenticator cache 60 s, eviction on removal, API 401/409 mapping
- [ ] T059 [US2] [frontend] Implement `Remote/{PairingProxyEndpoints,RemoteInfoEndpoint,DeviceAuthenticator}.cs` to pass T058
- [ ] T060 [P] [US2] [frontend] Failing bUnit tests `Components/Remote/RemoteControlPageTests.cs`: mode/problem/address/certificate/insecure warning per deployment; switch disabled when off; pairing dialog (code, QR SVG via QRCoder with the fragment URL, countdown, request card with check number, allow/reject/cancel, stopped message); device list with connected state, rename (Advanced), remove + confirm, remove all; audit table (Expert only); level visibility; device names rendered as text (SV-19)
- [ ] T061 [US2] [frontend] Implement `Components/Remote/{RemoteControlPage,PairingDialog,PairingQr,DeviceList,AuditLog}.razor(.cs)`, nav link, feature catalogue entries `remote.*` to pass T060; switch-off and removal call `RemoteHub.Disconnect` (stubbed until T071)
- [ ] T062 [P] [US2] [frontend] Failing Vitest `src/remote-client/tests/pairing.test.ts` + `ui/pairing-view.test.ts`: code from `#pair=` fragment then fragment removed, name validation, redeem errors (invalid, rate-limited with countdown, disabled), check number shown, polling until allowed/rejected/expired, key stored once, text-only rendering (SV-19)
- [ ] T063 [US2] [frontend] Implement `src/remote-client/src/{pairing.ts,ui/pairing-view.ts}` + `index.html` shell markup to pass T062
- [ ] T064 [P] [US2] [frontend] Help topics `remote-control`, `remote-pairing`, `remote-security`, `remote-https-setup` (area `remote`), glossary `pwa`, `lan`, `pairing`; help tests first
- [ ] T065 [P] [US2] [sec] [qa] E2E `e2e/tests/remote-pairing.spec.ts` (two contexts): not set up; switch off -> remote shows off; pair by code with check-number comparison; QR fragment path; reject; 5 wrong codes -> stopped (SV-09); remove device -> remote shows removed within 1 s (SV-08); remove all; audit log (Expert); "Not encrypted" warnings (FR-016)
- [ ] T066 [P] [US2] [sec] [qa] Security suite `e2e/tests/remote-security.spec.ts` (Playwright `request` + raw WebSocket): SV-02, SV-03, SV-04 (main, remote, API), SV-06, SV-07, SV-14, SV-17, SV-18; project `remote-https-smoke` runs the TLS handshake, `wss`, headers and SV-02 on `lan-https`

**Checkpoint HO:us2** (backend, frontend, qa).

---

## Phase 5: User Story 3 - Control playback from the paired device (P1)

- [ ] T067 [P] [US3] [sec] [frontend] Failing tests `Remote/RemoteSocketEndpointTests.cs` (TestServer WebSocket client): subprotocol required, Origin, hello timeout 4408, bad key 4401, off 4403, fifth connection 4409, same device replaces 4000, oversize/binary/invalid JSON/schema 4400, 21 commands per second 4429, bounded send queue (SV-06, SV-07, SV-15, SV-16 using every `remote-examples/invalid` file)
- [ ] T068 [US3] [frontend] Implement `Remote/{RemoteSocketEndpoint,CommandRateLimiter,RemoteMessageReader}.cs` to pass T067
- [ ] T069 [P] [US3] [frontend] Failing tests `RemoteCommandValidatorTests.cs` + `RemoteCommandRouterTests.cs`: schema-level and live-state validation for all 24 commands (ranges, unknown stem/section ids, `unavailable` when 012/014/016 features are missing), table-driven routing to handler delegates, `ack` reasons
- [ ] T070 [US3] [frontend] Implement `Remote/{RemoteCommandValidator,RemoteCommandRouter,RemoteCommands}.cs` to pass T069
- [ ] T071 [P] [US3] [frontend] Failing tests `RemoteHubTests.cs` + `PlayerRegistryTests.cs`: register/unregister players and navigators, active-player rule (last play or focus, ties by registration), snapshot fan-out coalesced to <= 10/s and every 2 s while playing (fake time), `remotes` count, `Disconnect(device|all, reason)` sends `bye` and closes within 1 s (SV-08), state for no player
- [ ] T072 [US3] [frontend] Implement `Remote/{RemoteHub,PlayerRegistry,SnapshotScheduler}.cs` to pass T071
- [ ] T073 [P] [US3] [frontend] Failing tests `RemoteStateProjectorTests.cs`: mixer state -> `state` message matching `remote-messages.schema.json` (validated in the test), <= 8 KB with 32 stems and 64 sections, families via `RemoteFamilies`, `practisedFamily`, setlist, content flags/revision
- [ ] T074 [US3] [frontend] Implement `Remote/RemoteStateProjector.cs` to pass T073
- [ ] T075 [P] [US3] [frontend] Failing bUnit tests `Components/Mixer/MixerRemoteTests.cs`: the mixer registers a `RemotePlayerBridge` only when remote mode is effective; each remote command runs the same handler as the UI/014 shortcut (`InvokeAsync`), nothing is saved; `family.practise` mutes exactly one family and restores the previous mute states (FR-031); state changes publish snapshots; dispose unregisters; remote indicator in the shell (`remote-indicator`)
- [ ] T076 [US3] [frontend] Implement `Components/Mixer/Mixer.Remote.cs`, `Remote/RemotePlayerBridge.cs`, `Components/Shell/RemoteIndicator.razor` to pass T075; replace the T061 disconnect stub
- [ ] T077 [P] [US3] [frontend] Failing Vitest `src/remote-client/tests/ui/{transport,mix}.test.ts`: buttons send the right commands, disabled without player, position display from the clock, tempo slider/nudges, section chips + loop, stems mute/solo, practise-my-part per family in fixed order, ack rejections in the assertive live region, connection state in the polite live region
- [ ] T078 [US3] [frontend] Implement `src/remote-client/src/ui/{app,tabs,transport-view,mix-view,live-regions}.ts` + `remote.css` (shared tokens) to pass T077
- [ ] T079 [P] [US3] [qa] E2E `e2e/tests/remote-control.spec.ts` (two contexts): play/pause/seek/skip/next section/loop section/tempo 80 %/metronome/count-in/master; changes on the computer reach the remote; two remotes in arrival order; no-player state; Wi-Fi drop simulated with `context.setOffline` -> reconnect without replay; invalid command -> nothing changes
- [ ] T080 [P] [US3] [qa] E2E `e2e/tests/remote-families.spec.ts`: "practise my part" for each of the six families on a fixture with all families (SC-007) and `e2e/tests/remote-latency.spec.ts`: 50 commands, p95 command -> engine state <= 150 ms, state push <= 200 ms, interpolated position error <= 100 ms (SC-001, SC-002); prints the numbers for the hand-off

**Checkpoint HO:us3** (frontend, qa).

---

## Phase 6: User Story 4 - Music-stand view on the tablet (P2)

- [ ] T081 [P] [US4] [sec] [frontend] Failing tests `Remote/RemoteContentEndpointsTests.cs`: lyrics/chords/sheets/map/artwork only for the active player's track; other track, non-sheet asset, no player -> 404 (SV-17); Bearer required (401 + `WWW-Authenticate`); size caps; `no-store`; shapes match `remote-lan.openapi.yaml` (schema validation in the test)
- [ ] T082 [US4] [frontend] Implement `Remote/RemoteContentEndpoints.cs` (+ projections) to pass T081
- [ ] T083 [P] [US4] [frontend] Failing Vitest `src/remote-client/tests/stand/*.test.ts`: part picker from `content`, lyrics current line follows the interpolated position, chord current, content refetch on `revision` change keeping the family, text size, orientation attribute, wake lock (`on`/`off`/`unsupported`/`insecure`) with a mocked `navigator.wakeLock`
- [ ] T084 [US4] [frontend] Implement `src/remote-client/src/stand/{stand-view,lyrics-view,chords-view,wake-lock}.ts` to pass T083
- [ ] T085 [P] [US4] [frontend] Failing Vitest `src/remote-client/tests/sheet/*.test.ts` (pdf.js mocked): lazy load, render page to canvas, `isEvalSupported: false` / scripting disabled, page follow with a map, manual turning and swipe without a map, error state for broken PDFs
- [ ] T086 [US4] [frontend] Implement `src/remote-client/src/sheet/{sheet-view,page-follow,pdf-loader}.ts` (pdf.js worker emitted as a same-origin file) to pass T085
- [ ] T087 [P] [US4] [qa] E2E `e2e/tests/remote-stand.spec.ts`: stand tab follows playback (lyrics line, chord, sheet page with map), song change reloads content within 2 s keeping the family, families with content per fixture, content of another track refused; a11y of the remote at 390x844, 768x1024, 1024x768 (SC-006)

**Checkpoint HO:us4** (frontend, qa).

---

## Phase 7: User Story 5 - Install StemStudio as an app (P2)

- [ ] T088 [P] [US5] [frontend] Failing Vitest `src/remote-client/tests/sw/routing.test.ts` and `src/web-interop/tests/sw-routing.test.ts`: the pure routing decision for each request class of SV-22 (navigation -> network-first/offline; precached shell -> cache; `/_blazor`, `/_framework/*`, API origin, stems, assets, non-GET, cross-origin -> pass-through); cache versioning and old-cache cleanup
- [ ] T089 [US5] [frontend] Implement main `wwwroot/sw.js` (built from `src/web-interop/src/sw/*`), `offline.html`, `manifest.webmanifest`, icons (192, 512, maskable, apple-touch 180 from `favicon.svg`), and the remote equivalents under `/remote/` to pass T088
- [ ] T090 [P] [US5] [frontend] Failing Vitest `src/web-interop/tests/pwa.test.ts` + bUnit `InstallAppButtonTests.cs`: registration only in secure contexts, install prompt available/accepted/dismissed, hidden when standalone, Safari hint dismissible; remote client: install button only when `secure`
- [ ] T091 [US5] [frontend] Implement `src/web-interop/src/pwa.ts`, `Components/Shell/InstallAppButton.razor`, `App.razor` manifest link + SW registration, remote-client `install.ts`, help topic `install-app` to pass T090
- [ ] T092 [P] [US5] [qa] E2E `e2e/tests/pwa.spec.ts` + Lighthouse config: installability on localhost (main) and on the secure loopback remote origin; offline page within 2 s after `docker compose stop web`; no stem/API/`_blazor` response from the service worker (inspect `response.fromServiceWorker()`); new version replaces the shell after one reload; insecure-context emulation (`--host-resolver-rules`) -> remote shows install hint "needs HTTPS" and no SW (SC-005)

**Checkpoint HO:us5** (frontend, qa).

---

## Phase 8: User Story 6 - Songs and setlists from the remote (P3)

- [ ] T093 [P] [US6] [frontend] Failing bUnit/unit tests `NavigatorBridgeTests.cs`: every open tab registers a navigator; `track.open` opens the mixer, with unsaved changes asks first (`waiting-for-computer` ack, then state); setlist previous/next disabled at the ends; `library.search` returns only completed tracks with title/artist/duration (<= 25)
- [ ] T094 [US6] [frontend] Implement `Remote/NavigatorBridge.cs`, `Components/Shell/RemoteNavigator.razor`, setlist/search handlers to pass T093
- [ ] T095 [P] [US6] [frontend] Failing Vitest `src/remote-client/tests/ui/songs.test.ts`: setlist card, search input debounce (300 ms), results list, open button, waiting state
- [ ] T096 [US6] [frontend] Implement `src/remote-client/src/ui/songs-view.ts` to pass T095
- [ ] T097 [P] [US6] [qa] E2E `e2e/tests/remote-songs.spec.ts`: setlist of three fixtures, next song twice, unsaved-changes prompt on the computer, search results without paths or lyrics

**Checkpoint HO:us6** (frontend, qa).

---

## Phase 9: Polish (inside the wave, per owner)

- [ ] T098 [P] [backend] Coverage >= 90 % line and branch for Domain/Persistence/Api remote code; `dotnet format --verify-no-changes`; SIG check; XML docs; HO:polish
- [ ] T099 [P] [frontend] Coverage >= 90 % for `StemStudio.Web` (incl. Remote), `src/web-interop`, `src/remote-client`; eslint/tsc clean; bundle size check (remote first load <= 300 KB gzip without pdf.js); TSDoc; HO:polish with dependency licences (QRCoder, pdfjs-dist)
- [ ] T100 [P] [sec] [frontend] Self-review against remote-protocol R1-R8 and ADR-027 threat table; log scan over a full E2E run for keys/codes/secrets (SV-13); document results in HO:polish
- [ ] T101 [P] [qa] Full E2E run (all projects incl. `remote`, `remote-https-smoke`), a11y and visual suites green; flake report; traceability table complete; HO:polish
- [ ] T102 [P] [sec] [quality-gate] Review of every `[sec]` test (asserts the SV behaviour, not just status codes; negative cases present); `run_all.py` 0 errors; SIG-REPORT update; HO:polish
- [ ] T103 [P] [devops] CI green with all new jobs; image size unchanged (+ remote assets only); `scripts/lan-check` tested on Windows and Linux; HO:polish
- [ ] T104 [P] [frontend] Docs-impact notes in HO:polish (screens, settings, set-up steps) for the manuals (FR-037)
- [ ] T105 [P] [backend] Docs-impact notes (endpoints, tables, audit) in HO:polish

---

## Integration wave (CTO, after all wave hand-offs are accepted)

- [ ] T106 CTO: merge the wave branches; resolve contract merge with 012-017 (additive); full `scripts/test-all.*` green
- [ ] T107 CTO: user manual chapter "Tablet and remote" + FAQ/troubleshooting (DELTA.md 9)
- [ ] T108 CTO: technical manual: deployment (remote mode, compose override, mkcert walkthrough, bind-address change and release note), security (threat model, network exposure), APIs, data model, operations; regenerate PDFs; `node tools/docs/check.mjs` green
- [ ] T109 CTO: `docs/THIRD-PARTY-LICENSES.md` (QRCoder MIT, pdfjs-dist Apache-2.0; mkcert as an owner tool), `SECURITY.md` remote paragraph, `README.md` quick note, `docs/STATUS.md`
- [ ] T110 CTO: quickstart Q1 (default exposure) on the reference machine, including the port scan of Q6 with remote mode off
- [ ] T111 CTO: quickstart Q2 (stand view, install, offline page)
- [ ] T112 CTO: quickstart Q3 (`lan-http`) with a real phone
- [ ] T113 CTO: quickstart Q4 (`lan-https`, mkcert) with a real iPad and an Android device: install, wake lock, certificate expiry negative test
- [ ] T114 CTO: quickstart Q5 (two remotes, audit log) and Wi-Fi latency p95 (SC-001) recorded
- [ ] T115 CTO: quickstart Q6 port scan with remote mode on: only the remote port
- [ ] T116 CTO: security sign-off: ADR-027 status -> Accepted, residual risks acknowledged (lan-http, mkcert root key, pre-existing API CSRF follow-up issue created)
- [ ] T117 CTO: release note on the `127.0.0.1` default and `STEMSTUDIO_BIND_ADDRESS`
- [ ] T118 CTO: owner walkthrough of stand view on the music stand (SC-003 pairing <= 30 s, 4 actions)
- [ ] T119 CTO: STATUS, roadmap tick, close the feature
- [ ] T120 CTO: follow-up backlog items: one-shot name-constrained CA (ADR-027 upgrade path), API-wide CSRF hardening (research F-03), agent remote tools (Won't for now)

---

## Execution plan per owner (parallel wave)

| Owner | Foundational | US1 | US2 | US3 | US4 | US5 | US6 | Polish | Count |
|---|---|---|---|---|---|---|---|---|---|
| architect (Phase 0) | T001-T007 | | | | | | | | 7 |
| quality-gate | T008, T009 | | | | | | | T102 | 3 |
| devops | T010-T013 | | | | | | | T103 | 5 |
| qa | T014, T015 | T047, T048 | T065, T066 | T079, T080 | T087 | T092 | T097 | T101 | 12 |
| backend | T016-T027 | | T049-T055 | | | | | T098, T105 | 21 |
| frontend | T028-T039 | T040-T046 | T056-T064 | T067-T078 | T081-T086 | T088-T091 | T093-T096 | T099, T100, T104 | 57 |
| CTO (integration) | | | | | | | | T106-T120 | 15 |
| **Total** | | | | | | | | | **120** |

Hand-off markers: `HO:foundational` after T027 (backend) / T039 (frontend); `HO:us1`..`HO:us6` after
each story's last task of that owner; `HO:polish` after the polish task.

Parallel opportunities: inside each phase all `[P]` tasks of one owner can start together (tests);
backend US2 (T049-T055) and frontend US1 (T040-T046) run at the same time; frontend US2 web work
(T056-T061) can use a fake API client until backend T055 lands; the remote client tracks (T036-T039,
T062-T063, T077-T078, T083-T086, T095-T096) are independent of the Blazor tracks and can be split
across two frontend sessions; qa authors every spec against the ids before the UI exists.

## Dependencies & Execution Order

- Phase 0 (architect) -> wave. T003 (reference machine verification) must pass before devops T011
  is merged; if Docker Desktop cannot publish on one address, the plan falls back to the documented
  non-Docker path and T011 keeps only the `127.0.0.1` change.
- Inside the wave: foundational -> US1 and US2 -> US3 -> US4/US5/US6 per owner; US5 depends only on
  foundational (can run in parallel with US3).
- Integration wave after all `HO:polish` hand-offs are accepted.

## Definition of Done checklist (mirrors the constitution)

- [ ] Every task pair red -> green with commit hashes in the hand-offs
- [ ] Coverage >= 90 % line and branch: API, web, `src/web-interop`, `src/remote-client`
- [ ] `dotnet build` 0 warnings, `dotnet format`, eslint, tsc, ruff (unchanged), SIG check 0 errors
- [ ] Redocly valid for both 018 OpenAPI files and the merged backend 1.17.0; schema examples check green
- [ ] SV-01..SV-22 green; ADR-027 accepted
- [ ] Public API documented (XML docs, TSDoc); licences recorded
- [ ] Manuals updated and PDFs regenerated; `tools/docs/check.mjs` green
- [ ] Hand-offs written; STATUS updated; Conventional Commits
