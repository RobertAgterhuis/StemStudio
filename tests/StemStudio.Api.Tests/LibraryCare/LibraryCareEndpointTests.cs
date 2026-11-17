using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.LibraryCare.Gate;
using StemStudio.Api.LibraryCare.Jobs;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.LibraryCare;

namespace StemStudio.Api.Tests.LibraryCare;

/// <summary>013 T037: the library-care endpoints of backend-api 1.12.0 (status, backups, jobs).</summary>
public sealed class LibraryCareEndpointTests : ApiTestBase
{
    private readonly LibraryFixture _library;

    public LibraryCareEndpointTests() => _library = new LibraryFixture(Factory);

    private LibraryJobRunner Runner => Factory.Services.GetRequiredService<LibraryJobRunner>();

    [Fact]
    public async Task Status_IdleLibrary()
    {
        using var empty = await Client.GetAsync(ApiRoutes.LibraryCareStatus, Ct);
        (await empty.BodyAsync<LibraryCareStatusDto>()).LibraryEmpty.ShouldBeTrue();
        await _library.TrackAsync();

        using var response = await Client.GetAsync(ApiRoutes.LibraryCareStatus, Ct);

        var status = await response.BodyAsync<LibraryCareStatusDto>();
        (status.Gate, status.ActiveJob, status.LibraryEmpty, status.BackupFolder.Writable, status.BackupFolder.HostPathHint).ShouldBe(
            (GateModeDto.Open, null, false, true, "./backups"));
        status.ActiveWork.ShouldBeEmpty();
        await ContractAssert.MatchesAsync(response, "getLibraryCareStatus");
    }

    [Fact]
    public async Task Status_ListsActiveWorkAndAnswersDuringMaintenance()
    {
        await Factory.SeedAsync(SyncTestData.UrlTrack());
        using (var busy = await Client.GetAsync(ApiRoutes.LibraryCareStatus, Ct))
        {
            var work = (await busy.BodyAsync<LibraryCareStatusDto>()).ActiveWork.ShouldHaveSingleItem();
            work.Kind.ShouldBe(ActiveWorkKindDto.Import);
        }

        Factory.Services.GetRequiredService<LibraryGate>().EnterMaintenance();
        using var response = await Client.GetAsync(ApiRoutes.LibraryCareStatus, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.BodyAsync<LibraryCareStatusDto>()).Gate.ShouldBe(GateModeDto.Maintenance);
    }

    [Fact]
    public async Task Estimate_SizesPerCategoryForTheOptions()
    {
        await _library.TrackAsync();

        using var response = await Client.GetAsync(ApiRoutes.BackupEstimateFor(new BackupOptionsDto { DetailStems = false }), Ct);

        var estimate = await response.BodyAsync<BackupEstimateDto>();
        var detail = estimate.Categories.Single(category => category.Category == BackupCategoryDto.DetailStems);
        (detail.Included, detail.Optional, detail.Files, detail.Regenerability).ShouldBe((false, true, 6, RegenerabilityDto.Regenerable));
        estimate.Categories.Single(category => category.Category == BackupCategoryDto.Stems).Bytes.ShouldBe(6 * 64);
        estimate.TotalBytes.ShouldBe(estimate.Categories.Where(category => category.Included).Sum(category => category.Bytes));
        estimate.Enough.ShouldBeTrue();
        await ContractAssert.MatchesAsync(response, "estimateBackup");
    }

    [Fact]
    public async Task Estimate_RejectsNonBooleanOptions()
    {
        using var response = await Client.GetAsync($"{ApiRoutes.BackupEstimate}?detailStems=maybe", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateBackup_Accepts_ThenTheJobCompletes_AndTheListShowsTheArchive()
    {
        await _library.TrackAsync();

        using var created = await Client.PostAsJsonAsync(ApiRoutes.Backups, new BackupOptionsDto(), Ct);

        created.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var job = await created.BodyAsync<LibraryJobDto>();
        created.Headers.Location!.OriginalString.ShouldBe(ApiRoutes.LibraryJob(job.JobId));
        await ContractAssert.MatchesAsync(created, "createBackup");
        await Runner.WhenIdleAsync();
        using var got = await Client.GetAsync(ApiRoutes.LibraryJob(job.JobId), Ct);
        (await got.BodyAsync<LibraryJobDto>()).Status.ShouldBe(LibraryJobStatusDto.Completed);
        await ContractAssert.MatchesAsync(got, "getLibraryJob");
        using var list = await Client.GetAsync(ApiRoutes.Backups, Ct);
        var archive = (await list.BodyAsync<BackupListDto>()).Archives.ShouldHaveSingleItem();
        (archive.Recognised, archive.Counts!.Tracks, archive.Compatibility.Restorable, archive.Compatibility.MigrationNeeded).ShouldBe((true, 1, true, false));
        archive.Included.ShouldContain(BackupCategoryDto.Stems);
        await ContractAssert.MatchesAsync(list, "listBackups");
    }

    [Fact]
    public async Task List_ShowsForeignZipsAsUnrecognisedAndHidesPartialFiles()
    {
        Directory.CreateDirectory(Factory.BackupDir);
        await File.WriteAllTextAsync(Path.Combine(Factory.BackupDir, "notes.txt"), "x", Ct);
        await File.WriteAllTextAsync(Path.Combine(Factory.BackupDir, "broken.zip"), "not a zip", Ct);
        await File.WriteAllTextAsync(Path.Combine(Factory.BackupDir, "stemstudio-backup-1.zip.partial"), "x", Ct);
        using (var zip = ZipFile.Open(Path.Combine(Factory.BackupDir, "holiday-photos.zip"), ZipArchiveMode.Create))
        {
            zip.CreateEntry("photo.jpg");
        }

        using var response = await Client.GetAsync(ApiRoutes.Backups, Ct);

        var archives = (await response.BodyAsync<BackupListDto>()).Archives;
        archives.Select(archive => archive.FileName).ShouldBe(["broken.zip", "holiday-photos.zip"], ignoreOrder: true);
        archives.ShouldAllBe(archive => !archive.Recognised && archive.Reason != null && !archive.Compatibility.Restorable);
        await ContractAssert.MatchesAsync(response, "listBackups");
    }

    [Fact]
    public async Task Details_DownloadVerifyAndDelete()
    {
        await _library.TrackAsync();
        await _library.VoiceProfileAsync();
        var name = await BackupAsync();

        using var details = await Client.GetAsync(ApiRoutes.Backup(name), Ct);
        var dto = await details.BodyAsync<BackupDetailsDto>();
        (dto.Entries > 6, dto.VoiceProfiles.ShouldHaveSingleItem().Valid, dto.SchemaLastMigration.Length > 0).ShouldBe((true, true, true));
        await ContractAssert.MatchesAsync(details, "getBackup");

        using var file = await Client.GetAsync(ApiRoutes.BackupFile(name), Ct);
        file.Content.Headers.ContentDisposition!.FileName!.Trim('"').ShouldBe(name);
        (file.Headers.AcceptRanges.Single(), file.Headers.ETag is not null).ShouldBe(("bytes", true));
        using var range = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.BackupFile(name)) { Headers = { Range = new RangeHeaderValue(0, 9) } };
        using var partial = await Client.SendAsync(range, Ct);
        (partial.StatusCode, partial.Content.Headers.ContentLength).ShouldBe((HttpStatusCode.PartialContent, 10));
        using var beyond = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.BackupFile(name)) { Headers = { Range = new RangeHeaderValue(1L << 40, null) } };
        (await Client.SendAsync(beyond, Ct)).StatusCode.ShouldBe(HttpStatusCode.RequestedRangeNotSatisfiable);

        using var verify = await Client.PostAsync(ApiRoutes.VerifyBackup(name), null, Ct);
        verify.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await Runner.WhenIdleAsync();
        var verified = (await Runner.GetAsync((await verify.BodyAsync<LibraryJobDto>()).JobId, Ct))!;
        verified.Report.ShouldBeOfType<VerifyReportDto>().Ok.ShouldBeTrue();

        using var deleted = await Client.DeleteAsync(ApiRoutes.Backup(name), Ct);
        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        File.Exists(Path.Combine(Factory.BackupDir, name)).ShouldBeFalse();
    }

    [Fact]
    public async Task Verify_ReportsADamagedArchive()
    {
        await _library.TrackAsync();
        var name = await BackupAsync();
        var path = Path.Combine(Factory.BackupDir, name);
        var bytes = await File.ReadAllBytesAsync(path, Ct);
        var stem = FindStored(bytes, "files/tracks/");
        bytes[stem] ^= 0xFF;
        await File.WriteAllBytesAsync(path, bytes, Ct);

        using var verify = await Client.PostAsync(ApiRoutes.VerifyBackup(name), null, Ct);
        await Runner.WhenIdleAsync();

        var report = (await Runner.GetAsync((await verify.BodyAsync<LibraryJobDto>()).JobId, Ct))!.Report.ShouldBeOfType<VerifyReportDto>();
        (report.Ok, report.Problem!.Code).ShouldBe((false, LibraryJobErrorCodeDto.ArchiveCorrupt));
    }

    [Theory]
    [InlineData("GET", "")]
    [InlineData("GET", "/file")]
    [InlineData("DELETE", "")]
    [InlineData("POST", "/verify")]
    public async Task UnknownArchives_Are404(string method, string suffix)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), ApiRoutes.Backup("nope.zip") + suffix);

        (await Client.SendAsync(request, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("..%2Fstemstudio.db")]
    [InlineData("a.txt")]
    [InlineData(".hidden.zip")]
    public async Task InvalidArchiveNames_Are404(string name)
    {
        (await Client.GetAsync($"{ApiRoutes.Backups}/{name}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Details_OfANonBackupIs422()
    {
        Directory.CreateDirectory(Factory.BackupDir);
        await File.WriteAllTextAsync(Path.Combine(Factory.BackupDir, "broken.zip"), "not a zip", Ct);

        using var response = await Client.GetAsync(ApiRoutes.Backup("broken.zip"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.JsonAsync()).GetProperty("type").GetString().ShouldBe(LibraryCareProblemTypes.BaseUri + LibraryCareProblemTypes.ArchiveInvalid);
    }

    [Fact]
    public async Task Delete_IsRefusedWhileAJobUsesTheArchiveAndDuringABackup()
    {
        await _library.TrackAsync();
        var name = await BackupAsync();
        var release = new TaskCompletionSource();
        Runner.TryStart(LibraryJobKindDto.Verify, name, async (_, ct) => { await release.Task.WaitAsync(ct); return new VerifyReportDto { Ok = true, EntriesChecked = 0, BytesChecked = 0, Problem = null }; }, out _).ShouldBeTrue();

        using var inUse = await Client.DeleteAsync(ApiRoutes.Backup(name), Ct);
        (await inUse.JsonAsync()).GetProperty("type").GetString().ShouldBe(LibraryCareProblemTypes.BaseUri + LibraryCareProblemTypes.ArchiveInUse);
        using var second = await Client.PostAsync(ApiRoutes.VerifyBackup(name), null, Ct);
        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        release.SetResult();
        await Runner.WhenIdleAsync();

        Factory.Services.GetRequiredService<LibraryGate>().TryBeginBackup();
        using var gated = await Client.DeleteAsync(ApiRoutes.Backup(name), Ct);
        (await gated.JsonAsync()).GetProperty("type").GetString().ShouldBe(LibraryCareProblemTypes.BaseUri + LibraryCareProblemTypes.BackupRunning);
    }

    [Fact]
    public async Task Jobs_ListGetAndCancel()
    {
        var release = new TaskCompletionSource();
        Runner.TryStart(LibraryJobKindDto.Backup, "a.zip", async (_, ct) => { await release.Task.WaitAsync(ct); return new VerifyReportDto { Ok = true, EntriesChecked = 0, BytesChecked = 0, Problem = null }; }, out var job).ShouldBeTrue();

        using var list = await Client.GetAsync(ApiRoutes.LibraryJobsWithLimit(5), Ct);
        (await list.BodyAsync<List<LibraryJobDto>>()).ShouldContain(item => item.JobId == job.JobId);
        await ContractAssert.MatchesAsync(list, "listLibraryJobs");
        using var cancel = await Client.PostAsync(ApiRoutes.CancelLibraryJob(job.JobId), null, Ct);
        cancel.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await ContractAssert.MatchesAsync(cancel, "cancelLibraryJob");
        await Runner.WhenIdleAsync();

        using var again = await Client.PostAsync(ApiRoutes.CancelLibraryJob(job.JobId), null, Ct);
        (await again.JsonAsync()).GetProperty("type").GetString().ShouldBe(LibraryCareProblemTypes.BaseUri + LibraryCareProblemTypes.NotCancellable);
        (await Client.PostAsync(ApiRoutes.CancelLibraryJob(Guid.NewGuid()), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Client.GetAsync(ApiRoutes.LibraryJob(Guid.NewGuid()), Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public async Task Jobs_LimitOutOfRangeIs400(int limit) =>
        (await Client.GetAsync(ApiRoutes.LibraryJobsWithLimit(limit), Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

    [Fact]
    public async Task CreateBackup_RefusesWhileAJobRuns()
    {
        var release = new TaskCompletionSource();
        Runner.TryStart(LibraryJobKindDto.Verify, "a.zip", async (_, ct) => { await release.Task.WaitAsync(ct); return new VerifyReportDto { Ok = true, EntriesChecked = 0, BytesChecked = 0, Problem = null }; }, out _).ShouldBeTrue();

        using var response = await Client.PostAsJsonAsync(ApiRoutes.Backups, new BackupOptionsDto(), Ct);
        release.SetResult();
        await Runner.WhenIdleAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await ContractAssert.MatchesAsync(response, "createBackup");
    }

    private async Task<string> BackupAsync()
    {
        using var created = await Client.PostAsJsonAsync(ApiRoutes.Backups, new BackupOptionsDto(), Ct);
        await Runner.WhenIdleAsync();
        var job = (await Runner.GetAsync((await created.BodyAsync<LibraryJobDto>()).JobId, Ct))!;
        return job.Report.ShouldBeOfType<BackupReportDto>().FileName;
    }

    private static int FindStored(byte[] bytes, string prefix)
    {
        var marker = System.Text.Encoding.ASCII.GetBytes(prefix);
        var at = bytes.AsSpan().IndexOf(marker);
        var nameEnd = bytes.AsSpan(at).IndexOf(".flac"u8) + at + 5;
        return nameEnd + 2;
    }
}
