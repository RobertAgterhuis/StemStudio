using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Persistence;

/// <summary>Registers the SQLite context and the repositories.</summary>
public static class PersistenceServiceCollectionExtensions
{
    /// <summary>Adds <see cref="StemStudioDbContext"/> (SQLite) and the scoped repositories.</summary>
    /// <param name="services">Service collection.</param>
    /// <param name="connectionString">SQLite connection string, e.g. <c>Data Source=/data/stemstudio.db</c>.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddStemStudioPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<StemStudioDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<ITrackRepository, TrackRepository>();
        services.AddScoped<IMixRepository, MixRepository>();
        return services;
    }
}
