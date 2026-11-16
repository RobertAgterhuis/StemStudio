# Hand-off: ai-agent / 011 US3 (T050, T051)

Branch `011-lane-agent`. No LLM called (stub `HttpMessageHandler`s only); no key anywhere. Builds on
`ai-agent-011-us2.md`.

## What was built (`src/StemStudio.Agent/Backends`)

- `OllamaChatClientFactory(HttpClient)`: `Create(model)` -> `OllamaApiClient(http, model)`; `Options(think, cpuOnly)` sets
  `num_ctx` 16,384 (`AddOllamaOption(OllamaOption.NumCtx, ...)`), `num_gpu` 0 only for the CPU fallback, and the
  top-level `think` / `keep_alive: "10m"` through `RawRepresentationFactory = _ => new ChatRequest {...}` (research F-13;
  no `DelegatingChatClient` shim needed). Tests capture the `/api/chat` body.
- `OllamaBackendProbe(HttpClient, TimeProvider)`: `GET /api/version`, `GET /api/tags`, `POST /api/show` per model
  (capability `tools`), 2 s budget in total; never pulls. Result `OllamaProbeResult(Status, Ready, Reason, Fix)`:
  - unreachable / hanging / broken JSON -> "Local AI not reachable at <base url>." + "Start Ollama on this computer
    (https://ollama.com) and keep it running.";
  - default model missing -> "Model qwen3.5:9b is not pulled." + "ollama pull qwen3.5:9b (6.6 GB)" (size known for the
    two default models, otherwise the bare command);
  - model without tools -> "Model X does not support tool calling." + "Choose a model with tool support in Settings,
    e.g. qwen3.5:9b.". A missing fallback model is reported only as `fallbackPresent: false`.
- `OllamaGpuRelease : ILlmGpuRelease`: `POST /api/generate {model, keep_alive: 0}` for the default and the fallback
  model (once when equal), 2 s budget, swallows HTTP errors and timeouts. The backend decides whether to call it
  (backend `ollama` and a GPU worker, DELTA.md section 5).
- `AgentModelSelector : IAgentChatSession` with `ChatBackends {Ollama, Claude?, Fake?}`: per turn, the run's backend;
  Ollama uses the default model, or the fallback on the CPU while `IGpuActivity.IsHeavyJobActive()` (change reason
  `FallbackReason`; the runner publishes `model.changed` when the model differs from the previous turn), with
  `TurnTimeoutSeconds` from the settings. `none`, an unconfigured Claude and a missing fake session throw
  `BackendUnavailableException` -> run `failed` / `backend-unavailable` with the readable message. The selector never
  switches to Claude on its own.

  The runner maps `BackendUnavailableException` (thrown by a chat session when selecting the turn) to `RunStopException`
  (`backend-unavailable`), so the terminal `run.failed` event carries the readable message.

## Files changed

`src/StemStudio.Agent/Backends/{OllamaChatClientFactory,OllamaBackendProbe,OllamaGpuRelease,AgentModelSelector}.cs`,
`src/StemStudio.Agent/Runtime/{BackendUnavailableException,AgentRunner}.cs`,
`tests/StemStudio.Agent.Tests/Backends/OllamaBackendTests.cs`, `tests/StemStudio.Agent.Tests/Fakes/StubHttpHandler.cs`.

## Red -> green commits

| Pair | Red | Green |
|---|---|---|
| T050 / T051 | bc31627 | 8815c3f |

Review before the green commit: the GPU-release de-duplication test could not fail (the stub threw on the first request);
split into a de-duplication test against a succeeding stub and a separate "errors swallowed" test. Probe and release
bodies split below 15 LOC; the backend-unavailable catch moved out of `RunGuardedAsync` into a `NextTurnAsync` wrapper.

## Tests and coverage

`dotnet test tests/StemStudio.Agent.Tests -p:CollectCoverage=true`: **416 passed**, 0 failed.
`StemStudio.Agent`: **line 98.06 %, branch 93.95 %, method 97.05 %** (gate 90/90 passed).

## Quality checks

Build 0 warnings; `dotnet format --verify-no-changes` clean for `src/StemStudio.Agent` and the tests (working-tree CRLF
normalised to LF per `.gitattributes`). `uv run --no-project --with lizard==1.24.3 python tools/quality/sig_check.py
src/StemStudio.Agent`: 101 files, 319 units, **0 errors, 33 warnings** (unchanged, T079b).

## Dependencies added

None (OllamaSharp 5.5.0 was already referenced by the skeleton).

## Docs impact

Setup: Ollama on the host (`StemStudio__Agent__Ollama__BaseUrl`), `ollama pull qwen3.5:9b` (6.6 GB) and `qwen3.5:4b`
(3.4 GB) for the CPU fallback; the unavailable texts above are what the panel shows.

## Open issues / requests

1. **backend**: create one `HttpClient` with `BaseAddress = StemStudio__Agent__Ollama__BaseUrl` (trailing slash) for the
   factory, probe and release; `GET /api/agent/status` can build `AgentStatusDto.Ollama` and `reason`/`fix` from
   `OllamaProbeResult` until `IAgentStatusService` is delivered by this lane (with US4, together with the Claude probe
   and the DI registration in `AddStemStudioAgent()`).

## Next owner

ai-agent continues with US4 (T064, T065); backend T052-T055 can build on the probe and release.
