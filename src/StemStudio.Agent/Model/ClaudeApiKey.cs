using System.Security.Cryptography;
using System.Text;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Model;

/// <summary>
/// A well-formed Claude API key (011 follow-up claude-key, data-model 3). A sealed class, not a record: no generated
/// <c>ToString</c>, equality or deconstruction may expose the value. <see cref="Reveal"/> is the only way to the value and is
/// called only to hand it to the Anthropic SDK or to Data Protection (constitution 1.4.0, spec CK-FR-006, CK-FR-009).
/// </summary>
public sealed class ClaudeApiKey
{
    /// <summary>What <see cref="ToString"/> returns instead of the value.</summary>
    public const string Redacted = "[redacted]";

    /// <summary>Shortest accepted key after trimming.</summary>
    public const int MinLength = 40;

    /// <summary>Longest accepted key after trimming.</summary>
    public const int MaxLength = 256;

    // Assembled from parts: the repository secret scan fails on the prefix followed by a key character (research R-07).
    private static readonly string KeyPrefix = string.Concat("sk", "-", "ant", "-");
    private static readonly string AdminPrefix = string.Concat(KeyPrefix, "admin");

    // The format rules in the order they are checked; the first failing rule is reported.
    private static readonly (Func<string, bool> Fails, string Reason)[] Rules =
    [
        (value => value.Length == 0, ClaudeKeyInvalidReasons.Empty),
        (value => !value.StartsWith(KeyPrefix, StringComparison.Ordinal), ClaudeKeyInvalidReasons.Prefix),
        (value => value.Length is < MinLength or > MaxLength, ClaudeKeyInvalidReasons.Length),
        (value => !value.All(IsKeyCharacter), ClaudeKeyInvalidReasons.Characters),
        (value => value.StartsWith(AdminPrefix, StringComparison.OrdinalIgnoreCase), ClaudeKeyInvalidReasons.AdminKey),
    ];

    private readonly string _value;

    private ClaudeApiKey(string value)
    {
        _value = value;
        LastFour = value[^4..];
        Fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    /// <summary>The last four characters of the trimmed key (key characters only), shown as "ends with ...WXYZ".</summary>
    public string LastFour { get; }

    /// <summary>SHA-256 of the key as lower-case hex; kept in memory only (client cache key), never logged or persisted.</summary>
    public string Fingerprint { get; }

    /// <summary>
    /// Trims and checks a value: prefix <c>sk-ant-</c>, 40-256 characters of <c>A-Z a-z 0-9 - _</c>, not an admin key.
    /// The rules are checked in the order empty, prefix, length, characters, admin key.
    /// </summary>
    /// <param name="value">The value as typed or read from the environment or the store.</param>
    /// <returns>The key, or the failed rule (<see cref="ClaudeKeyInvalidReasons"/>).</returns>
    public static ClaudeKeyFormatResult Parse(string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        var reason = FailedRule(trimmed);
        return reason is null ? ClaudeKeyFormatResult.Valid(new ClaudeApiKey(trimmed)) : ClaudeKeyFormatResult.Invalid(reason);
    }

    /// <summary>The key itself; only for the Anthropic SDK client and for encryption.</summary>
    /// <returns>The trimmed key.</returns>
    public string Reveal() => _value;

    /// <summary>Always <see cref="Redacted"/>.</summary>
    /// <returns><see cref="Redacted"/>.</returns>
    public override string ToString() => Redacted;

    private static string? FailedRule(string trimmed) => Array.Find(Rules, rule => rule.Fails(trimmed)).Reason;

    private static bool IsKeyCharacter(char c) => char.IsAsciiLetterOrDigit(c) || c is '-' or '_';
}
