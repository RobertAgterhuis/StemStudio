using System.Net;
using System.Text.Json;
using StemStudio.Contracts.Agent;
using StemStudio.Web.Api;

namespace StemStudio.Web.Agent;

/// <summary>
/// Readable messages for failed Claude key calls (backend API 1.21.0). Every message is a fixed text chosen by the problem
/// code, its <c>reason</c> or the status: the problem's <c>detail</c> is never shown, so a value can never be repeated
/// (CK-FR-009), even by a misbehaving server.
/// </summary>
public static class ClaudeKeyErrors
{
    private const string Format = "This is not a Claude API key: it must start with sk-ant- and be 40 to 256 characters (letters, digits, - and _).";

    private static readonly Dictionary<string, string> Reasons = new(StringComparer.Ordinal)
    {
        [ClaudeKeyInvalidReasons.Empty] = "Enter the key first: the field was empty.",
        [ClaudeKeyInvalidReasons.Prefix] = "This is not a Claude API key: it must start with sk-ant-.",
        [ClaudeKeyInvalidReasons.Length] = "The key must be 40 to 256 characters long.",
        [ClaudeKeyInvalidReasons.Characters] = "The key may contain only letters, digits, - and _ (no spaces inside).",
        [ClaudeKeyInvalidReasons.AdminKey] = "This is an Anthropic admin key. Use a normal API key from the Anthropic console.",
    };

    /// <summary>True for <c>403 local-only</c>: the page then shows the local-only notice instead of an error.</summary>
    /// <param name="error">The mapped error.</param>
    /// <returns>Whether the call was refused as not local.</returns>
    public static bool IsLocalOnly(ApiError error) => error.Status == HttpStatusCode.Forbidden && error.Code == AgentProblemTypes.LocalOnly;

    /// <summary>The message of a failed key call.</summary>
    /// <param name="error">The mapped error.</param>
    /// <returns>A fixed, value-free text.</returns>
    public static string Message(ApiError error) => error.Code switch
    {
        AgentProblemTypes.ClaudeKeyInvalid => Reasons.GetValueOrDefault(ReasonOf(error), Format),
        AgentProblemTypes.KeyStoreUnavailable => "The key store is not available, so the key was not saved. The ANTHROPIC_API_KEY environment variable still works.",
        AgentProblemTypes.InvalidBody => "The key could not be sent. Reload the page and try again.",
        _ => ByStatus(error),
    };

    private static string ByStatus(ApiError error) => error.Status switch
    {
        HttpStatusCode.RequestEntityTooLarge => "The value is too long for a Claude API key.",
        HttpStatusCode.ServiceUnavailable when error.Code is null => ApiError.Unreachable.Title,
        _ => $"The key action failed ({(int)error.Status}). Try again.",
    };

    private static string ReasonOf(ApiError error) =>
        error.Extensions.TryGetValue("reason", out var reason) && reason.ValueKind == JsonValueKind.String ? reason.GetString()! : string.Empty;
}
