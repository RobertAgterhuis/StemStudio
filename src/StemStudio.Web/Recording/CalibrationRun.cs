using System.Globalization;
using StemStudio.Contracts.Recording;
using StemStudio.Web.Api;

namespace StemStudio.Web.Recording;

/// <summary>
/// One calibration dialog (FR-007, US2): runs the loopback click test, stores a successful measurement for the open input,
/// explains a failure with a tip, and takes a manual value (0-1000 ms in 0.1 ms steps). The engine never throws for a failed
/// measurement; it refuses only while playing ("Pause playback to calibrate").
/// </summary>
/// <param name="engine">The recording engine.</param>
/// <param name="latency">The calibrations.</param>
/// <param name="input">The open input (its label is the calibration key).</param>
public sealed class CalibrationRun(IRecordingEngine engine, LatencyChoice latency, InputInfo input)
{
    /// <summary>Largest manual latency in milliseconds.</summary>
    public const double MaxManualMilliseconds = 1000;

    /// <summary>True while the clicks play.</summary>
    public bool Running { get; private set; }

    /// <summary>The last measurement, or null.</summary>
    public CalibrationResult? Result { get; private set; }

    /// <summary>A readable error (refused start, failed save), or null.</summary>
    public string? Error { get; private set; }

    /// <summary>True after the value was stored.</summary>
    public bool Saved { get; private set; }

    /// <summary>The input label.</summary>
    public string DeviceLabel => input.Label;

    /// <summary>The stored calibration of the input, or null.</summary>
    public InputCalibrationDto? Stored => latency.For(input);

    /// <summary>The manual value shown in the fields (ms).</summary>
    public double ManualMilliseconds { get; private set; }

    /// <summary>Runs the click test; a successful result is stored at once (one calibration per input).</summary>
    /// <returns>A task that completes with the result.</returns>
    public async Task StartAsync()
    {
        (Running, Error, Result, Saved) = (true, null, null, false);
        try
        {
            Result = await engine.CalibrateLatencyAsync();
            await (Result.Ok ? SaveAsync(Measured(Result)) : Task.CompletedTask);
        }
        catch (RecordingException ex)
        {
            Error = ex.Message;
        }
        finally
        {
            Running = false;
        }
    }

    /// <summary>Sets the manual value (clamped to 0-1000 ms, rounded to 0.1 ms).</summary>
    /// <param name="milliseconds">The value.</param>
    public void SetManual(double milliseconds) =>
        ManualMilliseconds = Math.Round(Math.Clamp(milliseconds, 0, MaxManualMilliseconds), 1, MidpointRounding.AwayFromZero);

    /// <summary>Stores the manual value (rounded to whole samples, data-model 2.5).</summary>
    /// <returns>A task that completes when stored.</returns>
    public Task SaveManualAsync() => SaveAsync(new InputCalibrationInput
    {
        DeviceLabel = input.Label,
        DeviceIdHint = input.DeviceId,
        LatencySamples = (int)RecordingLimits.ToSamples(ManualMilliseconds),
        Method = CalibrationMethodDto.Manual,
        OutputLatencySamples = input.OutputLatencySamples,
    });

    /// <summary>Forgets the stored calibration of the input (the estimate is used again).</summary>
    /// <returns>A task that completes when deleted.</returns>
    public async Task DeleteAsync()
    {
        if (Stored is { } stored)
        {
            Error = await ApiCall.RunAsync(() => latency.DeleteAsync(stored));
            Saved = false;
        }
    }

    /// <summary>The tip for a failure reason (US2 scenario 3).</summary>
    /// <param name="reason">The engine reason.</param>
    /// <returns>One sentence.</returns>
    public static string Tip(string? reason) => reason switch
    {
        "noSignal" => "No clicks came back. Connect an output of your interface to the input with a cable, or hold a headphone cup against the microphone, and turn the input gain up.",
        "tooFewClicks" => "Only some clicks came back. Turn the output or the input gain up and keep the room quiet.",
        "inconsistent" => "The clicks came back at different times. Use a cable instead of the speakers, or close other audio programs.",
        "tooLong" => "The delay is longer than one second. Check that monitoring in other programs is off, or enter the latency by hand.",
        _ => "The measurement did not work. Try again or enter the latency by hand.",
    };

    /// <summary>Milliseconds with one decimal for a sample count.</summary>
    /// <param name="samples">Samples, or null.</param>
    /// <returns>The text, or an empty string.</returns>
    public static string Ms(int? samples) => samples is { } s
        ? string.Create(CultureInfo.InvariantCulture, $"{Math.Round(s * 1000.0 / RecordingLimits.SampleRate, 1):0.0} ms")
        : string.Empty;

    private InputCalibrationInput Measured(CalibrationResult result) => new()
    {
        DeviceLabel = input.Label,
        DeviceIdHint = input.DeviceId,
        LatencySamples = result.LatencySamples!.Value,
        Method = CalibrationMethodDto.Loopback,
        SpreadSamples = result.SpreadSamples,
        DetectedClicks = result.DetectedClicks,
        OutputLatencySamples = result.OutputLatencySamples,
    };

    private async Task SaveAsync(InputCalibrationInput value)
    {
        Error = await ApiCall.RunAsync(() => latency.SaveAsync(value));
        Saved = Error is null;
    }
}
