namespace StemStudio.Contracts.LyricAssist;

/// <summary>Body of <c>PUT /api/lyrics/{lyricsId}/own-work-override</c> (schema <c>OwnWorkConfirmRequest</c>).</summary>
public sealed record OwnWorkConfirmRequest
{
    /// <summary>The matching track to override; must be a current match.</summary>
    public required Guid MatchedTrackId { get; init; }

    /// <summary>Typed name of the person confirming (1-80 characters).</summary>
    public required string ConfirmedBy { get; init; }

    /// <summary>Statement version shown; must be the current one (else <c>409 statement-outdated</c>).</summary>
    public required string StatementVersion { get; init; }

    /// <summary>Must be true (the tick box).</summary>
    public required bool Confirm { get; init; }
}
