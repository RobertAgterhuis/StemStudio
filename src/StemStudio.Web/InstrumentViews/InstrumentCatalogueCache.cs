using StemStudio.Contracts.InstrumentViews;
using StemStudio.Web.Api;

namespace StemStudio.Web.InstrumentViews;

/// <summary>
/// The static instrument catalogue (<c>getInstrumentCatalogue</c>), loaded once per circuit and shared by the score
/// dialog, the key display and the singing panel (data-model 7.5). A failed load is not cached.
/// </summary>
/// <param name="api">The backend client.</param>
public sealed class InstrumentCatalogueCache(IStemStudioApiClient api)
{
    private Task<InstrumentCatalogueDto>? _loading;

    /// <summary>The catalogue once loaded, or null.</summary>
    public InstrumentCatalogueDto? Current { get; private set; }

    /// <summary>Returns the cached catalogue or loads it.</summary>
    /// <param name="cancellationToken">Cancels the first load.</param>
    /// <returns>The catalogue.</returns>
    public Task<InstrumentCatalogueDto> GetAsync(CancellationToken cancellationToken = default) =>
        Current is { } current ? Task.FromResult(current) : LoadAsync(cancellationToken);

    // One request at a time; a failed load is tried again on the next call.
    private async Task<InstrumentCatalogueDto> LoadAsync(CancellationToken cancellationToken)
    {
        _loading ??= api.GetInstrumentCatalogueAsync(cancellationToken);
        try
        {
            return Current = await _loading;
        }
        finally
        {
            _loading = null;
        }
    }
}
