using Microsoft.AspNetCore.ResponseCompression;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Domain.InstrumentViews;

namespace StemStudio.Api.InstrumentViews;

/// <summary>
/// Maps <c>getVocalPitch</c>, <c>analyseVocalPitch</c> and <c>deleteVocalPitch</c> (backend 1.15.0; FR-040). The GET answer can
/// hold 90,000 frames and is the only response served compressed (gzip or Brotli on request).
/// </summary>
public static class VocalPitchEndpoints
{
    /// <summary>The route of the reference melody.</summary>
    public const string Route = "/api/tracks/{trackId:guid}/vocal-pitch";

    private const string Suffix = "/vocal-pitch";

    /// <summary>Maps the reference melody operations.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapVocalPitchEndpoints(this IEndpointRouteBuilder app) => app.MapPlainAnalysis(new PlainAnalysisRoute<TrackAnalysisDto<VocalPitchDocumentDto>>(
        Route,
        new(AnalysisKind.VocalPitch, InstrumentViewMapper.ToVocalPitchDto),
        ("getVocalPitch", "analyseVocalPitch", "deleteVocalPitch"),
        "reference melody"));

    /// <summary>Registers the response compression used for the reference melody (JSON only) and puts it first in the pipeline.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddVocalPitchCompression(this IServiceCollection services) => services
        .AddSingleton<IStartupFilter, VocalPitchCompressionFilter>()
        .AddResponseCompression(options =>
    {
        // The melody holds no secret and no request text, so compressing it over HTTPS gives no BREACH oracle.
        options.EnableForHttps = true;
        options.MimeTypes = ["application/json"];
        options.Providers.Add<BrotliCompressionProvider>();
        options.Providers.Add<GzipCompressionProvider>();
    });

    /// <summary>Compresses the GET of the reference melody only; every other response passes unchanged.</summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The same builder.</returns>
    public static IApplicationBuilder UseVocalPitchCompression(this IApplicationBuilder app) =>
        app.UseWhen(IsMelodyRead, branch => branch.UseResponseCompression());

    /// <summary>True for <c>GET /api/tracks/{id}/vocal-pitch</c>.</summary>
    /// <param name="context">The request.</param>
    /// <returns>Whether the response is compressed.</returns>
    public static bool IsMelodyRead(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var path = context.Request.Path.Value ?? string.Empty;
        return HttpMethods.IsGet(context.Request.Method)
            && path.StartsWith("/api/tracks/", StringComparison.OrdinalIgnoreCase)
            && path.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>Adds <see cref="VocalPitchEndpoints.UseVocalPitchCompression"/> in front of the application pipeline.</summary>
internal sealed class VocalPitchCompressionFilter : IStartupFilter
{
    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.UseVocalPitchCompression();
        next(app);
    };
}
