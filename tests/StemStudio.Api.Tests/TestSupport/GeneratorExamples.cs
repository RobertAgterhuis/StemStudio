using System.Net;
using System.Text;

namespace StemStudio.Api.Tests.TestSupport;

/// <summary>The generator contract example copies (<c>Generator/Examples</c>) and generator problem bodies.</summary>
internal static class GeneratorExamples
{
    public const string GenerationId = "5b0f7e2c9a1d4c3e8f6a2b1d0c9e8f7a";

    public static string Read(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Generator", "Examples", name + ".json"));

    public static HttpResponseMessage Response(HttpStatusCode status, string name) => StubHttpMessageHandler.Json(status, Read(name));

    /// <summary>The example with another generation id (each test generation gets its own job).</summary>
    public static HttpResponseMessage ResponseFor(HttpStatusCode status, string name, string generationId) =>
        StubHttpMessageHandler.Json(status, Read(name).Replace(GenerationId, generationId, StringComparison.Ordinal));

    public static HttpResponseMessage Problem(HttpStatusCode status, string? code, string detail)
    {
        var codeMember = code is null ? string.Empty : $",\"code\":\"{code}\"";
        var json = $"{{\"type\":\"about:blank\",\"title\":\"Refused\",\"status\":{(int)status},\"detail\":\"{detail}\"{codeMember}}}";
        return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/problem+json") };
    }
}
