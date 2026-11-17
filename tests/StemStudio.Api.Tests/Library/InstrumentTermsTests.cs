using StemStudio.Api.Library;
using StemStudio.Contracts.Library;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Mixes;
using StemStudio.Domain.Rendering;
using StemStudio.Domain.Scores;
using static StemStudio.Api.Tests.Library.SnapshotBuilder;

namespace StemStudio.Api.Tests.Library;

/// <summary>022 T060: instrument words of an item (research R-08, FR-024, FR-036).</summary>
public sealed class InstrumentTermsTests
{
    private static readonly Guid TrackA = Guid.Parse("0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a");

    private static InstrumentTerms Of(Asset asset) => InstrumentTerms.Of(asset);

    [Theory]
    [InlineData("drums.snare", "Snare", "drums snare")]
    [InlineData("guitar.acoustic-guitar", "Acoustic guitar", "guitar acoustic guitar")]
    [InlineData("vocals.lead", "Lead vocals", "vocals lead")]
    [InlineData("bass", "Bass", "bass")]
    public void Channel_IdWordsAndLabel(string channel, string label, string idWords)
    {
        var term = Of(new SnapshotBuilder().Export(TrackA, Imported("a", "b"), channel)).Terms.Single();

        term.Label.ShouldBe(label);
        term.Words.ShouldContain(idWords);
        term.Words.ShouldContain(label.ToLowerInvariant());
    }

    [Fact]
    public void EveryFamily_HasTheSameNumberOfAliases_OnStemsAndDetails()
    {
        foreach (var family in InstrumentLabels.Families)
        {
            var builder = new SnapshotBuilder();
            var stem = Of(builder.Export(TrackA, Imported("a", "b"), family.Stem)).Terms.Single();
            stem.Aliases.Count.ShouldBe(3, family.Stem);
            foreach (var detail in family.Details)
            {
                Of(builder.Export(TrackA, Imported("a", "b"), detail.ChannelId)).Terms.Single().Aliases.Count.ShouldBe(3, detail.ChannelId);
            }
        }
    }

    [Fact]
    public void Mix_IndexesEveryChannelPlusMix()
    {
        var terms = Of(new SnapshotBuilder().Mix(TrackA, Imported("a", "b"), "bass", "drums.kick", "unknown"));

        terms.Terms.Select(term => term.Label).ShouldBe(["Kick", "Bass", InstrumentLabels.Mix]);
        terms.Group.ShouldBe(2);
    }

    [Fact]
    public void Mix_WithoutChannels_UsesThePlan()
    {
        var recipe = new ExportRecipe
        {
            TrackId = TrackA,
            Mode = ExportMode.Mix,
            Format = new ExportFormat(AudioFormat.Flac, 16, null),
            Mix = new ExportMix(1, [ChannelSettings.Default("vocals")]) { DetailChannels = [DetailChannelSettings.Default("drums", "snare")] },
        };
        var asset = Asset.Create(Guid.NewGuid(), "mix", LibraryTestData.ExportProvenance(TrackA) with { Recipe = recipe }, LibraryTestData.T0);

        Of(asset).Terms.Select(term => term.Label).ShouldBe(["Vocals", "Snare", InstrumentLabels.Mix]);
    }

    [Fact]
    public void Mix_WithoutChannelsOrPlan_IsOnlyMix()
    {
        var recipe = new ExportRecipe { TrackId = TrackA, Mode = ExportMode.Mix, Format = new ExportFormat(AudioFormat.Flac, 16, null) };
        var asset = Asset.Create(Guid.NewGuid(), "mix", LibraryTestData.ExportProvenance(TrackA) with { Recipe = recipe }, LibraryTestData.T0);

        Of(asset).Terms.Single().Label.ShouldBe(InstrumentLabels.Mix);
    }

    [Theory]
    [InlineData(ScoreProfile.Keys, "Keys", "piano keys")]
    [InlineData(ScoreProfile.Drums, "Drums", "drums")]
    [InlineData(ScoreProfile.Vocals, "Vocals", "vocals")]
    [InlineData(ScoreProfile.Guitar, "Guitar", "guitar")]
    [InlineData(ScoreProfile.Piano, "Piano", "piano")]
    [InlineData(ScoreProfile.Bass, "Bass", "bass")]
    public void Score_UsesItsProfile(ScoreProfile profile, string label, string idWords)
    {
        var terms = Of(new SnapshotBuilder().Score(TrackA, Imported("a", "b"), profile));

        (terms.Terms.Single().Label, terms.Group).ShouldBe((label, 3));
        terms.Terms.Single().Words.ShouldContain(idWords);
    }

    [Fact]
    public void GeneratedTake_IsMix()
    {
        var terms = Of(new SnapshotBuilder().Generated("Zomeravond"));

        (terms.Terms.Single().Label, terms.Group, terms.Terms.Single().Aliases.Count).ShouldBe((InstrumentLabels.Mix, 4, 0));
    }

    [Fact]
    public void MixerOrder_StemsBeforeDetails()
    {
        var builder = new SnapshotBuilder();
        var (drums, bass, snare) = (Of(builder.Export(TrackA, Imported("a", "b"), "drums")), Of(builder.Export(TrackA, Imported("a", "b"), "bass")), Of(builder.Export(TrackA, Imported("a", "b"), "drums.snare")));

        (drums.Group, bass.Group, snare.Group).ShouldBe((0, 0, 1));
        drums.Order.ShouldBeLessThan(bass.Order);
    }
}
