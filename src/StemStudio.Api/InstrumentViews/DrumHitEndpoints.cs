using StemStudio.Contracts.InstrumentViews;
using StemStudio.Domain.InstrumentViews;

namespace StemStudio.Api.InstrumentViews;

/// <summary>Maps <c>getDrumHits</c>, <c>detectDrumHits</c> and <c>deleteDrumHits</c> (backend 1.15.0; FR-030).</summary>
public static class DrumHitEndpoints
{
    /// <summary>The route of the drum hits.</summary>
    public const string Route = "/api/tracks/{trackId:guid}/drum-hits";

    /// <summary>Maps the drum hits operations.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapDrumHitEndpoints(this IEndpointRouteBuilder app) => app.MapPlainAnalysis(new PlainAnalysisRoute<TrackAnalysisDto<DrumHitsDocumentDto>>(
        Route,
        new(AnalysisKind.DrumHits, InstrumentViewMapper.ToDrumHitsDto),
        ("getDrumHits", "detectDrumHits", "deleteDrumHits"),
        "drum hits"));
}
