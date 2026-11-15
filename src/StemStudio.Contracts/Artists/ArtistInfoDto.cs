namespace StemStudio.Contracts.Artists;

/// <summary>Artist facts from MusicBrainz, Wikidata and Wikipedia (backend contract 1.4.0).</summary>
public sealed record ArtistInfoDto
{
    /// <summary>MusicBrainz artist id.</summary>
    public required Guid Mbid { get; init; }

    /// <summary>Artist name.</summary>
    public required string Name { get; init; }

    /// <summary>Artist type, when known.</summary>
    public ArtistTypeDto? Type { get; init; }

    /// <summary>ISO 3166-1 alpha-2 country code, when known.</summary>
    public string? Country { get; init; }

    /// <summary>Year the artist started.</summary>
    public int? BeginYear { get; init; }

    /// <summary>Year the artist ended; null while active.</summary>
    public int? EndYear { get; init; }

    /// <summary>Genres (Wikidata, at most 8).</summary>
    public IReadOnlyList<string> Genres { get; init; } = [];

    /// <summary>Members of a group (Wikidata, at most 12).</summary>
    public IReadOnlyList<ArtistMemberDto> Members { get; init; } = [];

    /// <summary>Official website (https).</summary>
    public string? Website { get; init; }

    /// <summary>Wikipedia excerpt with attribution data, when available.</summary>
    public WikipediaSummaryDto? Summary { get; init; }
}
