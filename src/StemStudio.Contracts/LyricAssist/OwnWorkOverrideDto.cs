namespace StemStudio.Contracts.LyricAssist;

/// <summary>A confirmed own-work override (schema <c>OwnWorkOverride</c>, owner decision Q1).</summary>
public sealed record OwnWorkOverrideDto
{
    /// <summary>Statement version confirmed (<see cref="LyricAssistLimits.OwnWorkStatementVersion"/>).</summary>
    public required string StatementVersion { get; init; }

    /// <summary>Typed name of the person confirming (1-80 characters).</summary>
    public required string ConfirmedBy { get; init; }

    /// <summary>When it was confirmed.</summary>
    public required DateTimeOffset ConfirmedAt { get; init; }

    /// <summary>The only track whose lyrics the guard ignores for this sheet.</summary>
    public required Guid MatchedTrackId { get; init; }

    /// <summary>Title snapshot of that track (at most 200 characters).</summary>
    public required string MatchedTrackTitle { get; init; }
}
