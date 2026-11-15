using StemStudio.Domain.Lyrics;

namespace StemStudio.Domain.Tests.Lyrics;

/// <summary>010 T014: own lyrics sheets, normalisation, sections and density (FR-001, FR-002, FR-003).</summary>
public sealed class LyricsSheetTests
{
    private static readonly DateTime T0 = new(2026, 11, 15, 13, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Normalize_ContractExample() =>
        LyricsNormaliser.Normalize("[verse]\r\nSing it loud, sing it free   \r\nEvery note is part of me\r\n\r\n\r\n\r\n[chorus]\r\nThis little song belongs to me")
            .ShouldBe("[verse]\nSing it loud, sing it free\nEvery note is part of me\n\n\n[chorus]\nThis little song belongs to me");

    [Fact]
    public void Normalize_StripsLrcTimestampsAndTags() =>
        LyricsNormaliser.Normalize("[ar:Somebody]\n[ti:Song]\n[00:01.00]Paper boats along the gutter\n[00:04.50]racing past the corner shop\n[00:15.00]\n[00:16.00][00:40.12]Sing it loud and sing it slow\r[01:02]let the little rivers go\n\n")
            .ShouldBe("Paper boats along the gutter\nracing past the corner shop\n\nSing it loud and sing it slow\nlet the little rivers go");

    [Fact]
    public void Normalize_QaLrcFixture() =>
        LyricsNormaliser.Normalize("[00:01.00]Paper boats along the gutter\n[00:04.50]racing past the corner shop\n[00:08.00]every puddle is an ocean\n[00:11.50]and the rain will never stop\n[00:15.00]\n[00:16.00]Sing it loud and sing it slow\n[00:19.50]let the little rivers go\n")
            .ShouldBe("Paper boats along the gutter\nracing past the corner shop\nevery puddle is an ocean\nand the rain will never stop\n\nSing it loud and sing it slow\nlet the little rivers go");

    [Fact]
    public void Normalize_KeepsUnknownBracketsAndNull() =>
        LyricsNormaliser.Normalize("  [spoken] hello  \n\tline").ShouldBe("  [spoken] hello\n\tline");

    [Fact]
    public void Create_ValidatesTheNormalisedText()
    {
        var sheet = LyricsSheet.Create(Guid.NewGuid(), new LyricsContent(" This little song ", "en", "a\r\nb   "), T0);

        (sheet.Title, sheet.Language, sheet.Text, sheet.CreatedAt, sheet.UpdatedAt).ShouldBe(("This little song", "en", "a\nb", T0, T0));
        sheet.Snapshot().ShouldBe(new LyricsSnapshot(sheet.Id, "This little song", "en", "a\nb"));
    }

    [Theory]
    [InlineData("", "en", "x", "title")]
    [InlineData("t", "xx", "x", "language")]
    [InlineData("t", "en", "[00:01.00]", "text")]
    public void Create_RejectsInvalidFields(string title, string language, string text, string field) =>
        Should.Throw<DomainValidationException>(() => LyricsSheet.Create(Guid.NewGuid(), new LyricsContent(title, language, text), T0))
            .Errors.ShouldContain(e => e.Field == field);

    [Fact]
    public void Create_TextLongerThan4096AfterNormalisationIsRefused()
    {
        LyricsRules.Validate(new LyricsContent("t", "en", new string('a', 4096))).ShouldBeEmpty();
        LyricsRules.Validate(new LyricsContent("t", "en", new string('a', 4097))).Single().Field.ShouldBe("text");
        LyricsRules.Validate(new LyricsContent(new string('t', 81), "unknown", "x")).Single().Field.ShouldBe("title");
    }

    [Fact]
    public void Update_ReplacesTheSheet()
    {
        var sheet = LyricsSheet.Create(Guid.NewGuid(), new LyricsContent("a", "en", "x"), T0);

        sheet.Update(new LyricsContent("b", "nl", "y"), T0.AddHours(1));

        (sheet.Title, sheet.Language, sheet.Text, sheet.UpdatedAt).ShouldBe(("b", "nl", "y", T0.AddHours(1)));
        Should.Throw<DomainValidationException>(() => sheet.Update(new LyricsContent("", "nl", "y"), T0));
    }

    [Fact]
    public void Languages_ContainTheAceStepList()
    {
        LyricsRules.Languages.Count.ShouldBe(51);
        LyricsRules.Languages.ShouldContain("no");
        LyricsRules.Languages.ShouldContain("unknown");
    }

    [Fact]
    public void Parse_FindsMarkersNumbersAndLineCounts()
    {
        var sections = LyricsSections.Parse("intro text\n[Verse]\na\nb\n\n[CHORUS]\nc\n[verse 2]\n[bridge]\n[spoken]\nd\n[pre-chorus]\ne");

        sections.ShouldBe([
            new LyricsSection(SectionMarker.Verse, null, 2),
            new LyricsSection(SectionMarker.Chorus, null, 1),
            new LyricsSection(SectionMarker.Verse, 2, 0),
            new LyricsSection(SectionMarker.Bridge, null, 2),
            new LyricsSection(SectionMarker.PreChorus, null, 1),
        ]);
    }

    [Fact]
    public void Parse_EveryMarker()
    {
        LyricsSections.Parse("[intro]\n[outro]\n[instrumental]").Select(s => s.Marker).ShouldBe([SectionMarker.Intro, SectionMarker.Outro, SectionMarker.Instrumental]);
        LyricsSections.Parse("no markers").ShouldBeEmpty();
    }

    [Fact]
    public void Propose_ContractVector()
    {
        var proposal = LyricsSections.Propose("Sing it loud, sing it free\nEvery note is part of me\n\nThis little song belongs to me\n\nPaper planes and a borrowed tune\n\nThis little song belongs to me");

        proposal.Text.ShouldBe("[verse]\nSing it loud, sing it free\nEvery note is part of me\n\n[chorus]\nThis little song belongs to me\n\n[verse 2]\nPaper planes and a borrowed tune\n\n[chorus]\nThis little song belongs to me");
        proposal.Sections.ShouldBe([
            new LyricsSection(SectionMarker.Verse, null, 2),
            new LyricsSection(SectionMarker.Chorus, null, 1),
            new LyricsSection(SectionMarker.Verse, 2, 1),
            new LyricsSection(SectionMarker.Chorus, null, 1),
        ]);
        proposal.WordsPerSecondAt120s.ShouldBe(30 / 120.0);
    }

    [Fact]
    public void Propose_RepeatsIgnoreCaseAndPunctuation()
    {
        var proposal = LyricsSections.Propose("A b\n\nHey, Ho!\n\nhey ho\n\n\n\nc d");

        proposal.Sections.Select(s => s.Marker).ShouldBe([SectionMarker.Verse, SectionMarker.Chorus, SectionMarker.Chorus, SectionMarker.Verse]);
    }

    [Fact]
    public void Propose_KeepsExistingMarkers()
    {
        var proposal = LyricsSections.Propose("[chorus]\nla la");

        (proposal.Text, proposal.Sections.Single().Marker).ShouldBe(("[chorus]\nla la", SectionMarker.Chorus));
    }

    [Fact]
    public void Density_CountsWordsOutsideMarkers()
    {
        LyricsDensity.Words("[verse]\nSing it loud\n\n[chorus 2]\nla  la").ShouldBe(5);
        LyricsDensity.WordsPerSecond("one two three four five", 1).ShouldBe(5);
        LyricsDensity.IsTooDense("one two three four five", 1).ShouldBeTrue();
        LyricsDensity.IsTooDense("one two three four", 1).ShouldBeFalse();
        LyricsDensity.WordsPerSecond("x", 0).ShouldBe(0);
        LyricsDensity.Warning(93, 20).ShouldBe("The lyrics have 4.7 words per second; at most 4 sing clearly. Choose a longer duration or shorten the text.");
    }
}
