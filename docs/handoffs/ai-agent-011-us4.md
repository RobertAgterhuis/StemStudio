# Hand-off: ai-agent / 011 US4 (T064, T065)

Branch `011-lane-agent`. No LLM called: Claude only against a stub `HttpMessageHandler` (base address
`https://api.anthropic.com/`, nothing leaves the process), a sentinel key from a fake `IEnvironmentReader`; no real key
anywhere. Builds on `ai-agent-011-us3.md`.

## What was built

- **Claude backend** (`Backends/`, constitution 1.3.0 opt-in):
  - `ClaudeChatClientFactory(IEnvironmentReader, HttpClient? = null)`: reads `ANTHROPIC_API_KEY` only through the reader,
    per turn, hands it to the SDK and nowhere else; one `AnthropicClient` per key value (connections reused), `MaxRetries = 0`
    (no hidden retries of paid requests). `Create(model)` = `AsIChatClient(model, 8000, AnthropicThinkingMode.Adaptive)`
    wrapped in `ClaudeErrorMapping`; `Options(effort, maxOutputTokens)` sets `ChatOptions.Reasoning.Effort`
    (`output_config.effort`) and `MaxOutputTokens`; tool mode auto comes from `TurnBuilder` (asserted on the wire:
    `model`, `stream: true`, `max_tokens`, `output_config.effort`, `thinking.type: adaptive`, `tool_choice.type: auto`).
  - `ClaudeChatSession : IAgentChatSession`: key present and notice acknowledged, otherwise `BackendUnavailableException`
    (`ClaudeErrors.KeyMissing` / `NoticeMissing`); model and effort of the settings; `ClaudeTurnTimeoutSeconds`; **per-run
    budget**: before every request, input >= `ClaudeMaxInputTokensPerRun` or output >= `ClaudeMaxOutputTokensPerRun` ends
    the run `budget-exhausted` without a request; each turn may use only the remaining output budget (`max_tokens`).
  - `ClaudeErrorMapping` (`DelegatingChatClient`): `stop_reason: refusal` (MEAI `ContentFilter`) -> `refusal`;
    `AnthropicApiException` 401/403/429/5xx/other -> `backend-unavailable` with "... (HTTP n). Switch to local to continue
    with the model on this computer."; connection errors -> `ClaudeErrors.Unreachable`. The SDK exception is **not** kept as
    the cause (its message can contain the response body), so nothing of the response reaches logs or events; a test with a
    response body that echoes the sentinel key proves it is in no event, log line or failure message.
  - `ClaudeBackendProbe`: `Status(settings)` = key present / notice / model, **no request**; `TestAsync` only on the
    explicit test: `GET /v1/models/{id}` (2 s), readable detail, never the key or the body.
  - The selector never switches to Claude (or another paid model): a "must not be called" handler proves 0 requests for
    Ollama runs and for every status call.
- **`AgentStatusService : IAgentStatusService`** (`Backends/`): Ollama probed for every backend (2 s; Settings shows it),
  `ready`/`activeModel`/`usingFallback`/`reason`/`fix` per selected backend (Ollama: fallback model while `IGpuActivity`
  is busy, not ready when the fallback is not pulled; Claude: key + notice; none: `OffReason`/`OffFix`; fake: ready with model
  `scripted` when a readable script is configured). `TestAsync(ollama|claude)`, other backends `NotTestable`.
  `ListTools(context)` = registry offers with availability, using the service states the runner last read (shared
  `ServiceStatusReader` via `ToolCatalog`; "unknown" before the first run).
- **`UiToolGateway : IUiToolGateway`** (`Runtime/`, singleton): waits per (run, step), `TryComplete` once (false for
  unknown/other run/late answers -> 409 `ui-result-not-expected`), timeout -> `{ok:false, "The page did not respond within
  15 seconds."}`, cancellation ends the wait. The step executor now registers the wait **before** publishing
  `ui.requested`, so an immediate page answer is not lost (test with a sink that answers synchronously).
- **`AddStemStudioAgent()`** (all `TryAdd`): `IEnvironmentReader` (`ProcessEnvironmentReader`, blank = unset),
  `TimeProvider.System`, `IGpuActivity` (`IdleGpuActivity` default; the backend's own registration after the call wins),
  one `OllamaConnection` (`HttpClient`, base address from `StemStudio__Agent__Ollama__BaseUrl` via the reader, default
  `http://localhost:11434/`, trailing slash ensured, `Timeout = Infinite` because every call has its own bound - the 100 s
  default would otherwise cut 120 s turns and look like a user cancel), `OllamaChatClientFactory`, `OllamaBackendProbe`,
  `ILlmGpuRelease` (`OllamaGpuRelease`), `ClaudeChatClientFactory`, `ClaudeBackendProbe`, `ChatBackends` (Ollama, Claude
  session, **`Fake` = `ScriptedChatSession`** from the file named by `STEMSTUDIO_AGENT_FAKE_SCRIPT`; blank, missing or
  unreadable = no fake), `IAgentChatSession` = `AgentModelSelector` (routes each run by its backend, so a fake script can
  never answer an Ollama/Claude run), the four 001-003 + meta tool providers, `AgentToolRegistry`, `ServiceStatusReader`,
  `ToolCatalog`, `IUiToolGateway`, `BackendProbes`, `IAgentStatusService`, **one** `AgentRunService` singleton with
  `IAgentRunService` forwarding to it. The library registers **no** store, `IRunEventSink` or `IStemStudioActions`.
- Fix (backend requirement, qa us6 request 3): `approval.requested.device` is `gpu` or `cpu`
  (`ToolEstimate.DeviceCode`); the descriptive device text stays in tool results.

## Files changed

`src/StemStudio.Agent/AgentServiceCollectionExtensions.cs`;
`src/StemStudio.Agent/Backends/{AgentStatusService,ClaudeBackendProbe,ClaudeChatClientFactory,ClaudeChatSession,ClaudeErrorMapping,OllamaConnection,OllamaBackendProbe}.cs`;
`src/StemStudio.Agent/Runtime/{UiToolGateway,ProcessEnvironmentReader,IdleGpuActivity,ToolCatalog,AgentServices,AgentRunner,ServiceStatusReader,StepExecutor,ApprovalGate}.cs`;
`src/StemStudio.Agent/Tools/ToolInvocation.cs`; tests `AgentServiceCollectionExtensionsTests.cs`,
`Backends/{ClaudeBackendTests,AgentStatusServiceTests}.cs`, `Runtime/UiToolGatewayTests.cs`,
`Fakes/{FakeEnvironment,TestServiceProvider,StubHttpHandler,RecordingEventSink,RunnerHarness}.cs`,
`Tools/Providers/ProjectionEdgeTests.cs`, `Runtime/ApprovalFlowTests.cs`.

## Red -> green commits

| Pair | Red | Green |
|---|---|---|
| T064 / T065 | 25d82ee | dc58a45 |
| device cpu/gpu | 5f85594 | 6c958a2 |

## Tests and coverage

`dotnet test tests/StemStudio.Agent.Tests -p:CollectCoverage=true`: **474 passed**, 0 failed.
`StemStudio.Agent`: **line 98.21 %, branch 93.29 %, method 97.64 %** (gate 90/90 passed; measured at dc58a45, the
device fix adds covered lines only).

## Quality checks

Build 0 warnings (TreatWarningsAsErrors); `dotnet format --verify-no-changes` clean for src and tests.
`sig_check src/StemStudio.Agent`: 111 files, 367 units, **0 errors, 33 warnings** (all pre-existing, T079b).

## Dependencies added

None (Anthropic 13.3.0 was referenced by the skeleton). SDK facts verified here: `AnthropicApiException.StatusCode`,
`AnthropicIOException`, `ModelService.Retrieve(id, ...)`; with `ApiKey` set the SDK 13.3.0 sends the key as
`Authorization: Bearer` and `x-stainless-retry-count: 0`.

## Docs impact

Technical manual: `ANTHROPIC_API_KEY` must be in the environment of the API process (compose passes the host's variable
through at start); Claude requests are not retried; failures are readable and end with "Switch to local"; per-run token
budget (default 60,000 input / 8,000 output tokens). "Test connection" for Claude makes one free model lookup.

## Open issues / requests

1. **architect**: add `Microsoft.Extensions.DependencyInjection` (10.x, MIT) to `tests/StemStudio.Agent.Tests.csproj`; the
   DI tests use a small `Fakes/TestServiceProvider` over the abstractions until then.
2. **backend**: `IAgentRunService`, `IAgentStatusService`, `IUiToolGateway`, `ILlmGpuRelease`, `IEnvironmentReader` are now
   registered by `AddStemStudioAgent()`; `AgentStatusProbe` can stop falling back. The Ollama base URL is read from the
   environment variable `StemStudio__Agent__Ollama__BaseUrl` (no `IConfiguration` dependency in the library); if you
   want appsettings support, tell me and I add an `AddStemStudioAgent(Action<...>)` overload.
3. **architect**: the XML doc of `ApprovalRequestDto.Device` still says "GPU (RTX 4070)"; the wire value is `gpu`/`cpu`.
4. **qa** (us1 request 1): the fake session replaces `{{context.trackId}}` / `{{context.mixId}}` by a plain text replace in
   the script JSON (also inside longer strings). The committed scripts use them only as whole values, so the behaviour
   matches; a whole-value-only replacement follows with T079b.

## Next owner

ai-agent continues with US5 (T068, T069); backend T060/T061 can use `IAgentStatusService.TestAsync` and the Claude probe.
