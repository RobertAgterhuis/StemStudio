using System.Globalization;
using StemStudio.Domain.LibraryCare;

namespace StemStudio.Domain.Tests.LibraryCare;

/// <summary>013 T013: classification of library files (data-model 6.2), regenerability and clean-up rules (FR-022, SC-007, SC-010).</summary>
public sealed class StorageClassifierTests
{
    private static readonly Guid Track = Guid.Parse("0b7e4f1c-6a2d-4c8e-9f10-2a3b4c5d6e7f");
    private static readonly Guid Export = Guid.Parse("1b7e4f1c-6a2d-4c8e-9f10-2a3b4c5d6e7f");
    private static readonly Guid Score = Guid.Parse("2b7e4f1c-6a2d-4c8e-9f10-2a3b4c5d6e7f");
    private static readonly Guid Recording = Guid.Parse("3b7e4f1c-6a2d-4c8e-9f10-2a3b4c5d6e7f");
    private static readonly Guid GeneratedAsset = Guid.Parse("4b7e4f1c-6a2d-4c8e-9f10-2a3b4c5d6e7f");
    private static readonly Guid Generation = Guid.Parse("5b7e4f1c-6a2d-4c8e-9f10-2a3b4c5d6e7f");
    private static readonly Guid Voice = Guid.Parse("6b7e4f1c-6a2d-4c8e-9f10-2a3b4c5d6e7f");
    private static readonly Guid Unknown = Guid.Parse("7b7e4f1c-6a2d-4c8e-9f10-2a3b4c5d6e7f");
    private static readonly string Upload = new string('b', 64) + ".wav";

    private static readonly LibraryIndex Index = new()
    {
        Tracks = new HashSet<Guid> { Track },
        Assets = new Dictionary<Guid, BackupCategory>
        {
            [Export] = BackupCategory.Exports,
            [Score] = BackupCategory.Scores,
            [Recording] = BackupCategory.Recordings,
            [GeneratedAsset] = BackupCategory.Generations,
        },
        Generations = new HashSet<Guid> { Generation },
        VoiceProfiles = new HashSet<Guid> { Voice },
        PendingUploads = new HashSet<string>(StringComparer.Ordinal) { Upload },
    };

    [Theory]
    [InlineData("stemstudio.db", StorageCategory.Database)]
    [InlineData("stemstudio.db-wal", StorageCategory.Database)]
    [InlineData("stemstudio.db-shm", StorageCategory.Database)]
    [InlineData("tracks/{T}/stems/vocals.flac", StorageCategory.Stems)]
    [InlineData("tracks/{T}/artwork/auto/cover.jpg", StorageCategory.Artwork)]
    [InlineData("tracks/{T}/artwork/user/thumb.jpg", StorageCategory.Artwork)]
    [InlineData("tracks/{T}/detail/drums/kick.flac", StorageCategory.DetailStems)]
    [InlineData("assets/{E}/audio.flac", StorageCategory.Exports)]
    [InlineData("assets/{S}/score.pdf", StorageCategory.Scores)]
    [InlineData("assets/{R}/audio.flac", StorageCategory.Recordings)]
    [InlineData("assets/{A}/audio.flac", StorageCategory.Generations)]
    [InlineData("generations/{G}/take-1.flac", StorageCategory.Generations)]
    [InlineData("voices/{V}/sample.flac", StorageCategory.VoiceProfiles)]
    [InlineData("uploads/{U}", StorageCategory.PendingUploads)]
    [InlineData("library-care/staging/{T}/stemstudio.db", StorageCategory.RestoreStaging)]
    [InlineData("library-care/previous/{T}/tracks/x", StorageCategory.PreviousLibrary)]
    [InlineData("tracks/{T}/stems/vocals.flac.0123.tmp", StorageCategory.Leftovers)]
    [InlineData("uploads/abc.tmp", StorageCategory.Leftovers)]
    [InlineData("tracks/{X}/stems/vocals.flac", StorageCategory.Leftovers)]
    [InlineData("tracks/{X}/detail/drums/kick.flac", StorageCategory.Leftovers)]
    [InlineData("assets/{X}/audio.flac", StorageCategory.Leftovers)]
    [InlineData("generations/{X}/take-1.flac", StorageCategory.Leftovers)]
    [InlineData("voices/{X}/sample.flac", StorageCategory.Leftovers)]
    [InlineData("uploads/cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc.mp3", StorageCategory.Leftovers)]
    [InlineData("tracks/{T}/notes.txt", StorageCategory.Leftovers)]
    [InlineData("random.bin", StorageCategory.Leftovers)]
    public void Classify_UsesTheLayoutAndTheIndex(string template, StorageCategory expected)
    {
        var classification = StorageClassifier.Classify(Expand(template), Index).ShouldNotBeNull();

        classification.Category.ShouldBe(expected);
    }

    [Theory]
    [InlineData("library-care/jobs/x.json")]
    [InlineData("library-care/journal/x.json")]
    [InlineData("library-care/cleanup/x.json")]
    public void Classify_LibraryCareBookkeeping_IsNotCounted(string path) => StorageClassifier.Classify(path, Index).ShouldBeNull();

    [Fact]
    public void Classify_KeepsTheEntityOfAMatchedFile()
    {
        StorageClassifier.Classify(Expand("tracks/{T}/detail/drums/kick.flac"), Index)!.EntityId.ShouldBe(Track);
        StorageClassifier.Classify(Expand("uploads/{U}"), Index)!.EntityId.ShouldBeNull();
    }

    [Theory]
    [InlineData("Export", false, BackupCategory.Exports)]
    [InlineData("Transcription", false, BackupCategory.Exports)]
    [InlineData("Generation", false, BackupCategory.Generations)]
    [InlineData("recording", false, BackupCategory.Recordings)]
    [InlineData("Recording", false, BackupCategory.Recordings)]
    [InlineData("Transcription", true, BackupCategory.Scores)]
    [InlineData("Generation", true, BackupCategory.Scores)]
    public void AssetCategory_FollowsKindAndProvenanceOrigin(string origin, bool isScore, BackupCategory expected) =>
        BackupCategories.ForAsset(isScore, origin).ShouldBe(expected);

    [Theory]
    [InlineData(StorageCategory.DetailStems, Regenerability.Regenerable)]
    [InlineData(StorageCategory.Leftovers, Regenerability.Regenerable)]
    [InlineData(StorageCategory.WorkerSeparationCache, Regenerability.Regenerable)]
    [InlineData(StorageCategory.WorkerDetailCache, Regenerability.Regenerable)]
    [InlineData(StorageCategory.WorkerJobFiles, Regenerability.Regenerable)]
    [InlineData(StorageCategory.WorkerScratch, Regenerability.Regenerable)]
    [InlineData(StorageCategory.Stems, Regenerability.Original)]
    [InlineData(StorageCategory.Exports, Regenerability.Original)]
    [InlineData(StorageCategory.Recordings, Regenerability.Original)]
    [InlineData(StorageCategory.VoiceProfiles, Regenerability.Original)]
    [InlineData(StorageCategory.Database, Regenerability.Original)]
    [InlineData(StorageCategory.PreviousLibrary, Regenerability.Original)]
    [InlineData(StorageCategory.RestoreStaging, Regenerability.Regenerable)]
    public void Regenerability_IsFixedPerCategory(StorageCategory category, Regenerability expected) =>
        Regenerabilities.Of(category).ShouldBe(expected);

    [Fact]
    public void BackupCategories_OptionalAndAlwaysIncluded()
    {
        BackupCategories.Optional.ShouldBe(
            [BackupCategory.DetailStems, BackupCategory.Exports, BackupCategory.Scores, BackupCategory.Generations, BackupCategory.Recordings, BackupCategory.VoiceProfiles]);
        BackupCategories.All.Except(BackupCategories.Optional).ShouldBe(
            [BackupCategory.Database, BackupCategory.Stems, BackupCategory.Artwork, BackupCategory.PendingUploads]);
        BackupCategories.ToStorage(BackupCategory.Recordings).ShouldBe(StorageCategory.Recordings);
        Regenerabilities.Of(BackupCategory.DetailStems).ShouldBe(Regenerability.Regenerable);
    }

    [Theory]
    [InlineData(CleanupKind.DetailStems, StorageCategory.DetailStems)]
    [InlineData(CleanupKind.Leftovers, StorageCategory.Leftovers)]
    public void CleanupRules_MatchOnlyTheirRegenerableCategory(CleanupKind kind, StorageCategory category)
    {
        foreach (var candidate in Enum.GetValues<StorageCategory>())
        {
            CleanupRules.Removes(kind, candidate).ShouldBe(candidate == category, candidate.ToString());
        }
    }

    [Fact]
    public void CleanupRules_WorkerCacheRemovesNoLibraryFile() =>
        Enum.GetValues<StorageCategory>().ShouldAllBe(category => !CleanupRules.Removes(CleanupKind.WorkerCache, category));

    [Fact]
    public void Property_OverGeneratedLibraries_CleanupMatchesOnlyRegenerablePaths()
    {
        var random = new Random(1301);
        for (var run = 0; run < 200; run++)
        {
            var (paths, index) = GeneratedLibrary(random, withOrphans: true);
            foreach (var path in paths)
            {
                var category = StorageClassifier.Classify(path, index)!.Category;
                var removed = Enum.GetValues<CleanupKind>().Any(kind => CleanupRules.Removes(kind, category));
                if (removed)
                {
                    Regenerabilities.Of(category).ShouldBe(Regenerability.Regenerable, path);
                }
            }
        }
    }

    [Fact]
    public void Property_OriginalsAreNeverRemoved_IncludingRecordingsAndVoiceSamples()
    {
        var random = new Random(1302);
        var (paths, index) = GeneratedLibrary(random, withOrphans: false);
        var originals = new[] { "stems", "artwork", "assets", "generations", "voices" };

        foreach (var path in paths.Where(p => originals.Any(folder => p.Contains("/" + folder + "/", StringComparison.Ordinal) || p.StartsWith(folder + "/", StringComparison.Ordinal))))
        {
            var category = StorageClassifier.Classify(path, index)!.Category;
            Enum.GetValues<CleanupKind>().ShouldAllBe(kind => !CleanupRules.Removes(kind, category), path);
        }
    }

    private static string Expand(string template) => template
        .Replace("{T}", Id(Track), StringComparison.Ordinal).Replace("{E}", Id(Export), StringComparison.Ordinal)
        .Replace("{S}", Id(Score), StringComparison.Ordinal).Replace("{R}", Id(Recording), StringComparison.Ordinal)
        .Replace("{A}", Id(GeneratedAsset), StringComparison.Ordinal).Replace("{G}", Id(Generation), StringComparison.Ordinal)
        .Replace("{V}", Id(Voice), StringComparison.Ordinal).Replace("{X}", Id(Unknown), StringComparison.Ordinal)
        .Replace("{U}", Upload, StringComparison.Ordinal);

    private static string Id(Guid id) => id.ToString("D", CultureInfo.InvariantCulture);

    private static (List<string> Paths, LibraryIndex Index) GeneratedLibrary(Random random, bool withOrphans)
    {
        var tracks = Enumerable.Range(0, random.Next(1, 5)).Select(_ => Guid.NewGuid()).ToList();
        var assets = Enumerable.Range(0, random.Next(0, 5)).ToDictionary(_ => Guid.NewGuid(), _ => BackupCategories.Optional[random.Next(1, 5)]);
        var paths = new List<string> { "stemstudio.db" };
        foreach (var track in tracks)
        {
            paths.Add($"tracks/{Id(track)}/stems/vocals.flac");
            paths.Add($"tracks/{Id(track)}/artwork/user/cover.jpg");
            paths.Add($"tracks/{Id(track)}/detail/drums/kick.flac");
        }

        paths.AddRange(assets.Keys.Select(asset => $"assets/{Id(asset)}/audio.flac"));
        var generation = Guid.NewGuid();
        var voice = Guid.NewGuid();
        paths.Add($"generations/{Id(generation)}/take-1.flac");
        paths.Add($"voices/{Id(voice)}/sample.flac");
        if (withOrphans)
        {
            paths.Add($"tracks/{Id(Guid.NewGuid())}/stems/bass.flac");
            paths.Add("uploads/x.tmp");
        }

        var index = new LibraryIndex
        {
            Tracks = tracks.ToHashSet(),
            Assets = assets,
            Generations = new HashSet<Guid> { generation },
            VoiceProfiles = new HashSet<Guid> { voice },
            PendingUploads = new HashSet<string>(StringComparer.Ordinal),
        };
        return (paths, index);
    }
}
