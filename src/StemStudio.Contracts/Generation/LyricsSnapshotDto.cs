namespace StemStudio.Contracts.Generation;

/// <summary>The lyrics a song was made from (1.9.0).</summary>
public sealed record LyricsSnapshotDto
{
    /// <summary>The saved sheet; null for inline text or after it was deleted.</summary>
    public Guid? LyricsId { get; init; }

    /// <summary>Title snapshot.</summary>
    public required string Title { get; init; }

    /// <summary>Language code (<see cref="ContractLimits.LyricsLanguages"/>).</summary>
    public required string Language { get; init; }

    /// <summary>Normalised text snapshot.</summary>
    public required string Text { get; init; }
}
