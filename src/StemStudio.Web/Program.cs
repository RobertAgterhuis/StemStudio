using Microsoft.Extensions.Options;
using Radzen;
using StemStudio.Web;
using StemStudio.Web.Api;
using StemStudio.Web.Audio;
using StemStudio.Web.Components;
using StemStudio.Web.Generation;
using StemStudio.Web.Help;
using StemStudio.Web.Interop;
using StemStudio.Web.Lyrics;
using StemStudio.Web.Preferences;
using StemStudio.Web.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddRadzenComponents();
builder.Services.AddScoped<IAudioEngine, AudioEngineInterop>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddTransient<IPollingTimer, PollingTimer>();
builder.Services.AddScoped<IConfirmService, DialogConfirmService>();
builder.Services.AddScoped<IKeyboardShortcuts, KeyboardShortcuts>();
builder.Services.AddScoped<IWebInterop, WebInterop>();
builder.Services.AddScoped<IVoiceRecorder, VoiceRecorderInterop>();
builder.Services.AddScoped<IPreferencesStore, LocalPreferencesStore>();
builder.Services.AddScoped<PreferencesState>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<TourService>();
builder.Services.AddScoped<HelpContext>();
builder.Services.AddScoped<PageCommands>();
builder.Services.AddScoped<ShellOverlays>();
builder.Services.AddScoped<KaraokePreferences>();
builder.Services.AddScoped<GenerationStatusSource>();
builder.Services.AddScoped<ITourLauncher>(services => services.GetRequiredService<TourService>());

// Help content is validated once at start-up (contracts/help-content.md 1): invalid content fails fast.
builder.Services.AddSingleton<IHelpCatalog>(HelpCatalog.FromAssembly(typeof(HelpCatalog).Assembly));
builder.Services.AddSingleton<IHelpSearch, HelpSearch>();
builder.Services.Configure<StemStudioWebOptions>(builder.Configuration.GetSection(StemStudioWebOptions.SectionName));
builder.Services.AddHttpClient<IStemStudioApiClient, StemStudioApiClient>(
    StemStudioWebOptions.ApiClientName,
    (services, client) =>
    {
        client.BaseAddress = services.GetRequiredService<IOptions<StemStudioWebOptions>>().Value.ApiBaseUrl;
        client.Timeout = TimeSpan.FromMinutes(10); // 200 MiB uploads are streamed through this client
    });

var app = builder.Build();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapGet("/health", () => TypedResults.Ok(new { status = "ok" }));
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

await app.RunAsync();

/// <summary>Web entry point. Public so integration tests can host it with <c>WebApplicationFactory</c>.</summary>
public partial class Program;
