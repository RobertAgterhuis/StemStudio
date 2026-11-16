using System.Globalization;
using System.Text.Json;
using StemStudio.Contracts.Agent;

namespace StemStudio.Web.Agent;

/// <summary>Readable texts and attribute values of the confirmation card (FR-012, FR-013; ui-test-ids 1.10.0).</summary>
public static class ApprovalText
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    private static readonly Dictionary<string, string> Reasons = new(StringComparer.Ordinal)
    {
        [AgentDecisionReasons.RiskHeavy] = "Long job: it can take minutes and uses the graphics card or the processor",
        [AgentDecisionReasons.RiskDestructive] = "Deletes or overwrites without undo",
        [AgentDecisionReasons.Tainted] = "Proposed after reading external content (tags, lyrics or online facts): check the arguments",
        [AgentDecisionReasons.UrlNotFromUser] = "The link does not appear in your own messages",
        [AgentDecisionReasons.UndoCompensation] = "Removes what an earlier step created",
    };

    /// <summary>The readable text of a reason code (unknown codes are shown as they are).</summary>
    /// <param name="code">The reason code.</param>
    /// <returns>The text.</returns>
    public static string Reason(string code) => Reasons.GetValueOrDefault(code, code);

    /// <summary>The exact arguments, indented.</summary>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The JSON text.</returns>
    public static string Arguments(JsonElement arguments) => JsonSerializer.Serialize(arguments, Indented);

    /// <summary><c>data-device</c>: <c>cpu</c>, <c>gpu</c> or empty when unknown.</summary>
    /// <param name="device">The device of the event (<c>cpu</c>, <c>cuda</c>, ...).</param>
    /// <returns>The attribute value.</returns>
    public static string Device(string? device) => device switch
    {
        null => string.Empty,
        _ when device.Equals("cpu", StringComparison.OrdinalIgnoreCase) => "cpu",
        _ => "gpu",
    };

    /// <summary><c>data-minutes</c>: the estimate in invariant culture, or empty.</summary>
    /// <param name="minutes">The estimate.</param>
    /// <returns>The attribute value.</returns>
    public static string Minutes(double? minutes) => minutes?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    /// <summary>"About 7 minutes on the graphics card".</summary>
    /// <param name="minutes">The estimate, or null.</param>
    /// <param name="device">The device of the event, or null.</param>
    /// <returns>The text.</returns>
    public static string Estimate(double? minutes, string? device)
    {
        var where = Device(device) switch
        {
            "cpu" => " on the processor",
            "gpu" => " on the graphics card",
            _ => string.Empty,
        };
        if (minutes is not { } value)
        {
            return $"Duration unknown{where}";
        }

        var rounded = Math.Max(1, (int)Math.Round(value, MidpointRounding.AwayFromZero));
        return $"About {rounded} {(rounded == 1 ? "minute" : "minutes")}{where}";
    }
}
