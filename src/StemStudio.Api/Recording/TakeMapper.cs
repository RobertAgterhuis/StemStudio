using StemStudio.Api.Tracks;
using StemStudio.Contracts;
using StemStudio.Contracts.Recording;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Recording;

namespace StemStudio.Api.Recording;

/// <summary>Maps takes and calibrations to the 1.14.0 DTOs; status, error, size and audio URL come from the take's asset (data-model 2.1).</summary>
public static class TakeMapper
{
    /// <summary>Maps a take with its asset.</summary>
    /// <param name="take">The take.</param>
    /// <param name="asset">Its asset, or null when it is gone.</param>
    /// <returns>The DTO.</returns>
    public static RecordingTakeDto ToDto(RecordingTake take, Asset? asset)
    {
        ArgumentNullException.ThrowIfNull(take);
        return WithLevels(Header(take, asset), take) with
        {
            InputMode = Enum.Parse<InputChannelModeDto>(take.InputMode.ToString()),
            InputChannels = take.InputChannels,
            Latency = Latency(take),
            DeviceLabel = take.DeviceLabel,
            MixId = take.MixId,
            Favorite = take.Favorite,
            AdjustmentSamples = take.AdjustmentSamples,
            AlignmentRevision = take.AlignmentRevision,
        };
    }

    /// <summary>Every required member at its default; the mapping fills it in groups (keeps each unit small).</summary>
    private static readonly RecordingTakeDto Blank = new()
    {
        Id = Guid.Empty,
        TrackId = Guid.Empty,
        Number = 0,
        Name = string.Empty,
        Part = RecordingPartDto.None,
        Status = TakeStatusDto.Saving,
        SessionId = Guid.Empty,
        Partial = false,
        StartSample = 0,
        LengthSamples = 0,
        StartSeconds = 0,
        DurationSeconds = 0,
        InputMode = InputChannelModeDto.Mono1,
        InputChannels = 1,
        Latency = new TakeLatencyDto { Samples = 0, Milliseconds = 0, Method = LatencyMethodDto.Estimated },
        Favorite = false,
        PeakDbfs = 0,
        ClippedSamples = 0,
        Dropouts = 0,
        Quiet = false,
        AdjustmentSamples = 0,
        AlignmentRevision = 0,
        SizeBytes = 0,
        AssetId = Guid.Empty,
        RecordedAt = default,
        CreatedAt = default,
        UpdatedAt = default,
    };

    private static RecordingTakeDto Header(RecordingTake take, Asset? asset) => Blank with
    {
        Id = take.Id,
        TrackId = take.TrackId,
        Number = take.Number,
        Name = take.Name,
        Part = Enum.Parse<RecordingPartDto>(take.Part.ToString()),
        Status = Status(asset),
        Error = asset is { Status: AssetStatus.Failed } ? asset.Error : null,
        SessionId = take.SessionId,
        PassIndex = take.PassIndex,
        Partial = take.Partial,
        StartSample = take.StartSample,
        LengthSamples = take.LengthSamples,
        StartSeconds = LatencyConversion.ToSeconds(take.StartSample),
        DurationSeconds = LatencyConversion.ToSeconds(take.LengthSamples),
        SizeBytes = asset?.SizeBytes ?? 0,
        AssetId = take.AssetId,
        AudioUrl = asset is { Files.Count: > 0 } ? ApiRoutes.AssetFile(asset.Id, AssetFileRoles.Audio) : null,
        RecordedAt = TrackMapper.Utc(take.RecordedAt),
        CreatedAt = TrackMapper.Utc(take.CreatedAt),
        UpdatedAt = TrackMapper.Utc(Max(take.UpdatedAt, asset?.UpdatedAt)),
    };

    private static RecordingTakeDto WithLevels(RecordingTakeDto dto, RecordingTake take) => dto with
    {
        PeakDbfs = take.PeakDbfs,
        ClippedSamples = take.ClippedSamples,
        Dropouts = take.Dropouts,
        Quiet = take.Quiet,
    };

    /// <summary>The take status of an asset status: queued and rendering are saving.</summary>
    /// <param name="asset">The asset, or null.</param>
    /// <returns>The status.</returns>
    public static TakeStatusDto Status(Asset? asset) => asset?.Status switch
    {
        AssetStatus.Ready => TakeStatusDto.Ready,
        AssetStatus.Failed or null => TakeStatusDto.Failed,
        _ => TakeStatusDto.Saving,
    };

    /// <summary>Maps a calibration.</summary>
    /// <param name="calibration">The calibration.</param>
    /// <returns>The DTO.</returns>
    public static InputCalibrationDto ToDto(InputCalibration calibration)
    {
        ArgumentNullException.ThrowIfNull(calibration);
        return new InputCalibrationDto
        {
            Id = calibration.Id,
            DeviceLabel = calibration.DeviceLabel,
            DeviceIdHint = calibration.DeviceIdHint,
            LatencySamples = calibration.LatencySamples,
            LatencyMilliseconds = LatencyConversion.ToMilliseconds(calibration.LatencySamples),
            Method = calibration.Method == CalibrationMethod.Loopback ? CalibrationMethodDto.Loopback : CalibrationMethodDto.Manual,
            SpreadSamples = calibration.SpreadSamples,
            DetectedClicks = calibration.DetectedClicks,
            OutputLatencySamples = calibration.OutputLatencySamples,
            MeasuredAt = TrackMapper.Utc(calibration.MeasuredAt),
        };
    }

    private static TakeLatencyDto Latency(RecordingTake take) => new()
    {
        Samples = take.LatencySamples,
        Milliseconds = LatencyConversion.ToMilliseconds(take.LatencySamples),
        Method = Enum.Parse<LatencyMethodDto>(take.LatencyMethod.ToString()),
        CalibrationId = take.CalibrationId,
    };

    private static DateTime Max(DateTime take, DateTime? asset) => asset is { } value && value > take ? value : take;
}

/// <summary>File roles of library items used by recording.</summary>
public static class AssetFileRoles
{
    /// <summary>The audio file.</summary>
    public const string Audio = "audio";
}
