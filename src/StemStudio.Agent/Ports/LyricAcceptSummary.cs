namespace StemStudio.Agent.Ports;

/// <summary>Result of an accept or undo without text.</summary>
public sealed record LyricAcceptSummary
{
    /// <summary>New revision.</summary>
    public required int Revision { get; init; }

    /// <summary>Suggestions applied.</summary>
    public required int Applied { get; init; }

    /// <summary>Requested suggestions skipped as stale.</summary>
    public required int Stale { get; init; }
}
