using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.Setlists;
using StemStudio.Api.Tests.SongSections;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Practice;

namespace StemStudio.Api.Tests.Practice;

/// <summary>Feature 014 T096: the local practice log (FR-025..FR-028, SC-009).</summary>
public sealed class PracticeEndpointsTests : ApiTestBase
{
    private static readonly DateTimeOffset Now = ApiFactory.Start;

    private static PracticeSessionInputDto Session(Guid trackId, double minutesAgo = 20, double played = 600) => new()
    {
        TrackId = trackId,
        MixId = null,
        StartedAt = Now.AddMinutes(-minutesAgo),
        EndedAt = Now.AddMinutes(-minutesAgo + 10),
        PlayedSeconds = played,
        LoopPasses = 4,
        MaxSpeedPercent = 90,
        Sections = [],
    };

    private static PracticeSectionInputDto Part(Guid? sectionId, string label, double played, double? trainer = null) =>
        new() { SectionId = sectionId, Kind = PracticeSectionKindDto.Solo, Label = label, PlayedSeconds = played, LoopPasses = 3, TrainerReachedPercent = trainer };

    [Fact]
    public async Task Settings_DefaultOn_AndPutChangesTheGivenFields()
    {
        using var initial = await Client.GetAsync(new Uri(ApiRoutes.PracticeLogSettings, UriKind.Relative), Ct);
        await ContractAssert.MatchesAsync(initial, "getPracticeLogSettings");
        (await initial.BodyAsync<PracticeLogSettingsDto>()).ShouldBe(new PracticeLogSettingsDto { Enabled = true, NoticeAcknowledged = false, Sessions = 0, UpdatedAt = null });

        using var updated = await Client.PutAsJsonAsync(ApiRoutes.PracticeLogSettings, new PracticeLogSettingsUpdateRequest { NoticeAcknowledged = true }, Ct);
        using var empty = await Client.PutAsJsonAsync(ApiRoutes.PracticeLogSettings, new PracticeLogSettingsUpdateRequest(), Ct);

        await ContractAssert.MatchesAsync(updated, "updatePracticeLogSettings");
        var stored = await updated.BodyAsync<PracticeLogSettingsDto>();
        (stored.Enabled, stored.NoticeAcknowledged, stored.UpdatedAt).ShouldBe((true, true, (DateTimeOffset?)Now));
        empty.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Upsert_CreatesAndThenReplacesTheSessionAndItsSections()
    {
        var track = await Factory.SeedAsync(SectionTestData.Completed());
        var mix = await Factory.SeedMixAsync(track);
        var id = Guid.NewGuid();
        var solo = Guid.NewGuid();

        using var created = await PutAsync(id, Session(track.Id) with { MixId = mix.Id, Sections = [Part(solo, "Solo", 300, 80), Part(null, "", 100)] });
        using var replaced = await PutAsync(id, Session(track.Id, played: 590) with { MixId = mix.Id, Sections = [Part(solo, "Solo", 500, 85)] });

        created.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ContractAssert.MatchesAsync(created, "upsertPracticeSession");
        var session = await replaced.BodyAsync<PracticeSessionDto>();
        (session.Id, session.PlayedSeconds, session.MixId, session.Sections.Single().TrainerReachedPercent).ShouldBe((id, 590.0, (Guid?)mix.Id, (double?)85));
        using var settings = await Client.GetAsync(new Uri(ApiRoutes.PracticeLogSettings, UriKind.Relative), Ct);
        (await settings.BodyAsync<PracticeLogSettingsDto>()).Sessions.ShouldBe(1);
    }

    [Fact]
    public async Task Upsert_ConcurrentSavesOfANewSession_StoreItOnce()
    {
        var track = await Factory.SeedAsync(SectionTestData.Completed());
        var id = Guid.NewGuid();

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(i => PutAsync(id, Session(track.Id, played: 100 + i))));

        responses.ShouldAllBe(response => response.StatusCode == HttpStatusCode.OK);
        (await ListAsync(track.Id)).Single().Id.ShouldBe(id);
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task Upsert_WhileTheLogIsOff_Is409AndStoresNothing()
    {
        var track = await Factory.SeedAsync(SectionTestData.Completed());
        (await Client.PutAsJsonAsync(ApiRoutes.PracticeLogSettings, new PracticeLogSettingsUpdateRequest { Enabled = false }, Ct)).Dispose();

        using var response = await PutAsync(Guid.NewGuid(), Session(track.Id));

        await SectionEndpointsTests.AssertProblemAsync(response, HttpStatusCode.Conflict, "practice-log-off");
        await ContractAssert.MatchesAsync(response, "upsertPracticeSession");
        (await ListAsync(null)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Upsert_UnknownTrack_Is404_AndRuleBreaksAre422()
    {
        var track = await Factory.SeedAsync(SectionTestData.Completed());
        var other = await Factory.SeedAsync(SectionTestData.Completed("aaaaaaaaaaa"));
        var foreignMix = await Factory.SeedMixAsync(other);

        using var unknown = await PutAsync(Guid.NewGuid(), Session(Guid.NewGuid()));
        using var future = await PutAsync(Guid.NewGuid(), Session(track.Id, minutesAgo: -10));
        using var tooLong = await PutAsync(Guid.NewGuid(), Session(track.Id, played: 700));
        using var sectionsTooLong = await PutAsync(Guid.NewGuid(), Session(track.Id, played: 100) with { Sections = [Part(null, "A", 60), Part(null, "B", 60)] });
        using var mix = await PutAsync(Guid.NewGuid(), Session(track.Id) with { MixId = foreignMix.Id });

        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await future.FieldErrorsAsync("startedAt")).ShouldNotBeEmpty();
        await ContractAssert.MatchesAsync(future, "upsertPracticeSession");
        (await tooLong.FieldErrorsAsync("playedSeconds")).ShouldNotBeEmpty();
        (await sectionsTooLong.FieldErrorsAsync("sections")).ShouldNotBeEmpty();
        (await mix.FieldErrorsAsync("mixId")).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Upsert_MovingASessionToAnotherTrack_Is422()
    {
        var (first, second) = (await Factory.SeedAsync(SectionTestData.Completed()), await Factory.SeedAsync(SectionTestData.Completed("aaaaaaaaaaa")));
        var id = Guid.NewGuid();
        (await PutAsync(id, Session(first.Id))).Dispose();

        using var moved = await PutAsync(id, Session(second.Id));

        (await moved.FieldErrorsAsync("trackId")).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task List_PagesNewestFirst_PerTrack_AndChecksTheLimit()
    {
        var (first, second) = (await Factory.SeedAsync(SectionTestData.Completed()), await Factory.SeedAsync(SectionTestData.Completed("aaaaaaaaaaa")));
        var (older, newer) = (Guid.NewGuid(), Guid.NewGuid());
        (await PutAsync(older, Session(first.Id, minutesAgo: 120))).Dispose();
        (await PutAsync(newer, Session(first.Id, minutesAgo: 30))).Dispose();
        (await PutAsync(Guid.NewGuid(), Session(second.Id))).Dispose();

        (await ListAsync(first.Id)).Select(session => session.Id).ShouldBe([newer, older]);
        (await ListAsync(first.Id, $"&before={Uri.EscapeDataString(Now.AddMinutes(-60).ToString("O"))}")).Select(session => session.Id).ShouldBe([older]);
        (await ListAsync(null, "?limit=1")).Count.ShouldBe(1);
        using var bad = await Client.GetAsync(new Uri(ApiRoutes.PracticeSessions + "?limit=0", UriKind.Relative), Ct);
        bad.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await ContractAssert.MatchesAsync(bad, "listPracticeSessions");
    }

    [Fact]
    public async Task Delete_OneSession_PerTrack_AndAll()
    {
        var (first, second) = (await Factory.SeedAsync(SectionTestData.Completed()), await Factory.SeedAsync(SectionTestData.Completed("aaaaaaaaaaa")));
        var single = Guid.NewGuid();
        (await PutAsync(single, Session(first.Id, minutesAgo: 90))).Dispose();
        (await PutAsync(Guid.NewGuid(), Session(first.Id))).Dispose();
        (await PutAsync(Guid.NewGuid(), Session(second.Id))).Dispose();

        using var one = await Client.DeleteAsync(new Uri(ApiRoutes.PracticeSession(single), UriKind.Relative), Ct);
        using var again = await Client.DeleteAsync(new Uri(ApiRoutes.PracticeSession(single), UriKind.Relative), Ct);
        using var perTrack = await Client.DeleteAsync(new Uri(ApiRoutes.PracticeSessionsOf(first.Id), UriKind.Relative), Ct);
        using var all = await Client.DeleteAsync(new Uri(ApiRoutes.PracticeSessions, UriKind.Relative), Ct);

        (one.StatusCode, again.StatusCode).ShouldBe((HttpStatusCode.NoContent, HttpStatusCode.NotFound));
        await ContractAssert.MatchesAsync(perTrack, "deletePracticeSessions");
        (await perTrack.BodyAsync<DeletedCountDto>()).Deleted.ShouldBe(1);
        (await all.BodyAsync<DeletedCountDto>()).Deleted.ShouldBe(1);
    }

    [Fact]
    public async Task Summary_TotalsDaysSectionsAndTempo_WithTheOffset()
    {
        var track = await Factory.SeedAsync(SectionTestData.Completed());
        var set = await Factory.SeedDetectedAsync(track.Id);
        var solo = set.Sections[3];
        (await PutAsync(Guid.NewGuid(), Session(track.Id, minutesAgo: 60 * 24, played: 300) with { Sections = [Part(solo.Id, "Old label", 200, 80)] })).Dispose();
        (await PutAsync(Guid.NewGuid(), Session(track.Id, minutesAgo: 30, played: 600) with { Sections = [Part(solo.Id, "Old label", 400, 90)] })).Dispose();

        using var response = await Client.GetAsync(new Uri(ApiRoutes.PracticeSummaryFor(track.Id, 7, 60), UriKind.Relative), Ct);

        await ContractAssert.MatchesAsync(response, "getPracticeSummary");
        var summary = await response.BodyAsync<PracticeSummaryDto>();
        (summary.TodaySeconds, summary.Last7DaysSeconds, summary.TotalSeconds, summary.Sessions).ShouldBe((600.0, 900.0, 900.0, 2));
        summary.Days.Count.ShouldBe(7);
        summary.Days[^1].Date.ShouldBe(new DateOnly(2026, 11, 14));
        var section = summary.Sections.Single();
        (section.SectionId, section.Label, section.Kind, section.Seconds, section.BestTrainerPercent).ShouldBe(((Guid?)solo.Id, solo.Label, "solo", 600.0, (double?)90));
        summary.Tempo.Select(point => point.Percent).ShouldBe([80, 90]);
    }

    [Fact]
    public async Task Summary_WithoutSessions_IsZeros_AndBadQueriesAre422Or404()
    {
        var track = await Factory.SeedAsync(SectionTestData.Completed());

        using var zeros = await Client.GetAsync(new Uri(ApiRoutes.PracticeSummaryFor(track.Id, 30, 0), UriKind.Relative), Ct);
        using var unknown = await Client.GetAsync(new Uri(ApiRoutes.PracticeSummaryFor(Guid.NewGuid(), 30, 0), UriKind.Relative), Ct);
        using var days = await Client.GetAsync(new Uri(ApiRoutes.PracticeSummaryFor(track.Id, 366, 0), UriKind.Relative), Ct);
        using var offset = await Client.GetAsync(new Uri(ApiRoutes.PracticeSummaryFor(track.Id, 30, 900), UriKind.Relative), Ct);
        using var noTrack = await Client.GetAsync(new Uri(ApiRoutes.PracticeSummary, UriKind.Relative), Ct);

        var summary = await zeros.BodyAsync<PracticeSummaryDto>();
        (summary.TotalSeconds, summary.Sessions, summary.LastPractisedAt, summary.Days.Count).ShouldBe((0.0, 0, (DateTimeOffset?)null, 30));
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await days.FieldErrorsAsync("days")).ShouldNotBeEmpty();
        (await offset.FieldErrorsAsync("utcOffsetMinutes")).ShouldNotBeEmpty();
        (await noTrack.FieldErrorsAsync("trackId")).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Overview_ListsTracksByLastPractice()
    {
        var (first, second) = (await Factory.SeedAsync(SectionTestData.Completed()), await Factory.SeedAsync(SectionTestData.Completed("aaaaaaaaaaa")));
        (await PutAsync(Guid.NewGuid(), Session(first.Id, minutesAgo: 300))).Dispose();
        (await PutAsync(Guid.NewGuid(), Session(second.Id, minutesAgo: 30, played: 120))).Dispose();

        using var response = await Client.GetAsync(new Uri(ApiRoutes.PracticeOverviewFor(60), UriKind.Relative), Ct);
        using var bad = await Client.GetAsync(new Uri(ApiRoutes.PracticeOverviewFor(-841), UriKind.Relative), Ct);

        await ContractAssert.MatchesAsync(response, "getPracticeOverview");
        var overview = await response.BodyAsync<List<PracticeTrackTotalDto>>();
        overview.Select(total => (total.TrackId, total.TotalSeconds)).ShouldBe([(second.Id, 120.0), (first.Id, 600.0)]);
        overview[0].Title.ShouldBe(second.Title);
        bad.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task TrackDelete_DeletesItsPracticeRecords()
    {
        var track = await Factory.SeedAsync(SectionTestData.Completed());
        (await PutAsync(Guid.NewGuid(), Session(track.Id))).Dispose();

        (await Client.DeleteAsync(new Uri(ApiRoutes.Track(track.Id), UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await ListAsync(null)).ShouldBeEmpty();
    }

    [Fact]
    public async Task NoPracticeEndpoint_CallsAnotherHost()
    {
        var spy = new OutgoingSpy();
        using var factory = new ApiFactory { ConfigureServices = services => services.ConfigureHttpClientDefaults(builder => builder.AddHttpMessageHandler(() => spy)) };
        using var client = factory.CreateClient();
        var track = await factory.SeedAsync(SectionTestData.Completed());
        var id = Guid.NewGuid();
        var workerCalls = factory.Worker.CreateCalls;

        foreach (var request in Requests(track.Id, id))
        {
            using var response = await client.SendAsync(request, Ct);
            ((int)response.StatusCode).ShouldBeLessThan(500);
        }

        spy.Requests.ShouldBeEmpty();
        factory.PracticeWorker.Calls.ShouldBeEmpty();
        factory.Worker.CreateCalls.ShouldBe(workerCalls);
    }

    private static IEnumerable<HttpRequestMessage> Requests(Guid trackId, Guid sessionId)
    {
        yield return new HttpRequestMessage(HttpMethod.Get, ApiRoutes.PracticeLogSettings);
        yield return new HttpRequestMessage(HttpMethod.Put, ApiRoutes.PracticeLogSettings) { Content = JsonContent.Create(new PracticeLogSettingsUpdateRequest { NoticeAcknowledged = true }) };
        yield return new HttpRequestMessage(HttpMethod.Put, ApiRoutes.PracticeSession(sessionId)) { Content = JsonContent.Create(Session(trackId)) };
        yield return new HttpRequestMessage(HttpMethod.Get, ApiRoutes.PracticeSessionsOf(trackId));
        yield return new HttpRequestMessage(HttpMethod.Get, ApiRoutes.PracticeSummaryFor(trackId, 30, 60));
        yield return new HttpRequestMessage(HttpMethod.Get, ApiRoutes.PracticeOverviewFor(60));
        yield return new HttpRequestMessage(HttpMethod.Delete, ApiRoutes.PracticeSession(sessionId));
        yield return new HttpRequestMessage(HttpMethod.Delete, ApiRoutes.PracticeSessions);
    }

    private Task<HttpResponseMessage> PutAsync(Guid id, PracticeSessionInputDto session) =>
        Client.PutAsJsonAsync(ApiRoutes.PracticeSession(id), session, Ct);

    private async Task<List<PracticeSessionDto>> ListAsync(Guid? trackId, string extra = "")
    {
        var route = trackId is { } id ? ApiRoutes.PracticeSessionsOf(id) + extra : ApiRoutes.PracticeSessions + extra;
        using var response = await Client.GetAsync(new Uri(route, UriKind.Relative), Ct);
        await ContractAssert.MatchesAsync(response, "listPracticeSessions");
        return await response.BodyAsync<List<PracticeSessionDto>>();
    }

    /// <summary>Records every request an outgoing HttpClient would send (none may happen).</summary>
    private sealed class OutgoingSpy : DelegatingHandler
    {
        public ConcurrentQueue<string> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Enqueue(request.RequestUri?.ToString() ?? string.Empty);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }
    }
}
