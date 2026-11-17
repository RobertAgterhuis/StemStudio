using StemStudio.Api.Assets;
using StemStudio.Api.Library;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Generations;
using StemStudio.Domain.Rendering;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Generations;

/// <summary>A take to register in the library.</summary>
/// <param name="FilePath">Absolute path of the take file.</param>
/// <param name="Title">Item name ("&lt;source&gt; - &lt;style&gt; (take n)").</param>
/// <param name="Generation">The generation.</param>
/// <param name="Take">The take.</param>
public sealed record GeneratedAudioEntry(string FilePath, string Title, Generation Generation, GenerationTake Take);

/// <summary>The 007 library as seen by generations (009 data-model 3.6).</summary>
public interface IGeneratedAudioLibrary
{
    /// <summary>Copies the take into the library as a ready audio item (origin generation).</summary>
    /// <param name="entry">The take.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The item id.</returns>
    Task<Guid> RegisterAsync(GeneratedAudioEntry entry, CancellationToken cancellationToken);

    /// <summary>Removes an item and its files (an item the user already deleted is ignored).</summary>
    /// <param name="assetId">The item.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task RemoveAsync(Guid assetId, CancellationToken cancellationToken);
}

/// <summary>Binds <see cref="IGeneratedAudioLibrary"/> to the 007 asset repository and file store.</summary>
/// <param name="assets">Asset repository.</param>
/// <param name="files">Asset file store.</param>
/// <param name="clock">Clock.</param>
/// <param name="filer">Files the new item in the library folders (feature 022).</param>
public sealed class GeneratedAudioLibraryAdapter(IAssetRepository assets, IAssetFileStore files, TimeProvider clock, ILibraryFiler filer) : IGeneratedAudioLibrary
{
    private const string FileName = "audio.flac";

    /// <inheritdoc />
    public async Task<Guid> RegisterAsync(GeneratedAudioEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var id = Guid.NewGuid();
        var stored = await files.SaveAsync(id, FileName, (stream, ct) => CopyAsync(entry.FilePath, stream, ct), cancellationToken);
        var file = new AssetFile("audio", FileName, "audio/flac", stored.SizeBytes, stored.Sha256);
        var audio = new AudioFacts(AudioFormat.Flac, 24, null, entry.Take.DurationSeconds, Math.Min(0, entry.Take.PeakDbfs), 0) { SampleRate = 48_000 };
        var asset = Asset.CreateGenerated(id, Provenance(entry), new GeneratedAudio(entry.Title, [file], audio), clock.GetUtcNow().UtcDateTime);
        await filer.FileAsync(asset, cancellationToken);
        assets.Add(asset);
        await assets.SaveAsync(cancellationToken);
        return id;
    }

    /// <inheritdoc />
    public async Task RemoveAsync(Guid assetId, CancellationToken cancellationToken)
    {
        if (await assets.FindAsync(assetId, cancellationToken) is { } asset)
        {
            await assets.RemoveAsync(asset, cancellationToken);
        }

        files.DeleteAll(assetId);
    }

    private static Provenance Provenance(GeneratedAudioEntry entry)
    {
        var generation = entry.Generation;
        var source = GenerationText.Source(generation);
        return new Provenance
        {
            Origin = AssetOrigin.Generation,
            TrackId = generation.SourceTrackId,
            TrackTitle = GenerationText.Cut(source.Title, 300),
            Contents = GenerationText.Cut($"{source.Description}, take {entry.Take.Index}", 200),
            Generation = new GenerationSource(generation.Id, entry.Take.Index, generation.Kind),
        };
    }

    private static async Task CopyAsync(string path, Stream destination, CancellationToken cancellationToken)
    {
        await using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        await source.CopyToAsync(destination, cancellationToken);
    }
}
