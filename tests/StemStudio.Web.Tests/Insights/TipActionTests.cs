using Bunit;
using NSubstitute;
using StemStudio.Contracts.Insights;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Components.Insights;
using StemStudio.Web.Insights;
using StemStudio.Web.Tests.Fakes;
using StemStudio.Web.Tests.Pages;

namespace StemStudio.Web.Tests.Insights;

/// <summary>
/// Tips with actions (T053, US2 scenarios 1-5, FR-005, FR-006): split further starts the 002 detail separation, open
/// detail stem opens the mixer with the parent expanded, mute opens the mixer with the stem muted (Q2), buttons are
/// native buttons with their visible text as name, at most two tips, no GPU tip.
/// </summary>
public sealed class TipActionTests : TrackDetailTestContext
{
    private static readonly Guid Track = TestData.TrackId;

    private static QualityReportDto WithTips(params StemQualityViewDto[] stems) => InsightsData.ReadyReport() with { Stems = stems };

    private IRenderedComponent<StemStudio.Web.Components.Pages.TrackDetail> PageWith(QualityReportDto report, TrackDto? track = null)
    {
        Api.GetQualityReportAsync(Track, Arg.Any<CancellationToken>()).Returns(report);
        return RenderPage(track ?? TestData.Track());
    }

    [Fact]
    public async Task SplitFurther_StartsTheDetailSeparation()
    {
        Api.StartDetailAsync(Track, Arg.Any<CancellationToken>()).Returns(TestData.WithDetail(TestData.Phase(DetailPhaseStatusDto.Queued)));
        var page = PageWith(InsightsData.ReadyReport());

        var button = page.Find("[data-testid=quality-tip-action-other-try-detail-separation]");
        button.TextContent.Trim().ShouldBe("Split further");
        await button.ClickAsync(new());

        await Api.Received(1).StartDetailAsync(Track, Arg.Any<CancellationToken>());
        page.WaitForAssertion(() => page.Find("[data-testid=detail-status]").TextContent.ShouldNotBeEmpty());
    }

    [Fact]
    public async Task OpenDetailStem_OpensTheMixerWithTheParentExpanded()
    {
        var tip = InsightsData.Tip(TipCodeDto.UseDetailStem, "Use the kick detail stem instead.", InsightsData.Action(TipActionKindDto.OpenDetail, "drums", "drums", "kick"));
        var page = PageWith(WithTips(InsightsData.Stem("drums", QualityRatingDto.Poor, null, tip)), TestData.DetailTrack());

        var button = page.Find("[data-testid=quality-tip-action-drums-use-detail-stem]");
        button.TextContent.Trim().ShouldBe("Open detail stem");
        await button.ClickAsync(new());

        NavigatedTo.ShouldEndWith($"tracks/{Track}/mixer?expand=drums");
    }

    [Fact]
    public async Task ListenToRest_OfADetailStem_ExpandsItsParent()
    {
        var tip = InsightsData.Tip(TipCodeDto.ListenToRest, "Part of this sound went to the 'rest' stem; listen to it too.",
            InsightsData.Action(TipActionKindDto.OpenDetail, "guitar.lead", "guitar", "rest"));
        var page = PageWith(WithTips(InsightsData.Stem("guitar", QualityRatingDto.Fair), InsightsData.Stem("guitar.lead", QualityRatingDto.Fair, "guitar", tip)));

        await page.Find("[data-testid=quality-tip-action-guitar-lead-listen-to-rest]").ClickAsync(new());

        NavigatedTo.ShouldEndWith($"tracks/{Track}/mixer?expand=guitar");
    }

    [Fact]
    public async Task Mute_OpensTheMixerWithTheStemMuted_WithoutAnApiCall()
    {
        var page = PageWith(InsightsData.ReadyReport());

        var button = page.Find("[data-testid=quality-tip-action-piano-not-in-song]");
        button.TextContent.Trim().ShouldBe("Mute");
        await button.ClickAsync(new());

        NavigatedTo.ShouldEndWith($"tracks/{Track}/mixer?mute=piano");
        await Api.DidNotReceive().UpdateMixAsync(Arg.Any<Guid>(), Arg.Any<StemStudio.Contracts.Mixes.MixSettingsDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void AdviceTips_HaveNoButton_AtMostTwoTipsAreShown_AndNoneMentionsTheGpu()
    {
        var tips = new[]
        {
            InsightsData.Tip(TipCodeDto.HardToIsolate, "Guitar is hard to isolate with today's models; this is close to the best StemStudio can do."),
            InsightsData.Tip(TipCodeDto.Clean, "Clean enough to practise with."),
            InsightsData.Tip(TipCodeDto.ListenToRest, "Third tip."),
        };
        var page = PageWith(WithTips(InsightsData.Stem("guitar", QualityRatingDto.Poor, null, tips)));

        page.Find("[data-testid=quality-tip-guitar-hard-to-isolate]").TextContent.ShouldContain("hard to isolate");
        page.FindAll("[data-testid^=quality-tip-guitar-]").Count.ShouldBe(2);
        page.FindAll("[data-testid^=quality-tip-action-guitar-]").ShouldBeEmpty();
        page.Find("[data-testid=quality-panel]").TextContent.ShouldNotContain("GPU");
    }

    [Fact]
    public void Buttons_AreNativeKeyboardButtons_NamedByTheirText()
    {
        var page = PageWith(InsightsData.ReadyReport());

        foreach (var button in page.FindAll("[data-testid^=quality-tip-action-]"))
        {
            button.LocalName.ShouldBe("button");
            button.GetAttribute("tabindex").ShouldNotBe("-1");
            button.HasAttribute("disabled").ShouldBeFalse();
        }

        StemStudio.Web.Tests.InstrumentViews.InstrumentViewsAccessibility.EveryControlIsNamedWithItsVisibleText(page.Find("[data-testid=quality-panel]"));
    }

    [Fact]
    public async Task TipList_DisablesItsButtons_WhileTheActionRuns()
    {
        var gate = new TaskCompletionSource();
        var actions = Substitute.For<ITipActions>();
        actions.RunAsync(Arg.Any<TipActionDto>()).Returns(gate.Task);
        var list = Render<TipList>(p => p.Add(c => c.Stem, InsightsData.ReadyReport().Stems[0]).Add(c => c.Actions, actions));

        var click = list.Find("button").ClickAsync(new());

        list.Find("button").HasAttribute("disabled").ShouldBeTrue();
        gate.SetResult();
        await click;
        list.Find("button").HasAttribute("disabled").ShouldBeFalse();
        await actions.Received(1).RunAsync(Arg.Is<TipActionDto>(a => a.Kind == TipActionKindDto.SplitDetail && a.Parent == "other"));
    }

    [Theory]
    [InlineData(TipActionKindDto.SplitDetail, "Split further")]
    [InlineData(TipActionKindDto.OpenDetail, "Open detail stem")]
    [InlineData(TipActionKindDto.Mute, "Mute")]
    public void ButtonTexts_FollowTheContract(TipActionKindDto kind, string text) => TipText.Button(kind).ShouldBe(text);

    [Fact]
    public void Links_AreRelativeToTheTrack_AndEscapeTheChannel()
    {
        TipLinks.Mixer(Track, InsightsData.Action(TipActionKindDto.Mute, "drums.kick")).ShouldBe($"tracks/{Track}/mixer?mute=drums.kick");
        TipLinks.Mixer(Track, InsightsData.Action(TipActionKindDto.OpenDetail, "vocals", "vocals", "lead")).ShouldBe($"tracks/{Track}/mixer?expand=vocals");
        TipLinks.Mixer(Track, InsightsData.Action(TipActionKindDto.OpenDetail, "vocals")).ShouldBe($"tracks/{Track}/mixer?expand=vocals");
        TipLinks.Mixer(Track, InsightsData.Action(TipActionKindDto.Mute, "a b")).ShouldBe($"tracks/{Track}/mixer?mute=a%20b");
    }
}
