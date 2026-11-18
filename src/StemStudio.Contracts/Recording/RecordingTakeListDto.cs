namespace StemStudio.Contracts.Recording;

/// <summary>The takes of a track, newest first (backend-api 1.14.0, feature 015).</summary>
public sealed record RecordingTakeListDto
{
    /// <summary>At most 200 takes.</summary>
    public required IReadOnlyList<RecordingTakeDto> Items { get; init; }

    /// <summary>Sum of their sizes.</summary>
    public required long TotalSizeBytes { get; init; }
}
