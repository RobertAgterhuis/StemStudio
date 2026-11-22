using System.Net;
using Bunit;
using Microsoft.AspNetCore.Components;
using NSubstitute;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Contracts.ScoreFollow;
using StemStudio.Contracts.Scores;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Api;
using StemStudio.Web.Audio;
using StemStudio.Web.Components.InstrumentViews;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.InstrumentViews;

/// <summary>The drum trainer (T090, FR-031, FR-033, FR-034, FR-062; ui-test-ids 1.15.0 drum-*).</summary>
public sealed class DrumTrainerTests : PageTestContext
{
    private const string FakeScore = "<div data-testid=\"fake-score-panel\"></div>";
    private static readonly Guid Track = TestData.TrackId;
    private static readonly DrumPieceDto[] Present = [DrumPieceDto.Kick, DrumPieceDto.Snare, DrumPieceDto.Hihat];

    private readonly List<DrumPieceDto?> _guides = [];
    private DrumPieceDto? _guide;
    private bool _engineReady = true;
    private bool _reducedMotion;

    public DrumTrainerTests() => Api.ListTrackScoresAsync(Track, Arg.Any<CancellationToken>()).Returns([]);

    private static TrackScoreDto DrumScore() => PracticeFlowData.TrackScore() with { Profile = ScoreProfileDto.Drums, Source = "drums" };

    private static ApiException Error(string detail) =>
        new(new ApiError(HttpStatusCode.Conflict, "Conflict", detail, new Dictionary<string, string[]>()));

    private void Hits(params TrackAnalysisDto<DrumHitsDocumentDto>[] answers) =>
        Api.GetDrumHitsAsync(Track, Arg.Any<CancellationToken>()).Returns(answers[0], answers[1..]);

    private void Parameters(ComponentParameterCollectionBuilder<DrumTrainer> p) => p
        .Add(c => c.Track, DrumlessTests.DrumTrack())
        .Add(c => c.EngineReady, _engineReady)
        .Add(c => c.ReducedMotion, _reducedMotion)
        .Add(c => c.Position, 1.2)
        .Add(c => c.GuidePiece, _guide)
        .Add(c => c.GuidePieces, Present)
        .Add(c => c.OnGuide, piece => _guides.Add(piece))
        .Add(c => c.ScoreView, (RenderFragment)(b => b.AddMarkupContent(0, FakeScore)));

    private IRenderedComponent<DrumTrainer> RenderTrainer() => Render<DrumTrainer>(Parameters);

    private IRenderedComponent<CascadingValue<ExperienceContext>> RenderAt(ExperienceLevel level) => RenderAtLevel<DrumTrainer>(level, Parameters);

    private void DrumScoreCanBeMade(AssetDto created)
    {
        Api.GetScoreOptionsAsync(Arg.Any<AudioSourceDto>(), Arg.Any<CancellationToken>()).Returns(ScoreData.DrumOptions(requireSplit: false));
        Api.CreateScoreAsync(Arg.Any<CreateScoreRequest>(), Arg.Any<CancellationToken>()).Returns(created);
    }

    [Fact]
    public void Ready_ShowsTheEmptyLaneHost_AttachesTheHits_AndTheLimits()
    {
        Hits(InstrumentViewsData.DrumHits());

        var trainer = RenderTrainer();

        trainer.Find("[data-testid=drum-trainer]").GetAttribute("aria-label").ShouldBe("Drum trainer");
        var lanes = trainer.Find("[data-testid=drum-lanes]");
        lanes.ChildElementCount.ShouldBe(0);
        lanes.GetAttribute("role").ShouldBe("img");
        lanes.GetAttribute("aria-label").ShouldNotBeNullOrWhiteSpace();
        trainer.Find("[data-testid=drum-hits-status]").GetAttribute("data-status").ShouldBe("ready");
        trainer.Find(".ss-drum-limits").TextContent.ShouldContain("fast fills");
        Views.Received(1).AttachHitLanesAsync(lanes.Id!,
            Arg.Is<IReadOnlyList<DrumHitEvent>>(h => h.Count == 3 && h[0].Piece == "kick" && h[0].TimeMs == 500 && h[0].Strength == 0.9),
            Arg.Is<HitLaneOptions>(o => o.LookAheadMs == 4000));
    }

    [Fact]
    public void StaleHits_StayOnTheLanes()
    {
        Hits(InstrumentViewsData.DrumHits(AnalysisStatusDto.Stale));

        var trainer = RenderTrainer();

        trainer.Find("[data-testid=drum-hits-status]").GetAttribute("data-status").ShouldBe("stale");
        Views.Received(1).AttachHitLanesAsync(Arg.Any<string>(), Arg.Is<IReadOnlyList<DrumHitEvent>>(h => h.Count == 3), Arg.Any<HitLaneOptions>());
    }

    [Fact]
    public void NoHitsYet_DetectsThemOnFirstOpen()
    {
        Hits(InstrumentViewsData.DrumHits(AnalysisStatusDto.None));
        Api.DetectDrumHitsAsync(Track, Arg.Any<CancellationToken>()).Returns(InstrumentViewsData.DrumHits(AnalysisStatusDto.Queued));

        var trainer = RenderTrainer();

        Api.Received(1).DetectDrumHitsAsync(Track, Arg.Any<CancellationToken>());
        trainer.Find("[data-testid=drum-hits-status]").GetAttribute("data-status").ShouldBe("queued");
    }

    [Fact]
    public void DetectionError_IsShown()
    {
        Hits(InstrumentViewsData.DrumHits(AnalysisStatusDto.None));
        Api.DetectDrumHitsAsync(Track, Arg.Any<CancellationToken>()).Returns<TrackAnalysisDto<DrumHitsDocumentDto>>(_ => throw Error("The worker is busy."));

        var trainer = RenderTrainer();

        trainer.Find("[data-testid=drum-trainer] [role=alert]").TextContent.ShouldBe("The worker is busy.");
    }

    [Fact]
    public void Running_ShowsProgress_PollsUntilReady()
    {
        Hits(InstrumentViewsData.DrumHits(AnalysisStatusDto.Running) with { Progress = 0.4 }, InstrumentViewsData.DrumHits());

        var trainer = RenderTrainer();

        trainer.Find("[data-testid=drum-hits-status]").TextContent.ShouldContain("40 %");
        trainer.FindAll("[data-testid=drum-lanes]").ShouldBeEmpty();
        Timer.Tick();
        trainer.WaitForAssertion(() => trainer.Find("[data-testid=drum-hits-status]").GetAttribute("data-status").ShouldBe("ready"));
        trainer.WaitForAssertion(() => trainer.Find("[data-testid=drum-lanes]"));
    }

    [Fact]
    public async Task NotSplit_SaysSo_StartsTheDetailPhase_AndWaitsForTheHits()
    {
        var unavailable = InstrumentViewsData.DrumHits(AnalysisStatusDto.Unavailable) with { Reason = "The drums have not been split." };
        Hits(unavailable, InstrumentViewsData.DrumHits());
        Api.StartDetailAsync(Track, Arg.Any<CancellationToken>()).Returns(TestData.Track());
        var trainer = RenderTrainer();

        trainer.Find("[data-testid=drum-hits-split-required]").TextContent.ShouldContain("split");
        await trainer.Find("[data-testid=drum-hits-split-start]").ClickAsync(new());

        await Api.Received(1).StartDetailAsync(Track, Arg.Any<CancellationToken>());
        trainer.Find("[data-testid=drum-hits-split-start]").HasAttribute("disabled").ShouldBeTrue();
        trainer.Find("[data-testid=drum-hits-split-required]").TextContent.ShouldContain("Splitting");
        Timer.Tick();
        trainer.WaitForAssertion(() => trainer.Find("[data-testid=drum-hits-status]").GetAttribute("data-status").ShouldBe("ready"));
        trainer.FindAll("[data-testid=drum-hits-split-required]").ShouldBeEmpty();
    }

    [Fact]
    public async Task SplitStartError_IsShown()
    {
        Hits(InstrumentViewsData.DrumHits(AnalysisStatusDto.Unavailable));
        Api.StartDetailAsync(Track, Arg.Any<CancellationToken>()).Returns<TrackDto>(_ => throw Error("Another split is running."));
        var trainer = RenderTrainer();

        await trainer.Find("[data-testid=drum-hits-split-start]").ClickAsync(new());

        trainer.Find("[data-testid=drum-trainer] [role=alert]").TextContent.ShouldBe("Another split is running.");
        trainer.Find("[data-testid=drum-hits-split-start]").HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public void Beginner_LaneLabelsInNotationOrder_WithoutGuideOrLookAhead()
    {
        Hits(InstrumentViewsData.DrumHits());

        var trainer = RenderAt(ExperienceLevel.Beginner);

        var labels = trainer.FindAll("[data-testid^=drum-lane-label-]");
        labels.Select(l => l.GetAttribute("data-testid")).ShouldBe(
            ["drum-lane-label-cymbals", "drum-lane-label-hihat", "drum-lane-label-toms", "drum-lane-label-snare", "drum-lane-label-kick"]);
        labels.Select(l => l.TextContent.Trim()).ShouldBe(["Cymbals", "Hi-hat", "Toms", "Snare", "Kick"]);
        labels.ShouldAllBe(l => l.TagName == "BUTTON" && l.HasAttribute("disabled") && !l.HasAttribute("aria-pressed"));
        trainer.FindAll("[data-testid=drum-look-ahead]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Advanced_ALaneLabel_TogglesTheGuide()
    {
        Hits(InstrumentViewsData.DrumHits());
        var trainer = RenderAt(ExperienceLevel.Advanced);

        trainer.Find("[data-testid=drum-lane-label-toms]").HasAttribute("disabled").ShouldBeTrue("the track has no toms channel");
        var hihat = trainer.Find("[data-testid=drum-lane-label-hihat]");
        hihat.GetAttribute("aria-pressed").ShouldBe("false");
        await hihat.ClickAsync(new());

        _guides.ShouldBe([DrumPieceDto.Hihat]);
    }

    [Fact]
    public async Task Advanced_ThePressedGuide_IsReleasedWithASecondClick()
    {
        Hits(InstrumentViewsData.DrumHits());
        _guide = DrumPieceDto.Hihat;
        var trainer = RenderAt(ExperienceLevel.Advanced);

        var hihat = trainer.Find("[data-testid=drum-lane-label-hihat]");
        hihat.GetAttribute("aria-pressed").ShouldBe("true");
        await hihat.ClickAsync(new());

        _guides.ShouldBe([null]);
    }

    [Fact]
    public async Task Advanced_LookAhead_ChangesTheLanes_AndIsRemembered()
    {
        Hits(InstrumentViewsData.DrumHits());
        var trainer = RenderAt(ExperienceLevel.Advanced);

        var slider = trainer.Find("[data-testid=drum-look-ahead]");
        (slider.GetAttribute("min"), slider.GetAttribute("max"), slider.GetAttribute("aria-valuenow")).ShouldBe(("2000", "8000", "4000"));
        await slider.ChangeAsync(new ChangeEventArgs { Value = "6000" });

        await Views.Received(1).SetHitLaneOptionsAsync(Arg.Is<HitLaneOptions>(o => o.LookAheadMs == 6000));
        trainer.Find("[data-testid=drum-look-ahead]").GetAttribute("aria-valuenow").ShouldBe("6000");
        JSInterop.VerifyInvoke("localStorage.setItem").Arguments.ShouldBe(["stemstudio.drums.lookAheadMs", "6000"]);
    }

    [Fact]
    public async Task LookAhead_OutOfRange_IsClamped()
    {
        Hits(InstrumentViewsData.DrumHits());
        var trainer = RenderAt(ExperienceLevel.Advanced);

        await trainer.Find("[data-testid=drum-look-ahead]").ChangeAsync(new ChangeEventArgs { Value = "90000" });

        await Views.Received(1).SetHitLaneOptionsAsync(Arg.Is<HitLaneOptions>(o => o.LookAheadMs == 8000));
    }

    [Fact]
    public void StoredLookAhead_IsUsedFromAdvanced_ButNotAtBeginner()
    {
        JSInterop.Setup<string?>("localStorage.getItem", "stemstudio.drums.lookAheadMs").SetResult("7000");
        Hits(InstrumentViewsData.DrumHits());

        RenderAt(ExperienceLevel.Advanced);
        Views.Received(1).AttachHitLanesAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<DrumHitEvent>>(), Arg.Is<HitLaneOptions>(o => o.LookAheadMs == 7000));
        Views.ClearReceivedCalls();
        RenderAt(ExperienceLevel.Beginner);
        Views.Received(1).AttachHitLanesAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<DrumHitEvent>>(), Arg.Is<HitLaneOptions>(o => o.LookAheadMs == 4000));
    }

    [Fact]
    public void BarText_ListsTheHitsOfTheCurrentBar()
    {
        Hits(InstrumentViewsData.DrumHits());

        var text = RenderTrainer().Find("[data-testid=drum-bar-text]");

        text.ClassList.ShouldContain("ss-sr-only");
        text.TextContent.ShouldContain("Bar 1");
        text.TextContent.ShouldContain("Snare on 2 (loud)");
    }

    [Fact]
    public void ReducedMotion_IsPassedToTheLanes()
    {
        Hits(InstrumentViewsData.DrumHits());

        _reducedMotion = true;

        RenderTrainer();

        Views.Received(1).AttachHitLanesAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<DrumHitEvent>>(), Arg.Is<HitLaneOptions>(o => o.ReducedMotion == true));
    }

    [Fact]
    public void BeforeTheEngineIsReady_TheLanesAreNotAttached()
    {
        Hits(InstrumentViewsData.DrumHits());

        _engineReady = false;

        RenderTrainer();

        Views.DidNotReceiveWithAnyArgs().AttachHitLanesAsync(default!, default!, default!);
    }

    [Fact]
    public void EngineError_IsShown()
    {
        Hits(InstrumentViewsData.DrumHits());
        Views.AttachHitLanesAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<DrumHitEvent>>(), Arg.Any<HitLaneOptions>())
            .Returns(Task.FromException(new AudioEngineException("Hit lane host not found: x")));

        var trainer = RenderTrainer();

        trainer.WaitForAssertion(() => trainer.Find("[data-testid=drum-trainer] [role=alert]").TextContent.ShouldContain("Hit lane host not found"));
    }

    [Fact]
    public async Task Dispose_DetachesTheLanes()
    {
        Hits(InstrumentViewsData.DrumHits());
        var trainer = RenderTrainer();

        await trainer.Instance.DisposeAsync();

        await Views.Received(1).DetachHitLanesAsync();
    }

    [Fact]
    public void ADrumScore_IsShownNextToTheLanes()
    {
        Hits(InstrumentViewsData.DrumHits());
        Api.ListTrackScoresAsync(Track, Arg.Any<CancellationToken>()).Returns([DrumScore()]);

        var score = RenderTrainer().Find("[data-testid=drum-score]");

        score.QuerySelector("[data-testid=fake-score-panel]").ShouldNotBeNull();
        score.QuerySelector("[data-testid=drum-score-generate]").ShouldBeNull();
    }

    [Fact]
    public async Task WithoutADrumScore_GenerateMakesOne_ThenShowsIt()
    {
        Hits(InstrumentViewsData.DrumHits());
        Api.ListTrackScoresAsync(Track, Arg.Any<CancellationToken>()).Returns([PracticeFlowData.TrackScore()], [DrumScore()]);
        DrumScoreCanBeMade(ScoreData.Asset(AssetStatusDto.Queued));
        Api.GetAssetAsync(ScoreData.ScoreId, Arg.Any<CancellationToken>()).Returns(ScoreData.Asset(AssetStatusDto.Ready));
        var trainer = RenderTrainer();
        trainer.Find("[data-testid=drum-score]").QuerySelector("[data-testid=fake-score-panel]").ShouldBeNull();

        await trainer.Find("[data-testid=drum-score-generate]").ClickAsync(new());
        trainer.WaitForAssertion(() => trainer.Find("[data-testid=drum-score-generate]").HasAttribute("disabled").ShouldBeTrue());
        Timer.Tick();

        trainer.WaitForAssertion(() => trainer.Find("[data-testid=drum-score] [data-testid=fake-score-panel]"));
        var request = (CreateScoreRequest)Api.ReceivedCalls().Single(c => c.GetMethodInfo().Name == nameof(Api.CreateScoreAsync)).GetArguments()[0]!;
        (request.Profile, request.Source.Channel).ShouldBe((ScoreProfileDto.Drums, "drums"));
    }

    [Fact]
    public async Task GenerateFailure_IsShown()
    {
        Hits(InstrumentViewsData.DrumHits());
        DrumScoreCanBeMade(ScoreData.Asset(AssetStatusDto.Failed) with { Error = "The drums are silent." });
        var trainer = RenderTrainer();

        await trainer.Find("[data-testid=drum-score-generate]").ClickAsync(new());

        trainer.WaitForAssertion(() => trainer.Find("[data-testid=drum-score] [role=alert]").TextContent.ShouldBe("The drums are silent."));
    }

    [Fact]
    public async Task GenerateApiError_IsShown()
    {
        Hits(InstrumentViewsData.DrumHits());
        Api.GetScoreOptionsAsync(Arg.Any<AudioSourceDto>(), Arg.Any<CancellationToken>()).Returns<ScoreOptionsDto>(_ => throw Error("No drums stem."));
        var trainer = RenderTrainer();

        await trainer.Find("[data-testid=drum-score-generate]").ClickAsync(new());

        trainer.WaitForAssertion(() => trainer.Find("[data-testid=drum-score] [role=alert]").TextContent.ShouldBe("No drums stem."));
    }

    [Fact]
    public void Expert_EveryControlIsNamedWithItsVisibleText()
    {
        Hits(InstrumentViewsData.DrumHits());

        InstrumentViewsAccessibility.EveryControlIsNamedWithItsVisibleText(RenderAt(ExperienceLevel.Expert).Find("[data-testid=drum-trainer]"));
    }
}
