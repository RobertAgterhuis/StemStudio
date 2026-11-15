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
        var (mix, problem) = await MixAsync(request, track, cancellationToken);
        return problem is null ? Draft(track, Recipe(request, mix)) : (null, problem);
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
