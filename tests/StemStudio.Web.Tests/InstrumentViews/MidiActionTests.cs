using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Contracts.Scores;
using StemStudio.Web.Api;
using StemStudio.Web.Components.InstrumentViews;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.InstrumentViews;

/// <summary>The one-click MIDI action of a channel (T081, FR-023, ui-test-ids midi-*).</summary>
public sealed class MidiActionTests : PageTestContext
{
    private static readonly Guid Track = TestData.TrackId;

    private IRenderedComponent<MidiAction> RenderAction(string channel = "bass") =>
        Render<MidiAction>(p => p.Add(c => c.Track, TestData.Track()).Add(c => c.Channel, channel));

    private void Existing(params AssetDto[] items) =>
        Api.ListChannelScoresAsync(Track, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new AssetListDto(items, items.Length, 0));

    private void Options(ScoreOptionsDto options) =>
        Api.GetScoreOptionsAsync(Arg.Any<AudioSourceDto>(), Arg.Any<CancellationToken>()).Returns(options);

    private void WaitForCreate(IRenderedComponent<MidiAction> action) =>
        action.WaitForState(() => Api.ReceivedCalls().Any(c => c.GetMethodInfo().Name == nameof(Api.CreateScoreAsync)));

    private string LastNavigation() => Services.GetRequiredService<BunitNavigationManager>().History.First().Uri;

    private CreateScoreRequest SentRequest() =>
        (CreateScoreRequest)Api.ReceivedCalls().Single(c => c.GetMethodInfo().Name == nameof(Api.CreateScoreAsync)).GetArguments()[0]!;

    [Fact]
    public async Task ReadyItem_IsReused_AndItsMidiIsDownloaded()
    {
        Existing(ScoreData.Asset(AssetStatusDto.Ready));
        var action = RenderAction();

        await action.Find("[data-testid=midi-bass]").ClickAsync(new());

        _ = Api.DidNotReceive().CreateScoreAsync(Arg.Any<CreateScoreRequest>(), Arg.Any<CancellationToken>());
        action.WaitForAssertion(() => LastNavigation().ShouldEndWith($"/api/assets/{ScoreData.ScoreId}/files/midi"));
        action.WaitForAssertion(() => action.Find("[data-testid=midi-bass]").GetAttribute("aria-busy").ShouldBeNull());
    }

    [Fact]
    public async Task NoItem_CreatesOne_ShowsProgress_ThenDownloads()
    {
        Existing();
        Options(ScoreData.Options());
        Api.CreateScoreAsync(Arg.Any<CreateScoreRequest>(), Arg.Any<CancellationToken>()).Returns(ScoreData.Asset(AssetStatusDto.Queued));
        Api.GetAssetAsync(ScoreData.ScoreId, Arg.Any<CancellationToken>()).Returns(
            ScoreData.Asset(AssetStatusDto.Rendering) with { Progress = 40 }, ScoreData.Asset(AssetStatusDto.Ready));
        var action = RenderAction();

        await action.Find("[data-testid=midi-bass]").ClickAsync(new());

        action.Find("[data-testid=midi-bass]").GetAttribute("aria-busy").ShouldBe("true");
        action.Find("[data-testid=midi-progress-bass]").GetAttribute("aria-valuenow").ShouldBe("0");
        Timer.Tick();
        action.WaitForAssertion(() => action.Find("[data-testid=midi-progress-bass]").GetAttribute("aria-valuenow").ShouldBe("40"));
        Timer.Tick();
        action.WaitForAssertion(() => action.FindAll("[data-testid=midi-progress-bass]").ShouldBeEmpty());
        LastNavigation().ShouldEndWith($"/api/assets/{ScoreData.ScoreId}/files/midi");
        SentRequest().Profile.ShouldBe(ScoreProfileDto.Bass);
        SentRequest().Source.Channel.ShouldBe("bass");
    }

    [Fact]
    public async Task GuitarMidi_AsksForNoTablature()
    {
        Existing();
        Options(ScoreData.Options("guitar", ScoreProfileDto.Guitar) with
        {
            Tablature = new ScoreTablatureOptionsDto
            {
                Available = true,
                DefaultTuning = TuningIdDto.GuitarStandard,
                Tunings = [TuningIdDto.GuitarStandard],
                CapoAllowed = true,
            },
        });
        Api.CreateScoreAsync(Arg.Any<CreateScoreRequest>(), Arg.Any<CancellationToken>()).Returns(ScoreData.Asset(AssetStatusDto.Ready));

        var action = RenderAction("guitar");
        await action.Find("[data-testid=midi-guitar]").ClickAsync(new());

        WaitForCreate(action);
        SentRequest().Tablature.ShouldBeNull();
    }

    [Fact]
    public async Task DetailChannel_UsesDashesInItsTestIds_AndTheDrumsProfile()
    {
        Existing();
        Options(ScoreData.DrumOptions(requireSplit: false));
        Api.CreateScoreAsync(Arg.Any<CreateScoreRequest>(), Arg.Any<CancellationToken>()).Returns(ScoreData.Asset(AssetStatusDto.Ready));
        var action = RenderAction("drums.kick");

        await action.Find("[data-testid=midi-drums-kick]").ClickAsync(new());

        WaitForCreate(action);
        SentRequest().Profile.ShouldBe(ScoreProfileDto.Drums);
        SentRequest().Source.Channel.ShouldBe("drums.kick");
    }

    [Fact]
    public async Task FailedItem_ShowsTheReason()
    {
        Existing();
        Options(ScoreData.Options());
        Api.CreateScoreAsync(Arg.Any<CreateScoreRequest>(), Arg.Any<CancellationToken>()).Returns(ScoreData.Asset(AssetStatusDto.Queued));
        Api.GetAssetAsync(ScoreData.ScoreId, Arg.Any<CancellationToken>()).Returns(ScoreData.Asset(AssetStatusDto.Failed) with { Error = "The worker stopped." });
        var action = RenderAction();

        await action.Find("[data-testid=midi-bass]").ClickAsync(new());
        Timer.Tick();

        action.WaitForAssertion(() => action.Find("[data-testid=midi-error-bass]").TextContent.ShouldContain("The worker stopped."));
        action.Find("[data-testid=midi-error-bass]").GetAttribute("role").ShouldBe("alert");
        action.Find("[data-testid=midi-bass]").GetAttribute("aria-busy").ShouldBeNull();
    }

    [Fact]
    public async Task ApiError_ShowsTheMessage_AndTheActionCanBeRepeated()
    {
        Api.ListChannelScoresAsync(Track, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<AssetListDto>(_ => throw new ApiException("Could not reach the server."));
        var action = RenderAction();

        await action.Find("[data-testid=midi-bass]").ClickAsync(new());

        action.WaitForAssertion(() => action.Find("[data-testid=midi-error-bass]").TextContent.ShouldContain("Could not reach the server."));
        action.Find("[data-testid=midi-bass]").HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public async Task OptionsThatAskForInput_ShowTheirMessage_WithoutCreating()
    {
        Existing();
        Options(ScoreData.Options(suggested: null));
        var action = RenderAction();

        await action.Find("[data-testid=midi-bass]").ClickAsync(new());

        action.WaitForAssertion(() => action.Find("[data-testid=midi-error-bass]").TextContent.ShouldNotBeNullOrWhiteSpace());
        _ = Api.DidNotReceive().CreateScoreAsync(Arg.Any<CreateScoreRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void TheButton_HasItsVisibleTextInItsName_AndTheHelpAnchor()
    {
        var action = RenderAction("drums.kick");
        var button = action.Find("[data-testid=midi-drums-kick]");

        button.TextContent.Trim().ShouldEndWith("MIDI");
        button.GetAttribute("aria-label").ShouldBe("MIDI of kick");
        button.GetAttribute("data-help-id").ShouldBe("midi-download");
        InstrumentViewsAccessibility.EveryControlIsNamedWithItsVisibleText(action.Find(".ss-midi"));
    }
}
