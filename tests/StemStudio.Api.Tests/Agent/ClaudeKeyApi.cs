using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StemStudio.Agent.Backends;
using StemStudio.Agent.Ports;
using StemStudio.Api.Agent.Secrets;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Agent;

namespace StemStudio.Api.Tests.Agent;

/// <summary>
/// The API with the real key store and key provider (011 follow-up claude-key): a substitute environment, a substitute agent
/// status service, and a "must not be called" Anthropic handler behind the Claude client factory.
/// </summary>
internal sealed class ClaudeKeyApi : IDisposable
{
    private readonly Lazy<HttpClient> _client;

    public ClaudeKeyApi(string? dataDir = null, string? keyRingDir = null, bool httpLogging = false)
    {
        var settings = new Dictionary<string, string?>();
        if (keyRingDir is not null)
        {
            settings[DataProtectionSetup.KeyRingDirKey] = keyRingDir;
        }

        if (httpLogging)
        {
            settings["Logging:LogLevel:Microsoft.AspNetCore.HttpLogging"] = "Information";
        }

        StatusService.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(AgentStatuses.Ready());
        Factory = new ApiFactory(dataDir) { Settings = settings, ConfigureServices = services => Register(services, httpLogging) };
        _client = new Lazy<HttpClient>(Factory.CreateClient);
    }

    public ApiFactory Factory { get; }

    public HttpClient Client => _client.Value;

    public IEnvironmentReader Environment { get; } = Substitute.For<IEnvironmentReader>();

    public IAgentStatusService StatusService { get; } = Substitute.For<IAgentStatusService>();

    /// <summary>Anthropic stand-in: every request is recorded and answered 500; tests assert it stays empty.</summary>
    public StubHttpMessageHandler Anthropic { get; } = new(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void UseEnvironmentKey(string? key) => Environment.GetVariable(ClaudeKeyProvider.KeyVariable).Returns(key);

    public Task<HttpResponseMessage> GetStatusAsync() => Client.GetAsync(ApiRoutes.AgentClaudeKey, Ct);

    public Task<HttpResponseMessage> PutKeyAsync(string? apiKey) => PutRawAsync(apiKey is null ? "{}" : System.Text.Json.JsonSerializer.Serialize(new { apiKey }));

    public Task<HttpResponseMessage> PutRawAsync(string body, string mediaType = "application/json") =>
        Client.PutAsync(ApiRoutes.AgentClaudeKey, new StringContent(body, Encoding.UTF8, mediaType), Ct);

    public Task<HttpResponseMessage> DeleteKeyAsync() => Client.DeleteAsync(ApiRoutes.AgentClaudeKey, Ct);

    public async Task<ClaudeKeyStatusDto> StatusAsync()
    {
        using var response = await GetStatusAsync();
        return await response.BodyAsync<ClaudeKeyStatusDto>();
    }

    public async Task<AgentSettingsDto> SettingsAsync() =>
        (await Client.GetFromJsonAsync<AgentSettingsDto>(ApiRoutes.AgentSettings, ApiFactory.Json, Ct))!;

    public void Dispose()
    {
        if (_client.IsValueCreated)
        {
            _client.Value.Dispose();
        }

        Factory.Dispose();
    }

    private void Register(IServiceCollection services, bool httpLogging)
    {
        services.AddSingleton(Environment);
        services.AddSingleton(StatusService);
        services.AddSingleton(sp => ActivatorUtilities.CreateInstance<ClaudeChatClientFactory>(
            sp, new HttpClient(Anthropic) { BaseAddress = new Uri("http://anthropic.invalid/") }));
        if (httpLogging)
        {
            services.AddHttpLogging(options =>
            {
                options.LoggingFields = HttpLoggingFields.All;
                options.CombineLogs = true;
            });
            services.AddSingleton<IStartupFilter, HttpLoggingStartup>();
        }
    }

    /// <summary>Puts <c>UseHttpLogging</c> in front of the API pipeline, as a host that logs every request would.</summary>
    private sealed class HttpLoggingStartup : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.UseHttpLogging();
            next(app);
        };
    }
}
