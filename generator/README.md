# StemStudio generator (optional, GPU-only)

Internal FastAPI service for the generative features: style transfer (feature 009) and songs from
own lyrics (feature 010) with ACE-Step 1.5, and own-voice conversion with SoulX-Singer-SVC (the
`singer` deployment of the same code base). Contract:
`specs/010-lyrics-to-song/contracts/generator-api.openapi.yaml` (1.1.0, contains 009's 1.0.0).
Decisions: ADR-018, ADR-019.

- Base install (`uv sync`): no torch, used by CI, E2E and the `fake` image variant
  (`STEMSTUDIO_GENERATOR=fake`).
- `uv sync --extra acestep` (Linux x86_64): ACE-Step 1.5 at commit `ca1e85f`, torch
  2.10.0+cu128; image `stemstudio/generator:local`.
- `uv sync --extra soulx` (Linux x86_64): transformers 4.41.2 and the SVC runtime; the SoulX-Singer
  source (commit `81aeb3a`) is added by the image build; image `stemstudio/singer:local`.

The two extras conflict (transformers 4.41 vs >= 4.51) and are never installed together.

```sh
uv sync --frozen
uv run --frozen pytest --cov --cov-branch
uv run --frozen ruff check . && uv run --frozen ruff format --check . && uv run --frozen mypy src tests
```
