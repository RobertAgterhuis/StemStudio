# Hand-off: CTO / 011 follow-up claude-key, integration (T053-T056)

Branch `011-claude-key-spec` (worktree `.claude/worktrees/011-claude-key`). Nothing pushed, nothing merged into `main`.
Anthropic was never called; no key-shaped literal was written; no `ANTHROPIC_API_KEY` was set in any shell used.
The owner's compose project `stemstudio` (:8080/:8081) was not stopped, rebuilt or recreated, none of its volumes or
`stemstudio/*` image tags was touched (container ids and image ids identical before and after, see "Owner stack").

## Merge commits (`--no-ff`, in order, each with the Co-Authored-By trailer)

| Lane | Merge commit | Conflicts |
|---|---|---|
| `ck-lane-quality` | `1b2dac61` | none |
| `ck-lane-devops` | `45e711c6` | `tasks.md` ticks (all kept) |
| `ck-lane-agent` | `5df99392` | none |
| `ck-lane-backend` (contains `ck-lane-agent`) | `748af94d` | none |
| `ck-lane-frontend` | `c70a3fc4` | `tasks.md` ticks (all kept) |
| `ck-lane-qa` | `6aba9e5d` | `tasks.md` ticks (all kept) |

The lane-internal merge `d600b5ed` (ck-lane-agent into ck-lane-backend) has no trailer (git default message); accepted
inside the lane history. `git log --format='%ae %ce' main..HEAD` shows only `ragterhuis@agterhuis.net`.

## What was done after the merges (one commit per item, red before green where behaviour changed)

1. **Port overload** (architect): `IAgentStatusService.TestAsync(AgentBackendDto, ClaudeKeyTestSourceDto, CancellationToken)`;
   `AgentStatusProbe` passes `claudeKeySource ?? active` through any implementation, the concrete-type check is gone
   (`e2e55330`, red `14b8e1b7`, green `4d9cb8da`).
2. **ui-test-ids 1.21.0** (unreleased, delta and merged document): `agent-unavailable-switch-to-local` (button inside
   `agent-unavailable`) and `agent-key-changed` (the CK-FR-016 note); the frontend puts `data-testid="agent-key-changed"` on
   the note (`af1446df` red, `4dd9cef4`; `66e0aab6` red, `89a9596a`).
3. **T053 contract text and decisions**: `updateAgentSettings` now says "a usable Claude key" (environment or stored), the
   `KeyStoreUnavailable` example is the API's own text (`ClaudeKeyProvider.StoreUnavailableReason(not-writable)`), both as
   pre-release text changes of 1.21.0 pinned by `ClaudeKeyContractTests` (`/api/agent/settings` differs from 1.10.0 only in
   that description). The ai-agent decisions (environment value format-checked like a stored key; one reason/fix pair,
   priority store unavailable > unreadable > malformed environment value; `KeyMissing`/`KeyMissingFix` texts) are recorded in
   data-model section 3 and ADR-035 **D8** (`70c2e559` red, `2c5ef76d`, `6c724e8c`).
4. **Backend wiring verified** (no change needed): `ProtectedClaudeKeyStore` is the `IStoredClaudeKeyReader`;
   `Invalidate()` after PUT and DELETE; settings 409 rule and status fallback read `IClaudeKeyProvider.Resolve()`; Api.Tests
   use `ClaudeKeySentinels.Environment`; every 422 carries `reason`; the 403 type is `.../local-only`; `GET /api/agent/status`
   fills `claude.key` (also on the fallback path); Claude without a usable key is `ready:false` with reason and fix (agent
   tests `ClaudeKeyBackendTests`, API test `Delete_KeepsBackendClaudeAndTheAgentThenReportsNoKey`); `keySource` in results.
5. **Test hardening** (quality-gate R1-R12, qa F1-F8):
   - R1 `ClaudeKeySentinels.SearchForms`: plain value, the key's middle (no prefix, no last four), UTF-8 and UTF-16 base64
     in all three alignments; proven on a key inside a larger base64 payload and on a masked key.
   - R2/F1/F2 `ClaudeKeyLeakTests`: positive controls (the stand-in received exactly the stored key, run failed
     `backend-unavailable`, at least one Trace entry, the scanned database holds the key row); scopes searched; files
     scanned while the host runs (`FileShare.ReadWrite`) and after shutdown. **F2 finding**: the API's SQLite uses the
     default rollback journal (no WAL in `src`), so `-wal`/`-shm` never exist; the test records `PRAGMA journal_mode`
     (`delete` or `wal`) and requires `-wal` if WAL is ever enabled. The store test proves the scanned file holds the
     ciphertext.
   - **F1 finding (real gap)**: `appsettings.json` sets `Microsoft.AspNetCore` to `Warning`, which won over the fixture's
     `Microsoft=Trace`: pipeline and Data Protection logs were never searched by any "Trace" leak test. `ClaudeKeyApi` now
     sets `Microsoft.AspNetCore=Trace`, and the new `TestSupport/KeyLeaks` helper asserts a Trace entry before searching
     messages, exceptions and scopes. With the fix all leak tests stay green (no leak found).
   - R3 `ListLoggerProvider` records scope state; R4 key ring guard rows (`..`, `.`, `//`, trailing separator, case variant on
     Windows/macOS) and the **decision**: a key ring directory that contains the library, backups or app is allowed (the
     ring never travels inside a copy of them; data-model section 1); R5 devops negative tests assert the exact problem;
     R6 key test path searched with SearchForms and Trace logs; R7 the unreadable ciphertext is in neither the run failure
     nor its SSE events; **R8** CORS preflight for PUT/DELETE/POST on the key route never lists the method, GET does (mutation
     check: `WithMethods("GET","PUT")` fails the test); R9 every refusal asserts the `local-only` problem and an unchanged
     store, F4 a refused write over a stored key keeps it; **R10** `X-Forwarded-For`, `X-Forwarded-Host`, `X-Real-IP`,
     `Forwarded` from a LAN peer stay `403`; R11 `localhost.` and `127.0.0.1.` are not local; R12 JSON serialisation of
     `ClaudeApiKey`, `ClaudeKeyResolution`, `StoredClaudeKey`, `SelectedClaudeKey` never writes the value; F3 refused bodies
     carrying the key (bad JSON, array, >1 KiB, 422) at Trace with nothing stored; F5 replace keeps exactly one row,
     deleting an unreadable key removes the row.
   - **F6 findings (fixed, red first)**: the key handlers relied on the disabled button only; a forged click under
     local-only reached `StoreClaudeKeyAsync` (bUnit proved it). Save, delete-confirm, replace and test now re-check their
     guard (`68ef2b51`). And a key-write problem that echoed the key in title/detail/errors/extensions was carried into
     `ApiException.Message`; `ClaudeKeyProblems` keeps only the status, a known problem type and a known `reason`
     (`33ce6ad3`). The page never showed those fields.
   - F8 the typed agent client (registered through `IHttpClientFactory` as in `Program.cs`) and the circuit never log the key
     at Trace (`ClaudeKeyWebTraceLogTests`; bUnit's own test renderer `Bunit.*` logs event args and is the only category
     left out).
6. **Test-only Anthropic base-URL override: not done.** It needs a seam in the production Claude client path that would
   send a real stored or environment key to a configurable host. Making it safe needs more than a flag (a compile-time
   exclusion, a compose-check rule that only the E2E override file may set it, and a stub image with a request counter).
   That is a separate devops + ai-agent task, not something to fold into an integration wave. CK-SC-002 (no request without
   an explicit click) therefore stays proven at unit/API level: `ClaudeKeyBackendTests` (status/probe without a request),
   the Api must-not-call stand-in (`Anthropic.Requests.ShouldBeEmpty()` over save, replace, delete, status, settings, the
   unreadable run) and `ClaudeKeyBackendTestTests` (exactly one request per test, with the requested key).

## Tests and coverage (`pwsh scripts/test-all.ps1 -ContinueOnError`, final run)

**60 of 60 steps passed** (exit 0), including the manuals check after the PDF rebuild, contracts lint (incl. 011
claude-key), secret scan and its self-test, the key volume checks (pytest + the 4 compose combinations) and `run_all.py`.

| Component | Tests | Line | Branch |
|---|---|---:|---:|
| StemStudio.Domain | 752 passed | 98.97 % | 94.52 % |
| StemStudio.Agent | 720 passed | 98.91 % | 94.18 % |
| StemStudio.AgentEval | 134 passed | 99.83 % | 92.85 % |
| StemStudio.Persistence | 114 passed | 98.91 % | 95 % |
| StemStudio.Web | 1848 passed | 99.57 % | 93.71 % |
| StemStudio.Api (+ Contracts) | 1651 passed, 2 skipped (Linux-only) | 98.98 % (Contracts 100 %) | 92.08 % (Contracts 100 %) |
| worker | 2381 passed, 3 skipped | 100 % | (incl.) |
| generator | 501 passed, 2 skipped | 99.05 % | (incl.) |
| audio engine | 287 passed | 100 % | 100 % |
| web-interop | 254 passed | 99.69 % | 97.95 % |
| tools/quality | 666 passed | 100 % | 100 % |
| scripts (key volume checks) | 45 passed | 100 % | 100 % |
| test fixtures | 250 passed | 100 % | 100 % |

`dotnet build -c Release`: 0 warnings. `dotnet format --verify-no-changes`: exit 0. E2E lint, typecheck and list (831 tests in
47 files) passed.

## Quality checks

- `dotnet build StemStudio.slnx -c Release`: 0 warnings, 0 errors. `dotnet format --verify-no-changes`: exit 0.
- `tools/quality/run_all.py`: **2210 files, 17 375 units, 252 clones, 3 exemptions, 0 errors, 815 warnings**;
  `docs/quality/SIG-REPORT.md` regenerated (`7aa02b44`).
- Redocly 2.78.0: backend 1.21.0 and the claude-key delta valid (11 accepted `operation-4xx-response` warnings).
- `scripts/secret_scan.py`: clean (self-test passed). Key volume checks: 4 compose combinations 10/10.

## E2E (separate compose project `ck-e2e`, ports 18080/18081, images `ck-e2e/*:local`, own volumes, retries 0)

Stack: a copy of `docker-compose.yml` in the scratchpad (project name `ck-e2e`, images `ck-e2e/{worker,api,web}:local`,
host ports 18080/18081, `StemStudio__WebOrigin`/`PublicApiBaseUrl` on those ports, absolute build contexts), fake separator,
`COMPOSE_PROJECT_NAME=ck-e2e`, `E2E_COMPOSE_FILE`/`COMPOSE_FILE` = the copy. No `ANTHROPIC_API_KEY` in any shell (`api` checked:
variable not set). Removed afterwards with `docker compose -p ck-e2e down -v` and the three `ck-e2e/*` images.

| Suite | Result (retries 0) |
|---|---|
| default `npm test` (fresh stack, agent `none`, all fixtures generated) | **381 passed, 115 skipped, 0 failed** (34.7 min) |
| `npm run test:claude-key` (Chromium + Firefox) | **20 passed** (after two E2E fixes, below) |
| `scripts/agent-e2e.ps1 fake` (targets `ck-e2e` through `COMPOSE_PROJECT_NAME` + `COMPOSE_FILE`) | **18 passed** (us1 4, us2-delete-mix/-delete-track/-detail/-inject 2 each, us5 2+2, no script 2; us6 `@generative` not selected) |
| `scripts/agent-e2e.ps1 none` | **2 passed** |
| visual (Playwright v1.65.1-noble container, fresh stack, agent `ollama` unreachable at `host.docker.internal:11434` as in CI) | first run: exactly the 6 `settings--<level>--<theme>` baselines differ (diff reviewed: the new key section and the actions row below it); regenerated only those 6; re-run **110 passed, 6 skipped** |
| a11y (same container and stack) | **142 passed, 6 skipped** |

E2E fixes (qa-owned files, committed at integration, `e2e: ...` commit): the page object counted the key field and then
waited in `inputValue()` for a field the save had removed (Firefox hang); axe-core's `target-size` rule crashes Firefox's
content process ("Page crashed") on Settings with a stored key, reproduced on Windows and in the Linux Playwright image,
isolated rule by rule; `axeViolationsIn` skips that rule in Firefox only (Chromium and the a11y project still run it).

## Quickstart (T056)

Run on `ck-e2e` (project and volume names substituted):

| Row | Result |
|---|---|
| Q1-Q3 | build 0 warnings, all .NET tests green with coverage above, Redocly valid, `run_all.py` 0 errors, secret scan clean |
| Q4 | `@claude-key` 20/20 (save, last four, DOM/network/console clean, replace, delete with confirmation, unreadable after key ring loss, local-only API and page, environment wins, logs and SQLite files clean) |
| Q5 | `ClaudeKeyLeakTests` green with the new positive controls |
| Q6 | must-not-call stand-in at API level (0 requests over start, status, save, replace, delete; 1 per Test key); no E2E proof (item 6) |
| Q7 | `ck-e2e_api-keys` exists; `/keys` is `700`, uid `1654` (= `id -u` of the app); `worker` has no `/keys`; key file `-rw------- app` |
| Q8 | a random well-formed dummy key stored through the `web` container (internal caller, body on stdin); after `down` + `up --build --wait` the status is still `stored`, `readable`, same last four |
| Q9 | after `down`, `docker volume rm ck-e2e_api-keys`, `up --wait`: API healthy, `stored-unreadable` with "The stored key cannot be read on this installation ..." / "Enter the key again."; tracks, mixes, settings, status 200; the full E2E suite ran on that code |
| Q9b | no second LAN device; the E2E local-only page test opened the page under this computer's LAN address (controls disabled, notice shown) and the API test refused a foreign `Origin` with `403 local-only` |
| Q10 | real key: left to the owner |

Security sign-off (threat model T1-T11): T1-T5 and T7 covered by [sec] tests incl. the new R8 (CORS) and R10 (forwarded
headers); T6 waits for 018; T8-T11 documented in the help topic, `SECURITY.md` and technical manual chapter 8. Residual
risk T7 accepted by the owner until 018 (Q1).

## Owner stack

Before and after (`docker ps` and `docker image ls`): containers `fbbff1736d5e` web, `0c28d8bfba4d` api, `ff8e81b3c883`
worker, `6bf431e2d46f` singer, `0a9de343348a` generator unchanged (running 4-6 hours, healthy); `stemstudio/*` image ids
identical (api `ba2cba300da0`, web `29b758a479ff`, worker `abbe1e721f86` / `local-gpu` `18df1fad499c`, ...); no
`stemstudio_*` volume touched. Every compose command used `-p ck-e2e` or `COMPOSE_PROJECT_NAME=ck-e2e` with the copy; after
the first agent recreate only `ck-e2e-api-1` was new. **Owner stack untouched: yes.**

## Dependencies added

None.

## Docs impact (done in this wave)

README (Claude key in Settings, the key volume), `SECURITY.md` (threat model summary), `.claude/agents/ai-agent.md` key rule,
011 `spec.md` FR-018/FR-019/SC-006, 011 `data-model.md` 1, `tasks.md` principle line, ADR-020, ADR-035 D8, technical manual
(chapters 3 variables + "Claude key storage", 5, 6 key endpoints, 7 migration `AddAgentSecrets`, 8 threat model), user manual
(chapter 13 "Your Claude API key", chapter 16 troubleshooting); PDFs and `build-manifest.json` rebuilt; `docs/STATUS.md`.

## Open items

1. **Test-only Anthropic base-URL override** for an E2E proof of CK-SC-002 and pressing **Test key** in E2E (item 6):
   devops + ai-agent design (compile-time or compose-check guarded), not done.
2. **Firefox + axe `target-size` crash** on Settings with a stored key: worked around in `e2e/support/axe.ts`; worth a
   minimal reproduction for axe-core / Mozilla (qa).
3. The Claude "Not configured" line in Settings still names only `ANTHROPIC_API_KEY` ("set ANTHROPIC_API_KEY ... and restart
   the API") right above the key section that offers the other way (frontend copy, small).
4. Soft SIG warnings (unit size 16-18 lines) in the backend key code, listed by the quality-gate polish hand-off.
5. Quickstart Q10 (real key) and a second LAN device for Q9b: owner.
6. 018 must close T7 (`127.0.0.1` binding) and 013 must keep the key ring out of backups (013 T035/T048/T062).

## Next owner

Owner: review, run quickstart Q10 with the real key if wanted, then decide on pushing / the PR into `main`.
