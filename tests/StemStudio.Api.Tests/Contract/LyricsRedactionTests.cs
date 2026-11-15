using StemStudio.Contracts.Lyrics;

namespace StemStudio.Api.Tests.Contract;

/// <summary>
/// The Contracts lyrics records never print the lyric text in <c>ToString()</c> (FR-010, ADR-015 D6), the same way as the
/// domain records: counts, times and states only.
/// </summary>
public sealed class LyricsRedactionTests
{
    private const string Secret = "Secret lyric words";

    private static readonly LyricsWordDto Word = new(Secret, 100, 400, WordOriginDto.Aligned, LowConfidence: true);

    private static readonly LyricsLineDto Line = new(100, 900, Secret, [Word]);

    [Fact]
    public void WordDto_ToString_HidesTheTextAndKeepsTheTiming()
    {
        var text = Word.ToString();

        text.ShouldNotContain(Secret);
        text.ShouldBe("LyricsWordDto { StartMs = 100, EndMs = 400, Origin = Aligned, LowConfidence = True }");
    }

    [Fact]
    public void LineDto_ToString_HidesTheTextAndCountsTheWords()
    {
        var text = Line.ToString();

        text.ShouldNotContain(Secret);
        text.ShouldBe("LyricsLineDto { StartMs = 100, EndMs = 900, Words = 1 }");
    }

    [Fact]
    public void LyricsDto_ToString_HidesTheLinesAndCountsThem()
    {
        var dto = new LyricsDto { Status = LyricsStatusDto.Found, Reason = "ok", SyncLevel = SyncLevelDto.Word, Language = "en", Credit = "Lyrics: LRCLIB", OffsetMs = -120, Lines = [Line] };

        var text = dto.ToString();

        text.ShouldNotContain(Secret);
        text.ShouldBe("LyricsDto { Status = Found, Reason = ok, SyncLevel = Word, Origin = , Language = en, Source = , Credit = Lyrics: LRCLIB, OffsetMs = -120, Instrumental = False, Lines = 1, Alignment =  }");
    }

    [Fact]
    public void SetLyricsRequest_ToString_HidesTheTextAndGivesItsLength()
    {
        var text = new SetLyricsRequest(Secret, "en").ToString();

        text.ShouldNotContain(Secret);
        text.ShouldBe($"SetLyricsRequest {{ Text = {Secret.Length} characters, Language = en }}");
    }
}
