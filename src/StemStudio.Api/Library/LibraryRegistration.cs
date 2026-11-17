using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Library;

/// <summary>
/// Composition hook of feature 022 (library by artist and album): repositories, filer, start-up backfill, folder index,
/// search, library info and the endpoints of the tag <c>library</c>. Registered and mapped only here.
/// </summary>
public static class LibraryRegistration
{
    /// <summary>Registers the 022 services.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddLibraryFolders(this IServiceCollection services)
    {
        services.AddScoped<ILibraryRepository, LibraryRepository>();
        services.AddScoped<ILibraryFiler, LibraryFiler>();
        services.AddSingleton<LibraryFilingBackfill>();
        services.AddSingleton<ILibraryFilingTrigger>(provider => provider.GetRequiredService<LibraryFilingBackfill>());
        services.AddHostedService(provider => provider.GetRequiredService<LibraryFilingBackfill>());
        services.AddSingleton<LibraryIndex>();
        services.AddScoped<LibraryBrowseService>();
        return services;
    }

    /// <summary>Maps the 022 endpoints (tag <c>library</c>).</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapLibraryEndpoints(this IEndpointRouteBuilder app) => app.MapLibraryFolderEndpoints();
}
