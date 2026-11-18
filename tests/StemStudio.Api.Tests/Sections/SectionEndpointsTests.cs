using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.SongSections;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Tests.Worker;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Sections;
using StemStudio.Domain.Sections;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Tests.SongSections;

/// <summary>Feature 014 T054: reading and detecting sections, the detection trigger and the job applier (FR-001, FR-007, FR-008).</summary>
public sealed class SectionEndpointsTests : ApiTestBase
{
    private FakeWorkerPracticeClient Practice => Factory.PracticeWorker;

    [Fact]
    public async Task Get_UnknownTrack_Is404()
    {
        using var response = await Client.GetAsync(new Uri(ApiRoutes.Sections(Guid.NewGuid()), UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_NeverDetected_IsStatusNone()
    {
        var track = await Factory.SeedAsync(SectionTestData.Completed());

        using var response = await Client.GetAsync(new Uri(ApiRoutes.Sections(track.Id), UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ContractAssert.MatchesAsync(response, "getSections");
        var set = await response.BodyAsync<SectionSetDto>();
        (set.TrackId, set.Status, set.Revision, set.UpdatedAt, set.Sections.Count).ShouldBe((track.Id, SectionSetStatusDto.None, 0, (DateTimeOffset?)null, 0));
    }

    [Fact]
    public async Task Get_Ready_ReturnsTheSectionsWithBarNumbers()
    {
        var track = await Factory.SeedAsync(SectionTestData.Completed());
        await Factory.SeedDetectedAsync(track.Id);

        using var response = await Client.GetAsync(new Uri(ApiRoutes.Sections(track.Id), UriKind.Relative), Ct);

        await ContractAssert.MatchesAsync(response, "getSections");
        var set = await response.BodyAsync<SectionSetDto>();
        (set.Status, set.Origin, set.Revision, set.Analyzer, set.Snap).ShouldBe((SectionSetStatusDto.Ready, SectionSetOriginDto.Detected, 1, "stems-novelty/1.0.0", (SectionSnapDto?)SectionSnapDto.Downbeat));
        set.Sections.Select(section => section.Kind).ShouldBe([SectionKindDto.Intro, SectionKindDto.Verse, SectionKindDto.Chorus, SectionKindDto.Solo, SectionKindDto.Outro]);
        set.Sections[3].Instrument.ShouldBe("guitar.electric-guitar");
        set.Sections[0].StartBar.ShouldBe(0);
        set.Sections[1].StartBar.ShouldBe(1);
    }

    [Fact]
    public async Task Detect_Queues_AndSendsTheStemsGridAndLength()
    {
        var track = await Factory.SeedAsync(SectionTestData.Completed());

        using var response = await DetectAsync(track.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await ContractAssert.MatchesAsync(response, "detectSections");
        var set = await response.BodyAsync<SectionSetDto>();
        set.Status.ShouldBe(SectionSetStatusDto.Queued);
        var sent = Practice.SectionRequests.Single();
        sent.Stems.Keys.ShouldBe(StemCatalog.SixStem, ignoreOrder: true);
        sent.Stems["bass"].ShouldBe($"tracks/{track.Id:D}/stems/bass.flac");
        (sent.DurationSeconds, sent.Grid!.Bpm, sent.Details.Count).ShouldBe((213.04, 113.5, 0));
    }

    [Fact]
    public async Task Detect_SendsTheKeptDetailStems()
    {
        var track = await Factory.SeedAsync(SyncTestData.DetailCompleted());

        using var response = await DetectAsync(track.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var details = Practice.SectionRequests.Single().Details;
        details.ShouldNotBeEmpty();
        details.ShouldAllBe(detail => detail.Path == $"tracks/{track.Id:D}/detail/{detail.Channel.Replace('.', '/')}.flac");
    }

    [Fact]
    public async Task Detect_UnknownTrack_Is404()
    {
        using var response = await DetectAsync(Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Detect_TrackNotCompleted_Is409TrackNotReady()
    {
        var track = await Factory.SeedAsync(SyncTestData.UrlTrack());

        using var response = await DetectAsync(track.Id);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "track-not-ready");
        Practice.SectionRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Detect_AfterEdits_IsRefusedWithoutOverwrite_AndAllowedWithIt()
    {
        var track = await Factory.SeedAsync(SectionTestData.Completed());
        await Factory.SeedDetectedAsync(track.Id);
        using (var edit = await Client.PutAsJsonAsync(ApiRoutes.Sections(track.Id), new SectionsUpdateRequest
        {
            Revision = 1,
            Sections = [new SectionInputDto { Kind = SectionKindDto.Verse, Label = "All", StartSeconds = 0, EndSeconds = 64 }],
        }, Ct))
        {
            edit.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var refused = await DetectAsync(track.Id);
        using var allowed = await DetectAsync(track.Id, overwrite: true);

        await AssertProblemAsync(refused, HttpStatusCode.Conflict, "sections-edited");
        allowed.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Detect_WhileRunning_Is409DetectionRunning()
    {
        var track = await Factory.SeedAsync(SectionTestData.Completed());
        using (var first = await DetectAsync(track.Id))
        {
            first.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        }

        using var second = await DetectAsync(track.Id, overwrite: true);

        await AssertProblemAsync(second, HttpStatusCode.Conflict, "detection-running");
        Practice.SectionRequests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Detect_ConcurrentRequests_SubmitOnlyOnce()
    {
        var track = await Factory.SeedAsync(SectionTestData.Completed());

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => DetectAsync(track.Id)));

        responses.Count(response => response.StatusCode == HttpStatusCode.Accepted).ShouldBe(1);
        responses.Count(response => response.StatusCode == HttpStatusCode.Conflict).ShouldBe(3);
        Practice.SectionRequests.Count.ShouldBe(1);
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task Detect_WorkerDown_Is503_AndNothingIsQueued()
    {
        var track = await Factory.SeedAsync(SectionTestData.Completed());
        Practice.Unavailable = true;

        using var response = await DetectAsync(track.Id);

        await AssertProblemAsync(response, HttpStatusCode.ServiceUnavailable, "worker-unavailable");
        (await Factory.FindSectionsAsync(track.Id)).ShouldBeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "Not Found")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "The beat grid is invalid.")]
    public async Task Detect_WorkerRejectsTheJob_StoresFailedWithAReadableReason_AndARetryQueues(HttpStatusCode status, string detail)
    {
        var track = await Factory.SeedAsync(SectionTestData.Completed());
        Practice.SubmitFailure = new WorkerRequestException(status, detail);

        using var rejected = await DetectAsync(track.Id);

        rejected.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await ContractAssert.MatchesAsync(rejected, "detectSections");
        var failed = await rejected.BodyAsync<SectionSetDto>();
        (failed.Status, failed.Error).ShouldBe((SectionSetStatusDto.Failed, SectionService.SubmitRejected + " " + detail));
        (await Factory.FindSectionsAsync(track.Id))!.Status.ShouldBe(SectionSetStatus.Failed);

        Practice.SubmitFailure = null;
        using var retry = await DetectAsync(track.Id);

        var queued = await retry.BodyAsync<SectionSetDto>();
        (queued.Status, queued.Error).ShouldBe((SectionSetStatusDto.Queued, (string?)null));
    }

    [Fact]
    public async Task CompletedSeparation_WithTheWorkerRejectingTheJob_StoresFailed_AndDoesNotTryAgainByItself()
    {
        var track = await Factory.SeedAsync(SyncTestData.UrlTrack());
        Factory.CompleteSeparations();
        Practice.SubmitFailure = new WorkerRequestException(HttpStatusCode.NotFound, "Not Found");

        await Factory.SyncTracksAsync();

        (await Factory.FindTrackAsync(track.Id))!.Status.ShouldBe(TrackStatus.Completed);
        var set = (await Factory.FindSectionsAsync(track.Id))!;
        (set.Status, set.Error).ShouldBe((SectionSetStatus.Failed, SectionService.SubmitRejected + " Not Found"));
        var stored = await Factory.FindTrackAsync(track.Id);
        Practice.SubmitFailure = null;
        (await Factory.WithScopeAsync(sp => sp.GetRequiredService<SectionService>().DetectAutomaticallyAsync(stored!, CancellationToken.None))).ShouldBeFalse();
        Practice.SectionRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Detect_WithoutABody_DoesNotOverwrite()
    {
        var track = await Factory.SeedAsync(SectionTestData.Completed());

        using var response = await Client.PostAsync(new Uri(ApiRoutes.SectionsDetect(track.Id), UriKind.Relative), null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task CompletedSeparation_StartsDetection()
    {
        var track = await Factory.SeedAsync(SyncTestData.UrlTrack());
        Factory.CompleteSeparations();

        await Factory.SyncTracksAsync();

        var set = (await Factory.FindSectionsAsync(track.Id))!;
        (set.Status, set.WorkerJobId).ShouldBe((SectionSetStatus.Queued, Practice.SectionJobs.Keys.Single()));
        Practice.SectionRequests.Single().Stems.Count.ShouldBe(6);
    }

    [Fact]
    public async Task CompletedSeparation_WithTheTriggerOff_DoesNotDetect()
    {
        using var factory = new ApiFactory { Settings = new Dictionary<string, string?> { ["StemStudio:Practice:SectionDetectOnComplete"] = "false" } };
        var track = await factory.SeedAsync(SyncTestData.UrlTrack());
        factory.CompleteSeparations();

        await factory.SyncTracksAsync();

        (await factory.FindTrackAsync(track.Id))!.Status.ShouldBe(TrackStatus.Completed);
        (await factory.FindSectionsAsync(track.Id)).ShouldBeNull();
        factory.PracticeWorker.SectionRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task CompletedSeparation_WithTheWorkerLaneDown_LeavesStatusNone_AndTheTrackCompleted()
    {
        var track = await Factory.SeedAsync(SyncTestData.UrlTrack());
        Factory.CompleteSeparations();
        Practice.Unavailable = true;

        await Factory.SyncTracksAsync();

        (await Factory.FindTrackAsync(track.Id))!.Status.ShouldBe(TrackStatus.Completed);
        (await Factory.FindSectionsAsync(track.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task ReSeparation_KeepsTheSections()
    {
        var track = await Factory.SeedAsync(SyncTestData.FailedTrack());
        var stored = await Factory.SeedDetectedAsync(track.Id);
        using (var retry = await Client.PostAsync(new Uri(ApiRoutes.RetryTrack(track.Id), UriKind.Relative), null, Ct))
        {
            retry.IsSuccessStatusCode.ShouldBeTrue();
        }

        Factory.CompleteSeparations();
        await Factory.SyncTracksAsync();

        var set = (await Factory.FindSectionsAsync(track.Id))!;
        (set.Status, set.Revision).ShouldBe((SectionSetStatus.Ready, 1));
        set.Sections.Select(section => section.Id).ShouldBe(stored.Sections.Select(section => section.Id));
        Practice.SectionRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Applier_Completed_StoresNewSections_AndDeletesTheWorkerJob()
    {
        var track = await Factory.SeedAsync(SectionTestData.Completed());
        await Factory.SeedDetectedAsync(track.Id);
        using (var detect = await DetectAsync(track.Id))
        {
            detect.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        }

        var jobId = Practice.SectionJobs.Keys.Single();
        var before = (await Factory.FindSectionsAsync(track.Id))!.Sections.Select(section => section.Id).ToList();
        Practice.SetSection(jobId, WorkerPracticeJobState.Running);
        await Factory.SyncSectionsAsync();
        (await Factory.FindSectionsAsync(track.Id))!.Status.ShouldBe(SectionSetStatus.Detecting);
        Practice.CompleteSection(jobId);

        await Factory.SyncSectionsAsync();

        var set = (await Factory.FindSectionsAsync(track.Id))!;
        (set.Status, set.Revision, set.Origin, set.WorkerJobId).ShouldBe((SectionSetStatus.Ready, 2, SectionSetOrigin.Detected, (string?)null));
        set.Sections.Count.ShouldBe(5);
        set.Sections.ShouldAllBe(section => !before.Contains(section.Id));
        Practice.Deleted.ShouldBe([jobId]);
    }

    [Fact]
    public async Task Applier_Failed_StoresTheReason_AndDeletesTheWorkerJob()
    {
        var track = await Factory.SeedAsync(SectionTestData.Completed());
        using (var detect = await DetectAsync(track.Id))
        {
            detect.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        }

        var jobId = Practice.SectionJobs.Keys.Single();
        Practice.FailSection(jobId);

        await Factory.SyncSectionsAsync();

        using var response = await Client.GetAsync(new Uri(ApiRoutes.Sections(track.Id), UriKind.Relative), Ct);
        await ContractAssert.MatchesAsync(response, "getSections");
        var set = await response.BodyAsync<SectionSetDto>();
        (set.Status, set.Error).ShouldBe((SectionSetStatusDto.Failed, "The stems of this track could not be read."));
        Practice.Deleted.ShouldBe([jobId]);
    }

    [Fact]
    public async Task Applier_LostJob_FailsWithARetryText()
    {
        var track = await Factory.SeedAsync(SectionTestData.Completed());
        using (var detect = await DetectAsync(track.Id))
        {
            detect.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        }

        Practice.SectionJobs.Clear();

        await Factory.SyncSectionsAsync();

        var set = (await Factory.FindSectionsAsync(track.Id))!;
        (set.Status, set.Error).ShouldBe((SectionSetStatus.Failed, StemStudio.Api.SongSections.SectionJobApplier.JobLost));
        Practice.Deleted.ShouldBeEmpty();
    }

    [Fact]
    public async Task Applier_QueuedJob_StaysQueued_AndWorkerDownChangesNothing()
    {
        var track = await Factory.SeedAsync(SectionTestData.Completed());
        using (var detect = await DetectAsync(track.Id))
        {
            detect.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        }

        await Factory.SyncSectionsAsync();
        (await Factory.FindSectionsAsync(track.Id))!.Status.ShouldBe(SectionSetStatus.Queued);
        Practice.Unavailable = true;

        await Factory.SyncSectionsAsync();

        (await Factory.FindSectionsAsync(track.Id))!.Status.ShouldBe(SectionSetStatus.Queued);
    }

    [Fact]
    public async Task FailedDetection_CanBeStartedAgain()
    {
        var track = await Factory.SeedAsync(SectionTestData.Completed());
        using (var detect = await DetectAsync(track.Id))
        {
            detect.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        }

        Practice.FailSection(Practice.SectionJobs.Keys.Single());
        await Factory.SyncSectionsAsync();

        using var again = await DetectAsync(track.Id);

        again.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        Practice.SectionRequests.Count.ShouldBe(2);
    }

    private Task<HttpResponseMessage> DetectAsync(Guid trackId, bool overwrite = false) =>
        Client.PostAsJsonAsync(ApiRoutes.SectionsDetect(trackId), new DetectSectionsRequest { Overwrite = overwrite }, Ct);

    internal static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.ShouldBe(status);
        var json = await response.JsonAsync();
        json.GetProperty("type").GetString().ShouldBe(PracticeFlowProblemTypes.BaseUri + code);
        json.GetProperty("title").GetString().ShouldNotBeNullOrWhiteSpace();
    }
}
