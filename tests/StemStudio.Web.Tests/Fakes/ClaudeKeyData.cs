using System.Net;
using System.Text.Json;
using StemStudio.Contracts.Agent;
using StemStudio.TestDoubles.ClaudeKey;
using StemStudio.Web.Api;

namespace StemStudio.Web.Tests.Fakes;

/// <summary>Contract-shaped Claude key statuses and problems (backend-api 1.21.0, 011 follow-up claude-key).</summary>
public static class ClaudeKeyData
{
    public static readonly DateTimeOffset SavedAt = new(2026, 11, 16, 15, 40, 0, TimeSpan.Zero);

    /// <summary>Last four characters of <see cref="ClaudeKeySentinels.Environment"/>.</summary>
    public static string EnvFour => ClaudeKeySentinels.Environment[^4..];

    /// <summary>Last four characters of <see cref="ClaudeKeySentinels.Stored"/>.</summary>
    public static string StoredFour => ClaudeKeySentinels.Stored[^4..];

    /// <summary>Last four characters of <see cref="ClaudeKeySentinels.Replacement"/>.</summary>
    public static string ReplacementFour => ClaudeKeySentinels.Replacement[^4..];

    public static ClaudeKeyStatusDto NotConfigured() => ClaudeKeyStatusDto.NotConfigured;

    public static ClaudeKeyStatusDto Environment(StoredClaudeKeyStateDto stored = StoredClaudeKeyStateDto.Absent) => new()
    {
        Configured = true,
        Source = ClaudeKeySourceDto.Environment,
        State = ClaudeKeyStateDto.Environment,
        LastFour = EnvFour,
        Stored = stored,
        StoredLastFour = stored == StoredClaudeKeyStateDto.Absent ? null : StoredFour,
        StoredAt = stored == StoredClaudeKeyStateDto.Absent ? null : SavedAt,
        StoreAvailable = true,
        Reason = stored == StoredClaudeKeyStateDto.Unreadable ? UnreadableReason : null,
        Fix = stored == StoredClaudeKeyStateDto.Unreadable ? "Enter the key again." : null,
    };

    public static ClaudeKeyStatusDto Stored(string? lastFour = null) => new()
    {
        Configured = true,
        Source = ClaudeKeySourceDto.Stored,
        State = ClaudeKeyStateDto.Stored,
        LastFour = lastFour ?? StoredFour,
        Stored = StoredClaudeKeyStateDto.Readable,
        StoredLastFour = lastFour ?? StoredFour,
        StoredAt = SavedAt,
        StoreAvailable = true,
        Reason = null,
        Fix = null,
    };

    public static ClaudeKeyStatusDto Unreadable() => new()
    {
        Configured = false,
        Source = null,
        State = ClaudeKeyStateDto.StoredUnreadable,
        LastFour = null,
        Stored = StoredClaudeKeyStateDto.Unreadable,
        StoredLastFour = StoredFour,
        StoredAt = SavedAt,
        StoreAvailable = true,
        Reason = UnreadableReason,
        Fix = "Enter the key again.",
    };

    public static ClaudeKeyStatusDto StoreUnavailable() => ClaudeKeyStatusDto.NotConfigured with
    {
        StoreAvailable = false,
        Reason = "The key store is not available - its folder is not writable. The ANTHROPIC_API_KEY environment variable still works.",
    };

    public const string UnreadableReason =
        "The stored key cannot be read on this installation (the encryption keys are missing or different, e.g. after a restore on another computer).";

    /// <summary>The agent status whose Claude part carries <paramref name="key"/>.</summary>
    public static AgentStatusDto AgentStatus(ClaudeKeyStatusDto key, AgentBackendDto backend = AgentBackendDto.Ollama) => AgentData.Status(backend: backend) with
    {
        Claude = new ClaudeStatusDto { KeyConfigured = key.Configured, NoticeAcknowledged = false, Model = ClaudeModelDto.ClaudeOpus55, Key = key },
    };

    /// <summary>A key endpoint problem; <paramref name="detail"/> may carry a leaked value to prove the page never echoes it.</summary>
    public static ApiException Problem(HttpStatusCode status, string code, string? detail = null, string? reason = null) =>
        new(new ApiError(status, "Problem", detail, new Dictionary<string, string[]>())
        {
            Type = AgentProblemTypes.Uri(code),
            Extensions = reason is null
                ? new Dictionary<string, JsonElement>()
                : new Dictionary<string, JsonElement> { ["reason"] = JsonSerializer.SerializeToElement(reason) },
        });
}
