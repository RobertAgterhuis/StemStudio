using Bunit;
using Microsoft.AspNetCore.Components.Web;
using NSubstitute;
using StemStudio.Contracts.Mixes;
using StemStudio.Web.Tests.Fakes;
using StemStudio.Web.Tests.Mixer;
using MixerPage = StemStudio.Web.Components.Mixer.Mixer;

namespace StemStudio.Web.Tests.InstrumentViews;

/// <summary>"Practise as ..." (T113, FR-060, FR-062, spec US6): one entry point per family, minus-one mix and the family's view.</summary>
public sealed class PractiseAsTests : MixerTestContext
{
    private static readonly string[] Families = ["vocals", "drums", "bass", "guitar", "keys", "orchestral"];

    public PractiseAsTests()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(DrumlessTests.DrumTrack());
        Api.ListTrackScoresAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns([]);
        Api.GetDrumHitsAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(InstrumentViewsData.DrumHits());
        Api.GetLyricsAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(LyricsData.Found());
        Api.GetVocalPitchAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(InstrumentViewsData.VocalPitch());
    }

    private IRenderedComponent<MixerPage> Ready()
    {
        var page = RenderMixer();
        page.WaitForAssertion(() => page.Find("[data-testid=transport-play]").HasAttribute("disabled").ShouldBeFalse());
        Engine.ClearReceivedCalls();
        return page;
    }

    private static async Task ChooseAsync(IRenderedComponent<MixerPage> page, string family)
    {
        await page.Find("[data-testid=practise-as]").ClickAsync(new MouseEventArgs());
        await page.Find($"[data-testid=practise-as-{family}]").ClickAsync(new MouseEventArgs());
    }

    [Fact]
    public async Task TheMenu_ListsEveryFamily_AsRadioItems()
    {
        var page = Ready();
        var button = page.Find("[data-testid=practise-as]");
        (button.GetAttribute("aria-haspopup"), button.GetAttribute("aria-expanded"), button.GetAttribute("data-family")).ShouldBe(("menu", "false", string.Empty));
        button.GetAttribute("data-help-id").ShouldBe("practise-as");

        await button.ClickAsync(new MouseEventArgs());

        page.Find("[data-testid=practise-as]").GetAttribute("aria-expanded").ShouldBe("true");
        var items = page.FindAll("[role=menu] [role=menuitemradio]");
        items.Select(i => i.GetAttribute("data-testid")).ShouldBe(Families.Select(f => $"practise-as-{f}"));
        items.Select(i => i.TextContent.Trim()).ShouldBe(["Vocals", "Drums", "Bass", "Guitar", "Keys", "Strings / brass / woodwinds"]);
        items.ShouldAllBe(i => i.GetAttribute("aria-checked") == "false");
    }

    [Theory]
    [InlineData("vocals", "singing", "vocals.lead")]
    [InlineData("drums", "drum-trainer", "drums.kick")]
    [InlineData("bass", "tablature", "bass")]
    [InlineData("guitar", "tablature", "guitar")]
    [InlineData("keys", "chords", "piano")]
    [InlineData("orchestral", "part", "other")]
    public async Task AFamily_GetsItsMinusOneMix_AndItsView(string family, string view, string mutedChannel)
    {
        var page = Ready();

        await ChooseAsync(page, family);

        await Engine.Received().SetChannelAsync(Arg.Is<ChannelSettingsDto>(c => c.Stem == mutedChannel && c.Mute));
        page.Find("[data-testid=instrument-view]").GetAttribute("data-view").ShouldBe(view);
        page.Find("[data-testid=practise-as]").GetAttribute("data-family").ShouldBe(family);
        page.FindAll("[role=menu]").ShouldBeEmpty();
        JSInterop.VerifyInvoke("localStorage.setItem").Arguments.ShouldBe(["stemstudio.views.family", $"\"{family}\""]);
    }

    [Fact]
    public async Task TheChosenFamily_IsChecked_AndOthersAreAudible()
    {
        var page = Ready();
        page.Find("[data-testid=mute-bass]").Click();

        await ChooseAsync(page, "guitar");
        await page.Find("[data-testid=practise-as]").ClickAsync(new MouseEventArgs());

        page.Find("[data-testid=practise-as-guitar]").GetAttribute("aria-checked").ShouldBe("true");
        page.Find("[data-testid=mute-bass]").GetAttribute("aria-pressed").ShouldBe("false");
        page.Find("[data-testid=mute-guitar]").GetAttribute("aria-pressed").ShouldBe("true");
    }

    [Fact]
    public async Task ARememberedFamily_IsOffered_NotApplied()
    {
        JSInterop.Setup<string?>("localStorage.getItem", "stemstudio.views.family").SetResult("\"keys\"");

        var page = Ready();

        page.WaitForAssertion(() => page.Find("[data-testid=practise-as-offer]").TextContent.ShouldContain("Practise as Keys again?"));
        page.Find("[data-testid=mute-piano]").GetAttribute("aria-pressed").ShouldBe("false");
        page.FindAll("[data-testid=instrument-view]").ShouldBeEmpty();
        await page.Find("[data-testid=practise-as-offer] button").ClickAsync(new MouseEventArgs());
        page.Find("[data-testid=mute-piano]").GetAttribute("aria-pressed").ShouldBe("true");
        page.FindAll("[data-testid=practise-as-offer]").ShouldBeEmpty();
    }

    [Fact]
    public void AnUnknownRememberedFamily_IsIgnored()
    {
        JSInterop.Setup<string?>("localStorage.getItem", "stemstudio.views.family").SetResult("\"<script>\"");

        Ready().FindAll("[data-testid=practise-as-offer]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Keyboard_ArrowsMoveTheFocus_EscapeCloses()
    {
        var page = Ready();
        await page.Find("[data-testid=practise-as]").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });

        string Focused() => page.FindAll("[role=menuitemradio]").Single(i => i.GetAttribute("tabindex") == "0").GetAttribute("data-testid")!;
        Focused().ShouldBe("practise-as-vocals");
        var menu = page.Find("[role=menu]");
        await menu.KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });
        Focused().ShouldBe("practise-as-drums");
        await page.Find("[role=menu]").KeyDownAsync(new KeyboardEventArgs { Key = "End" });
        Focused().ShouldBe("practise-as-orchestral");
        await page.Find("[role=menu]").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });
        Focused().ShouldBe("practise-as-vocals");
        await page.Find("[role=menu]").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowUp" });
        Focused().ShouldBe("practise-as-orchestral");
        await page.Find("[role=menu]").KeyDownAsync(new KeyboardEventArgs { Key = "Home" });
        Focused().ShouldBe("practise-as-vocals");

        await page.Find("[role=menu]").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        page.FindAll("[role=menu]").ShouldBeEmpty();
        page.Find("[data-testid=practise-as]").GetAttribute("aria-expanded").ShouldBe("false");
    }

    [Fact]
    public async Task TheOrchestralView_PicksTheWrittenInstrument_AndRemembersIt()
    {
        var page = Ready();

        await ChooseAsync(page, "orchestral");

        var select = page.Find("[data-testid=instrument-view] [data-testid=score-part-instrument]");
        page.WaitForAssertion(() => page.FindAll("[data-testid=instrument-view] [data-testid=score-part-instrument] option").Count.ShouldBe(3));
        await select.ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "alto-sax-eb" });
        JSInterop.VerifyInvoke("localStorage.setItem", 2).Arguments.ShouldBe(["stemstudio.views.instrument", "\"alto-sax-eb\""]);
    }

    [Fact]
    public async Task TheKeysView_ShowsTheChordSheet()
    {
        Api.GetChordSheetAsync(TestData.TrackId, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(InstrumentViewsData.Sheet());
        var page = Ready();

        await ChooseAsync(page, "keys");

        page.WaitForAssertion(() => page.Find("[data-testid=instrument-view] [data-testid=chord-sheet]"));
    }

    [Fact]
    public async Task TheTabView_OffersToGenerateTab_WithoutAScore()
    {
        var page = Ready();

        await ChooseAsync(page, "guitar");

        page.Find("[data-testid=instrument-view]").TextContent.ShouldContain("Generate tab");
    }

    [Fact]
    public void TheMenu_LivesOutsideTheDock()
    {
        var page = Ready();

        page.Find(".ss-mixer-dock").QuerySelector("[data-testid=practise-as]").ShouldBeNull();
    }
}
