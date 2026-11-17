using System.Net;
using StemStudio.Api.Security;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;

namespace StemStudio.Api.Tests.Security;

/// <summary>Security review 013 item 12: body-less state-changing POSTs cannot be fired from another site.</summary>
public sealed class CrossSiteRequestGuardTests : ApiTestBase
{
    [Theory]
    [InlineData("http://evil.test", null)]
    [InlineData("null", null)]
    [InlineData(null, "cross-site")]
    [InlineData("http://web.test", "cross-site")]
    public async Task CrossSitePost_IsRefused(string? origin, string? fetchSite)
    {
        using var response = await SendAsync(HttpMethod.Post, ApiRoutes.RevertRestore, origin, fetchSite);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.JsonAsync()).GetProperty("detail").GetString().ShouldBe(CrossSiteRequestGuard.Detail);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("http://web.test", null)]
    [InlineData("http://WEB.test/", "same-site")]
    [InlineData(null, "none")]
    public async Task SameSiteOrServerSidePost_Passes(string? origin, string? fetchSite)
    {
        using var response = await SendAsync(HttpMethod.Post, ApiRoutes.RevertRestore, origin, fetchSite);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpperCasePath_IsGuardedToo()
    {
        using var response = await SendAsync(HttpMethod.Post, ApiRoutes.VerifyBackup("x.zip").ToUpperInvariant(), "http://evil.test", null);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CrossSiteRead_Passes()
    {
        using var response = await SendAsync(HttpMethod.Get, ApiRoutes.LibraryCareStatus, "http://evil.test", "cross-site");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Preflight_IsAnsweredByCors()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, ApiRoutes.RevertRestore);
        request.Headers.Add("Origin", "http://evil.test");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        using var response = await Client.SendAsync(request, Ct);

        response.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden);
        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? origin, string? fetchSite)
    {
        using var request = new HttpRequestMessage(method, path);
        if (origin is not null)
        {
            request.Headers.Add("Origin", origin);
        }

        if (fetchSite is not null)
        {
            request.Headers.Add("Sec-Fetch-Site", fetchSite);
        }

        return await Client.SendAsync(request, Ct);
    }
}
