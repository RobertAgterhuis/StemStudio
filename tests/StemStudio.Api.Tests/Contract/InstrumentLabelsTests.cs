using StemStudio.Contracts;
using StemStudio.Contracts.Library;

namespace StemStudio.Api.Tests.Contract;

/// <summary>
/// The instrument word catalogue of feature 022 (architect, Phase 0 T003, research R-08): closed over every stem and detail id,
/// the same structure for every family (MoSCoW "every instrument counts equally").
/// </summary>
public sealed class InstrumentLabelsTests
{
    public static TheoryData<string> AllChannelIds =>
        [.. StemNames.SixStem, .. DetailNames.Parents.SelectMany(parent => DetailNames.AllowedFor(parent).Select(detail => $"{parent}.{detail}"))];

    [Fact]
    public void Families_AreTheSixStemsInDisplayOrder()
    {
        InstrumentLabels.Families.Select(family => family.Stem).ShouldBe(StemNames.SixStem);
    }

    [Theory]
    [MemberData(nameof(AllChannelIds))]
    public void EveryStemAndDetailId_HasAShortLabel(string channelId)
    {
        InstrumentLabels.TryGetLabel(channelId, out var label).ShouldBeTrue();
        label.ShouldNotBeNullOrWhiteSpace();
        label.Length.ShouldBeLessThanOrEqualTo(ContractLimits.InstrumentLabelMaxLength);
        InstrumentLabels.FamilyOf(channelId)!.Stem.ShouldBe(channelId.Split('.')[0]);
    }

    [Fact]
    public void Channels_AreExactlyTheStemAndDetailIds()
    {
        var expected = StemNames.SixStem.Concat(DetailNames.Parents.SelectMany(parent => DetailNames.AllowedFor(parent).Select(detail => $"{parent}.{detail}")));

        InstrumentLabels.Channels.Select(channel => channel.ChannelId).ShouldBe(expected, ignoreOrder: true);
        InstrumentLabels.Channels.Select(channel => channel.ChannelId).ShouldBeUnique();
    }

    [Fact]
    public void EveryFamily_HasTheSameNumberOfAliases()
    {
        InstrumentLabels.Families.Select(family => family.Aliases.Count).Distinct().ShouldHaveSingleItem().ShouldBeGreaterThan(0);
        InstrumentLabels.Families.SelectMany(family => family.Aliases).ShouldBeUnique();
    }

    [Fact]
    public void Details_FollowTheAllowedOrderWithRestLast()
    {
        foreach (var family in InstrumentLabels.Families)
        {
            family.Details.Select(channel => channel.ChannelId).ShouldBe(DetailNames.AllowedFor(family.Stem).Select(detail => $"{family.Stem}.{detail}"));
        }

        InstrumentLabels.Families.Single(family => family.Stem == StemNames.Bass).Details.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("drums.snare", "Snare")]
    [InlineData("drums.hihat", "Hi-hat")]
    [InlineData("vocals.lead", "Lead vocals")]
    [InlineData("vocals.rest", "Other vocals")]
    [InlineData("guitar.acoustic-guitar", "Acoustic guitar")]
    [InlineData("piano.keys", "Keys")]
    [InlineData("other.strings", "Strings")]
    [InlineData("other.rest", "Other sounds")]
    [InlineData("bass", "Bass")]
    public void Labels_AreTheEverydayNames(string channelId, string label)
    {
        InstrumentLabels.TryGetLabel(channelId, out var actual).ShouldBeTrue();
        actual.ShouldBe(label);
    }

    [Theory]
    [InlineData("drums.snare", new[] { "drums snare", "snare", "drum", "drum kit", "percussion kit" })]
    [InlineData("guitar.acoustic-guitar", new[] { "guitar acoustic guitar", "acoustic guitar", "guitars", "gtr", "guitar part" })]
    [InlineData("bass", new[] { "bass", "bass guitar", "bassline", "low end" })]
    public void SearchWords_AreIdWordsLabelAndFamilyAliases(string channelId, string[] words)
    {
        InstrumentLabels.SearchWordsOf(channelId).ShouldBe(words);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bass.kick")]
    [InlineData("drums.bass")]
    [InlineData("DRUMS")]
    [InlineData("mix")]
    public void UnknownIds_HaveNoLabelFamilyOrWords(string? channelId)
    {
        InstrumentLabels.TryGetLabel(channelId, out var label).ShouldBeFalse();
        label.ShouldBeNull();
        InstrumentLabels.FamilyOf(channelId).ShouldBeNull();
        InstrumentLabels.SearchWordsOf(channelId).ShouldBeEmpty();
    }

    [Fact]
    public void Mix_IsALabel()
    {
        InstrumentLabels.Mix.ShouldBe("Mix");
    }
}
