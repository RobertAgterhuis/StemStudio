using StemStudio.Api.Storage;

namespace StemStudio.Api.Tests.Storage;

/// <summary>Detail stem files under <c>tracks/&lt;id&gt;/detail/&lt;parent&gt;/&lt;name&gt;.flac</c> (T032).</summary>
public sealed class DetailStemFileStoreTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("stemstudio-detail-store-").FullName;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public void Path_IsDerivedFromTrackIdParentAndName()
    {
        var trackId = Guid.Parse("0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a");

        Store().GetPath(new DetailStemKey(trackId, "drums", "kick"))
            .ShouldBe(Path.Combine(_root, "tracks", trackId.ToString(), "detail", "drums", "kick.flac"));
        Store().GetPath(new DetailStemKey(trackId, "guitar", "acoustic-guitar"))
            .ShouldBe(Path.Combine(_root, "tracks", trackId.ToString(), "detail", "guitar", "acoustic-guitar.flac"));
    }

    [Theory]
    [InlineData("drums", "../kick")]
    [InlineData("drums", "Kick")]
    [InlineData("drums", "lead")]
    [InlineData("bass", "rest")]
    [InlineData("..", "rest")]
    [InlineData("drums", "")]
    public void Path_RejectsNamesOutsideTheEnums(string parent, string name)
    {
        Should.Throw<ArgumentException>(() => Store().GetPath(new DetailStemKey(Guid.NewGuid(), parent, name)));
        Store().Find(new DetailStemKey(Guid.NewGuid(), parent, name)).ShouldBeNull();
    }

    [Fact]
    public async Task Save_WritesThroughATempFileAndReturnsTheSize()
    {
        var store = Store();
        var key = new DetailStemKey(Guid.NewGuid(), "vocals", "lead");
        string? tempSeen = null;

        var size = await store.SaveAsync(key, async (stream, ct) =>
        {
            tempSeen = ((FileStream)stream).Name;
            await stream.WriteAsync(new byte[] { 1, 2, 3, 4 }, ct);
        }, Ct);

        size.ShouldBe(4);
        (await File.ReadAllBytesAsync(store.GetPath(key), Ct)).ShouldBe([1, 2, 3, 4]);
        tempSeen.ShouldNotBe(store.GetPath(key));
        File.Exists(tempSeen).ShouldBeFalse();
        store.Find(key)!.Length.ShouldBe(4);
    }

    [Fact]
    public async Task Save_FailureLeavesNoFile()
    {
        var store = Store();
        var key = new DetailStemKey(Guid.NewGuid(), "drums", "snare");

        await Should.ThrowAsync<IOException>(() => store.SaveAsync(key, (_, _) => throw new IOException("network"), Ct));

        store.Find(key).ShouldBeNull();
        Directory.GetFiles(Path.GetDirectoryName(store.GetPath(key))!).ShouldBeEmpty();
    }

    [Fact]
    public void Find_IsNullForAMissingFile()
    {
        Store().Find(new DetailStemKey(Guid.NewGuid(), "drums", "kick")).ShouldBeNull();
    }

    [Fact]
    public async Task DeleteAll_RemovesOnlyTheDetailFolderOfTheTrack()
    {
        var store = Store();
        var stems = new StemFileStore(new DataDirectory(_root));
        var trackId = Guid.NewGuid();
        var other = new DetailStemKey(Guid.NewGuid(), "drums", "kick");
        await store.SaveAsync(new DetailStemKey(trackId, "drums", "kick"), WriteBytes, Ct);
        await store.SaveAsync(other, WriteBytes, Ct);
        await stems.SaveStemAsync(trackId, "drums", WriteBytes, Ct);

        store.DeleteAll(trackId);
        store.DeleteAll(Guid.NewGuid());

        Directory.Exists(Path.Combine(_root, "tracks", trackId.ToString(), "detail")).ShouldBeFalse();
        stems.FindStem(trackId, "drums").ShouldNotBeNull();
        store.Find(other).ShouldNotBeNull();
    }

    private static Task WriteBytes(Stream stream, CancellationToken ct) => stream.WriteAsync(new byte[8], ct).AsTask();

    private DetailStemFileStore Store() => new(new DataDirectory(_root));
}
