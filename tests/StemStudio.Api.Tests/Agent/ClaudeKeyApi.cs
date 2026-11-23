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

/// <summary>A request of a local-only test: method, route, body and where it comes from.</summary>
/// <param name="Method">HTTP method.</param>
/// <param name="Route">Route.</param>
internal sealed record KeyRequest(HttpMethod Method, string Route)
{
    public string? Body { get; init; }

    /// <summary>Peer address (<see cref="TestPeerStartup"/>); <c>none</c> = no address.</summary>
    public string Peer { get; init; } = "127.0.0.1";

    public string? Host { get; init; }

    public string? Origin { get; init; }

    /// <summary>Further request headers (for example forwarded headers a spoofing client adds).</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();
}

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
        // Refactor 003: the app-wide host allow-list is opened to every name here, as an owner who opens the API on the LAN
        // would, so the key filter's own host layer (CK-FR-012) stays under test; HostAllowListTests covers the default.
        var settings = new Dictionary<string, string?> { [KeyEntryPolicy.InternalCallerKey] = options.InternalCaller, ["StemStudio:AllowedHosts"] = "*" };
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
            // appsettings.json sets Microsoft.AspNetCore to Warning, which wins over a shorter prefix: override it explicitly, or
            // the pipeline and Data Protection logs are never searched (qa F1).
            settings["Logging:LogLevel:Default"] = "Trace";
            settings["Logging:LogLevel:Microsoft"] = "Trace";
            settings["Logging:LogLevel:Microsoft.AspNetCore"] = "Trace";
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

    /// <summary>Sends "Mute the drums" in a new conversation and waits until the run has ended.</summary>
    public async Task<AgentRunDto> RunToTheEndAsync()
    {
        using var created = await Client.PostAsJsonAsync(ApiRoutes.AgentConversations, new CreateConversationRequest(), Ct);
        var conversation = await created.BodyAsync<ConversationDto>();
        using var sent = await Client.PostAsJsonAsync(ApiRoutes.AgentMessages(conversation.ConversationId),
            new SendAgentMessageRequest { Text = "Mute the drums", Context = new PageContextDto { Page = "mixer" } }, Ct);
        sent.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var runId = (await sent.BodyAsync<AgentMessageAcceptedDto>()).RunId;
        AgentRunDto? run = null;
        await Eventually.WaitUntilAsync(async () =>
            (run = await Client.GetFromJsonAsync<AgentRunDto>(ApiRoutes.AgentRun(runId), ApiFactory.Json, Ct))!.EndedAt is not null);
        return run!;
    }

    /// <summary>Sends a request as another peer, with another <c>Host</c> or an <c>Origin</c> (local-only tests).</summary>
    public async Task<HttpResponseMessage> SendAsync(KeyRequest key)
    {
        ArgumentNullException.ThrowIfNull(key);
        using var request = new HttpRequestMessage(key.Method, key.Route);
        if (key.Body is not null)
        {
            request.Content = new StringContent(key.Body, Encoding.UTF8, "application/json");
        }

        request.Headers.Add(TestPeerStartup.Header, key.Peer);
        if (key.Host is not null)
        {
            request.Headers.Host = key.Host;
        }

        if (key.Origin is not null)
        {
            request.Headers.Add("Origin", key.Origin);
        }

        foreach (var (name, value) in key.Headers)
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        return await Client.SendAsync(request, Ct);
    }

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
