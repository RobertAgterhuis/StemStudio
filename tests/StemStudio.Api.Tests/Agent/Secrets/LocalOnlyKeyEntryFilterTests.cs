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

        response.StatusCode.ShouldBe(accepted ? HttpStatusCode.OK : HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Filter_RunsBeforeTheBodyIsRead()
    {
        using var oversized = await _api.SendAsync(Put(ClaudeKeySentinels.Stored + new string('a', 4096)) with { Peer = "192.168.1.20" });
        using var malformed = await _api.SendAsync(new KeyRequest(HttpMethod.Put, ApiRoutes.AgentClaudeKey) { Body = "not json", Peer = "192.168.1.20" });

        oversized.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        malformed.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
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

    private static async Task ShouldBeLocalOnlyAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await ContractAssert.MatchesAsync(response, "storeClaudeKey");
        var problem = await response.JsonAsync();
        problem.GetProperty("type").GetString().ShouldBe(AgentProblemTypes.Uri(AgentProblemTypes.LocalOnly));
        problem.GetProperty("detail").GetString().ShouldBe(LocalOnlyKeyEntryFilter.Detail);
    }
}
