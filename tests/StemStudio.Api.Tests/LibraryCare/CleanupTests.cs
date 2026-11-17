using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.LibraryCare.Gate;
using StemStudio.Api.LibraryCare.Jobs;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Rendering;

namespace StemStudio.Api.Tests.LibraryCare;

/// <summary>013 T058: clean-up plans and their execution (FR-022-FR-026, SC-007, SC-010).</summary>
public sealed class CleanupTests : ApiTestBase
{
    private const string Sha = "6b86b273ff34fce19d6b804eff5a3f5747ada4eaa22f1d49c01e52ddb7875b4b";
    private readonly LibraryFixture _library;

    public CleanupTests() => _library = new LibraryFixture(Factory);

    [Fact]
    public async Task DetailStems_PreviewThenExecute_RemovesOnlyDetailFilesAndMarksTheTrack()
    {
        var track = await _library.TrackAsync();
        await _library.ExportAsync(track);
        await _library.VoiceProfileAsync();
        var before = _library.LibraryFiles();

        using var preview = await PreviewAsync(new CleanupActionDto { Kind = CleanupKindDto.DetailStems });
        await ContractAssert.MatchesAsync(preview, "previewCleanup");
        var plan = await preview.BodyAsync<CleanupPlanDto>();
        var item = plan.Items.ShouldHaveSingleItem();
        (item.TrackId, item.Files, item.Bytes, plan.TotalBytes, plan.ExpiresAt - plan.CreatedAt).ShouldBe((track.Id, 6, 192L, 192L, TimeSpan.FromMinutes(10)));

        using var executed = await ExecuteAsync(plan.PlanId);
        await ContractAssert.MatchesAsync(executed, "executeCleanup");
        var result = await executed.BodyAsync<CleanupResultDto>();

        result.FreedBytes.ShouldBe(192);
        var after = _library.LibraryFiles();
        before.Keys.Except(after.Keys).ShouldAllBe(path => path.Contains("/detail/", StringComparison.Ordinal));
        after.Keys.ShouldAllBe(path => before.ContainsKey(path) && before[path] == after[path]);
        (await Factory.FindTrackAsync(track.Id))!.DetailPhase!.FilesRemovedAt.ShouldNotBeNull();
        (await ExecuteAsync(plan.PlanId)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Originals_AreNeverPlanned()
    {
        var track = await _library.TrackAsync(detail: false);
        await _library.ExportAsync(track);
        await _library.GenerationAsync(track);
        await _library.VoiceProfileAsync();

        var plan = await (await PreviewAsync(
            new CleanupActionDto { Kind = CleanupKindDto.DetailStems },
            new CleanupActionDto { Kind = CleanupKindDto.Leftovers })).BodyAsync<CleanupPlanDto>();

        plan.TotalBytes.ShouldBe(0);
        plan.Skipped.ShouldContain(skip => skip.Kind == CleanupKindDto.DetailStems && skip.Reason == CleanupSkipReasonDto.NothingToRemove);
    }

    [Fact]
    public async Task Leftovers_RemovesTempFilesUnreferencedUploadsAndOrphans()
    {
        var track = await _library.TrackAsync(detail: false);
        await _library.FileAsync("tracks/x.tmp", 5);
        await _library.FileAsync("uploads/" + new string('c', 64) + ".mp3", 6);
        await _library.FileAsync($"tracks/{Guid.NewGuid():D}/stems/vocals.flac", 7);
        Worker.OnGetStorage = () => Task.FromResult(Storage([]) with { ScratchBytes = 100 });

        var plan = await (await PreviewAsync(new CleanupActionDto { Kind = CleanupKindDto.Leftovers })).BodyAsync<CleanupPlanDto>();
        var result = await (await ExecuteAsync(plan.PlanId)).BodyAsync<CleanupResultDto>();

        plan.Items.ShouldHaveSingleItem().Bytes.ShouldBe(118);
        result.Removed.ShouldContain(removed => removed.Kind == CleanupKindDto.Leftovers && removed.Files == 3);
        _library.LibraryFiles().Keys.ShouldAllBe(path => path.Contains(LibraryFixture.Id(track.Id), StringComparison.Ordinal));
        var request = Worker.CleanupRequests.ShouldHaveSingleItem();
        (request.Scratch, request.OldJobFiles, request.CacheEntries.Count).ShouldBe((true, true, 0));
    }

    [Fact]
    public async Task WorkerCache_PlansTheTracksEntries_AndSkipsEntriesInUse()
    {
        var track = await _library.TrackAsync(detail: false);
        var busy = await _library.TrackAsync("ddddddddddd", detail: false);
        busy = await Factory.UpdateTrackAsync(busy.Id, t => t.RehydrateWorkerJob("ddddddddddddddddddddddddddddddd1", LibraryFixture.T0));
        Worker.OnGetStorage = () => Task.FromResult(Storage([
            new(Sha, 100, 50, 0, 0, [track.WorkerJobId!], false, false, null),
            new(new string('d', 64), 10, 0, 0, 0, [busy.WorkerJobId!], true, false, null)]));
        Worker.OnCleanStorage = request => Task.FromResult(new WorkerStorageCleanupResult(150, request.CacheEntries, []));

        var plan = await (await PreviewAsync(new CleanupActionDto { Kind = CleanupKindDto.WorkerCache })).BodyAsync<CleanupPlanDto>();
        var result = await (await ExecuteAsync(plan.PlanId)).BodyAsync<CleanupResultDto>();

        (plan.Items.ShouldHaveSingleItem().TrackId, plan.Items[0].Bytes).ShouldBe((track.Id, 150L));
        plan.Skipped.ShouldContain(skip => skip.TrackId == busy.Id && skip.Reason == CleanupSkipReasonDto.JobActive);
        Worker.CleanupRequests.ShouldHaveSingleItem().CacheEntries.ShouldBe([Sha]);
        result.FreedBytes.ShouldBe(150);
    }

    [Fact]
    public async Task WorkerUnavailable_IsASkip()
    {
        await _library.TrackAsync(detail: false);

        var plan = await (await PreviewAsync(new CleanupActionDto { Kind = CleanupKindDto.WorkerCache })).BodyAsync<CleanupPlanDto>();

        plan.Skipped.ShouldHaveSingleItem().Reason.ShouldBe(CleanupSkipReasonDto.WorkerUnavailable);
    }

    [Fact]
    public async Task ActiveDetailAndExports_AreSkipped()
    {
        var track = await _library.TrackAsync();
        await _library.AddAsync(Asset.Create(Guid.NewGuid(), "Busy export", new Provenance
        {
            Origin = AssetOrigin.Export,
            TrackId = track.Id,
            TrackTitle = track.Title,
            Contents = "drums, raw",
            Recipe = new ExportRecipe { TrackId = track.Id, Mode = ExportMode.Raw, Channels = ["drums"], Format = new ExportFormat(AudioFormat.Flac, 16, null) },
        }, LibraryFixture.T0));

        var plan = await (await PreviewAsync(new CleanupActionDto { Kind = CleanupKindDto.DetailStems, TrackIds = [track.Id] })).BodyAsync<CleanupPlanDto>();

        plan.Items.ShouldBeEmpty();
        plan.Skipped.ShouldHaveSingleItem().Reason.ShouldBe(CleanupSkipReasonDto.ExportActive);
    }

    [Fact]
    public async Task StaleAndExpiredPlans_AreRefused()
    {
        var track = await _library.TrackAsync();
        var plan = await (await PreviewAsync(new CleanupActionDto { Kind = CleanupKindDto.DetailStems })).BodyAsync<CleanupPlanDto>();
        await File.AppendAllTextAsync(_library.PathOf($"tracks/{LibraryFixture.Id(track.Id)}/detail/drums/kick.flac"), "changed", Ct);

        using var stale = await ExecuteAsync(plan.PlanId);
        (await stale.JsonAsync()).GetProperty("type").GetString().ShouldEndWith(LibraryCareProblemTypes.PlanStale);

        var second = await (await PreviewAsync(new CleanupActionDto { Kind = CleanupKindDto.DetailStems })).BodyAsync<CleanupPlanDto>();
        Factory.Clock.Advance(TimeSpan.FromMinutes(11));
        using var expired = await ExecuteAsync(second.PlanId);
        (await expired.JsonAsync()).GetProperty("type").GetString().ShouldEndWith(LibraryCareProblemTypes.PlanExpired);
        (await ExecuteAsync(Guid.NewGuid())).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Cleanup_IsRefusedDuringABackupAndWhileALibraryJobRuns()
    {
        Factory.Services.GetRequiredService<LibraryGate>().TryBeginBackup();
        (await PreviewAsync(new CleanupActionDto { Kind = CleanupKindDto.Leftovers })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        Factory.Services.GetRequiredService<LibraryGate>().EndBackup();
        var release = new TaskCompletionSource();
        var runner = Factory.Services.GetRequiredService<LibraryJobRunner>();
        runner.TryStart(LibraryJobKindDto.Verify, "a.zip", async (_, ct) => { await release.Task.WaitAsync(ct); return new VerifyReportDto { Ok = true, EntriesChecked = 0, BytesChecked = 0, Problem = null }; }, out _);

        using var response = await PreviewAsync(new CleanupActionDto { Kind = CleanupKindDto.Leftovers });
        release.SetResult();
        await runner.WhenIdleAsync();

        (await response.JsonAsync()).GetProperty("type").GetString().ShouldEndWith(LibraryCareProblemTypes.LibraryJobRunning);
    }

    [Fact]
    public async Task InvalidRequests_Are400()
    {
        using var empty = await Client.PostAsJsonAsync(ApiRoutes.CleanupPreview, new CleanupRequestDto { Actions = [] }, Ct);

        empty.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // Security review 013 item 11: plan files do not pile up; each preview prunes the expired ones.
    [Fact]
    public async Task Preview_PrunesExpiredPlans()
    {
        await _library.TrackAsync();
        var first = await (await PreviewAsync(new CleanupActionDto { Kind = CleanupKindDto.DetailStems })).BodyAsync<CleanupPlanDto>();
        var folder = Path.Combine(Factory.DataDir, "library-care", "cleanup");
        await File.WriteAllTextAsync(Path.Combine(folder, Guid.NewGuid().ToString("D") + ".json"), "not a plan", Ct);
        Factory.Clock.Advance(TimeSpan.FromMinutes(11));

        var second = await (await PreviewAsync(new CleanupActionDto { Kind = CleanupKindDto.DetailStems })).BodyAsync<CleanupPlanDto>();

        Directory.GetFiles(folder).Select(Path.GetFileNameWithoutExtension).ShouldBe([second.PlanId.ToString("D")]);
        (await ExecuteAsync(first.PlanId)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    private static WorkerStorage Storage(IReadOnlyList<WorkerCacheEntry> entries) =>
        new(ApiFactory.Start, [new("data", 1000, 5000), new("models", 1000, 5000)], entries, 0, 0, 0, []);

    private Task<HttpResponseMessage> PreviewAsync(params CleanupActionDto[] actions) =>
        Client.PostAsJsonAsync(ApiRoutes.CleanupPreview, new CleanupRequestDto { Actions = actions }, Ct);

    private Task<HttpResponseMessage> ExecuteAsync(Guid planId) =>
        Client.PostAsJsonAsync(ApiRoutes.Cleanup, new ExecuteCleanupRequestDto { PlanId = planId }, Ct);
}
