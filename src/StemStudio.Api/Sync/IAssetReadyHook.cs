using StemStudio.Domain.Assets;

namespace StemStudio.Api.Sync;

/// <summary>
/// Extension point of the asset sync: runs when an item has just become ready, before the sync saves it (feature 014:
/// a new 008 score gets its score view). A hook adds tracked changes only; the sync's save stores them with the item.
/// A failing hook is logged and never fails the item.
/// </summary>
public interface IAssetReadyHook
{
    /// <summary>Called once when the item becomes ready.</summary>
    /// <param name="asset">The ready item (tracked, not saved yet).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task OnReadyAsync(Asset asset, CancellationToken cancellationToken);
}
