using System.Net;
using System.Net.Http.Json;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Health;

namespace StemStudio.Api.Tests;

public sealed class HealthEndpointTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task GetHealth_ReturnsOk()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(new Uri(ApiRoutes.Health, UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>(TestContext.Current.CancellationToken);
        body.ShouldBe(new HealthResponse("ok"));
    }
}
