using StemStudio.Api.Lyrics;
using StemStudio.Api.Tests.Metadata;

namespace StemStudio.Api.Tests.Lyrics;

/// <summary>022 hash-title guard: the lyrics lookup of an old upload with a hashed lookup title (feature 006).</summary>
public sealed class LyricsMapperTests
{
    [Theory]
    [InlineData("eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee")]
    [InlineData("eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee.wav")]
    public void LookupRequest_OfAStoredHashedLookupTitle_UsesTheTracksOwnTitle(string lookupTitle) =>
        LyricsMapper.ToLookupRequest(MetadataMapperTests.HashTitledUpload(lookupTitle)).Title.ShouldBe("Kettle Song");

    [Theory]
    [InlineData("Real Title")]
    [InlineData("deadbeef")]
    public void LookupRequest_KeepsARealLookupTitle(string lookupTitle) =>
        LyricsMapper.ToLookupRequest(MetadataMapperTests.HashTitledUpload(lookupTitle)).Title.ShouldBe(lookupTitle);
}
