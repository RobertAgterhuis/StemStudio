using System.Net.Http.Headers;

namespace StemStudio.Api.Tests.TestSupport;

/// <summary>CORS checks shared by the stem and detail stem tests.</summary>
internal static class CorsAssert
{
    public static void AddRangedOriginRequest(HttpRequestMessage request, string origin)
    {
        request.Headers.Add("Origin", origin);
        request.Headers.Range = new RangeHeaderValue(0, 1);
    }

    /// <summary>The web origin is allowed and sees the range headers; any other origin gets no CORS headers.</summary>
    public static void AllowsOnlyTheWebOrigin(HttpResponseMessage response, bool allowed)
    {
        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins).ShouldBe(allowed);
        if (!allowed)
        {
            return;
        }

        origins!.Single().ShouldBe("http://web.test");
        var exposed = string.Join(",", response.Headers.GetValues("Access-Control-Expose-Headers"));
        exposed.ShouldContain("Content-Range");
        exposed.ShouldContain("Accept-Ranges");
        exposed.ShouldContain("ETag");
    }
}
