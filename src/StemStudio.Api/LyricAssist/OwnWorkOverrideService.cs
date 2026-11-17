using StemStudio.Contracts;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Domain;
using StemStudio.Domain.Lyrics.Assist;

namespace StemStudio.Api.LyricAssist;

/// <summary>
/// The own-work override (owner decision Q1, FR-031): the user confirms the current statement with a typed name for one track the sheet
/// currently matches; the override covers only that track and never the verbatim hold-back or the imitation refusal. Not an agent tool.
/// </summary>
/// <param name="stores">Stores.</param>
/// <param name="corpus">Library lyrics.</param>
/// <param name="clock">Clock.</param>
public sealed class OwnWorkOverrideService(LyricAssistStores stores, LyricsCorpusProvider corpus, TimeProvider clock)
{
    /// <summary>Confirms own work.</summary>
    /// <param name="lyricsId">Sheet id.</param>
    /// <param name="request">The confirmation.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200, 400, 404 or 409.</returns>
    public async Task<IResult> ConfirmAsync(Guid lyricsId, OwnWorkConfirmRequest? request, CancellationToken cancellationToken)
    {
        if (await stores.Sheets.FindAsync(lyricsId, cancellationToken) is not { } sheet)
        {
            return SheetProblems.NotFound(lyricsId);
        }

        if (RequestProblem(request) is { } problem)
        {
            return problem;
        }

        var matches = VerbatimGuard.MatchingTracks(sheet.Text, await corpus.RefreshAsync(cancellationToken));
        if (matches.FirstOrDefault(track => track.TrackId == request!.MatchedTrackId) is not { } track)
        {
            return LyricAssistProblems.OverrideNotApplicable();
        }

        OwnWorkOverride ownWork;
        try
        {
            ownWork = OwnWorkOverride.Create(request!.ConfirmedBy, track, clock.GetUtcNow().UtcDateTime);
        }
        catch (DomainValidationException invalid)
        {
            return ApiProblems.Validation(invalid.Errors, string.Empty);
        }

        sheet.ConfirmOwnWork(ownWork);
        await stores.Sheets.SaveAsync(cancellationToken);
        return TypedResults.Ok(LyricAssistMapper.ToDto(ownWork));
    }

    private static IResult? RequestProblem(OwnWorkConfirmRequest? request)
    {
        if (request is not { Confirm: true })
        {
            return ApiProblems.Validation("confirm", "Tick the box to confirm that this text is your own work.");
        }

        return request.StatementVersion != OwnWorkOverride.CurrentStatementVersion ? LyricAssistProblems.StatementOutdated() : null;
    }

    /// <summary>Revokes the override.</summary>
    /// <param name="lyricsId">Sheet id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>204 or 404.</returns>
    public async Task<IResult> RevokeAsync(Guid lyricsId, CancellationToken cancellationToken)
    {
        if (await stores.Sheets.FindAsync(lyricsId, cancellationToken) is not { } sheet || !sheet.RevokeOwnWork())
        {
            return ApiProblems.NotFound($"Lyrics {lyricsId} have no own-work confirmation.");
        }

        await stores.Sheets.SaveAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}

/// <summary>Maps the own-work override routes.</summary>
public static class OwnWorkOverrideEndpoints
{
    /// <summary>Maps PUT and DELETE.</summary>
    /// <param name="app">Route builder.</param>
    /// <returns>The builder.</returns>
    public static IEndpointRouteBuilder MapOwnWorkOverrideEndpoints(this IEndpointRouteBuilder app)
    {
        const string Route = ApiRoutes.LyricsSheets + "/{lyricsId:guid}/own-work-override";
        app.MapPut(Route, (Guid lyricsId, OwnWorkConfirmRequest? request, OwnWorkOverrideService service, CancellationToken ct) => service.ConfirmAsync(lyricsId, request, ct))
            .WithName("confirmOwnWork").WithTags(LyricAssistEndpoints.Tag).Produces<OwnWorkOverrideDto>().ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        app.MapDelete(Route, (Guid lyricsId, OwnWorkOverrideService service, CancellationToken ct) => service.RevokeAsync(lyricsId, ct))
            .WithName("revokeOwnWork").WithTags(LyricAssistEndpoints.Tag).Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
        return app;
    }
}
