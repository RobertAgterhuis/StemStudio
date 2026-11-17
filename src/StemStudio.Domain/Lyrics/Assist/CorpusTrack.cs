namespace StemStudio.Domain.Lyrics.Assist;

/// <summary>A library track whose 006 lyrics contain a matched line.</summary>
/// <param name="TrackId">Track id.</param>
/// <param name="Title">Track title (for the <c>not-own-lyrics</c> message).</param>
public sealed record CorpusTrack(Guid TrackId, string Title);
