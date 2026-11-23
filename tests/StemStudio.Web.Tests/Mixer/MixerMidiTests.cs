using Bunit;
using NSubstitute;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Mixer;

/// <summary>The MIDI action on every mixer strip, standard and detail (T081, FR-023, ui-test-ids midi-&lt;key&gt;).</summary>
public sealed class MixerMidiTests : MixerTestContext
{
    [Fact]
    public void EveryStandardStrip_HasAMidiAction()
    {
        var page = RenderMixer();

        foreach (var key in new[] { "vocals", "drums", "bass", "other" })
        {
            page.Find($"[data-testid=strip-{key}] [data-testid=midi-{key}]").TextContent.ShouldContain("MIDI");
        }
    }

    [Fact]
    public async Task ExpandedDetailStrips_HaveOneToo_WithDashedKeys()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(TestData.DetailTrack());
        var page = RenderMixer();

        await page.Find("[data-testid=expand-drums]").ClickAsync(new());

        page.Find("[data-testid=group-drums] [data-testid=midi-drums-kick]").GetAttribute("aria-label").ShouldBe("MIDI of kick");
    }
}
