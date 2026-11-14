namespace StemStudio.Api;

/// <summary>API settings, bound from the <c>StemStudio</c> configuration section (environment: <c>StemStudio__DataDir</c>, ...).</summary>
public sealed class StemStudioApiOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "StemStudio";

    /// <summary>Data directory for the SQLite database, uploads and stems; relative paths resolve against the content root.</summary>
    public string DataDir { get; set; } = "../../data/api";

    /// <summary>Base URL of the processing worker.</summary>
    public Uri WorkerBaseUrl { get; set; } = new("http://localhost:8000");

    /// <summary>Browser origin of the Blazor app, allowed by CORS to GET tracks and stems.</summary>
    public string WebOrigin { get; set; } = "http://localhost:8080";

    /// <summary>Interval of the job synchroniser in milliseconds.</summary>
    public int JobPollIntervalMs { get; set; } = 1000;
}
