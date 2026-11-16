namespace StemStudio.Contracts.Agent;

/// <summary>
/// Body of PUT /api/agent/settings/claude-key (backend API 1.21.0, write-only). A class, not a record, so that no generated
/// <c>ToString</c>, equality or deconstruction can ever expose the value; <see cref="ToString"/> is redacted.
/// </summary>
public sealed class StoreClaudeKeyRequest
{
    /// <summary>The Anthropic API key as typed (1-512 characters; the server trims it and checks the format).</summary>
    public required string ApiKey { get; init; }

    /// <summary>A redacted description that never contains the value.</summary>
    /// <returns>The type name with the value replaced by <c>[redacted]</c>.</returns>
    public override string ToString() => "StoreClaudeKeyRequest { ApiKey = [redacted] }";
}
