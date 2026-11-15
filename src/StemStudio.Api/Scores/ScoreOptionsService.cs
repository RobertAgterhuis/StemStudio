using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Scores;
using StemStudio.Domain.Scores;

namespace StemStudio.Api.Scores;

/// <summary>
/// <c>GET /api/scores/options</c> (data-model 4.5): suggested and allowed profiles, the drums split rule, the grid status,
/// the default quality and an estimate per profile (two for piano) for the worker's device.
/// </summary>
/// <param name="sources">Resolves the source.</param>
/// <param name="estimator">Reads the worker's device and computes the estimates.</param>
public sealed class ScoreOptionsService(ScoreSourceResolver sources, ScoreEstimator estimator)
{
    /// <summary>The options of a source.</summary>
    /// <param name="trackId">Track (with <paramref name="channel"/>).</param>
    /// <param name="channel">Channel id.</param>
    /// <param name="assetId">Library item (alone).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200, 404, 409 or 422.</returns>
    public async Task<IResult> GetAsync(Guid? trackId, string? channel, Guid? assetId, CancellationToken cancellationToken)
    {
        var (source, problem) = await sources.ResolveAsync(new AudioSourceDto { TrackId = trackId, Channel = channel, AssetId = assetId }, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        return TypedResults.Ok(Options(source!, await estimator.EnvironmentAsync(cancellationToken)));
    }

    private ScoreOptionsDto Options(ResolvedSource source, ScoreEnvironment environment)
    {
        var allowed = ProfileCatalog.Allowed(source.Channel);
        return new ScoreOptionsDto
        {
            Source = ScoreMapper.ToDto(source.Source),
            DurationSeconds = source.DurationSeconds,
            SuggestedProfile = source.Channel is { } channel ? Dto(ProfileCatalog.Suggest(channel)) : Dto(source.AssetSuggestion),
            AllowedProfiles = allowed.Select(profile => ScoreMapper.Map<ScoreProfileDto>(profile)).ToList(),
            DrumsRequireSplit = source.Channel == "drums" && source.DrumPieces.Count == 0,
            GridStatus = ScoreMapper.Map<GridStatusDto>(TempoGridResolver.StatusOf(source.Track?.BeatGrid)),
            DefaultQuality = environment.Gpu ? ScoreQualityDto.Accurate : ScoreQualityDto.Fast,
            Estimates = allowed.SelectMany(profile => Estimates(profile, source.DurationSeconds, environment)).ToList(),
            Limits = allowed.Select(profile => new ProfileLimitDto(ScoreMapper.Map<ScoreProfileDto>(profile), ProfileCatalog.Limit(profile))).ToList(),
            Attribution = ProfileCatalog.PianoAttribution,
        };
    }

    private IEnumerable<ScoreEstimateDto> Estimates(ScoreProfile profile, double duration, ScoreEnvironment environment)
    {
        var qualities = profile == ScoreProfile.Piano ? new ScoreQuality?[] { ScoreQuality.Accurate, ScoreQuality.Fast } : [null];
        return qualities.Select(quality => new ScoreEstimateDto(
            ScoreMapper.Map<ScoreProfileDto>(profile),
            quality is { } value ? ScoreMapper.Map<ScoreQualityDto>(value) : null,
            estimator.Seconds(duration, ProfileCatalog.Engine(profile, quality), environment)));
    }

    private static ScoreProfileDto? Dto(ScoreProfile? profile) => profile is { } value ? ScoreMapper.Map<ScoreProfileDto>(value) : null;
}
