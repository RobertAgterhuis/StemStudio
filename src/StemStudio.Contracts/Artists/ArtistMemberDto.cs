namespace StemStudio.Contracts.Artists;

/// <summary>A member of a group (Wikidata).</summary>
/// <param name="Name">Member name.</param>
/// <param name="FromYear">Year the membership started, when known.</param>
/// <param name="ToYear">Year it ended; null while a member or unknown.</param>
public sealed record ArtistMemberDto(string Name, int? FromYear = null, int? ToYear = null);
