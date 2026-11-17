using Bunit;
using NSubstitute;
using StemStudio.Contracts;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Export;
using StemStudio.Web.Mixer;
using StemStudio.Web.Services;
using StemStudio.Web.Tests.Fakes;
using StemStudio.Web.Tests.Mixer;

namespace StemStudio.Web.Tests.Care;

/// <summary>
/// Detail stems removed by the clean-up (013 T060, FR-025; ui-test-ids 1.12.0 mixer-detail-removed): the mixer loads the
/// parents only, shows the notice and keeps the mix's detail settings when it is saved.
/// </summary>
public sealed class DetailRemovedTests : MixerTestContext
{
    private static readonly DateTimeOffset Removed = new(2026, 11, 17, 11, 0, 0, TimeSpan.Zero);

    private static TrackDto RemovedTrack() => TestData.Track() with { Detail = TestData.CompletedDetail() with { FilesRemovedAt = Removed } };

    private static MixDto DetailMix() => TestData.Mix("No snare") with
    {
        Settings = TestData.Settings("No snare") with
        {
            Expanded = [StemNames.Drums],
            DetailChannels = [new(StemNames.Drums, "snare", 0.8, 0, true, false)],
        },
    };

    public DetailRemovedTests()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(RemovedTrack());
        Api.GetMixAsync(TestData.MixId, Arg.Any<CancellationToken>()).Returns(DetailMix());
    }

    [Fact]
    public void SplitParents_AreEmpty_OnceTheDetailFilesAreRemoved()
    {
        DetailPhaseText.SplitParents(RemovedTrack()).ShouldBeEmpty();
        DetailPhaseText.SplitParents(TestData.DetailTrack()).Count.ShouldBe(2);
        new MixerState(RemovedTrack()).Groups.ShouldBeEmpty();
    }

    [Fact]
    public void Mixer_WithADetailMix_ShowsTheNotice_AndNoDetailGroups()
    {
        var page = RenderMixer(TestData.MixId);

        var notice = page.Find("[data-testid=mixer-detail-removed]");
        notice.TextContent.ShouldContain("Their channels are not loaded; the stems they came from play as usual.");
        notice.GetAttribute("data-help-id").ShouldBe("mixer-detail-removed");
        page.FindAll("[data-testid=group-drums], [data-testid=expand-drums]").ShouldBeEmpty();
        page.Find("[data-testid=fader-drums]").ShouldNotBeNull();
    }

    [Fact]
    public void Mixer_WithoutAMix_HasNoNotice()
    {
        RenderMixer().FindAll("[data-testid=mixer-detail-removed]").ShouldBeEmpty();
    }

    [Fact]
    public void Mixer_WithAMixWithoutDetailSettings_HasNoNotice()
    {
        Api.GetMixAsync(TestData.MixId, Arg.Any<CancellationToken>()).Returns(TestData.Mix());

        RenderMixer(TestData.MixId).FindAll("[data-testid=mixer-detail-removed]").ShouldBeEmpty();
    }

    [Fact]
    public void Saving_KeepsTheDetailSettings_OfTheMix()
    {
        var state = new MixerState(RemovedTrack());
        state.ApplyMix(DetailMix());

        var settings = state.ToSettings("No snare");

        settings.Expanded.ShouldBe([StemNames.Drums]);
        settings.DetailChannels.Single().ShouldBe(new DetailChannelSettingsDto(StemNames.Drums, "snare", 0.8, 0, true, false));
        state.KeepsRemovedDetailSettings.ShouldBeTrue();
    }

    [Fact]
    public void ATrackWithDetailStems_KeepsTheNormalBehaviour()
    {
        var state = new MixerState(TestData.DetailTrack());
        state.ApplyMix(DetailMix());

        state.KeepsRemovedDetailSettings.ShouldBeFalse();
        state.ToSettings("x").DetailChannels.Count.ShouldBe(6);
    }

    [Fact]
    public void StemPack_OffersNoDetailStems_OnceRemoved()
    {
        ExportTarget.ForStemPack(RemovedTrack()).HasDetail.ShouldBeFalse();
        ExportTarget.ForStemPack(TestData.DetailTrack()).HasDetail.ShouldBeTrue();
    }
}
