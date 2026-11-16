using System.Text.Json;
using StemStudio.Agent.Tools;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Policy;

/// <summary>
/// The deterministic policy gate (ADR-020 D5, data-model 6). Guardrails deny; heavy and destructive calls always need
/// approval; any state change after external text (taint) or with a URL not typed by the user needs approval; read and
/// meta calls never do. A plan-scoped approval covers later heavy steps on the same target whose only reason is the
/// risk. Fresh undo compensations run without approval, other compensations need it. Never decided by the model.
/// </summary>
public static class ToolPolicy
{
    /// <summary>Refusal for the artist-imitation guardrail.</summary>
    public const string ImitationRefusal = "Styles describe genres and sounds, not artists: describe the music (genre, instruments, mood) instead of naming an artist or a singer.";

    /// <summary>Refusal for lyrics that were not written by the user.</summary>
    public const string LyricsRefusal = "Song lyrics must come from your own message: paste the lyrics in the chat or pick one of your saved lyrics sheets.";

    /// <summary>Decides one call.</summary>
    /// <param name="input">The call and its run state.</param>
    /// <returns>The decision.</returns>
    public static PolicyDecision Decide(PolicyInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (Guardrail(input) is { } denial)
        {
            return denial;
        }

        if (NeverAsks(input.Tool))
        {
            return PolicyDecision.Auto;
        }

        var reasons = Reasons(input);
        return reasons.Count == 0 || CoveredByPlan(input, reasons) ? PolicyDecision.Auto : new PolicyDecision(StepDecisionDto.ApprovalRequired, reasons, null);
    }

    /// <summary>Read tools and meta tools run without approval (after the guardrails).</summary>
    private static bool NeverAsks(AgentToolDescriptor tool) => tool.Risk == ToolRiskDto.Read || tool.Kind == ToolKindDto.Meta;

    private static PolicyDecision? Guardrail(PolicyInput input)
    {
        var guard = input.Tool.ImitationArguments.Count == 0 ? null : ImitationGuard.Create(input.LibraryArtists);
        if (guard is not null && Texts(input.Arguments, input.Tool.ImitationArguments).Any(guard.IsImitation))
        {
            return PolicyDecision.Deny(AgentDecisionReasons.ArtistImitation, ImitationRefusal);
        }

        var lyrics = Texts(input.Arguments, input.Tool.LyricsArguments);
        return lyrics.Any(text => !ProvenanceChecker.LyricsFromUser(text, input.UserMessages))
            ? PolicyDecision.Deny(AgentDecisionReasons.LyricsNotFromUser, LyricsRefusal)
            : null;
    }

    private static List<string> Reasons(PolicyInput input)
    {
        var reasons = new List<string>();
        var freshCompensation = input.IsCompensation && input.Fresh;
        AddIf(reasons, input.Tool.Risk == ToolRiskDto.Heavy && !freshCompensation, AgentDecisionReasons.RiskHeavy);
        AddIf(reasons, input.Tool.Risk == ToolRiskDto.Destructive && !freshCompensation, AgentDecisionReasons.RiskDestructive);
        AddIf(reasons, input.Tainted, AgentDecisionReasons.Tainted);
        AddIf(reasons, Texts(input.Arguments, input.Tool.UrlArguments).Any(url => !ProvenanceChecker.UrlFromUser(url, input.UserMessages)), AgentDecisionReasons.UrlNotFromUser);
        AddIf(reasons, input.IsCompensation && !input.Fresh, AgentDecisionReasons.UndoCompensation);
        return reasons;
    }

    private static void AddIf(List<string> reasons, bool condition, string reason)
    {
        if (condition)
        {
            reasons.Add(reason);
        }
    }

    private static bool CoveredByPlan(PolicyInput input, List<string> reasons)
    {
        if (reasons is not [AgentDecisionReasons.RiskHeavy] || !input.Plan.Any(item => item.Tool == input.Tool.Name))
        {
            return false;
        }

        var target = SingleTarget(input);
        return target is not null && input.PlanGrants.Any(grant => grant.ToolName == input.Tool.Name && grant.TargetId == target);
    }

    private static string? SingleTarget(PolicyInput input) =>
        input.Tool.TargetArgument is { } name && input.Arguments.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>The string values of the named arguments (strings, or the strings of arrays).</summary>
    /// <param name="arguments">Arguments object.</param>
    /// <param name="names">Argument names.</param>
    /// <returns>The values.</returns>
    internal static IEnumerable<string> Texts(JsonElement arguments, IReadOnlyList<string> names) =>
        names.SelectMany(name => arguments.TryGetProperty(name, out var value) ? Strings(value) : []);

    private static IEnumerable<string> Strings(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => [value.GetString()!],
        JsonValueKind.Array => value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!),
        _ => [],
    };
}
