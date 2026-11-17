using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Generations;
using StemStudio.Domain.LibraryCare;
using StemStudio.Domain.Voices;
using StemStudio.Persistence;

namespace StemStudio.Api.LibraryCare.Backup;

/// <summary>
/// Removes excluded categories from the staged database copy with the domain's own rules (research R-04, data-model 4.3),
/// so a restored library never points at missing files; voice profiles without a complete consent record are scrubbed the
/// same way, with a warning (FR-009). Ends with <c>PRAGMA integrity_check</c>.
/// </summary>
/// <param name="categorize">Archive category of a library item (tests pass a recordings fixture before feature 015).</param>
public sealed class BackupScrubber(Func<Asset, BackupCategory> categorize)
{
    /// <summary>Creates the scrubber with the production categories.</summary>
    public BackupScrubber()
        : this(AssetCategories.Of)
    {
    }

    /// <summary>Scrubs the staged database.</summary>
    /// <param name="stagedDatabase">Path of the staged copy.</param>
    /// <param name="excluded">Excluded categories.</param>
    /// <param name="snapshotTime">Time of the snapshot (UTC), used for removed detail stems.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Warnings (profiles left out for incomplete consent).</returns>
    public async Task<IReadOnlyList<ArchiveWarningDto>> ScrubAsync(
        string stagedDatabase, IReadOnlyCollection<BackupCategory> excluded, DateTime snapshotTime, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(excluded);
        IReadOnlyList<ArchiveWarningDto> warnings;
        await using (var context = StagedContext(stagedDatabase))
        {
            await ScrubTracksAsync(context, excluded, snapshotTime, cancellationToken);
            await ScrubAssetsAsync(context, excluded, cancellationToken);
            await ScrubGenerationsAsync(context, excluded, cancellationToken);
            warnings = await ScrubVoiceProfilesAsync(context, excluded.Contains(BackupCategory.VoiceProfiles), snapshotTime, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        await CheckIntegrityAsync(stagedDatabase, cancellationToken);
        return warnings;
    }

    /// <summary>A context on a staging database file (no pooling).</summary>
    /// <param name="path">The file.</param>
    /// <returns>The context.</returns>
    public static StemStudioDbContext StagedContext(string path) =>
        new(new DbContextOptionsBuilder<StemStudioDbContext>().UseSqlite(DatabaseSnapshot.StagingConnectionString(path)).Options);

    private static async Task ScrubTracksAsync(StemStudioDbContext context, IReadOnlyCollection<BackupCategory> excluded, DateTime snapshotTime, CancellationToken cancellationToken)
    {
        if (!excluded.Contains(BackupCategory.DetailStems))
        {
            return;
        }

        foreach (var track in (await context.Tracks.ToListAsync(cancellationToken)).Where(track => track.HasDetailFiles))
        {
            track.RemoveDetailFiles(snapshotTime);
        }
    }

    private async Task ScrubAssetsAsync(StemStudioDbContext context, IReadOnlyCollection<BackupCategory> excluded, CancellationToken cancellationToken)
    {
        var assets = await context.Assets.ToListAsync(cancellationToken);
        context.Assets.RemoveRange(assets.Where(asset => excluded.Contains(categorize(asset))));
    }

    private static async Task ScrubGenerationsAsync(StemStudioDbContext context, IReadOnlyCollection<BackupCategory> excluded, CancellationToken cancellationToken)
    {
        if (excluded.Contains(BackupCategory.Generations))
        {
            context.Generations.RemoveRange(await context.Generations.ToListAsync(cancellationToken));
        }
    }

    private static async Task<IReadOnlyList<ArchiveWarningDto>> ScrubVoiceProfilesAsync(
        StemStudioDbContext context, bool excludeAll, DateTime snapshotTime, CancellationToken cancellationToken)
    {
        var profiles = await context.VoiceProfiles.ToListAsync(cancellationToken);
        var removed = profiles.Where(profile => excludeAll || !ConsentProofRules.IsComplete(profile.Consent)).ToList();
        var conversions = (await context.Generations.ToListAsync(cancellationToken)).Where(generation => generation.VoiceProfileId is not null).ToList();
        foreach (var profile in removed)
        {
            conversions.ForEach(conversion => conversion.DetachVoiceProfile(profile.Id, Generation.ProfileNotInBackupSuffix, snapshotTime));
        }

        context.VoiceProfiles.RemoveRange(removed);
        return excludeAll ? [] : [.. removed.Select(Warning)];
    }

    private static ArchiveWarningDto Warning(VoiceProfile profile) => new()
    {
        Code = ArchiveWarningCodeDto.VoiceProfileWithoutConsent,
        Message = $"Voice profile \"{profile.DisplayName}\" has no complete consent record and is not in the backup.",
    };

    private static async Task CheckIntegrityAsync(string stagedDatabase, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(DatabaseSnapshot.StagingConnectionString(stagedDatabase));
        await connection.OpenAsync(cancellationToken);
        if (!await DatabaseSnapshot.IsIntactAsync(connection, cancellationToken))
        {
            throw new Jobs.LibraryJobException(LibraryJobErrorCodeDto.IoError, "The database copy is damaged; try again.");
        }
    }
}
