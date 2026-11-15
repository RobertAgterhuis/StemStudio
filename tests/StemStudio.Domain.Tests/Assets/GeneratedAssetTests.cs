using StemStudio.Domain.Assets;
using StemStudio.Domain.Generations;

namespace StemStudio.Domain.Tests.Assets;

/// <summary>009 T039: library items of generated takes (origin generation, ready at once, 48 kHz facts).</summary>
public sealed class GeneratedAssetTests
{
    private static readonly DateTime T0 = new(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly AssetFile File = new("audio", "audio.flac", "audio/flac", 1234, new string('a', 64));

    [Fact]
    public void CreateGenerated_IsReadyWithItsFile()
    {
        var generation = new GenerationSource(Guid.NewGuid(), 2, GenerationKind.Restyle);
        var provenance = Provenance(generation);
        var audio = new AudioFacts(AudioFormat.Flac, 24, null, 45, -0.9, 0) { SampleRate = 48_000 };

        var asset = Asset.CreateGenerated(Guid.NewGuid(), "Song - Jazz trio (take 2)", provenance, [File], audio, T0);

        (asset.Kind, asset.Status, asset.Progress, asset.SizeBytes).ShouldBe((AssetKind.Audio, AssetStatus.Ready, 100, 1234L));
        asset.Provenance.Origin.ShouldBe(AssetOrigin.Generation);
        asset.Provenance.Generation.ShouldBe(generation);
        asset.Audio!.SampleRate.ShouldBe(48_000);
        asset.TrackId.ShouldBe(provenance.TrackId);
        asset.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void CreateGenerated_NeedsAGenerationProvenance() =>
        Should.Throw<ArgumentException>(() =>
            Asset.CreateGenerated(Guid.NewGuid(), "x", Provenance(null), [File], new AudioFacts(AudioFormat.Flac, 24, null, 45, -1, 0), T0));

    [Fact]
    public void CreateGenerated_LongNamesAreCut()
    {
        var asset = Asset.CreateGenerated(Guid.NewGuid(), new string('n', 200), Provenance(new GenerationSource(Guid.NewGuid(), 1, GenerationKind.Restyle)),
            [File], new AudioFacts(AudioFormat.Flac, 24, null, 45, -1, 0), T0);

        asset.Name.Length.ShouldBe(AssetNaming.MaxNameLength);
    }

    private static Provenance Provenance(GenerationSource? generation) => new()
    {
        Origin = AssetOrigin.Generation,
        TrackId = Guid.NewGuid(),
        TrackTitle = "Song",
        Contents = "restyle, Jazz trio, take 2",
        Generation = generation,
    };
}
