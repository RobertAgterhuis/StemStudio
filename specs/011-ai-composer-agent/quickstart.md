# Quickstart / validation: AI Composer Agent (feature 011)

Validation script for the CTO after the integration wave. A1-A6 run anywhere (fake backend, no
LLM); A7-A13 need the reference machine with Ollama; A14 is optional and paid (owner only).

## Prerequisites

- Ollama on the host (reference machine: 0.40.16, `E:\AI\Apps\Ollama\ollama.exe`), models pulled
  by the user: `ollama pull qwen3.5:9b` (6.6 GB) and `ollama pull qwen3.5:4b` (3.4 GB). Check:
  `curl http://localhost:11434/api/tags`.
- Docker Desktop resolves `host.docker.internal` (research F-10). On Linux engines compose adds
  `host-gateway`.
- Optional Claude: `ANTHROPIC_API_KEY` exported **in the shell** that runs `docker compose up`
  (never in `.env`, compose files or scripts). Amendment ratified (constitution 1.3.0).

## A. Automated (CI-equivalent, no LLM)

| # | Step | Expected |
|---|---|---|
| A1 | `dotnet build StemStudio.slnx` | 0 warnings |
| A2 | `dotnet test` (all .NET test projects, coverage) | green; `StemStudio.Agent` >= 90 % line and branch; Api/Web/Persistence gates unchanged |
| A3 | Redocly lint of `specs/011-ai-composer-agent/contracts/backend-api.openapi.yaml` | valid OpenAPI 3.1 |
| A4 | `python tools/quality/run_all.py` | 0 errors, incl. the `StemStudio.Agent` dependency rules |
| A5 | `STEMSTUDIO_SEPARATOR=fake STEMSTUDIO_AGENT_BACKEND=fake docker compose up -d --build --wait`, then Playwright default suite + `agent.spec.ts` | all green (panel, plan, approvals, reject, undo, UI tools on the mixer, unavailable state) |
| A6 | Same stack with `STEMSTUDIO_AGENT_BACKEND=none` | full existing suite green; `agent-unavailable` shown (SC-005) |

## B. Reference machine (local model)

| # | Step | Expected |
|---|---|---|
| A7 | Default start `docker compose up --build`; open http://localhost:8080; open the panel | badge `Ollama (qwen3.5:9b)`, ready within 3 s |
| A8 | Stop Ollama (tray -> Quit); reopen the panel | "Local AI not reachable" + fix within 3 s; import/mixer still work (SC-005) |
| A9 | `ollama rm` is **not** needed: in Settings choose a non-pulled model name | 422 "model not available" with the `ollama pull` command |
| A10 | Send case 1, 3, 6, 7, 13 (approve), 14 (reject), 16 (approve) of the evaluation set by hand | plan + steps visible; confirmations as listed; state matches |
| A11 | Run `dotnet run --project tools/agent-eval -- --backend ollama --model qwen3.5:9b --runs 3 --out tools/agent-eval/out/<date>-<model>-run<n>.md` (outside Docker: `StemStudio__Agent__Ollama__BaseUrl=http://localhost:11434/`) | report at `--out` (default `agent-eval-report.md`), history kept in `tools/agent-eval/out/`; median success >= 85 %, nl and en >= 80 % (SC-001); latency p50 <= 3 s warm (SC-004) |
| A12 | GPU stack (`-f docker-compose.gpu.yml`); with the panel used (model loaded), start "split further" on a fresh track; watch `nvidia-smi` and `ollama ps` | Ollama model unloaded before the job (`ollama ps` empty); Mega 53 step on the GPU (no "Not enough free GPU memory" reason) (SC-008); chatting during the job shows `qwen3.5:4b` in the badge |
| A13 | Disconnect the network; send cases 3, 6, 11 | work as online (US3-3) |

## C. Optional, paid (owner)

| # | Step | Expected |
|---|---|---|
| A14 | Export `ANTHROPIC_API_KEY` in the shell, restart the stack; Settings -> Claude (acknowledge the notice); "Test connection"; send the A10 cases by hand (the harness does not wire `--backend claude`; it exits 2) | key never visible in UI/API/logs; usage shown per run; success >= 95 %; switch back to Local |
| A15 | Unset the key, restart | Claude "not configured", not selectable; no request to `api.anthropic.com` (SC-006) |
