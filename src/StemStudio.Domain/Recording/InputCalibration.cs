namespace StemStudio.Domain.Recording;

/// <summary>The values of a calibration as sent (upsert by device label).</summary>
/// <param name="DeviceLabel">Device label (trimmed, 1..200).</param>
/// <param name="DeviceIdHint">Last device id seen (&lt;= 512).</param>
/// <param name="LatencySamples">Round trip, 0..44,100.</param>
/// <param name="Method">Loopback or manual.</param>
/// <param name="SpreadSamples">Loopback spread 0..44 (loopback only).</param>
/// <param name="DetectedClicks">Loopback clicks 6..12 (loopback only).</param>
/// <param name="OutputLatencySamples">Output latency snapshot, 0..44,100.</param>
public sealed record CalibrationValues(
    string? DeviceLabel, string? DeviceIdHint, int LatencySamples, CalibrationMethod Method, int? SpreadSamples, int? DetectedClicks, int OutputLatencySamples)
{
    /// <summary>The violations; empty when valid.</summary>
    /// <returns>Errors with request field names.</returns>
    public IReadOnlyList<DomainError> Validate()
    {
        var errors = new ValidationErrors();
        var label = DeviceLabel?.Trim() ?? string.Empty;
        errors.Check(label.Length is >= 1 and <= RecordingConstants.MaxDeviceLabelLength, "deviceLabel", $"Must be 1 to {RecordingConstants.MaxDeviceLabelLength} characters long.");
        errors.Check(!label.Any(char.IsControl), "deviceLabel", "Must not contain control characters.");
        errors.Check(DeviceIdHint is null || DeviceIdHint.Length <= RecordingConstants.MaxDeviceIdHintLength, "deviceIdHint", $"Must be at most {RecordingConstants.MaxDeviceIdHintLength} characters long.");
        errors.Check(LatencySamples is >= 0 and <= RecordingConstants.MaxLatencySamples, "latencySamples", $"Must be between 0 and {RecordingConstants.MaxLatencySamples}.");
        errors.Check(OutputLatencySamples is >= 0 and <= RecordingConstants.MaxLatencySamples, "outputLatencySamples", $"Must be between 0 and {RecordingConstants.MaxLatencySamples}.");
        errors.Check(Enum.IsDefined(Method), "method", "Must be loopback or manual.");
        LoopbackErrors(errors);
        return errors.Errors;
    }

    private void LoopbackErrors(ValidationErrors errors)
    {
        var loopback = Method == CalibrationMethod.Loopback;
        errors.Check(SpreadSamples is null || (loopback && SpreadSamples is >= 0 and <= RecordingConstants.MaxSpreadSamples), "spreadSamples", loopback
            ? $"Must be between 0 and {RecordingConstants.MaxSpreadSamples}."
            : "Only a loopback calibration has a spread.");
        errors.Check(DetectedClicks is null || (loopback && DetectedClicks is >= RecordingConstants.MinDetectedClicks and <= RecordingConstants.MaxDetectedClicks), "detectedClicks", loopback
            ? $"Must be between {RecordingConstants.MinDetectedClicks} and {RecordingConstants.MaxDetectedClicks}."
            : "Only a loopback calibration has detected clicks.");
    }
}

/// <summary>A stored input-latency calibration of one device (data-model 2.5); unique by trimmed device label.</summary>
public sealed class InputCalibration
{
    private InputCalibration()
    {
        DeviceLabel = string.Empty;
    }

    /// <summary>Identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Device label (unique, ordinal, trimmed).</summary>
    public string DeviceLabel { get; private set; }

    /// <summary>Last device id seen.</summary>
    public string? DeviceIdHint { get; private set; }

    /// <summary>Round trip in samples.</summary>
    public int LatencySamples { get; private set; }

    /// <summary>Loopback or manual.</summary>
    public CalibrationMethod Method { get; private set; }

    /// <summary>Loopback spread.</summary>
    public int? SpreadSamples { get; private set; }

    /// <summary>Loopback clicks found.</summary>
    public int? DetectedClicks { get; private set; }

    /// <summary>Output latency at measurement.</summary>
    public int OutputLatencySamples { get; private set; }

    /// <summary>When it was measured (UTC).</summary>
    public DateTime MeasuredAt { get; private set; }

    /// <summary>The stored form of a label (trimmed).</summary>
    /// <param name="label">The label.</param>
    /// <returns>The key.</returns>
    public static string LabelKey(string? label) => label?.Trim() ?? string.Empty;

    /// <summary>Creates a calibration.</summary>
    /// <param name="id">New identifier.</param>
    /// <param name="values">Valid values.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <returns>The calibration.</returns>
    /// <exception cref="DomainValidationException">A value breaks a rule.</exception>
    public static InputCalibration Create(Guid id, CalibrationValues values, DateTime nowUtc)
    {
        var calibration = new InputCalibration { Id = id };
        calibration.Replace(values, nowUtc);
        return calibration;
    }

    /// <summary>Replaces the measurement (upsert of the same label).</summary>
    /// <param name="values">Valid values.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <exception cref="DomainValidationException">A value breaks a rule.</exception>
    public void Replace(CalibrationValues values, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Validate() is { Count: > 0 } errors)
        {
            throw new DomainValidationException(errors);
        }

        (DeviceLabel, DeviceIdHint, LatencySamples, Method) = (LabelKey(values.DeviceLabel), values.DeviceIdHint, values.LatencySamples, values.Method);
        (SpreadSamples, DetectedClicks, OutputLatencySamples, MeasuredAt) = (values.SpreadSamples, values.DetectedClicks, values.OutputLatencySamples, nowUtc);
    }
}
