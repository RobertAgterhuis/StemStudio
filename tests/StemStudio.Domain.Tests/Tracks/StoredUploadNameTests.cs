using StemStudio.Domain.Tracks;

namespace StemStudio.Domain.Tests.Tracks;

/// <summary>022 hash-title guard: a lookup title that is the stored (hashed) upload name counts as absent.</summary>
public sealed class StoredUploadNameTests
{
    private const string Sha = "28c19d35e0b1a4f6c7d8e9fa0b1c2d3e4f5a6b7c8d9e0f1a2b3c4d5e6f7a8b9c";

    [Theory]
    [InlineData(Sha)]
    [InlineData(Sha + ".mp3")]
    [InlineData("28C19D35E0B1A4F6C7D8E9FA0B1C2D3E")]
    [InlineData("  " + Sha + "  ")]
    public void HexHash_IsAStoredName(string title) => StoredUploadName.IsStoredName(title, null).ShouldBeTrue();

    [Fact]
    public void TheTracksOwnStoredFileStem_IsAStoredName() =>
        StoredUploadName.IsStoredName("upload-42", "upload-42.wav").ShouldBeTrue();

    [Fact]
    public void TheTracksOwnStoredFileName_IsAStoredName() =>
        StoredUploadName.IsStoredName("upload-42.wav", "upload-42.wav").ShouldBeTrue();

    [Theory]
    [InlineData("Enter Sandman")]
    [InlineData("deadbeef")]
    [InlineData("cafe")]
    [InlineData("abcdef0123456789abcdef0123456")]
    [InlineData(Sha + "0")]
    [InlineData(Sha + " live")]
    public void ANormalOrShortHexTitle_IsKept(string title) => StoredUploadName.IsStoredName(title, Sha + ".mp3").ShouldBeFalse();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NoTitle_IsNotAStoredName(string? title) => StoredUploadName.IsStoredName(title, "x.mp3").ShouldBeFalse();
    [Fact]
    public void Usable_DropsOnlyAStoredName()
    {
        StoredUploadName.Usable(Sha, null).ShouldBeNull();
        StoredUploadName.Usable("Enter Sandman", Sha + ".mp3").ShouldBe("Enter Sandman");
        StoredUploadName.Usable(null, null).ShouldBeNull();
    }
}
