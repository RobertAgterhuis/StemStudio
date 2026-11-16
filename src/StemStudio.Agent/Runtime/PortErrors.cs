using StemStudio.Agent.Policy;
using StemStudio.Agent.Ports;

namespace StemStudio.Agent.Runtime;

/// <summary>
/// Errors of the StemStudio API ports. Their messages can quote external content (titles, provider texts), so they reach
/// the model only as <c>untrusted_text</c> under a fixed trusted text; known problem codes get a fixed readable message.
/// Errors the agent raises itself (status 0, e.g. "No mixer is open") stay trusted.
/// </summary>
internal static class PortErrors
{
    /// <summary>Text of the consent problems of 010 (tool catalogue).</summary>
    public const string RenewConsent = Tools.Providers.SongToolProvider.ConsentNotCurrent;

    private static readonly Dictionary<string, string> Known = new(StringComparer.Ordinal)
    {
        ["imitation"] = ToolPolicy.ImitationRefusal,
        ["consent-required"] = RenewConsent,
        ["consent-outdated"] = RenewConsent,
        ["preset-read-only"] = "Preset styles cannot be deleted; only your own styles can.",
        ["generation-not-retryable"] = "Only failed or cancelled generations can be retried.",
    };

    public static StepFailure From(AgentActionException exception)
    {
        if (exception.StatusCode == 0)
        {
            return new StepFailure(exception.Message, null);
        }

        return exception.ProblemCode is { } code && Known.TryGetValue(code, out var known)
            ? new StepFailure(known, null)
            : new StepFailure(exception.Message, exception.Message, Fixed(exception.StatusCode));
    }

    private static string Fixed(int status) => status == 404
        ? "The item does not exist (HTTP 404)."
        : $"StemStudio could not do this step (HTTP {status}).";
}
