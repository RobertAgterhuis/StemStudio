using System.Net;
using System.Net.Http.Json;
using StemStudio.Api.LibraryCare;
using StemStudio.Api.LibraryCare.Archive;
using StemStudio.Api.LibraryCare.Jobs;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Domain.LibraryCare;

namespace StemStudio.Api.Tests.LibraryCare;

/// <summary>013 T042/T048: manifest rules, problem texts and the remaining restore refusals.</summary>
public sealed class ManifestRulesTests : ApiTestBase
{
    private static readonly string Hash = new('a', 64);

    private static BackupManifest Valid => ManifestBuilder.Build(new ManifestInput
    {
        BackupId = Guid.NewGuid(),
        CreatedAt = DateTime.UtcNow,
        AppVersion = "1",
        Migrations = ["20261114215330_Initial"],
        Selected = [],
        Counts = new ManifestCounts(),
        Entries = [new ManifestEntry("db/stemstudio.db", BackupCategory.Database, 1, Hash, null)],
        VoiceProfiles = [new ManifestVoiceProfile(Guid.NewGuid().ToString("D"), "Me", new ManifestConsent("consent.v1", Hash, "A", DateTime.UtcNow), "files/voices/x/sample.flac", Hash)],
        Warnings = [],
    });

    public static TheoryData<string> Invalid() => ["format", "hash", "encryption", "noentries", "migration", "sha", "size", "path", "profileSha", "profileId", "totals", "bytes", "backupId", "nomigrations"];

    [Theory]
    [MemberData(nameof(Invalid))]
    public void BrokenManifests_AreInvalid(string defect)
    {
        var manifest = Break(Valid, defect);

        Should.Throw<LibraryJobException>(() => ManifestRules.Check(manifest)).Code.ShouldBe(LibraryJobErrorCodeDto.ArchiveInvalid);
    }

    [Theory]
    [InlineData("2.0.0")]
    [InlineData("1.x")]
    public void OtherVersions_AreIncompatible(string version) =>
        Should.Throw<LibraryJobException>(() => ManifestRules.Check(Valid with { FormatVersion = version })).Code.ShouldBe(LibraryJobErrorCodeDto.ArchiveIncompatible);

    [Fact]
    public void ValidManifest_Passes() => ManifestRules.Check(Valid with { FormatVersion = "1.3.0" }).ShouldNotBeNull();

    [Theory]
    [InlineData(LibraryJobKindDto.Backup)]
    [InlineData(LibraryJobKindDto.Verify)]
    [InlineData(LibraryJobKindDto.Restore)]
    [InlineData(LibraryJobKindDto.UndoRestore)]
    public void JobRunning_IsTheSameProblemForEveryKind(LibraryJobKindDto kind)
    {
        var job = LibraryJobs.Queued(Guid.NewGuid(), kind, null, DateTimeOffset.UtcNow);

        LibraryCareProblems.JobRunning(job).Code.ShouldBe(LibraryCareProblemTypes.LibraryJobRunning);
        LibraryCareProblems.FolderUnavailable(null).Code.ShouldBe(LibraryCareProblemTypes.BackupsFolderUnavailable);
    }

    [Fact]
    public async Task Restore_RefusesIncompatibleArchivesForReplaceAndMerge()
    {
        var library = new LibraryFixture(Factory);
        await library.TrackAsync();
        Directory.CreateDirectory(Factory.BackupDir);
        HostileArchives.Write(Path.Combine(Factory.BackupDir, "newer.zip"), [HostileArchives.StemEntry()], manifest =>
            manifest["schema"] = new { provider = "sqlite", lastMigration = "20991231000000_Future", migrations = new[] { "20991231000000_Future" } });
        var replace = new RestoreOptionsDto { Mode = RestoreModeDto.Replace, Confirmation = "REPLACE" };

        using var merge = await Client.PostAsJsonAsync(ApiRoutes.RestoreBackup("newer.zip"), new RestoreOptionsDto { Mode = RestoreModeDto.Merge }, Ct);
        (merge.StatusCode, (await merge.JsonAsync()).GetProperty("type").GetString()).ShouldBe((HttpStatusCode.UnprocessableEntity, LibraryCareProblemTypes.BaseUri + LibraryCareProblemTypes.ArchiveIncompatible));
        using var newer = await Client.PostAsJsonAsync(ApiRoutes.RestoreBackup("newer.zip"), replace, Ct);
        (await newer.JsonAsync()).GetProperty("type").GetString().ShouldEndWith(LibraryCareProblemTypes.ArchiveIncompatible);
        using var details = await Client.GetAsync(ApiRoutes.Backup("newer.zip"), Ct);
        (await details.BodyAsync<BackupDetailsDto>()).Compatibility.Restorable.ShouldBeFalse();
    }

    private static BackupManifest Break(BackupManifest manifest, string defect) => defect switch
    {
        "format" => manifest with { Format = "x" },
        "hash" => manifest with { HashAlgorithm = "md5" },
        "encryption" => manifest with { Encryption = "aes" },
        "noentries" => manifest with { Entries = [], Totals = new ManifestTotals(0, 0) },
        "migration" => manifest with { Schema = manifest.Schema with { Migrations = ["bad"] } },
        "nomigrations" => manifest with { Schema = manifest.Schema with { Migrations = [] } },
        "sha" => manifest with { Entries = [manifest.Entries[0] with { Sha256 = "x" }] },
        "size" => manifest with { Entries = [manifest.Entries[0] with { SizeBytes = -1 }], Totals = new ManifestTotals(1, -1) },
        "path" => manifest with { Entries = [manifest.Entries[0] with { Path = new string('a', 300) }] },
        "profileSha" => manifest with { VoiceProfiles = [manifest.VoiceProfiles[0] with { SampleSha256 = "x" }] },
        "profileId" => manifest with { VoiceProfiles = [manifest.VoiceProfiles[0] with { Id = "x" }] },
        "totals" => manifest with { Totals = new ManifestTotals(5, 1) },
        "bytes" => manifest with { Totals = new ManifestTotals(1, 99) },
        _ => manifest with { BackupId = "x" },
    };
}
