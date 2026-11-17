using System.Globalization;
using Microsoft.EntityFrameworkCore;
using StemStudio.Api.LibraryCare.Archive;
using StemStudio.Api.Storage;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Generations;
using StemStudio.Domain.LibraryCare;
using StemStudio.Domain.Tracks;
using StemStudio.Domain.Tracks.Detail;
using StemStudio.Domain.Voices;
using StemStudio.Persistence;

namespace StemStudio.Api.LibraryCare.Backup;

/// <summary>A file of the backup with a readable label for the progress (never a path).</summary>
/// <param name="Source">The archive source.</param>
/// <param name="Label">e.g. "Song title - stems".</param>
public sealed record SelectedFile(ArchiveSource Source, string Label);

/// <summary>What a backup writes: files in archive order, consent proofs and row counts.</summary>
/// <param name="Files">Files in category order, sorted by path inside a category.</param>
/// <param name="VoiceProfiles">Consent proofs.</param>
/// <param name="Counts">Rows of the archived database.</param>
public sealed record BackupPlan(IReadOnlyList<SelectedFile> Files, IReadOnlyList<ManifestVoiceProfile> VoiceProfiles, ManifestCounts Counts);

/// <summary>
/// Selects the files of a backup from the scrubbed snapshot (data-model 4.4): stems and artwork of completed tracks,
/// present detail stems, ready library items, completed generations' takes, voice samples and uploads of tracks that did
/// not complete. Files are selected from the snapshot, never from the live folder.
/// </summary>
/// <param name="data">The data directory (sources).</param>
public sealed class BackupSelection(DataDirectory data)
{
    private static readonly string[] ArtworkFiles = ["auto/cover.jpg", "auto/thumb.jpg", "user/cover.jpg", "user/thumb.jpg"];

    /// <summary>Reads the plan from the staged database.</summary>
    /// <param name="context">A context on the staged database.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The plan.</returns>
    public async Task<BackupPlan> ReadAsync(StemStudioDbContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var tracks = await context.Tracks.AsNoTracking().ToListAsync(cancellationToken);
        var assets = await context.Assets.AsNoTracking().ToListAsync(cancellationToken);
        var generations = await context.Generations.AsNoTracking().ToListAsync(cancellationToken);
        var profiles = await context.VoiceProfiles.AsNoTracking().ToListAsync(cancellationToken);
        var files = tracks.SelectMany(TrackFiles)
            .Concat(assets.Where(asset => asset.Status == AssetStatus.Ready).SelectMany(AssetFiles))
            .Concat(generations.Where(generation => generation.Status == GenerationState.Completed).SelectMany(GenerationFiles))
            .Concat(profiles.Select(VoiceFile))
            .Where(file => LibraryLayout.IsAllowed(LibraryLayout.ToDataPath(file.Source.EntryName)))
            .OrderBy(file => file.Source.Category).ThenBy(file => file.Source.EntryName, StringComparer.Ordinal)
            .ToList();
        return new BackupPlan(files, [.. profiles.Select(Proof)], await CountAsync(context, cancellationToken));
    }

    /// <summary>Rows per entity type (styles: user styles only).</summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The counts.</returns>
    public static async Task<ManifestCounts> CountAsync(StemStudioDbContext context, CancellationToken cancellationToken) => new()
    {
        Tracks = await context.Tracks.CountAsync(cancellationToken),
        Mixes = await context.Mixes.CountAsync(cancellationToken),
        Assets = await context.Assets.CountAsync(cancellationToken),
        Generations = await context.Generations.CountAsync(cancellationToken),
        Styles = (await context.Styles.AsNoTracking().ToListAsync(cancellationToken)).Count(style => !style.IsPreset),
        LyricsSheets = await context.LyricsSheets.CountAsync(cancellationToken),
        VoiceProfiles = await context.VoiceProfiles.CountAsync(cancellationToken),
        Artists = await context.Artists.CountAsync(cancellationToken),
        AgentConversations = await context.AgentConversations.CountAsync(cancellationToken),
    };

    private static ManifestVoiceProfile Proof(VoiceProfile profile) => new(
        Id(profile.Id),
        profile.DisplayName,
        new ManifestConsent(profile.Consent.StatementVersion, profile.Consent.StatementSha256, profile.Consent.PersonName.Trim(), profile.Consent.ConsentedAt),
        LibraryLayout.ToEntryName(profile.SamplePath),
        profile.SampleFileSha256);

    private static string Id(Guid id) => id.ToString("D", CultureInfo.InvariantCulture);

    private IEnumerable<SelectedFile> TrackFiles(Track track)
    {
        var folder = $"tracks/{Id(track.Id)}";
        if (track.Status != TrackStatus.Completed)
        {
            return track.UploadFileName is { } upload
                ? [File($"uploads/{upload}", BackupCategory.PendingUploads, new ManifestEntity("upload", Id(track.Id)), $"{track.Title} - upload")]
                : [];
        }

        var entity = new ManifestEntity("track", Id(track.Id));
        return track.Stems.Select(stem => File($"{folder}/stems/{stem.Name}.flac", BackupCategory.Stems, entity, $"{track.Title} - stems"))
            .Concat(ArtworkFiles.Where(name => System.IO.File.Exists(Source($"{folder}/artwork/{name}")))
                .Select(name => File($"{folder}/artwork/{name}", BackupCategory.Artwork, entity, $"{track.Title} - artwork")))
            .Concat(DetailFiles(track, folder, entity));
    }

    private IEnumerable<SelectedFile> DetailFiles(Track track, string folder, ManifestEntity entity) => track.HasDetailFiles
        ? track.DetailPhase!.Parents.Where(parent => parent.Outcome == ParentOutcomeKind.Split)
            .SelectMany(parent => parent.Stems.Select(stem => File($"{folder}/detail/{parent.Parent}/{stem.Name}.flac", BackupCategory.DetailStems, entity, $"{track.Title} - detail stems")))
        : [];

    private IEnumerable<SelectedFile> AssetFiles(Asset asset)
    {
        var category = AssetCategories.Of(asset);
        var entity = new ManifestEntity("asset", Id(asset.Id));
        return asset.Files.Select(file => File($"assets/{Id(asset.Id)}/{file.FileName}", category, entity, $"{asset.Name} - library item"));
    }

    private IEnumerable<SelectedFile> GenerationFiles(Generation generation)
    {
        var entity = new ManifestEntity("generation", Id(generation.Id));
        return generation.Takes.Select(take => File(take.FilePath, BackupCategory.Generations, entity, "Generation - takes"));
    }

    private SelectedFile VoiceFile(VoiceProfile profile) =>
        File(profile.SamplePath, BackupCategory.VoiceProfiles, new ManifestEntity("voiceProfile", Id(profile.Id)), $"{profile.DisplayName} - voice sample");

    private SelectedFile File(string relativePath, BackupCategory category, ManifestEntity entity, string label) =>
        new(new ArchiveSource(LibraryLayout.ToEntryName(relativePath), category, Source(relativePath), entity), label);

    private string Source(string relativePath) => Path.Combine(data.Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
}
