using System.Globalization;
using StemStudio.Contracts.Agent;

namespace StemStudio.Web.Agent;

/// <summary>
/// Readable texts of the Claude key status (011 follow-up claude-key, US2). Built only from the status members
/// (<c>lastFour</c>, <c>storedLastFour</c>, <c>storedAt</c>): never from a key.
/// </summary>
public static class ClaudeKeyText
{
    /// <summary>The fix of an unreadable stored key (ui-test-ids 1.21.0 <c>settings-agent-claude-key-unreadable</c>).</summary>
    public const string UnreadableFix = "Enter the key again: press Replace key and paste the key from the Anthropic console.";

    /// <summary>The notice of <c>settings-agent-claude-key-local-only</c> (CK-FR-012).</summary>
    public const string LocalOnly =
        "The Claude key can only be entered on the computer StemStudio runs on. Open StemStudio there as http://localhost:8080 to enter, replace, delete or test it.";

    private const string Unreadable =
        "The stored key cannot be read on this installation (the encryption keys are missing or different, e.g. after a restore on another computer).";

    /// <summary>The status line of <c>settings-agent-claude-key-status</c>.</summary>
    /// <param name="key">The key status.</param>
    /// <returns>The text.</returns>
    public static string Status(ClaudeKeyStatusDto key) => key.State switch
    {
        ClaudeKeyStateDto.Environment => $"From the environment (ANTHROPIC_API_KEY), ends with ...{key.LastFour}.",
        ClaudeKeyStateDto.Stored => $"Stored key, ends with ...{key.LastFour}, saved on {Date(key.StoredAt)}. It is stored encrypted on this computer.",
        ClaudeKeyStateDto.StoredUnreadable => $"Stored key (ends with ...{key.StoredLastFour}) cannot be read.",
        _ => NoKey(key),
    };

    /// <summary>The note of <c>settings-agent-claude-key-env-override</c>.</summary>
    /// <param name="key">The key status.</param>
    /// <returns>The text.</returns>
    public static string EnvironmentOverride(ClaudeKeyStatusDto key) =>
        $"A saved key exists (ends with ...{key.StoredLastFour}, saved {Date(key.StoredAt)}) but is not used while ANTHROPIC_API_KEY is set.";

    /// <summary>The explanation of <c>settings-agent-claude-key-unreadable</c>: the reason and its fix.</summary>
    /// <param name="key">The key status.</param>
    /// <returns>The text.</returns>
    public static string UnreadableReason(ClaudeKeyStatusDto key) => key.Reason ?? Unreadable;

    /// <summary>True when the environment variable overrides a stored key (<c>settings-agent-claude-key-env-override</c>).</summary>
    /// <param name="key">The key status.</param>
    /// <returns>Whether the note is shown.</returns>
    public static bool IsOverridden(ClaudeKeyStatusDto key) =>
        key.Source == ClaudeKeySourceDto.Environment && key.Stored != StoredClaudeKeyStateDto.Absent;

    /// <summary>The <c>data-source</c> value: the wire name of the source, or empty.</summary>
    /// <param name="key">The key status.</param>
    /// <returns>The attribute value.</returns>
    public static string Source(ClaudeKeyStatusDto key) => key.Source is { } source ? AgentWire.Name(source) : string.Empty;

    private static string NoKey(ClaudeKeyStatusDto key) => key.StoreAvailable
        ? "No key. Enter the Claude API key below, or set ANTHROPIC_API_KEY in the environment of the StemStudio API."
        : $"No key. {key.Reason ?? "The key store is not available. The ANTHROPIC_API_KEY environment variable still works."}";

    private static string Date(DateTimeOffset? at) =>
        at?.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture) ?? "at an unknown time";
}
