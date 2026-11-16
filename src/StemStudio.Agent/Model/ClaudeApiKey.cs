namespace StemStudio.Agent.Model;

/// <summary>
/// A well-formed Claude API key (011 follow-up claude-key, data-model 3). A sealed class, not a record: no generated
/// <c>ToString</c>, equality or deconstruction may expose the value. <see cref="Reveal"/> is the only way to the value and is
/// called only to hand it to the Anthropic SDK or to Data Protection (constitution 1.4.0, spec CK-FR-006, CK-FR-009).
/// </summary>
/// <remarks>Skeleton (architect T005): the members throw until the ai-agent implements them (T007/T008).</remarks>
public sealed class ClaudeApiKey
{
    /// <summary>What <see cref="ToString"/> returns instead of the value.</summary>
    public const string Redacted = "[redacted]";

    private ClaudeApiKey()
    {
    }

    /// <summary>The last four characters of the trimmed key (key characters only), shown as "ends with ...WXYZ".</summary>
    public string LastFour => throw NotBuilt();

    /// <summary>SHA-256 of the key as lower-case hex; kept in memory only (client cache key), never logged or persisted.</summary>
    public string Fingerprint => throw NotBuilt();

    /// <summary>
    /// Trims and checks a value: prefix <c>sk-ant-</c>, 40-256 characters of <c>A-Z a-z 0-9 - _</c>, not an admin key.
    /// </summary>
    /// <param name="value">The value as typed or read from the environment or the store.</param>
    /// <returns>The key, or the failed rule (<see cref="StemStudio.Contracts.Agent.ClaudeKeyInvalidReasons"/>).</returns>
    public static ClaudeKeyFormatResult Parse(string? value) => throw NotBuilt();

    /// <summary>The key itself; only for the Anthropic SDK client and for encryption.</summary>
    /// <returns>The trimmed key.</returns>
    public string Reveal() => throw NotBuilt();

    /// <summary>Always <see cref="Redacted"/>.</summary>
    /// <returns><see cref="Redacted"/>.</returns>
    public override string ToString() => Redacted;

    private static NotImplementedException NotBuilt() =>
        new("ClaudeApiKey is implemented by the ai-agent (011 follow-up claude-key T008).");
}
