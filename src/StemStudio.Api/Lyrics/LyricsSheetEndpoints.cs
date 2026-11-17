using StemStudio.Api.LibraryCare.Gate;
using StemStudio.Api.Styles;
using StemStudio.Contracts;
using StemStudio.Contracts.Lyrics;
using StemStudio.Domain.Lyrics;

namespace StemStudio.Api.Lyrics;

/// <summary>Maps the own-lyrics routes of backend 1.9.0 (feature 010).</summary>
public static partial class LyricsSheetEndpoints
{
    /// <summary>Longest text accepted before normalisation.</summary>
    public const int MaxRawTextLength = 65_536;

    /// <summary>Maps the lyrics sheet endpoints.</summary>
    /// <param name="app">Endpoint builder.</param>
    /// <returns>The builder.</returns>
    public static IEndpointRouteBuilder MapLyricsSheetEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(ApiRoutes.LyricsSections, (SectionsRequest? request) => ProposeSections(request))
            .WithName("proposeSections").WithTags("songs").Produces<SectionProposalDto>().ProducesValidationProblem();
        return app.MapSheetEndpoints();
    }

    /// <summary>Proposes section markers; nothing is stored.</summary>
    /// <param name="request">The text.</param>
    /// <returns>200 or 400.</returns>
    public static IResult ProposeSections(SectionsRequest? request)
    {
        if (string.IsNullOrWhiteSpace(request?.Text) || request.Text.Length > MaxRawTextLength)
        {
            return ApiProblems.Validation("text", "Enter 1 to 65,536 characters of lyrics.");
        }

        var proposal = LyricsSections.Propose(request.Text);
        return TypedResults.Ok(new SectionProposalDto
        {
            Text = proposal.Text,
            Sections = proposal.Sections.Select(ToDto).ToList(),
            WordsPerSecondAt120s = proposal.WordsPerSecondAt120s,
        });
    }

    private static LyricsSectionDto ToDto(LyricsSection section) =>
        new() { Marker = StyleMapper.Map<SectionMarkerDto>(section.Marker), Number = section.Number, LineCount = section.LineCount };

    private const string SheetRoute = ApiRoutes.LyricsSheets + "/{lyricsId:guid}";

    private static IEndpointRouteBuilder MapSheetEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.LyricsSheets, (string? q, LyricsSheetService sheets, CancellationToken ct) => sheets.ListAsync(q, ct))
            .WithName("listLyricsSheets").WithTags("songs").Produces<List<LyricsSheetDto>>().ProducesValidationProblem();
        app.MapPost(ApiRoutes.LyricsSheets, (LyricsInput? input, LyricsSheetService sheets, CancellationToken ct) => sheets.CreateAsync(input, ct))
            .WithName("createLyricsSheet").WithTags("songs").Produces<LyricsSheetDto>(StatusCodes.Status201Created).ProducesValidationProblem();
        app.MapPost(ApiRoutes.LyricsSheetUpload, async (HttpRequest request, LyricsSheetService sheets, CancellationToken ct) =>
                request.HasFormContentType ? await sheets.UploadAsync(await request.ReadFormAsync(ct), ct) : ApiProblems.Validation("file", "Send the file as multipart/form-data."))
            .WithName("uploadLyricsSheet").WithTags("songs").DisableAntiforgery().Produces<LyricsSheetDto>(StatusCodes.Status201Created).ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge).ProducesProblem(StatusCodes.Status415UnsupportedMediaType);
        return app.MapSheetItemEndpoints();
    }

    private static IEndpointRouteBuilder MapSheetItemEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(SheetRoute, (Guid lyricsId, LyricsSheetService sheets, CancellationToken ct) => sheets.GetAsync(lyricsId, ct))
            .WithName("getLyricsSheet").WithTags("songs").Produces<LyricsSheetDto>().ProducesProblem(StatusCodes.Status404NotFound);
        app.MapPut(SheetRoute, (Guid lyricsId, LyricsInput? input, LyricsSheetService sheets, CancellationToken ct) => sheets.UpdateAsync(lyricsId, input, ct))
            .WithName("updateLyricsSheet").WithTags("songs").Produces<LyricsSheetDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        app.MapDelete(SheetRoute, (Guid lyricsId, LyricsSheetService sheets, CancellationToken ct) => sheets.DeleteAsync(lyricsId, ct))
            .WithName("deleteLyricsSheet").RequireGateOpen().WithTags("songs").Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
        return app;
    }
}
