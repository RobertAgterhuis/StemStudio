# ADR-004: Stem storage and serving

- **Status**: Accepted
- **Date**: 2026-11-14
- **Decider**: architect agent, on the CTO brief

## Context

Stems are FLAC (ADR-001). The browser audio engine must decode whole stems; Blazor Server runs
over a SignalR circuit that is unsuited to megabytes of binary data.

## Decision

- After a job completes, the API copies the stems from the worker into its own storage
  `<DataDir>/tracks/<trackId>/stems/<name>.flac` and the result into SQLite. The API is the
  system of record; the worker cache may be wiped without losing tracks.
- `GET /api/tracks/{id}/stems/{name}` serves the file with `Content-Type: audio/flac`, HTTP Range
  (`enableRangeProcessing`), ETag and Last-Modified. Stem names are validated against the enum;
  paths are built from the GUID and the enum only.
- CORS: a named policy allows `GET` from `StemStudio:WebOrigin`. The engine fetches stems directly
  from `StemStudio:PublicApiBaseUrl` (browser view); the Web server uses `StemStudio:ApiBaseUrl`.

## Consequences

- No audio crosses the SignalR circuit; browsers cache stems via ETag.
- Two base URLs must be configured correctly in compose (internal vs published port).
- Disk use roughly doubles while the worker cache is kept (acceptable for a local MVP).
