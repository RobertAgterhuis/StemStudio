namespace StemStudio.Domain.Tracks;

/// <summary>Detected tempo and beat positions of a track (FR-007). Values are checked by <see cref="Validate()"/>.</summary>
public sealed record BeatGrid
{
    private static readonly Bounds BpmBounds = new(0, 400);
    private static readonly Bounds BeatsPerBarBounds = new(1, 12);

    /// <summary>Global tempo 0..400 (0 = detection failed).</summary>
    public required double Bpm { get; init; }

    /// <summary>Beat times in seconds, non-negative and strictly ascending.</summary>
    public required IReadOnlyList<double> Beats { get; init; }

    /// <summary>Beat-1 times in seconds; a subset of <see cref="Beats"/>.</summary>
    public required IReadOnlyList<double> Downbeats { get; init; }

    /// <summary>Beats per bar 1..12 (default 4).</summary>
    public int BeatsPerBar { get; init; } = 4;

    /// <summary>Detection confidence 0..1.</summary>
    public required double Confidence { get; init; }

    /// <summary>False when there is no clear pulse; the metronome then starts disabled.</summary>
    public required bool Reliable { get; init; }

    /// <summary>Checks every rule of the beat grid.</summary>
    /// <returns>The violations with paths below <c>beatGrid</c>; empty when valid.</returns>
    public IReadOnlyList<DomainError> Validate()
    {
        var errors = new ValidationErrors("beatGrid");
        Validate(errors);
        return errors.Errors;
    }

    /// <summary>Writes the violations into a collector.</summary>
    /// <param name="errors">Collector positioned at the beat grid.</param>
    internal void Validate(ValidationErrors errors)
    {
        errors.Range("bpm", Bpm, BpmBounds);
        errors.Range("beatsPerBar", BeatsPerBar, BeatsPerBarBounds);
        errors.Range("confidence", Confidence, Bounds.Unit);
        errors.Check(IsStrictlyAscending(Beats), "beats", "Beat times must be non-negative and strictly ascending.");
        var beats = Beats.ToHashSet();
        errors.Check(Downbeats.All(beats.Contains), "downbeats", "Downbeats must be a subset of the beats.");
    }

    private static bool IsStrictlyAscending(IReadOnlyList<double> beats)
    {
        for (var i = 0; i < beats.Count; i++)
        {
            if (beats[i] < 0 || (i > 0 && beats[i] <= beats[i - 1]))
            {
                return false;
            }
        }

        return true;
    }
}
