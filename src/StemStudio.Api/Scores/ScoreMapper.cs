using StemStudio.Api.Tracks;
using StemStudio.Api.Worker;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Scores;
using StemStudio.Domain.Rendering;
using StemStudio.Domain.Scores;

namespace StemStudio.Api.Scores;

/// <summary>Maps scores between the contract DTOs and the Domain (feature 008).</summary>
public static class ScoreMapper
{
    /// <summary>The domain request of a DTO for a resolved source.</summary>
    /// <param name="request">The DTO.</param>
    /// <param name="source">The resolved source.</param>
    /// <returns>The request.</returns>
    public static ScoreRequest ToDomain(CreateScoreRequest request, ScoreSource source)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new ScoreRequest(source)
        {
            Profile = MapOrNull<ScoreProfile>(request.Profile),
            Quality = MapOrNull<ScoreQuality>(request.Quality),
            Resolution = MapOrNull<ScoreResolution>(request.Resolution),
            KeyOverride = request.KeyOverride,
            Tempo = request.Tempo is { } tempo ? new ManualTempo(tempo.Bpm, tempo.BeatsPerBar, tempo.FirstDownbeatSeconds) : null,
            Range = request.Range is { } range ? new ExportRange(range.StartSeconds, range.EndSeconds) : null,
        };
    }

    /// <summary>Maps a recipe.</summary>
    /// <param name="recipe">The recipe.</param>
    /// <returns>The DTO.</returns>
    public static ScoreRecipeDto ToDto(ScoreRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        return new ScoreRecipeDto
        {
            Source = ToDto(recipe.Source),
            Profile = Map<ScoreProfileDto>(recipe.Profile),
            Quality = MapOrNull<ScoreQualityDto>(recipe.Quality),
            Resolution = Map<ScoreResolutionDto>(recipe.Resolution),
            KeyOverride = recipe.KeyOverride,
            Tempo = recipe.Tempo is { } tempo ? new ScoreTempoDto(tempo.Bpm, tempo.BeatsPerBar, tempo.FirstDownbeatSeconds) : null,
            Range = recipe.Range is { } range ? new RangeDto(range.StartSeconds, range.EndSeconds) : null,
        };
    }

    /// <summary>Maps score facts.</summary>
    /// <param name="facts">The facts.</param>
    /// <returns>The DTO.</returns>
    public static ScoreFactsDto ToDto(ScoreFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return Counts(facts, new ScoreFactsDto
        {
            Profile = Map<ScoreProfileDto>(facts.Profile),
            Engine = Map<ScoreEngineDto>(facts.Engine),
            Bpm = facts.Bpm,
            TimeSignature = facts.TimeSignature,
            Bars = facts.Bars,
            Notes = facts.Notes,
            Pages = facts.Pages,
            Resolution = Map<ScoreResolutionDto>(facts.Resolution),
        });
    }

    private static ScoreFactsDto Counts(ScoreFacts facts, ScoreFactsDto dto) => dto with
    {
        Quality = MapOrNull<ScoreQualityDto>(facts.Quality),
        Key = facts.Key is { } key ? new KeyInfoDto(key.Tonic, Map<KeyModeDto>(key.Mode), key.Confidence, Map<KeySourceDto>(key.Source)) : null,
        Warnings = facts.Warnings.Select(warning => new ScoreWarningDto(Map<ScoreWarningCodeDto>(warning.Code), warning.Text)).ToList(),
        Attribution = facts.Attribution,
        Device = TrackMapper.ToDto(facts.Device),
    };

    /// <summary>The facts of a worker summary (no device, attribution as sent).</summary>
    /// <param name="summary">The summary.</param>
    /// <returns>The facts (not validated).</returns>
    public static ScoreFacts ToFacts(WorkerScoreSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        return WithKey(summary, new ScoreFacts
        {
            Profile = summary.Profile,
            Engine = summary.Engine,
            Bpm = summary.Bpm,
            TimeSignature = summary.TimeSignature,
            Bars = summary.Bars,
            Notes = summary.Notes,
            Pages = summary.Pages,
            Resolution = summary.Resolution,
        });
    }

    private static ScoreFacts WithKey(WorkerScoreSummary summary, ScoreFacts facts) => facts with
    {
        Quality = summary.Quality,
        Key = summary.Key,
        Warnings = summary.Warnings,
    };

    /// <summary>Maps a source.</summary>
    /// <param name="source">The source.</param>
    /// <returns>The DTO.</returns>
    public static AudioSourceDto ToDto(ScoreSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.AssetId is { } assetId ? AudioSourceDto.FromAsset(assetId) : AudioSourceDto.FromChannel(source.TrackId!.Value, source.Channel!);
    }

    /// <summary>Maps an enum to the enum of the same member name.</summary>
    /// <typeparam name="T">Target enum.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The mapped value.</returns>
    public static T Map<T>(Enum value)
        where T : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(value);
        return Enum.Parse<T>(value.ToString());
    }

    private static T? MapOrNull<T>(Enum? value)
        where T : struct, Enum => value is null ? null : Map<T>(value);
}
