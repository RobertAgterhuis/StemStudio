using Microsoft.Extensions.DependencyInjection.Extensions;
using StemStudio.Api.LibraryCare.Backup;
using StemStudio.Api.LibraryCare.Jobs;
using StemStudio.Api.LibraryCare.Storage;
using StemStudio.Api.Storage;

namespace StemStudio.Api.LibraryCare;

/// <summary>Registers library care (feature 013): folders, jobs, backup, restore, storage and clean-up.</summary>
public static class LibraryCareRegistration
{
    /// <summary>Adds the library-care services.</summary>
    /// <param name="services">Services.</param>
    /// <param name="options">Bound API options.</param>
    /// <param name="contentRoot">Content root (relative <c>BackupDir</c> values resolve against it).</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddLibraryCare(this IServiceCollection services, StemStudioApiOptions options, string contentRoot)
    {
        ArgumentNullException.ThrowIfNull(options);
        var backups = Path.GetFullPath(Path.Combine(contentRoot, options.BackupDir));
        services.AddSingleton(provider => new LibraryCarePaths(provider.GetRequiredService<DataDirectory>(), backups));
        services.AddSingleton<LibraryJobStore>();
        services.AddSingleton<LibraryJobRunner>();
        services.AddSingleton<LibraryJobRecovery>();
        services.TryAddSingleton<ILibraryFiles, LibraryFiles>();
        services.AddSingleton<LibraryWalker>();
        AddBackup(services);
        return services;
    }

    private static void AddBackup(IServiceCollection services)
    {
        services.AddSingleton<BackupFolder>();
        services.AddSingleton(provider => new DatabaseSnapshot(provider.GetRequiredService<TimeProvider>(), provider.GetRequiredService<ILogger<DatabaseSnapshot>>()));
        services.AddSingleton<BackupSelection>();
        services.AddSingleton<BackupRunServices>();
        services.AddSingleton<BackupRun>();
        services.AddScoped<BackupEstimates>();
        services.AddScoped<BackupService>();
    }

    /// <summary>Start-up recovery of library care (before the endpoints are served).</summary>
    /// <param name="services">Root services.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public static Task RecoverLibraryCareAsync(this IServiceProvider services, CancellationToken cancellationToken) =>
        services.GetRequiredService<LibraryJobRecovery>().RecoverAsync(cancellationToken);
}
