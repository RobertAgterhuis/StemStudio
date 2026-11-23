using StemStudio.Contracts.Insights;
using StemStudio.Web.Insights;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Insights;

/// <summary>Display rules of the quality report (T046, R20-1, R20-3, data-model 4.3).</summary>
public sealed class QualityTextTests
{
    [Theory]
    [InlineData(QualityRatingDto.Good, "Good", "check_circle")]
    [InlineData(QualityRatingDto.Fair, "Fair", "info")]
    [InlineData(QualityRatingDto.Poor, "Poor", "warning")]
    [InlineData(QualityRatingDto.NotPresent, "Probably not in this song", "block")]
    [InlineData(QualityRatingDto.Unknown, "Not rated", "help_outline")]
    public void EveryRating_HasAWordAndItsOwnIcon(QualityRatingDto rating, string word, string icon) =>
        (QualityText.Rating(rating), QualityText.Icon(rating)).ShouldBe((word, icon));

    [Theory]
    [InlineData("vocals", "Vocals", "vocals")]
    [InlineData("drums.kick", "Kick (drums)", "drums-kick")]
    [InlineData("guitar.rest", "Rest (guitar)", "guitar-rest")]
    public void Channels_HaveALabel_AndADotFreeKey(string channel, string label, string key) =>
        (QualityText.Channel(channel), QualityText.Key(channel)).ShouldBe((label, key));

    [Fact]
    public void AriaLabel_WithoutReason_IsChannelAndRating() =>
        QualityText.AriaLabel(InsightsData.Stem("bass", QualityRatingDto.Unknown) with { Reason = " " }).ShouldBe("Bass: not rated");

    [Fact]
    public void Bleed_NamesTheCoOccurrenceCap_AndOmitsAMissingSource()
    {
        var bleed = InsightsData.ReadyReport().Stems[0].Bleed;

        QualityText.Bleed(bleed with { MainSource = "bass", CoOccurrence = true })
            .ShouldBe("Bleed: medium, mostly bass (may be the two instruments playing together)");
        QualityText.Bleed(bleed with { MainSource = null, Level = QualityLevelDto.Low }).ShouldBe("Bleed: low");
    }

    [Fact]
    public void Artefacts_NameEveryKind()
    {
        var artefacts = InsightsData.ReadyReport().Stems[0].Artefacts with
        {
            Level = QualityLevelDto.High,
            Kinds = [ArtefactKindDto.Flicker, ArtefactKindDto.Dropouts, ArtefactKindDto.ModelResidual, ArtefactKindDto.RestShare],
        };

        QualityText.Artefacts(artefacts).ShouldBe("Artefacts: high (flicker, dropouts, left-over of the detail model, much went to the rest stem)");
    }

    [Fact]
    public void Analyzer_IsNull_WithoutMeasuredStems() =>
        QualityText.Analyzer(InsightsData.NoReport()).ShouldBeNull();

    [Fact]
    public void Layout_PutsAChildWithoutItsParentAtTheEnd_AndFindsByChannel()
    {
        var orphan = InsightsData.Stem("guitar.lead", QualityRatingDto.Fair, "guitar");
        var report = InsightsData.WithStems(orphan);

        var groups = QualityLayout.Group(report.Stems);

        groups.Select(g => g.Stem.Channel).ShouldBe(["other", "piano", "guitar.lead"]);
        groups[^1].Children.ShouldBeEmpty();
        QualityLayout.Find(report, "piano")!.Rating.ShouldBe(QualityRatingDto.NotPresent);
        QualityLayout.Find(null, "piano").ShouldBeNull();
    }

    [Theory]
    [InlineData(QualityReportStatusDto.None, false, "Check stem quality")]
    [InlineData(QualityReportStatusDto.Queued, true, "Check again")]
    [InlineData(QualityReportStatusDto.Running, true, "Check again")]
    [InlineData(QualityReportStatusDto.Ready, false, "Check again")]
    [InlineData(QualityReportStatusDto.Failed, false, "Check again")]
    public void Status_DecidesPollingAndTheButtonText(QualityReportStatusDto status, bool active, string text) =>
        (QualityLayout.IsActive(status), QualityLayout.CheckText(status)).ShouldBe((active, text));
}
