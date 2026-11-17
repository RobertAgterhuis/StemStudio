namespace StemStudio.Api.Library;

/// <summary>
/// Composition hook of feature 022 (library by artist and album). Skeleton (architect, 022+023 round start): the
/// backend-022 lane fills both methods (filer, backfill, folder index, search, library info, endpoints) so no other lane
/// edits <see cref="ApiComposition"/>.
/// </summary>
public static class LibraryRegistration
{
    /// <summary>Registers the 022 services.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddLibraryFolders(this IServiceCollection services) => services;

    /// <summary>Maps the 022 endpoints (tag <c>library</c>).</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapLibraryEndpoints(this IEndpointRouteBuilder app) => app;
}
