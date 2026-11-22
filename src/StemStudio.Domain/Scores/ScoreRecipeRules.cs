using StemStudio.Domain.Rendering;

namespace StemStudio.Domain.Scores;

/// <summary>A score request as received (null = default; data-model 4.2).</summary>
/// <param name="Source">The source.</param>
public sealed record ScoreRequest(ScoreSource Source)
{
    /// <summary>The profile, or null for the suggestion.</summary>
    public ScoreProfile? Profile { get; init; }

    /// <summary>The piano quality, or null for the device default.</summary>
    public ScoreQuality? Quality { get; init; }

    /// <summary>The resolution, or null for the profile default.</summary>
    public ScoreResolution? Resolution { get; init; }

    /// <summary>A key name, or null.</summary>
    public string? KeyOverride { get; init; }

    /// <summary>A manual tempo, or null.</summary>
    public ManualTempo? Tempo { get; init; }

    /// <summary>A range of the source, or null.</summary>
    public ExportRange? Range { get; init; }

    /// <summary>Tablature settings (feature 016), or null.</summary>
    public TablatureSettings? Tablature { get; init; }

    /// <summary>Part settings (feature 016), or null.</summary>
    public PartSettings? Part { get; init; }

    /// <summary>Place chord symbols above the top staff (feature 016).</summary>
    public bool IncludeChords { get; init; }
}

/// <summary>What the rules need to know about the source and the worker.</summary>
/// <param name="DurationSeconds">Duration of the source.</param>
/// <param name="DrumsSplit">Whether the drums of the track are split into detail stems.</param>
/// <param name="GpuWorker">Whether the worker runs on a GPU.</param>
public sealed record ScoreContext(double DurationSeconds, bool DrumsSplit, bool GpuWorker)
{
    /// <summary>The suggestion for a library item (from its export), else null.</summary>
    public ScoreProfile? SuggestedProfile { get; init; }
}

/// <summary>Validates a score request and resolves its defaults (data-model 4.2; FR-002, FR-006, FR-008, FR-009).</summary>
public static class ScoreRecipeRules
{
    /// <summary>Shortest range in seconds.</summary>
    public const double MinRangeSeconds = 2;

    private static readonly Bounds BpmBounds = new(30, 300);
    private static readonly Bounds BeatsPerBarBounds = new(1, 12);
    private static readonly Bounds FirstDownbeatBounds = new(0, 900);

    /// <summary>The recipe of a request.</summary>
    /// <param name="request">The request.</param>
    /// <param name="context">The source and worker facts.</param>
    /// <returns>The recipe.</returns>
    /// <exception cref="DomainValidationException">A value breaks a rule (422).</exception>
    /// <exception cref="ScoreConflictException">Drums before the drums are split (409).</exception>
    /// <exception cref="ScoreUnprocessableException">Tablature that does not fit the profile (typed 422).</exception>
    public static ScoreRecipe Resolve(ScoreRequest request, ScoreContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        var profile = Validate(request, context);
        if (profile == ScoreProfile.Drums && !context.DrumsSplit)
        {
            throw new ScoreConflictException(ScoreConflict.DrumsNotSplit);
        }

        return Recipe(request, profile, context.GpuWorker);
    }

    /// <summary>The end of the default name, e.g. "bass (sheet music)".</summary>
    /// <param name="label">The channel or profile.</param>
    /// <returns>The text.</returns>
    public static string NameSuffix(string label) => $"{label} (sheet music)";

    /// <summary>The provenance contents, e.g. "bass, sheet music".</summary>
    /// <param name="label">The channel or profile.</param>
    /// <returns>The text.</returns>
    public static string Contents(string label) => $"{label}, sheet music";

    private static ScoreRecipe Recipe(ScoreRequest request, ScoreProfile profile, bool gpuWorker) => new()
    {
        Source = request.Source,
        Profile = profile,
        Quality = profile == ScoreProfile.Piano ? request.Quality ?? (gpuWorker ? ScoreQuality.Accurate : ScoreQuality.Fast) : null,
        Resolution = request.Resolution ?? ProfileCatalog.DefaultResolution(profile),
        KeyOverride = request.KeyOverride,
        Tempo = request.Tempo,
        Range = request.Range,
        IncludeChords = request.IncludeChords,
        Tablature = request.Tablature,
    };

    private static ScoreProfile Validate(ScoreRequest request, ScoreContext context)
    {
        var errors = new ValidationErrors();
        var profile = request.Profile ?? (request.Source.Channel is { } channel ? ProfileCatalog.Suggest(channel) : context.SuggestedProfile);
        var allowed = profile is { } chosen && ProfileCatalog.Allowed(request.Source.Channel).Contains(chosen);
        errors.Check(allowed, "profile", "The profile is not available for this source.");
        if (allowed)
        {
            ScoreTablatureRules.Check(request.Tablature, profile!.Value);
        }

        errors.Check(ScoreTablatureRules.CapoInRange(request.Tablature), "tablature.capo", ScoreTablatureRules.CapoRangeText);
        errors.Check(request.KeyOverride is null || KeyNames.IsKnown(request.KeyOverride), "keyOverride", "Must be one of the 30 key names, e.g. \"E minor\" or \"B- major\".");
        errors.Check(!request.IncludeChords || profile is not { } withChords || ScoreNotation.ChordsAllowed(withChords), "includeChords", ScoreNotation.ChordsNotAllowed);
        CheckTempo(errors.Nested("tempo"), request.Tempo);
        CheckRange(errors, request.Range, context.DurationSeconds);
        errors.ThrowIfAny();
        return profile!.Value;
    }

    private static void CheckTempo(ValidationErrors errors, ManualTempo? tempo)
    {
        if (tempo is not null)
        {
            errors.Range("bpm", tempo.Bpm, BpmBounds);
            errors.Range("beatsPerBar", tempo.BeatsPerBar, BeatsPerBarBounds);
            errors.Range("firstDownbeatSeconds", tempo.FirstDownbeatSeconds, FirstDownbeatBounds);
        }
    }

    private static void CheckRange(ValidationErrors errors, ExportRange? range, double duration) =>
        errors.Check(
            range is null || (range.StartSeconds >= 0 && range.EndSeconds <= duration && range.EndSeconds - range.StartSeconds >= MinRangeSeconds),
            "range",
            $"Must lie inside the source and be at least {MinRangeSeconds} seconds long.");
}
