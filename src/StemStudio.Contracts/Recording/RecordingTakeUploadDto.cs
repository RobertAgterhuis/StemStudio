namespace StemStudio.Contracts.Recording;

/// <summary>The <c>metadata</c> part of <c>createRecordingTake</c> (backend-api 1.14.0, feature 015).</summary>
public sealed record RecordingTakeUploadDto
{
    /// <summary>Session of the Record press.</summary>
    public required Guid SessionId { get; init; }

    /// <summary>Loop pass 1..50; null without loop.</summary>
    public int? PassIndex { get; init; }

    /// <summary>A final pass shorter than the loop.</summary>
    public required bool Partial { get; init; }

    /// <summary>Song sample of the first sample (&gt;= 0).</summary>
    public required long StartSample { get; init; }

    /// <summary>Length (&gt;= 22,050); must match the WAV.</summary>
    public required long LengthSamples { get; init; }

    /// <summary>Channel mode.</summary>
    public required InputChannelModeDto InputMode { get; init; }

    /// <summary>Applied compensation, 0..44,100.</summary>
    public required int LatencySamples { get; init; }

    /// <summary>Where the latency came from.</summary>
    public required LatencyMethodDto LatencyMethod { get; init; }

    /// <summary>The calibration used.</summary>
    public Guid? CalibrationId { get; init; }

    /// <summary>Input device label (&lt;= 200).</summary>
    public string? DeviceLabel { get; init; }

    /// <summary>Recorded part.</summary>
    public required RecordingPartDto Part { get; init; }

    /// <summary>Mix loaded while recording.</summary>
    public Guid? MixId { get; init; }

    /// <summary>Name (1..80); null = "Take n".</summary>
    public string? Name { get; init; }

    /// <summary>Playback speed while recording; must be 1 (else 422 <c>recording-speed</c>).</summary>
    public required double Speed { get; init; }

    /// <summary>Transpose while recording; must be 0.</summary>
    public required int TransposeSemitones { get; init; }

    /// <summary>Capture gaps filled with zeros.</summary>
    public required int Dropouts { get; init; }

    /// <summary>Start of the recording.</summary>
    public required DateTimeOffset RecordedAt { get; init; }
}
