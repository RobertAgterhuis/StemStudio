using StemStudio.Contracts.Generation;

namespace StemStudio.Contracts.Tracks;

/// <summary>Where a track came from (1.8.0; vocal members 1.9.0). Absent on imported tracks.</summary>
public sealed record TrackOriginDto
{
    /// <summary>Imported or generated.</summary>
    public required TrackOriginKindDto Kind { get; init; }

    /// <summary>The generation of a generated track.</summary>
    public Guid? GenerationId { get; init; }

    /// <summary>The take of a generated track.</summary>
    public int? TakeIndex { get; init; }

    /// <summary>The source track of a restyle, for the source link.</summary>
    public Guid? SourceTrackId { get; init; }

    /// <summary>True for generated tracks.</summary>
    public bool AiGenerated { get; init; }

    /// <summary>AI voice label (1.9.0).</summary>
    public VocalLabelDto? Vocal { get; init; }

    /// <summary>Voice profile of a converted take (1.9.0).</summary>
    public string? VoiceProfileName { get; init; }

    /// <summary>Lyrics sheet of a song (1.9.0).</summary>
    public Guid? LyricsSheetId { get; init; }
}
