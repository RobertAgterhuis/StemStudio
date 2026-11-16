using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Backends;

/// <summary>Red stub of T009; implemented in T010.</summary>
/// <param name="environment">Environment reader.</param>
/// <param name="stored">Stored key reader.</param>
public sealed class ClaudeKeyProvider(IEnvironmentReader environment, IStoredClaudeKeyReader stored) : IClaudeKeyProvider
{
    /// <summary>Reason of an unreadable stored key.</summary>
    public const string UnreadableReason = "";

    /// <summary>Fix of an unreadable stored key.</summary>
    public const string UnreadableFix = "";

    /// <summary>Fix of a malformed environment value.</summary>
    public const string MalformedEnvironmentFix = "";

    /// <summary>Fix of an unavailable store.</summary>
    public const string StoreUnavailableFix = "";

    private readonly IEnvironmentReader _environment = environment;
    private readonly IStoredClaudeKeyReader _stored = stored;

    /// <summary>Reason of a malformed environment value.</summary>
    /// <param name="code">Format code.</param>
    /// <returns>The text.</returns>
    public static string MalformedEnvironmentReason(string code) => throw new NotImplementedException(code);

    /// <summary>Reason of an unavailable store.</summary>
    /// <param name="code">Reason code.</param>
    /// <returns>The text.</returns>
    public static string StoreUnavailableReason(string? code) => throw new NotImplementedException(code);

    /// <inheritdoc />
    public ClaudeKeyResolution Resolve() => throw new NotImplementedException(_environment.ToString() + _stored);

    /// <inheritdoc />
    public SelectedClaudeKey? KeyFor(ClaudeKeyTestSourceDto source) => throw new NotImplementedException();

    /// <inheritdoc />
    public void Invalidate() => throw new NotImplementedException();
}
