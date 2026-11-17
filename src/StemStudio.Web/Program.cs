using Microsoft.Extensions.Options;
using Radzen;
using StemStudio.Web;
using StemStudio.Web.Agent;
using StemStudio.Web.Api;
using StemStudio.Web.Audio;
using StemStudio.Web.Components;
using StemStudio.Web.Generation;
using StemStudio.Web.Help;
using StemStudio.Web.Interop;
using StemStudio.Web.LibraryCare;
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
builder.Services.AddScoped<NavigationUiTools>();
builder.Services.AddScoped<AgentUiToolHost>();
builder.Services.AddScoped<AgentRunNotifier>();
builder.Services.AddScoped<AgentPanelState>();
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


// Composer agent (011): its own typed client; the event stream is read with ResponseHeadersRead, so the timeout
// covers each request's headers only.
builder.Services.AddHttpClient<IAgentApiClient, AgentApiClient>(
    (services, client) => client.BaseAddress = services.GetRequiredService<IOptions<StemStudioWebOptions>>().Value.ApiBaseUrl);

// Library care (013): its own typed client and one status poller per circuit (shell indicator, maintenance overlay, /care).
builder.Services.AddHttpClient<ILibraryCareClient, LibraryCareClient>(
    (services, client) => client.BaseAddress = services.GetRequiredService<IOptions<StemStudioWebOptions>>().Value.ApiBaseUrl);
builder.Services.AddScoped<LibraryCareStatusSource>();

// Claude key entry only on the computer itself (011 follow-up claude-key, CK-FR-012): the circuit's host-name guard.
builder.Services.AddScoped<PageOrigin>();

var app = builder.Build();

// An address without an endpoint (for example /import) renders the not-found page in the app shell instead of an
// empty 404 body: https://learn.microsoft.com/aspnet/core/blazor/fundamentals/navigation?view=aspnetcore-10.0#not-found-responses
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();
app.MapStaticAssets();
app.MapGet("/health", () => TypedResults.Ok(new { status = "ok" }));
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

await app.RunAsync();

/// <summary>Web entry point. Public so integration tests can host it with <c>WebApplicationFactory</c>.</summary>
public partial class Program;
