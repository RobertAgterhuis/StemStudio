using StemStudio.Contracts.Lyrics;

namespace StemStudio.Agent.Ports;

/// <summary>Lyrics state of a track without any lyrics text (FR-026): what get_lyrics_status may show.</summary>
public sealed record LyricsSummary
{
    /// <summary>Lyrics status.</summary>
    public required LyricsStatusDto Status { get; init; }

    /// <summary>Line or word timing, when lyrics exist.</summary>
    public SyncLevelDto? SyncLevel { get; init; }

    /// <summary>Where the lyrics came from.</summary>
    public LyricsOriginDto? Origin { get; init; }

    /// <summary>Online source, when any.</summary>
    public LyricsSourceDto? Source { get; init; }

    /// <summary>True when the track is marked instrumental.</summary>
    public required bool Instrumental { get; init; }

    /// <summary>Readable reason of notFound, failed or disabled.</summary>
    public string? Reason { get; init; }
}
