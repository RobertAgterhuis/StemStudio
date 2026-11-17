namespace StemStudio.Contracts.LibraryCare;

/// <summary>Storage of items without a track (<c>StorageOverview.unattached</c>) (backend-api 1.12.0, feature 013).</summary>
public sealed record UnattachedStorageDto
{
    /// <summary>Exports whose track was deleted.</summary>
    public required long ExportsBytes { get; init; }

    /// <summary>Sheet music without a track.</summary>
    public required long ScoresBytes { get; init; }

    /// <summary>Generations without a track.</summary>
    public required long GenerationsBytes { get; init; }

    /// <summary>Voice samples.</summary>
    public required long VoiceProfilesBytes { get; init; }

    /// <summary>Number of lyrics sheets.</summary>
    public required int LyricsSheets { get; init; }
}
