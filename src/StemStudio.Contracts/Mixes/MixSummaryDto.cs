namespace StemStudio.Contracts.Mixes;

/// <summary>Entry of the mix library (<c>GET /api/mixes</c>), newest first.</summary>
/// <param name="Id">Mix identifier.</param>
/// <param name="TrackId">Track identifier.</param>
/// <param name="TrackTitle">Title of the track, for display.</param>
/// <param name="Name">Mix name.</param>
/// <param name="UpdatedAt">Last modification time (UTC).</param>
public sealed record MixSummaryDto(Guid Id, Guid TrackId, string TrackTitle, string Name, DateTimeOffset UpdatedAt);
