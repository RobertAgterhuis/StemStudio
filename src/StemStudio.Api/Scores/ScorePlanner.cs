using StemStudio.Contracts.Scores;
using StemStudio.Domain.Scores;

namespace StemStudio.Api.Scores;

/// <summary>A planned score: the source, the recipe with its defaults resolved, and the grid to send.</summary>
/// <param name="Source">The resolved source.</param>
/// <param name="Recipe">The recipe.</param>
/// <param name="Grid">The time base.</param>
public sealed record ScoreDraft(ResolvedSource Source, ScoreRecipe Recipe, TempoGrid Grid)
{
    /// <summary>The chord symbols of an <c>includeChords</c> score (feature 016), or null.</summary>
    public ScoreChords? Chords { get; init; }
}

/// <summary>Validates a score request and resolves the recipe and the grid (data-model 4.2, 4.3).</summary>
/// <param name="sources">Resolves the source.</param>
/// <param name="estimator">Reads the worker's device.</param>
/// <param name="chords">Attaches the chord symbols of <c>includeChords</c> (feature 016).</param>
public sealed class ScorePlanner(ScoreSourceResolver sources, ScoreEstimator estimator, ScoreChordSource chords)
{
    /// <summary>Plans a new request.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The draft or the problem (404, 409, 422).</returns>
    public async Task<(ScoreDraft? Draft, IResult? Problem)> PlanAsync(CreateScoreRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (source, problem) = await sources.ResolveAsync(request.Source, cancellationToken);
        if (problem is not null)
        {
            return (null, problem);
        }

        var environment = await estimator.EnvironmentAsync(cancellationToken);
        return await WithChordsAsync(Plan(source!, ScoreMapper.ToDomain(request, source!.Source), environment.Gpu), cancellationToken);
    }

    /// <summary>Plans a stored recipe again (retry): its resolved values are kept.</summary>
    /// <param name="recipe">The stored recipe.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The draft or the problem.</returns>
    public async Task<(ScoreDraft? Draft, IResult? Problem)> ReplanAsync(ScoreRecipe recipe, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        var (source, problem) = await sources.ResolveAsync(ScoreMapper.ToDto(recipe.Source), cancellationToken);
        var request = new ScoreRequest(recipe.Source)
        {
            Profile = recipe.Profile,
            Quality = recipe.Quality,
            Resolution = recipe.Resolution,
            KeyOverride = recipe.KeyOverride,
            Tempo = recipe.Tempo,
            Range = recipe.Range,
            Tablature = recipe.Tablature,
            Part = recipe.Part,
            IncludeChords = recipe.IncludeChords,
        };
        return problem is null ? await WithChordsAsync(Plan(source!, request, gpu: false), cancellationToken) : (null, problem); // the stored quality is kept, so the device does not matter
    }

    private async Task<(ScoreDraft?, IResult?)> WithChordsAsync((ScoreDraft? Draft, IResult? Problem) planned, CancellationToken cancellationToken) =>
        planned.Draft is { } draft ? await chords.AttachAsync(draft, cancellationToken) : planned;

    private static (ScoreDraft?, IResult?) Plan(ResolvedSource source, ScoreRequest request, bool gpu)
    {
        try
        {
            var recipe = ScoreRecipeRules.Resolve(request, new ScoreContext(source.DurationSeconds, source.DrumPieces.Count > 0, gpu) { SuggestedProfile = source.AssetSuggestion });
            var grid = TempoGridResolver.Resolve(source.Track?.BeatGrid, recipe.Tempo, TempoGridResolver.Offset(source.AssetRangeStart, recipe.Range?.StartSeconds));
            return grid is null ? (null, ScoreProblems.TempoRequired()) : (new ScoreDraft(source, recipe, grid), null);
        }
        catch (Exception exception) when (ScoreProblems.For(exception) is { } problem)
        {
            return (null, problem);
        }
    }
}
