using System.Text.Json;
using StemStudio.Contracts.Agent;
using StemStudio.Web.Api;

namespace StemStudio.Web.Agent;

/// <summary>
/// Problem handling of the Claude key writes (011 follow-up claude-key, qa F6): the request carried the key, so a misbehaving
/// server could echo it in any field of its problem. The client keeps only what the page needs - the status, a known problem
/// type and a known 422 <c>reason</c> - and drops the title, detail, field errors and every other extension, so neither the
/// exception message nor a log of the circuit can carry the value.
/// </summary>
internal static class ClaudeKeyProblems
{
    private const string Title = "The Claude key request failed.";

    private static readonly string[] KnownTypes =
    [
        AgentProblemTypes.ClaudeKeyInvalid, AgentProblemTypes.InvalidBody, AgentProblemTypes.LocalOnly, AgentProblemTypes.KeyStoreUnavailable,
    ];

    private static readonly string[] KnownReasons =
    [
        ClaudeKeyInvalidReasons.Empty, ClaudeKeyInvalidReasons.Prefix, ClaudeKeyInvalidReasons.Length,
        ClaudeKeyInvalidReasons.Characters, ClaudeKeyInvalidReasons.AdminKey,
    ];

    /// <summary>Runs a key write and rethrows a failure as an <see cref="ApiException"/> holding only the safe parts.</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="call">The write.</param>
    /// <returns>The result.</returns>
    public static async Task<T> GuardAsync<T>(Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (ApiException ex)
        {
            throw new ApiException(Safe(ex.Error));
        }
    }

    private static ApiError Safe(ApiError error) => new(error.Status, Title, null, new Dictionary<string, string[]>())
    {
        Type = KnownTypes.Contains(error.Code) && error.Type == AgentProblemTypes.Uri(error.Code!) ? error.Type : null,
        Extensions = KnownReason(error) is { } reason ? new Dictionary<string, JsonElement> { ["reason"] = reason } : [],
    };

    private static JsonElement? KnownReason(ApiError error) =>
        error.Extensions.TryGetValue("reason", out var reason) && reason.ValueKind == JsonValueKind.String && KnownReasons.Contains(reason.GetString())
            ? reason
            : null;
}
