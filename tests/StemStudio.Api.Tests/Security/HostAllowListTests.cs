using System.Net;
using Microsoft.AspNetCore.Http;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;

namespace StemStudio.Api.Tests.Security;

/// <summary>Refactor 003 step 2: the API's host allow-list (<c>StemStudio:AllowedHosts</c>, ADR-027 D6 DNS rebinding).</summary>
public sealed class HostAllowListTests
{
    private const string AllowedHostsKey = "StemStudio:AllowedHosts";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("localhost;127.0.0.1;[::1];api;stemstudio.lan", "stemstudio.lan:8081", HttpStatusCode.OK)]
    [InlineData("localhost;127.0.0.1;[::1];api;192.168.1.10", "192.168.1.10:8081", HttpStatusCode.OK)]
    [InlineData("stemstudio.lan", "localhost", HttpStatusCode.BadRequest)]
    [InlineData(" ", "evil.example", HttpStatusCode.BadRequest)]
    [InlineData(" ", "api:8081", HttpStatusCode.OK)]
    public async Task ConfiguredList_ReplacesTheDefault(string configured, string host, HttpStatusCode expected)
    {
        using var factory = new ApiFactory { Settings = new Dictionary<string, string?> { [AllowedHostsKey] = configured } };

        using var response = await HealthAsync(factory, host);

        response.StatusCode.ShouldBe(expected);
    }

    [Fact]
    public async Task KeyEntry_WithAForeignHost_IsRefusedBeforeTheKeyFilter()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Put, ApiRoutes.AgentClaudeKey) { Content = new StringContent("{}") };
        request.Headers.Host = "evil.example";

        using var response = await client.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task EmptyHost_IsRefused()
    {
        using var factory = new ApiFactory();
        var server = factory.Server;

        var context = await server.SendAsync(http =>
        {
            http.Request.Method = HttpMethods.Get;
            http.Request.Path = ApiRoutes.Health;
            http.Request.Host = default;
        }, Ct);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    private static async Task<HttpResponseMessage> HealthAsync(ApiFactory factory, string host)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.Health);
        request.Headers.Host = host;
        return await client.SendAsync(request, Ct);
    }
}
