using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Agent.Ports;
using StemStudio.Api.LibraryCare.Jobs;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.LibraryCare;
using StemStudio.TestDoubles.ClaudeKey;

namespace StemStudio.Api.Tests.LibraryCare;

/// <summary>013 T048: restore, previous library, undo and discard end to end (FR-012-FR-018; 011 CK-FR-017).</summary>
public sealed class RestoreServiceTests : ApiTestBase
{
    private readonly LibraryFixture _library;

    public RestoreServiceTests() => _library = new LibraryFixture(Factory);

    private LibraryJobRunner Runner => Factory.Services.GetRequiredService<LibraryJobRunner>();

    [Fact]
    public async Task Replace_BringsBackTheLibraryAndKeepsThePreviousOne()
    {
        var track = await _library.TrackAsync();
        await _library.TrackAsync("ccccccccccc", detail: false);
        var files = _library.LibraryFiles();
        var name = await BackupAsync(Client, Factory);
        (await Client.DeleteAsync(ApiRoutes.Track(track.Id), Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var response = await RestoreAsync(name, new RestoreOptionsDto { Mode = RestoreModeDto.Replace, Confirmation = "REPLACE" });
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await ContractAssert.MatchesAsync(response, "restoreBackup");
        var job = await FinishedAsync(response);

        job.Status.ShouldBe(LibraryJobStatusDto.Completed, job.Error?.Message);
        var report = job.Report.ShouldBeOfType<RestoreReportDto>();
        (report.Mode, report.PreviousLibraryKept, report.MigratedFrom).ShouldBe((RestoreReportModeDto.Replace, true, null));
        (await Factory.FindTrackAsync(track.Id)).ShouldNotBeNull();
        _library.LibraryFiles().ShouldBe(files);
        using var previous = await Client.GetAsync(ApiRoutes.PreviousLibrary, Ct);
        var dto = await previous.BodyAsync<PreviousLibraryDto>();
        (dto.RestoredArchive, dto.Counts.Tracks).ShouldBe((name, 1));
        await ContractAssert.MatchesAsync(previous, "getPreviousLibrary");
    }

    [Fact]
    public async Task Replace_OfANonEmptyLibraryNeedsTheTypedConfirmation()
    {
        await _library.TrackAsync();
        var name = await BackupAsync(Client, Factory);

        using var response = await RestoreAsync(name, new RestoreOptionsDto { Mode = RestoreModeDto.Replace, Confirmation = "yes" });

        (response.StatusCode, await TypeAsync(response)).ShouldBe((HttpStatusCode.UnprocessableEntity, LibraryCareProblemTypes.ConfirmationRequired));
    }

    [Fact]
    public async Task ActiveWork_BlocksARestore()
    {
        await _library.TrackAsync();
        var name = await BackupAsync(Client, Factory);
        await Factory.SeedAsync(SyncTestData.UrlTrack("bbbbbbbbbbb"));

        using var response = await RestoreAsync(name, new RestoreOptionsDto { Mode = RestoreModeDto.Replace, Confirmation = "REPLACE" });

        (await TypeAsync(response)).ShouldBe(LibraryCareProblemTypes.JobsActive);
        (await response.JsonAsync()).GetProperty("activeWork").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task UndoAndDiscard_OfThePreviousLibrary()
    {
        var track = await _library.TrackAsync();
        await _library.TrackAsync("ccccccccccc", detail: false);
        var name = await BackupAsync(Client, Factory);
        (await Client.DeleteAsync(ApiRoutes.Track(track.Id), Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await FinishedAsync(await RestoreAsync(name, new RestoreOptionsDto { Mode = RestoreModeDto.Replace, Confirmation = "REPLACE" }));

        using var second = await RestoreAsync(name, new RestoreOptionsDto { Mode = RestoreModeDto.Replace, Confirmation = "REPLACE" });
        (await TypeAsync(second)).ShouldBe(LibraryCareProblemTypes.PreviousLibraryExists);

        using var revert = await Client.PostAsync(ApiRoutes.RevertRestore, null, Ct);
        revert.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await ContractAssert.MatchesAsync(revert, "revertRestore");
        var undo = await FinishedAsync(revert);
        undo.Report.ShouldBeOfType<RestoreReportDto>().Mode.ShouldBe(RestoreReportModeDto.Undo);
        (await Factory.FindTrackAsync(track.Id)).ShouldBeNull();

        using var wrong = await Client.PostAsJsonAsync(ApiRoutes.DiscardPreviousLibrary, new ConfirmationDto { Confirmation = "yes" }, Ct);
        (await TypeAsync(wrong)).ShouldBe(LibraryCareProblemTypes.ConfirmationRequired);
        using var discard = await Client.PostAsJsonAsync(ApiRoutes.DiscardPreviousLibrary, new ConfirmationDto { Confirmation = "DISCARD" }, Ct);
        discard.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Client.GetAsync(ApiRoutes.PreviousLibrary, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Client.PostAsync(ApiRoutes.RevertRestore, null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Client.PostAsJsonAsync(ApiRoutes.DiscardPreviousLibrary, new ConfirmationDto { Confirmation = "DISCARD" }, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EmptyLibrary_IsRestoredWithoutConfirmation_AndAnotherKeyRingMakesTheStoredKeyUnreadable()
    {
        await _library.TrackAsync();
        using (var stored = await Client.PutAsJsonAsync(ApiRoutes.AgentClaudeKey, new { apiKey = ClaudeKeySentinels.Stored }, Ct))
        {
            stored.IsSuccessStatusCode.ShouldBeTrue();
        }

        var name = await BackupAsync(Client, Factory);
        using var other = new ApiFactory();
        using var client = other.CreateClient();
        Directory.CreateDirectory(other.BackupDir);
        File.Copy(Path.Combine(Factory.BackupDir, name), Path.Combine(other.BackupDir, name));

        using var response = await client.PostAsJsonAsync(ApiRoutes.RestoreBackup(name), new RestoreOptionsDto { Mode = RestoreModeDto.Replace }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var runner = other.Services.GetRequiredService<LibraryJobRunner>();
        await runner.WhenIdleAsync();
        var job = (await runner.GetAsync((await response.BodyAsync<LibraryJobDto>()).JobId, Ct))!;

        var report = job.Report.ShouldBeOfType<RestoreReportDto>();
        (report.Mode, report.PreviousLibraryKept).ShouldBe((RestoreReportModeDto.Empty, false));
        report.Warnings.ShouldContain("The stored Claude key must be entered again.");
        other.Services.GetRequiredService<IStoredClaudeKeyReader>().Read().State.ShouldBe(StoredClaudeKeyStateDto.Unreadable);
        (await other.AllTracksAsync()).Count.ShouldBe(1);
        (await client.GetAsync(ApiRoutes.PreviousLibrary, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SameKeyRing_KeepsTheStoredKeyReadable()
    {
        await _library.TrackAsync();
        using (var stored = await Client.PutAsJsonAsync(ApiRoutes.AgentClaudeKey, new { apiKey = ClaudeKeySentinels.Stored }, Ct))
        {
            stored.IsSuccessStatusCode.ShouldBeTrue();
        }

        var name = await BackupAsync(Client, Factory);

        var job = await FinishedAsync(await RestoreAsync(name, new RestoreOptionsDto { Mode = RestoreModeDto.Replace, Confirmation = "REPLACE" }));

        job.Report.ShouldBeOfType<RestoreReportDto>().Warnings.ShouldNotContain("The stored Claude key must be entered again.");
        Factory.Services.GetRequiredService<IStoredClaudeKeyReader>().Read().State.ShouldBe(StoredClaudeKeyStateDto.Readable);
    }

    [Fact]
    public async Task HostileArchive_FailsWithoutChangingTheLibrary()
    {
        var track = await _library.TrackAsync();
        var files = _library.LibraryFiles();
        Directory.CreateDirectory(Factory.BackupDir);
        HostileArchives.Write(Path.Combine(Factory.BackupDir, "evil.zip"), [HostileArchives.StemEntry(), new("files/../../etc/passwd", [1])]);

        var job = await FinishedAsync(await RestoreAsync("evil.zip", new RestoreOptionsDto { Mode = RestoreModeDto.Replace, Confirmation = "REPLACE" }));

        (job.Status, job.Error!.Code).ShouldBe((LibraryJobStatusDto.Failed, LibraryJobErrorCodeDto.ArchiveUnsafe));
        _library.LibraryFiles().ShouldBe(files);
        (await Factory.FindTrackAsync(track.Id)).ShouldNotBeNull();
    }

    [Fact]
    public async Task UnknownOrUnrecognisedArchives_AreRefused()
    {
        await _library.TrackAsync();
        Directory.CreateDirectory(Factory.BackupDir);
        await File.WriteAllTextAsync(Path.Combine(Factory.BackupDir, "broken.zip"), "x", Ct);
        var options = new RestoreOptionsDto { Mode = RestoreModeDto.Replace, Confirmation = "REPLACE" };

        (await RestoreAsync("nope.zip", options)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await TypeAsync(await RestoreAsync("broken.zip", options))).ShouldBe(LibraryCareProblemTypes.ArchiveInvalid);
    }

    internal static async Task<string> BackupAsync(HttpClient client, ApiFactory factory)
    {
        using var created = await client.PostAsJsonAsync(ApiRoutes.Backups, new BackupOptionsDto(), Ct);
        var runner = factory.Services.GetRequiredService<LibraryJobRunner>();
        await runner.WhenIdleAsync();
        return (await runner.GetAsync((await created.BodyAsync<LibraryJobDto>()).JobId, Ct))!.Report.ShouldBeOfType<BackupReportDto>().FileName;
    }

    private static async Task<string?> TypeAsync(HttpResponseMessage response) =>
        (await response.JsonAsync()).GetProperty("type").GetString()![LibraryCareProblemTypes.BaseUri.Length..];

    private Task<HttpResponseMessage> RestoreAsync(string name, RestoreOptionsDto options) =>
        Client.PostAsJsonAsync(ApiRoutes.RestoreBackup(name), options, Ct);

    private async Task<LibraryJobDto> FinishedAsync(HttpResponseMessage accepted)
    {
        await Runner.WhenIdleAsync();
        return (await Runner.GetAsync((await accepted.BodyAsync<LibraryJobDto>()).JobId, Ct))!;
    }
}
