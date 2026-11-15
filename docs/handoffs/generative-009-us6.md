# Hand-off: generative / 009 US6 - availability and GPU-required refusals (T051-T052)

## What was built

`tests/api/test_health.py` (T051) covers:

- `/health` on a ready fake stack: engine, kinds, `voiceSampleCheck`, `ready`/`reason`, the four
  ACE-Step models, and the queue.
- The fake `no-gpu` mode: `ready: false` with the 003 text `NOT_AVAILABLE`, and a CPU device.
- `409 gpuRequired` for a create without a GPU. The body is exactly the contract problem, and no
  job is stored.
- The real `acestep` engine without torch: not ready, with `NO_CUDA_BUILD`, and every model
  `present: false`.
- A real GPU probe result (monkeypatched) makes the service ready.

Every body is validated against the `GeneratorHealth` schema (1.1.0).

**T052**: no code change was needed; T033 already delivered the behaviour. The tests passed on
their first run (`e0c2fa4`). This is recorded here as a TDD deviation.

## Tests and coverage / Quality checks

See `generative-009-010-polish.md`.

## Next owner

CTO; frontend/backend consume `GET /health` via `GET /api/generation/status`.
