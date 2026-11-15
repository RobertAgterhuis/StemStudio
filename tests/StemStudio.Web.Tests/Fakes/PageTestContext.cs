using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Radzen;
using StemStudio.Web.Api;
using StemStudio.Web.Audio;
using StemStudio.Web.Help;
using StemStudio.Web.Interop;
using StemStudio.Web.Preferences;
using StemStudio.Web.Services;

namespace StemStudio.Web.Tests.Fakes;

/// <summary>bUnit context with Radzen, loose JS interop and substitutes for every frontend port.</summary>
public abstract class PageTestContext : BunitContext
{
    private static readonly Lazy<HelpCatalog> RealHelp = new(() => HelpCatalog.FromAssembly(typeof(HelpCatalog).Assembly));

    protected PageTestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddRadzenComponents();
        Services.AddSingleton(Api);
        Services.AddSingleton(Engine);
        Services.AddSingleton(Confirm);
        Services.AddSingleton(Substitute.For<IKeyboardShortcuts>());
        Services.AddSingleton<IPollingTimer>(Timer);
        Services.AddSingleton(Options.Create(new StemStudioWebOptions()));
        Services.AddSingleton<TimeProvider>(Time);
        Services.AddSingleton<IWebInterop>(Interop);
        Services.AddSingleton<IPreferencesStore>(Store);
        Services.AddSingleton<PreferencesState>();
        Services.AddSingleton<ToastService>();
        Services.AddSingleton<IHelpCatalog>(_ => RealHelp.Value);
        Services.AddSingleton<IHelpSearch, HelpSearch>();
        Services.AddSingleton(Substitute.For<ITourLauncher>());
        Services.AddScoped<HelpContext>();
    }

    protected IStemStudioApiClient Api { get; } = Substitute.For<IStemStudioApiClient>();

    protected IAudioEngine Engine { get; } = Substitute.For<IAudioEngine>();

    protected IConfirmService Confirm { get; } = Substitute.For<IConfirmService>();

    protected ManualPollingTimer Timer { get; } = new();

    protected FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 11, 15, 10, 0, 0, TimeSpan.Zero));

    protected FakeWebInterop Interop { get; } = new();

    protected FakePreferencesStore Store { get; } = new();

    protected PreferencesState Preferences => Services.GetRequiredService<PreferencesState>();

    protected static ApiException Problem(System.Net.HttpStatusCode status, string title, string? detail = null) =>
        new(new ApiError(status, title, detail, new Dictionary<string, string[]>()));

    protected string NavigatedTo => Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().Uri;
}
