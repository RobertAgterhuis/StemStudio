namespace StemStudio.Domain.Mixes;

/// <summary>A-B loop region in seconds (FR-013): 0 &lt;= Start &lt; End &lt;= track duration.</summary>
/// <param name="Start">Loop start (A).</param>
/// <param name="End">Loop end (B).</param>
/// <param name="Enabled">Whether looping is active.</param>
public sealed record LoopRegion(double Start, double End, bool Enabled)
{
    /// <summary>Writes the violations into a collector.</summary>
    /// <param name="errors">Collector positioned at the loop.</param>
    /// <param name="durationSeconds">Track duration.</param>
    internal void Validate(ValidationErrors errors, double durationSeconds)
    {
        errors.Check(Start >= 0, "start", "The loop start must not be negative.");
        errors.Check(End > Start, "end", "The loop end must be after the loop start.");
        errors.Check(End <= durationSeconds, "end", "The loop end must not be after the end of the track.");
    }
}
