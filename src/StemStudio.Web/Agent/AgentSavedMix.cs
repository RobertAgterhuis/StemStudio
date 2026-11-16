using System.Text.Json;
using StemStudio.Contracts.Agent;

namespace StemStudio.Web.Agent;

/// <summary>
/// Recognises the step event of an agent save (<c>save_mix_as</c>, <c>update_mix</c>) that succeeded, so the open mixer
/// can record the save and clear its unsaved indicator like a save of its own (011 O3).
/// </summary>
public static class AgentSavedMix
{
    private static readonly HashSet<string> SaveTools = new(StringComparer.Ordinal) { "save_mix_as", "update_mix" };

    /// <summary>The step and the mix of a successful save step, or null for every other event.</summary>
    /// <param name="run">The run view after the event (it names the step's tool).</param>
    /// <param name="e">The event.</param>
    /// <returns>The step id and the saved mix id from the step's result summary.</returns>
    public static (Guid StepId, Guid MixId)? From(AgentRunView run, AgentEvent e)
    {
        if (e.Type != AgentEventTypes.StepUpdated || e.Read<StepUpdatedEventDto>() is not { Status: AgentStepStatusDto.Succeeded } update)
        {
            return null;
        }

        var step = run.Steps.FirstOrDefault(s => s.StepId == update.StepId);
        return step is not null && SaveTools.Contains(step.Tool) && MixId(update.ResultSummary) is { } mixId ? (step.StepId, mixId) : null;
    }

    private static Guid? MixId(JsonElement? summary) =>
        summary is { ValueKind: JsonValueKind.Object } result && result.TryGetProperty("mixId", out var id)
            && id.ValueKind == JsonValueKind.String && Guid.TryParse(id.GetString(), out var mixId)
            ? mixId
            : null;
}
