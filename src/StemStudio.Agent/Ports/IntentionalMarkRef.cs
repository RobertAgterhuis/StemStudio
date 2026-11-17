using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Agent.Ports;

/// <summary>A created intentional mark without its text.</summary>
public sealed record IntentionalMarkRef
{
    /// <summary>Mark id.</summary>
    public required Guid MarkId { get; init; }

    /// <summary>Scope.</summary>
    public required IntentionalScopeDto Scope { get; init; }
}
