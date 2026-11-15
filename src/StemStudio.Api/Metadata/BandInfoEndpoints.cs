using StemStudio.Api.Tracks;
using StemStudio.Contracts.Artists;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Api.Metadata;

/// <summary>Maps <c>GET /api/tracks/{trackId}/band-info</c> and <c>POST /api/tracks/{trackId}/metadata/lookup</c> (backend 1.4.0).</summary>
public static class BandInfoEndpoints
{
    /// <summary>Maps the band-info endpoints.</summary>
    /// <param name="app">Endpoint builder.</param>
    /// <returns>The builder.</returns>
    public static IEndpointRouteBuilder MapBandInfoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(TrackEndpoints.TrackRoute + "/band-info", (Guid trackId, MetadataService metadata, CancellationToken ct) => metadata.GetBandInfoAsync(trackId, ct))
            .WithName("getBandInfo").WithTags("band-info").Produces<BandInfoDto>().ProducesProblem(StatusCodes.Status404NotFound);
        app.MapPost(TrackEndpoints.TrackRoute + "/metadata/lookup", (Guid trackId, MetadataLookupRequest? request, MetadataService metadata, CancellationToken ct) =>
                metadata.LookupAsync(trackId, request, ct))
            .WithName("lookupTrackMetadata").WithTags("band-info").Produces<TrackDto>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);
        return app;
    }
}
