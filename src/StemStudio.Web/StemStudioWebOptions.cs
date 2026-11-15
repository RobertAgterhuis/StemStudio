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
}
