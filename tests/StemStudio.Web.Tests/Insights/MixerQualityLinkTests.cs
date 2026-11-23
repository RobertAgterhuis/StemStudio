using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StemStudio.Contracts.Mixes;
using StemStudio.Web.Tests.Fakes;
using StemStudio.Web.Tests.Mixer;

namespace StemStudio.Web.Tests.Insights;

/// <summary>The mixer side of the tip actions (T053, Q2/Q3): <c>?mute=</c> mutes the stem unsaved, <c>?expand=</c> expands the parent.</summary>
public sealed class MixerQualityLinkTests : MixerTestContext
{
    private Bunit.IRenderedComponent<StemStudio.Web.Components.Mixer.Mixer> RenderWith(string query)
    {
        Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().NavigateTo($"tracks/{TestData.TrackId}/mixer?{query}");
        return RenderMixer();
    }

    [Fact]
    public void Mute_MutesTheStemInTheLiveMix_Unsaved()
    {
        var page = RenderWith("mute=piano");

        page.WaitForAssertion(() => page.Find("[data-testid=mute-piano]").GetAttribute("aria-pressed").ShouldBe("true"));
        page.Find("[data-testid=unsaved-indicator]").ShouldNotBeNull();
        Engine.Received().SetChannelAsync(Arg.Is<ChannelSettingsDto>(c => c.Stem == "piano" && c.Mute));
        Api.DidNotReceive().UpdateMixAsync(Arg.Any<Guid>(), Arg.Any<MixSettingsDto>(), Arg.Any<CancellationToken>());
        page.Find("[data-testid=mute-vocals]").GetAttribute("aria-pressed").ShouldBe("false");
    }

    [Fact]
    public void Mute_OfAnUnknownChannel_ChangesNothing()
    {
        var page = RenderWith("mute=theremin");

        page.WaitForAssertion(() => page.Find("[data-testid=mute-piano]"));
        page.FindAll("[data-testid=unsaved-indicator]").ShouldBeEmpty();
    }

    [Fact]
    public void Expand_ExpandsTheSplitParent_OnceTheEngineIsReady()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(TestData.DetailTrack());

        var page = RenderWith("expand=drums");

        page.WaitForAssertion(() => page.Find("[data-testid=group-drums]").ShouldNotBeNull());
        Engine.Received(1).SetExpandedAsync("drums", true);
        page.Find("[data-testid=expand-drums]").GetAttribute("aria-expanded").ShouldBe("true");
    }

    [Fact]
    public void Expand_OfAParentWithoutDetailStems_IsIgnored()
    {
        var page = RenderWith("expand=vocals");

        page.WaitForAssertion(() => page.Find("[data-testid=strip-vocals]"));
        Engine.DidNotReceive().SetExpandedAsync(Arg.Any<string>(), Arg.Any<bool>());
    }
}
