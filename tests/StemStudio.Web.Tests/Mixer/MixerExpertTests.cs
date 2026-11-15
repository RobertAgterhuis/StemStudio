using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StemStudio.Contracts.Mixes;
using StemStudio.Web.Experience;
using StemStudio.Web.Interop;
using StemStudio.Web.Preferences;
using StemStudio.Web.Services;
using StemStudio.Web.Tests.Fakes;
using MixerPage = StemStudio.Web.Components.Mixer.Mixer;

namespace StemStudio.Web.Tests.Mixer;

/// <summary>Mixer keyboard commands and Expert controls (T060, FR-017, FR-019).</summary>
public sealed class MixerExpertTests : MixerTestContext
{
    private IRenderedComponent<CascadingValue<ExperienceContext>> RenderAt(ExperienceLevel level)
    {
        var page = Render<CascadingValue<ExperienceContext>>(p => p
            .Add(c => c.Value, new ExperienceContext(level, DensityPreference.Comfortable))
            .AddChildContent<MixerPage>(m => m.Add(x => x.Id, TestData.TrackId)));
        page.WaitForAssertion(() => Interop.HasShortcuts(ShortcutScope.Mixer).ShouldBeTrue());
        return page;
    }

    private Task CommandAsync(IRenderedComponent<CascadingValue<ExperienceContext>> page, string command) =>
        page.InvokeAsync(() => Interop.CommandAsync(ShortcutScope.Mixer, command));

    private void Position(double seconds) => Engine.PositionChanged += Raise.Event<EventHandler<double>>(Engine, seconds);

    [Fact]
    public async Task Stop_AndSeek_CallTheEngineLikeTheTransport()
    {
        var page = RenderAt(ExperienceLevel.Advanced);
        Position(10);

        await CommandAsync(page, "mixer.stop");
        await CommandAsync(page, "mixer.seek:5");
        await CommandAsync(page, "mixer.seek:-1");

        await Engine.Received(1).StopAsync();
        await Engine.Received(1).SeekAsync(15);
        await Engine.Received(1).SeekAsync(9);
    }

    [Fact]
    public async Task Seek_IsClampedToTheSong()
    {
        var page = RenderAt(ExperienceLevel.Advanced);
        Position(2);

        await CommandAsync(page, "mixer.seek:-5");
        Position(212);
        await CommandAsync(page, "mixer.seek:5");

        await Engine.Received(1).SeekAsync(0);
        await Engine.Received(1).SeekAsync(213.04);
    }

    [Fact]
    public async Task LoopKeys_SetAandB_AtThePosition_AndToggle()
    {
        var page = RenderAt(ExperienceLevel.Advanced);

        Position(10);
        await CommandAsync(page, "mixer.loop.a");
        Position(20);
        await CommandAsync(page, "mixer.loop.b");
        await CommandAsync(page, "mixer.loop.toggle");

        await Engine.Received().SetLoopAsync(new LoopRegionDto(10, 20, true));
        page.Find("[data-testid=loop-toggle]").GetAttribute("aria-pressed").ShouldBe("true");
    }

    [Fact]
    public async Task MetronomeKey_TogglesTheClick()
    {
        var page = RenderAt(ExperienceLevel.Advanced);
        var before = page.Find("[data-testid=metronome-toggle]").GetAttribute("aria-pressed");

        await CommandAsync(page, "mixer.metronome.toggle");

        page.Find("[data-testid=metronome-toggle]").GetAttribute("aria-pressed").ShouldNotBe(before);
        await Engine.Received().SetMetronomeAsync(Arg.Any<MetronomeSettingsDto>());
    }

    [Fact]
    public async Task ChannelKeys_SelectAStrip_ThenMuteAndSoloIt()
    {
        var page = RenderAt(ExperienceLevel.Advanced);

        await CommandAsync(page, "mixer.channel.select:2");
        await CommandAsync(page, "mixer.channel.mute");
        await CommandAsync(page, "mixer.channel.solo");

        page.Find("[data-testid=strip-drums]").GetAttribute("data-selected").ShouldBe("true");
        page.Find("[data-testid=strip-vocals]").HasAttribute("data-selected").ShouldBeFalse();
        page.Find("[data-testid=mute-drums]").GetAttribute("aria-pressed").ShouldBe("true");
        page.Find("[data-testid=solo-drums]").GetAttribute("aria-pressed").ShouldBe("true");
    }

    [Fact]
    public async Task MuteWithoutSelection_AndOutOfRangeSelection_DoNothing()
    {
        var page = RenderAt(ExperienceLevel.Advanced);
        Engine.ClearReceivedCalls();

        await CommandAsync(page, "mixer.channel.mute");
        await CommandAsync(page, "mixer.channel.select:9");
        await CommandAsync(page, "mixer.channel.solo");
        await CommandAsync(page, "mixer.unknown");

        await Engine.DidNotReceive().SetChannelAsync(Arg.Any<ChannelSettingsDto>());
        page.FindAll("[data-selected=true]").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("mixer.save")]
    [InlineData("mixer.save-as")]
    public async Task SaveKeys_OpenTheNameDialog_ForAnUnsavedMix(string command)
    {
        var page = RenderAt(ExperienceLevel.Advanced);

        await CommandAsync(page, command);

        page.Find("[data-testid=mix-name-input]").ShouldNotBeNull();
    }

    [Fact]
    public async Task Leaving_DisposesTheMixerKeys_AndThePageActions()
    {
        var page = RenderAt(ExperienceLevel.Advanced);
        Services.GetRequiredService<PageCommands>().Items.Select(i => i.Id).ShouldContain("mixer-save");

        await DisposeComponentsAsync();

        Interop.HasShortcuts(ShortcutScope.Mixer).ShouldBeFalse();
        Services.GetRequiredService<PageCommands>().Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task PageAction_Save_OpensTheDialog()
    {
        var page = RenderAt(ExperienceLevel.Advanced);

        await page.InvokeAsync(() => Services.GetRequiredService<PageCommands>().Items.Single(i => i.Id == "mixer-save").Run());

        page.Find("[data-testid=mix-name-input]").ShouldNotBeNull();
    }

    [Fact]
    public void Expert_HasNumericFields_ThatFollowTheSliders()
    {
        var page = RenderAt(ExperienceLevel.Expert);

        page.Find("[data-testid=fader-vocals]").Input("40");

        page.Find("[data-testid=fader-value-vocals]").GetAttribute("value").ShouldBe("40");
        page.Find("[data-testid=pan-value-vocals]").GetAttribute("value").ShouldBe("0");
    }

    [Fact]
    public async Task Expert_NumericFields_DriveTheSliders_AndTheEngine()
    {
        var page = RenderAt(ExperienceLevel.Expert);

        page.Find("[data-testid=fader-value-vocals]").Change("55");
        page.Find("[data-testid=pan-value-vocals]").Change("-30");
        page.Find("[data-testid=pan-value-vocals]").Change("oops");

        page.Find("[data-testid=fader-vocals]").GetAttribute("aria-valuenow").ShouldBe("55");
        page.Find("[data-testid=pan-vocals]").GetAttribute("aria-valuenow").ShouldBe("-30");
        await Engine.Received().SetChannelAsync(Arg.Is<ChannelSettingsDto>(c => c.Stem == "vocals" && Math.Abs(c.Volume - 0.55) < 1e-9));
    }

    [Fact]
    public void Expert_ShowsDbReadoutsUnderEveryMeter()
    {
        var page = RenderAt(ExperienceLevel.Expert);

        page.Find("[data-testid=meter-db-vocals]").TextContent.ShouldBe("-inf");
        page.Find("[data-testid=meter-db-master]").TextContent.ShouldBe("-inf");
        page.Find("[data-testid=fader-value-vocals]").GetAttribute("data-help-id").ShouldBe("mixer-numeric-entry");
    }

    [Fact]
    public void Advanced_HasNoExpertControls()
    {
        var page = RenderAt(ExperienceLevel.Advanced);

        page.FindAll("[data-testid^=fader-value-]").ShouldBeEmpty();
        page.FindAll("[data-testid^=pan-value-]").ShouldBeEmpty();
        page.FindAll("[data-testid^=meter-db-]").ShouldBeEmpty();
    }

    [Fact]
    public void Expert_TransportShowsKbdHints()
    {
        RenderAt(ExperienceLevel.Expert).Find("[data-testid=transport-play]").ParentElement!.QuerySelectorAll("kbd").Length.ShouldBeGreaterThan(0);
    }
}
