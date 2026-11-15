using StemStudio.Api.Storage;
using StemStudio.Contracts;

namespace StemStudio.Api.Artwork;

/// <summary>Which picture folder of a track: the best automatic picture or the user's own (data-model 5).</summary>
public enum ArtworkSlot
{
    /// <summary><c>artwork/auto/</c>.</summary>
    Auto,

    /// <summary><c>artwork/user/</c>.</summary>
    User,
}

/// <summary>The two normalised JPEGs of one picture.</summary>
/// <param name="Cover">600 px cover.</param>
/// <param name="Thumb">160 px thumbnail.</param>
public sealed record ArtworkImages(byte[] Cover, byte[] Thumb);

/// <summary>Picture files under <c>&lt;DataDir&gt;/tracks/&lt;trackId&gt;/artwork/{auto,user}/{cover,thumb}.jpg</c> (FR-007).</summary>
public interface IArtworkFileStore
{
    /// <summary>Writes both JPEGs through temporary files, then renames them into place.</summary>
    /// <param name="trackId">Track ID.</param>
    /// <param name="slot">Folder.</param>
    /// <param name="images">The two JPEGs.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task SaveAsync(Guid trackId, ArtworkSlot slot, ArtworkImages images, CancellationToken cancellationToken);

    /// <summary>The stored file of a variant, or null when the variant is unknown or the file is missing.</summary>
    /// <param name="trackId">Track ID.</param>
    /// <param name="slot">Folder.</param>
    /// <param name="variant"><c>cover</c> or <c>thumb</c>.</param>
    /// <returns>The file or null.</returns>
    FileInfo? Find(Guid trackId, ArtworkSlot slot, string variant);

    /// <summary>Deletes a picture folder, if present.</summary>
    /// <param name="trackId">Track ID.</param>
    /// <param name="slot">Folder.</param>
    void Delete(Guid trackId, ArtworkSlot slot);
}

/// <summary>Local-disk implementation of <see cref="IArtworkFileStore"/>; paths come from the GUID and fixed names only.</summary>
/// <param name="dataDirectory">The API data directory.</param>
public sealed class ArtworkFileStore(DataDirectory dataDirectory) : IArtworkFileStore
{
    /// <inheritdoc />
    public async Task SaveAsync(Guid trackId, ArtworkSlot slot, ArtworkImages images, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(images);
        var folder = SlotDirectory(trackId, slot);
        Directory.CreateDirectory(folder);
        await CommitAsync([Pending(folder, ApiRoutes.ArtworkCover, images.Cover), Pending(folder, ApiRoutes.ArtworkThumb, images.Thumb)], cancellationToken);
    }

    /// <inheritdoc />
    public FileInfo? Find(Guid trackId, ArtworkSlot slot, string variant)
    {
        if (variant is not (ApiRoutes.ArtworkCover or ApiRoutes.ArtworkThumb))
        {
            return null;
        }

        var file = new FileInfo(Target(SlotDirectory(trackId, slot), variant));
        return file.Exists ? file : null;
    }

    /// <inheritdoc />
    public void Delete(Guid trackId, ArtworkSlot slot)
    {
        var folder = SlotDirectory(trackId, slot);
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>Writes every temp file first, then renames them all; temp files never survive.</summary>
    private static async Task CommitAsync(PendingFile[] files, CancellationToken cancellationToken)
    {
        try
        {
            foreach (var file in files)
            {
                await File.WriteAllBytesAsync(file.Temp, file.Bytes, cancellationToken);
            }

            Array.ForEach(files, file => File.Move(file.Temp, file.Target, overwrite: true));
        }
        finally
        {
            Array.ForEach(files, file => File.Delete(file.Temp));
        }
    }

    private static PendingFile Pending(string folder, string variant, byte[] bytes) =>
        new(Path.Combine(folder, $"{variant}.{Guid.NewGuid():N}.tmp"), Target(folder, variant), bytes);

    private static string Target(string folder, string variant) => Path.Combine(folder, variant + ".jpg");

    private string SlotDirectory(Guid trackId, ArtworkSlot slot) =>
        Path.Combine(dataDirectory.TrackDirectory(trackId), "artwork", slot == ArtworkSlot.User ? "user" : "auto");

    private sealed record PendingFile(string Temp, string Target, byte[] Bytes);
}
