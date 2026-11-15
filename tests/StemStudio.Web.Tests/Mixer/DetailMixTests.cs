using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts;
using StemStudio.Contracts.Mixes;
using StemStudio.Web.Audio;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Mixer;

/// <summary>Mixes with expansion state and detail channel settings (FR-015, SC-005, US5).</summary>
public sealed class DetailMixTests : MixerTestContext
{
    private static readonly Guid OldMixId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    public DetailMixTests()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(TestData.DetailTrack());
        Api.GetMixAsync(TestData.MixId, Arg.Any<CancellationToken>()).Returns(NoSnare());
        Api.GetMixAsync(OldMixId, Arg.Any<CancellationToken>()).Returns(TestData.Mix() with { Id = OldMixId });
    }

    private static MixDto NoSnare() => TestData.Mix("No snare") with
    {
        Settings = TestData.Settings("No snare") with
        {
            Expanded = [StemNames.Drums],
            DetailChannels = [new(StemNames.Drums, "snare", 0.8, 0, true, false), new(StemNames.Drums, "kick", 0.6, 0.1, false, false)],
        },
    };

    [Fact]
    public async Task SaveAs_SendsExpansionAndDetailSettings()
    {
        Api.CreateMixAsync(Arg.Any<CreateMixRequest>(), Arg.Any<CancellationToken>()).Returns(c => NoSnare() with { Settings = c.Arg<CreateMixRequest>().Settings });
        var page = RenderMixer();
        await page.Find("[data-testid=expand-drums]").ClickAsync(new());
        await page.Find("[data-testid=mute-drums-snare]").ClickAsync(new());

        await page.Find("[data-testid=mix-save-as]").ClickAsync(new());
        page.Find("[data-testid=mix-name-input]").Input("No snare");
        await page.Find("[data-testid=mix-save-confirm]").ClickAsync(new());

        await Api.Received(1).CreateMixAsync(
            Arg.Is<CreateMixRequest>(r => r.Settings.Expanded.SequenceEqual(new[] { StemNames.Drums })
                && r.Settings.DetailChannels.Count == 6
                && r.Settings.DetailChannels.Single(d => d.Parent == StemNames.Drums && d.Stem == "snare").Mute),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadingAMix_ExpandsBeforeApplyingDetailChannels()
    {
        var page = RenderMixer(TestData.MixId);

        page.Find("[data-testid=group-drums]").ShouldNotBeNull();
        page.Find("[data-testid=expand-drums]").GetAttribute("aria-expanded").ShouldBe("true");
        page.Find("[data-testid=mute-drums-snare]").GetAttribute("aria-pressed").ShouldBe("true");
        page.Find("[data-testid=fader-drums-kick]").GetAttribute("aria-valuenow").ShouldBe("60");
        page.FindAll("[data-testid=unsaved-indicator]").ShouldBeEmpty();
        Received.InOrder(() =>
        {
            Engine.LoadTrackAsync(Arg.Any<StemStudio.Contracts.Tracks.TrackDto>());
            Engine.SetExpandedAsync(StemNames.Drums, true);
            Engine.SetChannelAsync(new ChannelSettingsDto("drums.snare", 0.8, 0, true, false));
        });
        await Engine.DidNotReceive().SetExpandedAsync(StemNames.Vocals, true);
    }

    [Fact]
    public async Task LoadingAnOldMix_CollapsesEverything_AndRestores001Settings()
    {
        var page = RenderMixer(TestData.MixId);
        Engine.ClearReceivedCalls();

        Services.GetRequiredService<NavigationManager>().NavigateTo($"tracks/{TestData.TrackId}/mixer?mixId={OldMixId}");

        page.WaitForAssertion(() => page.FindAll("[data-testid=group-drums]").ShouldBeEmpty());
        await Engine.Received(1).SetExpandedAsync(StemNames.Drums, false);
        await Engine.DidNotReceive().SetExpandedAsync(Arg.Any<string>(), true);
        await Engine.Received(1).SetChannelAsync(new ChannelSettingsDto("drums.snare", 0.8, 0, false, false));
        await Engine.Received(1).SetChannelAsync(new ChannelSettingsDto(StemNames.Guitar, 0, 0, true, false));
        page.Find("[data-testid=expand-drums]").GetAttribute("aria-expanded").ShouldBe("false");
        page.FindAll("[data-testid=unsaved-indicator]").ShouldBeEmpty();
    }

    [Fact]
    public async Task DetailChanges_MarkTheMixDirty()
    {
        var page = RenderMixer(TestData.MixId);

        await page.Find("[data-testid=solo-drums-kick]").ClickAsync(new());

        page.Find("[data-testid=unsaved-indicator]").ShouldNotBeNull();
    }

    [Fact]
    public void RejectedExpandWhileLoadingAMix_ShowsTheError_AndKeepsTheMixerUsable()
    {
        Engine.SetExpandedAsync(StemNames.Drums, true).ThrowsAsync(new AudioEngineException("Failed to load stem \"drums.kick\": HTTP 404"));

        var page = RenderMixer(TestData.MixId);

        page.Find("[data-testid=mixer-engine-error]").TextContent.ShouldContain("drums.kick");
        page.Find("[data-testid=expand-drums]").GetAttribute("aria-expanded").ShouldBe("false");
        page.Find("[data-testid=transport-play]").HasAttribute("disabled").ShouldBeFalse();
    }
}
