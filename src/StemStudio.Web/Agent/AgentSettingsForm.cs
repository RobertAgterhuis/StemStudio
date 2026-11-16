using StemStudio.Contracts.Agent;

namespace StemStudio.Web.Agent;

/// <summary>
/// The editable agent settings of the settings page (US4, FR-019, FR-020): backend, models and the Claude options.
/// Claude can be saved only after its paid-service notice was acknowledged (now or before).
/// </summary>
public sealed class AgentSettingsForm
{
    /// <summary>Shown when Claude is saved without the acknowledgement.</summary>
    public const string AcknowledgeMessage = "Acknowledge the notice about the paid Claude service before saving.";

    /// <summary>The selected backend.</summary>
    public AgentBackendDto Backend { get; set; }

    /// <summary>The default Ollama model.</summary>
    public string OllamaModel { get; set; } = string.Empty;

    /// <summary>The Ollama model used while a GPU job runs.</summary>
    public string OllamaFallbackModel { get; set; } = string.Empty;

    /// <summary>The Claude model.</summary>
    public ClaudeModelDto ClaudeModel { get; set; }

    /// <summary>The Claude effort.</summary>
    public ClaudeEffortDto ClaudeEffort { get; set; }

    /// <summary>True when the notice is acknowledged (stored, or ticked now).</summary>
    public bool NoticeAcknowledged { get; set; }

    /// <summary>Starts from the stored settings.</summary>
    /// <param name="settings">The stored settings.</param>
    /// <returns>The form.</returns>
    public static AgentSettingsForm From(AgentSettingsDto settings) => new()
    {
        Backend = settings.Backend,
        OllamaModel = settings.OllamaModel,
        OllamaFallbackModel = settings.OllamaFallbackModel,
        ClaudeModel = settings.ClaudeModel,
        ClaudeEffort = settings.ClaudeEffort,
        NoticeAcknowledged = settings.ClaudeNoticeAcknowledged,
    };

    /// <summary>The model choices: the tool-capable pulled models, plus the stored one when it is missing.</summary>
    /// <param name="ollama">The Ollama status, or null.</param>
    /// <param name="current">The stored model.</param>
    /// <returns>The model names.</returns>
    public static IReadOnlyList<string> ModelChoices(OllamaStatusDto? ollama, string current)
    {
        var models = (ollama?.Models ?? []).Where(m => m.Tools).Select(m => m.Name).ToList();
        return models.Contains(current, StringComparer.Ordinal) || current.Length == 0 ? models : [.. models, current];
    }

    /// <summary>The reason the form cannot be saved, or null.</summary>
    /// <returns>The message.</returns>
    public string? Validate() => Backend == AgentBackendDto.Claude && !NoticeAcknowledged ? AcknowledgeMessage : null;

    /// <summary>The update request.</summary>
    /// <returns>The request.</returns>
    public UpdateAgentSettingsRequest ToRequest() => new()
    {
        Backend = Backend,
        OllamaModel = OllamaModel,
        OllamaFallbackModel = OllamaFallbackModel,
        ClaudeModel = ClaudeModel,
        ClaudeEffort = ClaudeEffort,
        AcknowledgeClaudeNotice = Backend == AgentBackendDto.Claude && NoticeAcknowledged,
    };
}
