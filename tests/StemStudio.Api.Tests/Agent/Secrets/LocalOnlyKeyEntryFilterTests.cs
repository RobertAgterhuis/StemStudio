using System.Net;
using Microsoft.Extensions.Logging;
using StemStudio.Api.Agent.Secrets;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Agent;
using StemStudio.TestDoubles.ClaudeKey;

namespace StemStudio.Api.Tests.Agent.Secrets;

/// <summary>011 follow-up claude-key T028: the local-only rule of the key writes [sec] (CK-FR-012, DELTA.md section 4).</summary>
public sealed class LocalOnlyKeyEntryFilterTests : IDisposable
{
    private static readonly IPAddress WebContainer = IPAddress.Parse("172.18.0.5");
    private readonly ClaudeKeyApi _api = new(new() { InternalCaller = "web" });

    public LocalOnlyKeyEntryFilterTests() => _api.Hosts["web"] = [WebContainer];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _api.Dispose();

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("172.18.0.5")]
    [InlineData("::ffff:172.18.0.5")]
    public async Task LoopbackAndTheInternalCaller_MayStoreTheKey(string peer)
    {
        using var response = await PutAsync(peer);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("192.168.1.20")]
    [InlineData("172.18.0.6")]
    [InlineData("none")]
    public async Task OtherPeers_AreRefusedWith403(string peer)
    {
        using var response = await PutAsync(peer);

        await ShouldBeLocalOnlyAsync(response);
        (await _api.StatusAsync()).Stored.ShouldBe(StoredClaudeKeyStateDto.Absent);
    }

    [Fact]
    public async Task WithoutAConfiguredInternalCaller_OnlyLoopbackIsAccepted()
    {
        using var api = new ClaudeKeyApi();
        api.Hosts["web"] = [WebContainer];

        using var response = await api.SendAsync(Put(ClaudeKeySentinels.Stored) with { Peer = WebContainer.ToString() });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("localhost:8081", true)]
    [InlineData("127.0.0.1:8081", true)]
    [InlineData("[::1]:8081", true)]
    [InlineData("api:8081", true)]
    [InlineData("API", true)]
    [InlineData("192.168.1.10:8081", false)]
    [InlineData("stemstudio.lan", false)]
    [InlineData("localhost.evil.test", false)]
    public async Task HostOutsideTheAllowList_IsRefused(string host, bool accepted)
    {
        using var response = await PutAsync("127.0.0.1", host: host);

        if (accepted)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        else
        {
            await ShouldBeLocalOnlyAsync(response);
            await NothingShouldBeStoredAsync();
        }
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("http://web.test", true)]
    [InlineData("http://web.test/", true)]
    [InlineData("http://evil.test", false)]
    [InlineData("null", false)]
    public async Task ForeignOrigin_IsRefused(string? origin, bool accepted)
    {
        using var response = await PutAsync("127.0.0.1", origin: origin);

        if (accepted)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            return;
        }

        await ShouldBeLocalOnlyAsync(response);
        await NothingShouldBeStoredAsync();
    }

    // qa F4: a refused write never replaces or removes a stored key; R9: every refusal is the local-only problem.
    [Theory]
    [InlineData("192.168.1.20", null, null)]
    [InlineData("127.0.0.1", "stemstudio.lan", null)]
    [InlineData("127.0.0.1", null, "http://evil.test")]
    public async Task RefusedWriteOverAStoredKey_KeepsIt(string peer, string? host, string? origin)
    {
        using (var stored = await PutAsync("127.0.0.1"))
        {
            stored.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var replace = await _api.SendAsync(Put(ClaudeKeySentinels.Replacement) with { Peer = peer, Host = host, Origin = origin });
        using var delete = await _api.SendAsync(new KeyRequest(HttpMethod.Delete, ApiRoutes.AgentClaudeKey) { Peer = peer, Host = host, Origin = origin });

        await ShouldBeLocalOnlyAsync(replace);
        await ShouldBeLocalOnlyAsync(delete, "deleteClaudeKey");
        var status = await _api.StatusAsync();
        (status.Stored, status.StoredLastFour).ShouldBe((StoredClaudeKeyStateDto.Readable, "STO1"));
    }

    // R10: the API has no forwarded-headers middleware; a LAN client cannot claim to be loopback or localhost through them.
    [Theory]
    [InlineData("X-Forwarded-For", "127.0.0.1")]
    [InlineData("X-Forwarded-Host", "localhost")]
    [InlineData("X-Real-IP", "127.0.0.1")]
    [InlineData("Forwarded", "for=127.0.0.1;host=localhost")]
    public async Task ForwardedHeaders_CannotSpoofThePeer(string header, string value)
    {
        var request = Put(ClaudeKeySentinels.Stored) with
        {
            Peer = "192.168.1.20",
            Headers = new Dictionary<string, string> { [header] = value, ["X-Forwarded-Proto"] = "http" },
        };

        using var response = await _api.SendAsync(request);

        await ShouldBeLocalOnlyAsync(response);
        await NothingShouldBeStoredAsync();
    }

    [Fact]
    public async Task AllForwardedHeadersTogether_CannotSpoofThePeer()
    {
        var request = Put(ClaudeKeySentinels.Stored) with
        {
            Peer = "192.168.1.20",
            Headers = new Dictionary<string, string> { ["X-Forwarded-For"] = "127.0.0.1", ["X-Forwarded-Host"] = "localhost", ["X-Forwarded-Proto"] = "http" },
        };

        using var response = await _api.SendAsync(request);

        await ShouldBeLocalOnlyAsync(response);
        await NothingShouldBeStoredAsync();
    }

    // R8: the browser's first line against cross-site writes (T5) - the CORS policy of the web origin allows GET only, so a
    // preflight for PUT or DELETE on the key route never lists the method.
    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("POST")]
    public async Task CorsPreflightForAWrite_NeverAllowsTheMethod(string method)
    {
        using var response = await PreflightAsync(method);

        AllowedMethods(response).ShouldNotContain(method);
        await NothingShouldBeStoredAsync();
    }

    [Fact]
    public async Task CorsPreflightForGet_IsAllowedForTheWebOrigin()
    {
        using var response = await PreflightAsync("GET");

        AllowedMethods(response).ShouldContain("GET");
        response.Headers.GetValues("Access-Control-Allow-Origin").ShouldBe(["http://web.test"]);
    }

    [Fact]
    public async Task Filter_RunsBeforeTheBodyIsRead()
    {
        using var oversized = await _api.SendAsync(Put(ClaudeKeySentinels.Stored + new string('a', 4096)) with { Peer = "192.168.1.20" });
        using var malformed = await _api.SendAsync(new KeyRequest(HttpMethod.Put, ApiRoutes.AgentClaudeKey) { Body = "not json", Peer = "192.168.1.20" });

        await ShouldBeLocalOnlyAsync(oversized);
        await ShouldBeLocalOnlyAsync(malformed);
        await NothingShouldBeStoredAsync();
    }

    [Fact]
    public async Task GetStatus_IsNotFiltered()
    {
        using var response = await _api.SendAsync(new KeyRequest(HttpMethod.Get, ApiRoutes.AgentClaudeKey) { Peer = "192.168.1.20", Host = "stemstudio.lan", Origin = "http://evil.test" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refusals_AreLoggedAtMostOncePerMinuteWithoutTheBody()
    {
        using (await PutAsync("192.168.1.20"))
        using (await PutAsync("192.168.1.21", host: "stemstudio.lan"))
        {
            _api.Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        }

        using (await PutAsync("127.0.0.1", origin: "http://evil.test"))
        {
        }

        var refused = _api.Factory.Logs.For(nameof(LocalOnlyKeyEntryFilter)).ToList();
        refused.Count.ShouldBe(2);
        refused.ShouldAllBe(entry => entry.Level == LogLevel.Information);
        refused[0].Message.ShouldContain("peer");
        refused[0].Message.ShouldContain("192.168.1.20");
        refused[1].Message.ShouldContain("origin");
        foreach (var form in ClaudeKeySentinels.SearchForms(ClaudeKeySentinels.Stored))
        {
            _api.Factory.Logs.Entries.ShouldAllBe(entry => !entry.Message.Contains(form, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task UnresolvableInternalCaller_AcceptsOnlyLoopback()
    {
        _api.Hosts.Clear();

        using var response = await PutAsync(WebContainer.ToString());

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DnsResolver_ResolvesLocalhostAndSwallowsUnknownHosts()
    {
        var resolver = new DnsHostAddressResolver();

        (await resolver.ResolveAsync("localhost", Ct)).ShouldContain(address => IPAddress.IsLoopback(address));
        (await resolver.ResolveAsync("stemstudio-no-such-host.invalid", Ct)).ShouldBeEmpty();
    }

    [Fact]
    public void Policy_ReadsItsConfiguration()
    {
        KeyEntryPolicy.InternalCallerKey.ShouldBe("StemStudio:Agent:KeyEntry:InternalCaller");
        new KeyEntryPolicy(" ", "http://localhost:8080").InternalCaller.ShouldBeNull();
        new KeyEntryPolicy("web", "http://localhost:8080/").IsAllowedOrigin("http://LOCALHOST:8080").ShouldBeTrue();
    }

    private Task<HttpResponseMessage> PutAsync(string peer, string? host = null, string? origin = null) =>
        _api.SendAsync(Put(ClaudeKeySentinels.Stored) with { Peer = peer, Host = host, Origin = origin });

    private static KeyRequest Put(string key) => new(HttpMethod.Put, ApiRoutes.AgentClaudeKey) { Body = "{\"apiKey\":\"" + key + "\"}" };

    private async Task<HttpResponseMessage> PreflightAsync(string method)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, ApiRoutes.AgentClaudeKey);
        request.Headers.Add("Origin", "http://web.test");
        request.Headers.Add("Access-Control-Request-Method", method);
        request.Headers.Add("Access-Control-Request-Headers", "content-type");
        return await _api.Client.SendAsync(request, Ct);
    }

    private static string[] AllowedMethods(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Access-Control-Allow-Methods", out var values)
            ? [.. values.SelectMany(value => value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))]
            : [];

    private async Task NothingShouldBeStoredAsync() => (await _api.StatusAsync()).Stored.ShouldBe(StoredClaudeKeyStateDto.Absent);

    private static async Task ShouldBeLocalOnlyAsync(HttpResponseMessage response, string operation = "storeClaudeKey")
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await ContractAssert.MatchesAsync(response, operation);
        var problem = await response.JsonAsync();
        problem.GetProperty("type").GetString().ShouldBe(AgentProblemTypes.Uri(AgentProblemTypes.LocalOnly));
        problem.GetProperty("detail").GetString().ShouldBe(LocalOnlyKeyEntryFilter.Detail);
    }
}
