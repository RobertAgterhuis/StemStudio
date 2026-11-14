using System.Text.Json;
using StemStudio.Domain.Tracks;

namespace StemStudio.Domain.Tests.Tracks;

public sealed class SourceKeyTests
{
    private const string Sha = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";

    private static readonly Dictionary<string, string> ReasonKeywords = new(StringComparer.Ordinal)
    {
        ["empty"] = "enter",
        ["malformed"] = "valid link",
        ["scheme"] = "http",
        ["host"] = "only youtube",
        ["playlist"] = "playlist",
        ["missing-id"] = " id",
        ["unsupported-path"] = "video",
        ["album"] = "album",
        ["artist"] = "artist",
        ["episode"] = "podcast",
        ["show"] = "podcast",
    };

    public static TheoryData<string, string, string, string> AcceptedVectors()
    {
        var data = new TheoryData<string, string, string, string>();
        foreach (var vector in LoadVectors().GetProperty("accepted").EnumerateArray())
        {
            data.Add(Text(vector, "url"), Text(vector, "sourceType"), Text(vector, "sourceKey"), Text(vector, "canonicalUrl"));
        }

        return data;
    }

    public static TheoryData<string, string> RejectedVectors()
    {
        var data = new TheoryData<string, string>();
        foreach (var vector in LoadVectors().GetProperty("rejected").EnumerateArray())
        {
            data.Add(Text(vector, "url"), Text(vector, "reason"));
        }

        return data;
    }

    [Fact]
    public void Vectors_AreAllLoaded()
    {
        AcceptedVectors().Count.ShouldBe(16);
        RejectedVectors().Count.ShouldBe(22);
    }

    [Theory]
    [MemberData(nameof(AcceptedVectors))]
    public void Parse_AcceptedVector_ReturnsKeyAndCanonicalUrl(string url, string sourceType, string sourceKey, string canonicalUrl)
    {
        var result = SourceKey.Parse(url);

        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Error.ShouldBeNull();
        result.Key.ShouldNotBeNull();
        result.Key.Value.ShouldBe(sourceKey);
        result.Key.Type.ToString().ToLowerInvariant().ShouldBe(sourceType);
        result.CanonicalUrl.ShouldBe(canonicalUrl);
        result.Key.CanonicalUrl.ShouldBe(canonicalUrl);
    }

    [Theory]
    [MemberData(nameof(RejectedVectors))]
    public void Parse_RejectedVector_ReturnsActionableError(string url, string reason)
    {
        var result = SourceKey.Parse(url);

        result.IsSuccess.ShouldBeFalse();
        result.Key.ShouldBeNull();
        result.CanonicalUrl.ShouldBeNull();
        result.Error.ShouldNotBeNullOrWhiteSpace();
        result.Error.ToLowerInvariant().ShouldContain(ReasonKeywords[reason]);
    }

    [Fact]
    public void Parse_Null_IsRejectedAsEmpty()
    {
        SourceKey.Parse(null).Error!.ToLowerInvariant().ShouldContain("enter");
    }

    [Fact]
    public void Parse_OverlongUrl_IsRejected()
    {
        var url = "https://youtu.be/dQw4w9WgXcQ?x=" + new string('a', 2048);

        SourceKey.Parse(url).Error!.ShouldContain("2048");
    }

    [Theory]
    [InlineData("https://open.spotify.com/intl-nl/album/4aawyAB9vmqN3uQ7FjRGTy", "album")]
    [InlineData("https://open.spotify.com/", " id")]
    [InlineData("https://open.spotify.com/user/someone", "track")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ/extra", " id")]
    [InlineData("https://www.youtube.com/shorts/", " id")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXc!", " id")]
    [InlineData("https://www.youtube.com/watch?foo&v", " id")]
    public void Parse_EdgeCases_AreRejected(string url, string keyword)
    {
        SourceKey.Parse(url).Error!.ToLowerInvariant().ShouldContain(keyword);
    }

    [Fact]
    public void ForUpload_BuildsUploadKeyWithoutCanonicalUrl()
    {
        var key = SourceKey.ForUpload(Sha);

        key.Value.ShouldBe("upload:" + Sha);
        key.Type.ShouldBe(SourceType.Upload);
        key.CanonicalUrl.ShouldBeNull();
        key.ToString().ShouldBe(key.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("9F86D081884C7D659A2FEAA0C55AD015A3BF4F1B2B0B822CD15D6C15B0F00A08")]
    [InlineData("abc")]
    public void ForUpload_RejectsNonLowerHexSha256(string sha)
    {
        Should.Throw<ArgumentException>(() => SourceKey.ForUpload(sha));
    }

    [Theory]
    [InlineData("youtube:dQw4w9WgXcQ", SourceType.Youtube, "https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("spotify:4cOdK2wGLETKBW3PvgPWqT", SourceType.Spotify, "https://open.spotify.com/track/4cOdK2wGLETKBW3PvgPWqT")]
    [InlineData("upload:" + Sha, SourceType.Upload, null)]
    public void FromStored_RestoresKey(string value, SourceType type, string? canonical)
    {
        var key = SourceKey.FromStored(value);

        key.Value.ShouldBe(value);
        key.Type.ShouldBe(type);
        key.CanonicalUrl.ShouldBe(canonical);
    }

    [Theory]
    [InlineData("vimeo:123")]
    [InlineData("youtube:short")]
    [InlineData("spotify:tooShortId")]
    [InlineData("nokey")]
    public void FromStored_RejectsInvalidKeys(string value)
    {
        Should.Throw<ArgumentException>(() => SourceKey.FromStored(value));
    }

    private static JsonElement LoadVectors()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Tracks", "source-url-vectors.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
    }

    private static string Text(JsonElement element, string name) => element.GetProperty(name).GetString()!;
}
