using StemStudio.Api.LibraryCare.Backup;
using StemStudio.Api.LibraryCare.Jobs;
using StemStudio.Api.LibraryCare.Restore;
using StemStudio.Api.Storage;
using StemStudio.Contracts.LibraryCare;
using static StemStudio.Api.Tests.LibraryCare.HostileArchives;

namespace StemStudio.Api.Tests.LibraryCare;

/// <summary>013 T042: the hostile corpus is refused in the order of data-model 3.4; extraction stays inside staging (SC-004).</summary>
public sealed class ArchiveValidatorTests : IDisposable
{
    private const string Name = "x.zip";

    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("stemstudio-validate-");
    private readonly FakeLibraryFiles _files = new();
    private readonly LibraryCarePaths _paths;

    public ArchiveValidatorTests()
    {
        _paths = new LibraryCarePaths(new DataDirectory(Path.Combine(_root.FullName, "data")), Path.Combine(_root.FullName, "backups"));
        Directory.CreateDirectory(_paths.BackupsDirectory);
        Directory.CreateDirectory(_paths.Data.Root);
        File.WriteAllText(Path.Combine(_paths.Data.Root, "keep.txt"), "live");
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<string, LibraryJobErrorCodeDto> Corpus() => new()
    {
        { "H1", LibraryJobErrorCodeDto.ArchiveUnsafe },
        { "H2", LibraryJobErrorCodeDto.ArchiveUnsafe },
        { "H3", LibraryJobErrorCodeDto.ArchiveUnsafe },
        { "H4", LibraryJobErrorCodeDto.ArchiveUnsafe },
        { "H5", LibraryJobErrorCodeDto.ArchiveUnsafe },
        { "H7", LibraryJobErrorCodeDto.ArchiveUnsafe },
        { "H8", LibraryJobErrorCodeDto.ArchiveCorrupt },
        { "H9", LibraryJobErrorCodeDto.ArchiveCorrupt },
        { "H10", LibraryJobErrorCodeDto.ArchiveCorrupt },
        { "H11", LibraryJobErrorCodeDto.ArchiveUnsafe },
        { "H12", LibraryJobErrorCodeDto.ArchiveInvalid },
        { "H12b", LibraryJobErrorCodeDto.ArchiveInvalid },
        { "notzip", LibraryJobErrorCodeDto.ArchiveInvalid },
        { "nomanifest", LibraryJobErrorCodeDto.ArchiveInvalid },
        { "format2", LibraryJobErrorCodeDto.ArchiveIncompatible },
        { "newer", LibraryJobErrorCodeDto.ArchiveIncompatible },
        { "H16", LibraryJobErrorCodeDto.InsufficientDiskSpace },
        { "duplicatePath", LibraryJobErrorCodeDto.ArchiveInvalid },
        { "nullApp", LibraryJobErrorCodeDto.ArchiveInvalid },
        { "nullEntry", LibraryJobErrorCodeDto.ArchiveInvalid },
        { "noFormatVersion", LibraryJobErrorCodeDto.ArchiveInvalid },
        { "format2Broken", LibraryJobErrorCodeDto.ArchiveIncompatible },
    };

    [Theory]
    [MemberData(nameof(Corpus))]
    public async Task HostileArchive_IsRefusedWithItsCodeAndNothingOutsideStagingChanges(string vector, LibraryJobErrorCodeDto code)
    {
        Build(vector);
        var before = Snapshot();

        var error = await Should.ThrowAsync<LibraryJobException>(() => ValidateAndExtractAsync());

        error.Code.ShouldBe(code, vector);
        error.Message.ShouldNotContain(_root.FullName);
        Snapshot().ShouldBe(before);
    }

    [Fact]
    public async Task ValidArchive_IsExtractedIntoStagingWithVerifiedHashes()
    {
        Write(Archive, [StemEntry()]);

        await ValidateAndExtractAsync();

        var staged = Path.Combine(Staging, "files", "tracks", Track, "stems", "vocals.flac");
        (await File.ReadAllBytesAsync(staged, Ct)).ShouldBe(Stem);
        File.Exists(Path.Combine(Staging, "stemstudio.db")).ShouldBeTrue();
    }

    [Fact]
    public void ReparsePointArchive_IsRefused()
    {
        Write(Archive, [StemEntry()]);
        ArchiveValidator.IsLink(new FileInfo(Archive)).ShouldBeFalse();
        Should.Throw<LibraryJobException>(() => Validator().Validate("../x.zip")).Code.ShouldBe(LibraryJobErrorCodeDto.ArchiveInvalid);
    }

    [Fact]
    public void Validate_MissingArchiveIsInvalid() =>
        Should.Throw<LibraryJobException>(() => Validator().Validate("missing.zip")).Code.ShouldBe(LibraryJobErrorCodeDto.ArchiveInvalid);

    [Fact]
    public void Staging_DestinationOutsideTheRootIsRefused()
    {
        Should.Throw<LibraryJobException>(() => StagingExtractor.Destination(Staging, "files/../../x")).Code.ShouldBe(LibraryJobErrorCodeDto.ArchiveUnsafe);
        StagingExtractor.Destination(Staging, "db/stemstudio.db").ShouldBe(Path.Combine(Staging, "stemstudio.db"));
    }

    public void Dispose() => _root.Delete(recursive: true);

    private string Archive => _paths.Archive(Name);

    private string Staging => _paths.Staging(Guid.Empty);

    private ArchiveValidator Validator() => new(_paths, _files, new KnownMigrations(["20261114215330_Initial", "20261115000000_Next"]));

    private async Task ValidateAndExtractAsync()
    {
        using var archive = Validator().Validate(Name);
        await new StagingExtractor(new LibraryCareOptions(), TimeProvider.System).ExtractAsync(archive, Staging, null, Ct);
    }

    private SortedDictionary<string, string> Snapshot() => new(
        Directory.EnumerateFiles(_root.FullName, "*", SearchOption.AllDirectories)
            .Where(path => !path.StartsWith(_paths.StagingDirectory, StringComparison.Ordinal))
            .ToDictionary(path => path, File.ReadAllText),
        StringComparer.Ordinal);

    private void Build(string vector)
    {
        var stem = StemEntry();
        var builders = new Dictionary<string, Action>
        {
            ["H1"] = () => Write(Archive, [stem, new("files/../../etc/passwd", [1])]),
            ["H2"] = () => Write(Archive, [stem, new("/data/stemstudio.db", [1])]),
            ["H3"] = () => Write(Archive, [new($"files\\tracks\\{Track}\\stems\\vocals.flac", Stem)]),
            ["H4"] = () => Write(Archive, [new("files/tracks/\u0001.flac", [1])]),
            ["H5"] = () => Write(Archive, [stem with { UnixMode = 0xA1FF }]),
            ["H7"] = () => Write(Archive, [stem, new($"files/tracks/{Track}/stems/drums.flac", Stem, Listed: false)]),
            ["H8"] = () => Write(Archive, [stem], AddPhantom),
            ["H9"] = () => Write(Archive, [stem with { DeclaredSize = 10 }]),
            ["H10"] = () => Write(Archive, [stem with { DeclaredHash = new string('0', 64) }]),
            ["H11"] = () => Write(Archive, [new($"files/tracks/{Track}/run.sh", [1])]),
            ["H12"] = () => Write(Archive, [stem], manifest => manifest.Remove("entries")),
            ["H12b"] = () => Write(Archive, [stem], manifest => manifest["format"] = "other"),
            ["notzip"] = () => File.WriteAllText(Archive, "not a zip"),
            ["nomanifest"] = WriteWithoutManifest,
            ["format2"] = () => Write(Archive, [stem], manifest => manifest["formatVersion"] = "2.0.0"),
            ["newer"] = () => Write(Archive, [stem], manifest => manifest["schema"] = new { provider = "sqlite", lastMigration = "20991231000000_Future", migrations = new[] { "20261114215330_Initial", "20991231000000_Future" } }),
            ["duplicatePath"] = () => Write(Archive, [stem], manifest => AddEntry(manifest, ((List<Dictionary<string, object?>>)manifest["entries"]!)[^1])),
            ["nullApp"] = () => Write(Archive, [stem], manifest => manifest["app"] = null),
            ["nullEntry"] = () => Write(Archive, [stem], manifest => ((List<Dictionary<string, object?>>)manifest["entries"]!).Add(null!)),
            ["noFormatVersion"] = () => Write(Archive, [stem], manifest => manifest["formatVersion"] = null),
            ["format2Broken"] = () => Write(Archive, [stem], manifest =>
            {
                manifest["formatVersion"] = "2.0.0";
                manifest["entries"] = "a format 2 shape";
            }),
            ["H16"] = () =>
            {
                _files.Free = 10;
                Write(Archive, [stem]);
            },
        };
        builders[vector]();
    }

    private void WriteWithoutManifest()
    {
        using var zip = System.IO.Compression.ZipFile.Open(Archive, System.IO.Compression.ZipArchiveMode.Create);
        zip.CreateEntry("photo.jpg");
    }

    private static void AddPhantom(Dictionary<string, object?> manifest) =>
        AddEntry(manifest, new Dictionary<string, object?> { ["path"] = $"files/tracks/{Track}/stems/bass.flac", ["category"] = "stems", ["sizeBytes"] = 1, ["sha256"] = new string('a', 64) });

    private static void AddEntry(Dictionary<string, object?> manifest, Dictionary<string, object?> entry)
    {
        var entries = (List<Dictionary<string, object?>>)manifest["entries"]!;
        entries.Add(entry);
        manifest["totals"] = new { entries = entries.Count, bytes = entries.Sum(entry => Convert.ToInt64(entry["sizeBytes"], System.Globalization.CultureInfo.InvariantCulture)) };
    }

    private sealed class KnownMigrations(IReadOnlyList<string> all) : IKnownMigrations
    {
        public IReadOnlyList<string> All => all;
    }
}
