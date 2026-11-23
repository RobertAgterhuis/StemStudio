using System.Net;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Agent.Secrets;
using StemStudio.Api.Security;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;

namespace StemStudio.Api.Tests.Security;

/// <summary>
/// Refactor proposal 003 (API exposure, ADR-027 D6): who may reach the API. Started as characterisation tests; the cases
/// marked "003 flipped" were turned around when the host allow-list (step 2) and the trusted-peer rule (step 3) landed.
/// </summary>
public sealed class ApiExposureTests : IDisposable
{
    private static readonly IPAddress WebContainer = IPAddress.Parse("172.18.0.5");
    private readonly ApiFactory _factory = new()
    {
        Settings = new Dictionary<string, string?> { [KeyEntryPolicy.InternalCallerKey] = "web" },
        ConfigureServices = services => services.AddSingleton<IHostAddressResolver>(new StaticResolver("web", WebContainer)),
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _factory.Dispose();

    // 003 flipped (step 3): a write without an Origin is accepted only from loopback or the internal caller.
    [Theory]
    [InlineData("192.168.1.20")]
    [InlineData("172.18.0.6")]
    [InlineData("172.18.0.1")]
    [InlineData("fe80::1")]
    [InlineData("none")]
    public async Task OriginLessPost_FromAnotherPeer_Is403(string peer)
    {
        using var response = await SendAsync(Post(peer));

        await ShouldBeRefusedAsync(response);
    }

    // 003 flipped (step 3): an empty Origin, a forwarded-for claim or a same-origin fetch mark does not make a LAN write local.
    [Theory]
    [InlineData("Origin", "")]
    [InlineData("X-Forwarded-For", "127.0.0.1")]
    [InlineData("Forwarded", "for=127.0.0.1")]
    [InlineData("X-Real-IP", "127.0.0.1")]
    [InlineData("Sec-Fetch-Site", "same-origin")]
    [InlineData("Sec-Fetch-Site", "none")]
    public async Task SpoofedOriginLessPost_FromAnotherPeer_Is403(string header, string value)
    {
        using var response = await SendAsync(Post("192.168.1.20") with { Extra = (header, value) });

        await ShouldBeRefusedAsync(response);
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task EveryStateChangingMethod_IsGuarded(string method)
    {
        using var response = await SendAsync(new Call(new HttpMethod(method), ApiRoutes.Track(Guid.NewGuid())) { Peer = "192.168.1.20" });

        await ShouldBeRefusedAsync(response);
    }

    [Fact]
    public async Task OriginLessRead_FromAnotherPeer_StillPasses()
    {
        using var response = await SendAsync(new Call(HttpMethod.Get, ApiRoutes.LibraryCareStatus) { Peer = "192.168.1.20" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // The Web app's Origin passes from any peer (browsers set it; the browser's peer behind Docker port forwarding is the
    // bridge gateway). A non-browser client can forge it: the 127.0.0.1 port binding (refactor 004) is the LAN defence.
    [Theory]
    [InlineData("192.168.1.20", "http://web.test", HttpStatusCode.NotFound)]
    [InlineData("192.168.1.20", "null", HttpStatusCode.Forbidden)]
    [InlineData("127.0.0.1", "http://evil.test", HttpStatusCode.Forbidden)]
    [InlineData("172.18.0.5", "http://evil.test", HttpStatusCode.Forbidden)]
    public async Task PostWithAnOrigin_IsJudgedByTheOrigin(string peer, string origin, HttpStatusCode expected)
    {
        using var response = await SendAsync(Post(peer, origin));

        response.StatusCode.ShouldBe(expected);
    }

    [Fact]
    public async Task CrossSiteFetchMark_IsRefusedEvenFromLoopback()
    {
        using var response = await SendAsync(Post("127.0.0.1") with { Extra = ("Sec-Fetch-Site", "cross-site") });

        await ShouldBeRefusedAsync(response);
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
    [InlineData("127.0.0.2")]
    [InlineData("::1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("172.18.0.5")]
    [InlineData("::ffff:172.18.0.5")]
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
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(call.Method, call.Path);
        request.Headers.Host = call.Host ?? request.Headers.Host;
        foreach (var (name, value) in call.Headers())
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        return await client.SendAsync(request, Ct);
    }

    private static async Task ShouldBeRefusedAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.JsonAsync()).GetProperty("detail").GetString().ShouldBe(CrossSiteRequestGuard.Detail);
    }

    private static Call Post(string? peer = null, string? origin = null) => new(HttpMethod.Post, ApiRoutes.RevertRestore) { Peer = peer, Origin = origin };

    private static Call Health(string host) => new(HttpMethod.Get, ApiRoutes.Health) { Host = host };

    private sealed record Call(HttpMethod Method, string Path)
    {
        public string? Peer { get; init; }

        public string? Host { get; init; }

        public string? Origin { get; init; }

        public (string Name, string Value)? Extra { get; init; }

        public IEnumerable<(string Name, string Value)> Headers()
        {
            (string Name, string? Value)[] all = [(TestPeerStartup.Header, Peer), ("Origin", Origin), (Extra?.Name ?? string.Empty, Extra?.Value)];
            return all.Where(header => header.Value is not null).Select(header => (header.Name, header.Value!));
        }
    }

    private sealed class StaticResolver(string host, IPAddress address) : IHostAddressResolver
    {
        public Task<IPAddress[]> ResolveAsync(string name, CancellationToken cancellationToken) =>
            Task.FromResult(string.Equals(name, host, StringComparison.OrdinalIgnoreCase) ? new[] { address } : []);
    }
}
