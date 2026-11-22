using StemStudio.Api.InstrumentViews;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Scores;
using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Scores;

namespace StemStudio.Api.Scores;

/// <summary>
/// <c>GET /api/scores/options</c> (data-model 4.5): suggested and allowed profiles, the drums split rule, the grid status,
/// the default quality and an estimate per profile (two for piano) for the worker's device.
/// </summary>
/// <param name="sources">Resolves the source.</param>
/// <param name="estimator">Reads the worker's device and computes the estimates.</param>
/// <param name="chords">Ready chord charts (feature 016 <c>chords</c> option).</param>
public sealed class ScoreOptionsService(ScoreSourceResolver sources, ScoreEstimator estimator, ScoreChordSource chords)
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

        var options = Options(source!, await estimator.EnvironmentAsync(cancellationToken));
        var chordsReady = await chords.ChartAsync(source!.Track?.Id, cancellationToken) is not null;
        return TypedResults.Ok(options with
        {
            Chords = new ScoreChordOptionsDto { Available = chordsReady && ProfileCatalog.Allowed(source.Channel, source.AssetSuggestion).Any(ScoreNotation.ChordsAllowed) },
        });
    }

    private ScoreOptionsDto Options(ResolvedSource source, ScoreEnvironment environment) => WithProfiles(source, environment, new ScoreOptionsDto
    {
        Source = ScoreMapper.ToDto(source.Source),
        DurationSeconds = source.DurationSeconds,
        DrumsRequireSplit = source.Channel == "drums" && source.DrumPieces.Count == 0,
        GridStatus = ScoreMapper.Map<GridStatusDto>(TempoGridResolver.StatusOf(source.Track?.BeatGrid)),
        DefaultQuality = environment.Gpu ? ScoreQualityDto.Accurate : ScoreQualityDto.Fast,
        Attribution = ProfileCatalog.PianoAttribution,
    });

    private ScoreOptionsDto WithProfiles(ResolvedSource source, ScoreEnvironment environment, ScoreOptionsDto options)
    {
        var allowed = ProfileCatalog.Allowed(source.Channel, source.AssetSuggestion);
        var suggested = source.Channel is { } channel ? ProfileCatalog.Suggest(channel) : source.AssetSuggestion;
        return options with
        {
            SuggestedProfile = Dto(suggested),
            Tablature = Tablature(suggested, allowed),
            Parts = Parts(suggested),
            AllowedProfiles = allowed.Select(profile => ScoreMapper.Map<ScoreProfileDto>(profile)).ToList(),
            Estimates = allowed.SelectMany(profile => Estimates(profile, source.DurationSeconds, environment)).ToList(),
            Limits = allowed.Select(profile => new ProfileLimitDto(ScoreMapper.Map<ScoreProfileDto>(profile), ProfileCatalog.Limit(profile))).ToList(),
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

    /// <summary>
    /// Tablature options (review L1): <c>available</c>, the default tuning and the capo follow the suggested profile; the tunings
    /// list holds those of every allowed guitar or bass profile, the suggested instrument's first, so the UI finds a tuning after
    /// switching the profile (the instrument is the id prefix, <c>guitar-</c> or <c>bass-</c>).
    /// </summary>
    private static ScoreTablatureOptionsDto Tablature(ScoreProfile? suggested, IReadOnlyList<ScoreProfile> allowed)
    {
        var instrument = suggested is { } profile ? ScoreTablatureRules.InstrumentOf(profile) : null;
        var instruments = allowed.Select(ScoreTablatureRules.InstrumentOf).OfType<string>().Distinct(StringComparer.Ordinal).OrderBy(name => name != instrument).ToList();
        var own = InstrumentCatalogue.Tunings.Where(tuning => tuning.Instrument == instrument).Select(tuning => CatalogueMapper.ToDto(tuning.Id)).ToList();
        return new ScoreTablatureOptionsDto
        {
            Available = own.Count > 0,
            DefaultTuning = own.Count > 0 ? own[0] : null,
            Tunings = instruments.SelectMany(name => InstrumentCatalogue.Tunings.Where(tuning => tuning.Instrument == name)).Select(tuning => CatalogueMapper.ToDto(tuning.Id)).ToList(),
            CapoAllowed = instrument == InstrumentCatalogue.GuitarInstrument,
        };
    }

    /// <summary>Part options of the suggested profile: a written instrument for every pitched profile, a clef outside the grand staff.</summary>
    private static ScorePartOptionsDto Parts(ScoreProfile? suggested)
    {
        var available = suggested is { } profile && ScorePartRules.PartAllowed(profile);
        return new ScorePartOptionsDto
        {
            Available = available,
            DefaultInstrument = Contracts.InstrumentViews.WrittenInstrumentDto.Concert,
            ClefAllowed = available && !ScorePartRules.IsGrandStaff(suggested!.Value),
        };
    }

    private static ScoreProfileDto? Dto(ScoreProfile? profile) => profile is { } value ? ScoreMapper.Map<ScoreProfileDto>(value) : null;
}
