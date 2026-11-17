namespace StemStudio.Contracts.Recording;

/// <summary>The <c>metadata</c> part of <c>replaceRecordingTakeAudio</c> (US7) (backend-api 1.14.0, feature 015).</summary>
public sealed record ReplaceTakeAudioMetadataDto
{
    /// <summary>New first song sample (&gt;= 0).</summary>
    public required long StartSample { get; init; }

    /// <summary>New length (&gt;= 22,050).</summary>
    public required long LengthSamples { get; init; }

    /// <summary>Shift of this replacement, -8,820..8,820 (positive = later).</summary>
    public required int AdjustmentSamples { get; init; }
}
