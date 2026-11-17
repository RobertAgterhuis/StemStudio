namespace StemStudio.Contracts.Recording;

/// <summary><c>Provenance.recording</c> of a take's asset, origin recording (backend-api 1.14.0, feature 015).</summary>
public sealed record RecordingRecipeDto
{
    /// <summary>The wire value of <see cref="Kind"/>.</summary>
    public const string RecordingKind = "recording";

    /// <summary>The wire value of <see cref="CaptureFile"/>.</summary>
    public const string CaptureFileName = "capture.wav";

    /// <summary>Always <c>recording</c>.</summary>
    public string Kind { get; init; } = RecordingKind;

    /// <summary>The take.</summary>
    public required Guid TakeId { get; init; }

    /// <summary>Always <c>capture.wav</c> (the transient upload the FLAC is rendered from).</summary>
    public string CaptureFile { get; init; } = CaptureFileName;
}
