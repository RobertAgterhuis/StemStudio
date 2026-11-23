namespace StemStudio.Web;

/// <summary>Configuration section <c>StemStudio</c> of the web front end.</summary>
public sealed class StemStudioWebOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "StemStudio";

    /// <summary>Name of the typed/named <see cref="HttpClient"/> that calls the API server-side.</summary>
    public const string ApiClientName = "StemStudioApi";

    /// <summary>API base URL used by the Blazor server (container network or localhost).</summary>
    public Uri ApiBaseUrl { get; set; } = new("http://localhost:8081");

    /// <summary>API base URL as seen by the browser; the audio engine fetches stems from here.</summary>
    public Uri PublicApiBaseUrl { get; set; } = new("http://localhost:8081");

    /// <summary>
    /// The stem quality report (<c>StemStudio:QualityReport</c>, feature 020, owner decision 2026-11-23); the same setting as
    /// the API's.
    /// </summary>
    public QualityReportWebOptions QualityReport { get; set; } = new();
}

/// <summary>The switch of the stem quality report in the web front end (<c>StemStudio:QualityReport</c>).</summary>
public sealed class QualityReportWebOptions
{
    /// <summary>
    /// True: the track page shows the quality card and the mixer the quality badges (<c>StemStudio:QualityReport:Enabled</c>,
    /// env <c>StemStudio__QualityReport__Enabled</c>). Default false: nothing of the quality report is shown, because gate G1
    /// failed on real music.
    /// </summary>
    public bool Enabled { get; set; }
}
