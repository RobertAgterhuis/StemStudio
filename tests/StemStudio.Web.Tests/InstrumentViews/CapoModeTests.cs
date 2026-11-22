using System.Net;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NSubstitute;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Web.Api;
using StemStudio.Web.Audio;
using StemStudio.Web.Components.InstrumentViews;
using StemStudio.Web.Components.Pages;
using StemStudio.Web.Experience;
using StemStudio.Web.InstrumentViews;
using StemStudio.Web.Tests.Fakes;
using StemStudio.Web.Tests.Mixer;

namespace StemStudio.Web.Tests.InstrumentViews;

/// <summary>Capo mode (T127, FR-075..FR-078, US8 AS5; ui-test-ids capo-*).</summary>
public sealed class CapoModeTests : PageTestContext
{
    private static readonly Guid Track = TestData.TrackId;
    private readonly List<CapoSetting> _changes = [];
    private int _transpose;

    public CapoModeTests() =>
        Api.GetCapoSuggestionsAsync(Track, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(InstrumentViewsData.Capo());

    private void Parameters(ComponentParameterCollectionBuilder<CapoMode> p) => p
        .Add(c => c.TrackId, Track)
        .Add(c => c.TransposeSemitones, _transpose)
        .Add(c => c.OnChanged, s => _changes.Add(s));

    private IRenderedComponent<CascadingValue<ExperienceContext>> RenderAt(ExperienceLevel level = ExperienceLevel.Beginner) =>
        RenderAtLevel<CapoMode>(level, Parameters);

    private static async Task TurnOnAsync(IRenderedComponent<CascadingValue<ExperienceContext>> page) =>
        await page.Find("[data-testid=capo-mode]").ClickAsync(new MouseEventArgs());

    [Fact]
    public void Off_ShowsOnlyTheToggle()
    {
        var page = RenderAt();

        page.Find("[data-testid=capo-mode]").GetAttribute("aria-pressed").ShouldBe("false");
        page.FindAll("[data-testid=capo-select], [data-testid=capo-suggestions], [data-testid=capo-badge]").ShouldBeEmpty();
    }

    [Fact]
    public async Task On_ShowsTheCapo_AndTheSuggestions_BestFirst()
    {
        var page = RenderAt();

        await TurnOnAsync(page);

        page.Find("[data-testid=capo-mode]").GetAttribute("aria-pressed").ShouldBe("true");
        page.Find("[data-testid=capo-select]").GetAttribute("aria-valuenow").ShouldBe("0");
        var list = page.Find("[data-testid=capo-suggestions]");
        list.TagName.ShouldBe("UL");
        list.HasAttribute("role").ShouldBeFalse();
        var first = page.Find("[data-testid=capo-suggestion-0]");
        (first.TagName, first.TextContent.Trim(), first.GetAttribute("data-capo")).ShouldBe(("BUTTON", "Capo 3 - C shapes, 100 % open", "3"));
        page.Find("[data-testid=capo-suggestion-1]").TextContent.Trim().ShouldBe("Capo 1 - D shapes, 75 % open");
        page.FindAll("[data-testid=capo-badge]").ShouldBeEmpty();
        _changes[^1].ShouldBe(new CapoSetting(true, 0, false));
    }

    [Fact]
    public async Task ASuggestion_IsAppliedWithOneClick_AndRemembered()
    {
        var page = RenderAt();
        await TurnOnAsync(page);

        await page.Find("[data-testid=capo-suggestion-0]").ClickAsync(new MouseEventArgs());

        page.Find("[data-testid=capo-badge]").TextContent.Trim().ShouldBe("Capo 3");
        page.Find("[data-testid=capo-select]").GetAttribute("aria-valuenow").ShouldBe("3");
        _changes[^1].ShouldBe(new CapoSetting(true, 3, false));
        var stored = JSInterop.Invocations["localStorage.setItem"][^1].Arguments;
        stored[0].ShouldBe("stemstudio.chords.capo");
        stored[1]!.ToString()!.ShouldContain(Track.ToString());
    }

    [Fact]
    public async Task TheCapo_IsClampedTo0Through12()
    {
        var page = RenderAt();
        await TurnOnAsync(page);

        await page.Find("[data-testid=capo-select]").ChangeAsync(new ChangeEventArgs { Value = "15" });

        _changes[^1].Capo.ShouldBe(12);
    }

    [Fact]
    public async Task AfterATranspose_TheShortcutPlaysTheOriginalShapes()
    {
        _transpose = 2;
        Api.GetCapoSuggestionsAsync(Track, 2, Arg.Any<CancellationToken>()).Returns(InstrumentViewsData.Capo() with { Transpose = 2, Shortcut = new CapoShortcutDto { Capo = 2 } });
        var page = RenderAt();
        await TurnOnAsync(page);

        var shortcut = page.Find("[data-testid=capo-shortcut]");
        shortcut.TextContent.Trim().ShouldBe("Capo 2: play the original shapes");
        await shortcut.ClickAsync(new MouseEventArgs());

        _changes[^1].ShouldBe(new CapoSetting(true, 2, false));
    }

    [Fact]
    public async Task Advanced_ShowsTheSoundingChords_OnRequest()
    {
        var page = RenderAt(ExperienceLevel.Advanced);
        await TurnOnAsync(page);

        await page.Find("[data-testid=capo-show-sounding]").ChangeAsync(new ChangeEventArgs { Value = true });

        _changes[^1].ShowSounding.ShouldBeTrue();
        var beginner = RenderAt();
        await TurnOnAsync(beginner);
        beginner.FindAll("[data-testid=capo-show-sounding]").ShouldBeEmpty();
    }

    [Fact]
    public void TheStoredSetting_ComesBack()
    {
        JSInterop.Setup<string?>("localStorage.getItem", "stemstudio.chords.capo")
            .SetResult($$$"""{"{{{Track}}}":{"on":true,"capo":2,"showSounding":false}}""");

        var page = RenderAt();

        page.WaitForAssertion(() => page.Find("[data-testid=capo-badge]").TextContent.Trim().ShouldBe("Capo 2"));
        _changes.ShouldContain(new CapoSetting(true, 2, false));
    }

    [Fact]
    public void AStoredCapoOutOfRange_IsClamped()
    {
        JSInterop.Setup<string?>("localStorage.getItem", "stemstudio.chords.capo")
            .SetResult($$$"""{"{{{Track}}}":{"on":true,"capo":99,"showSounding":false}}""");

        RenderAt();

        _changes[^1].Capo.ShouldBe(12);
    }

    [Fact]
    public async Task ChordsNotReady_SaysSo()
    {
        Api.GetCapoSuggestionsAsync(Track, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns<CapoSuggestionsDto>(_ => throw new ApiException(new ApiError(HttpStatusCode.Conflict, "Conflict", "The chords are not ready yet.", new Dictionary<string, string[]>())));
        var page = RenderAt();

        await TurnOnAsync(page);

        page.FindAll("[data-testid=capo-suggestion-0]").ShouldBeEmpty();
        page.Find(".ss-capo-mode").TextContent.ShouldContain("not ready");
    }

    [Fact]
    public async Task Expert_EveryControlIsNamedWithItsVisibleText()
    {
        var page = RenderAt(ExperienceLevel.Expert);
        await TurnOnAsync(page);

        InstrumentViewsAccessibility.EveryControlIsNamedWithItsVisibleText(page.Find(".ss-capo-mode"));
    }
}

/// <summary>Capo mode in the mixer and on the track page (T127, FR-075, FR-078, US8 AS5).</summary>
public sealed class MixerCapoTests : MixerTestContext
{
    public MixerCapoTests()
    {
        Api.GetCapoSuggestionsAsync(TestData.TrackId, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(InstrumentViewsData.Capo());
        Api.ListTrackScoresAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns([]);
    }

    private IRenderedComponent<StemStudio.Web.Components.Mixer.Mixer> Ready()
    {
        var page = RenderMixer();
        page.WaitForAssertion(() => page.Find("[data-testid=transport-play]").HasAttribute("disabled").ShouldBeFalse());
        return page;
    }

    private static async Task PractiseAsync(IRenderedComponent<StemStudio.Web.Components.Mixer.Mixer> page, string family)
    {
        await page.Find("[data-testid=practise-as]").ClickAsync(new MouseEventArgs());
        await page.Find($"[data-testid=practise-as-{family}]").ClickAsync(new MouseEventArgs());
    }

    [Fact]
    public async Task TheGuitarFamily_GetsCapoMode_WhichSetsTheChordDisplay()
    {
        var page = Ready();
        page.FindAll("[data-testid=capo-mode]").ShouldBeEmpty();

        await PractiseAsync(page, "guitar");
        await page.Find("[data-testid=capo-mode]").ClickAsync(new MouseEventArgs());
        await page.Find("[data-testid=capo-suggestion-0]").ClickAsync(new MouseEventArgs());

        await Views.Received().SetChordDisplayAsync(new ChordDisplay(3, false));
        page.Find(".ss-mixer-dock").QuerySelector("[data-testid=capo-mode]").ShouldBeNull();
    }

    [Theory]
    [InlineData("keys")]
    [InlineData("bass")]
    public async Task LeavingGuitar_ResetsTheChordDisplay(string family)
    {
        var page = Ready();
        await PractiseAsync(page, "guitar");
        await page.Find("[data-testid=capo-mode]").ClickAsync(new MouseEventArgs());
        await page.Find("[data-testid=capo-suggestion-0]").ClickAsync(new MouseEventArgs());
        Views.ClearReceivedCalls();

        await PractiseAsync(page, family);

        await Views.Received(1).SetChordDisplayAsync(new ChordDisplay(0, false));
    }

    [Fact]
    public async Task AnotherFamily_WithoutCapo_SendsNoChordDisplay()
    {
        var page = Ready();

        await PractiseAsync(page, "keys");

        await Views.DidNotReceiveWithAnyArgs().SetChordDisplayAsync(default!);
    }

    [Theory]
    [InlineData("bass")]
    [InlineData("keys")]
    public async Task BassAndKeys_HaveNoCapoControls(string family)
    {
        var page = Ready();

        await PractiseAsync(page, family);

        page.FindAll("[data-testid=capo-mode], [data-testid=capo-select], [data-testid=capo-badge]").ShouldBeEmpty();
    }

    [Fact]
    public void TrackPage_ARememberedGuitarist_GetsCapoMode_AndTheSheetAndTabUseTheCapo()
    {
        JSInterop.Setup<string?>("localStorage.getItem", "stemstudio.views.family").SetResult("\"guitar\"");
        JSInterop.Setup<string?>("localStorage.getItem", "stemstudio.chords.capo")
            .SetResult($$$"""{"{{{TestData.TrackId}}}":{"on":true,"capo":3,"showSounding":false}}""");
        Api.GetLyricsAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(LyricsData.Found());
        Api.GetChordsAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(InstrumentViewsData.Chords());
        Api.GetChordSheetAsync(TestData.TrackId, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(InstrumentViewsData.Sheet());

        var page = Render<TrackDetail>(p => p.Add(c => c.Id, TestData.TrackId));

        page.WaitForAssertion(() => page.Find("[data-testid=capo-badge]").TextContent.Trim().ShouldBe("Capo 3"));
        page.WaitForAssertion(() => Api.Received().GetChordSheetAsync(TestData.TrackId, Arg.Any<int>(), 3, Arg.Any<CancellationToken>()));
        page.Instance.ScoreCapo.ShouldBe(3);
    }

    [Fact]
    public void TrackPage_OtherFamilies_HaveNoCapoMode()
    {
        JSInterop.Setup<string?>("localStorage.getItem", "stemstudio.views.family").SetResult("\"bass\"");

        var page = Render<TrackDetail>(p => p.Add(c => c.Id, TestData.TrackId));

        page.WaitForAssertion(() => page.Find("[data-testid=track-bpm]"));
        page.FindAll("[data-testid=capo-mode]").ShouldBeEmpty();
    }
}
