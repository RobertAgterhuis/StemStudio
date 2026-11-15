using StemStudio.Contracts.Lyrics;
using StemStudio.Web.Lyrics;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Mixer.Karaoke;

/// <summary>Mapping and texts of the karaoke banner (006 T037/T038).</summary>
public sealed class KaraokeTextTests
{
    [Fact]
    public void Sync_OfAPlainDocument_IsNull() => LyricsText.Sync(LyricsData.Plain()).ShouldBeNull();

    [Theory]
    [InlineData(LyricsStatusDto.NotFound, "notFound")]
    [InlineData(LyricsStatusDto.AlignPending, "alignPending")]
    [InlineData(LyricsStatusDto.Found, "found")]
    public void Value_IsTheWireName(LyricsStatusDto status, string value) => LyricsText.Value(status).ShouldBe(value);

    [Fact]
    public void Notices_UseTheReasonWhenGiven_AndZeroWithoutProgress()
    {
        LyricsText.MixerNotice(LyricsData.Empty(LyricsStatusDto.NotFound, "No lyrics were found online. You can enter them yourself."))
            .ShouldStartWith("No lyrics were found online. You can enter them yourself.");
        LyricsText.MixerNotice(LyricsData.Empty(LyricsStatusDto.Aligning)).ShouldContain("(0 %)");
        LyricsText.MixerNotice(LyricsData.Plain(LyricsStatusDto.Found, null)).ShouldBe("These lyrics have no timing the banner can follow.");
    }

    [Fact]
    public void Mapper_RefusesEverythingButTimedFoundLyrics()
    {
        KaraokeMapper.ToKaraoke(null).ShouldBeNull();
        KaraokeMapper.ToKaraoke(LyricsData.Found() with { Status = LyricsStatusDto.AlignPending }).ShouldBeNull();
        KaraokeMapper.ToKaraoke(LyricsData.Found() with { Lines = [] }).ShouldBeNull();
        KaraokeMapper.ToKaraoke(LyricsData.Found(SyncLevelDto.Plain)).ShouldBeNull();
        var found = LyricsData.Found();
        KaraokeMapper.ToKaraoke(found with { Lines = [found.Lines[0] with { EndMs = null }] }).ShouldBeNull();
        KaraokeMapper.ToKaraoke(found with { Lines = [found.Lines[0] with { Words = [found.Lines[0].Words[0] with { EndMs = null }] }] }).ShouldBeNull();
        KaraokeMapper.ToKaraoke(found).ShouldNotBeNull().Lines.Count.ShouldBe(2);
    }

    [Theory]
    [InlineData(100, 110, 150)]
    [InlineData(100, 90, 50)]
    [InlineData(100, 100, 100)]
    [InlineData(-4990, -5000, -5000)]
    public void AfterKey_WidensOneNativeStep(int previous, int next, int expected) => OffsetInput.AfterKey(previous, next).ShouldBe(expected);
}
