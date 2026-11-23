using StemStudio.Web.Experience;
using StemStudio.Web.Help;

namespace StemStudio.Web.Tests.Insights;

/// <summary>
/// Help content of feature 020 (contracts/help-topics.md, help-content 1.2.0, FR-016; T026) on top of the 004 coverage
/// suite: every topic exists in its area, reviewed "020", at its levels, with the facts it must say; the deferred G2
/// topic does not exist.
/// </summary>
public sealed class InsightsHelpTests
{
    private const ExperienceLevel B = ExperienceLevel.Beginner;
    private const ExperienceLevel A = ExperienceLevel.Advanced;
    private const ExperienceLevel E = ExperienceLevel.Expert;

    private static readonly HelpCatalog Catalog = HelpCatalog.FromAssembly(typeof(HelpCatalog).Assembly);

    public static TheoryData<string, string, ExperienceLevel[]> Topics => new()
    {
        { "stem-quality", "insights", [B, A, E] },
        { "stem-quality-tips", "insights", [B, A, E] },
        { "stem-quality-measures", "insights", [A, E] },
        { "stem-quality-reliability", "insights", [A, E] },
        { "handouts", "handouts", [B, A, E] },
        { "handout-families", "handouts", [B, A, E] },
        { "handout-transposition", "handouts", [A, E] },
        { "handout-licence", "handouts", [B, A, E] },
        { "practice-notes", "handouts", [B, A, E] },
    };

    public static TheoryData<string, ExperienceLevel, string> Features => new()
    {
        { "quality.report", B, "stem-quality" },
        { "quality.details", A, "stem-quality-measures" },
        { "quality.indices", E, "stem-quality-reliability" },
        { "handout.one-click", B, "handouts" },
        { "handout.customise", A, "handout-families" },
        { "handout.transposition", A, "handout-transposition" },
        { "practice.notes-edit", B, "practice-notes" },
    };

    [Theory]
    [MemberData(nameof(Topics))]
    public void Topic_IsInItsArea_Reviewed020_AtItsLevels(string id, string area, ExperienceLevel[] levels)
    {
        var topic = Catalog.Topic(id).ShouldNotBeNull(id);

        (topic.Area, topic.Reviewed).ShouldBe((area, "020"));
        topic.Levels.ShouldBe(levels);
    }

    [Theory]
    [MemberData(nameof(Features))]
    public void CatalogueEntry_HasItsMinimumLevel_AndTopic(string id, ExperienceLevel min, string topic)
    {
        var feature = FeatureCatalog.Get(id);

        (feature.Min, feature.Max, feature.TopicId).ShouldBe((min, (ExperienceLevel?)null, topic));
        Catalog.Topic(topic).ShouldNotBeNull();
    }

    [Fact]
    public void DeferredG2_HasNoTopicAndNoEntry()
    {
        Catalog.Topic("reseparate-hq").ShouldBeNull();
        Should.Throw<ArgumentException>(() => FeatureCatalog.Get("reseparation.hq"));
    }

    [Fact]
    public void Areas_ComeAfterInstruments_BeforeTheGlossary() =>
        TopicParser.AreaOrder.ShouldBe(["start", "import", "tracks", "detail", "mixer", "mixes", "instruments", "insights", "handouts", "glossary"]);

    [Fact]
    public void QualityTopic_ExplainsEveryRating_TheEstimate_AndWhenReportsAreMade()
    {
        var body = Catalog.Topic("stem-quality")!.Markdown;

        foreach (var word in new[] { "Good", "Fair", "Poor", "Probably not in this song", "Not rated" })
        {
            body.ShouldContain(word);
        }

        body.ShouldContain("estimate", Case.Insensitive);
        body.ShouldContain("without the original recording");
        body.ShouldContain("Check stem quality");
    }

    [Fact]
    public void TipsTopic_NamesEveryButton_AndWhyThereIsNoGpuTip()
    {
        var body = Catalog.Topic("stem-quality-tips")!.Markdown;

        foreach (var button in new[] { "Split further", "Open detail stem", "Mute" })
        {
            body.ShouldContain(button);
        }

        body.ShouldContain("same models");
        body.ShouldNotContain("Re-separate");
    }

    [Fact]
    public void MeasureTopics_ExplainBleed_Artefacts_KickAndBass_AndTheValidationState()
    {
        var measures = Catalog.Topic("stem-quality-measures")!.Markdown;
        measures.ShouldContain("copy of another stem");
        measures.ShouldContain("flicker");
        measures.ShouldContain("kick and bass", Case.Insensitive);

        var reliability = Catalog.Topic("stem-quality-reliability")!.Markdown;
        reliability.ShouldContain("rough indication");
        reliability.ShouldContain("has not run yet");
        reliability.ShouldContain("analyzer version", Case.Insensitive);
    }

    [Fact]
    public void HandoutsTopic_SaysWhereThePdfGoes_AndHowItDiffersFromTheStandPdf()
    {
        var handouts = Catalog.Topic("handouts")!.Markdown;

        handouts.ShouldContain("library", Case.Insensitive);
        handouts.ShouldContain("Make again");
        handouts.ShouldContain("Print setlist");
        Catalog.Topic("practice-notes")!.Markdown.ShouldContain("one note per family", Case.Insensitive);
    }

    [Theory]
    [InlineData("Band overview")]
    [InlineData("Vocals")]
    [InlineData("Guitar")]
    [InlineData("Bass")]
    [InlineData("Keys")]
    [InlineData("Drums")]
    [InlineData("Melodic instruments")]
    public void FamiliesTopic_HasAParagraphPerFamily_AndNamesMissingParts(string family)
    {
        var families = Catalog.Topic("handout-families")!.Markdown;

        families.ShouldContain($"**{family}**");
        families.ShouldContain("Not included");
    }

    [Fact]
    public void TranspositionAndLicenceTopics_NameTheInstruments_AndTheFixedNotice()
    {
        var transposition = Catalog.Topic("handout-transposition")!.Markdown;
        new[] { "trumpet", "clarinet", "tenor sax", "alto sax", "horn" }.ShouldAllBe(i => transposition.Contains(i));
        transposition.ShouldContain("chords are transposed", Case.Insensitive);

        var licence = Catalog.Topic("handout-licence")!.Markdown;
        licence.ShouldContain(StemStudio.Contracts.ContractLimits.HandoutNoticeFooter);
        licence.ShouldContain("cannot be removed");
        licence.ShouldContain("pictures");
    }

    [Theory]
    [InlineData("track-screen")]
    [InlineData("setlist-edit")]
    [InlineData("library")]
    public void ScreenTopics_LinkToTheNewTopics(string id)
    {
        var related = Catalog.Topic(id)!.Related;

        related.ShouldContain("handouts");
        if (id == "track-screen")
        {
            related.ShouldContain("stem-quality");
        }
    }
}
