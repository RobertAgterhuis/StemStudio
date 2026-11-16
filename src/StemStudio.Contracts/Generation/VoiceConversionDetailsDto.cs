namespace StemStudio.Contracts.Generation;

/// <summary>What a voice conversion was made from (1.9.0).</summary>
public sealed record VoiceConversionDetailsDto
{
    /// <summary>The song generation.</summary>
    public required Guid SourceGenerationId { get; init; }

    /// <summary>The song take.</summary>
    public required int SourceTakeIndex { get; init; }

    /// <summary>The profile; null after it was deleted.</summary>
    public Guid? VoiceProfileId { get; init; }

    /// <summary>Name snapshot (ends with "(profile deleted)" after deletion).</summary>
    public required string VoiceProfileName { get; init; }

    /// <summary>The track whose stems are converted.</summary>
    public Guid? ImportedTrackId { get; init; }

    /// <summary>Semitones, -12..12.</summary>
    public int PitchShift { get; init; }

    /// <summary>Shift to the profile's register.</summary>
    public bool AutoShift { get; init; } = true;
}
