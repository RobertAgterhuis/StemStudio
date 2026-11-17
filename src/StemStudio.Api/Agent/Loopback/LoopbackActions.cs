using Microsoft.Extensions.Options;
using StemStudio.Agent.Ports;

namespace StemStudio.Api.Agent.Loopback;

/// <summary>
/// <see cref="IStemStudioActions"/> over the API's own public endpoints (feature 011, T035): one class per port group, all
/// sharing one <see cref="LoopbackClient"/>; the agent never touches the database, files or the worker.
/// </summary>
/// <param name="client">Loopback client.</param>
public sealed class LoopbackActions(LoopbackClient client) : IStemStudioActions
{
    /// <inheritdoc />
    public ITrackActions Tracks { get; } = new LoopbackTrackActions(client);

    /// <inheritdoc />
    public IMixActions Mixes { get; } = new LoopbackMixActions(client);

    /// <inheritdoc />
    public IArtistActions Artists { get; } = new LoopbackArtistActions(client);

    /// <inheritdoc />
    public ILyricsActions Lyrics { get; } = new LoopbackLyricsActions(client);

    /// <inheritdoc />
    public IAssetActions Assets { get; } = new LoopbackAssetActions(client);

    /// <inheritdoc />
    public IStyleActions Styles { get; } = new LoopbackStyleActions(client);

    /// <inheritdoc />
    public IGenerationActions Generations { get; } = new LoopbackGenerationActions(client);

    /// <inheritdoc />
    public ISongActions Songs { get; } = new LoopbackSongActions(client);

    /// <inheritdoc />
    public IVoiceActions Voices { get; } = new LoopbackVoiceActions(client);

    /// <inheritdoc />
    public ILibraryActions Library { get; } = new LoopbackLibraryActions(client);
}

/// <summary>Registers the loopback actions (typed <see cref="HttpClient"/> at <c>StemStudio__Agent__LoopbackBaseUrl</c>).</summary>
public static class LoopbackRegistration
{
    /// <summary>Default loopback base URL when neither the setting nor <c>ASPNETCORE_HTTP_PORTS</c> gives one.</summary>
    public static readonly Uri DefaultBaseUrl = new("http://127.0.0.1:8080/");

    /// <summary>Adds the loopback client and <see cref="IStemStudioActions"/>.</summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Configuration.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddLoopbackActions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient<LoopbackClient>((sp, http) =>
        {
            http.BaseAddress = BaseUrl(sp.GetRequiredService<IOptions<AgentHostOptions>>().Value, configuration);
            http.Timeout = TimeSpan.FromMinutes(5);
        });
        services.AddSingleton<IStemStudioActions>(sp => new LoopbackActions(sp.GetRequiredService<LoopbackClient>()));
        return services;
    }

    /// <summary>The configured base URL, else <c>http://127.0.0.1:{first ASPNETCORE_HTTP_PORTS}</c>, with a trailing slash.</summary>
    /// <param name="options">Host options.</param>
    /// <param name="configuration">Configuration (<c>HTTP_PORTS</c> / <c>ASPNETCORE_HTTP_PORTS</c>).</param>
    /// <returns>The base URL.</returns>
    public static Uri BaseUrl(AgentHostOptions options, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configuration);
        if (options.LoopbackBaseUrl is { } configured)
        {
            return new Uri(configured.ToString().TrimEnd('/') + "/");
        }

        var port = (configuration["HTTP_PORTS"] ?? configuration["ASPNETCORE_HTTP_PORTS"])?.Split(';', ',')[0].Trim();
        return string.IsNullOrEmpty(port) ? DefaultBaseUrl : new Uri($"http://127.0.0.1:{port}/");
    }
}
