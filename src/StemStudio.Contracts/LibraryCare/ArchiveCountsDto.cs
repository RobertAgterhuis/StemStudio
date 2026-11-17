namespace StemStudio.Contracts.LibraryCare;

/// <summary>Item counts of an archive or library (<c>ArchiveCounts</c>) (backend-api 1.12.0, feature 013).</summary>
public sealed record ArchiveCountsDto
{
    /// <summary>Tracks.</summary>
    public required int Tracks { get; init; }

    /// <summary>Mixes.</summary>
    public required int Mixes { get; init; }

    /// <summary>Library items.</summary>
    public required int Assets { get; init; }

    /// <summary>Generations.</summary>
    public required int Generations { get; init; }

    /// <summary>User styles.</summary>
    public required int Styles { get; init; }

    /// <summary>Lyrics sheets.</summary>
    public required int LyricsSheets { get; init; }

    /// <summary>Voice profiles.</summary>
    public required int VoiceProfiles { get; init; }
}
