using System.Globalization;
using StemStudio.Contracts.InstrumentViews;

namespace StemStudio.Web.InstrumentViews;

/// <summary>Readable status of a track analysis (chords, drum hits, reference melody; data-model 7.2).</summary>
public static class AnalysisText
{
    /// <summary>The status line.</summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="analysis">The analysis, or null while loading.</param>
    /// <param name="what">The job, e.g. "Chord detection".</param>
    /// <param name="result">The result, e.g. "chords".</param>
    /// <returns>The text.</returns>
    public static string Status<T>(TrackAnalysisDto<T>? analysis, string what, string result)
        where T : class => analysis?.Status switch
        {
            null => $"Loading {result}...",
            AnalysisStatusDto.None => $"{Capital(result)} have not been detected yet.",
            AnalysisStatusDto.Queued => $"{what} is waiting to start{Estimate(analysis.EstimateSeconds)}.",
            AnalysisStatusDto.Running => $"{Verb(what)} {result}: {Percent(analysis.Progress)}.",
            AnalysisStatusDto.Ready => $"{Capital(result)} are ready.",
            AnalysisStatusDto.Failed => $"{what} failed: {analysis.Reason ?? "unknown reason"}",
            AnalysisStatusDto.Unavailable => analysis.Reason is { } reason ? $"{what} is not available: {reason}" : $"{what} is not available.",
            _ => $"The stems changed since the {result} were detected; detect them again.",
        };

    /// <summary>True while the analysis is queued or running (the UI polls).</summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="analysis">The analysis.</param>
    /// <returns>Whether to poll.</returns>
    public static bool InProgress<T>(TrackAnalysisDto<T>? analysis)
        where T : class => analysis?.Status is AnalysisStatusDto.Queued or AnalysisStatusDto.Running;

    /// <summary>Progress 0..1 as a whole percentage.</summary>
    /// <param name="progress">The progress.</param>
    /// <returns>0..100.</returns>
    public static int PercentValue(double progress) => (int)Math.Round(Math.Clamp(progress, 0, 1) * 100);

    private static string Percent(double progress) => string.Create(CultureInfo.InvariantCulture, $"{PercentValue(progress)} %");

    private static string Estimate(int? seconds) => seconds is { } s ? string.Create(CultureInfo.InvariantCulture, $" (about {s} s)") : string.Empty;

    private static string Capital(string text) => char.ToUpperInvariant(text[0]) + text[1..];

    // "Chord detection" -> "Detecting"; "Melody analysis" -> "Analysing".
    private static string Verb(string what) => what.EndsWith("analysis", StringComparison.Ordinal) ? "Analysing" : "Detecting";
}
