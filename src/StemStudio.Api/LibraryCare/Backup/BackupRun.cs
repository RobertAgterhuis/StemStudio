using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using StemStudio.Api.LibraryCare.Archive;
using StemStudio.Api.LibraryCare.Gate;
using StemStudio.Api.LibraryCare.Jobs;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Domain.LibraryCare;

namespace StemStudio.Api.LibraryCare.Backup;

/// <summary>Services of a backup run.</summary>
/// <param name="Gate">The write gate.</param>
/// <param name="Paths">Library-care folders.</param>
/// <param name="Live">The live database.</param>
/// <param name="Snapshot">Database snapshot.</param>
/// <param name="Selection">File selection.</param>
/// <param name="Files">Free-space seam.</param>
public sealed record BackupRunServices(LibraryGate Gate, LibraryCarePaths Paths, LiveDatabase Live, DatabaseSnapshot Snapshot, BackupSelection Selection, ILibraryFiles Files);

/// <summary>
/// The work of a backup job (backup-archive 1.0.0 writer rules): snapshot, scrub, files, README and manifest, check,
/// rename. The write gate is closed for the whole run and opened in every outcome; staging and the partial file are removed.
/// </summary>
/// <param name="services">Services.</param>
/// <param name="options">API options.</param>
/// <param name="clock">Clock.</param>
public sealed class BackupRun(BackupRunServices services, IOptions<StemStudioApiOptions> options, TimeProvider clock)
{
    private static readonly string AppVersion = Version();

    /// <summary>Runs a backup.</summary>
    /// <param name="request">Options and archive name.</param>
    /// <param name="context">Progress.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The report.</returns>
    public async Task<LibraryJobReportDto> RunAsync(BackupRequest request, LibraryJobContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        var staging = services.Paths.Staging(context.JobId);
        var partial = services.Paths.Archive(request.FileName) + LibraryCareLimits.PartialSuffix;
        services.Gate.TryBeginBackup();
        try
        {
            return await BackUpAsync(request, context, staging, partial, cancellationToken);
        }
        finally
        {
            services.Gate.EndBackup();
            DeleteQuietly(staging, partial);
        }
    }

    private async Task<LibraryJobReportDto> BackUpAsync(BackupRequest request, LibraryJobContext context, string staging, string partial, CancellationToken cancellationToken)
    {
        var snapshotTime = clock.GetUtcNow().UtcDateTime;
        var database = Path.Combine(staging, "stemstudio.db");
        context.Report(new LibraryJobProgress(LibraryJobStageDto.Snapshot, "Copying the database", 0, 0, 0, null));
        await services.Snapshot.CreateAsync(services.Live.ConnectionString, database, cancellationToken);
        context.Report(new LibraryJobProgress(LibraryJobStageDto.Scrub, "Preparing the database copy", 1, 0, 0, null));
        var warnings = (await new BackupScrubber().ScrubAsync(database, BackupOptionsMapping.Excluded(request.Options), snapshotTime, cancellationToken)).ToList();
        var (plan, migrations) = await ReadPlanAsync(database, cancellationToken);
        await using var writer = new BackupArchiveWriter(services.Files, options.Value.LibraryCare, services.Paths.BackupsDirectory, clock);
        var written = await writer.WriteAsync(new ArchiveJob(partial, database, plan, warnings), context, cancellationToken);
        var manifest = ManifestBuilder.Build(new ManifestInput
        {
            BackupId = Guid.NewGuid(),
            CreatedAt = snapshotTime,
            AppVersion = AppVersion,
            Migrations = migrations,
            Selected = BackupOptionsMapping.Selected(request.Options).ToList(),
            Counts = plan.Counts,
            Entries = written,
            VoiceProfiles = plan.VoiceProfiles.Where(profile => written.Any(entry => entry.Path == profile.SamplePath)).ToList(),
            Warnings = warnings,
        });
        await writer.FinishAsync(manifest, context, cancellationToken);
        Check(partial, manifest, context);
        var final = services.Paths.Archive(request.FileName);
        ArchiveWriter.Commit(partial, final);
        return Report(request.FileName, new FileInfo(final).Length, manifest);
    }

    private async Task<(BackupPlan Plan, IReadOnlyList<string> Migrations)> ReadPlanAsync(string database, CancellationToken cancellationToken)
    {
        await using var staged = BackupScrubber.StagedContext(database);
        var plan = await services.Selection.ReadAsync(staged, cancellationToken);
        return (plan, await MigrationsAsync(database, cancellationToken));
    }

    /// <summary>Every migration id of a database, in order.</summary>
    /// <param name="database">The database file.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The ids.</returns>
    public static async Task<IReadOnlyList<string>> MigrationsAsync(string database, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(DatabaseSnapshot.StagingConnectionString(database, readOnly: true));
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId";
        var ids = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            ids.Add(reader.GetString(0));
        }

        return ids;
    }

    private static void Check(string partial, BackupManifest manifest, LibraryJobContext context)
    {
        context.Report(new LibraryJobProgress(LibraryJobStageDto.Check, "Checking the archive", 99, context.Job.BytesTotal, context.Job.BytesTotal, null));
        if (!ArchiveWriter.MatchesManifest(partial, manifest))
        {
            throw new LibraryJobException(LibraryJobErrorCodeDto.IoError, "The written archive does not match its manifest; try again.");
        }
    }

    private static BackupReportDto Report(string fileName, long bytes, BackupManifest manifest) => new()
    {
        FileName = fileName,
        Included = manifest.Entries.GroupBy(entry => entry.Category).OrderBy(group => group.Key).Select(group => new CategoryCountDto
        {
            Category = BackupOptionsMapping.ToDto(group.Key),
            Items = group.Key == BackupCategory.Database ? 1 : group.Select(entry => entry.Entity?.Id).Distinct(StringComparer.Ordinal).Count(),
            Files = group.Count(),
            Bytes = group.Sum(entry => entry.SizeBytes),
        }).ToList(),
        Entries = manifest.Entries.Count,
        Bytes = bytes,
        Warnings = manifest.Warnings,
    };

    private static void DeleteQuietly(string staging, string partial)
    {
        if (Directory.Exists(staging))
        {
            Directory.Delete(staging, recursive: true);
        }

        File.Delete(partial);
    }

    private static string Version()
    {
        var version = typeof(BackupRun).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
        return version.Length > 64 ? version[..64] : version;
    }
}

/// <summary>The request of a backup job.</summary>
/// <param name="Options">Backup options.</param>
/// <param name="FileName">Archive file name.</param>
public sealed record BackupRequest(BackupOptionsDto Options, string FileName);
