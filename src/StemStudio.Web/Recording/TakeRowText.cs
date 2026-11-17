using System.Globalization;
using StemStudio.Contracts.Recording;

namespace StemStudio.Web.Recording;

/// <summary>A take warning (<c>rec-take-warning</c> <c>data-warning</c>).</summary>
/// <param name="Code"><c>clipped</c>, <c>quiet</c> or <c>dropouts</c>.</param>
/// <param name="Text">Readable text with every warning of the take.</param>
public sealed record TakeWarning(string Code, string Text);

/// <summary>Texts of a take row (FR-014, US5).</summary>
public static class TakeRowText
{
    /// <summary>"Pass n", "Pass n, partial", "partial", or null.</summary>
    /// <param name="item">The take.</param>
    /// <returns>The text.</returns>
    public static string? Pass(TakeItem item) => (item.PassIndex, item.Partial) switch
    {
        ({ } pass, true) => $"Pass {pass}, partial",
        ({ } pass, false) => $"Pass {pass}",
        (null, true) => "partial",
        _ => null,
    };

    /// <summary>The status label next to the row.</summary>
    /// <param name="item">The take.</param>
    /// <returns>The label.</returns>
    public static string Status(TakeItem item) => item.Status switch
    {
        "local" => item.UploadFailed ? "Not saved" : "Saving...",
        "saving" => "Saving...",
        "failed" => "Saving failed",
        _ => "Saved",
    };

    /// <summary>Latency badge: method and milliseconds.</summary>
    /// <param name="take">The take.</param>
    /// <returns>For example "calibrated 25.0 ms".</returns>
    public static string Latency(RecordingTakeDto take)
    {
        var method = take.Latency.Method switch
        {
            LatencyMethodDto.Loopback => "calibrated",
            LatencyMethodDto.Manual => "manual",
            _ => "estimated",
        };
        return string.Create(CultureInfo.InvariantCulture, $"{method} {take.Latency.Milliseconds:0.0} ms");
    }

    /// <summary>The first warning code with the text of every warning, or null.</summary>
    /// <param name="take">The take.</param>
    /// <returns>The warning.</returns>
    public static TakeWarning? Warning(RecordingTakeDto take)
    {
        var found = new List<(string Code, string Text)>();
        Add(found, take.ClippedSamples > 0, "clipped", "Clipped: turn the input gain down");
        Add(found, take.Quiet, "quiet", "Very quiet: turn the input gain up");
        Add(found, take.Dropouts > 0, "dropouts", $"{take.Dropouts} dropouts: close other programs");
        return found.Count == 0 ? null : new TakeWarning(found[0].Code, string.Join("; ", found.Select(f => f.Text)));
    }

    /// <summary>Expert details: device, input mode, latency samples, dropouts, clipped count.</summary>
    /// <param name="take">The take.</param>
    /// <returns>The text.</returns>
    public static string Details(RecordingTakeDto take) => string.Create(
        CultureInfo.InvariantCulture,
        $"{take.DeviceLabel ?? "Unknown input"}, {StemStudio.Web.Services.WireName.Of(take.InputMode)}, {take.Latency.Samples} samples latency, {take.Dropouts} dropouts, {take.ClippedSamples} clipped samples");

    private static void Add(List<(string Code, string Text)> found, bool applies, string code, string text)
    {
        if (applies)
        {
            found.Add((code, text));
        }
    }
}
