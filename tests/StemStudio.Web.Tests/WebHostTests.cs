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
    public async Task GetRoot_ServesAppShell_WithInteractiveServerScript()
    {
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken);

        html.ShouldContain("blazor.web.js");
        html.ShouldContain("Radzen.Blazor.js");
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

    [Fact]
    public async Task Services_RegisterEveryShellService_Feature004()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        services.GetRequiredService<StemStudio.Web.Interop.IWebInterop>().ShouldBeOfType<StemStudio.Web.Interop.WebInterop>();
        services.GetRequiredService<StemStudio.Web.Preferences.IPreferencesStore>().ShouldBeOfType<StemStudio.Web.Preferences.LocalPreferencesStore>();
        services.GetRequiredService<StemStudio.Web.Preferences.PreferencesState>().ShouldNotBeNull();
        services.GetRequiredService<StemStudio.Web.Services.ToastService>().ShouldNotBeNull();
        services.GetRequiredService<StemStudio.Web.Help.IHelpCatalog>().Topic("welcome").ShouldNotBeNull();
        services.GetRequiredService<StemStudio.Web.Help.IHelpSearch>().ShouldBeOfType<StemStudio.Web.Help.HelpSearch>();
        services.GetRequiredService<StemStudio.Web.Help.HelpContext>().IsOpen.ShouldBeFalse();
        services.GetRequiredService<StemStudio.Web.Help.ITourLauncher>().ShouldBeOfType<StemStudio.Web.Help.TourService>();
    }
}
