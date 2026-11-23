using StemStudio.Domain.InstrumentViews;

namespace StemStudio.Domain.Tests.InstrumentViews;

/// <summary>Feature 016 T045: the family of every channel id (data-model 5.3, FR-060).</summary>
public sealed class FamilyRuleTests
{
    [Theory]
    [InlineData("vocals", InstrumentFamily.Vocals)]
    [InlineData("vocals.lead", InstrumentFamily.Vocals)]
    [InlineData("vocals.backing", InstrumentFamily.Vocals)]
    [InlineData("vocals.rest", InstrumentFamily.Vocals)]
    [InlineData("drums", InstrumentFamily.Drums)]
    [InlineData("drums.kick", InstrumentFamily.Drums)]
    [InlineData("drums.snare", InstrumentFamily.Drums)]
    [InlineData("drums.toms", InstrumentFamily.Drums)]
    [InlineData("drums.hihat", InstrumentFamily.Drums)]
    [InlineData("drums.cymbals", InstrumentFamily.Drums)]
    [InlineData("drums.rest", InstrumentFamily.Drums)]
    [InlineData("other.percussion", InstrumentFamily.Drums)]
    [InlineData("bass", InstrumentFamily.Bass)]
    [InlineData("guitar", InstrumentFamily.Guitar)]
    [InlineData("guitar.acoustic-guitar", InstrumentFamily.Guitar)]
    [InlineData("other.electric-guitar", InstrumentFamily.Guitar)]
    [InlineData("guitar.rest", InstrumentFamily.Guitar)]
    [InlineData("guitar.strings", InstrumentFamily.Orchestral)]
    [InlineData("piano", InstrumentFamily.Keys)]
    [InlineData("piano.keys", InstrumentFamily.Keys)]
    [InlineData("other.synth", InstrumentFamily.Keys)]
    [InlineData("piano.rest", InstrumentFamily.Keys)]
    [InlineData("piano.brass", InstrumentFamily.Orchestral)]
    [InlineData("other", InstrumentFamily.Orchestral)]
    [InlineData("other.strings", InstrumentFamily.Orchestral)]
    [InlineData("other.brass", InstrumentFamily.Orchestral)]
    [InlineData("other.woodwinds", InstrumentFamily.Orchestral)]
    [InlineData("other.rest", InstrumentFamily.Orchestral)]
    public void EveryChannel_BelongsToItsFamily(string channel, InstrumentFamily family) => FamilyRule.FamilyOf(channel).ShouldBe(family);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("kazoo")]
    [InlineData("other.kazoo")]
    [InlineData("kazoo.rest")]
    public void UnknownChannels_HaveNoFamily(string? channel) => FamilyRule.FamilyOf(channel).ShouldBeNull();

    [Fact]
    public void Families_HaveTheirLabelsStemsAndViews()
    {
        FamilyRule.Families.Select(f => f.Id).ShouldBe(Enum.GetValues<InstrumentFamily>());
        FamilyRule.InfoOf(InstrumentFamily.Orchestral).ShouldSatisfyAllConditions(
            info => info.Label.ShouldBe("Strings, brass and woodwinds"),
            info => info.Stems.ShouldBe(["other"]),
            info => info.DetailNames.ShouldBe(["strings", "brass", "woodwinds"]),
            info => info.View.ShouldBe(InstrumentView.Part));
        FamilyRule.InfoOf(InstrumentFamily.Drums).DetailNames.ShouldBe(["kick", "snare", "toms", "hihat", "cymbals", "percussion"]);
        FamilyRule.Families.Select(f => f.View).ShouldBe([InstrumentView.Singing, InstrumentView.DrumTrainer, InstrumentView.Tablature, InstrumentView.Tablature, InstrumentView.Chords, InstrumentView.Part]);
    }
}
