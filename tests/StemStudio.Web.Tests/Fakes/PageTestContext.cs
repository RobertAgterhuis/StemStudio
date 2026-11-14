using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Radzen;
using StemStudio.Web.Api;
using StemStudio.Web.Audio;
using StemStudio.Web.Services;

namespace StemStudio.Web.Tests.Fakes;

/// <summary>bUnit context with Radzen, loose JS interop and substitutes for every frontend port.</summary>
public abstract class PageTestContext : BunitContext
{
    protected PageTestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddRadzenComponents();
        Services.AddSingleton(Api);
        Services.AddSingleton(Engine);
        Services.AddSingleton<IPollingTimer>(Timer);
        Services.AddSingleton(Options.Create(new StemStudioWebOptions()));
    }

    protected IStemStudioApiClient Api { get; } = Substitute.For<IStemStudioApiClient>();

    protected IAudioEngine Engine { get; } = Substitute.For<IAudioEngine>();

    protected ManualPollingTimer Timer { get; } = new();

    protected static ApiException Problem(System.Net.HttpStatusCode status, string title, string? detail = null) =>
        new(new ApiError(status, title, detail, new Dictionary<string, string[]>()));

    protected string NavigatedTo => Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().Uri;
}
