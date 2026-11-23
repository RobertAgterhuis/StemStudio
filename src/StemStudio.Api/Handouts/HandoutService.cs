using StemStudio.Api.Assets;
using StemStudio.Api.Insights;
using StemStudio.Api.Library;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Handouts;
using StemStudio.Domain;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Handouts;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Handouts;

/// <summary>The collaborators of <see cref="HandoutService"/> that store and file items.</summary>
/// <param name="Assets">Asset repository.</param>
/// <param name="Reader">Maps items.</param>
/// <param name="Filer">Files new items in the library folders (feature 022).</param>
/// <param name="Disk">Free-space guard.</param>
/// <param name="Gate">The start lock (active limit, name, insert and submit).</param>
public sealed record HandoutStore(IAssetRepository Assets, AssetService Reader, ILibraryFiler Filer, DiskSpaceGuard Disk, HandoutGate Gate);

/// <summary>
/// Creates handout library items and makes failed ones again (FR-008..FR-014): validate, plan, assemble the document, store the
/// queued item and submit the worker handout job.
/// </summary>
/// <param name="planner">Plans the handout.</param>
/// <param name="assembler">Assembles the worker document.</param>
/// <param name="worker">The worker insights lane.</param>
/// <param name="store">Item storage.</param>
/// <param name="clock">Clock.</param>
/// <param name="logger">Logger.</param>
public sealed partial class HandoutService(
    HandoutPlanner planner, HandoutAssembler assembler, IWorkerInsightsClient worker, HandoutStore store, TimeProvider clock, ILogger<HandoutService> logger)
{
    /// <summary>Most handouts of one song or setlist that are queued or rendering at a time (security review 020, finding 4).</summary>
    public const int MaxActivePerSource = 3;

    /// <summary>The <c>document</c> error when the worker refuses a handout (its reason is logged, never echoed).</summary>
    public const string WorkerRefused = "The processing service could not lay out this handout. Try again, or leave out an ingredient.";

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary><c>POST /api/assets/handouts</c>.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>202, 404, 409, 422 or 503.</returns>
    public async Task<IResult> CreateAsync(CreateHandoutRequest request, CancellationToken cancellationToken)
    {
        if (HandoutRequests.Validate(request) is { } invalid)
        {
            return invalid;
        }

        var ask = new HandoutAsk(HandoutMapper.ToRecipe(request), request.Name?.Trim());
        var (draft, problem) = await planner.PlanAsync(ask, cancellationToken);
        return problem ?? (store.Disk.HasRoom() ? await CreateAsync(ask, draft!, cancellationToken) : HandoutProblems.InsufficientDiskSpace());
    }

    /// <summary>Plans the stored recipe of a failed handout again (current data) and resubmits it.</summary>
    /// <param name="asset">A failed handout item.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>202, 404, 409 or 503.</returns>
    public async Task<IResult> RetryAsync(Asset asset, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(asset);
        var (draft, problem) = await planner.PlanAsync(new HandoutAsk(asset.Provenance.HandoutRecipe!, null), cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        asset.Retry(Now);
        asset.Replan(draft!.Facts);
        return await SubmitAsync(asset, draft, false, cancellationToken);
    }

    private Task<IResult> CreateAsync(HandoutAsk ask, HandoutDraft draft, CancellationToken cancellationToken) => store.Gate.RunAsync(
        async () =>
        {
            if (await ActiveCountAsync(ask.Recipe, cancellationToken) >= MaxActivePerSource)
            {
                return InsightsProblems.HandoutsActive(MaxActivePerSource);
            }

            var label = Families.Label(draft.Handout.Family, draft.Handout.Transposition);
            var title = draft.Handout.Setlist?.Name ?? draft.Handout.Songs[0].Material.Title;
            var name = ask.Name ?? AssetNaming.DefaultName(title, HandoutNaming.Contents(label), await store.Assets.NamesAsync(cancellationToken));
            var asset = Asset.CreateHandout(Guid.NewGuid(), name, new HandoutStart(Provenance(ask.Recipe, draft, label), draft.Facts), Now);
            return await SubmitAsync(asset, draft, true, cancellationToken);
        },
        cancellationToken);

    /// <summary>The queued or rendering handouts of the recipe's song or setlist.</summary>
    private async Task<int> ActiveCountAsync(HandoutRecipe recipe, CancellationToken cancellationToken) =>
        (await store.Assets.ListActiveAsync(cancellationToken)).Count(asset => asset.Kind == AssetKind.Handout
            && asset.Provenance.HandoutRecipe is { } active && active.TrackId == recipe.TrackId && active.SetlistId == recipe.SetlistId);

    private async Task<IResult> SubmitAsync(Asset asset, HandoutDraft draft, bool added, CancellationToken cancellationToken)
    {
        WorkerHandoutJob job;
        try
        {
            job = await worker.CreateHandoutJobAsync(new WorkerHandoutJobRequest(assembler.Document(draft.Handout), draft.Facts.Paper), cancellationToken);
        }
        catch (WorkerUnavailableException)
        {
            return InsightsProblems.WorkerUnavailable();
        }
        catch (WorkerRequestException refused)
        {
            LogRefused(logger, refused.Detail);
            return HandoutProblems.Field("document", WorkerRefused);
        }

        var kept = await SubmittedJob.KeepAsync(
            () => StoreAsync(asset, job.HandoutJobId, added), () => worker.DeleteHandoutJobAsync(job.HandoutJobId, CancellationToken.None), logger);
        return kept!;
    }

    /// <summary>Stores the item of an accepted job; no cancellation from here on (the job exists).</summary>
    private async Task<IResult?> StoreAsync(Asset asset, string jobId, bool added)
    {
        asset.AssignJob(jobId, Now);
        if (added)
        {
            await store.Filer.FileAsync(asset, CancellationToken.None);
            store.Assets.Add(asset);
        }

        await store.Assets.SaveAsync(CancellationToken.None);
        return TypedResults.Accepted(ApiRoutes.Asset(asset.Id), await store.Reader.ToDtoAsync(asset, CancellationToken.None));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The processing service refused a handout: {Reason}")]
    private static partial void LogRefused(ILogger logger, string reason);

    private static Provenance Provenance(HandoutRecipe recipe, HandoutDraft draft, string label)
    {
        var handout = draft.Handout;
        var first = handout.Songs[0].Material;
        var title = handout.Setlist?.Name ?? first.Title;
        return new Provenance
        {
            Origin = AssetOrigin.Handout,
            TrackId = handout.Kind == HandoutKind.Song ? first.TrackId : null,
            TrackTitle = title.Length <= ExportService.MaxTrackTitleLength ? title : title[..ExportService.MaxTrackTitleLength],
            TrackArtist = handout.Kind == HandoutKind.Song ? first.Artist : null,
            SourceAssetIds = handout.Songs.Where(song => song.Include.Contains(IngredientCode.Notation)).SelectMany(song => song.Notation.Select(item => item.AssetId)).Distinct().ToList(),
            Contents = HandoutNaming.Contents(label),
            HandoutRecipe = recipe with { SetlistName = handout.Setlist?.Name },
        };
    }
}

/// <summary>Request rules of <c>createHandout</c> that need no data (422).</summary>
public static class HandoutRequests
{
    /// <summary>Most notation ids in a request.</summary>
    public const int MaxNotationIds = 400;

    /// <summary>The problem of an invalid request, or null.</summary>
    /// <param name="request">The request.</param>
    /// <returns>422 or null.</returns>
    public static IResult? Validate(CreateHandoutRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (request.Source is null || (request.Source.TrackId is null) == (request.Source.SetlistId is null))
        {
            errors["source"] = ["Give exactly one of trackId and setlistId."];
        }

        if (AddEnumErrors(errors, request))
        {
            AddCombinationErrors(errors, request);
        }

        if (request.NotationAssetIds is { Count: > MaxNotationIds })
        {
            errors["notationAssetIds"] = [$"Choose at most {MaxNotationIds} notation items."];
        }

        AddNameErrors(errors, request.Name);
        return errors.Count == 0 ? null : HandoutProblems.Fields(errors);
    }

    /// <summary>Refuses values the contract does not define (numbers, unknown names); true when every enum value is defined.</summary>
    private static bool AddEnumErrors(Dictionary<string, string[]> errors, CreateHandoutRequest request)
    {
        var before = errors.Count;
        if (!Enum.IsDefined(request.Family))
        {
            errors["family"] = ["Must be one of overview, vocals, guitar, bass, keys, drums, melodic."];
        }

        if (request.Include is { } include && !include.All(code => Enum.IsDefined(code)))
        {
            errors["include"] = ["Use the ingredient codes of the contract (facts, form-chart, chords-over-lyrics, lyrics, chord-chart, notation, practice-notes, practice-progress, setlist-notes)."];
        }

        if (request.Transposition is { } transposition && !Enum.IsDefined(transposition))
        {
            errors["transposition"] = ["Must be one of c, b-flat, e-flat, f."];
        }

        if (request.Paper is { } paper && !Enum.IsDefined(paper))
        {
            errors["paper"] = ["Must be a4 or letter."];
        }

        return errors.Count == before;
    }

    private static void AddCombinationErrors(Dictionary<string, string[]> errors, CreateHandoutRequest request)
    {
        if (request.Transposition is not null && request.Family != HandoutFamilyDto.Melodic)
        {
            errors["transposition"] = ["A transposition is only offered for the melodic family."];
        }

        AddIncludeErrors(errors, request);
    }

    private static void AddIncludeErrors(Dictionary<string, string[]> errors, CreateHandoutRequest request)
    {
        var kind = request.Source?.TrackId is not null ? HandoutKind.Song : HandoutKind.Setlist;
        var include = request.Include?.Select(code => HandoutMapper.Convert<IngredientCode>(code)).ToList();
        if (IngredientPlanner.Refused(HandoutMapper.Convert<HandoutFamily>(request.Family), kind, include) is { Count: > 0 } refused)
        {
            errors["include"] = [.. refused];
        }
    }

    private static void AddNameErrors(Dictionary<string, string[]> errors, string? name)
    {
        if (name is null)
        {
            return;
        }

        try
        {
            AssetNaming.Validate(name);
        }
        catch (DomainValidationException invalid)
        {
            errors["name"] = [.. invalid.Errors.Select(error => error.Message)];
        }
    }
}
