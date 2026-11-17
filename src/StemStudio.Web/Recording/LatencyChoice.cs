using StemStudio.Contracts.Recording;
using StemStudio.Web.Api;

namespace StemStudio.Web.Recording;

/// <summary>The latency a take is recorded with (FR-007): the stored calibration of the input, else the browser estimate.</summary>
/// <param name="Samples">L in samples.</param>
/// <param name="Method">Where L comes from.</param>
/// <param name="CalibrationId">The calibration used, or null.</param>
public sealed record LatencyInUse(int Samples, LatencyMethodDto Method, Guid? CalibrationId)
{
    /// <summary><c>data-method</c> of <c>rec-latency-status</c>.</summary>
    public string MethodName => Method.ToString().ToLowerInvariant();

    /// <summary>Text of <c>rec-latency-status</c>.</summary>
    public string Text => Method switch
    {
        LatencyMethodDto.Loopback => $"Calibrated latency: {RecordingText.Milliseconds(Samples)}",
        LatencyMethodDto.Manual => $"Latency set by hand: {RecordingText.Milliseconds(Samples)}",
        _ => $"Estimated latency: {RecordingText.Milliseconds(Samples)} (calibrate for exact timing)",
    };
}

/// <summary>The stored calibrations and the latency in use for the open input (FR-007, US2).</summary>
/// <param name="api">The backend client.</param>
public sealed class LatencyChoice(IStemStudioApiClient api)
{
    private List<InputCalibrationDto> _calibrations = [];

    /// <summary>The stored calibrations.</summary>
    public IReadOnlyList<InputCalibrationDto> Calibrations => _calibrations;

    /// <summary>Loads the calibrations; a failing list leaves the estimate in use.</summary>
    /// <returns>A task that completes when loaded.</returns>
    public async Task LoadAsync()
    {
        var (list, _) = await ApiCall.TryAsync(() => api.ListCalibrationsAsync());
        _calibrations = list is null ? [] : [.. list];
    }

    /// <summary>The calibration stored for a device label, or null.</summary>
    /// <param name="input">The open input, or null.</param>
    /// <returns>The calibration.</returns>
    public InputCalibrationDto? For(InputInfo? input) =>
        input is null ? null : _calibrations.FirstOrDefault(c => string.Equals(c.DeviceLabel, input.Label, StringComparison.Ordinal));

    /// <summary>The latency in use for the open input, or null without an input.</summary>
    /// <param name="input">The open input.</param>
    /// <returns>The latency.</returns>
    public LatencyInUse? InUse(InputInfo? input) => For(input) is { } c
        ? new LatencyInUse(c.LatencySamples, c.Method == CalibrationMethodDto.Manual ? LatencyMethodDto.Manual : LatencyMethodDto.Loopback, c.Id)
        : input is null ? null : new LatencyInUse(input.EstimatedLatencySamples, LatencyMethodDto.Estimated, null);

    /// <summary>True when the output latency differs from the calibration's snapshot by more than 5 ms (<c>rec-setup-changed</c>).</summary>
    /// <param name="input">The open input.</param>
    /// <returns>Whether to suggest calibrating again.</returns>
    public bool SetupChanged(InputInfo? input) =>
        For(input) is { } c && Math.Abs(input!.OutputLatencySamples - c.OutputLatencySamples) > RecordingLimits.SetupChangeSamples;

    /// <summary>Stores a calibration (replacing the one of the same label).</summary>
    /// <param name="input">The measurement or manual value.</param>
    /// <returns>The stored calibration.</returns>
    public async Task<InputCalibrationDto> SaveAsync(InputCalibrationInput input)
    {
        var saved = await api.UpsertCalibrationAsync(input);
        _calibrations.RemoveAll(c => c.DeviceLabel == saved.DeviceLabel);
        _calibrations.Add(saved);
        return saved;
    }

    /// <summary>Forgets a calibration.</summary>
    /// <param name="calibration">The calibration.</param>
    /// <returns>A task that completes when deleted.</returns>
    public async Task DeleteAsync(InputCalibrationDto calibration)
    {
        await api.DeleteCalibrationAsync(calibration.Id);
        _calibrations.RemoveAll(c => c.Id == calibration.Id);
    }
}
