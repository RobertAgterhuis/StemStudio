namespace StemStudio.Contracts.Generation;

/// <summary>Body of <c>POST /api/generations/{id}/takes/{index}/voice-conversions</c> (1.9.0).</summary>
public sealed record VoiceConversionRequest
{
    /// <summary>The voice profile.</summary>
    public required Guid ProfileId { get; init; }

    /// <summary>Semitones, -12..12.</summary>
    public int PitchShift { get; init; }

    /// <summary>Shift to the profile's register.</summary>
    public bool AutoShift { get; init; } = true;
}
