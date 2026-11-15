namespace StemStudio.Contracts.Mixes;

/// <summary>A-B loop region in seconds (spec FR-013). Requires 0 &lt;= Start &lt; End &lt;= track duration.</summary>
/// <param name="Start">Loop start (A) in seconds.</param>
/// <param name="End">Loop end (B) in seconds.</param>
/// <param name="Enabled">Whether looping is active.</param>
public sealed record LoopRegionDto(double Start, double End, bool Enabled);
