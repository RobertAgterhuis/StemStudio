using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Api.Agent.Stores;
using StemStudio.Contracts.Agent;

namespace StemStudio.Api.Agent;

/// <summary>
/// <c>GET/PUT /api/agent/settings</c> and <c>POST /api/agent/settings/test</c> (FR-018, FR-019, SC-006). Claude is accepted only
/// when <c>ANTHROPIC_API_KEY</c> is present and the notice is acknowledged; the key is only checked for presence, never stored,
/// logged or returned. Ollama models are validated against the probe only when they change (CI has no Ollama).
/// </summary>
/// <param name="settings">Settings store (the stored choice; the runtime reads it through <see cref="IAgentSettingsStore"/>).</param>
/// <param name="probe">Status probe (Ollama model list, backend test).</param>
/// <param name="environment">Reads <c>ANTHROPIC_API_KEY</c> for presence only.</param>
/// <param name="clock">Clock.</param>
public sealed class AgentSettingsService(EfAgentSettingsStore settings, AgentStatusProbe probe, IEnvironmentReader environment, TimeProvider clock)
{
    /// <summary>Name of the key variable (read for presence only).</summary>
    public const string KeyVariable = "ANTHROPIC_API_KEY";

    /// <summary>The settings.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200.</returns>
    public async Task<IResult> GetAsync(CancellationToken cancellationToken) =>
        TypedResults.Ok(AgentSettingsRules.ToDto(await settings.GetStoredAsync(cancellationToken)));

    /// <summary>Changes the settings.</summary>
    /// <param name="request">The update.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200, 400, 409 or 422.</returns>
    public async Task<IResult> UpdateAsync(UpdateAgentSettingsRequest? request, CancellationToken cancellationToken)
    {
        if (AgentSettingsRules.ShapeProblem(request) is { } invalid)
        {
            return invalid;
        }

        var current = await settings.GetStoredAsync(cancellationToken);
        var merged = AgentSettingsRules.Merge(current, request!, clock.GetUtcNow());
        if (await RefusalAsync(request!, current, merged, cancellationToken) is { } refused)
        {
            return refused;
        }

        await settings.SaveAsync(merged, cancellationToken);
        return TypedResults.Ok(AgentSettingsRules.ToDto(merged));
    }

    /// <summary>Tests a backend on the user's explicit request.</summary>
    /// <param name="request">ollama or claude.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 or 400.</returns>
    public async Task<IResult> TestAsync(TestAgentBackendRequest? request, CancellationToken cancellationToken)
    {
        if (request?.Backend is not (AgentBackendDto.Ollama or AgentBackendDto.Claude))
        {
            return ApiProblems.Validation("backend", "Choose ollama or claude.");
        }

        return TypedResults.Ok(await probe.TestAsync(request.Backend, cancellationToken));
    }

    private async Task<IResult?> RefusalAsync(UpdateAgentSettingsRequest request, AgentSettings current, AgentSettings merged, CancellationToken cancellationToken) =>
        AgentSettingsRules.LimitProblem(request.Limits) ?? ClaudeProblem(request, merged) ?? await ModelProblemAsync(current, merged, cancellationToken);

    // Checked only when the request selects Claude, so limits stay editable after the key was removed from the shell.
    private IResult? ClaudeProblem(UpdateAgentSettingsRequest request, AgentSettings merged)
    {
        if (request.Backend != AgentBackendDto.Claude)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(environment.GetVariable(KeyVariable)))
        {
            return AgentProblems.Conflict(AgentProblemTypes.ClaudeNotConfigured, "Claude is not configured: export ANTHROPIC_API_KEY in the shell that starts StemStudio.");
        }

        return merged.ClaudeNoticeAcknowledgedAt is null
            ? AgentProblems.Coded(StatusCodes.Status422UnprocessableEntity, AgentProblemTypes.NoticeNotAcknowledged, "Notice not acknowledged", "Read and acknowledge the Claude notice first.")
            : null;
    }

    private async Task<IResult?> ModelProblemAsync(AgentSettings current, AgentSettings merged, CancellationToken cancellationToken)
    {
        var changed = new[] { merged.OllamaModel, merged.OllamaFallbackModel }
            .Where(model => model != current.OllamaModel && model != current.OllamaFallbackModel).ToList();
        if (changed.Count == 0)
        {
            return null;
        }

        var models = (await probe.GetAsync(cancellationToken)).Ollama.Models;
        var missing = changed.FirstOrDefault(name => !models.Any(model => model.Tools && model.Name == name));
        return missing is null ? null : AgentProblems.Coded(StatusCodes.Status422UnprocessableEntity, AgentProblemTypes.ModelUnavailable,
            "Model not available", $"{missing} is not installed in Ollama with tool support. Run \"ollama pull {missing}\".");
    }
}
