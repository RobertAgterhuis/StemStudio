using Bunit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.Recording;
using StemStudio.Web.Components.Recording;
using StemStudio.Web.Recording;
using static StemStudio.Web.Tests.Recording.RecordingData;

namespace StemStudio.Web.Tests.Recording;

/// <summary>015 T063: compare radio group, part-none toggle, C cycle, aria states, mix settings of the strip (FR-012, FR-013, FR-021).</summary>
public sealed class TakeCompareTests : RecordingTestContext
{
    [Fact]
    public void WithoutALoadedTake_RendersNothing() =>
        Render<TakeCompare>(p => p.Add(c => c.Takes, Session.Takes)).Markup.Trim().ShouldBeEmpty();

    [Fact]
    public async Task RadioGroup_StartsAtBoth_AndSendsTheChosenMode()
    {
        await LoadAsync(Take());
        var changed = 0;
        var compare = Render<TakeCompare>(p => p.Add(c => c.Takes, Session.Takes).Add(c => c.OnChanged, () => changed++));

        compare.Find("[data-testid=take-compare]").GetAttribute("role").ShouldBe("radiogroup");
        Checked(compare).ShouldBe(["false", "false", "true"]);
        compare.Find("[data-testid=take-compare-take]").Click();

        await RecordingEngine.Received(1).SetTakeCompareAsync(TakeCompareMode.Take);
        Checked(compare).ShouldBe(["true", "false", "false"]);
        compare.Find("[data-testid=take-compare-take]").GetAttribute("tabindex").ShouldBe("0");
        compare.Find("[data-testid=take-compare-both]").GetAttribute("tabindex").ShouldBe("-1");
        changed.ShouldBe(1);
    }

    [Fact]
    public async Task ArrowKeys_MoveAndSelect_WithinTheGroup()
    {
        await LoadAsync(Take());
        var compare = Render<TakeCompare>(p => p.Add(c => c.Takes, Session.Takes));

        compare.Find("[data-testid=take-compare]").KeyDown("ArrowRight");
        Session.Takes.Compare.ShouldBe(TakeCompareMode.Take);
        compare.Find("[data-testid=take-compare]").KeyDown("ArrowLeft");
        Session.Takes.Compare.ShouldBe(TakeCompareMode.Both);
        compare.Find("[data-testid=take-compare]").KeyDown("ArrowUp");
        Session.Takes.Compare.ShouldBe(TakeCompareMode.Original);
        compare.Find("[data-testid=take-compare]").KeyDown("Enter");

        Session.Takes.Compare.ShouldBe(TakeCompareMode.Original);
    }

    [Fact]
    public async Task C_CyclesBothTakeOriginal()
    {
        await LoadAsync(Take());

        await Session.CycleCompareAsync();
        Session.Takes.Compare.ShouldBe(TakeCompareMode.Take);
        await Session.CycleCompareAsync();
        Session.Takes.Compare.ShouldBe(TakeCompareMode.Original);
        await Session.CycleCompareAsync();

        Session.Takes.Compare.ShouldBe(TakeCompareMode.Both);
    }

    [Fact]
    public async Task PartNone_OffersOnlyTheToggle_AndCSwitchesTheTakeOff()
    {
        await LoadAsync(Take() with { Part = RecordingPartDto.None });
        var compare = Render<TakeCompare>(p => p.Add(c => c.Takes, Session.Takes));

        compare.FindAll("[data-testid=take-compare]").ShouldBeEmpty();
        var toggle = compare.Find("[data-testid=take-compare-toggle]");
        (toggle.GetAttribute("role"), toggle.GetAttribute("aria-checked")).ShouldBe(("switch", "true"));
        toggle.Click();
        compare.Find("[data-testid=take-compare-toggle]").GetAttribute("aria-checked").ShouldBe("false");
        await Session.CycleCompareAsync();

        Session.Takes.Compare.ShouldBe(TakeCompareMode.Both);
        Received.InOrder(() =>
        {
            RecordingEngine.SetTakeCompareAsync(TakeCompareMode.Original);
            RecordingEngine.SetTakeCompareAsync(TakeCompareMode.Both);
        });
    }

    [Fact]
    public async Task C_WithoutATake_DoesNothing()
    {
        await Session.CycleCompareAsync();

        await RecordingEngine.DidNotReceive().SetTakeCompareAsync(Arg.Any<TakeCompareMode>());
    }

    [Fact]
    public async Task EngineRefusal_IsShownAsTheTakeError()
    {
        RecordingEngine.SetTakeCompareAsync(Arg.Any<TakeCompareMode>()).ThrowsAsync(RecordingException.FromEngine("not implemented: setTakeCompare", new InvalidOperationException()));
        await LoadAsync(Take());

        await Session.Takes.SetCompareAsync(TakeCompareMode.Take);

        Session.Takes.Error.ShouldBe("not implemented: setTakeCompare");
    }

    [Fact]
    public async Task UserMuteAndSolo_AreNotTouchedByCompare()
    {
        await LoadAsync(Take());
        Engine.ClearReceivedCalls();

        await Session.Takes.SetCompareAsync(TakeCompareMode.Original);

        await Engine.DidNotReceive().SetChannelAsync(Arg.Any<StemStudio.Contracts.Mixes.ChannelSettingsDto>());
    }

    [Fact]
    public async Task RestoringAMix_LoadsTheTakeWithItsStripSettings()
    {
        await OpenWithInputAsync();
        Api.ListTakesAsync(TrackId, Arg.Any<CancellationToken>()).Returns(List(Take()));
        await Session.Takes.LoadAsync(TrackId);

        await Session.Takes.RestoreAsync(new TakeChannelSettingsDto { TakeId = TakeId, Volume = 0.5, Pan = 0.25, Mute = false, Solo = true });

        Session.Takes.Loaded!.Id.ShouldBe(TakeId.ToString("D"));
        await Engine.Received().SetChannelAsync(new StemStudio.Contracts.Mixes.ChannelSettingsDto("take", 0.5, 0.25, false, true));
        Session.Takes.MixSettings()!.Solo.ShouldBeTrue();
    }

    [Fact]
    public async Task RestoringAMix_IgnoresAStaleOrMissingTake()
    {
        await OpenWithInputAsync();

        await Session.Takes.RestoreAsync(null);
        await Session.Takes.RestoreAsync(new TakeChannelSettingsDto { TakeId = Guid.NewGuid(), Volume = 1, Pan = 0, Mute = false, Solo = false });

        Session.Takes.Loaded.ShouldBeNull();
        Session.Takes.MixSettings().ShouldBeNull();
    }

    private async Task LoadAsync(RecordingTakeDto take)
    {
        await OpenWithInputAsync();
        Api.ListTakesAsync(TrackId, Arg.Any<CancellationToken>()).Returns(List(take));
        await Session.Takes.LoadAsync(TrackId);
        await Session.Takes.LoadIntoStripAsync(Session.Takes.Items[0]);
    }

    private static string?[] Checked(IRenderedComponent<TakeCompare> compare) =>
        [.. new[] { "take", "original", "both" }.Select(m => compare.Find($"[data-testid=take-compare-{m}]").GetAttribute("aria-checked"))];
}
