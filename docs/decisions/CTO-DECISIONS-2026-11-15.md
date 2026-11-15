# CTO decisions taken under the owner's autonomous mandate (2026-11-15)

Owner, 2026-11-15 14:17: "implement all features that are now described without my intervention;
run features in parallel where possible". Open owner questions from the 004-011 architects were
decided as follows; the owner can review and reverse any of them.

| Feature | Question | Decision |
|---|---|---|
| 011 | Ratify the optional Claude backend amendment | Yes, constitution 1.3.0 (owner chose "both, configurable") |
| 011 | Default Claude model | `claude-opus-5-5`, configurable to `claude-sonnet-5-5` in settings |
| 009/010 | Generative services may be GPU-only | Yes, constitution 1.3.0 narrow exception; disabled with reason without GPU |
| 010 | Fourth container `singer` (transformers conflict) | Accepted |
| 010 | Consent statement wording EN/NL | Architect draft (010 data-model 3.3) accepted; owner may edit later |
| 006 | Alignment test clip for SC-003 | qa selects a CC0/public-domain sung recording (or synthesises one); no third-party lyrics in the repo |
| 005 | `STEMSTUDIO_CONTACT_URL` | Generic default without personal data; owner can configure later |
| 004 | Owner design approval (T038) before visual baselines | CTO approves; screenshots are kept in `docs/design-review/` for later owner review |
| 008 | Quality level, drum prerequisite, piano fast default, asset retention | Architect decisions accepted |
| 007 | Assets survive track deletion; 1 GiB free-disk guard | Accepted |
| 009 | 21 packages in worker/uv.lock younger than 2 weeks (pydantic 2.14.3, onnxruntime 1.32.0, ...) | Accepted as locked by earlier features; the >= 2-week rule applies to new pins and upgrades. fastapi was moved to 0.162.0. Re-check at the next dependency refresh |
| 009 | Worker GPU file lease: polling (not strict FIFO), CPU fallbacks after releasing the file lease, unreadable lock file -> GPU without cross-container lease | Accepted; ADR-018 D3 wording to be aligned in the 009 integration |
| 009/010 | Clones between worker and generator production code (config env helpers, DeviceInfo/Problem schemas) | Accepted as flagged warnings (separate services may not import each other; no shared package in the plan) |
| 010 | WebM/Opus voice samples cannot be read by soundfile | Decode with ffmpeg via an argument list (no shell); this meets the intent of "never a shell" |
