using System.Net;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Agent.Secrets;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;

namespace StemStudio.Api.Tests.Security;

/// <summary>
/// Refactor proposal 003 (API exposure, ADR-027 D6): who may reach the API. Characterisation tests first; the cases marked
/// "003 flips" pin the behaviour before the change and are turned around when the change lands.
/// </summary>
public sealed class ApiExposureTests : IDisposable
{
    private static readonly IPAddress WebContainer = IPAddress.Parse("172.18.0.5");
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public ApiExposureTests()
    {
        _factory = new ApiFactory
        {
            Settings = new Dictionary<string, string?> { [KeyEntryPolicy.InternalCallerKey] = "web" },
            ConfigureServices = services => services.AddSingleton<IHostAddressResolver>(new StaticResolver("web", WebContainer)),
        };
        _client = _factory.CreateClient();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // 003 flips: a LAN device without an Origin may change data today.
    [Theory]
    [InlineData("192.168.1.20")]
    [InlineData("172.18.0.6")]
    public async Task OriginLessPost_FromAnotherPeer_PassesToday(string peer)
    {
        using var response = await SendAsync(Post(peer));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // 003 flips: an empty Origin or a forwarded-for claim does not make a LAN write local, and passes today all the same.
    [Theory]
    [InlineData("Origin", "")]
    [InlineData("X-Forwarded-For", "127.0.0.1")]
    [InlineData("Forwarded", "for=127.0.0.1")]
    public async Task SpoofedOriginLessPost_FromAnotherPeer_PassesToday(string header, string value)
    {
        using var response = await SendAsync(Post("192.168.1.20") with { Extra = (header, value) });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // 003 flipped (step 2): a Host outside the allow-list is refused before routing (DNS rebinding, ADR-027 D6).
    [Theory]
    [InlineData("evil.example")]
    [InlineData("192.168.1.10:8081")]
    [InlineData("localhost.evil.example")]
    [InlineData("127.0.0.1.nip.io:8081")]
    [InlineData("[0:0:0:0:0:0:0:1]:8081")]
    public async Task ForeignHost_Is400(string host)
    {
        using var response = await SendAsync(Health(host));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("172.18.0.5")]
    public async Task OriginLessPost_FromLoopbackOrTheInternalCaller_Passes(string peer)
    {
        using var response = await SendAsync(Post(peer));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("127.0.0.1:8081")]
    [InlineData("[::1]:8081")]
    [InlineData("api:8081")]
    [InlineData("API")]
    [InlineData("LocalHost:18081")]
    public async Task LocalHostNames_AreAccepted(string host)
    {
        using var response = await SendAsync(Health(host));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<HttpResponseMessage> SendAsync(Call call)
    {
        using var request = new HttpRequestMessage(call.Method, call.Path);
        if (call.Peer is not null)
        {
            request.Headers.Add(TestPeerStartup.Header, call.Peer);
        }

        if (call.Host is not null)
        {
            request.Headers.Host = call.Host;
        }

        if (call.Origin is not null)
        {
            request.Headers.Add("Origin", call.Origin);
        }

        if (call.Extra is var (name, value))
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        return await _client.SendAsync(request, Ct);
    }

    private static Call Post(string? peer = null, string? origin = null) => new(HttpMethod.Post, ApiRoutes.RevertRestore) { Peer = peer, Origin = origin };

    private static Call Health(string host) => new(HttpMethod.Get, ApiRoutes.Health) { Host = host };

    private sealed record Call(HttpMethod Method, string Path)
    {
        public string? Peer { get; init; }

        public string? Host { get; init; }

        public string? Origin { get; init; }

        public (string Name, string Value)? Extra { get; init; }
    }

    private sealed class StaticResolver(string host, IPAddress address) : IHostAddressResolver
    {
        public Task<IPAddress[]> ResolveAsync(string name, CancellationToken cancellationToken) =>
            Task.FromResult(string.Equals(name, host, StringComparison.OrdinalIgnoreCase) ? new[] { address } : []);
    }
}
