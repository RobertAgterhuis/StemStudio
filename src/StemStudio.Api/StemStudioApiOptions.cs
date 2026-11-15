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

    /// <summary>
    /// Attempt timeout for creating a worker job (POST). Spotify imports resolve the YouTube match synchronously
    /// (spotDL), so this is at least 60 seconds.
    /// </summary>
    public TimeSpan WorkerCreateJobTimeout { get; set; } = TimeSpan.FromSeconds(90);

    /// <summary>Attempt timeout for every other worker call: status polls, results and stem response headers.</summary>
    public TimeSpan WorkerPollTimeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Age after which a queued track without a worker job is failed as "Import interrupted"; longer than
    /// <see cref="WorkerCreateJobTimeout"/>, so an import that is still waiting for the worker is never swept.
    /// </summary>
    public TimeSpan OrphanedImportTimeout { get; set; } = TimeSpan.FromSeconds(120);

    /// <summary>Exports are refused (409 <c>insufficient-disk-space</c>) below this many free bytes on the data drive (feature 007).</summary>
    public long MinFreeDiskBytes { get; set; } = StemStudio.Contracts.ContractLimits.MinFreeDiskBytesDefault;
}
