namespace StemStudio.Domain.Recording;

/// <summary>The values of a new take (validated upload metadata plus the API's scan of the WAV).</summary>
public sealed record NewTake
{
    /// <summary>The track.</summary>
    public required Guid TrackId { get; init; }

    /// <summary>The library item holding the audio.</summary>
    public required Guid AssetId { get; init; }

    /// <summary>Number per track.</summary>
    public required int Number { get; init; }

    /// <summary>Name, or null for the default name.</summary>
    public string? Name { get; init; }

    /// <summary>Part.</summary>
    public required RecordingPart Part { get; init; }

    /// <summary>Session of the Record press.</summary>
    public required Guid SessionId { get; init; }

    /// <summary>Loop pass 1..50 or null.</summary>
    public int? PassIndex { get; init; }

    /// <summary>Partial last pass.</summary>
    public required bool Partial { get; init; }

    /// <summary>First song sample.</summary>
    public required long StartSample { get; init; }

    /// <summary>Length.</summary>
    public required long LengthSamples { get; init; }

    /// <summary>Channel mode.</summary>
    public required InputChannelMode InputMode { get; init; }

    /// <summary>Channels of the uploaded WAV.</summary>
    public required int InputChannels { get; init; }

    /// <summary>Applied compensation.</summary>
    public required int LatencySamples { get; init; }

    /// <summary>Latency source.</summary>
    public required LatencyMethod LatencyMethod { get; init; }

    /// <summary>Calibration used.</summary>
    public Guid? CalibrationId { get; init; }

    /// <summary>Device label as sent.</summary>
    public string? DeviceLabel { get; init; }

    /// <summary>Mix loaded while recording.</summary>
    public Guid? MixId { get; init; }

    /// <summary>Level facts of the upload.</summary>
    public required PeakScan Scan { get; init; }

    /// <summary>Capture gaps.</summary>
    public required int Dropouts { get; init; }

    /// <summary>Start of the recording (UTC).</summary>
    public required DateTime RecordedAt { get; init; }
}

/// <summary>One recorded take of a track (feature 015, data-model 2.1); its audio is a hidden library item (origin recording).</summary>
public sealed class RecordingTake
{
    private RecordingTake()
    {
        Name = string.Empty;
    }

    /// <summary>Identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>The track (cascade delete).</summary>
    public Guid TrackId { get; private set; }

    /// <summary>The library item holding <c>audio.flac</c>.</summary>
    public Guid AssetId { get; private set; }

    /// <summary>Number per track, from 1.</summary>
    public int Number { get; private set; }

    /// <summary>Name, 1..80 characters.</summary>
    public string Name { get; private set; }

    /// <summary>The stem the take replaces in compare mode.</summary>
    public RecordingPart Part { get; private set; }

    /// <summary>Session of the Record press.</summary>
    public Guid SessionId { get; private set; }

    /// <summary>Loop pass, or null.</summary>
    public int? PassIndex { get; private set; }

    /// <summary>Partial last pass.</summary>
    public bool Partial { get; private set; }

    /// <summary>First song sample.</summary>
    public long StartSample { get; private set; }

    /// <summary>Length in samples.</summary>
    public long LengthSamples { get; private set; }

    /// <summary>Channel mode.</summary>
    public InputChannelMode InputMode { get; private set; }

    /// <summary>Channels of the uploaded WAV.</summary>
    public int InputChannels { get; private set; }

    /// <summary>Applied compensation.</summary>
    public int LatencySamples { get; private set; }

    /// <summary>Latency source.</summary>
    public LatencyMethod LatencyMethod { get; private set; }

    /// <summary>Calibration used (set null when it is deleted).</summary>
    public Guid? CalibrationId { get; private set; }

    /// <summary>Device label.</summary>
    public string? DeviceLabel { get; private set; }

    /// <summary>Mix loaded while recording (set null when it is deleted).</summary>
    public Guid? MixId { get; private set; }

    /// <summary>Starred.</summary>
    public bool Favorite { get; private set; }

    /// <summary>Peak, -150..0 dBFS.</summary>
    public double PeakDbfs { get; private set; }

    /// <summary>Samples at full scale.</summary>
    public int ClippedSamples { get; private set; }

    /// <summary>Capture gaps.</summary>
    public int Dropouts { get; private set; }

    /// <summary>Total timing adjustment applied.</summary>
    public int AdjustmentSamples { get; private set; }

    /// <summary>+1 per applied timing adjustment.</summary>
    public int AlignmentRevision { get; private set; }

    /// <summary>Start of the recording (UTC).</summary>
    public DateTime RecordedAt { get; private set; }

    /// <summary>Created (UTC).</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>Last change (UTC).</summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>Peak below -50 dBFS.</summary>
    public bool Quiet => PeakDbfs < RecordingConstants.QuietPeakDbfs;

    /// <summary>Creates a take.</summary>
    /// <param name="id">New identifier.</param>
    /// <param name="values">The values.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <returns>The take.</returns>
    /// <exception cref="DomainValidationException">The name breaks a rule.</exception>
    public static RecordingTake Create(Guid id, NewTake values, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(values.Scan);
        return new RecordingTake
        {
            Id = id,
            TrackId = values.TrackId,
            AssetId = values.AssetId,
            Number = values.Number,
            Name = values.Name is null ? TakeNaming.DefaultName(values.Number, values.Part) : TakeNaming.Validate(values.Name),
            Part = values.Part,
            SessionId = values.SessionId,
            PassIndex = values.PassIndex,
            Partial = values.Partial,
            StartSample = values.StartSample,
            LengthSamples = values.LengthSamples,
            InputMode = values.InputMode,
            InputChannels = values.InputChannels,
            LatencySamples = values.LatencySamples,
            LatencyMethod = values.LatencyMethod,
            CalibrationId = values.CalibrationId,
            DeviceLabel = TakeNaming.CleanDeviceLabel(values.DeviceLabel),
            MixId = values.MixId,
            PeakDbfs = values.Scan.PeakDbfs,
            ClippedSamples = values.Scan.ClippedSamples,
            Dropouts = values.Dropouts,
            RecordedAt = values.RecordedAt,
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc,
        };
    }

    /// <summary>Renames the take (trimmed, 1..80, no control characters).</summary>
    /// <param name="name">New name.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <exception cref="DomainValidationException">The name breaks a rule.</exception>
    public void Rename(string? name, DateTime nowUtc) => (Name, UpdatedAt) = (TakeNaming.Validate(name), nowUtc);

    /// <summary>Stars or unstars the take.</summary>
    /// <param name="favorite">Starred.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void SetFavorite(bool favorite, DateTime nowUtc) => (Favorite, UpdatedAt) = (favorite, nowUtc);

    /// <summary>Changes the part.</summary>
    /// <param name="part">The part.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void SetPart(RecordingPart part, DateTime nowUtc) => (Part, UpdatedAt) = (part, nowUtc);

    /// <summary>Applies a timing adjustment (US7, data-model 2.4): the new span, the summed adjustment and revision + 1.</summary>
    /// <param name="startSample">New first song sample.</param>
    /// <param name="lengthSamples">New length.</param>
    /// <param name="adjustmentSamples">This replacement's shift.</param>
    /// <param name="scan">Level facts of the new audio.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <exception cref="ArgumentOutOfRangeException">The adjustment is outside +/-200 ms.</exception>
    public void Realign(long startSample, long lengthSamples, int adjustmentSamples, PeakScan scan, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(scan);
        if (!TakeRules.IsValidAdjustment(adjustmentSamples))
        {
            throw new ArgumentOutOfRangeException(nameof(adjustmentSamples), adjustmentSamples, "The adjustment must lie within +/-8,820 samples.");
        }

        (StartSample, LengthSamples, PeakDbfs, ClippedSamples) = (startSample, lengthSamples, scan.PeakDbfs, scan.ClippedSamples);
        (AdjustmentSamples, AlignmentRevision, UpdatedAt) = (AdjustmentSamples + adjustmentSamples, AlignmentRevision + 1, nowUtc);
    }

    /// <summary>Forgets a deleted calibration (the latency stays).</summary>
    public void DetachCalibration() => CalibrationId = null;

    /// <summary>Forgets a deleted mix.</summary>
    public void DetachMix() => MixId = null;
}
