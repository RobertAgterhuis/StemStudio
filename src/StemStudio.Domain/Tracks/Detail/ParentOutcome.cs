namespace StemStudio.Domain.Tracks.Detail;

/// <summary>One stored detail stem; the file path is derived from the track ID, the parent and the name.</summary>
/// <param name="Name">Detail stem name, allowed for the parent (see <see cref="DetailCatalog.AllowedFor"/>).</param>
/// <param name="SizeBytes">File size in bytes (&gt;= 0).</param>
public sealed record DetailStem(string Name, long SizeBytes);

/// <summary>Outcome of the detail phase for one standard stem.</summary>
/// <param name="Parent">Standard stem name of the track.</param>
/// <param name="Outcome">What happened to the stem.</param>
/// <param name="Stems">Detail stems in display order with <c>rest</c> last; empty unless the outcome is split.</param>
public sealed record ParentOutcome(string Parent, ParentOutcomeKind Outcome, IReadOnlyList<DetailStem> Stems);

/// <summary>A worker status report of a detail job in a non-terminal state.</summary>
/// <param name="Status">Queued or running.</param>
/// <param name="Stage">Human-readable stage, e.g. "Splitting drums (1 of 3)".</param>
/// <param name="CurrentParent">Parent stem being split, or null.</param>
/// <param name="Progress">Progress 0..100 (values outside are clamped).</param>
public sealed record DetailProgress(DetailPhaseStatus Status, string? Stage, string? CurrentParent, int Progress);
