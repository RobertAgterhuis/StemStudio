using StemStudio.Contracts.Recording;
using StemStudio.Web.Recording;

namespace StemStudio.Web.InstrumentViews;

/// <summary>Readable texts of singing feedback (016 ui-test-ids <c>singing-latency</c>).</summary>
public static class SingingText
{
    /// <summary>"Latency: 42.0 ms (calibrated)", the estimate with a hint, or a hint without an open input.</summary>
    /// <param name="latency">The 015 latency in use, or null.</param>
    /// <returns>The text.</returns>
    public static string Latency(LatencyInUse? latency) => latency switch
    {
        null => "Latency: no microphone is open - choose one in Recording settings",
        { Method: LatencyMethodDto.Estimated } => $"Latency: {RecordingText.Milliseconds(latency.Samples)} (estimated - calibrate in Recording settings)",
        _ => $"Latency: {RecordingText.Milliseconds(latency.Samples)} (calibrated)",
    };
}
