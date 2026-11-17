using System.Globalization;
using StemStudio.Contracts.Recording;
using StemStudio.Web.Api;

namespace StemStudio.Web.Recording;

/// <summary>
/// Timing adjustment of the loaded take (FR-016, US7, Expert): a live, playback-only shift (engine
/// <c>setTakeAdjustment</c>, absolute in samples relative to the take's start) that "Apply" stores: the engine re-encodes
/// the shifted take (<c>captureWav(takeId)</c>, cut at song sample 0) and the API replaces the audio, keeping the old one
/// until the new one is ready.
/// </summary>
public sealed partial class TakeLibrary
{
    /// <summary>Largest shift in milliseconds (+-200 ms = +-8,820 samples).</summary>
    public const double MaxAdjustmentMilliseconds = 200;

    /// <summary>The unapplied shift of the loaded take in milliseconds (0.1 ms steps).</summary>
    public double PendingAdjustmentMs { get; private set; }

    /// <summary>Shifts the loaded take live (clamped to +-200 ms, rounded to 0.1 ms).</summary>
    /// <param name="milliseconds">Positive = later.</param>
    /// <returns>A task that completes when delivered.</returns>
    public async Task PreviewAdjustmentAsync(double milliseconds)
    {
        PendingAdjustmentMs = Math.Round(Math.Clamp(milliseconds, -MaxAdjustmentMilliseconds, MaxAdjustmentMilliseconds), 1, MidpointRounding.AwayFromZero);
        try
        {
            await engine.SetTakeAdjustmentAsync((int)RecordingLimits.ToSamples(PendingAdjustmentMs));
        }
        catch (RecordingException ex)
        {
            Error = ex.Message;
        }
    }

    /// <summary>Back to the stored timing.</summary>
    /// <returns>A task that completes when delivered.</returns>
    public Task ResetAdjustmentAsync() => PreviewAdjustmentAsync(0);

    /// <summary>Stores the pending shift (a no-op at 0); the take is <c>saving</c> until the new audio is ready.</summary>
    /// <returns>A task that completes when the server accepted the new audio.</returns>
    public async Task ApplyAdjustmentAsync()
    {
        if (Loaded is not { Take: { } take } item || PendingAdjustmentMs == 0)
        {
            return;
        }

        var samples = (int)RecordingLimits.ToSamples(PendingAdjustmentMs);
        var (updated, error) = await ApiCall.TryAsync(() => ReplaceAsync(take, samples));
        Error = error;
        if (updated is not null)
        {
            item.Apply(updated);
            _realigned.Add(item);
            PendingAdjustmentMs = 0;
        }
    }

    /// <summary>"Timing adjusted by X ms" for a take with a stored adjustment, or null.</summary>
    /// <param name="take">The take.</param>
    /// <returns>The note.</returns>
    public static string? AdjustmentNote(RecordingTakeDto? take) => take is { AdjustmentSamples: not 0 } t
        ? string.Create(CultureInfo.InvariantCulture, $"Timing adjusted by {t.AdjustmentSamples * 1000.0 / RecordingLimits.SampleRate:+0.0;-0.0} ms")
        : null;

    /// <summary>Span of the re-encoded take: the start moves by the shift and is cut at song sample 0.</summary>
    /// <param name="take">The take.</param>
    /// <param name="samples">The shift.</param>
    /// <returns>The metadata of <c>PUT /api/takes/{id}/audio</c>.</returns>
    public static ReplaceTakeAudioMetadataDto Shifted(RecordingTakeDto take, int samples)
    {
        var start = take.StartSample + samples;
        return new ReplaceTakeAudioMetadataDto
        {
            StartSample = Math.Max(0, start),
            LengthSamples = take.LengthSamples + Math.Min(0, start),
            AdjustmentSamples = samples,
        };
    }

    private async Task<RecordingTakeDto> ReplaceAsync(RecordingTakeDto take, int samples)
    {
        var reference = await engine.CaptureWavAsync(take.Id.ToString("D", CultureInfo.InvariantCulture));
        await using (reference)
        {
            await using var stream = await reference.OpenReadStreamAsync(RecordingLimits.DefaultMaxUploadBytes);
            return await api.ReplaceTakeAudioAsync(take.Id, stream, Shifted(take, samples));
        }
    }
}
