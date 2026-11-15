using StemStudio.Domain.Mixes;
using StemStudio.Domain.Tracks;
using static StemStudio.Domain.Tests.Tracks.DetailTestData;

namespace StemStudio.Domain.Tests.Mixes;

/// <summary>Expansion and detail channel settings of mixes (FR-015, SC-005).</summary>
public sealed class MixDetailTests
{
    [Fact]
    public void Create_StoresExpansionAndDetailChannels()
    {
        var track = DetailCompleted();

        var mix = Mix.Create(Guid.NewGuid(), track, WithDetail(), T0);

        mix.Expanded.ShouldBe(["drums"]);
        mix.DetailChannels.ShouldBe([Snare(mute: true)]);
        mix.Settings.Expanded.ShouldBe(["drums"]);
        mix.Settings.DetailChannels.ShouldBe(mix.DetailChannels);
    }

    [Fact]
    public void OldMixesWithoutDetailSettingsStayValid()
    {
        var settings = MixTests.Settings();

        settings.Expanded.ShouldBeEmpty();
        settings.DetailChannels.ShouldBeEmpty();
        settings.Validate(CompletedTrack()).ShouldBeEmpty();
        settings.Validate(DetailCompleted()).ShouldBeEmpty();
    }

    [Fact]
    public void DetailChannelDefaults_MatchTheDataModel()
    {
        var channel = DetailChannelSettings.Default("drums", "kick");

        (channel.Parent, channel.Stem, channel.Volume, channel.Pan, channel.Mute, channel.Solo).ShouldBe(("drums", "kick", 0.8, 0.0, false, false));
    }

    [Theory]
    [InlineData("guitar")]
    [InlineData("bass")]
    [InlineData("unknown")]
    public void Expanded_RequiresASplitParent(string parent)
    {
        var settings = MixTests.Settings() with { Expanded = [parent] };

        Fields(settings, DetailCompleted()).ShouldBe(["expanded[0]"]);
    }

    [Fact]
    public void Expanded_IsInvalidWhenTheTrackHasNoCompletedDetailPhase()
    {
        var settings = MixTests.Settings() with { Expanded = ["drums"] };

        Fields(settings, CompletedTrack()).ShouldBe(["expanded[0]"]);
        Fields(settings, Requested()).ShouldBe(["expanded[0]"]);
    }

    [Fact]
    public void Expanded_MustBeUnique()
    {
        var settings = MixTests.Settings() with { Expanded = ["drums", "vocals", "drums"] };

        Fields(settings, DetailCompleted()).ShouldBe(["expanded[2]"]);
    }

    public static TheoryData<DetailChannelSettings, string> InvalidChannels() => new()
    {
        { Snare() with { Stem = "toms" }, "detailChannels[0].stem" },
        { Snare() with { Stem = "lead" }, "detailChannels[0].stem" },
        { Snare() with { Parent = "guitar", Stem = "strings" }, "detailChannels[0].parent" },
        { Snare() with { Parent = "bass", Stem = "rest" }, "detailChannels[0].parent" },
        { Snare() with { Volume = 1.5 }, "detailChannels[0].volume" },
        { Snare() with { Volume = double.NaN }, "detailChannels[0].volume" },
        { Snare() with { Pan = -1.1 }, "detailChannels[0].pan" },
    };

    [Theory]
    [MemberData(nameof(InvalidChannels))]
    public void DetailChannels_AreValidatedAgainstTheTrack(DetailChannelSettings channel, string field)
    {
        var settings = MixTests.Settings() with { DetailChannels = [channel] };

        Fields(settings, DetailCompleted()).ShouldBe([field]);
    }

    [Fact]
    public void DetailChannels_AtMostOnePerParentAndStem()
    {
        var settings = MixTests.Settings() with { DetailChannels = [Snare(), Snare(mute: true), Snare() with { Parent = "vocals", Stem = "rest" }] };

        Fields(settings, DetailCompleted()).ShouldBe(["detailChannels[1].stem"]);
    }

    [Fact]
    public void DetailChannels_AreKeptForCollapsedParents()
    {
        var settings = MixTests.Settings() with { Expanded = [], DetailChannels = [Snare(mute: true)] };

        Fields(settings, DetailCompleted()).ShouldBeEmpty();
    }

    [Fact]
    public void Update_ReplacesExpansionAndDetailChannels()
    {
        var track = DetailCompleted();
        var mix = Mix.Create(Guid.NewGuid(), track, WithDetail(), T0);

        mix.Update(track, MixTests.Settings() with { Expanded = ["vocals"] }, T0.AddMinutes(1));

        mix.Expanded.ShouldBe(["vocals"]);
        mix.DetailChannels.ShouldBeEmpty();
    }

    [Fact]
    public void Update_WithInvalidDetailSettingsLeavesTheMixUnchanged()
    {
        var track = DetailCompleted();
        var mix = Mix.Create(Guid.NewGuid(), track, WithDetail(), T0);

        Should.Throw<DomainValidationException>(() => mix.Update(track, WithDetail() with { Expanded = ["piano"] }, T0));

        mix.Expanded.ShouldBe(["drums"]);
    }

    [Fact]
    public void Duplicate_CopiesExpansionAndDetailChannels()
    {
        var mix = Mix.Create(Guid.NewGuid(), DetailCompleted(), WithDetail(), T0);

        var copy = mix.Duplicate(Guid.NewGuid(), [mix.Name], T0.AddMinutes(1));

        copy.Expanded.ShouldBe(mix.Expanded);
        copy.DetailChannels.ShouldBe(mix.DetailChannels);
    }

    private static MixSettings WithDetail() => MixTests.Settings("No snare") with { Expanded = ["drums"], DetailChannels = [Snare(mute: true)] };

    private static DetailChannelSettings Snare(bool mute = false) => new() { Parent = "drums", Stem = "snare", Volume = 0.8, Pan = 0, Mute = mute, Solo = false };

    private static List<string> Fields(MixSettings settings, Track track) => settings.Validate(track).Select(e => e.Field).ToList();
}
