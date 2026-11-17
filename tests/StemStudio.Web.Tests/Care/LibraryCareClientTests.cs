using System.Net;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Web.Api;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Care;

/// <summary>Every library care call of backend API 1.12.0 (013 delta): route, method, body and the problem code.</summary>
public sealed class LibraryCareClientTests : IDisposable
{
    private const string Job = """
        {"jobId":"8e0c1f2a-3b4d-4e5f-8a6b-7c8d9e0f1a2b","kind":"backup","status":"completed","stage":null,"stageText":null,"progress":100,
         "bytesDone":10,"bytesTotal":10,"currentItem":null,"etaSeconds":null,"archiveFileName":"a.zip","cancellable":false,
         "report":{"fileName":"a.zip","included":[],"entries":3,"bytes":10,"warnings":[],"type":"backup"},"error":null,
         "createdAt":"2026-11-17T10:00:00Z","startedAt":"2026-11-17T10:00:01Z","finishedAt":"2026-11-17T10:01:00Z"}
        """;

    private const string Folder = """{"available":true,"writable":true,"hostPathHint":"./backups","freeBytes":100,"totalBytes":200,"reason":null}""";

    private readonly StubHttpMessageHandler _handler = new();
    private readonly LibraryCareClient _client;

    public LibraryCareClientTests() => _client = new LibraryCareClient(StubHttpMessageHandler.CreateClient(_handler));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _handler.Dispose();

    private (HttpMethod, string, string?) Sent(int index = 0) => (_handler.Requests[index].Method, _handler.Requests[index].PathAndQuery, _handler.Requests[index].Body);

    [Fact]
    public async Task Status_ReadsTheGateAndTheActiveJob_WithItsReportOutOfOrder()
    {
        _handler.Respond(HttpStatusCode.OK, $$"""{"gate":"backupRunning","activeJob":{{Job}},"backupFolder":{{Folder}},"previousLibrary":null,"activeWork":[],"libraryEmpty":false}""");

        var status = await _client.GetStatusAsync(Ct);

        status.Gate.ShouldBe(GateModeDto.BackupRunning);
        status.ActiveJob!.Report.ShouldBeOfType<BackupReportDto>().Entries.ShouldBe(3);
        Sent().ShouldBe((HttpMethod.Get, "/api/library-care/status", null));
    }

    [Fact]
    public async Task Backups_ListEstimateCreateGetDeleteVerify_UseTheirRoutes()
    {
        _handler.Respond(HttpStatusCode.OK, $$"""{"folder":{{Folder}},"archives":[]}""")
            .Respond(HttpStatusCode.OK, $$"""{"totalBytes":5,"categories":[],"folder":{{Folder}},"enough":true}""")
            .Respond(HttpStatusCode.Accepted, Job)
            .Respond(HttpStatusCode.OK, """
                {"fileName":"a b.zip","sizeBytes":1,"modifiedAt":"2026-11-17T10:00:00Z","recognised":true,"reason":null,"backupId":null,"createdAt":null,
                 "appVersion":null,"backendApi":null,"formatVersion":"1.0.0","included":["database"],"counts":null,
                 "compatibility":{"restorable":true,"reason":null,"migrationNeeded":false},"excluded":["detailStems"],"entries":2,"totalBytes":3,
                 "schemaLastMigration":"m1","voiceProfiles":[],"warnings":[]}
                """)
            .Respond(HttpStatusCode.NoContent)
            .Respond(HttpStatusCode.Accepted, Job);

        (await _client.ListBackupsAsync(Ct)).Archives.ShouldBeEmpty();
        (await _client.EstimateBackupAsync(new BackupOptionsDto { DetailStems = false }, Ct)).TotalBytes.ShouldBe(5);
        (await _client.CreateBackupAsync(new BackupOptionsDto { DetailStems = false }, Ct)).Kind.ShouldBe(LibraryJobKindDto.Backup);
        (await _client.GetBackupAsync("a b.zip", Ct)).Excluded.ShouldBe([BackupCategoryDto.DetailStems]);
        await _client.DeleteBackupAsync("a.zip", Ct);
        await _client.VerifyBackupAsync("a.zip", Ct);

        Sent(0).ShouldBe((HttpMethod.Get, "/api/backups", null));
        Sent(1).ShouldBe((HttpMethod.Get, "/api/backups/estimate?detailStems=false&exports=true&scores=true&generations=true&recordings=true&voiceProfiles=true", null));
        Sent(2).ShouldBe((HttpMethod.Post, "/api/backups",
            """{"detailStems":false,"exports":true,"scores":true,"generations":true,"recordings":true,"voiceProfiles":true}"""));
        Sent(3).ShouldBe((HttpMethod.Get, "/api/backups/a%20b.zip", null));
        Sent(4).ShouldBe((HttpMethod.Delete, "/api/backups/a.zip", null));
        Sent(5).ShouldBe((HttpMethod.Post, "/api/backups/a.zip/verify", null));
    }

    [Fact]
    public async Task RestoreJobsAndPreviousLibrary_UseTheirRoutes()
    {
        _handler.Respond(HttpStatusCode.Accepted, Job).Respond(HttpStatusCode.OK, $"[{Job}]").Respond(HttpStatusCode.OK, Job)
            .Respond(HttpStatusCode.Accepted, Job).Respond(HttpStatusCode.Accepted, Job).Respond(HttpStatusCode.NoContent);
        var id = Guid.Parse("8e0c1f2a-3b4d-4e5f-8a6b-7c8d9e0f1a2b");

        await _client.RestoreBackupAsync("a.zip", new RestoreOptionsDto { Mode = RestoreModeDto.Replace, Confirmation = "REPLACE" }, Ct);
        (await _client.ListJobsAsync(1, Ct)).Count.ShouldBe(1);
        await _client.GetJobAsync(id, Ct);
        await _client.CancelJobAsync(id, Ct);
        await _client.RevertRestoreAsync(Ct);
        await _client.DiscardPreviousLibraryAsync("DISCARD", Ct);

        Sent(0).ShouldBe((HttpMethod.Post, "/api/backups/a.zip/restore", """{"mode":"replace","voiceProfiles":true,"confirmation":"REPLACE"}"""));
        Sent(1).ShouldBe((HttpMethod.Get, "/api/library-jobs?limit=1", null));
        Sent(2).ShouldBe((HttpMethod.Get, $"/api/library-jobs/{id}", null));
        Sent(3).ShouldBe((HttpMethod.Post, $"/api/library-jobs/{id}/cancel", null));
        Sent(4).ShouldBe((HttpMethod.Post, "/api/library-care/previous/revert", null));
        Sent(5).ShouldBe((HttpMethod.Post, "/api/library-care/previous/discard", """{"confirmation":"DISCARD"}"""));
    }

    [Fact]
    public async Task Storage_AndCleanup_UseTheirRoutes()
    {
        const string plan = """{"planId":"11111111-2222-4333-8444-555555555555","createdAt":"2026-11-17T10:00:00Z","expiresAt":"2026-11-17T10:10:00Z","items":[],"skipped":[],"totalBytes":0}""";
        _handler.Respond(HttpStatusCode.OK, """
                {"measuredAt":"2026-11-17T10:00:00Z","volumes":[],"categories":[],"tracks":[],
                 "unattached":{"exportsBytes":0,"scoresBytes":0,"generationsBytes":0,"voiceProfilesBytes":0,"lyricsSheets":0},
                 "modelCaches":[],"workerEntries":[],"backups":{"count":0,"bytes":0,"folderAvailable":true,"reason":null},"worker":{"available":false,"reason":"down"}}
                """)
            .Respond(HttpStatusCode.OK, plan)
            .Respond(HttpStatusCode.OK, """{"planId":"11111111-2222-4333-8444-555555555555","freedBytes":9,"removed":[],"failed":[]}""");
        var planId = Guid.Parse("11111111-2222-4333-8444-555555555555");

        (await _client.GetStorageOverviewAsync(true, Ct)).Worker.Reason.ShouldBe("down");
        (await _client.PreviewCleanupAsync(new CleanupRequestDto { Actions = [new CleanupActionDto { Kind = CleanupKindDto.Leftovers }] }, Ct)).PlanId.ShouldBe(planId);
        (await _client.ExecuteCleanupAsync(planId, Ct)).FreedBytes.ShouldBe(9);

        Sent(0).ShouldBe((HttpMethod.Get, "/api/storage?refresh=true", null));
        Sent(1).ShouldBe((HttpMethod.Post, "/api/storage/cleanup/preview", """{"actions":[{"kind":"leftovers"}]}"""));
        Sent(2).ShouldBe((HttpMethod.Post, "/api/storage/cleanup", $$"""{"planId":"{{planId}}"}"""));
    }

    [Fact]
    public async Task Problems_CarryTheirCode()
    {
        _handler.RespondProblem(HttpStatusCode.Conflict, """{"type":"https://stemstudio.local/problems/library-job-running","title":"Busy","status":409,"detail":"Another library job is running."}""");

        var ex = await Should.ThrowAsync<ApiException>(() => _client.CreateBackupAsync(new BackupOptionsDto(), Ct));

        (ex.Error.Code, ex.Error.UserMessage).ShouldBe(("library-job-running", "Another library job is running."));
    }
}
