using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Domain;
using StemStudio.Domain.Rendering;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Assets;

/// <summary>A planned export: the track, the normalised recipe and the render plan.</summary>
/// <param name="Track">The completed track.</param>
/// <param name="Recipe">The recipe to store.</param>
/// <param name="Plan">What the worker renders.</param>
public sealed record ExportDraft(Track Track, ExportRecipe Recipe, RenderPlan Plan);

/// <summary>
/// Validates an export request against its track and mix and plans it (data-model 4.2, 4.3): 404 for an unknown track or
/// mix, 409 <c>track-not-ready</c>, 422 for values outside their limits and for <c>nothing-to-export</c>.
/// </summary>
/// <param name="tracks">Track repository.</param>
/// <param name="mixes">Mix repository.</param>
public sealed class ExportPlanner(ITrackRepository tracks, IMixRepository mixes)
{
    /// <summary>Plans a request.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The draft, or the problem.</returns>
    public async Task<(ExportDraft? Draft, IResult? Problem)> PlanAsync(CreateAudioExportRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (ExportRequestRules.Validate(request) is { Count: > 0 } invalid)
        {
            return (null, AssetProblems.Invalid(invalid));
        }

        var track = await tracks.FindAsync(request.TrackId, cancellationToken);
        return TrackProblem(track, request.TrackId) is { } problem ? (null, problem) : await PlanForAsync(request, track!, cancellationToken);
    }

    private static IResult? TrackProblem(Track? track, Guid trackId) => track switch
    {
        null => Tracks.TrackService.NotFound(trackId),
        { Status: not TrackStatus.Completed } => AssetProblems.TrackNotReady(),
        _ => null,
    };

    private async Task<(ExportDraft?, IResult?)> PlanForAsync(CreateAudioExportRequest request, Track track, CancellationToken cancellationToken)
    {
        if (RangeError(request.Range, track.DurationSeconds ?? 0) is { } rangeError)
        {
            return (null, AssetProblems.Invalid([rangeError]));
        }

        var (mix, problem) = await MixAsync(request, track, cancellationToken);
        return problem is null ? Draft(track, Recipe(request, mix)) : (null, problem);
    }

    /// <summary>FR-006: 0 &lt;= start &lt; end &lt;= the track duration and at least 0.5 s long.</summary>
    private static DomainError? RangeError(RangeDto? range, double duration) =>
        range is null || (range.StartSeconds >= 0 && range.EndSeconds <= duration && range.EndSeconds - range.StartSeconds >= ContractLimits.MinExportRangeSeconds)
            ? null
            : new DomainError("range", FormattableString.Invariant($"The range must lie within the track (0 to {duration:0.##} s) and be at least {ContractLimits.MinExportRangeSeconds} s long."));

    /// <summary>Plans a stored recipe again (retry); a channel that no longer exists makes the source unavailable.</summary>
    /// <param name="track">The track, or null when it was deleted.</param>
    /// <param name="recipe">The stored recipe.</param>
    /// <returns>The draft, or the problem.</returns>
    public static (ExportDraft? Draft, IResult? Problem) Replan(Track? track, ExportRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        if (track is not { Status: TrackStatus.Completed })
        {
            return (null, AssetProblems.SourceUnavailable("The track of this item was deleted, so it cannot be rendered again."));
        }

        var (draft, problem) = Draft(track, recipe);
        return problem is null ? (draft, null) : (null, AssetProblems.SourceUnavailable("A channel of this item no longer exists on the track."));
    }

    private static (ExportDraft?, IResult?) Draft(Track track, ExportRecipe recipe)
    {
        try
        {
            return (new ExportDraft(track, recipe, RenderPlanner.Plan(RenderSource.Of(track), recipe.Mode, recipe.Channels, recipe.Mix)), null);
        }
        catch (Exception exception) when (PlanProblem(exception) is { } problem)
        {
            return (null, problem);
        }
    }

    private static IResult? PlanProblem(Exception exception) => exception switch
    {
        DomainValidationException invalid => AssetProblems.Invalid(invalid.Errors),
        NothingToExportException => AssetProblems.NothingToExport(),
        _ => null,
    };

    private static ExportRecipe Recipe(CreateAudioExportRequest request, ExportMix? mix)
    {
        var raw = request.Mode == ExportModeDto.Raw;
        return new ExportRecipe
        {
            TrackId = request.TrackId,
            Mode = raw ? ExportMode.Raw : ExportMode.Mix,
            Channels = request.Channels?.ToList(),
            Mix = raw ? null : mix,
            Range = request.Range is { } range ? new ExportRange(range.StartSeconds, range.EndSeconds) : null,
            Format = ExportRequestRules.Resolve(request.Format, SingleRaw(raw, request.Channels)),
        };
    }

    private static bool SingleRaw(bool raw, IReadOnlyList<string>? channels) => raw && channels is { Count: 1 };


    private async Task<(ExportMix? Mix, IResult? Problem)> MixAsync(CreateAudioExportRequest request, Track track, CancellationToken cancellationToken)
    {
        if (request.Mode == ExportModeDto.Raw)
        {
            return (null, null);
        }

        var mix = request.Mix is { } sent ? AssetMapper.ToDomain(sent) : await SavedMixAsync(request.MixId!.Value, track.Id, cancellationToken);
        if (mix is null)
        {
            return (null, ApiProblems.NotFound($"Mix {request.MixId} of track {track.Id} does not exist."));
        }

        var errors = RenderPlanner.ValidateMix(mix, track);
        return errors.Count == 0 ? (mix, null) : (null, AssetProblems.Invalid(errors, "mix"));
    }

    private async Task<ExportMix?> SavedMixAsync(Guid mixId, Guid trackId, CancellationToken cancellationToken)
    {
        var mix = await mixes.FindAsync(mixId, cancellationToken);
        return mix is null || mix.TrackId != trackId
            ? null
            : new ExportMix(mix.MasterVolume, mix.Channels) { Expanded = mix.Expanded, DetailChannels = mix.DetailChannels };
    }
}
