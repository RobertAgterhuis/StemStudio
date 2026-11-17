using StemStudio.Contracts.Recording;

namespace StemStudio.Api.Recording;

/// <summary>Options of play-along recording (section <c>StemStudio:Recording</c>, data-model 1).</summary>
public sealed class RecordingOptions
{
    /// <summary>The configuration section.</summary>
    public const string SectionName = "StemStudio:Recording";

    /// <summary>Request body limit of the two take upload endpoints (default 256 MiB); the global upload limit is unchanged.</summary>
    public long MaxUploadBytes { get; set; } = RecordingLimits.DefaultMaxUploadBytes;
}
