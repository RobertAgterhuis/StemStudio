using System.Diagnostics.CodeAnalysis;

namespace StemStudio.Agent.Model;

/// <summary>Outcome of <see cref="ClaudeApiKey.Parse"/>: the key, or the code of the failed format rule. Never holds the raw value.</summary>
public sealed class ClaudeKeyFormatResult
{
    private ClaudeKeyFormatResult(ClaudeApiKey? key, string? error)
    {
        Key = key;
        Error = error;
    }

    /// <summary>The key when the value is well-formed; else null.</summary>
    public ClaudeApiKey? Key { get; }

    /// <summary>The failed rule (<see cref="StemStudio.Contracts.Agent.ClaudeKeyInvalidReasons"/>) when not well-formed; else null.</summary>
    public string? Error { get; }

    /// <summary>True when the value is a well-formed key.</summary>
    [MemberNotNullWhen(true, nameof(Key))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsValid => Key is not null;

    /// <summary>A well-formed key.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The result.</returns>
    public static ClaudeKeyFormatResult Valid(ClaudeApiKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return new ClaudeKeyFormatResult(key, null);
    }

    /// <summary>A value that failed a format rule.</summary>
    /// <param name="reason">A code of <see cref="StemStudio.Contracts.Agent.ClaudeKeyInvalidReasons"/>.</param>
    /// <returns>The result.</returns>
    public static ClaudeKeyFormatResult Invalid(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new ClaudeKeyFormatResult(null, reason);
    }

    /// <summary>"valid" or "invalid (reason)"; never the value.</summary>
    /// <returns>The description.</returns>
    public override string ToString() => IsValid ? "valid" : $"invalid ({Error})";
}
