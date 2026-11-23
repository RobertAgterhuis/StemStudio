using System.Net;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Insights;
using StemStudio.Contracts.Tracks;
using StemStudio.Domain.Insights;

namespace StemStudio.Api.Tests.Insights;

/// <summary>
/// Feature 020 T044: <c>GET/POST /api/tracks/{id}/quality</c> and <c>Track.quality</c> against backend 1.19.0: the report with
/// the reason texts of data-model 4.6, the fixed disclaimer, detail stems with their parent, and the badge on the track.
/// </summary>
public sealed class QualityEndpointsTests : ApiTestBase
{
    private ApiTestFactoryPair Pair => new(Factory, Client);

    [Fact]
    public async Task ATrackWithoutAReport_HasStatusNone_AndTheDisclaimer()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());

        using var response = await Client.GetAsync(new Uri(ApiRoutes.Quality(track.Id), UriKind.Relative), Ct);

        await ContractAssert.MatchesAsync(response, "getQualityReport");
        var report = await response.BodyAsync<QualityReportDto>();
        (report.Status, report.Progress, report.Outdated, report.Stems.Count, report.Disclaimer).ShouldBe((QualityReportStatusDto.None, 0, false, 0, ContractLimits.QualityDisclaimer));
        report.Summary.ShouldBe(new QualitySummaryDto { Good = 0, Fair = 0, Poor = 0, NotPresent = 0, Unknown = 0 });
    }

    [Fact]
    public async Task AReadyReport_MatchesTheContract_WithReasonTextsAndDetailParents()
    {
        var track = await Factory.SeedAsync(SyncTestData.DetailCompleted());
        var result = QualityTestData.Result("fake-1", ("vocals", QualityRating.Fair), ("drums", QualityRating.Good), ("bass", QualityRating.Good), ("guitar", QualityRating.Poor), ("piano", QualityRating.NotPresent), ("other", QualityRating.Fair), ("drums.kick", QualityRating.Good), ("drums.rest", QualityRating.Unknown));
        await Pair.MeasureAsync(track.Id, result);

        using var response = await Client.GetAsync(new Uri(ApiRoutes.Quality(track.Id), UriKind.Relative), Ct);

        await ContractAssert.MatchesAsync(response, "getQualityReport");
        var report = await response.BodyAsync<QualityReportDto>();
        report.Summary.ShouldBe(new QualitySummaryDto { Good = 3, Fair = 2, Poor = 1, NotPresent = 1, Unknown = 1 });
        var vocals = report.Stems[0];
        (vocals.Channel, vocals.Parent, vocals.Reason, vocals.Bleed.MainSource, vocals.Bleed.Level).ShouldBe(("vocals", (string?)null, "Some sound of drums is in this stem.", "drums", QualityLevelDto.Medium));
        report.Stems[1].Reason.ShouldBe("Little bleed and few artefacts.");
        (report.Stems[6].Channel, report.Stems[6].Parent).ShouldBe(("drums.kick", "drums"));
        report.Stems.ShouldAllBe(stem => stem.PreviousRating == null && stem.Bleed.Reliability == ReliabilityDto.Indicative);
    }

    [Fact]
    public async Task TheRequest_Is202WithTheQueuedReport_AndItsLocation()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());

        using var response = await Client.RequestQualityAsync(track.Id);

        await ContractAssert.MatchesAsync(response, "requestQualityReport");
        response.Headers.Location!.ToString().ShouldBe(ApiRoutes.Quality(track.Id));
        (await response.BodyAsync<QualityReportDto>()).Status.ShouldBe(QualityReportStatusDto.Queued);
    }

    [Fact]
    public async Task TheRequestProblems_MatchTheContract()
    {
        var processing = await Factory.SeedAsync(SyncTestData.UrlTrack("aaaaaaaaaaa"));
        var down = await Factory.SeedAsync(SyncTestData.CompletedTrack("bbbbbbbbbbb"));

        using var missing = await Client.RequestQualityAsync(Guid.NewGuid());
        using var getMissing = await Client.GetAsync(new Uri(ApiRoutes.Quality(Guid.NewGuid()), UriKind.Relative), Ct);
        using var notReady = await Client.RequestQualityAsync(processing.Id);
        Factory.InsightsWorker.Unavailable = true;
        using var unavailable = await Client.RequestQualityAsync(down.Id);

        await ContractAssert.MatchesAsync(missing, "requestQualityReport");
        await ContractAssert.MatchesAsync(getMissing, "getQualityReport");
        await ContractAssert.MatchesAsync(notReady, "requestQualityReport");
        await ContractAssert.MatchesAsync(unavailable, "requestQualityReport");
        (missing.StatusCode, getMissing.StatusCode, notReady.StatusCode, unavailable.StatusCode).ShouldBe((HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.Conflict, HttpStatusCode.ServiceUnavailable));
    }

    [Fact]
    public async Task TheTrack_CarriesTheBadge_OnlyOnceAReportExists()
    {
        var measured = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        var plain = await Factory.SeedAsync(SyncTestData.CompletedTrack("zzzzzzzzzzz"));
        await Pair.MeasureAsync(measured.Id, QualityTestData.SixGood());

        using var one = await Client.GetAsync(new Uri(ApiRoutes.Track(measured.Id), UriKind.Relative), Ct);
        using var all = await Client.GetAsync(new Uri(ApiRoutes.Tracks, UriKind.Relative), Ct);

        await ContractAssert.MatchesAsync(one, "getTrack");
        await ContractAssert.MatchesAsync(all, "listTracks");
        var badge = (await one.BodyAsync<TrackDto>()).Quality!;
        (badge.Status, badge.Summary.Good, badge.Outdated).ShouldBe((QualityReportStatusDto.Ready, 6, false));
        var list = await all.BodyAsync<List<TrackDto>>();
        list.Single(track => track.Id == plain.Id).Quality.ShouldBeNull();
        list.Single(track => track.Id == measured.Id).Quality!.Status.ShouldBe(QualityReportStatusDto.Ready);
        (await one.Content.ReadAsStringAsync(Ct)).ShouldContain("\"quality\":{\"status\":\"ready\"");
    }

    [Fact]
    public async Task TheBadge_ShowsAQueuedRun()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        (await Client.RequestQualityAsync(track.Id)).Dispose();

        using var response = await Client.GetAsync(new Uri(ApiRoutes.Track(track.Id), UriKind.Relative), Ct);

        (await response.BodyAsync<TrackDto>()).Quality!.Status.ShouldBe(QualityReportStatusDto.Queued);
    }
}
