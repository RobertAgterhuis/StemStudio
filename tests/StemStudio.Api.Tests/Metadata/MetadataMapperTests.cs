using StemStudio.Api.Metadata;
using StemStudio.Api.Worker;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Tests.Metadata;

/// <summary>Worker-reported metadata of a track: an upload keeps the title of its original file name, also in its online lookup.</summary>
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

    [Fact]
    public void UploadWithoutTagTitle_LooksUpItsOriginalFileName_NotTheHashedStoredName()
    {
        var hash = new string('c', 64);
        var track = Track.FromUpload(Guid.NewGuid(), SourceKey.ForUpload(hash), new UploadedFile("Kettle Song.wav", hash + ".wav"), T0);

        var query = MetadataMapper.ToQuery(track, new WorkerSourceMetadata(hash, null, null, DurationSeconds: 12, LookupTitle: hash), 12);

        query.ShouldBe(new LookupQuery("Kettle Song", null, null, 12));
    }

    [Fact]
    public void UploadWithTagTitle_LooksUpTheTagTitle()
    {
        var hash = new string('d', 64);
        var track = Track.FromUpload(Guid.NewGuid(), SourceKey.ForUpload(hash), new UploadedFile("track01.wav", hash + ".wav"), T0);

        var query = MetadataMapper.ToQuery(track, new WorkerSourceMetadata(hash, null, null, Album: "Tagged Album", LookupTitle: "Tagged Title", LookupArtist: "Tagged Artist"), 12);

        query.ShouldBe(new LookupQuery("Tagged Title", "Tagged Artist", "Tagged Album", 12));
    }

    [Fact]
    public void UrlSource_LooksUpTheReportedLookupTitle()
    {
        var track = Track.FromUrl(Guid.NewGuid(), SourceKey.Parse("https://youtu.be/dQw4w9WgXcQ").Key!, "https://youtu.be/dQw4w9WgXcQ", T0);

        var query = MetadataMapper.ToQuery(track, new WorkerSourceMetadata("Rick Astley - Never Gonna Give You Up (Official Video)", null, null, DurationSeconds: 213, LookupTitle: "Never Gonna Give You Up", LookupArtist: "Rick Astley"));

        query.ShouldBe(new LookupQuery("Never Gonna Give You Up", "Rick Astley", null, 213));
    }
    /// <summary>A track imported before 404c8031 that stores the hashed upload name as lookup title.</summary>
    internal static Track HashTitledUpload(string lookupTitle)
    {
        var hash = new string('e', 64);
        var track = Track.FromUpload(Guid.NewGuid(), SourceKey.ForUpload(hash), new UploadedFile("Kettle Song.wav", hash + ".wav"), T0);
        track.ScheduleLookup(new LookupQuery(lookupTitle, null), T0);
        return track;
    }

    [Theory]
    [InlineData("eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee")]
    [InlineData("eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee.wav")]
    public void Request_OfAStoredHashedLookupTitle_UsesTheTracksOwnTitle(string lookupTitle) =>
        MetadataMapper.ToRequest(HashTitledUpload(lookupTitle)).Title.ShouldBe("Kettle Song");

    [Theory]
    [InlineData("Real Title")]
    [InlineData("deadbeef")]
    public void Request_KeepsARealLookupTitle(string lookupTitle) =>
        MetadataMapper.ToRequest(HashTitledUpload(lookupTitle)).Title.ShouldBe(lookupTitle);
}
