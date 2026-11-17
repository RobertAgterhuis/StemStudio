using StemStudio.Domain.Lyrics.Assist;

namespace StemStudio.Domain.Tests.Lyrics.Assist;

/// <summary>023 T047: corpus index, sheet guard (>= 3 lines and >= 25 %), cliché set passes, output guard, own-work override (FR-003, FR-023, FR-031).</summary>
public sealed class VerbatimGuardTests
{
    private static readonly CorpusTrack Song = new(Guid.NewGuid(), "Paper Lanterns");
    private static readonly CorpusTrack Song2 = new(Guid.NewGuid(), "Salt and Static");
    private static readonly CorpusTrack Cliches = new(Guid.NewGuid(), "Baby Baby");
    private static readonly CorpusTrack ClichesNl = new(Guid.NewGuid(), "Voor Altijd");

    private static readonly LyricsCorpusIndex Corpus = new([
        Lyrics(Song, "t-fake-song.en.txt"), Lyrics(Song2, "t-fake-song-2.en.txt"), Lyrics(Cliches, "t-fake-cliches.en.txt"), Lyrics(ClichesNl, "t-fake-cliches.nl.txt"),
    ]);

    [Fact]
    public void Key_NormalisesCaseDiacriticsAndPunctuation() =>
        VerbatimGuard.Key("  Één, twee... DON'T  stop!").ShouldBe("een twee dont stop");

    [Fact]
    public void NotOwnSheet_IsRefusedNamingTheTrack() =>
        VerbatimGuard.RefusingTrack(AssistFixtures.Sheet("not-own.en.txt"), Corpus, null).ShouldBe(Song);

    [Fact]
    public void Override_CoversOnlyItsTrack()
    {
        VerbatimGuard.RefusingTrack(AssistFixtures.Sheet("not-own.en.txt"), Corpus, Song.TrackId).ShouldBeNull();
        VerbatimGuard.RefusingTrack(AssistFixtures.Sheet("not-own-two-tracks.en.txt"), Corpus, Song.TrackId).ShouldBe(Song2);
        VerbatimGuard.RefusingTrack(AssistFixtures.Sheet("not-own-two-tracks.en.txt"), Corpus, null).ShouldBe(Song);
    }

    [Theory]
    [InlineData("cliche-sheet.en.txt")]
    [InlineData("cliche-sheet.nl.txt")]
    [InlineData("verbatim.en.txt")]
    [InlineData("zomeravond.nl.txt")]
    public void OwnSheets_PassTheGuard(string file) =>
        VerbatimGuard.RefusingTrack(AssistFixtures.Sheet(file), Corpus, null).ShouldBeNull();

    [Fact]
    public void MatchingTracks_ForTheOverride()
    {
        VerbatimGuard.MatchingTracks(AssistFixtures.Sheet("not-own-two-tracks.en.txt"), Corpus).ShouldBe([Song, Song2]);
        VerbatimGuard.MatchingTracks(AssistFixtures.Sheet("cliche-sheet.en.txt"), Corpus).ShouldBeEmpty();
    }

    [Fact]
    public void OutputGuard_WithholdsRunsAndFullLines()
    {
        var run = AssistFixtures.Expected().GetProperty("corpus").GetProperty("t-fake-song").GetProperty("verbatimRun").GetString()!;

        VerbatimGuard.IsVerbatim("oh " + run.ToUpperInvariant() + " tonight", Corpus).ShouldBeTrue();
        VerbatimGuard.IsVerbatim(string.Join(' ', run.Split(' ').Take(6)) + " and more of mine", Corpus).ShouldBeFalse();
        VerbatimGuard.IsVerbatim(string.Join('\n', run.Split(' ').Chunk(4).Select(c => string.Join(' ', c))), Corpus).ShouldBeTrue();
        VerbatimGuard.IsVerbatim("a line that is mine alone", Corpus).ShouldBeFalse();
        VerbatimGuard.IsVerbatim(null, Corpus).ShouldBeFalse();
        LyricsCorpusIndex.Empty.TracksWithLine("x").ShouldBeEmpty();
    }

    [Fact]
    public void FullLineOfFiveWords_IsIndexed()
    {
        var index = new LyricsCorpusIndex([new CorpusLyrics(Song, ["One two three four five", "short line", "One two three four five"]), new CorpusLyrics(Song2, ["one two three four five!"])]);

        index.TracksWithLine("one two three four five").ShouldBe([Song, Song2]);
        index.TracksWithLine("short line").ShouldBeEmpty();
        VerbatimGuard.IsVerbatim("One, two, three, four, five", index).ShouldBeTrue();
    }

    private static CorpusLyrics Lyrics(CorpusTrack track, string file) =>
        new(track, AssistFixtures.Read(Path.Combine("corpus", file)).Split('\n'));
}
