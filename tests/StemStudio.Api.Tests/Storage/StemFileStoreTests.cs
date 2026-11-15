using System.Security.Cryptography;
using StemStudio.Api.Storage;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;

namespace StemStudio.Api.Tests.Storage;

public sealed class StemFileStoreTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("stemstudio-store-").FullName;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public void StemPath_IsDerivedFromTrackIdAndStemName()
    {
        var store = new StemFileStore(new DataDirectory(_root));
        var trackId = Guid.Parse("0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a");

        store.GetStemPath(trackId, "vocals").ShouldBe(Path.Combine(_root, "tracks", trackId.ToString(), "stems", "vocals.flac"));
    }

    [Theory]
    [InlineData("../vocals")]
    [InlineData("Vocals")]
    [InlineData("vocals.flac")]
    [InlineData("")]
    public void StemPath_RejectsUnknownStemNames(string stem)
    {
        var store = new StemFileStore(new DataDirectory(_root));

        Should.Throw<ArgumentException>(() => store.GetStemPath(Guid.NewGuid(), stem));
    }

    [Fact]
    public async Task SaveStem_WritesThroughTempFileAndReturnsSize()
    {
        var store = new StemFileStore(new DataDirectory(_root));
        var trackId = Guid.NewGuid();
        string? tempSeen = null;

        var size = await store.SaveStemAsync(trackId, "drums", async (stream, ct) =>
        {
            tempSeen = ((FileStream)stream).Name;
            await stream.WriteAsync(new byte[] { 1, 2, 3 }, ct);
        }, Ct);

        size.ShouldBe(3);
        var path = store.GetStemPath(trackId, "drums");
        (await File.ReadAllBytesAsync(path, Ct)).ShouldBe([1, 2, 3]);
        tempSeen.ShouldNotBe(path);
        File.Exists(tempSeen).ShouldBeFalse();
        store.FindStem(trackId, "drums")!.Length.ShouldBe(3);
    }

    [Fact]
    public async Task SaveStem_FailureLeavesNoFile()
    {
        var store = new StemFileStore(new DataDirectory(_root));
        var trackId = Guid.NewGuid();

        await Should.ThrowAsync<IOException>(() => store.SaveStemAsync(trackId, "bass", (_, _) => throw new IOException("network"), Ct));

        store.FindStem(trackId, "bass").ShouldBeNull();
        Directory.GetFiles(Path.GetDirectoryName(store.GetStemPath(trackId, "bass"))!).ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteTrackFiles_RemovesTheTrackDirectoryOnly()
    {
        var store = new StemFileStore(new DataDirectory(_root));
        var trackId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        await store.SaveStemAsync(trackId, "vocals", (s, ct) => s.WriteAsync(new byte[4], ct).AsTask(), Ct);
        await store.SaveStemAsync(otherId, "vocals", (s, ct) => s.WriteAsync(new byte[4], ct).AsTask(), Ct);

        store.DeleteTrackFiles(trackId);

        Directory.Exists(Path.Combine(_root, "tracks", trackId.ToString())).ShouldBeFalse();
        store.FindStem(otherId, "vocals").ShouldNotBeNull();
    }

    [Fact]
    public void DeleteTrackFiles_MissingDirectoryIsANoOp()
    {
        var store = new StemFileStore(new DataDirectory(_root));

        Should.NotThrow(() => store.DeleteTrackFiles(Guid.NewGuid()));
    }

    [Fact]
    public async Task SaveStem_ReplacesExistingFile()
    {
        var store = new StemFileStore(new DataDirectory(_root));
        var trackId = Guid.NewGuid();
        await store.SaveStemAsync(trackId, "other", (s, ct) => s.WriteAsync(new byte[10], ct).AsTask(), Ct);

        await store.SaveStemAsync(trackId, "other", (s, ct) => s.WriteAsync(new byte[2], ct).AsTask(), Ct);

        store.FindStem(trackId, "other")!.Length.ShouldBe(2);
    }

    [Fact]
    public void FindStem_UnknownStemReturnsNull()
    {
        new StemFileStore(new DataDirectory(_root)).FindStem(Guid.NewGuid(), "flute").ShouldBeNull();
    }

    [Fact]
    public async Task Upload_IsStoredUnderShaAndExtensionWithHash()
    {
        var store = new UploadStore(new DataDirectory(_root));
        var bytes = new byte[] { 5, 6, 7, 8 };
        var expectedSha = Convert.ToHexStringLower(SHA256.HashData(bytes));

        var upload = await store.SaveAsync(new MemoryStream(bytes), ".MP3", Ct);

        upload.Sha256.ShouldBe(expectedSha);
        upload.FileName.ShouldBe(expectedSha + ".mp3");
        upload.SizeBytes.ShouldBe(4);
        store.GetPath(upload.FileName).ShouldBe(Path.Combine(_root, "uploads", expectedSha + ".mp3"));
        store.Exists(upload.FileName).ShouldBeTrue();
        Directory.GetFiles(Path.Combine(_root, "uploads")).Length.ShouldBe(1);
    }

    [Fact]
    public async Task Upload_TooLargeLeavesNoFile()
    {
        var store = new UploadStore(new DataDirectory(_root), maxBytes: 3);

        await Should.ThrowAsync<UploadTooLargeException>(() => store.SaveAsync(new MemoryStream(new byte[4]), ".wav", Ct));

        Directory.GetFiles(Path.Combine(_root, "uploads")).ShouldBeEmpty();
    }

    [Fact]
    public async Task Upload_EmptyFileIsRejected()
    {
        var store = new UploadStore(new DataDirectory(_root));

        await Should.ThrowAsync<EmptyUploadException>(() => store.SaveAsync(new MemoryStream(), ".wav", Ct));

        Directory.GetFiles(Path.Combine(_root, "uploads")).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(".ogg")]
    [InlineData("")]
    [InlineData(".mp3/../x")]
    public async Task Upload_RejectsUnsupportedExtension(string extension)
    {
        var store = new UploadStore(new DataDirectory(_root));

        await Should.ThrowAsync<ArgumentException>(() => store.SaveAsync(new MemoryStream([1]), extension, Ct));
    }

    [Fact]
    public async Task Upload_DeleteRemovesFile()
    {
        var store = new UploadStore(new DataDirectory(_root));
        var upload = await store.SaveAsync(new MemoryStream([1]), ".flac", Ct);

        store.Delete(upload.FileName);
        store.Delete(upload.FileName);

        store.Exists(upload.FileName).ShouldBeFalse();
    }

    [Theory]
    [InlineData("../stemstudio.db")]
    [InlineData("abc.mp3")]
    [InlineData("9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08.exe")]
    [InlineData("9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08.mp3\n")]
    public void Upload_PathRejectsForeignFileNames(string fileName)
    {
        var store = new UploadStore(new DataDirectory(_root));

        Should.Throw<ArgumentException>(() => store.GetPath(fileName));
        store.Exists(fileName).ShouldBeFalse();
    }

    [Fact]
    public async Task HashingCopy_AcceptsExactly200MiB()
    {
        var result = await HashingCopy.CopyAsync(new ZeroStream(ContractLimits.MaxUploadBytes), Stream.Null, ContractLimits.MaxUploadBytes, Ct);

        result.SizeBytes.ShouldBe(209_715_200);
        result.Sha256.Length.ShouldBe(64);
    }

    [Fact]
    public async Task HashingCopy_Rejects200MiBPlusOneByte()
    {
        await Should.ThrowAsync<UploadTooLargeException>(
            () => HashingCopy.CopyAsync(new ZeroStream(ContractLimits.MaxUploadBytes + 1), Stream.Null, ContractLimits.MaxUploadBytes, Ct));
    }

    [Fact]
    public void DataDirectory_ResolvesRelativePathAgainstContentRoot()
    {
        var directory = DataDirectory.Resolve("data", _root);

        directory.Root.ShouldBe(Path.Combine(_root, "data"));
        DataDirectory.Resolve(_root, "/elsewhere").Root.ShouldBe(_root);
    }

    [Fact]
    public void Exceptions_HaveStandardConstructors()
    {
        new UploadTooLargeException().Message.ShouldContain("209,715,200");
        new UploadTooLargeException("x").Message.ShouldBe("x");
        new UploadTooLargeException("x", new IOException()).InnerException.ShouldNotBeNull();
        new EmptyUploadException().Message.ShouldContain("empty");
        new EmptyUploadException("x").Message.ShouldBe("x");
        new EmptyUploadException("x", new IOException()).InnerException.ShouldNotBeNull();
    }
}
