using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StemStudio.Api.Insights;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.SongSections;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Insights;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Api.Tests.Insights;

/// <summary>
/// The switch <c>StemStudio:QualityReport:Enabled</c> (owner decision 2026-11-23, G1 failed on real music): off by default.
/// While off there is no automatic report, <c>POST</c> answers the contract's 503 with a readable detail and stores nothing,
/// <c>GET</c> answers status <c>none</c> and <c>Track.quality</c> is absent, also for a track that kept an older report.
/// </summary>
public sealed class QualityReportSwitchTests
{
    private static readonly Dictionary<string, string?> Off = new() { [QualityReportSwitch.Key] = "false" };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void TheReport_IsOffByDefault()
    {
        var options = new InsightsOptions();

        (options.QualityReport.Enabled, options.AutoQualityReport, options.AutomaticQualityReports).ShouldBe((false, true, false));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void AutomaticReports_NeedTheReportSwitchedOn(bool enabled, bool automatic, bool expected)
    {
        var options = new InsightsOptions { QualityReport = { Enabled = enabled }, AutoQualityReport = automatic };

        options.AutomaticQualityReports.ShouldBe(expected);
    }

    [Fact]
    public void TheSetting_IsBoundFromConfiguration()
    {
        using var factory = new ApiFactory { Settings = Off };
        using var client = factory.CreateClient();

        factory.Services.GetRequiredService<IOptions<InsightsOptions>>().Value.QualityReport.Enabled.ShouldBeFalse();
    }

    [Fact]
    public void AValueThatIsNoBoolean_StopsTheStart()
    {
        using var factory = new ApiFactory { Settings = new Dictionary<string, string?> { [QualityReportSwitch.Key] = "maybe" } };

        var error = Should.Throw<InvalidOperationException>(() => factory.CreateClient().Dispose());

        error.ToString().ShouldContain("QualityReport");
    }

    [Fact]
    public async Task WhileOff_ASeparationStartsNoReport()
    {
        using var factory = new ApiFactory { Settings = Off };
        factory.CreateClient().Dispose();
        await factory.SeedAsync(SyncTestData.UrlTrack());
        factory.CompleteSeparations();

        await factory.SyncTracksAsync();

        (await factory.AllTracksAsync()).Single().QualityReport.ShouldBeNull();
        factory.InsightsWorker.QualityRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task WhileOff_TheAutomaticStartIsRefused()
    {
        using var factory = new ApiFactory { Settings = Off };
        factory.CreateClient().Dispose();
        var track = await factory.SeedAsync(SyncTestData.CompletedTrack());

        var started = await factory.WithScopeAsync(async sp =>
            await sp.GetRequiredService<QualityService>().StartAutomaticallyAsync((await factory.FindTrackAsync(track.Id))!, CancellationToken.None));

        started.ShouldBeFalse();
        factory.InsightsWorker.QualityRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task WhileOff_ARequestAnswers503_AndStoresNothing()
    {
        using var factory = new ApiFactory { Settings = Off };
        using var client = factory.CreateClient();
        var track = await factory.SeedAsync(SyncTestData.CompletedTrack());

        using var response = await client.RequestQualityAsync(track.Id);

        await ContractAssert.MatchesAsync(response, "requestQualityReport");
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        var problem = await response.JsonAsync();
        problem.GetProperty("detail").GetString().ShouldBe(InsightsProblems.QualityReportOffDetail);
        problem.GetProperty("title").GetString().ShouldBe("Stem quality report switched off");
        factory.InsightsWorker.QualityRequests.ShouldBeEmpty();
        (await factory.FindTrackAsync(track.Id))!.QualityReport.ShouldBeNull();
    }

    [Fact]
    public async Task WhileOff_ARequestForAnUnknownTrack_IsStillNotFound()
    {
        using var factory = new ApiFactory { Settings = Off };
        using var client = factory.CreateClient();

        using var response = await client.RequestQualityAsync(Guid.NewGuid());

        await ContractAssert.MatchesAsync(response, "requestQualityReport");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WhileOff_TheReportIsNone_AndTheTrackHasNoBadge_EvenWithAnOlderReport()
    {
        using var factory = new ApiFactory { Settings = Off };
        using var client = factory.CreateClient();
        var track = SyncTestData.CompletedTrack();
        track.RequestQuality("v1", DateTime.UtcNow);
        await factory.SeedAsync(track);

        using var report = await client.GetAsync(new Uri(ApiRoutes.Quality(track.Id), UriKind.Relative), Ct);
        using var one = await client.GetAsync(new Uri(ApiRoutes.Track(track.Id), UriKind.Relative), Ct);
        using var all = await client.GetAsync(new Uri(ApiRoutes.Tracks, UriKind.Relative), Ct);

        await ContractAssert.MatchesAsync(report, "getQualityReport");
        await ContractAssert.MatchesAsync(one, "getTrack");
        var dto = await report.BodyAsync<QualityReportDto>();
        (dto.Status, dto.Progress, dto.Stems.Count, dto.Outdated).ShouldBe((QualityReportStatusDto.None, 0, 0, false));
        (await one.BodyAsync<TrackDto>()).Quality.ShouldBeNull();
        (await one.Content.ReadAsStringAsync(Ct)).ShouldNotContain("\"quality\"");
        (await all.BodyAsync<List<TrackDto>>()).Single().Quality.ShouldBeNull();
        (await factory.FindTrackAsync(track.Id))!.QualityReport.ShouldNotBeNull();
    }

    [Fact]
    public async Task WhileOn_ASeparationStartsAReport()
    {
        using var factory = new ApiFactory { Settings = new Dictionary<string, string?> { [QualityReportSwitch.Key] = "true" } };
        factory.CreateClient().Dispose();
        await factory.SeedAsync(SyncTestData.UrlTrack());
        factory.CompleteSeparations();

        await factory.SyncTracksAsync();

        factory.InsightsWorker.QualityRequests.Count.ShouldBe(1);
    }
}

/// <summary>The setting key of the quality report switch.</summary>
internal static class QualityReportSwitch
{
    /// <summary><c>StemStudio:QualityReport:Enabled</c>.</summary>
    public const string Key = "StemStudio:QualityReport:Enabled";
}
