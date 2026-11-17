namespace StemStudio.Api.LyricAssist;

/// <summary>
/// Composition hook of feature 023 (lyric writing assistant): dictionaries, assist service and runner, suggestions, revisions,
/// loopback actions and the endpoints. The only place 023 registers or maps anything (no edit of <see cref="ApiComposition"/>).
/// </summary>
public static class LyricAssistRegistration
{
    /// <summary>Registers the 023 services.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddLyricAssist(this IServiceCollection services)
    {
        services.AddScoped<LyricsSheetChanges>();
        services.AddScoped<ILyricAssistCanceller, LyricAssistCanceller>();
        return services;
    }

    /// <summary>Maps the 023 endpoints (tag <c>lyric-assist</c>).</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapLyricAssistEndpoints(this IEndpointRouteBuilder app) => app;
}
