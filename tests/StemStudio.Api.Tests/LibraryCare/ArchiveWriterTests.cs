using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StemStudio.Api.LibraryCare.Archive;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Domain.LibraryCare;
using StemStudio.TestDoubles.JsonSchema;

namespace StemStudio.Api.Tests.LibraryCare;

/// <summary>013 T033: the archive writer and the manifest (backup-archive 1.0.0, FR-001, FR-005, FR-008).</summary>
public sealed class ArchiveWriterTests : IDisposable
{
    private const string Track = "0b7e4f1c-6a2d-4c8e-9f10-2a3b4c5d6e7f";
    private static readonly DateTime Created = new(2026, 11, 16, 14, 22, 33, DateTimeKind.Utc);
    private static readonly SchemaSubsetValidator Schema =
        SchemaSubsetValidator.Load(Path.Combine(AppContext.BaseDirectory, "Contract", "013", "backup-manifest.schema.json"));

    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("stemstudio-archive-");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string Partial => Path.Combine(_root.FullName, "stemstudio-backup-20261116-142233.zip.partial");

    [Fact]
    public async Task Archive_HasTheDatabaseFirstTheFilesInOrderTheReadmeAndTheManifestLast()
    {
        var (entries, manifest) = await WriteSampleAsync();

        using var zip = ZipFile.OpenRead(Partial);
        zip.Entries.Select(entry => entry.FullName).ShouldBe(
            ["db/stemstudio.db", $"files/tracks/{Track}/stems/vocals.flac", $"files/assets/{Track}/score.musicxml", "README.txt", "manifest.json"]);
        entries.Select(entry => entry.Path).ShouldBe(zip.Entries.Take(3).Select(entry => entry.FullName));
        manifest.Totals.Entries.ShouldBe(3);
        manifest.Totals.Bytes.ShouldBe(entries.Sum(entry => entry.SizeBytes));
    }

    [Fact]
    public async Task Audio_IsStoredAndTextIsDeflated()
    {
        await WriteSampleAsync();

        using var zip = ZipFile.OpenRead(Partial);
        var stem = zip.GetEntry($"files/tracks/{Track}/stems/vocals.flac")!;
        var score = zip.GetEntry($"files/assets/{Track}/score.musicxml")!;
        stem.CompressedLength.ShouldBe(stem.Length);
        score.CompressedLength.ShouldBeLessThan(score.Length);
        ArchiveWriter.IsStored("a.FLAC").ShouldBeTrue();
        ArchiveWriter.IsStored("cover.jpg").ShouldBeTrue();
        ArchiveWriter.IsStored("x.pdf").ShouldBeTrue();
        ArchiveWriter.IsStored("x.mid").ShouldBeFalse();
    }

    [Fact]
    public async Task EveryEntry_HasItsSha256AndSize()
    {
        var (entries, _) = await WriteSampleAsync();

        using var zip = ZipFile.OpenRead(Partial);
        foreach (var entry in entries)
        {
            await using var stream = zip.GetEntry(entry.Path)!.Open();
            var bytes = new MemoryStream();
            await stream.CopyToAsync(bytes, Ct);
            (Convert.ToHexStringLower(SHA256.HashData(bytes.ToArray())), bytes.Length).ShouldBe((entry.Sha256, entry.SizeBytes));
        }
    }

    [Fact]
    public async Task Manifest_IsValidAgainstTheSchemaAndReadable()
    {
        await WriteSampleAsync();

        using var zip = ZipFile.OpenRead(Partial);
        await using var stream = zip.GetEntry("manifest.json")!.Open();
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: Ct);
        Schema.Validate(document.RootElement).ShouldBeEmpty();
        var root = document.RootElement;
        (root.GetProperty("format").GetString(), root.GetProperty("formatVersion").GetString()).ShouldBe(("stemstudio-backup", "1.0.0"));
        root.GetProperty("contents").GetProperty("included").EnumerateArray().Select(e => e.GetString()).ShouldBe(["database", "stems", "scores"]);
        root.GetProperty("contents").GetProperty("excluded").EnumerateArray().Select(e => (e.GetProperty("category").GetString(), e.GetProperty("reason").GetString()))
            .ShouldContain(("detailStems", "option"));
        root.GetProperty("contents").GetProperty("excluded").EnumerateArray().Select(e => e.GetProperty("category").GetString()).ShouldContain("recordings");
        root.GetProperty("entries")[1].GetProperty("entity").GetProperty("type").GetString().ShouldBe("track");
    }

    [Fact]
    public async Task Readme_ExplainsTheArchiveAndTheVoiceData()
    {
        await WriteSampleAsync();

        using var zip = ZipFile.OpenRead(Partial);
        using var reader = new StreamReader(zip.GetEntry("README.txt")!.Open());
        var text = await reader.ReadToEndAsync(Ct);
        text.ShouldContain("StemStudio");
        text.ShouldContain("not encrypted");
    }

    [Fact]
    public async Task Check_ComparesTheCentralDirectoryWithTheManifest()
    {
        var (_, manifest) = await WriteSampleAsync();

        ArchiveWriter.MatchesManifest(Partial, manifest).ShouldBeTrue();
        ArchiveWriter.MatchesManifest(Partial, manifest with { Entries = manifest.Entries.Take(2).ToList() }).ShouldBeFalse();
        ArchiveWriter.MatchesManifest(Partial, manifest with { Entries = [.. manifest.Entries.Take(2), manifest.Entries[2] with { SizeBytes = 1 }] }).ShouldBeFalse();
        ArchiveWriter.MatchesManifest(Path.Combine(_root.FullName, "missing.zip"), manifest).ShouldBeFalse();
    }

    [Fact]
    public async Task Commit_RenamesThePartialFile()
    {
        await WriteSampleAsync();
        var final = Partial[..^".partial".Length];

        ArchiveWriter.Commit(Partial, final);

        (File.Exists(final), File.Exists(Partial)).ShouldBe((true, false));
    }

    [Fact]
    public async Task MissingSource_IsReportedAndNotWritten()
    {
        await using (var writer = ArchiveWriter.Create(Partial, 4096))
        {
            var missing = await writer.AddFileAsync(new ArchiveSource($"files/tracks/{Track}/stems/bass.flac", BackupCategory.Stems, Path.Combine(_root.FullName, "nope.flac"), null), null, Ct);
            missing.ShouldBeNull();
        }

        using var zip = ZipFile.OpenRead(Partial);
        zip.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task MoreThan65535Entries_AreWrittenAsZip64AndReadBack()
    {
        await using (var writer = ArchiveWriter.Create(Partial, 4096))
        {
            for (var i = 0; i < 70_000; i++)
            {
                await writer.AddTextAsync($"files/x/{i}", "x", Ct);
            }
        }

        using var zip = ZipFile.OpenRead(Partial);
        zip.Entries.Count.ShouldBe(70_000);
        zip.Entries[^1].FullName.ShouldBe("files/x/69999");
    }

    public void Dispose() => _root.Delete(recursive: true);

    private async Task<(IReadOnlyList<ManifestEntry> Entries, BackupManifest Manifest)> WriteSampleAsync()
    {
        var db = await SourceAsync("stemstudio.db", new string('d', 300));
        var stem = await SourceAsync("vocals.flac", new string('v', 500));
        var score = await SourceAsync("score.musicxml", new string('s', 4000));
        var entries = new List<ManifestEntry>();
        await using (var writer = ArchiveWriter.Create(Partial, 4096))
        {
            entries.Add((await writer.AddFileAsync(new ArchiveSource("db/stemstudio.db", BackupCategory.Database, db, null), null, Ct))!);
            entries.Add((await writer.AddFileAsync(new ArchiveSource($"files/tracks/{Track}/stems/vocals.flac", BackupCategory.Stems, stem, new ManifestEntity("track", Track)), null, Ct))!);
            entries.Add((await writer.AddFileAsync(new ArchiveSource($"files/assets/{Track}/score.musicxml", BackupCategory.Scores, score, new ManifestEntity("asset", Track)), null, Ct))!);
            var manifest = ManifestBuilder.Build(Input(entries));
            await writer.AddTextAsync(ArchiveWriter.ReadmeEntry, ArchiveWriter.ReadmeText, Ct);
            await writer.AddManifestAsync(manifest, Ct);
            return (entries, manifest);
        }
    }

    private static ManifestInput Input(IReadOnlyList<ManifestEntry> entries) => new()
    {
        BackupId = Guid.Parse("5b0f6a3e-2c1d-4e8f-9a7b-1c2d3e4f5a6b"),
        CreatedAt = Created,
        AppVersion = "1.12.0+test",
        Migrations = ["20261114215330_Initial", "20261115200748_AddLyricsAndVoices"],
        Selected = [BackupCategory.Scores, BackupCategory.Exports, BackupCategory.Recordings],
        Counts = new ManifestCounts { Tracks = 1, Assets = 1 },
        Entries = entries,
        VoiceProfiles = [],
        Warnings = [new ArchiveWarningDto { Code = ArchiveWarningCodeDto.MissingFile, Message = "1 file was missing." }],
    };

    private async Task<string> SourceAsync(string name, string content)
    {
        var path = Path.Combine(_root.FullName, name);
        await File.WriteAllTextAsync(path, content, Encoding.UTF8, Ct);
        return path;
    }
}
