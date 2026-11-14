using Microsoft.Extensions.Options;
using Radzen;
using StemStudio.Web;
using StemStudio.Web.Components;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddRadzenComponents();
builder.Services.Configure<StemStudioWebOptions>(builder.Configuration.GetSection(StemStudioWebOptions.SectionName));
builder.Services.AddHttpClient(
    StemStudioWebOptions.ApiClientName,
    (services, client) => client.BaseAddress = services.GetRequiredService<IOptions<StemStudioWebOptions>>().Value.ApiBaseUrl);

var app = builder.Build();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapGet("/health", () => TypedResults.Ok(new { status = "ok" }));
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

await app.RunAsync();

/// <summary>Web entry point. Public so integration tests can host it with <c>WebApplicationFactory</c>.</summary>
public partial class Program;
