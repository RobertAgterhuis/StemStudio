using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StemStudio.Contracts.Mixes;
using StemStudio.Web.Audio;
using StemStudio.Web.Experience;
using StemStudio.Web.Interop;
using StemStudio.Web.Services;

namespace StemStudio.Web.Tests.Mixer.Practice;

/// <summary>Practice keyboard commands and palette actions of the mixer (012 T045, FR-017).</summary>
public sealed class MixerShortcutsPracticeTests : PracticeTestContext
{
    private Task CommandAsync(IRenderedComponent<CascadingValue<ExperienceContext>> page, string command) =>
        page.InvokeAsync(() => Interop.CommandAsync(ShortcutScope.Mixer, command));

    private Task PaletteAsync(IRenderedComponent<CascadingValue<ExperienceContext>> page, string label) =>
        page.InvokeAsync(() => Services.GetRequiredService<PageCommands>().Items.Single(i => i.Label == label).Run());

    private static string Speed(IRenderedComponent<CascadingValue<ExperienceContext>> page) =>
        page.Find("[data-testid=practice-panel]").GetAttribute("data-speed")!;

    private static string Transpose(IRenderedComponent<CascadingValue<ExperienceContext>> page) =>
        page.Find("[data-testid=practice-panel]").GetAttribute("data-transpose")!;

    [Fact]
    public async Task SpeedKeys_StepByFivePercent_LikeTheButtons_AndClamp()
    {
        var page = RenderAt();

        await CommandAsync(page, "mixer.speed:-5");
        Speed(page).ShouldBe("95");
        await CommandAsync(page, "mixer.speed:5");
        await CommandAsync(page, "mixer.speed:5");
        await CommandAsync(page, "mixer.speed:5");
        await CommandAsync(page, "mixer.speed:5");
        await CommandAsync(page, "mixer.speed:5");

        Speed(page).ShouldBe("120");
        page.Find("[data-testid=unsaved-indicator]").ShouldNotBeNull();
    }

    [Fact]
    public async Task TransposeKeys_StepOneSemitone_AndClamp()
    {
        var page = RenderAt();

        await CommandAsync(page, "mixer.transpose:1");
        Transpose(page).ShouldBe("1");
        for (var i = 0; i < 14; i++)
        {
            await CommandAsync(page, "mixer.transpose:-1");
        }

        Transpose(page).ShouldBe("-12");
    }

    [Fact]
    public async Task ResetKey_RestoresTheOriginalSpeedAndKey()
    {
        MixWith(At(70, 3));
        var page = RenderAt(withMix: true);
        page.WaitForAssertion(() => Speed(page).ShouldBe("70"));
        NextInterval();

        await CommandAsync(page, "mixer.practice.reset");

        (Speed(page), Transpose(page)).ShouldBe(("100", "0"));
        await Engine.Received().SetPracticeAsync(PracticeSettingsDto.Default);
    }

    [Theory]
    [InlineData("mixer.speed:5")]
    [InlineData("mixer.speed:-5")]
    [InlineData("mixer.transpose:1")]
    [InlineData("mixer.transpose:-1")]
    [InlineData("mixer.practice.reset")]
    public async Task PracticeKeys_AreIgnored_WhileTheStageIsUnavailable(string command)
    {
        MixWith(At(80, 2));
        var page = RenderAt(withMix: true);
        page.WaitForAssertion(() => Engine.Received(1).SetPracticeAsync(Arg.Any<PracticeSettingsDto>()));
        RaiseStatus(PracticeStatus.ForUnavailable("No AudioWorklet."));
        page.WaitForAssertion(() => page.Find("[data-testid=practice-unavailable]").ShouldNotBeNull());
        NextInterval();
        Engine.ClearReceivedCalls();

        await CommandAsync(page, command);

        await Engine.DidNotReceive().SetPracticeAsync(Arg.Any<PracticeSettingsDto>());
        page.FindAll("[data-testid=unsaved-indicator]").ShouldBeEmpty();
    }

    [Fact]
    public async Task SpeedKeys_AreIgnored_WhileTheTrainerRuns()
    {
        var page = RenderAt();
        RaiseStatus(Running(60));
        page.WaitForAssertion(() => Speed(page).ShouldBe("60"));

        await CommandAsync(page, "mixer.speed:5");

        await Engine.DidNotReceive().SetPracticeAsync(Arg.Any<PracticeSettingsDto>());
    }

    [Fact]
    public async Task PaletteActions_ChangeSpeedAndReset()
    {
        var page = RenderAt();

        await PaletteAsync(page, "Speed -5 %");
        Speed(page).ShouldBe("95");
        await PaletteAsync(page, "Speed -5 %");
        await PaletteAsync(page, "Speed +5 %");
        Speed(page).ShouldBe("95");
        await PaletteAsync(page, "Original speed and key");

        Speed(page).ShouldBe("100");
    }
}
