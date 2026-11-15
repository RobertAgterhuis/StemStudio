using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Tracks;
using static StemStudio.Domain.Tests.Lyrics.LyricsTestData;

namespace StemStudio.Domain.Tests.Lyrics;

/// <summary>Feature 006 T019: document invariants (data-model 2.1), pasted text, credit, estimate and the automatic alignment rule.</summary>
public sealed class LyricsDocumentTests
{
    [Fact]
    public void ValidDocuments_PassTheRules()
    {
        foreach (var document in new[] { Word(), Line(), Aligned(), Plain(), Plain(LyricsSource.User), Instrumental() })
        {
            Should.NotThrow(() => LyricsDocumentRules.Validate(document));
        }
    }

    [Fact]
    public void TimingRules_AreCheckedPerLineAndWord()
    {
        var document = Word() with
        {
            Lines =
            [
                new LyricsLine(2000, 1000, "a b", [new LyricsWord("a", 2000, 2100, WordOrigin.Source, false), new LyricsWord("b", 500, 4000, WordOrigin.Source, false)]),
                new LyricsLine(100, 9000, "c", [new LyricsWord("c", 100, 3200, WordOrigin.Source, false)]),
                new LyricsLine(9000, 9500, "d", [new LyricsWord("d", null, 9500, WordOrigin.Source, false)]),
            ],
        };

        var fields = Fields(document);

        fields.ShouldContain("lines[0].endMs");
        fields.ShouldContain("lines[0].words[1].startMs");
        fields.ShouldContain("lines[0].words[1].endMs");
        fields.ShouldContain("lines[1].startMs");
        fields.ShouldContain("lines[1].words[0].endMs");
        fields.ShouldContain("lines[2].words[0].startMs");
    }

    [Fact]
    public void WordsOutsideTheirLineOrDecreasing_AreRejected()
    {
        var document = Word() with
        {
            Lines = [new LyricsLine(1000, 2000, "a b", [new LyricsWord("a", 1500, 1600, WordOrigin.Source, false), new LyricsWord("b", 1400, 2500, WordOrigin.Source, false)])],
        };

        Fields(document).ShouldBe(["lines[0].words[1].startMs", "lines[0].words[1].endMs"], ignoreOrder: true);
    }

    [Fact]
    public void PlainDocuments_HaveNoTimesAndNoWordOrigins()
    {
        var document = Plain() with
        {
            Lines = [new LyricsLine(0, 10, "a", [new LyricsWord("a", null, null, WordOrigin.Aligned, false)])],
            Origin = LyricsOrigin.Source,
        };

        Fields(document).ShouldBe(["origin", "lines[0].startMs", "lines[0].words[0].origin"], ignoreOrder: true);
    }

    [Fact]
    public void TimedDocuments_NeedOriginsAndAPlainOriginIsOnlyForPlain()
    {
        var document = Word() with
        {
            Origin = LyricsOrigin.None,
            Lines = [new LyricsLine(0, 10, "a", [new LyricsWord("a", 0, 10, null, false)])],
        };

        Fields(document).ShouldBe(["origin", "lines[0].words[0].origin"], ignoreOrder: true);
    }

    [Fact]
    public void SizeAndTextLimits_AreEnforced()
    {
        var words = Enumerable.Range(0, 41).Select(_ => new LyricsWord("x", null, null, null, false)).ToList();
        var document = Plain() with
        {
            Language = "EN",
            SourceId = new string('1', 65),
            PlainText = new string('x', 20_001),
            Lines =
            [
                new LyricsLine(null, null, new string('x', 501), words),
                new LyricsLine(null, null, string.Empty, []),
                new LyricsLine(null, null, "y", [new LyricsWord(new string('y', 101), null, null, null, false), new LyricsWord(" ", null, null, null, false)]),
            ],
        };

        Fields(document).ShouldBe(
            ["language", "sourceId", "plainText", "lines[0].text", "lines[0].words", "lines[1].text", "lines[1].words", "lines[2].words[0].text", "lines[2].words[1].text"], ignoreOrder: true);
    }

    [Fact]
    public void TooManyLinesAndALinedInstrumental_AreRejected()
    {
        var line = new LyricsLine(null, null, "x", [new LyricsWord("x", null, null, null, false)]);
        var many = Plain() with { Lines = Enumerable.Repeat(line, 401).ToList() };
        var lined = Instrumental() with { Lines = [line] };

        Fields(many).ShouldBe(["lines"]);
        Fields(lined).ShouldBe(["lines"]);
    }

    [Fact]
    public void TimedLinesWithoutTimesAndPlainWordsWithTimes_AreRejected()
    {
        var timed = Word() with { Lines = [new LyricsLine(null, 10, "a", [new LyricsWord("a", 0, 10, WordOrigin.Source, false)])] };
        var plain = Plain() with { Lines = [new LyricsLine(null, null, "a", [new LyricsWord("a", null, 10, null, false), new LyricsWord("b", 5, null, null, false)])] };

        Fields(timed).ShouldBe(["lines[0].startMs"]);
        Fields(plain).ShouldBe(["lines[0].words[0].startMs", "lines[0].words[1].startMs"]);
    }

    [Fact]
    public void Printing_ShowsTheTimingButNeverTheText()
    {
        var document = Word();

        var printed = string.Join(" | ", document, document.Lines[0], document.Lines[0].Words[0]);

        printed.ShouldNotContain("Fake");
        printed.ShouldContain("SyncLevel = Word, Origin = Source, Source = Lrclib, Language = en, Instrumental = False, Lines = 2");
        printed.ShouldContain("StartMs = 10950, EndMs = 13200, Words = 3");
        printed.ShouldContain("StartMs = 10950, EndMs = 11400, Origin = Source, LowConfidence = False");
    }

    [Fact]
    public void ValidationMessages_NeverQuoteTheLyrics()
    {
        var document = Word() with { Lines = [new LyricsLine(10, 5, "Secret words", [new LyricsWord("Secret", 10, 5, WordOrigin.Source, false)])] };

        var error = Should.Throw<DomainValidationException>(() => LyricsDocumentRules.Validate(document));

        error.Message.ShouldNotContain("Secret");
    }

    [Fact]
    public void FromUserText_SplitsLinesAndWordsAndNormalisesLineBreaks()
    {
        var document = LyricsDocument.FromUserText("  Fake line one\r\n\r\nLa\tla  la\rLast  ", "en");

        (document.SyncLevel, document.Origin, document.Source, document.Language, document.SourceId, document.Instrumental).ShouldBe((SyncLevel.Plain, LyricsOrigin.None, LyricsSource.User, "en", null, false));
        document.Lines.Select(line => line.Text).ShouldBe(["Fake line one", "La\tla  la", "Last"]);
        document.Lines[1].Words.Select(word => word.Text).ShouldBe(["La", "la", "la"]);
        document.Lines.SelectMany(line => line.Words).ShouldAllBe(word => word.StartMs == null && word.Origin == null);
        document.PlainText.ShouldBe("Fake line one\n\nLa\tla  la\nLast");
        LyricsDocument.FromUserText("One", null).Language.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \n \t")]
    [InlineData("bell\u0007")]
    [InlineData("nul\0")]
    [InlineData(null)]
    public void FromUserText_RejectsEmptyTextAndControlCharacters(string? text)
    {
        var error = Should.Throw<DomainValidationException>(() => LyricsDocument.FromUserText(text!, null));

        error.Errors.Single().Field.ShouldBe("text");
    }

    [Fact]
    public void FromUserText_RejectsOversizedTextLinesAndBadLanguages()
    {
        Should.Throw<DomainValidationException>(() => LyricsDocument.FromUserText(new string('x', 20_001), null)).Errors.Single().Field.ShouldBe("text");
        Should.Throw<DomainValidationException>(() => LyricsDocument.FromUserText(string.Join(' ', Enumerable.Repeat(new string('x', 50), 10)), null)).Errors.Single().Field.ShouldBe("lines[0].text");
        Should.Throw<DomainValidationException>(() => LyricsDocument.FromUserText("a", "english")).Errors.Single().Field.ShouldBe("language");
    }

    [Theory]
    [InlineData(LyricsSource.Lrclib, LyricsOrigin.Source, "Lyrics: LRCLIB")]
    [InlineData(LyricsSource.Lrclib, LyricsOrigin.Interpolated, "Lyrics: LRCLIB; word timing estimated")]
    [InlineData(LyricsSource.Lrclib, LyricsOrigin.Aligned, "Lyrics: LRCLIB; timing: automatic alignment")]
    [InlineData(LyricsSource.User, LyricsOrigin.None, "Lyrics: entered by you")]
    [InlineData(LyricsSource.User, LyricsOrigin.Aligned, "Lyrics: entered by you; timing: automatic alignment")]
    public void Credit_NamesTheSourceAndTheTimingOrigin(LyricsSource source, LyricsOrigin origin, string credit)
    {
        LyricsCredit.For(Word(source, origin)).ShouldBe(credit);
    }

    [Fact]
    public void Credit_IsNullWithoutADocument()
    {
        LyricsCredit.For(null).ShouldBeNull();
    }

    [Theory]
    [InlineData(213.04, ProcessingDeviceKind.Cpu, true, 185)]
    [InlineData(213.04, ProcessingDeviceKind.Cpu, false, 325)]
    [InlineData(262, ProcessingDeviceKind.Gpu, true, 28)]
    [InlineData(262, ProcessingDeviceKind.Gpu, false, 168)]
    public void Estimate_FollowsTheWorkerFormula(double duration, ProcessingDeviceKind device, bool cached, int seconds)
    {
        AlignmentEstimate.Seconds(duration, device, cached).ShouldBe(seconds);
    }

    [Theory]
    [InlineData(420, ProcessingDeviceKind.Cpu, "Aligning takes about 7 minutes on the CPU.")]
    [InlineData(185, ProcessingDeviceKind.Cpu, "Aligning takes about 3 minutes on the CPU.")]
    [InlineData(28, ProcessingDeviceKind.Gpu, "Aligning takes about 1 minute on the GPU.")]
    public void Estimate_IsDescribedInWholeMinutes(int seconds, ProcessingDeviceKind device, string text)
    {
        AlignmentEstimate.Describe(seconds, device).ShouldBe(text);
    }

    [Theory]
    [InlineData(AlignAuto.Gpu, ProcessingDeviceKind.Gpu, true, true)]
    [InlineData(AlignAuto.Gpu, ProcessingDeviceKind.Cpu, true, false)]
    [InlineData(AlignAuto.Gpu, ProcessingDeviceKind.Gpu, false, false)]
    [InlineData(AlignAuto.Always, ProcessingDeviceKind.Cpu, true, true)]
    [InlineData(AlignAuto.Always, ProcessingDeviceKind.Gpu, false, false)]
    [InlineData(AlignAuto.Never, ProcessingDeviceKind.Gpu, true, false)]
    public void AutoAlignRule_NeedsTheCompletedStandardSeparationAndTheWorkerSetting(AlignAuto auto, ProcessingDeviceKind device, bool completed, bool applies)
    {
        AutoAlignRule.Applies(auto, device, completed).ShouldBe(applies);
    }

    private static string[] Fields(LyricsDocument document) =>
        Should.Throw<DomainValidationException>(() => LyricsDocumentRules.Validate(document)).Errors.Select(error => error.Field).ToArray();
}
