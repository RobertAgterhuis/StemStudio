namespace StemStudio.Domain.Tracks;

/// <summary>One stored stem of a completed track. The file path is derived from the track ID and the name.</summary>
/// <param name="Name">Stem name, one of <see cref="StemCatalog.SixStem"/>.</param>
/// <param name="SizeBytes">File size in bytes (&gt;= 0).</param>
public sealed record Stem(string Name, long SizeBytes);
