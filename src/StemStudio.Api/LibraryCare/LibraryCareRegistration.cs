using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using StemStudio.Api.LibraryCare.Backup;
using StemStudio.Api.LibraryCare.Jobs;
using StemStudio.Api.LibraryCare.Restore;
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
        services.AddSingleton<StorageOverviewCache>();
        services.AddSingleton<StorageVolumes>();
        services.AddScoped<StorageOverviewService>();
        services.AddScoped<Cleanup.CleanupPlanner>();
        services.AddScoped<Cleanup.CleanupExecutor>();
        AddBackup(services);
        AddRestore(services);
        return services;
    }

    private static void AddRestore(IServiceCollection services)
    {
        services.AddSingleton<IKnownMigrations>(provider => provider.GetRequiredService<MigrationCatalog>());
        services.AddSingleton<ArchiveValidator>();
        services.AddSingleton(provider => new StagingExtractor(provider.GetRequiredService<IOptions<StemStudioApiOptions>>().Value.LibraryCare, provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton<StagedDatabase>();
        services.AddSingleton<RestoreJournal>();
        services.TryAddSingleton<IJournalStepHook, NoJournalStepHook>();
        services.AddSingleton(provider => new LibrarySwitcher(
            provider.GetRequiredService<Gate.LibraryGate>(), provider.GetRequiredService<RestoreJournal>(), provider.GetRequiredService<ILibraryFiles>(),
            provider.GetRequiredService<IJournalStepHook>(), TimeSpan.FromMilliseconds(200))
        {
            AfterSwitch = () => RestoreRun.UseWal(provider.GetRequiredService<LiveDatabase>()),
        });
        services.AddSingleton<PreviousLibraryStore>();
        services.AddSingleton<IPreviousLibraryReader>(provider => provider.GetRequiredService<PreviousLibraryStore>());
        services.AddSingleton<LibraryMerger>();
        services.AddSingleton<RestoreRunServices>();
        services.AddSingleton<RestoreRun>();
        services.AddSingleton<UndoRun>();
        services.AddSingleton<RestoreEndpointServices>();
        services.AddScoped<RestoreService>();
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
        services.AddSingleton<MigrationCatalog>();
        services.AddSingleton<BackupCatalog>();
        services.AddSingleton<VerifyRun>();
        services.AddScoped<LibraryState>();
    }

    /// <summary>Start-up recovery of library care (before the endpoints are served).</summary>
    /// <param name="services">Root services.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task (the restore journal is recovered, and committed journals finished, before the jobs).</returns>
    public static async Task RecoverLibraryCareAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        var journal = services.GetRequiredService<RestoreJournal>();
        await journal.RecoverAsync(services.GetRequiredService<ILibraryFiles>(), cancellationToken);
        if (await journal.CompleteCommittedAsync(cancellationToken) > 0)
        {
            RestoreRun.UseWal(services.GetRequiredService<LiveDatabase>());
        }

        await services.GetRequiredService<LibraryJobRecovery>().RecoverAsync(cancellationToken);
    }
}
