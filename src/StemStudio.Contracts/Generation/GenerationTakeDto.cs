namespace StemStudio.Contracts.Generation;

/// <summary>One take of a generation (1.8.0; vocal members 1.9.0).</summary>
public sealed record GenerationTakeDto
{
    /// <summary>Take number from 1.</summary>
    public required int Index { get; init; }

    /// <summary>Seed.</summary>
    public required int Seed { get; init; }

    /// <summary>Length.</summary>
    public required double DurationSeconds { get; init; }

    /// <summary>Relative URL of the take audio.</summary>
    public required string AudioUrl { get; init; }

    /// <summary>The take's item in the asset library.</summary>
    public Guid? LibraryAssetId { get; init; }

    /// <summary>The track imported from the take.</summary>
    public Guid? ImportedTrackId { get; init; }

    /// <summary>Model and revision.</summary>
    public required GenerationTakeModelDto Model { get; init; }

    /// <summary>Always true.</summary>
    public bool AiGenerated { get; init; } = true;

    /// <summary>AI voice label (1.9.0); null for restyles.</summary>
    public VocalLabelDto? Vocal { get; init; }

    /// <summary>Voice profile of a converted take (1.9.0).</summary>
    public string? VoiceProfileName { get; init; }

    /// <summary>The converted vocal of a voice-conversion take (1.9.0).</summary>
    public string? VocalAudioUrl { get; init; }
}
