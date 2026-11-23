# Hand-off: backend / 016 US4 (T095, T096)

Lane L4, branch `016-round-lane-l4`. Not pushed, not merged. Contracts frozen; no deviations. No EF model change.

## Tasks (red -> green)

| Task | Red | Green |
|---|---|---|
| T095/T096 `getVocalPitch`, `analyseVocalPitch`, `deleteVocalPitch`: source `vocals.lead` when the vocals split kept a lead stem, else `vocals`; 409 `no-vocals`/`track-not-ready`/`analysis-running`, 503, estimate on 202, never automatic, the GET compressed (gzip/Brotli) and no other response compressed; ids un-staged | `3338a58b` | `fe179a49` |

## Tests and coverage (coverlet gate, Release, full run at `fe179a49`)

| Component | Tests | Line | Branch |
|---|---|---|---|
| StemStudio.Domain | 4198 passed | 99.13 % | 94.19 % |
| StemStudio.Persistence | 179 passed | 99.48 % | 95.23 % |
| StemStudio.Api | 3364 passed, 4 skipped (existing platform skips) | 98.54 % | 90.74 % |
| StemStudio.Contracts | (via Api tests) | 100 % | 100 % |

Quality: build 0 warnings; `dotnet format --verify-no-changes` clean; `tools/quality/run_all.py` PASS (0 errors, 462 clones, so jscpd ran).

## Decisions

1. The reference melody uses the shared `MapPlainAnalysis` (now in `PlainAnalysisEndpoints.cs`). Drum hits and the melody differ only in route, kind, names and text.
2. Response compression is in the shared framework (`Microsoft.AspNetCore.ResponseCompression`), so no package was added. It is scoped to `GET /api/tracks/{id}/vocal-pitch` through `UseWhen`, with the JSON MIME type only. Stems (range requests), score files and every other response stay uncompressed, and a test checks this. `EnableForHttps` is on: the melody holds no secret and reflects no request text, so it gives no BREACH oracle. The middleware is added by an `IStartupFilter` registered in `AddInstrumentViews`, so `ApiComposition` does not grow (as the orchestrator asked for the SIG findings).
3. A 90,000-frame melody (the contract maximum) compresses to under 200 KB in the test.

## Security self-review (US4)

No new request input: there is no body, and the only input is the route GUID. Compression is limited to one non-secret JSON response (see decision 2). Worker results stay bounded by the foundational applier (frames <= 90,000).

## Dependencies

None added (ResponseCompression is part of `Microsoft.AspNetCore.App`).
