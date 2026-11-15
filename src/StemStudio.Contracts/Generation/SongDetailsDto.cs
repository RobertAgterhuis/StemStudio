using StemStudio.Contracts.Styles;

namespace StemStudio.Contracts.Generation;

/// <summary>What a song was made from (1.9.0).</summary>
public sealed record SongDetailsDto
{
    /// <summary>The lyrics snapshot.</summary>
    public required LyricsSnapshotDto Lyrics { get; init; }

    /// <summary>The style snapshot; null with a free caption.</summary>
    public StyleSnapshotDto? Style { get; init; }

    /// <summary>The free caption, when used.</summary>
    public string? FreeCaption { get; init; }

    /// <summary>Vocal options.</summary>
    public required SongVoiceDto Voice { get; init; }

    /// <summary>True for an instrumental backing.</summary>
    public bool Instrumental { get; init; }

    /// <summary>Tempo, when given.</summary>
    public int? Bpm { get; init; }

    /// <summary>Key, e.g. <c>E minor</c>, when given.</summary>
    public string? KeyScale { get; init; }

    /// <summary>Beats per bar (<c>2</c>, <c>3</c>, <c>4</c>, <c>6</c>), when given.</summary>
    public string? TimeSignature { get; init; }

    /// <summary>Requested length.</summary>
    public required double DurationSeconds { get; init; }

    /// <summary>The caption sent to the model.</summary>
    public required string Caption { get; init; }
}
