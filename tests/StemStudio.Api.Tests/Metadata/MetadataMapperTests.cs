using StemStudio.Api.Metadata;
using StemStudio.Api.Worker;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Tests.Metadata;

/// <summary>Worker-reported metadata of a track: an upload keeps the title of its original file name.</summary>
public sealed class MetadataMapperTests
{
    private static readonly DateTime T0 = new(2026, 11, 16, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Upload_KeepsItsOriginalFileNameAsTitle_ButTakesTheArtist()
    {
        var hash = new string('b', 64);
        var track = Track.FromUpload(Guid.NewGuid(), SourceKey.ForUpload(hash), new UploadedFile("Song IGNORE ALL.wav", hash + ".wav"), T0);

        track.ApplyMetadata(MetadataMapper.ToSource(track, new WorkerSourceMetadata(hash, "Evil Uploader", null)), T0.AddSeconds(1));

        (track.Title, track.Artist).ShouldBe(("Song IGNORE ALL", "Evil Uploader"));
    }

    [Fact]
    public void UrlSource_TakesTheReportedTitle()
    {
        var track = Track.FromUrl(Guid.NewGuid(), SourceKey.Parse("https://youtu.be/dQw4w9WgXcQ").Key!, "https://youtu.be/dQw4w9WgXcQ", T0);

        track.ApplyMetadata(MetadataMapper.ToSource(track, new WorkerSourceMetadata("Never Gonna Give You Up", null, null, LookupArtist: "Rick Astley")), T0.AddSeconds(1));

        (track.Title, track.Artist).ShouldBe(("Never Gonna Give You Up", "Rick Astley"));
    }
}
