# Hand-off: architect / 015 Phase 0 (ADR, engine hook check, contract merge, contract code, skeleton, spikes)

Branch `r14-p0-015` (worktree `.claude/worktrees/r14-p0-015`) from `main` e8d6a5d8 (001-013 merged,
incl. 012 practice tempo and 013 library care). Combined round 014+015: 014's architect works in
parallel and owns `docs/ROADMAP.md` and `docs/decisions/CTO-DECISIONS-2026-11-15.md` (not edited
here; decision rows below). Round 022+023 lands on `main` before the wave; the re-merge is
described in `specs/015-play-along-recording/contracts/DELTA.md` "Re-merge".

## What was built

Contract versions (allocated labels, nothing renumbered; ADR number check: ADR-024 free and
allocated to 015 -> **Accepted**):

| Contract | Document | Base used now |
|---|---|---|
| backend-api **1.14.0** | `specs/015-play-along-recording/contracts/backend-api.openapi.yaml` (built by `merge_recording_delta.py` from the delta `recording-api.openapi.yaml` 1.14.0-delta.2) | 013's 1.12.0 (014's 1.13.0 is not on `main`) |
| audio-engine **2.5.0** | `contracts/audio-engine.md` + `src/audio-engine/src/types.ts` (`EngineContractVersion = '2.5.0'`) | 012's 2.3.0 (014's 2.4.0 not on `main`) |
| ui-test-ids **1.14.0** | `contracts/ui-test-ids.md` | 013's 1.12.0 |
| render-vectors **1.1.0** | `contracts/render-vectors.json` from `contracts/generate_render_vectors.py` (`--check`) | 007's 1.0.0 (unchanged inside) |
| worker, generator, agent tool catalogue | unchanged | - |

Tasks: T001 (ADR-024 Accepted, engine hook check against the merged 012 engine), T002 (full
contracts, superset checker, Redocly), T003 (C# contracts + contract tests), T004 (engine types,
skeleton methods, capture worklet stub as third Vite entry, skeleton tests), T005 (spikes, partly;
see below), T006 (verification, this note). T025a/T025b added for the backend (013/022 integration).

Ready check (multi-agent-delivery 0): spec/plan/tasks/data-model/DELTA cross-checked; the
inconsistencies found were fixed in Phase 0 (decisions P0-1..P0-8): `Provenance.recipe` oneOf vs.
the typed `ExportRecipeDto`, `ChannelId` widening that would have reached scores and the agent tools,
`MixSettingsInput` that does not exist, `L + P` applied twice against heard anchors, no write gate
on take deletes, and the 022 filing rule that would file takes. Every task keeps a testable
acceptance criterion. `docs/process/LESSONS.md` is not on `main` yet; read from
`022-023-round` and applied (no scope cuts without the orchestrator, quiet logs, no stack started,
013 backend lessons: containment, rebuild-not-validate, `Mode=ReadWrite`).

## Decisions taken (binding; recorded in ADR-024 "Phase 0 outcome" and DELTA.md "Phase 0 decisions")

| # | Decision |
|---|---|
| P0-1 | Take recipe = optional `Provenance.recording` (`RecordingRecipe`), like 1.8.0 `generation`; `recipe` stays `ExportRecipe` or null |
| P0-2 | `ChannelId` unchanged; new `ExportChannelId` (`ChannelId` or `take`) only in `CreateAudioExportRequest.channels` and `ExportRecipe.channels`; C# `ContractLimits.ExportChannelIdPattern`; agent catalogue unchanged |
| P0-3 | Additions absent when unused: `MixSettings.take`, `CreateAudioExportRequest.takeId`, `Provenance.recording` (null), `Track.takeCount` (0) - existing examples stay byte-identical |
| P0-4 | 013 write gate on `deleteRecordingTake`, `deleteRecordingTakes`, `replaceRecordingTakeAudio` (409 `backup-running`, 503 `library-maintenance`) |
| P0-5 | Colliding delta responses are `Take*` in the full document; new `ManagedByTake` (409) on asset rename/delete, documented in the retry 409 |
| P0-6 | Engine `RecordingSupport.reasonCode`: `insecureContext`, `noMediaDevices`, `noAudioWorklet`, `rateMismatch` (UI `data-reason` mapping in ui-test-ids 1.14.0) |
| P0-7 | **Recording only in a secure context** = on the StemStudio computer via `http://localhost` / `127.0.0.1`; over a LAN address (http) the engine reports `insecureContext` (measured, below). No browser-flag workaround is documented. LAN residual risk T7 unchanged (018 closes it) |
| P0-8 | Capture mapping against the **heard** transport anchors (they contain the tempo-stage latency `P`): compensation `L` only, `CapturedTake.latencySamples` reports `L + P` |

### Decision rows for CTO-DECISIONS (to copy into `docs/decisions/CTO-DECISIONS-2026-11-15.md` at integration)

| Date | Feature | Decision | Recorded in |
|---|---|---|---|
| 2026-11-17 | 015 | ADR-024 accepted (number checked, no rename) | ADR-024 "Phase 0 outcome" |
| 2026-11-17 | 015 | Full documents built on `main`'s newest (backend 1.12.0, engine 2.3.0, UI ids 1.12.0) with 015's labels 1.14.0 / 2.5.0 / 1.14.0; re-merged in feature order (014 then 015) after 022/023 and 014 land | DELTA.md "Status", "Re-merge" |
| 2026-11-17 | 015 | `Provenance.recording` instead of a `recipe` variant (P0-1); `ExportChannelId` instead of widening `ChannelId` (P0-2) | DELTA.md P0-1, P0-2 |
| 2026-11-17 | 015 | 013 write gate on take delete / batch delete / replace audio (P0-4); takes = backup and storage category `recordings`, stored origin `Recording` (013 fixture unchanged); `InputCalibrations` in `database` | DELTA.md P0-4, section 7 |
| 2026-11-17 | 015 | Recording only on the StemStudio computer (secure context); LAN over http shows "insecure" with the reason (P0-7) | ADR-024 outcome 2, audio-engine.md "Browser constraints" |
| 2026-11-17 | 015 | 022 filing excludes origin `recording` (`IsFileable`, pending SQL) - backend T025a/T025b | DELTA.md section 7 |
| 2026-11-17 | 015 | render-vectors 1.1.0 = 1.0.0 + 8 take cases (take last in mixer order, `take-required`, raw take range = span); consumers switch from 007's file when they implement the take | DELTA.md 5, generate_render_vectors.py |
| 2026-11-17 | 015 | S-01 10-minute real-interface run, CPU and Firefox rate mismatch move to the owner checks (T089); S-02 circuit+API half measured by frontend (T043) and CTO (T089) | research.md S-01/S-02 results |

## Engine hook check against 012 (T001; details in audio-engine.md "Phase 0 outcome")

| Request | In 2.3.0 | Lane work |
|---|---|---|
| Transport segments | anchors (heard/played queues, explicit passes, count-in gaps) exist but are pruned | T015 adds an internal segment log |
| Tempo-stage latency | `TransportTiming.latency`, already inside heard anchors | none (P0-8) |
| Take through the tempo stage | stage sits on the master bus | none |
| Count-in bars / every pass | `PracticeSettings.countIn`, `CountIns` | apply `countInBars` for the session |

## Spikes (T005)

Browser matrix (Playwright 1.65.1, Chromium 159 full build with fake capture, Firefox 159 fake stream;
origins served by request interception, no server, no Docker):

| Origin | isSecureContext | mediaDevices | audioWorklet | Capture |
|---|---|---|---|---|
| http://localhost | true | yes | yes | Chromium 60 s / Firefox 60 s: **0 gaps** (1,292 batches of 2,048 frames) |
| http://127.0.0.1 | true | yes | yes | 3 s, 0 gaps (both) |
| http://LAN-IPv4 | false | **no** | **no** | impossible (012's time stretch is also unavailable there) |

S-02 browser half (Chromium): 5-min stereo 24-bit WAV encode 207 ms (79.4 MB), `blob.stream()` read
291 ms; 240 MiB Blob read 138 ms. .NET facts (Microsoft Learn, ASP.NET Core 10):
`OpenReadStreamAsync(maxAllowedSize)` defaults to 512,000 bytes (pass `MaxStreamBytes`),
`createJSStreamReference` accepts a Blob, zero-length streams are refused. Open: circuit + API
timing (frontend T043 timing test, CTO T089), 10-minute real interface + CPU + Firefox 48 kHz
mismatch (T089).

## What the lanes must know (lane split suggestion for the combined round)

015 is engine-heavy (26 engine tasks, 25 backend, 20 frontend, 10 qa). With the 4-agent limit
and 014's own lanes, suggested split for the 014+015 wave:

1. **audio-engine (015)** alone - the longest lane: T012-T019, T028-T035, T045/T046, T052/T053,
   T059/T060, T073, T077/T078. Start with T015 (segment log) and the pure units
   (wav, mapping, calibration, compare). Reads: audio-engine.md "Play-along recording" incl. Phase 0
   outcome, data-model 3, research R-03/R-04/R-09/R-10. Replace `src/recording/pending.ts`; keep
   `Mixer.analyser('input')` semantics; switch `tests/render-vectors.test.ts` to the 015 vectors
   in T018/T019. 014's engine work (score follower, 2.4.0) should go to a **different** lane
   (frontend-014 or a small engine-014 lane) and both touch `types.ts`/`engine.ts`: merge order
   014 then 015, conflicts limited to the version literal and the callback union (DELTA "Re-merge").
2. **backend (014 + 015)** - 015 T020-T025b first (WAV reader, padder, rules, migration, 013/022
   integration), then the stories. Reads: backend-api 1.14.0 (tag `recording` and the changed
   schemas), DELTA.md 1-2, 7, data-model 2. Remove ids from `PendingRecordingOperations`
   (`BackendContractTests.StemsAndMixes.cs`) as endpoints are served. Every new writer that deletes or
   replaces take files under `RequireGateOpen`; files are always under `assets/<server GUID>/`.
3. **frontend (014 + 015)** - 015 T026/T027, T040-T043 (incl. the S-02 timing test), the panels,
   help topics (data-model 5, one connection topic per instrument family). `rec-unavailable`
   `data-reason` from `reasonCode` (insecure/unsupported). Use the 1.14.0 ui ids only.
4. **qa + devops + stem-separation + quality-gate (bundled)** - fixtures (T009), E2E specs written
   against ids and contracts (no stack in the lane), Playwright `recording` project with
   `channel: 'chromium'` and the fake-device flags (T010), worker regression test T011, quality
   rules T007/T008. devops also wires in CI: Redocly lint of
   `specs/015-play-along-recording/contracts/{backend-api,recording-api}.openapi.yaml`,
   `check_superset.py` (default and with the delta as older), and
   `generate_render_vectors.py --check` for the 015 vectors.

Wave start condition: after 022/023 land, merge `main` and the 014 design head into the round, run
the "Re-merge" steps (about 30 min), then branch the lanes.

## Tests and coverage

```text
dotnet build StemStudio.slnx -c Release                        0 Warning(s), 0 Error(s)
dotnet format StemStudio.slnx --verify-no-changes              passed
dotnet test -c Release -p:CollectCoverage=true                 (line / branch / method)
  Domain.Tests        942 passed           Domain        98.87 % / 95.06 % / 97.67 %
  Agent.Tests         726 passed           Agent         99.03 % / 94.18 % / 98.59 %
  AgentEval.Tests     134 passed           AgentEval     99.83 % / 92.85 % / 100 %
  Persistence.Tests   121 passed           Persistence   99.01 % / 95 %    / 93.62 %
  Web.Tests          2358 passed           Web           99.65 % / 93.98 % / 99.74 %
  Api.Tests          2207 passed, 2 skipped Api          98.58 % / 91.1 %  / 98.87 %
                                           Contracts     100 %   / 100 %   / 100 %
  (first full run: 1 timing flake each in Agent HistoryAndUndoTests "database is locked" and
   ClaudeKeyLeakTests "Condition not met"; both pass in isolation 3x/2x and the full rerun is
   green; both are the known flakes fixed on 022-023-round, merges 43334b38 / d41eb75a)
audio-engine: tsc --noEmit clean; eslint clean; vitest 34 files, 509 passed;
  coverage 99.94 % lines / 99.86 % branches / 100 % functions / 99.94 % statements;
  npm run build -> audio-engine.js, time-stretch-processor.js, audio-engine-capture-worklet.js
Contracts: Redocly 2.78.0 lint backend-api 1.14.0 + recording delta: valid, 0 errors (20 warnings:
  12 operation-4xx-response in the full document, inherited except listInputCalibrations;
  7 no-unused-components + 1 operation-4xx in the delta)
  check_superset.py: superset yes (1.12.0 -> 1.14.0) and yes (delta -> 1.14.0)
  generate_render_vectors.py --check (015 1.1.0 and 007 1.0.0): up to date
worker / generator / web-interop: unchanged by 015 Phase 0, not rerun
```

## Quality checks

`tools/quality/run_all.py`: 2626 files, 0 errors -> exit 0. New warnings only in test code:
unit size of two contract tests (17 and 19 LOC) and the skeleton `it.each` table, and duplication of
four small helpers (`Canonical`, `Template`, `Int`, `SchemaOf`) between `LibraryCareContractTests`
and `RecordingContractTests` (left as is: a shared move touches the 013 tests; quality-gate may
request it in T008).

## Dependencies added

None (no npm, NuGet or Python package; the spikes used the existing Playwright install of `e2e/`).
Licences unchanged.

## Docs impact

User manual (CTO T091): recording works on the StemStudio computer (localhost) only, not from
another device on the network; technical manual: browser matrix, `StemStudio:Recording:*`
settings, the capture worklet file `audio-engine-capture-worklet.js`.

## Open issues / requests

1. S-01 (10 min real 48 kHz interface, CPU, Firefox mismatch) and S-02 (circuit + API) are open;
   assigned to T089 / T043 (recorded in research.md and tasks.md).
2. Re-merge after 022/023 and 014 (DELTA.md "Re-merge"); 014's architect owns 2.4.0 and 1.13.0.
3. CI wiring for the 015 contracts (devops T010 or the integration).
4. `docs/process/LESSONS.md` exists only on `022-023-round`; it reaches this round with `main`.

## Next owner

Orchestrator: re-merge after 022/023 + 014, then start the lane wave (split above).
