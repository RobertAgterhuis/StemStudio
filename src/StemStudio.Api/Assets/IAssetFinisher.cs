using StemStudio.Domain.Assets;

namespace StemStudio.Api.Assets;

/// <summary>Runs after the asset synchroniser saved an item; a feature finishes its own items (feature 015: recorded takes).</summary>
public interface IAssetFinisher
{
    /// <summary>Finishes a saved item; items the implementation does not own are ignored.</summary>
    /// <param name="asset">The item.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task FinishAsync(Asset asset, CancellationToken cancellationToken);
}
