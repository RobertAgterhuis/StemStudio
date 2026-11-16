namespace StemStudio.Contracts.Agent;

/// <summary>Values of the extension member <c>reason</c> of the 422 <c>claude-key-invalid</c> problem (backend API 1.21.0).</summary>
public static class ClaudeKeyInvalidReasons
{
    /// <summary>Nothing left after trimming.</summary>
    public const string Empty = "empty";

    /// <summary>The value does not start with the Anthropic key prefix.</summary>
    public const string Prefix = "prefix";

    /// <summary>Shorter than 40 or longer than 256 characters after trimming.</summary>
    public const string Length = "length";

    /// <summary>A character outside letters, digits, <c>-</c> and <c>_</c> (inner whitespace included).</summary>
    public const string Characters = "characters";

    /// <summary>An Anthropic admin key, which the agent must never use.</summary>
    public const string AdminKey = "admin-key";
}
