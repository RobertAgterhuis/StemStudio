using StemStudio.Agent.LyricAssist;
using StemStudio.Agent.Ports;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Domain.Lyrics.Assist;

namespace StemStudio.Api.LyricAssist;

/// <summary>The model layer of one assist.</summary>
/// <param name="Assistant">The assistant, or null when the model layer is unavailable.</param>
/// <param name="Backend">The selected 011 backend.</param>
/// <param name="Reason">Why it is unavailable.</param>
/// <param name="Fix">How to make it available.</param>
public sealed record ModelLayerState(ILyricAssistant? Assistant, AgentBackendDto Backend, string? Reason, string? Fix)
{
    /// <summary>True when the model layer can run.</summary>
    public bool Available => Assistant is not null;

    /// <summary>The model layer as shown in the pane.</summary>
    /// <returns>The layer.</returns>
    public AssistLayer Layer() => new(SuggestionSource.Model, LyricModelLayer.Name(Backend), Available, Available ? null : Reason, Available ? null : Fix);
}

/// <summary>
/// Picks the model layer through the ai-agent <see cref="ILyricAssistantFactory"/> (011 backend selection; fake scripts in E2E) and
/// knows the lyric-assistant Claude notice (FR-025, FR-026). Without a registered factory the model layer is unavailable.
/// </summary>
/// <param name="services">Services (the factory is optional).</param>
/// <param name="agentSettings">011 settings (selected backend).</param>
public sealed class LyricModelLayer(IServiceProvider services, IAgentSettingsStore agentSettings)
{
    /// <summary>Fix when no assistant is installed.</summary>
    public const string NotInstalledFix = "Choose Local AI (Ollama) in Settings to use the model layer.";

    /// <summary>The model layer of an assist (one assistant instance per assist).</summary>
    /// <param name="sheetTitle">Sheet title (selects a fake script).</param>
    /// <param name="kind">Kind.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The state.</returns>
    public async Task<ModelLayerState> ResolveAsync(string sheetTitle, AssistKind kind, CancellationToken cancellationToken)
    {
        var backend = (await agentSettings.GetAsync(cancellationToken)).Backend;
        if (services.GetService<ILyricAssistantFactory>() is not { } factory)
        {
            return new ModelLayerState(null, backend, LyricAssistProblemTypes.ModelUnavailable, NotInstalledFix);
        }

        var resolution = await factory.ResolveAsync(new LyricAssistModelContext(sheetTitle, LyricAssistMapper.Map<AssistKindDto>(kind)), cancellationToken);
        return new ModelLayerState(resolution.Assistant, backend, resolution.UnavailableReason, resolution.Fix);
    }

    /// <summary>The selected 011 backend.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The backend.</returns>
    public async Task<AgentBackendDto> BackendAsync(CancellationToken cancellationToken) => (await agentSettings.GetAsync(cancellationToken)).Backend;

    /// <summary>Display name of a backend's model layer.</summary>
    /// <param name="backend">Backend.</param>
    /// <returns>The name.</returns>
    public static string Name(AgentBackendDto backend) => backend switch
    {
        AgentBackendDto.Ollama => "Local AI (Ollama)",
        AgentBackendDto.Claude => "Claude (Anthropic)",
        AgentBackendDto.Fake => "Scripted model (tests)",
        _ => "Language model (none selected)",
    };

    /// <summary>The deterministic layers of a language.</summary>
    /// <param name="language">Language.</param>
    /// <returns>Dictionary and rules layers.</returns>
    public static IReadOnlyList<AssistLayer> Deterministic(AssistLanguage language) =>
    [
        new(SuggestionSource.Dictionary, language == AssistLanguage.Dutch ? "OpenTaal (nl)" : "SCOWL + CMUdict (en)", true),
        new(SuggestionSource.Rules, "Poetic-licence rules", true),
    ];
}
