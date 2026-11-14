using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StemStudio.Web.Api;
using StemStudio.Web.Audio;

namespace StemStudio.Web.Tests;

public sealed class WebHostTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task GetHealth_ReturnsOk()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public void ApiClient_UsesConfiguredServerSideBaseUrl()
    {
        var services = factory.Services;
        var options = services.GetRequiredService<IOptions<StemStudioWebOptions>>().Value;

        using var client = services.GetRequiredService<IHttpClientFactory>().CreateClient(StemStudioWebOptions.ApiClientName);

        client.BaseAddress.ShouldBe(options.ApiBaseUrl);
        options.PublicApiBaseUrl.ShouldBe(new Uri("http://localhost:8081"));
    }

    [Fact]
    public async Task Services_RegisterTypedApiClientAndAudioEngine()
    {
        await using var scope = factory.Services.CreateAsyncScope();

        scope.ServiceProvider.GetRequiredService<IStemStudioApiClient>().ShouldBeOfType<StemStudioApiClient>();
        scope.ServiceProvider.GetRequiredService<IAudioEngine>().ShouldBeOfType<AudioEngineInterop>();
    }
}
