using Bunit;
using Microsoft.AspNetCore.Components;
using NSubstitute;
using StemStudio.Contracts.Assets;
using StemStudio.Web.Experience;
using StemStudio.Web.Preferences;
using StemStudio.Web.Tests.Fakes;
using StemStudio.Web.Tests.Mixer;
using MixerPage = StemStudio.Web.Components.Mixer.Mixer;

namespace StemStudio.Web.Tests.Export;

/// <summary>What I hear and selected channels from the mixer (007 T044, FR-002, FR-014, ui-test-ids 1.5.0).</summary>
public sealed class MixExportTests : MixerTestContext
{
    public MixExportTests() =>
        Api.CreateAudioExportAsync(Arg.Any<CreateAudioExportRequest>(), Arg.Any<CancellationToken>()).Returns(AssetData.Asset(status: AssetStatusDto.Queued));

    private IRenderedComponent<CascadingValue<ExperienceContext>> RenderAt(ExperienceLevel level) =>
        Render<CascadingValue<ExperienceContext>>(p => p
            .Add(c => c.Value, new ExperienceContext(level, DensityPreference.Comfortable))
            .AddChildContent<MixerPage>(m => m.Add(x => x.Id, TestData.TrackId)));

    private static async Task OpenAsync(IRenderedComponent<CascadingValue<ExperienceContext>> page)
    {
        await page.Find("[data-testid=mute-vocals]").ClickAsync(new());
        await page.Find("[data-testid=mixer-export]").ClickAsync(new());
    }

    private static Task SubmitAsync(IRenderedComponent<CascadingValue<ExperienceContext>> page) =>
        page.Find("[data-testid=export-submit]").ClickAsync(new());

    [Fact]
    public async Task MixerExport_OpensTheDialogWithWhatIHearSelected()
    {
        var page = RenderAt(ExperienceLevel.Beginner);

        await OpenAsync(page);

        page.Find("[data-testid=export-dialog]").GetAttribute("data-mode").ShouldBe("mix");
        var audible = page.Find("[data-testid=export-source-audible]");
        (audible.GetAttribute("type"), audible.HasAttribute("checked")).ShouldBe(("radio", true));
        page.FindAll("[data-testid=export-source-selected]").ShouldBeEmpty();
        page.FindAll("[data-testid^=export-channel-]").ShouldBeEmpty();
    }

    [Fact]
    public async Task WhatIHear_SendsTheCurrentUnsavedMixWithoutAChannelList()
    {
        var page = RenderAt(ExperienceLevel.Advanced);
        await OpenAsync(page);

        await SubmitAsync(page);

        await Api.Received(1).CreateAudioExportAsync(
            Arg.Is<CreateAudioExportRequest>(r => r.Mode == ExportModeDto.Mix && r.Channels == null && r.MixId == null
                && r.Mix!.Channels.Single(c => c.Stem == "vocals").Mute && r.Mix.MasterVolume == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectedChannels_ArePrefilledWithTheAudibleChannels()
    {
        var page = RenderAt(ExperienceLevel.Advanced);
        await OpenAsync(page);

        await page.Find("[data-testid=export-source-selected]").ChangeAsync(new ChangeEventArgs { Value = "selected" });

        page.FindAll("[data-testid^=export-channel-]").Select(b => b.GetAttribute("data-testid")).ShouldBe(
            ["export-channel-vocals", "export-channel-drums", "export-channel-bass", "export-channel-guitar", "export-channel-piano", "export-channel-other"]);
        page.Find("[data-testid=export-channel-vocals]").HasAttribute("checked").ShouldBeFalse();
        page.Find("[data-testid=export-channel-drums]").HasAttribute("checked").ShouldBeTrue();
        page.Find("[data-testid=export-channel-drums]").GetAttribute("type").ShouldBe("checkbox");
    }

    [Fact]
    public async Task TheRequest_MatchesTheSelection()
    {
        var page = RenderAt(ExperienceLevel.Expert);
        await OpenAsync(page);
        await page.Find("[data-testid=export-source-selected]").ChangeAsync(new ChangeEventArgs { Value = "selected" });

        await page.Find("[data-testid=export-channel-drums]").ChangeAsync(new ChangeEventArgs { Value = false });
        await page.Find("[data-testid=export-channel-vocals]").ChangeAsync(new ChangeEventArgs { Value = true });
        await SubmitAsync(page);

        await Api.Received(1).CreateAudioExportAsync(
            Arg.Is<CreateAudioExportRequest>(r => r.Channels!.SequenceEqual(new[] { "vocals", "bass", "guitar", "piano", "other" }) && r.Mix != null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnEmptySelection_DisablesSubmit_AndWhatIHearEnablesItAgain()
    {
        var page = RenderAt(ExperienceLevel.Advanced);
        await OpenAsync(page);
        await page.Find("[data-testid=export-source-selected]").ChangeAsync(new ChangeEventArgs { Value = "selected" });

        foreach (var key in new[] { "drums", "bass", "guitar", "piano", "other" })
        {
            await page.Find($"[data-testid=export-channel-{key}]").ChangeAsync(new ChangeEventArgs { Value = false });
        }

        page.Find("[data-testid=export-submit]").HasAttribute("disabled").ShouldBeTrue();
        page.Find("[data-testid=export-error-hint]").TextContent.ShouldBe("Select at least one channel.");
        await page.Find("[data-testid=export-source-audible]").ChangeAsync(new ChangeEventArgs { Value = "audible" });
        page.Find("[data-testid=export-submit]").HasAttribute("disabled").ShouldBeFalse();
        page.FindAll("[data-testid^=export-channel-]").ShouldBeEmpty();
    }

    [Fact]
    public async Task SourceChoice_CarriesTheDialogHelpMarker()
    {
        var page = RenderAt(ExperienceLevel.Advanced);

        await OpenAsync(page);

        page.Find("fieldset[data-help-id=export-dialog]").QuerySelector("[data-testid=export-source-audible]").ShouldNotBeNull();
    }

    [Fact]
    public async Task MixerExport_IsANamedButton()
    {
        var page = RenderAt(ExperienceLevel.Beginner);

        var button = page.Find("[data-testid=mixer-export]");

        button.TextContent.ShouldContain("Save to library");
        await button.ClickAsync(new());
        page.Find("[data-testid=export-dialog]").ShouldNotBeNull();
    }
}
