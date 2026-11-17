using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.LibraryCare;
using StemStudio.Api.LibraryCare.Gate;
using StemStudio.Api.LibraryCare.Jobs;
using StemStudio.Api.LibraryCare.Restore;
using StemStudio.Api.Mixes;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.Mixes;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Mixes;
using StemStudio.Domain.Styles;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence;
using StemStudio.Persistence.Agent;
using StemStudio.TestDoubles.ClaudeKey;

namespace StemStudio.Api.Tests.LibraryCare;

/// <summary>
/// 013 T062: the merge restore (US5, research R-07, data-model 5.7): dependency order, same id and same source key, the
/// "(restored)" renames, one transaction with files copied only for added items, the report, and the 011 rule that the
/// archive's <c>AgentSecrets</c> are never imported.
/// </summary>
public sealed class LibraryMergerTests : IDisposable
{
    private readonly FakeLibraryFiles _files = new();
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;
    private readonly LibraryFixture _library;

    public LibraryMergerTests()
    {
        _factory = new ApiFactory { ConfigureServices = services => services.AddSingleton<ILibraryFiles>(_files) };
        _client = _factory.CreateClient();
        _library = new LibraryFixture(_factory);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private LibraryJobRunner Runner => _factory.Services.GetRequiredService<LibraryJobRunner>();

    [Fact]
    public async Task Merge_AddsWhatIsMissing_KeepsWhatExists_AndCopiesTheFilesOfAddedItems()
    {
        var kept = await _library.TrackAsync("aaaaaaaaaaa");
        var lost = await _library.TrackAsync("bbbbbbbbbbb");
        var mix = await MixAsync(lost, "Practice");
        var asset = await _library.ExportAsync(lost);
        var generation = await _library.GenerationAsync(lost);
        var sheet = LyricsSheet.Create(Guid.NewGuid(), new LyricsContent("Sheet", "en", "la la"), LibraryFixture.T0);
        await _library.AddAsync(sheet);
        var conversation = await ConversationAsync();
        var before = _library.LibraryFiles();
        var archive = await RestoreServiceTests.BackupAsync(_client, _factory);
        (await _client.DeleteAsync(ApiRoutes.Track(lost.Id), Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await RemoveAsync(context => context.Assets, asset.Id, $"assets/{LibraryFixture.Id(asset.Id)}");
        await RemoveAsync(context => context.Generations, generation.Id, $"generations/{LibraryFixture.Id(generation.Id)}");
        await RemoveAsync(context => context.LyricsSheets, sheet.Id, null);
        await RemoveAsync(context => context.AgentConversations, conversation.Id, null);
        var added = await _library.TrackAsync("ccccccccccc", detail: false);

        using var response = await RestoreAsync(archive, Merge());
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await ContractAssert.MatchesAsync(response, "restoreBackup");
        var report = await ReportAsync(response);

        (report.Mode, report.PreviousLibraryKept, report.MigratedFrom).ShouldBe((RestoreReportModeDto.Merge, false, null));
        (await _factory.FindTrackAsync(lost.Id)).ShouldNotBeNull();
        (await _factory.FindTrackAsync(kept.Id)).ShouldNotBeNull();
        (await _factory.FindTrackAsync(added.Id)).ShouldNotBeNull();
        (await CountAsync(context => context.Mixes.Where(m => m.Id == mix.Id))).ShouldBe(1);
        (await CountAsync(context => context.Assets.Where(a => a.Id == asset.Id))).ShouldBe(1);
        (await CountAsync(context => context.Generations.Where(g => g.Id == generation.Id))).ShouldBe(1);
        (await CountAsync(context => context.LyricsSheets.Where(s => s.Id == sheet.Id))).ShouldBe(1);
        (await CountAsync(context => context.AgentConversations.Where(c => c.Id == conversation.Id))).ShouldBe(1);
        (await CountAsync(context => context.AgentMessages.Where(m => m.ConversationId == conversation.Id))).ShouldBe(1);
        (await CountAsync(context => context.AgentRuns)).ShouldBe(1);
        (await CountAsync(context => context.AgentRunSteps)).ShouldBe(1);
        (await CountAsync(context => context.AgentApprovals)).ShouldBe(1);
        (await CountAsync(context => context.AgentUndoEntries)).ShouldBe(1);
        (await CountAsync(context => context.AgentRunEvents)).ShouldBe(1);
        var after = _library.LibraryFiles();
        before.ShouldAllBe(file => after.ContainsKey(file.Key) && after[file.Key] == file.Value);
        report.Skipped.ShouldContain(item => item.Id == LibraryFixture.Id(kept.Id) && item.Reason == SkippedItemReasonDto.SameId && item.Category == "tracks");
        report.Skipped.ShouldNotContain(item => item.Id == LibraryFixture.Id(lost.Id));
        report.Restored.Single(count => count.Category == BackupCategoryDto.Stems).Items.ShouldBe(1);
        (await _client.GetAsync(ApiRoutes.PreviousLibrary, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        _factory.Services.GetRequiredService<LibraryGate>().Mode.ShouldBe(LibraryGateMode.Open);
    }

    [Fact]
    public async Task SameSourceKeyWithAnotherId_IsSkippedWithItsDependants()
    {
        var original = await _library.TrackAsync("ddddddddddd");
        var mix = await MixAsync(original, "Practice");
        var asset = await _library.ExportAsync(original);
        var generation = await _library.GenerationAsync(original);
        var conversion = await _library.ConversionAsync(generation, await _library.VoiceProfileAsync());
        var archive = await RestoreServiceTests.BackupAsync(_client, _factory);
        (await _client.DeleteAsync(ApiRoutes.Track(original.Id), Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await RemoveAsync(context => context.Assets, asset.Id, $"assets/{LibraryFixture.Id(asset.Id)}");
        await RemoveAsync(context => context.Generations, generation.Id, $"generations/{LibraryFixture.Id(generation.Id)}");
        await RemoveAsync(context => context.Generations, conversion.Id, null);
        var again = await _library.TrackAsync("ddddddddddd", detail: false);
        var files = _library.LibraryFiles();

        var report = await ReportAsync(await RestoreAsync(archive, Merge()));

        (await _factory.FindTrackAsync(original.Id)).ShouldBeNull();
        (await _factory.FindTrackAsync(again.Id)).ShouldNotBeNull();
        (await CountAsync(context => context.Mixes)).ShouldBe(0);
        (await CountAsync(context => context.Assets)).ShouldBe(0);
        (await CountAsync(context => context.Generations)).ShouldBe(0);
        _library.LibraryFiles().ShouldBe(files);
        Reason(report, original.Id).ShouldBe((SkippedItemReasonDto.SameSource, original.Title));
        Reason(report, mix.Id).ShouldBe((SkippedItemReasonDto.DependencySkipped, "Practice"));
        Reason(report, asset.Id).Reason.ShouldBe(SkippedItemReasonDto.DependencySkipped);
        Reason(report, generation.Id).Reason.ShouldBe(SkippedItemReasonDto.DependencySkipped);
        Reason(report, conversion.Id).Reason.ShouldBe(SkippedItemReasonDto.DependencySkipped);
    }

    [Fact]
    public async Task UniqueNameClashes_AreRenamedRestored_AndTheLiveItemsKeepTheirIdsAndNames()
    {
        var track = await _library.TrackAsync();
        var mix = await MixAsync(track, "Practice");
        var style = await StyleAsync("Warm");
        var profile = await _library.VoiceProfileAsync("My voice");
        var archive = await RestoreServiceTests.BackupAsync(_client, _factory);
        await RemoveAsync(context => context.Mixes, mix.Id, null);
        await RemoveAsync(context => context.Styles, style.Id, null);
        await RemoveAsync(context => context.VoiceProfiles, profile.Id, $"voices/{LibraryFixture.Id(profile.Id)}");
        var liveMix = await MixAsync(track, "practice");
        var liveStyle = await StyleAsync("WARM");
        var liveProfile = await _library.VoiceProfileAsync("My voice");

        var report = await ReportAsync(await RestoreAsync(archive, Merge()));

        (await NameAsync(context => context.Mixes, mix.Id, m => m.Name)).ShouldBe("Practice (restored)");
        (await NameAsync(context => context.Mixes, liveMix.Id, m => m.Name)).ShouldBe("practice");
        (await NameAsync(context => context.Styles, style.Id, s => s.Name)).ShouldBe("Warm (restored)");
        (await NameAsync(context => context.Styles, liveStyle.Id, s => s.Name)).ShouldBe("WARM");
        (await NameAsync(context => context.VoiceProfiles, profile.Id, p => p.DisplayName)).ShouldBe("My voice (restored)");
        (await NameAsync(context => context.VoiceProfiles, liveProfile.Id, p => p.DisplayName)).ShouldBe("My voice");
        File.Exists(_library.PathOf(profile.SamplePath)).ShouldBeTrue();
        report.Skipped.Single(item => item.Id == LibraryFixture.Id(style.Id)).ShouldBe(new SkippedItemDto
        {
            Category = "styles",
            Id = LibraryFixture.Id(style.Id),
            Name = "Warm",
            Reason = SkippedItemReasonDto.SameId,
            RenamedTo = "Warm (restored)",
        });
        report.Skipped.Single(item => item.Id == LibraryFixture.Id(mix.Id)).RenamedTo.ShouldBe("Practice (restored)");
        report.Skipped.Single(item => item.Id == LibraryFixture.Id(profile.Id)).RenamedTo.ShouldBe("My voice (restored)");
    }

    [Fact]
    public async Task ArchiveOfTheCurrentLibrary_AddsNothing_AndReportsEveryItemAsSameId()
    {
        var track = await _library.TrackAsync();
        var mix = await MixAsync(track, "Practice");
        var style = await StyleAsync("Warm");
        var sheet = LyricsSheet.Create(Guid.NewGuid(), new LyricsContent("Sheet", "en", "la la"), LibraryFixture.T0);
        await _library.AddAsync(sheet);
        var conversation = await ConversationAsync();
        var archive = await RestoreServiceTests.BackupAsync(_client, _factory);
        var files = _library.LibraryFiles();

        var report = await ReportAsync(await RestoreAsync(archive, Merge()));

        report.Skipped.Select(item => (item.Category, item.Id, item.Reason)).ShouldBe(
        [
            ("styles", LibraryFixture.Id(style.Id), SkippedItemReasonDto.SameId),
            ("lyricsSheets", LibraryFixture.Id(sheet.Id), SkippedItemReasonDto.SameId),
            ("tracks", LibraryFixture.Id(track.Id), SkippedItemReasonDto.SameId),
            ("mixes", LibraryFixture.Id(mix.Id), SkippedItemReasonDto.SameId),
            ("agentConversations", LibraryFixture.Id(conversation.Id), SkippedItemReasonDto.SameId),
        ]);
        report.Restored.ShouldAllBe(count => count.Category == BackupCategoryDto.Database);
        _library.LibraryFiles().ShouldBe(files);
        (await CountAsync(context => context.Tracks)).ShouldBe(1);
    }

    [Fact]
    public async Task ConsentAndTheVoiceProfileOption_AreAppliedBeforeTheMerge()
    {
        await _library.TrackAsync();
        var profile = await _library.VoiceProfileAsync("My voice");
        var archive = await RestoreServiceTests.BackupAsync(_client, _factory);
        await RemoveAsync(context => context.VoiceProfiles, profile.Id, $"voices/{LibraryFixture.Id(profile.Id)}");

        var report = await ReportAsync(await RestoreAsync(archive, Merge() with { VoiceProfiles = false }));

        (await CountAsync(context => context.VoiceProfiles)).ShouldBe(0);
        Directory.Exists(_library.PathOf($"voices/{LibraryFixture.Id(profile.Id)}")).ShouldBeFalse();
        Reason(report, profile.Id).Reason.ShouldBe(SkippedItemReasonDto.Option);
    }

    [Fact]
    public async Task AgentSecretsOfTheArchive_AreNeverImported()
    {
        await _library.TrackAsync();
        await PutKeyAsync(ClaudeKeySentinels.Stored);
        var archive = await RestoreServiceTests.BackupAsync(_client, _factory);
        (await _client.DeleteAsync(ApiRoutes.AgentClaudeKey, Ct)).IsSuccessStatusCode.ShouldBeTrue();

        await ReportAsync(await RestoreAsync(archive, Merge()));

        (await CountAsync(context => context.AgentSecrets)).ShouldBe(0);
    }

    [Fact]
    public async Task TheLiveSecretIsNeverChangedByAMerge()
    {
        await _library.TrackAsync();
        await PutKeyAsync(ClaudeKeySentinels.Stored);
        var archive = await RestoreServiceTests.BackupAsync(_client, _factory);
        await PutKeyAsync(ClaudeKeySentinels.Replacement);
        var live = await SecretsAsync();

        var report = await ReportAsync(await RestoreAsync(archive, Merge()));

        (await SecretsAsync()).ShouldBe(live);
        report.Warnings.ShouldNotContain("The stored Claude key must be entered again.");
    }

    [Fact]
    public async Task AFailureWhileCopyingFiles_LeavesNothingOfTheMerge()
    {
        await _library.TrackAsync("aaaaaaaaaaa");
        var lost = await _library.TrackAsync("bbbbbbbbbbb");
        var asset = await _library.ExportAsync(lost);
        var pending = await _library.PendingUploadAsync();
        var archive = await RestoreServiceTests.BackupAsync(_client, _factory);
        (await _client.DeleteAsync(ApiRoutes.Track(lost.Id), Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await RemoveAsync(context => context.Assets, asset.Id, $"assets/{LibraryFixture.Id(asset.Id)}");
        await RemoveAsync(context => context.Tracks, pending.Id, null);
        File.Delete(_library.PathOf("uploads/" + pending.UploadFileName));
        var files = _library.LibraryFiles();
        _files.FailCopy = path => path.Contains("assets", StringComparison.Ordinal);

        var job = await FailedAsync(await RestoreAsync(archive, Merge()));

        job.Error!.Code.ShouldBe(LibraryJobErrorCodeDto.IoError);
        (await _factory.FindTrackAsync(lost.Id)).ShouldBeNull();
        (await _factory.FindTrackAsync(pending.Id)).ShouldBeNull();
        (await CountAsync(context => context.Assets)).ShouldBe(0);
        _library.LibraryFiles().ShouldBe(files);
        Directory.Exists(_library.PathOf($"tracks/{LibraryFixture.Id(lost.Id)}")).ShouldBeFalse();
        _factory.Services.GetRequiredService<LibraryGate>().Mode.ShouldBe(LibraryGateMode.Open);
    }

    [Fact]
    public async Task PendingUploads_AreCopied_AndAnOrphanFolderOfAnAddedTrackIsCompleted()
    {
        var lost = await _library.TrackAsync();
        var pending = await _library.PendingUploadAsync();
        var archive = await RestoreServiceTests.BackupAsync(_client, _factory);
        var files = _library.LibraryFiles();
        await RemoveAsync(context => context.Tracks, lost.Id, null);
        await RemoveAsync(context => context.Tracks, pending.Id, null);
        File.Delete(_library.PathOf("uploads/" + pending.UploadFileName));
        File.Delete(_library.PathOf($"tracks/{LibraryFixture.Id(lost.Id)}/stems/vocals.flac"));
        await File.WriteAllTextAsync(_library.PathOf($"tracks/{LibraryFixture.Id(lost.Id)}/stems/drums.flac"), "stale", Ct);

        await ReportAsync(await RestoreAsync(archive, Merge()));

        (await _factory.FindTrackAsync(lost.Id)).ShouldNotBeNull();
        (await _factory.FindTrackAsync(pending.Id))!.Status.ShouldBe(TrackStatus.Failed);
        _library.LibraryFiles().ShouldBe(files);
    }

    [Fact]
    public async Task AnUnexpectedFailure_LeavesNothingOfTheMerge()
    {
        var lost = await _library.TrackAsync();
        var archive = await RestoreServiceTests.BackupAsync(_client, _factory);
        (await _client.DeleteAsync(ApiRoutes.Track(lost.Id), Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var files = _library.LibraryFiles();
        (_files.FailCopy, _files.CopyError) = (path => path.EndsWith("vocals.flac", StringComparison.Ordinal), () => new InvalidOperationException("bug"));

        var job = await FailedAsync(await RestoreAsync(archive, Merge()));

        (job.Error!.Code, job.Error.Message).ShouldBe((LibraryJobErrorCodeDto.IoError, LibraryJobs.UnexpectedText));
        (await _factory.FindTrackAsync(lost.Id)).ShouldBeNull();
        _library.LibraryFiles().ShouldBe(files);
    }

    [Fact]
    public async Task AUniqueKeyTakenWhileTheMergeCopies_FailsTheCommitAndLeavesNothing()
    {
        var lost = await _library.TrackAsync("eeeeeeeeeee");
        var archive = await RestoreServiceTests.BackupAsync(_client, _factory);
        (await _client.DeleteAsync(ApiRoutes.Track(lost.Id), Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var files = _library.LibraryFiles();
        var rival = SyncTestData.CompletedTrack("eeeeeeeeeee");
        var seeded = 0;
        _files.BeforeCopy = _ =>
        {
            if (Interlocked.Exchange(ref seeded, 1) == 0)
            {
                _factory.SeedAsync(rival).GetAwaiter().GetResult();
            }
        };

        var job = await FailedAsync(await RestoreAsync(archive, Merge()));

        (job.Error!.Code, job.Error.Message).ShouldBe((LibraryJobErrorCodeDto.IoError, LibraryMerger.FailedText));
        (await _factory.FindTrackAsync(lost.Id)).ShouldBeNull();
        (await _factory.AllTracksAsync()).Single().Id.ShouldBe(rival.Id);
        _library.LibraryFiles().ShouldBe(files);
        _factory.Services.GetRequiredService<LibraryGate>().Mode.ShouldBe(LibraryGateMode.Open);
    }

    [Fact]
    public async Task Merge_NeedsNoConfirmation_AndIsNotBlockedByAPreviousLibrary()
    {
        var track = await _library.TrackAsync();
        var archive = await RestoreServiceTests.BackupAsync(_client, _factory);
        await ReportAsync(await RestoreAsync(archive, new RestoreOptionsDto { Mode = RestoreModeDto.Replace, Confirmation = "REPLACE" }));
        (await _client.DeleteAsync(ApiRoutes.Track(track.Id), Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var report = await ReportAsync(await RestoreAsync(archive, Merge()));

        report.Mode.ShouldBe(RestoreReportModeDto.Merge);
        (await _factory.FindTrackAsync(track.Id)).ShouldNotBeNull();
        (await _client.GetAsync(ApiRoutes.PreviousLibrary, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private static RestoreOptionsDto Merge() => new() { Mode = RestoreModeDto.Merge };

    private static (SkippedItemReasonDto Reason, string? Name) Reason(RestoreReportDto report, Guid id)
    {
        var item = report.Skipped.Single(skipped => skipped.Id == LibraryFixture.Id(id));
        return (item.Reason, item.Name);
    }

    private Task<HttpResponseMessage> RestoreAsync(string name, RestoreOptionsDto options) =>
        _client.PostAsJsonAsync(ApiRoutes.RestoreBackup(name), options, Ct);

    private async Task<RestoreReportDto> ReportAsync(HttpResponseMessage accepted)
    {
        using (accepted)
        {
            accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            await Runner.WhenIdleAsync();
            var job = (await Runner.GetAsync((await accepted.BodyAsync<LibraryJobDto>()).JobId, Ct))!;
            job.Status.ShouldBe(LibraryJobStatusDto.Completed, job.Error?.Message);
            return job.Report.ShouldBeOfType<RestoreReportDto>();
        }
    }

    private async Task<LibraryJobDto> FailedAsync(HttpResponseMessage accepted)
    {
        using (accepted)
        {
            await Runner.WhenIdleAsync();
            var job = (await Runner.GetAsync((await accepted.BodyAsync<LibraryJobDto>()).JobId, Ct))!;
            job.Status.ShouldBe(LibraryJobStatusDto.Failed);
            return job;
        }
    }

    private async Task<Mix> MixAsync(Track track, string name)
    {
        var mix = Mix.Create(Guid.NewGuid(), track, MixMapper.ToDomain(MixEndpointsTests.Settings(name)), LibraryFixture.T0);
        await _library.AddAsync(mix);
        return mix;
    }

    private async Task<Style> StyleAsync(string name)
    {
        var style = Style.CreateUser(Guid.NewGuid(), new StyleFields
        {
            Name = name,
            GenreFamily = GenreFamily.Jazz,
            TempoFeel = TempoFeel.Laidback,
            VocalHandling = VocalHandling.Instrumental,
            DefaultStrength = Strength.Light,
        }, LibraryFixture.T0);
        await _library.AddAsync(style);
        return style;
    }

    private async Task<ConversationEntity> ConversationAsync()
    {
        var conversation = new ConversationEntity { Id = Guid.NewGuid(), Title = "Chat", CreatedAt = LibraryFixture.T0, UpdatedAt = LibraryFixture.T0 };
        await _library.AddAsync(conversation);
        await _library.AddAsync(new ConversationMessageEntity
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            Seq = 1,
            Role = "user",
            Text = "Hello",
            Page = "library",
            CreatedAt = LibraryFixture.T0,
        });
        var run = new AgentRunEntity { Id = Guid.NewGuid(), ConversationId = conversation.Id, Status = "Completed", StartedAt = LibraryFixture.T0 };
        await _library.AddAsync(run);
        var step = new RunStepEntity { Id = Guid.NewGuid(), RunId = run.Id, Seq = 1, Tool = "list_tracks", Status = "done" };
        await _library.AddAsync(step);
        await _library.AddAsync(new ApprovalEntity { Id = Guid.NewGuid(), RunId = run.Id, Status = "approved", RequestedAt = LibraryFixture.T0 });
        await _library.AddAsync(new UndoEntryEntity { Id = Guid.NewGuid(), ConversationId = conversation.Id, RunId = run.Id, StepId = step.Id, Ordinal = 1 });
        await _library.AddAsync(new RunEventEntity { RunId = run.Id, Seq = 1, Type = "run-started" });
        return conversation;
    }

    private async Task PutKeyAsync(string key)
    {
        using var stored = await _client.PutAsJsonAsync(ApiRoutes.AgentClaudeKey, new { apiKey = key }, Ct);
        stored.IsSuccessStatusCode.ShouldBeTrue();
    }

    private Task<List<string>> SecretsAsync() => _factory.WithScopeAsync(sp =>
        sp.GetRequiredService<StemStudioDbContext>().AgentSecrets.AsNoTracking().Select(secret => secret.Name + ":" + secret.Ciphertext).ToListAsync(Ct));

    private Task<int> CountAsync<T>(Func<StemStudioDbContext, IQueryable<T>> query) =>
        _factory.WithScopeAsync(sp => query(sp.GetRequiredService<StemStudioDbContext>()).CountAsync(Ct));

    private Task<string> NameAsync<T>(Func<StemStudioDbContext, DbSet<T>> set, Guid id, Func<T, string> name)
        where T : class => _factory.WithScopeAsync(async sp => name((await set(sp.GetRequiredService<StemStudioDbContext>()).FindAsync([id], Ct))!));

    private async Task RemoveAsync<T>(Func<StemStudioDbContext, DbSet<T>> set, Guid id, string? folder)
        where T : class
    {
        await _factory.WithScopeAsync(async sp =>
        {
            var context = sp.GetRequiredService<StemStudioDbContext>();
            set(context).Remove((await set(context).FindAsync([id], Ct))!);
            await context.SaveChangesAsync(Ct);
            return true;
        });
        if (folder is not null)
        {
            Directory.Delete(_library.PathOf(folder), recursive: true);
        }
    }
}
