namespace StemStudio.Api.LyricAssist;

/// <summary>
/// Composition hook of feature 023 (lyric writing assistant). Skeleton (architect, 022+023 round start): the
/// backend-023 lane fills both methods (dictionaries, assist service, suggestions, revisions, loopback actions,
/// endpoints) so no other lane edits <see cref="ApiComposition"/>.
/// </summary>
public static class LyricAssistRegistration
{
    /// <summary>Registers the 023 services.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddLyricAssist(this IServiceCollection services) => services;

    /// <summary>Maps the 023 endpoints (tag <c>lyric-assist</c>).</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapLyricAssistEndpoints(this IEndpointRouteBuilder app) => app;
}
