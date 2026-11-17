using StemStudio.Contracts;
using StemStudio.Contracts.Setlists;

namespace StemStudio.Api.Setlists;

/// <summary>Maps the setlist operations of backend 1.13.0 (tag <c>setlists</c>).</summary>
public static class SetlistEndpoints
{
    private const string Tag = "setlists";
    private const string ItemRoute = ApiRoutes.Setlists + "/{setlistId:guid}";

    /// <summary>Maps list, create, get, update, delete, replace entries and the PDF.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapSetlistEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.Setlists, (SetlistService setlists, CancellationToken ct) => setlists.ListAsync(ct))
            .WithName("listSetlists").WithTags(Tag).Produces<IReadOnlyList<SetlistSummaryDto>>();
        app.MapPost(ApiRoutes.Setlists, (SetlistCreateRequest request, SetlistService setlists, CancellationToken ct) => setlists.CreateAsync(request, ct))
            .WithName("createSetlist").WithTags(Tag).Produces<SetlistDto>(StatusCodes.Status201Created).ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);
        app.MapGet(ItemRoute, (Guid setlistId, bool? acknowledgeRemoved, SetlistService setlists, CancellationToken ct) => setlists.GetAsync(setlistId, acknowledgeRemoved ?? false, ct))
            .WithName("getSetlist").WithTags(Tag).Produces<SetlistDto>().ProducesProblem(StatusCodes.Status404NotFound);
        app.MapPatch(ItemRoute, (Guid setlistId, SetlistUpdateRequest request, SetlistService setlists, CancellationToken ct) => setlists.UpdateAsync(setlistId, request, ct))
            .WithName("updateSetlist").WithTags(Tag).Produces<SetlistDto>().ProducesProblem(StatusCodes.Status404NotFound).ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);
        app.MapDelete(ItemRoute, (Guid setlistId, SetlistService setlists, CancellationToken ct) => setlists.DeleteAsync(setlistId, ct))
            .WithName("deleteSetlist").WithTags(Tag).Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
        app.MapPut(ItemRoute + "/entries", (Guid setlistId, SetlistEntriesUpdateRequest request, SetlistService setlists, CancellationToken ct) => setlists.ReplaceEntriesAsync(setlistId, request, ct))
            .WithName("replaceSetlistEntries").WithTags(Tag).Produces<SetlistDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);
        app.MapGet(ItemRoute + "/pdf", PdfAsync)
            .WithName("getSetlistPdf").WithTags(Tag).Produces(StatusCodes.Status200OK, contentType: "application/pdf").ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status503ServiceUnavailable).ProducesProblem(StatusCodes.Status504GatewayTimeout);
        return app;
    }

    private static Task<IResult> PdfAsync(
        Guid setlistId, bool? lyrics, bool? sections, bool? notes, bool? pageBreakPerSong, SetlistPdfService pdf, CancellationToken ct) =>
        pdf.PdfAsync(setlistId, new SetlistPdfOptions(lyrics ?? true, sections ?? true, notes ?? true, pageBreakPerSong ?? true), ct);
}
