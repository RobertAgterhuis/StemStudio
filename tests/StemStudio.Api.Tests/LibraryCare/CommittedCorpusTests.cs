using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.LibraryCare.Jobs;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.LibraryCare;

namespace StemStudio.Api.Tests.LibraryCare;

/// <summary>
/// Security review 013 items 3-5: the committed corpus (<c>tests/fixtures/backup/archives</c>, qa T012) gives the same code
/// for verify and restore (CTO decision "verify and restore report the same code"), and the list shows each archive in the
/// state of <c>schema-manifest.json</c>.
/// </summary>
public sealed class CommittedCorpusTests : ApiTestBase
{
    private static readonly RestoreOptionsDto Replace = new() { Mode = RestoreModeDto.Replace, Confirmation = "REPLACE" };

    private LibraryJobRunner Runner => Factory.Services.GetRequiredService<LibraryJobRunner>();

    public static TheoryData<string, LibraryJobErrorCodeDto> VerifyCorpus() => new()
    {
        { "h13b-foreign-database.zip", LibraryJobErrorCodeDto.ArchiveCorrupt },
        { "h14-stored-path.zip", LibraryJobErrorCodeDto.ArchiveUnsafe },
        { "h17b-entry-count.zip", LibraryJobErrorCodeDto.ArchiveInvalid },
        { "h13a-integrity.zip", LibraryJobErrorCodeDto.ArchiveCorrupt },
        { "h06-duplicate-name.zip", LibraryJobErrorCodeDto.ArchiveUnsafe },
        { "h12a-manifest-schema.zip", LibraryJobErrorCodeDto.ArchiveInvalid },
        { "format-2.zip", LibraryJobErrorCodeDto.ArchiveIncompatible },
    };

    [Theory]
    [MemberData(nameof(VerifyCorpus))]
    public async Task Verify_ReportsTheCorpusCode(string fileName, LibraryJobErrorCodeDto code)
    {
        Copy(fileName);

        using var response = await Client.PostAsync(ApiRoutes.VerifyBackup(fileName), null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var report = (await FinishedAsync(response)).Report.ShouldBeOfType<VerifyReportDto>();
        (report.Ok, report.Problem?.Code).ShouldBe((false, (LibraryJobErrorCodeDto?)code), fileName);
    }

    [Theory]
    [InlineData("view")]
    [InlineData("vocal")]
    public async Task Verify_RunsTheDatabaseChecksOfRestore(string defect)
    {
        var library = new LibraryFixture(Factory);
        var track = await library.TrackAsync(detail: false);
        if (defect == "view")
        {
            await Factory.WithScopeAsync(sp => sp.GetRequiredService<Persistence.StemStudioDbContext>().Database.ExecuteSqlRawAsync("CREATE VIEW evil AS SELECT 1", Ct));
        }
        else
        {
            await library.GenerationAsync(track, _ => "../keys/key-1.xml");
        }

        var name = await RestoreServiceTests.BackupAsync(Client, Factory);

        using var response = await Client.PostAsync(ApiRoutes.VerifyBackup(name), null, Ct);
        var report = (await FinishedAsync(response)).Report.ShouldBeOfType<VerifyReportDto>();
        (report.Ok, report.Problem?.Code).ShouldBe((false, (LibraryJobErrorCodeDto?)LibraryJobErrorCodeDto.ArchiveUnsafe));
    }

    [Theory]
    [InlineData("h13b-foreign-database.zip", LibraryJobErrorCodeDto.ArchiveCorrupt)]
    [InlineData("h14-stored-path.zip", LibraryJobErrorCodeDto.ArchiveUnsafe)]
    public async Task Restore_FailsWithTheSameCodeAndChangesNothing(string fileName, LibraryJobErrorCodeDto code)
    {
        var library = new LibraryFixture(Factory);
        await library.TrackAsync();
        var files = library.LibraryFiles();
        Copy(fileName);

        using var response = await Client.PostAsJsonAsync(ApiRoutes.RestoreBackup(fileName), Replace, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var job = await FinishedAsync(response);
        (job.Status, job.Error?.Code).ShouldBe((LibraryJobStatusDto.Failed, (LibraryJobErrorCodeDto?)code));
        library.LibraryFiles().ShouldBe(files);
    }

    [Theory]
    [InlineData("h17b-entry-count.zip", LibraryCareProblemTypes.ArchiveInvalid)]
    [InlineData("format-2.zip", LibraryCareProblemTypes.ArchiveIncompatible)]
    public async Task Restore_IsRefusedBeforeAJob(string fileName, string type)
    {
        Copy(fileName);

        using var response = await Client.PostAsJsonAsync(ApiRoutes.RestoreBackup(fileName), Replace, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.JsonAsync()).GetProperty("type").GetString().ShouldBe(LibraryCareProblemTypes.BaseUri + type);
    }

    [Fact]
    public async Task List_ShowsEntryCountBombsAsUnrecognisedAndAnotherFormatAsIncompatible()
    {
        Copy("h17b-entry-count.zip");
        Copy("format-2.zip");
        Copy("valid-base.zip");

        using var response = await Client.GetAsync(ApiRoutes.Backups, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ContractAssert.MatchesAsync(response, "listBackups");
        var archives = (await response.BodyAsync<BackupListDto>()).Archives.ToDictionary(archive => archive.FileName);
        (archives["h17b-entry-count.zip"].Recognised, archives["h17b-entry-count.zip"].Compatibility.Restorable).ShouldBe((false, false));
        var other = archives["format-2.zip"];
        (other.Recognised, other.FormatVersion, other.Compatibility.Restorable, other.Compatibility.MigrationNeeded).ShouldBe((true, "2.0.0", false, false));
        other.Compatibility.Reason.ShouldNotBeNullOrWhiteSpace();
        (archives["valid-base.zip"].Recognised, archives["valid-base.zip"].Compatibility.Restorable).ShouldBe((true, true));
    }

    // Security review 013 item 6: one bad manifest never turns the whole list into a 500.
    [Fact]
    public async Task List_SurvivesManifestsWithNullMembersAndAnotherFormatShape()
    {
        Directory.CreateDirectory(Factory.BackupDir);
        HostileArchives.Write(Path.Combine(Factory.BackupDir, "null-app.zip"), [HostileArchives.StemEntry()], manifest => manifest["app"] = null);
        HostileArchives.Write(Path.Combine(Factory.BackupDir, "null-entry.zip"), [HostileArchives.StemEntry()], manifest => ((List<Dictionary<string, object?>>)manifest["entries"]!).Add(null!));
        HostileArchives.Write(Path.Combine(Factory.BackupDir, "format-3.zip"), [HostileArchives.StemEntry()], manifest =>
        {
            manifest["formatVersion"] = "3.1.0";
            manifest["entries"] = "another shape";
        });

        using var response = await Client.GetAsync(ApiRoutes.Backups, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ContractAssert.MatchesAsync(response, "listBackups");
        var archives = (await response.BodyAsync<BackupListDto>()).Archives.ToDictionary(archive => archive.FileName);
        (archives["null-app.zip"].Recognised, archives["null-entry.zip"].Recognised).ShouldBe((false, false));
        (archives["format-3.zip"].Recognised, archives["format-3.zip"].FormatVersion, archives["format-3.zip"].Compatibility.Restorable).ShouldBe((true, "3.1.0", false));
    }

    [Fact]
    public async Task Details_OfAnotherFormat_IsIncompatibleWithTheReason()
    {
        Copy("format-2.zip");

        using var response = await Client.GetAsync(ApiRoutes.Backup("format-2.zip"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ContractAssert.MatchesAsync(response, "getBackup");
        var details = await response.BodyAsync<BackupDetailsDto>();
        (details.Recognised, details.Compatibility.Restorable, details.FormatVersion).ShouldBe((true, false, "2.0.0"));
        details.Compatibility.Reason.ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("valid-base.zip")]
    [InlineData("schema-008.zip")]
    [InlineData("schema-010.zip")]
    public async Task ValidArchives_OfEverySchemaLevel_VerifyAndRestore(string fileName)
    {
        Copy(fileName);

        using var verify = await Client.PostAsync(ApiRoutes.VerifyBackup(fileName), null, Ct);
        var report = (await FinishedAsync(verify)).Report.ShouldBeOfType<VerifyReportDto>();
        using var restore = await Client.PostAsJsonAsync(ApiRoutes.RestoreBackup(fileName), Replace, Ct);
        var job = await FinishedAsync(restore);

        (report.Ok, report.Problem).ShouldBe((true, null));
        job.Status.ShouldBe(LibraryJobStatusDto.Completed, job.Error?.Message);
    }

    [Theory]
    [InlineData("table", "Tracks", "Tracks", true)]
    [InlineData("table", "__EFMigrationsHistory", "__EFMigrationsHistory", true)]
    [InlineData("table", "sqlite_sequence", "sqlite_sequence", true)]
    [InlineData("index", "sqlite_autoindex_Tracks_1", "Tracks", true)]
    [InlineData("table", "Extra", "Extra", false)]
    [InlineData("index", "IX_Evil", "Tracks", false)]
    [InlineData("index", "sqlite_autoindex_Extra_1", "Extra", false)]
    [InlineData("trigger", "Tracks", "Tracks", false)]
    [InlineData("view", "Tracks", "Tracks", false)]
    public void KnownSchema_AllowsOnlyTheMigrationsTablesAndIndexes(string type, string name, string table, bool known) =>
        StemStudio.Api.LibraryCare.Restore.KnownSchema.IsKnown(type, name, table).ShouldBe(known);

    private void Copy(string fileName)
    {
        Directory.CreateDirectory(Factory.BackupDir);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "backup", fileName), Path.Combine(Factory.BackupDir, fileName));
    }

    private async Task<LibraryJobDto> FinishedAsync(HttpResponseMessage accepted)
    {
        await Runner.WhenIdleAsync();
        return (await Runner.GetAsync((await accepted.BodyAsync<LibraryJobDto>()).JobId, Ct))!;
    }
}
