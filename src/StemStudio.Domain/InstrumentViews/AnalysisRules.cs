using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace StemStudio.Domain.InstrumentViews;

/// <summary>What happened to a track that may start an analysis automatically (data-model 7.3).</summary>
public enum AnalysisTrigger
{
    /// <summary>The standard separation completed.</summary>
    StandardCompleted,

    /// <summary>A detail split completed.</summary>
    DetailCompleted,
}

/// <summary>Switches of the automatic starts (<c>StemStudio:InstrumentViews</c>).</summary>
/// <param name="AutoChords">Start chords after the standard separation and when stale.</param>
/// <param name="AutoDrumHits">Start drum hits after the drums split and when stale.</param>
public sealed record AutoRuleSettings(bool AutoChords, bool AutoDrumHits);

/// <summary>One input stem of an analysis with the SHA-256 of its file.</summary>
/// <param name="Channel">Channel id (<c>bass</c>, <c>drums.kick</c>, <c>vocals.lead</c>).</param>
/// <param name="Sha256">Lower-case hex SHA-256 of the stem file.</param>
public sealed record AnalysisInput(string Channel, string Sha256);

/// <summary>Auto rules (data-model 7.3), the source fingerprint (7.1) and the estimates (7.7).</summary>
public static class AnalysisRules
{
    /// <summary>Fixed overhead of every estimate in seconds.</summary>
    public const int EstimateOverheadSeconds = 5;

    /// <summary>Extra seconds while the BTC weights are not downloaded yet.</summary>
    public const int WeightsDownloadSeconds = 15;

    /// <summary>Whether a trigger starts an analysis of this kind on its own (vocal pitch never does).</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="trigger">The trigger.</param>
    /// <param name="settings">The switches.</param>
    /// <returns>True when the kind follows the trigger.</returns>
    public static bool FollowsTrigger(AnalysisKind kind, AnalysisTrigger trigger, AutoRuleSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return (kind, trigger) switch
        {
            (AnalysisKind.Chords, AnalysisTrigger.StandardCompleted) => settings.AutoChords,
            (AnalysisKind.DrumHits, AnalysisTrigger.DetailCompleted) => settings.AutoDrumHits,
            _ => false,
        };
    }

    /// <summary>Whether a stale analysis of this kind runs again on its own.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="settings">The switches.</param>
    /// <returns>True for chords with <c>AutoChords</c> and drum hits with <c>AutoDrumHits</c>.</returns>
    public static bool RerunsWhenStale(AnalysisKind kind, AutoRuleSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return kind switch
        {
            AnalysisKind.Chords => settings.AutoChords,
            AnalysisKind.DrumHits => settings.AutoDrumHits,
            _ => false,
        };
    }

    /// <summary>
    /// Whether an automatic start may replace the current state: missing, none, unavailable (the prerequisite may be back),
    /// or stale without edits. Never over a run, a failure (the user retries) or a ready result.
    /// </summary>
    /// <param name="current">The stored analysis, or null.</param>
    /// <returns>True when an automatic start is allowed.</returns>
    public static bool MayStartAutomatically(TrackAnalysis? current) => current switch
    {
        null => true,
        { Status: AnalysisStatus.None or AnalysisStatus.Unavailable } => true,
        { Status: AnalysisStatus.Stale, HasEdits: false } => true,
        _ => false,
    };

    /// <summary>The source fingerprint: SHA-256 over the inputs sorted by channel (<c>channel=sha256</c> lines).</summary>
    /// <param name="inputs">The inputs.</param>
    /// <returns>Lower-case hex SHA-256 (64 characters).</returns>
    public static string Fingerprint(IEnumerable<AnalysisInput> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var text = new StringBuilder();
        foreach (var input in inputs.OrderBy(input => input.Channel, StringComparer.Ordinal))
        {
            text.Append(CultureInfo.InvariantCulture, $"{input.Channel}={input.Sha256}\n");
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    /// <summary>The shown estimate: <c>duration x factor + 5 s</c> (+ 15 s while the BTC weights are missing), rounded up (after 6 decimals, so 200 x 0.035 is 7).</summary>
    /// <param name="durationSeconds">Track length.</param>
    /// <param name="factor">Seconds per audio second.</param>
    /// <param name="weightsMissing">The BTC weights are not downloaded yet.</param>
    /// <returns>Seconds.</returns>
    public static int EstimateSeconds(double durationSeconds, double factor, bool weightsMissing) =>
        (int)Math.Ceiling(Math.Round((Math.Max(durationSeconds, 0) * Math.Max(factor, 0)) + EstimateOverheadSeconds + (weightsMissing ? WeightsDownloadSeconds : 0), 6));
}
