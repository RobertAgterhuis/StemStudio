using StemStudio.Domain.Generations;

namespace StemStudio.Domain.Tracks;

/// <summary>Where a generated track came from (feature 009 data-model 3.3); imported tracks have none.</summary>
public sealed record TrackOrigin
{
    /// <summary>Creates the origin.</summary>
    /// <param name="generationId">The generation.</param>
    /// <param name="takeIndex">The take (1 or 2).</param>
    /// <param name="kind">The generation kind.</param>
    /// <param name="sourceTrackId">The restyled track, for the source link.</param>
    public TrackOrigin(Guid generationId, int takeIndex, GenerationKind kind, Guid? sourceTrackId)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(takeIndex, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(takeIndex, Generation.MaxTakes);
        (GenerationId, TakeIndex, Kind, SourceTrackId) = (generationId, takeIndex, kind, sourceTrackId);
    }

    /// <summary>The generation.</summary>
    public Guid GenerationId { get; init; }

    /// <summary>The take number.</summary>
    public int TakeIndex { get; init; }

    /// <summary>The generation kind.</summary>
    public GenerationKind Kind { get; init; }

    /// <summary>The source track of a restyle.</summary>
    public Guid? SourceTrackId { get; init; }

    /// <summary>AI voice label of the take (010); null for restyles.</summary>
    public VocalLabel? Vocal { get; init; }

    /// <summary>Voice profile of a converted take (010).</summary>
    public string? VoiceProfileName { get; init; }

    /// <summary>The lyrics sheet of a song (010).</summary>
    public Guid? LyricsSheetId { get; init; }

    /// <summary>Always true.</summary>
    public bool AiGenerated { get; } = true;
}
