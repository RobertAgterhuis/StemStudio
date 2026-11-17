namespace StemStudio.Contracts.Recording;

/// <summary>One recorded take of a track (backend-api 1.14.0, feature 015).</summary>
public sealed record RecordingTakeDto
{
    /// <summary>Take id.</summary>
    public required Guid Id { get; init; }

    /// <summary>The track.</summary>
    public required Guid TrackId { get; init; }

    /// <summary>Running number per track, from 1.</summary>
    public required int Number { get; init; }

    /// <summary>Name, 1..80 characters.</summary>
    public required string Name { get; init; }

    /// <summary>Recorded part.</summary>
    public required RecordingPartDto Part { get; init; }

    /// <summary>Storage state.</summary>
    public required TakeStatusDto Status { get; init; }

    /// <summary>Readable error while failed (&lt;= 500 characters).</summary>
    public string? Error { get; init; }

    /// <summary>Shared by the passes of one Record press.</summary>
    public required Guid SessionId { get; init; }

    /// <summary>Loop pass 1..50; null without loop.</summary>
    public int? PassIndex { get; init; }

    /// <summary>A final pass shorter than the loop.</summary>
    public required bool Partial { get; init; }

    /// <summary>Song sample (44.1 kHz) of the take's first sample.</summary>
    public required long StartSample { get; init; }

    /// <summary>Length in samples.</summary>
    public required long LengthSamples { get; init; }

    /// <summary><see cref="StartSample"/> / 44,100.</summary>
    public required double StartSeconds { get; init; }

    /// <summary><see cref="LengthSamples"/> / 44,100.</summary>
    public required double DurationSeconds { get; init; }

    /// <summary>Channel mode recorded with.</summary>
    public required InputChannelModeDto InputMode { get; init; }

    /// <summary>1 or 2.</summary>
    public required int InputChannels { get; init; }

    /// <summary>Applied latency compensation.</summary>
    public required TakeLatencyDto Latency { get; init; }

    /// <summary>Input device label.</summary>
    public string? DeviceLabel { get; init; }

    /// <summary>Mix that was loaded while recording.</summary>
    public Guid? MixId { get; init; }

    /// <summary>Starred.</summary>
    public required bool Favorite { get; init; }

    /// <summary>Peak, -150..0 dBFS.</summary>
    public required double PeakDbfs { get; init; }

    /// <summary>Samples at full scale.</summary>
    public required int ClippedSamples { get; init; }

    /// <summary>Frames filled with zeros because of capture gaps.</summary>
    public required int Dropouts { get; init; }

    /// <summary>Peak below -50 dBFS.</summary>
    public required bool Quiet { get; init; }

    /// <summary>Total timing adjustment applied (US7).</summary>
    public required int AdjustmentSamples { get; init; }

    /// <summary>Incremented by every timing replacement.</summary>
    public required int AlignmentRevision { get; init; }

    /// <summary>Size of the stored FLAC.</summary>
    public required long SizeBytes { get; init; }

    /// <summary>The hidden library asset (origin recording).</summary>
    public required Guid AssetId { get; init; }

    /// <summary><c>/api/assets/{assetId}/files/audio</c> once ready (padded FLAC).</summary>
    public string? AudioUrl { get; init; }

    /// <summary>Start of the recording.</summary>
    public required DateTimeOffset RecordedAt { get; init; }

    /// <summary>Created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last change.</summary>
    public required DateTimeOffset UpdatedAt { get; init; }
}
