namespace StemStudio.Domain.Artists;

/// <summary>MusicBrainz artist type (feature 005, data-model 2.5).</summary>
public enum ArtistType
{
    /// <summary>A band or other group.</summary>
    Group,

    /// <summary>A single person.</summary>
    Person,

    /// <summary>An orchestra.</summary>
    Orchestra,

    /// <summary>A choir.</summary>
    Choir,

    /// <summary>A fictional character.</summary>
    Character,

    /// <summary>Any other type.</summary>
    Other,
}

/// <summary>A member of a group (Wikidata P527 with P580/P582).</summary>
/// <param name="Name">Member name (1..500 characters).</param>
/// <param name="FromYear">Year the membership started, when known.</param>
/// <param name="ToYear">Year it ended; null while a member or unknown.</param>
public sealed record ArtistMember(string Name, int? FromYear = null, int? ToYear = null);

/// <summary>A Wikipedia excerpt with its attribution data (CC BY-SA 4.0, data-model 2.6); stored and shown unmodified.</summary>
/// <param name="Language">Wiki language code (2-3 lower-case letters).</param>
/// <param name="Title">Article title.</param>
/// <param name="Extract">The excerpt (1..2000 characters).</param>
/// <param name="PageUrl">Article URL (https).</param>
/// <param name="HistoryUrl">History URL (https), linked as "Wikipedia contributors".</param>
/// <param name="RevisionId">Article revision, when known.</param>
public sealed record WikipediaSummary(string Language, string Title, string Extract, string PageUrl, string HistoryUrl, string? RevisionId = null);

/// <summary>Artist facts from one lookup (MusicBrainz, Wikidata, Wikipedia), validated by <see cref="Artist"/>.</summary>
public sealed record ArtistFacts
{
    /// <summary>MusicBrainz artist id (the key).</summary>
    public required Guid Mbid { get; init; }

    /// <summary>Artist name.</summary>
    public required string Name { get; init; }

    /// <summary>Artist type, when known.</summary>
    public ArtistType? Type { get; init; }

    /// <summary>ISO 3166-1 alpha-2 country code.</summary>
    public string? Country { get; init; }

    /// <summary>Year the artist started.</summary>
    public int? BeginYear { get; init; }

    /// <summary>Year the artist ended; null while active.</summary>
    public int? EndYear { get; init; }

    /// <summary>Genres (at most 8, each at most 100 characters).</summary>
    public IReadOnlyList<string> Genres { get; init; } = [];

    /// <summary>Members of a group (at most 12).</summary>
    public IReadOnlyList<ArtistMember> Members { get; init; } = [];

    /// <summary>Official website (https).</summary>
    public string? Website { get; init; }

    /// <summary>Wikidata item id (<c>Q</c> + digits).</summary>
    public string? WikidataId { get; init; }

    /// <summary>Wikipedia excerpt, when available.</summary>
    public WikipediaSummary? Summary { get; init; }

    /// <summary>When the facts were fetched (UTC).</summary>
    public required DateTime FetchedAt { get; init; }
}
