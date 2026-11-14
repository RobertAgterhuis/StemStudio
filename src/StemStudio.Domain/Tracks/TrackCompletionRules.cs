namespace StemStudio.Domain.Tracks;

/// <summary>Rules for a job result before a track is completed (duration, model -> stem set, beat grid).</summary>
internal static class TrackCompletionRules
{
    /// <summary>Validates the result.</summary>
    /// <param name="completion">The result.</param>
    /// <exception cref="DomainValidationException">A rule is violated.</exception>
    public static void Validate(TrackCompletion completion)
    {
        var errors = new ValidationErrors();
        var duration = completion.DurationSeconds;
        errors.Check(duration > 0 && duration <= Track.MaxDurationSeconds, "durationSeconds", "The duration must be more than 0 and at most 900 seconds.");
        var expected = StemCatalog.ForModel(completion.Model);
        errors.Check(expected is not null, "model", $"Unknown separation model '{completion.Model}'.");
        errors.Check(expected is null || MatchesStemSet(completion.Stems, expected), "stems", $"The stems do not match the stem set of model '{completion.Model}'.");
        errors.Check(completion.Stems.All(stem => stem.SizeBytes >= 0), "stems", "Stem sizes must not be negative.");
        completion.BeatGrid.Validate(errors.Nested("beatGrid"));
        errors.ThrowIfAny();
    }

    private static bool MatchesStemSet(IReadOnlyList<Stem> stems, IReadOnlyList<string> expected)
    {
        var names = stems.Select(stem => stem.Name).ToHashSet(StringComparer.Ordinal);
        return stems.Count == expected.Count && names.SetEquals(expected);
    }
}
