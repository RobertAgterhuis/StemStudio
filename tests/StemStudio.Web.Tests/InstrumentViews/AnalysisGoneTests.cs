using System.Net;
using Bunit;
using Microsoft.AspNetCore.Components;
using NSubstitute;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Web.Api;
using StemStudio.Web.Components.InstrumentViews;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.InstrumentViews;

/// <summary>A track deleted while an analysis is polled (review L5): the panels stop polling and show the not-found error.</summary>
public sealed class AnalysisGoneTests : PageTestContext
{
    private static readonly Guid Track = TestData.TrackId;
    private int _calls;

    private static ApiException NotFound() => new(ApiError.FromStatus(HttpStatusCode.NotFound));

    private T RunningThenGone<T>(T running) => ++_calls == 1 ? running : throw NotFound();

    private async Task AssertPollingStoppedAsync<TComponent>(IRenderedComponent<TComponent> panel)
        where TComponent : IComponent
    {
        Timer.Tick();
        panel.WaitForAssertion(() => panel.Find("[role=alert]").TextContent.ShouldNotBeNullOrWhiteSpace());
        await Task.Delay(150);
        Timer.Tick();
        await Task.Delay(150);

        (_calls, Timer.Waits).ShouldBe((2, 1), "no poll after the 404");
    }

    [Fact]
    public async Task ChordPanel_StopsPollingAfterA404()
    {
        Api.GetChordsAsync(Track, Arg.Any<CancellationToken>()).Returns(_ => RunningThenGone(InstrumentViewsData.Chords(AnalysisStatusDto.Running, 0.4)));

        await AssertPollingStoppedAsync(Render<ChordPanel>(p => p.Add(c => c.TrackId, Track)));
    }

    [Fact]
    public async Task SingingPanel_StopsPollingAfterA404()
    {
        Api.GetVocalPitchAsync(Track, Arg.Any<CancellationToken>()).Returns(_ => RunningThenGone(InstrumentViewsData.VocalPitch(AnalysisStatusDto.Running)));

        await AssertPollingStoppedAsync(Render<SingingPanel>(p => p.Add(c => c.Track, DrumlessTests.DrumTrack()).Add(c => c.Duration, 213)));
    }

    [Fact]
    public async Task DrumTrainer_StopsPollingAfterA404()
    {
        Api.ListTrackScoresAsync(Track, Arg.Any<CancellationToken>()).Returns([]);
        Api.GetDrumHitsAsync(Track, Arg.Any<CancellationToken>()).Returns(_ => RunningThenGone(InstrumentViewsData.DrumHits(AnalysisStatusDto.Running)));

        await AssertPollingStoppedAsync(Render<DrumTrainer>(p => p.Add(c => c.Track, DrumlessTests.DrumTrack()).Add(c => c.GuidePieces, Array.Empty<DrumPieceDto>())));
    }
}
