using StemStudio.Api.Styles;
using StemStudio.Api.Tracks;
using StemStudio.Contracts;
using StemStudio.Contracts.Generation;
using StemStudio.Domain;
using StemStudio.Domain.Generations;
using StemStudio.Domain.Styles;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Generations;

/// <summary>Creates restyles of completed tracks (009 FR-001, FR-002, FR-006, ADR-018 D7).</summary>
/// <param name="tracks">Track repository.</param>
/// <param name="styles">Style repository.</param>
/// <param name="availability">Generator availability.</param>
/// <param name="submitter">Submits the job.</param>
/// <param name="seeds">Seed source.</param>
/// <param name="clock">Clock.</param>
public sealed class RestyleService(
    ITrackRepository tracks, IStyleRepository styles, GenerationAvailability availability, GenerationSubmitter submitter, ISeedSource seeds, TimeProvider clock)
{
    /// <summary>Validates, snapshots the style, builds the parameters and submits the restyle.</summary>
    /// <param name="trackId">The source track.</param>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>202, 400, 404, 409 or 503.</returns>
    public async Task<IResult> CreateAsync(Guid trackId, RestyleRequest? request, CancellationToken cancellationToken)
    {
        var track = await tracks.FindAsync(trackId, cancellationToken);
        if (track is null || track.Status != TrackStatus.Completed)
        {
            return track is null ? TrackService.NotFound(trackId) : GenerationProblems.TrackNotReady();
        }

        var style = request is null ? null : await styles.FindAsync(request.StyleId, cancellationToken);
        if (style is null)
        {
            return ApiProblems.Validation("styleId", "The style does not exist.");
        }

        return await PlanAndSubmitAsync(track, style, request!, cancellationToken);
    }

    /// <summary>One random seed per take.</summary>
    /// <param name="count">Number of takes.</param>
    /// <returns>The seeds.</returns>
    public IReadOnlyList<int> NewSeeds(int count) => Enumerable.Range(0, count).Select(_ => seeds.NewSeed()).ToList();

    private async Task<IResult> PlanAndSubmitAsync(Track track, Style style, RestyleRequest request, CancellationToken cancellationToken)
    {
        RestylePlan plan;
        try
        {
            plan = RestylePlanner.Plan(Options(request), new RestyleSource(track.DurationSeconds ?? 0, track.Stems.Select(stem => stem.Name).ToList()), style.Fields);
        }
        catch (DomainValidationException invalid)
        {
            return ApiProblems.Validation(invalid.Errors, string.Empty);
        }

        var readiness = await availability.GetAsync(cancellationToken);
        return readiness.IsReady ? await SubmitAsync(track, style.Snapshot(), plan, cancellationToken) : GenerationProblems.NotReady(readiness);
    }

    private async Task<IResult> SubmitAsync(Track track, StyleSnapshot style, RestylePlan plan, CancellationToken cancellationToken)
    {
        var prompt = StylePromptBuilder.Build(style, plan.Strength);
        var details = new RestyleDetails
        {
            SourceTrackId = track.Id,
            SourceTrackTitle = track.Title,
            Range = plan.Range,
            IncludedStems = plan.Stems,
            Style = style,
            Strength = plan.Strength,
            Caption = prompt.Caption,
            AudioCoverStrength = prompt.AudioCoverStrength,
        };
        var generation = Generation.CreateRestyle(Guid.NewGuid(), details, NewSeeds(plan.Takes), clock.GetUtcNow().UtcDateTime);
        return await submitter.AddAndSubmitAsync(generation, cancellationToken)
            ?? TypedResults.Accepted(ApiRoutes.Generation(generation.Id), GenerationMapper.ToDto(generation));
    }

    private static RestyleOptions Options(RestyleRequest request) => new(
        StyleMapper.Map<RestyleMode>(request.Mode),
        request.RangeStartSeconds,
        request.RangeEndSeconds,
        request.IncludedStems,
        request.Strength is { } strength ? StyleMapper.Map<Strength>(strength) : null,
        request.Takes);
}
