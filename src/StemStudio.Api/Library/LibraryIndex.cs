using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Library;

/// <summary>The library revision (feature 022, research R-06): 16 hex of SHA-256 over the <see cref="LibraryStamp"/>.</summary>
public static class LibraryRevision
{
    /// <summary>The revision of a stamp.</summary>
    /// <param name="stamp">Counts and newest changes.</param>
    /// <returns>16 lower-case hex characters.</returns>
    public static string Of(LibraryStamp stamp)
    {
        ArgumentNullException.ThrowIfNull(stamp);
        var text = string.Join('|', stamp.Items, Ticks(stamp.ItemsUpdatedAt), Ticks(stamp.ItemsFiledAt), stamp.Tracks, Ticks(stamp.TracksUpdatedAt));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)).AsSpan(0, 8));
    }

    private static string Ticks(DateTime? value) => value?.Ticks.ToString(CultureInfo.InvariantCulture) ?? "-";
}

/// <summary>
/// The cached <see cref="LibrarySnapshot"/> (feature 022, data-model 4): rebuilt when the revision changes, one rebuild at a
/// time per revision (concurrent readers of the same revision share it).
/// </summary>
/// <param name="scopes">Scope factory (a rebuild reads in its own scope, so a cancelled request does not break it).</param>
public sealed class LibraryIndex(IServiceScopeFactory scopes)
{
    private readonly Lock _lock = new();
    private (string Revision, Task<LibrarySnapshot> Build)? _snapshot;

    /// <summary>The snapshot of the current revision.</summary>
    /// <param name="library">Data access of the caller (revision query).</param>
    /// <param name="cancellationToken">Cancellation of the caller's wait.</param>
    /// <returns>The snapshot.</returns>
    public async Task<LibrarySnapshot> GetAsync(ILibraryRepository library, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(library);
        var revision = LibraryRevision.Of(await library.StampAsync(cancellationToken));
        return await SnapshotFor(revision).WaitAsync(cancellationToken);
    }

    private Task<LibrarySnapshot> SnapshotFor(string revision)
    {
        lock (_lock)
        {
            if (_snapshot is not { } current || current.Revision != revision || current.Build.IsFaulted)
            {
                _snapshot = (revision, BuildAsync(revision));
            }

            return _snapshot.Value.Build;
        }
    }

    private async Task<LibrarySnapshot> BuildAsync(string revision)
    {
        await using var scope = scopes.CreateAsyncScope();
        var library = scope.ServiceProvider.GetRequiredService<ILibraryRepository>();
        var items = await library.AllItemsAsync(CancellationToken.None);
        var tracks = await library.TrackArtAsync(CancellationToken.None);
        return LibrarySnapshot.Build(revision, items, tracks);
    }
}
