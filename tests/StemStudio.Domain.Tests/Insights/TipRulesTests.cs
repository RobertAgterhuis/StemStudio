using StemStudio.Domain.Insights;
using StemStudio.Domain.Tracks.Detail;

namespace StemStudio.Domain.Tests.Insights;

/// <summary>Feature 020 T049: the tip rules of data-model 5.3 (order, at most two, actions, no GPU tip, no G2 tip).</summary>
public sealed class TipRulesTests
{
    private static readonly DateTime T0 = new(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ANotPresentStem_IsMuted()
    {
        var tips = TipRules.For(Stem("piano", QualityRating.NotPresent), Context());

        tips.Single().ShouldBe(new Tip(TipCode.NotInSong, "Mute this stem; the instrument is probably not in this song.", new TipAction(TipActionKind.Mute, "piano", null, null)));
    }

    [Theory]
    [InlineData("vocals")]
    [InlineData("drums")]
    [InlineData("guitar")]
    [InlineData("piano")]
    [InlineData("other")]
    public void AFairOrPoorSplittableStem_WithoutDetail_GetsSplitFurther(string channel)
    {
        var tip = TipRules.For(Stem(channel, QualityRating.Fair), Context())[0];

        tip.ShouldBe(new Tip(TipCode.TryDetailSeparation, "Split further (detail separation) may pull apart the instruments mixed into this stem.", new TipAction(TipActionKind.SplitDetail, channel, channel, null)));
    }

    [Theory]
    [InlineData(DetailPhaseStatus.Failed)]
    [InlineData(DetailPhaseStatus.Cancelled)]
    public void SplitFurther_IsOfferedAgainAfterAFailedOrCancelledSplit(DetailPhaseStatus status)
    {
        var detail = Phase(status, []);

        TipRules.For(Stem("other", QualityRating.Poor), Context(detail))[0].Code.ShouldBe(TipCode.TryDetailSeparation);
    }

    [Fact]
    public void SplitFurther_IsOfferedWhenTheDetailFilesWereRemoved_ButNotWhileASplitRunsOrExists()
    {
        var removed = Phase(DetailPhaseStatus.Completed, []) with { FilesRemovedAt = T0 };

        TipRules.For(Stem("other", QualityRating.Poor), Context(removed))[0].Code.ShouldBe(TipCode.TryDetailSeparation);
        TipRules.For(Stem("other", QualityRating.Poor), Context(Phase(DetailPhaseStatus.Running, []))).Select(tip => tip.Code).ShouldBe([TipCode.HardToIsolate]);
        TipRules.For(Stem("other", QualityRating.Poor), Context(Phase(DetailPhaseStatus.Completed, [Unsplit("other")]))).Select(tip => tip.Code).ShouldBe([TipCode.HardToIsolate]);
    }

    [Fact]
    public void BassIsNotSplittable_SoAPoorBassIsHardToIsolate()
    {
        var tips = TipRules.For(Stem("bass", QualityRating.Poor), Context());

        tips.Single().ShouldBe(new Tip(TipCode.HardToIsolate, "Bass is hard to isolate with today's models; this is close to the best StemStudio can do.", null));
    }

    [Fact]
    public void AFairBass_GetsNoTip_ButAFairGuitarAfterASplitIsHardToIsolate()
    {
        TipRules.For(Stem("bass", QualityRating.Fair), Context()).ShouldBeEmpty();
        TipRules.For(Stem("guitar", QualityRating.Fair), Context(Phase(DetailPhaseStatus.Completed, [Unsplit("guitar")]))).Single().Code.ShouldBe(TipCode.HardToIsolate);
    }

    [Fact]
    public void ABetterKeptChild_IsSuggested_AndBlocksHardToIsolate()
    {
        var detail = Phase(DetailPhaseStatus.Completed, [Split("guitar", "electric-guitar", "acoustic-guitar", "rest")]);
        var stems = new[]
        {
            Stem("guitar", QualityRating.Poor), Stem("guitar.electric-guitar", QualityRating.Fair), Stem("guitar.acoustic-guitar", QualityRating.Good), Stem("guitar.rest", QualityRating.Good),
        };

        var tips = TipRules.For(stems[0], new TipContext(stems, detail));

        tips.Single().ShouldBe(new Tip(TipCode.UseDetailStem, "Use the acoustic guitar detail stem instead.", new TipAction(TipActionKind.OpenDetail, "guitar", "guitar", "acoustic-guitar")));
    }

    [Fact]
    public void NoBetterChild_MeansNoDetailTip()
    {
        var detail = Phase(DetailPhaseStatus.Completed, [Split("drums", "kick", "rest")]);
        var stems = new[] { Stem("drums", QualityRating.Fair), Stem("drums.kick", QualityRating.Fair), Stem("drums.rest", QualityRating.Good) };

        TipRules.For(stems[0], new TipContext(stems, detail)).ShouldBeEmpty();
        TipRules.For(stems[0], new TipContext([stems[0]], detail)).ShouldBeEmpty("children not measured yet");
    }

    [Fact]
    public void ADetailStemWithRestShare_PointsToTheRestStem_AndIsHardToIsolateWhenPoor()
    {
        var stem = Stem("other.flute", QualityRating.Poor) with { Artefacts = new ArtefactEstimate(QualityLevel.High, 0.7, [ArtefactKind.RestShare], Reliability.Indicative) };

        var tips = TipRules.For(stem, Context());

        tips.ShouldBe([
            new Tip(TipCode.ListenToRest, "Part of this sound went to the 'rest' stem; listen to it too.", new TipAction(TipActionKind.OpenDetail, "other.flute", "other", "rest")),
            new Tip(TipCode.HardToIsolate, "Flute is hard to isolate with today's models; this is close to the best StemStudio can do.", null),
        ]);
    }

    [Fact]
    public void TheRestStemItself_GetsNoListenToRestTip()
    {
        var rest = Stem("drums.rest", QualityRating.Fair) with { Artefacts = new ArtefactEstimate(QualityLevel.Medium, 0.4, [ArtefactKind.RestShare], Reliability.Indicative) };

        TipRules.For(rest, Context()).ShouldBeEmpty();
    }

    [Fact]
    public void AGoodStem_IsClean_AndUnknownGetsNothing()
    {
        TipRules.For(Stem("vocals", QualityRating.Good), Context()).Single().ShouldBe(new Tip(TipCode.Clean, "Clean enough to practise with.", null));
        TipRules.For(Stem("vocals", QualityRating.Unknown), Context()).ShouldBeEmpty();
    }

    [Fact]
    public void AtMostTwoTips_InRuleOrder()
    {
        var stem = Stem("guitar.electric-guitar", QualityRating.NotPresent) with { Artefacts = new ArtefactEstimate(QualityLevel.High, 0.7, [ArtefactKind.RestShare], Reliability.Indicative) };

        TipRules.For(stem, Context()).Select(tip => tip.Code).ShouldBe([TipCode.NotInSong, TipCode.ListenToRest]);
    }

    [Fact]
    public void NoTipText_MentionsTheGpuOrHighQualityReseparation()
    {
        var detail = Phase(DetailPhaseStatus.Completed, [Split("vocals", "lead", "backing", "rest")]);
        var ratings = Enum.GetValues<QualityRating>();
        var channels = new[] { "vocals", "drums", "bass", "guitar", "piano", "other", "vocals.lead", "vocals.rest" };
        var texts = channels.SelectMany(channel => ratings.SelectMany(rating => new[] { Context(), Context(detail) }
            .SelectMany(context => TipRules.For(Stem(channel, rating), context)))).Select(tip => tip.Text).ToList();

        texts.ShouldNotBeEmpty();
        texts.ShouldAllBe(text => !text.Contains("GPU", StringComparison.OrdinalIgnoreCase) && !text.Contains("re-separate", StringComparison.OrdinalIgnoreCase) && text.Length <= 300);
    }

    [Fact]
    public void NullArguments_AreRefused()
    {
        Should.Throw<ArgumentNullException>(() => TipRules.For(null!, Context()));
        Should.Throw<ArgumentNullException>(() => TipRules.For(Stem("vocals", QualityRating.Good), null!));
    }

    private static TipContext Context(DetailPhase? detail = null) => new([], detail);

    private static DetailPhase Phase(DetailPhaseStatus status, IReadOnlyList<ParentOutcome> parents) =>
        new() { Status = status, Parents = parents, RequestedAt = T0, UpdatedAt = T0 };

    private static ParentOutcome Split(string parent, params string[] names) => new(parent, ParentOutcomeKind.Split, names.Select(name => new DetailStem(name, 10)).ToList());

    private static ParentOutcome Unsplit(string parent) => new(parent, ParentOutcomeKind.NothingFurther, []);

    private static StemQuality Stem(string channel, QualityRating rating) => new(
        channel,
        rating,
        QualityReason.Clean,
        100,
        0.5,
        -6,
        new BleedEstimate(QualityLevel.Low, 0.1, null, false, Reliability.Indicative),
        new ArtefactEstimate(QualityLevel.Low, 0.1, [], Reliability.Indicative));
}
