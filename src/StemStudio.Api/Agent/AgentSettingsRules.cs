using StemStudio.Agent.Model;
using StemStudio.Contracts.Agent;

namespace StemStudio.Api.Agent;

/// <summary>Validation and merging of <c>PUT /api/agent/settings</c> (data-model 2, backend 1.10.0 <c>AgentLimits</c>).</summary>
public static class AgentSettingsRules
{
    /// <summary>Longest model name.</summary>
    public const int MaxModelNameLength = 100;

    private static readonly (string Field, Func<AgentLimitsDto, int> Value, int Min, int Max)[] LimitRanges =
    [
        ("limits.maxModelTurns", l => l.MaxModelTurns, 1, 30),
        ("limits.maxToolCalls", l => l.MaxToolCalls, 1, 50),
        ("limits.maxInvalidCalls", l => l.MaxInvalidCalls, 1, 10),
        ("limits.turnTimeoutSeconds", l => l.TurnTimeoutSeconds, 10, 600),
        ("limits.claudeTurnTimeoutSeconds", l => l.ClaudeTurnTimeoutSeconds, 10, 600),
        ("limits.claudeMaxInputTokensPerRun", l => l.ClaudeMaxInputTokensPerRun, 1000, 1_000_000),
        ("limits.claudeMaxOutputTokensPerRun", l => l.ClaudeMaxOutputTokensPerRun, 500, 128_000),
        ("limits.maxJobWaitMinutes", l => l.MaxJobWaitMinutes, 1, 180),
    ];

    /// <summary>400 problems of the request shape (fake backend, empty or long model names).</summary>
    /// <param name="request">The update.</param>
    /// <returns>The problem, or null.</returns>
    public static IResult? ShapeProblem(UpdateAgentSettingsRequest? request) => request switch
    {
        null => ApiProblems.Validation("body", "Send the settings to change."),
        { Backend: AgentBackendDto.Fake } => ApiProblems.Validation("backend", "Choose ollama, claude or none."),
        _ when !IsModelName(request.OllamaModel) => ApiProblems.Validation("ollamaModel", "The model name has 1 to 100 characters."),
        _ when !IsModelName(request.OllamaFallbackModel) => ApiProblems.Validation("ollamaFallbackModel", "The model name has 1 to 100 characters."),
        _ => null,
    };

    /// <summary>422 when a limit is outside its range.</summary>
    /// <param name="limits">The limits, or null when unchanged.</param>
    /// <returns>The problem, or null.</returns>
    public static IResult? LimitProblem(AgentLimitsDto? limits)
    {
        if (limits is null)
        {
            return null;
        }

        var broken = LimitRanges.Where(range => range.Value(limits) < range.Min || range.Value(limits) > range.Max).ToList();
        return broken.Count == 0 ? null : AgentProblems.Unprocessable(broken[0].Field, $"Must be from {broken[0].Min} to {broken[0].Max}.");
    }

    /// <summary>The settings with the request applied.</summary>
    /// <param name="current">Stored settings.</param>
    /// <param name="request">The update.</param>
    /// <param name="now">Current time.</param>
    /// <returns>The merged settings.</returns>
    public static AgentSettings Merge(AgentSettings current, UpdateAgentSettingsRequest request, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(request);
        return MergeClaude(MergeOllama(current, request), request, now) with { Limits = request.Limits ?? current.Limits, UpdatedAt = now };
    }

    private static AgentSettings MergeOllama(AgentSettings current, UpdateAgentSettingsRequest request) => current with
    {
        Backend = request.Backend ?? current.Backend,
        OllamaModel = Trimmed(request.OllamaModel) ?? current.OllamaModel,
        OllamaFallbackModel = Trimmed(request.OllamaFallbackModel) ?? current.OllamaFallbackModel,
        OllamaThink = request.OllamaThink ?? current.OllamaThink,
    };

    private static string? Trimmed(string? name) => name?.Trim();

    private static AgentSettings MergeClaude(AgentSettings current, UpdateAgentSettingsRequest request, DateTimeOffset now) => current with
    {
        ClaudeModel = request.ClaudeModel ?? current.ClaudeModel,
        ClaudeEffort = request.ClaudeEffort ?? current.ClaudeEffort,
        ClaudeNoticeAcknowledgedAt = request.AcknowledgeClaudeNotice ? now : current.ClaudeNoticeAcknowledgedAt,
    };

    /// <summary>The settings DTO (never the key).</summary>
    /// <param name="settings">The settings.</param>
    /// <returns>The DTO.</returns>
    public static AgentSettingsDto ToDto(AgentSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new AgentSettingsDto
        {
            Backend = settings.Backend,
            OllamaModel = settings.OllamaModel,
            OllamaFallbackModel = settings.OllamaFallbackModel,
            OllamaThink = settings.OllamaThink,
            ClaudeModel = settings.ClaudeModel,
            ClaudeEffort = settings.ClaudeEffort,
            ClaudeNoticeAcknowledged = settings.ClaudeNoticeAcknowledgedAt is not null,
            Limits = settings.Limits,
            UpdatedAt = settings.UpdatedAt,
        };
    }

    private static bool IsModelName(string? name) => name is null || name.Trim().Length is > 0 and <= MaxModelNameLength;
}
