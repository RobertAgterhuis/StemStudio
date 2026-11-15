namespace StemStudio.Contracts.Lyrics;

/// <summary>
/// The lyrics of a track (backend contract 1.6.0): <c>GET /api/tracks/{id}/lyrics</c> only, served with
/// <c>Cache-Control: no-store</c>; never part of the track resources, never logged, never exported.
/// </summary>
public sealed record LyricsDto
{
    /// <summary>Lyrics state.</summary>
    public required LyricsStatusDto Status { get; init; }

    /// <summary>Readable reason (failed, notFound, disabled, the alignPending estimate).</summary>
    public string? Reason { get; init; }

    /// <summary>Best timing; null without a document.</summary>
    public SyncLevelDto? SyncLevel { get; init; }

    /// <summary>Dominant word origin; null without a document.</summary>
    public LyricsOriginDto? Origin { get; init; }

    /// <summary>ISO 639 language code, when known.</summary>
    public string? Language { get; init; }

    /// <summary>Where the text comes from; null without a document.</summary>
    public LyricsSourceDto? Source { get; init; }

    /// <summary>Credit shown with the banner, e.g. "Lyrics: LRCLIB; word timing estimated".</summary>
    public string? Credit { get; init; }

    /// <summary>User offset -5000..5000 ms in steps of 10 (positive = lyrics later).</summary>
    public int OffsetMs { get; init; }

    /// <summary>The song is instrumental.</summary>
    public bool Instrumental { get; init; }

    /// <summary>Lines (also for plain documents, with null times).</summary>
    public IReadOnlyList<LyricsLineDto> Lines { get; init; } = [];

    /// <summary>Alignment estimate, device and progress; null when no alignment is pending or running.</summary>
    public LyricsAlignmentDto? Alignment { get; init; }
}
