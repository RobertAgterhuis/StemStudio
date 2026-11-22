using Bunit;
using NSubstitute;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Contracts.Recording;
using StemStudio.Web.Audio;
using StemStudio.Web.Components.InstrumentViews;
using StemStudio.Web.InstrumentViews;
using StemStudio.Web.Recording;
using StemStudio.Web.Tests.Fakes;
using StemStudio.Web.Tests.Recording;

namespace StemStudio.Web.Tests.InstrumentViews;

/// <summary>"Score this take" on a 015 take (T105, FR-046, P2; ui-test-ids take-score-*).</summary>
public sealed class TakeScoreActionTests : PageTestContext
{
    private static readonly Guid Take = RecordingData.TakeId;

    public TakeScoreActionTests()
    {
        Api.GetLyricsAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(LyricsData.Found());
        Api.GetVocalPitchAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(InstrumentViewsData.VocalPitch());
    }

    private IRenderedComponent<Microsoft.AspNetCore.Components.CascadingValue<SingingSource>> RenderAction(TakeStatusDto status = TakeStatusDto.Ready) =>
        Render<Microsoft.AspNetCore.Components.CascadingValue<SingingSource>>(p => p
            .Add(c => c.Value, new SingingSource(TestData.Track(), PracticeFlowData.Sections(), 213))
            .AddChildContent<TakeScoreAction>(a => a.Add(c => c.Item, TakeItem.Saved(RecordingData.Take(status: status)))));

    [Fact]
    public async Task ScoringATake_ShowsItsAverage_WithoutSendingAnything()
    {
        Views.ScoreTakeAsync(Arg.Any<TakeScoreRequest>()).Returns(new TakeScoreResult { UnitsScored = 2, Average = 74, ToleranceCents = 100 });
        var action = RenderAction();

        await action.Find($"[data-testid=take-score-{Take}]").ClickAsync(new());

        var result = action.Find($"[data-testid=take-score-result-{Take}]");
        (result.GetAttribute("data-average"), result.TextContent.Trim()).ShouldBe(("74", "Average 74 over 2 lines"));
        var request = (TakeScoreRequest)Views.ReceivedCalls().Single(c => c.GetMethodInfo().Name == nameof(Views.ScoreTakeAsync)).GetArguments()[0]!;
        request.TakeUrl.ShouldEndWith($"/api/takes/{Take:D}/audio");
        (request.StartSample, request.ToleranceCents, request.Units.Count).ShouldBe((441_000L, 50, 2), "the default level is Advanced");
        _ = Api.DidNotReceive().AnalyseVocalPitchAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WithoutAReference_SaysHowToGetOne()
    {
        Api.GetVocalPitchAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(InstrumentViewsData.VocalPitch(AnalysisStatusDto.None));
        var action = RenderAction();

        await action.Find($"[data-testid=take-score-{Take}]").ClickAsync(new());

        action.Find($"[data-testid=take-score-result-{Take}]").TextContent.ShouldContain("Singing feedback");
        _ = Views.DidNotReceiveWithAnyArgs().ScoreTakeAsync(default!);
    }

    [Fact]
    public async Task AnEngineError_IsShown()
    {
        Views.ScoreTakeAsync(Arg.Any<TakeScoreRequest>()).Returns(Task.FromException<TakeScoreResult>(new AudioEngineException("The take could not be decoded.")));
        var action = RenderAction();

        await action.Find($"[data-testid=take-score-{Take}]").ClickAsync(new());

        action.Find($"[data-testid=take-score-result-{Take}]").TextContent.ShouldContain("could not be decoded");
    }

    [Fact]
    public async Task NoScoredLine_IsSaid()
    {
        Views.ScoreTakeAsync(Arg.Any<TakeScoreRequest>()).Returns(new TakeScoreResult { UnitsScored = 0 });
        var action = RenderAction();

        await action.Find($"[data-testid=take-score-{Take}]").ClickAsync(new());

        action.Find($"[data-testid=take-score-result-{Take}]").TextContent.ShouldContain("No line was scored");
    }

    [Fact]
    public void ATakeThatIsNotSaved_CannotBeScored() =>
        RenderAction(TakeStatusDto.Saving).FindAll($"[data-testid=take-score-{Take}]").ShouldBeEmpty();

    [Fact]
    public void WithoutASingingSource_NothingIsShown() =>
        Render<TakeScoreAction>(a => a.Add(c => c.Item, TakeItem.Saved(RecordingData.Take()))).Markup.Trim().ShouldBeEmpty();
}
