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

/// <summary>Options of <see cref="ClaudeKeyApi"/>.</summary>
internal sealed record ClaudeKeyApiOptions
{
    /// <summary>Data directory to reuse (not deleted by the factory); null = a fresh temp directory.</summary>
    public string? DataDir { get; init; }

    /// <summary>Key ring directory; null = the factory's sibling of the data directory.</summary>
    public string? KeyRingDir { get; init; }

    /// <summary>Adds <c>UseHttpLogging</c> with every field in front of the pipeline.</summary>
    public bool HttpLogging { get; init; }

    /// <summary>Internal caller of the local-only rule.</summary>
    public string? InternalCaller { get; init; }

    /// <summary>Logs every category at Trace.</summary>
    public bool TraceLogs { get; init; }

    /// <summary>Keeps the agent library's real status service (only for Claude tests against the stand-in; never Ollama).</summary>
    public bool RealStatusService { get; init; }
}

/// <summary>
/// The API with the real key store and key provider (011 follow-up claude-key): a substitute environment, a substitute agent
/// status service, and a "must not be called" Anthropic handler behind the Claude client factory.
/// </summary>
internal sealed class ClaudeKeyApi : IDisposable
{
    private readonly Lazy<HttpClient> _client;

    public ClaudeKeyApi(ClaudeKeyApiOptions? options = null)
    {
        options ??= new ClaudeKeyApiOptions();
        Anthropic = new StubHttpMessageHandler(request =>
        {
            AnthropicKeys.AddRange(request.Headers.SelectMany(header => header.Value)
                .Select(value => value.Replace("Bearer ", string.Empty, StringComparison.Ordinal))
                .Where(value => value.StartsWith(TestDoubles.ClaudeKey.ClaudeKeySentinels.Prefix, StringComparison.Ordinal)));
            return Task.FromResult(AnthropicReply(request));
        });
        var settings = new Dictionary<string, string?> { [KeyEntryPolicy.InternalCallerKey] = options.InternalCaller };
        if (options.KeyRingDir is not null)
        {
            settings[DataProtectionSetup.KeyRingDirKey] = options.KeyRingDir;
        }

        if (options.HttpLogging)
        {
            settings["Logging:LogLevel:Microsoft.AspNetCore.HttpLogging"] = "Information";
        }

        if (options.TraceLogs)
        {
            settings["Logging:LogLevel:Default"] = "Trace";
            settings["Logging:LogLevel:Microsoft"] = "Trace";
        }

        StatusService.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(AgentStatuses.Ready());
        Factory = new ApiFactory(options.DataDir) { Settings = settings, ConfigureServices = services => Register(services, options) };
        _client = new Lazy<HttpClient>(Factory.CreateClient);
    }

    public ApiFactory Factory { get; }

    public HttpClient Client => _client.Value;

    public IEnvironmentReader Environment { get; } = Substitute.For<IEnvironmentReader>();

    public IAgentStatusService StatusService { get; } = Substitute.For<IAgentStatusService>();

    /// <summary>Name resolution of the internal caller (host name -> addresses).</summary>
    public Dictionary<string, IPAddress[]> Hosts { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Anthropic stand-in: every request is recorded and answered by <see cref="AnthropicReply"/> (default 500).</summary>
    public StubHttpMessageHandler Anthropic { get; }

    /// <summary>The stand-in's answer; tests that expect no request assert <see cref="Anthropic"/> stays empty.</summary>
    public Func<HttpRequestMessage, HttpResponseMessage> AnthropicReply { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);

    /// <summary>Every key-shaped header value of the requests the stand-in received.</summary>
    public List<string> AnthropicKeys { get; } = [];

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

    private void Register(IServiceCollection services, ClaudeKeyApiOptions options)
    {
        services.AddSingleton(Environment);
        if (!options.RealStatusService)
        {
            services.AddSingleton(StatusService);
        }

        services.AddSingleton<IHostAddressResolver>(new FakeResolver(Hosts));
        services.AddSingleton(sp => ActivatorUtilities.CreateInstance<ClaudeChatClientFactory>(
            sp, new HttpClient(Anthropic) { BaseAddress = new Uri("http://anthropic.invalid/") }));
        if (options.HttpLogging)
        {
            services.AddHttpLogging(options =>
            {
                options.LoggingFields = HttpLoggingFields.All;
                options.CombineLogs = true;
            });
            services.AddSingleton<IStartupFilter, HttpLoggingStartup>();
        }
    }

    private sealed class FakeResolver(Dictionary<string, IPAddress[]> hosts) : IHostAddressResolver
    {
        public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken) =>
            Task.FromResult(hosts.GetValueOrDefault(host) ?? []);
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
