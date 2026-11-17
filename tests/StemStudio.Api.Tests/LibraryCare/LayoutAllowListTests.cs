using System.Globalization;
using StemStudio.Api.Artwork;
using StemStudio.Api.Assets;
using StemStudio.Api.Generations;
using StemStudio.Api.Storage;
using StemStudio.Contracts;
using StemStudio.Domain.LibraryCare;

namespace StemStudio.Api.Tests.LibraryCare;

/// <summary>
/// 013 Phase 0 T004 (architect): the layout allow-list of data-model.md 3.3 (binding for archive entries, stored paths, the
/// storage classifier and the clean-up) covers every path the 001-012 code generates. The patterns are the binding copy
/// of data-model 3.3, implemented by <see cref="LibraryLayout"/> (backend T014); this test keeps guarding the generators. The generators run for real on a
/// temporary data directory (stems, detail stems, uploads, artwork, asset files, takes); the voice-sample, converted-vocal and
/// generated-audio names are fixed literals in their services and are listed with their source.
/// </summary>
public sealed class LayoutAllowListTests : IDisposable
{
    private const string Detail = "(kick|snare|toms|hihat|cymbals|lead|backing|strings|brass|woodwinds|keys|synth|acoustic-guitar|electric-guitar|percussion|rest)";

    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("stemstudio-layout-");
    private readonly DataDirectory _data;
    private readonly System.Guid _id = System.Guid.NewGuid();

    public LayoutAllowListTests() => _data = new DataDirectory(_root.FullName);

    [Fact]
    public void StemPaths_AreAllowed()
    {
        var store = new StemFileStore(_data);

        StemNames.SixStem.Concat(StemNames.FourStem).Select(stem => Relative(store.GetStemPath(_id, stem))).ShouldAllBe(path => Allowed(path));
    }

    [Fact]
    public void DetailStemPaths_AreAllowedAndTheDetailNamesAreTheLayoutNames()
    {
        var store = new DetailStemFileStore(_data);
        var paths = DetailNames.Parents.SelectMany(parent => DetailNames.AllowedFor(parent).Select(name => store.GetPath(new DetailStemKey(_id, parent, name))));

        paths.Select(Relative).ShouldAllBe(path => Allowed(path));
        DetailNames.All.ShouldBe(Detail.Trim('(', ')').Split('|'), ignoreOrder: true);
    }

    [Theory]
    [InlineData(".mp3")]
    [InlineData(".wav")]
    [InlineData(".flac")]
    public async Task UploadPaths_AreAllowed(string extension)
    {
        using var content = new MemoryStream([1, 2, 3]);

        var stored = await new UploadStore(_data).SaveAsync(content, extension, TestContext.Current.CancellationToken);

        Allowed("uploads/" + stored.FileName).ShouldBeTrue(stored.FileName);
    }

    [Fact]
    public async Task ArtworkPaths_AreAllowed()
    {
        var store = new ArtworkFileStore(_data);
        foreach (var slot in Enum.GetValues<ArtworkSlot>())
        {
            await store.SaveAsync(_id, slot, new ArtworkImages([1], [2]), TestContext.Current.CancellationToken);
        }

        Files("tracks").ShouldAllBe(path => Allowed(path));
        Files("tracks").Count.ShouldBe(4);
    }

    [Theory]
    [InlineData("audio.flac")]
    [InlineData("audio.wav")]
    [InlineData("audio.mp3")]
    [InlineData("score.pdf")]
    [InlineData("score.musicxml")]
    [InlineData("score.mid")]
    public async Task AssetFilePaths_AreAllowed(string fileName)
    {
        await new AssetFileStore(_data).SaveAsync(_id, fileName, (stream, ct) => stream.WriteAsync(new byte[] { 1 }, ct).AsTask(), TestContext.Current.CancellationToken);

        var path = Files("assets").ShouldHaveSingleItem();

        Allowed(path).ShouldBeTrue(path);
    }

    [Fact]
    public void FixedNames_OfTakesVocalsSamplesAndGeneratedAudio_AreAllowed()
    {
        var id = _id.ToString("D", CultureInfo.InvariantCulture);
        string[] paths =
        [
            TakeStorage.RelativePath(_id, 1),
            $"generations/{id}/vocal-1.flac",   // TakeCollector (converted vocal)
            $"voices/{id}/sample.flac",         // VoiceProfileService.StoreSampleAsync
            $"assets/{id}/audio.flac",          // GeneratedAudioLibrary.FileName
        ];

        paths.ShouldAllBe(path => Allowed(path));
    }

    [Theory]
    [InlineData("tracks/../stemstudio.db")]
    [InlineData("assets/0b7e4f1c-6a2d-4c8e-9f10-2a3b4c5d6e7f/..")]
    [InlineData("assets/0b7e4f1c-6a2d-4c8e-9f10-2a3b4c5d6e7f/a..b")]
    [InlineData("generations/0b7e4f1c-6a2d-4c8e-9f10-2a3b4c5d6e7f/a/b/c/d")]
    [InlineData("tracks/0B7E4F1C-6A2D-4C8E-9F10-2A3B4C5D6E7F/stems/vocals.flac")]
    [InlineData("tracks/0b7e4f1c-6a2d-4c8e-9f10-2a3b4c5d6e7f/stems/vocals.wav")]
    [InlineData("library-care/jobs/x.json")]
    [InlineData("stemstudio.db")]
    [InlineData("/assets/0b7e4f1c-6a2d-4c8e-9f10-2a3b4c5d6e7f/audio.flac")]
    public void UnsafeOrForeignPaths_AreRefused(string path)
    {
        Allowed(path).ShouldBeFalse();
    }

    public void Dispose() => _root.Delete(recursive: true);

    private static bool Allowed(string path) => LibraryLayout.IsAllowed(path);

    private string Relative(string path) => Path.GetRelativePath(_root.FullName, path).Replace('\\', '/');

    private List<string> Files(string folder) =>
        [.. Directory.EnumerateFiles(Path.Combine(_root.FullName, folder), "*", SearchOption.AllDirectories).Select(Relative)];
}
