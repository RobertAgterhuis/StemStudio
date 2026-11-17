using System.Text.Json;
using Microsoft.Extensions.AI;
using StemStudio.Agent.Backends;
using StemStudio.Agent.Ports;
using StemStudio.Agent.Runtime;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Agent.LyricAssist;

/// <summary>Picks the backend and model of the next lyric model call (implemented by every <see cref="ILyricModel"/> of this library).</summary>
public interface ILyricModelSelection
{
    /// <summary>The backend and model the next call would use.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The model.</returns>
    /// <exception cref="LyricAssistException">No model backend is selected (<c>backend-unavailable</c>).</exception>
    Task<LyricModelInfo> SelectAsync(CancellationToken cancellationToken);
}

/// <summary>The chat client factories the lyric model client may use.</summary>
public sealed record LyricModelBackends
{
    /// <summary>Local Ollama (default).</summary>
    public required OllamaChatClientFactory Ollama { get; init; }

    /// <summary>Optional paid Claude backend (constitution 1.4.0); used only while Claude is the selected backend.</summary>
    public required ClaudeChatClientFactory Claude { get; init; }
}

/// <summary>
/// The real <see cref="ILyricModel"/> (ADR-037 D4, research R-05, FR-021, FR-025): the 011 selection rules - the agent backend
/// of the settings, the default Ollama model on the GPU or the fallback model on the CPU while a heavy GPU job runs, Claude only
/// when it is the selected backend (never automatically). The call is tool-less for Ollama (<c>format</c> = the output schema,
/// <c>think: false</c>, <c>num_ctx</c> 16384) and has exactly one forced tool <c>emit_suggestions</c> for Claude (thinking off,
/// because a forced tool choice does not allow it). Only the prompts are sent: no files, no secrets.
/// </summary>
/// <param name="settings">Agent settings (backend and models).</param>
/// <param name="gpu">Heavy GPU job state (fallback model).</param>
/// <param name="backends">Client factories.</param>
public sealed class LyricModelClient(IAgentSettingsStore settings, IGpuActivity gpu, LyricModelBackends backends) : ILyricModel, ILyricModelSelection
{
    /// <summary>Message when no model backend is selected.</summary>
    public const string NoBackend = "No language model is selected for the lyric assistant: choose Local AI (Ollama) in Settings.";

    /// <summary>Message when the backend changed while an assist runs.</summary>
    public const string BackendChanged = "The model backend changed while the assist was running; start the assist again.";

    /// <summary>Message when a heavy GPU job starts while an assist on the GPU model runs (the CPU fallback is not used for lyrics, CTO 023).</summary>
    public const string GpuBecameBusy = "The GPU became busy with another job while the assist was running; start the assist again when that job is done.";

    /// <summary>Description of the forced Claude tool.</summary>
    public const string ToolDescription = "Return the review of the marked block as items and misses.";

    private static readonly JsonElement Schema = JsonDocument.Parse(LyricAssistOutputSchema.Text).RootElement.Clone();

    /// <inheritdoc />
    public async Task<LyricModelInfo> SelectAsync(CancellationToken cancellationToken)
    {
        var current = await settings.GetAsync(cancellationToken);
        return current.Backend switch
        {
            AgentBackendDto.Ollama => OllamaInfo(current),
            AgentBackendDto.Claude => new LyricModelInfo { Backend = AgentBackendDto.Claude, Model = ClaudeChatClientFactory.ModelId(current.ClaudeModel), UsingFallback = false },
            _ => throw new LyricAssistException(AssistFailureCodeDto.BackendUnavailable, NoBackend, "Choose Local AI (Ollama) in Settings."),
        };
    }

    /// <inheritdoc />
    public async Task<LyricModelReply> CompleteAsync(LyricModelRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var model = await SelectAsync(cancellationToken);
        if (request.Model is { } pinned && pinned.Backend != model.Backend)
        {
            throw new LyricAssistException(AssistFailureCodeDto.BackendUnavailable, BackendChanged);
        }

        if (request.Model is { UsingFallback: false } && model.UsingFallback)
        {
            throw new LyricAssistException(AssistFailureCodeDto.BackendUnavailable, GpuBecameBusy);
        }

        ChatMessage[] messages = [new(ChatRole.System, request.SystemPrompt), new(ChatRole.User, request.UserPrompt)];
        var (client, options) = model.Backend == AgentBackendDto.Claude ? ClaudeCall(model, request) : OllamaCall(model, request);
        var response = await SendAsync(client, messages, options, cancellationToken);
        return new LyricModelReply
        {
            Json = model.Backend == AgentBackendDto.Claude ? ToolInput(response) : response.Text,
            Model = model,
            Usage = model.Backend == AgentBackendDto.Claude ? Usage(response.Usage) : null,
        };
    }

    /// <summary>The Ollama options of a lyric call (for tests): no thinking, the output schema as <c>format</c>, the kind's temperature.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cpuOnly">True for the CPU fallback model.</param>
    /// <returns>The options.</returns>
    public static ChatOptions OllamaOptions(LyricModelRequest request, bool cpuOnly)
    {
        ArgumentNullException.ThrowIfNull(request);
        var options = OllamaChatClientFactory.Options(think: false, cpuOnly);
        options.Temperature = (float)request.Temperature;
        options.ResponseFormat = ChatResponseFormat.ForJsonSchema(Schema, "lyric_assist_output", "Lyric assistant output");
        return options;
    }

    /// <summary>The Claude options of a lyric call: one forced tool whose input schema is the output schema, thinking off.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The options.</returns>
    public static ChatOptions ClaudeOptions(LyricModelRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new ChatOptions
        {
            Temperature = (float)request.Temperature,
            MaxOutputTokens = request.MaxOutputTokens ?? LyricAssistLimits.DefaultMaxOutputTokensPerAssist,
            Reasoning = new ReasoningOptions { Effort = ReasoningEffort.None },
            Tools = [AIFunctionFactory.CreateDeclaration(LyricAssistOutputSchema.ClaudeToolName, ToolDescription, Schema)],
            ToolMode = ChatToolMode.RequireSpecific(LyricAssistOutputSchema.ClaudeToolName),
        };
    }

    private LyricModelInfo OllamaInfo(Model.AgentSettings current)
    {
        var busy = gpu.IsHeavyJobActive();
        return new LyricModelInfo { Backend = AgentBackendDto.Ollama, Model = busy ? current.OllamaFallbackModel : current.OllamaModel, UsingFallback = busy };
    }

    private (IChatClient Client, ChatOptions Options) OllamaCall(LyricModelInfo model, LyricModelRequest request) =>
        (backends.Ollama.Create(model.Model), OllamaOptions(request, model.UsingFallback));

    private (IChatClient Client, ChatOptions Options) ClaudeCall(LyricModelInfo model, LyricModelRequest request) =>
        (CreateClaude(model.Model), ClaudeOptions(request));

    private IChatClient CreateClaude(string model)
    {
        try
        {
            return backends.Claude.Create(model);
        }
        catch (BackendUnavailableException error)
        {
            throw new LyricAssistException(AssistFailureCodeDto.BackendUnavailable, error.Message);
        }
    }

    private static async Task<ChatResponse> SendAsync(IChatClient client, ChatMessage[] messages, ChatOptions options, CancellationToken cancellationToken)
    {
        try
        {
            var updates = await client.GetStreamingResponseAsync(messages, options, cancellationToken).ToListAsync(cancellationToken);
            return updates.ToChatResponse();
        }
        catch (RunStopException stop)
        {
            throw new LyricAssistException(stop.Code == AgentFailureCodes.Refusal ? AssistFailureCodeDto.Refusal : AssistFailureCodeDto.BackendUnavailable, stop.Message);
        }
        catch (HttpRequestException)
        {
            throw new LyricAssistException(AssistFailureCodeDto.BackendUnavailable, "The local model (Ollama) could not be reached.", "Start Ollama and pull the model in Settings.");
        }
    }

    /// <summary>The arguments of the forced tool call as JSON text (empty object text when the model did not call it: invalid output).</summary>
    private static string ToolInput(ChatResponse response) =>
        response.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>().FirstOrDefault(call => call.Name == LyricAssistOutputSchema.ClaudeToolName) is { } call
            ? JsonSerializer.Serialize(call.Arguments ?? new Dictionary<string, object?>())
            : string.Empty;

    private static TokenUsageDto Usage(UsageDetails? usage) => new()
    {
        InputTokens = usage?.InputTokenCount ?? 0,
        OutputTokens = usage?.OutputTokenCount ?? 0,
        CachedInputTokens = usage?.CachedInputTokenCount ?? 0,
    };
}
