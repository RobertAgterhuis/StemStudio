using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Web.Api;
using StemStudio.Web.InstrumentViews;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.InstrumentViews;

/// <summary>The static catalogue is loaded once per circuit (T059, data-model 7.5 "cached by the UI").</summary>
public sealed class InstrumentCatalogueCacheTests
{
    private readonly IStemStudioApiClient _api = Substitute.For<IStemStudioApiClient>();

    [Fact]
    public async Task GetAsync_LoadsOnce_AndReturnsTheCachedCatalogue()
    {
        var catalogue = InstrumentViewsData.Catalogue();
        _api.GetInstrumentCatalogueAsync(Arg.Any<CancellationToken>()).Returns(catalogue);
        var cache = new InstrumentCatalogueCache(_api);

        (await cache.GetAsync()).ShouldBeSameAs(catalogue);
        (await cache.GetAsync()).ShouldBeSameAs(catalogue);

        await _api.Received(1).GetInstrumentCatalogueAsync(Arg.Any<CancellationToken>());
        cache.Current.ShouldBeSameAs(catalogue);
    }

    [Fact]
    public async Task GetAsync_AfterAFailure_TriesAgain()
    {
        var catalogue = InstrumentViewsData.Catalogue();
        _api.GetInstrumentCatalogueAsync(Arg.Any<CancellationToken>()).Returns(
            _ => throw new ApiException(ApiError.Unreachable), _ => Task.FromResult(catalogue));
        var cache = new InstrumentCatalogueCache(_api);

        await Should.ThrowAsync<ApiException>(() => cache.GetAsync());
        cache.Current.ShouldBeNull();
        (await cache.GetAsync()).ShouldBeSameAs(catalogue);
    }
}
