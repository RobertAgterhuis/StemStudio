using Microsoft.Net.Http.Headers;
using StemStudio.Domain.Assets;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Assets;

/// <summary>Reads library items and serves their files (backend 1.5.0 <c>getAsset</c>, <c>getAssetFile</c>).</summary>
/// <param name="assets">Asset repository.</param>
/// <param name="files">Asset file store.</param>
/// <param name="tracks">Track repository (for <c>trackAvailable</c>).</param>
public sealed class AssetService(IAssetRepository assets, IAssetFileStore files, ITrackRepository tracks)
{
    /// <summary>One item.</summary>
    /// <param name="id">Item ID.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 or 404.</returns>
    public async Task<IResult> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var asset = await assets.FindAsync(id, cancellationToken);
        return asset is null ? AssetProblems.NotFound(id) : TypedResults.Ok(await ToDtoAsync(asset, cancellationToken));
    }

    /// <summary>Serves a file of a ready item with Range, ETag (the quoted SHA-256) and <c>Content-Disposition</c>.</summary>
    /// <param name="id">Item ID.</param>
    /// <param name="role">File role.</param>
    /// <param name="inline">True for <c>inline</c> (player).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The file or 404.</returns>
    public async Task<IResult> GetFileAsync(Guid id, string role, bool inline, CancellationToken cancellationToken)
    {
        var asset = await assets.FindAsync(id, cancellationToken);
        var file = asset is { Status: AssetStatus.Ready } ? asset.Files.FirstOrDefault(entry => entry.Role == role) : null;
        var stored = file is null ? null : files.Find(id, file.FileName);
        return stored is null ? ApiProblems.NotFound($"Library item {id} has no ready file '{role}'.") : Serve(asset!, file!, stored, inline);
    }

    /// <summary>Maps an item with the availability of its track.</summary>
    /// <param name="asset">The item.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The DTO.</returns>
    public async Task<Contracts.Assets.AssetDto> ToDtoAsync(Asset asset, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(asset);
        var available = asset.TrackId is { } trackId && (await tracks.ExistingIdsAsync([trackId], cancellationToken)).Contains(trackId);
        return AssetMapper.ToDto(asset, available);
    }

    private static HeaderResult Serve(Asset asset, AssetFile file, FileInfo stored, bool inline)
    {
        var name = AssetNaming.DownloadFileName(asset.Name, Path.GetExtension(file.FileName).TrimStart('.'));
        var result = TypedResults.PhysicalFile(
            stored.FullName, file.ContentType, lastModified: stored.LastWriteTimeUtc, entityTag: new EntityTagHeaderValue($"\"{file.Sha256}\""), enableRangeProcessing: true);
        return new HeaderResult(result, HeaderNames.ContentDisposition, ContentDisposition.Attachment(name, inline));
    }
}
