namespace StemStudio.Web.Preferences;

/// <summary>Progress of one tour; a tour is offered again when its version is newer than <see cref="Version"/>.</summary>
/// <param name="Version">Tour content version the record applies to (&gt;= 1).</param>
/// <param name="Status">How the tour ended.</param>
/// <param name="At">When it ended (UTC).</param>
public sealed record TourRecord(int Version, TourStatus Status, DateTimeOffset At);
