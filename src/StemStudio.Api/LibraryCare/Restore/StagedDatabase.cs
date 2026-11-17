using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StemStudio.Api.LibraryCare.Archive;
using StemStudio.Api.LibraryCare.Backup;
using StemStudio.Api.LibraryCare.Jobs;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Generations;
using StemStudio.Domain.LibraryCare;
using StemStudio.Domain.Voices;
using StemStudio.Persistence;

namespace StemStudio.Api.LibraryCare.Restore;

/// <summary>What the database step found.</summary>
/// <param name="MigratedFrom">The archive's last migration when it was migrated; null otherwise.</param>
/// <param name="Skipped">Voice profiles not restored.</param>
/// <param name="Warnings">Readable warnings.</param>
public sealed record StagedResult(string? MigratedFrom, IReadOnlyList<SkippedItemDto> Skipped, IReadOnlyList<string> Warnings);

/// <summary>
/// The database step of a restore (data-model 3.4 steps 7-9, research R-08, R-09): integrity and migration history read-only,
/// migration of an older schema in staging, stored paths against the layout, consent proofs per profile, and the reconcile
/// of work that was running when the backup was made (FR-015). The archive's database is data: no SQL from it is executed.
/// </summary>
/// <param name="migrations">Known migrations.</param>
/// <param name="clock">Clock.</param>
public sealed class StagedDatabase(IKnownMigrations migrations, TimeProvider clock)
{
    /// <summary>Error text of work interrupted by the restore.</summary>
    public const string InterruptedText = "Interrupted by restore";

    /// <summary>Runs the database step on the staged library.</summary>
    /// <param name="stagingRoot">The staging folder.</param>
    /// <param name="manifest">The archive's manifest.</param>
    /// <param name="options">Restore options.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The result.</returns>
    public async Task<StagedResult> PrepareAsync(string stagingRoot, BackupManifest manifest, RestoreOptionsDto options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(options);
        var database = Path.Combine(stagingRoot, "stemstudio.db");
        await ArchivedDatabase.CheckReadOnlyAsync(database, manifest, cancellationToken);
        var migratedFrom = await MigrateAsync(database, manifest, cancellationToken);
        await using var context = ArchivedDatabase.Context(database);
        await ArchivedDatabase.CheckStoredPathsAsync(context, cancellationToken);
        var skipped = await new ConsentCheck(stagingRoot, manifest, clock).ApplyAsync(context, options.VoiceProfiles, cancellationToken);
        var warnings = await ReconcileAsync(context, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await ArchivedDatabase.CheckStoredPathsAsync(context, cancellationToken);
        return new StagedResult(migratedFrom, skipped, warnings);
    }

    private async Task<string?> MigrateAsync(string database, BackupManifest manifest, CancellationToken cancellationToken)
    {
        if (string.Equals(manifest.Schema.LastMigration, migrations.All[^1], StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            await using var context = ArchivedDatabase.Context(database);
            await context.Database.MigrateAsync(cancellationToken);
            return manifest.Schema.LastMigration;
        }
        catch (Exception exception) when (exception is SqliteException or InvalidOperationException or DbUpdateException)
        {
            throw new LibraryJobException(LibraryJobErrorCodeDto.MigrationFailed, "The older library database could not be updated; nothing was changed.", exception);
        }
    }

    private async Task<IReadOnlyList<string>> ReconcileAsync(StemStudioDbContext context, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var count = 0;
        foreach (var track in await context.Tracks.ToListAsync(cancellationToken))
        {
            count += Interrupt(!track.IsTerminal, () => track.Fail(InterruptedText, now)) + Interrupt(track.IsDetailActive, () => track.FailDetail(InterruptedText, now));
        }

        foreach (var asset in await context.Assets.ToListAsync(cancellationToken))
        {
            count += Interrupt(asset.IsActive, () => asset.Fail(InterruptedText, now));
        }

        foreach (var generation in await context.Generations.ToListAsync(cancellationToken))
        {
            count += Interrupt(generation.IsActive, () => generation.Fail(new GenerationError(GenerationErrorCode.Interrupted, InterruptedText), now));
        }

        return count == 0 ? [] : [string.Create(CultureInfo.InvariantCulture, $"{count} jobs were interrupted by the backup and are marked as failed; retry them.")];
    }

    private static int Interrupt(bool active, Action fail)
    {
        if (!active)
        {
            return 0;
        }

        fail();
        return 1;
    }
}

/// <summary>
/// Stored relative paths of the staged database must match the layout (data-model 3.4 step 8, H14): voice samples, take and
/// converted vocal paths, asset file names and pending uploads. Only the path columns are read, and a table that an older
/// schema does not have yet has no rows to check, so verify can run this on an archive it does not migrate.
/// </summary>
public static class StoredPaths
{
    /// <summary>Checks every stored path.</summary>
    /// <param name="context">The staged context.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public static async Task CheckAsync(StemStudioDbContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var tables = (await context.Database.SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type = 'table'").ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);
        var paths = (await SamplePathsAsync(context, tables, cancellationToken))
            .Concat(await TakePathsAsync(context, tables, cancellationToken))
            .Concat(await AssetPathsAsync(context, tables, cancellationToken))
            .Concat((await context.Tracks.AsNoTracking().Select(track => track.UploadFileName).ToListAsync(cancellationToken)).OfType<string>().Select(name => "uploads/" + name));
        if (paths.Any(path => !LibraryLayout.IsAllowed(path)))
        {
            throw ArchiveReader.Unsafe("The archived database refers to a file outside the library layout; nothing was changed.");
        }
    }

    private static async Task<IEnumerable<string>> SamplePathsAsync(StemStudioDbContext context, HashSet<string> tables, CancellationToken cancellationToken) =>
        tables.Contains("VoiceProfiles") ? await context.VoiceProfiles.AsNoTracking().Select(profile => profile.SamplePath).ToListAsync(cancellationToken) : [];

    private static async Task<IEnumerable<string>> TakePathsAsync(StemStudioDbContext context, HashSet<string> tables, CancellationToken cancellationToken)
    {
        if (!tables.Contains("Generations"))
        {
            return [];
        }

        var takes = await context.Generations.AsNoTracking().Select(generation => EF.Property<List<GenerationTake>>(generation, "_takes")).ToListAsync(cancellationToken);
        return takes.SelectMany(list => list ?? []).SelectMany(take => new[] { take.FilePath, take.VocalFilePath }).OfType<string>();
    }

    private static async Task<IEnumerable<string>> AssetPathsAsync(StemStudioDbContext context, HashSet<string> tables, CancellationToken cancellationToken)
    {
        if (!tables.Contains("Assets"))
        {
            return [];
        }

        var assets = await context.Assets.AsNoTracking().Select(asset => new { asset.Id, Files = EF.Property<List<AssetFile>>(asset, "_files") }).ToListAsync(cancellationToken);
        return assets.SelectMany(asset => (asset.Files ?? []).Select(file => $"assets/{asset.Id:D}/{file.FileName}"));
    }
}

/// <summary>Consent proofs of a restore (data-model 5.4): invalid or unwanted profiles are dropped and their conversions detached.</summary>
/// <param name="stagingRoot">The staging folder.</param>
/// <param name="manifest">The manifest.</param>
/// <param name="clock">Clock.</param>
public sealed class ConsentCheck(string stagingRoot, BackupManifest manifest, TimeProvider clock)
{
    /// <summary>Applies the rules.</summary>
    /// <param name="context">The staged context.</param>
    /// <param name="restoreProfiles">The <c>voiceProfiles</c> option.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The profiles not restored.</returns>
    public async Task<IReadOnlyList<SkippedItemDto>> ApplyAsync(StemStudioDbContext context, bool restoreProfiles, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var conversions = (await context.Generations.ToListAsync(cancellationToken)).Where(generation => generation.VoiceProfileId is not null).ToList();
        var skipped = new List<SkippedItemDto>();
        foreach (var profile in await context.VoiceProfiles.ToListAsync(cancellationToken))
        {
            var reason = restoreProfiles ? (IsValid(profile) ? (SkippedItemReasonDto?)null : SkippedItemReasonDto.ConsentInvalid) : SkippedItemReasonDto.Option;
            if (reason is { } why)
            {
                Drop(context, profile, conversions);
                skipped.Add(new SkippedItemDto { Category = "voiceProfiles", Id = profile.Id.ToString("D", CultureInfo.InvariantCulture), Name = profile.DisplayName, Reason = why });
            }
        }

        return skipped;
    }

    private bool IsValid(VoiceProfile profile)
    {
        var id = profile.Id.ToString("D", CultureInfo.InvariantCulture);
        var proof = manifest.VoiceProfiles.FirstOrDefault(candidate => candidate.Id == id);
        var sample = manifest.Entries.FirstOrDefault(entry => entry.Path == LibraryLayout.ToEntryName(profile.SamplePath));
        return proof is not null
            && ConsentProofRules.Check(proof.ToProof(), ConsentProof.From(profile.Consent, profile.SampleFileSha256), manifest.CreatedAt, sample?.Sha256) == ConsentProofProblem.None;
    }

    private void Drop(StemStudioDbContext context, VoiceProfile profile, List<Generation> conversions)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        conversions.ForEach(conversion => conversion.DetachVoiceProfile(profile.Id, Generation.ProfileNotRestoredSuffix, now));
        context.VoiceProfiles.Remove(profile);
        var folder = StagingExtractor.Destination(stagingRoot, LibraryLayout.FilesPrefix + $"voices/{profile.Id:D}");
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
