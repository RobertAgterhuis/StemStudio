using StemStudio.Api.Assets;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Voices;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Recording;

/// <summary>
/// Runs after the asset synchroniser saved an item (feature 015): once a take's asset is ready, its transient
/// <c>capture.wav</c> is deleted and the FLAC's hash is registered in the 010 audio-hash index, so the take is refused as a
/// voice sample. A failed take keeps its capture for "Try again".
/// </summary>
/// <param name="storage">Take files.</param>
/// <param name="hashes">The audio-hash index.</param>
/// <param name="logger">Logger.</param>
public sealed partial class TakeAssetFinisher(TakeFiles storage, IAudioHashRepository hashes, ILogger<TakeAssetFinisher> logger) : IAssetFinisher
{
    /// <inheritdoc/>
    public async Task FinishAsync(Asset asset, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (asset.Provenance.Origin != AssetOrigin.Recording || asset.Status != AssetStatus.Ready)
        {
            return;
        }

        try
        {
            storage.DeleteCapture(asset.Id);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogCaptureKept(logger, asset.Id, exception);
        }

        foreach (var file in asset.Files.Where(file => !string.IsNullOrEmpty(file.Sha256)))
        {
            if (!await hashes.ContainsAsync(file.Sha256, cancellationToken))
            {
                await hashes.AddAsync(new AudioHash(file.Sha256, AudioHashKind.Export, asset.Id), cancellationToken);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Take item {AssetId} is ready, but its capture file could not be deleted")]
    private static partial void LogCaptureKept(ILogger logger, Guid assetId, Exception exception);
}
