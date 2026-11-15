namespace StemStudio.Domain.Artists;

/// <summary>
/// One MusicBrainz artist shared by every track that matched it (feature 005, data-model 3.3). Deleted when no track
/// references it; refreshed after <see cref="RefreshAfter"/>.
/// </summary>
public sealed class Artist
{
    /// <summary>Age after which the stored facts are replaced by a newer lookup.</summary>
    public static readonly TimeSpan RefreshAfter = TimeSpan.FromDays(30);

    private List<string> _genres = [];
    private List<ArtistMember> _members = [];

    private Artist()
    {
        Name = string.Empty;
    }

    /// <summary>MusicBrainz artist id (primary key).</summary>
    public Guid Mbid { get; private set; }

    /// <summary>Artist name.</summary>
    public string Name { get; private set; }

    /// <summary>Artist type, when known.</summary>
    public ArtistType? Type { get; private set; }

    /// <summary>ISO 3166-1 alpha-2 country code.</summary>
    public string? Country { get; private set; }

    /// <summary>Year the artist started.</summary>
    public int? BeginYear { get; private set; }

    /// <summary>Year the artist ended; null while active.</summary>
    public int? EndYear { get; private set; }

    /// <summary>Genres (Wikidata).</summary>
    public IReadOnlyList<string> Genres => _genres;

    /// <summary>Members of a group (Wikidata).</summary>
    public IReadOnlyList<ArtistMember> Members => _members;

    /// <summary>Official website.</summary>
    public string? Website { get; private set; }

    /// <summary>Wikidata item id.</summary>
    public string? WikidataId { get; private set; }

    /// <summary>Wikipedia excerpt with attribution data.</summary>
    public WikipediaSummary? Summary { get; private set; }

    /// <summary>When the facts were fetched (UTC).</summary>
    public DateTime FetchedAt { get; private set; }

    /// <summary>Creates an artist from validated facts.</summary>
    /// <param name="facts">The facts.</param>
    /// <returns>The artist.</returns>
    /// <exception cref="DomainValidationException">A fact breaks a value limit.</exception>
    public static Artist Create(ArtistFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var artist = new Artist { Mbid = facts.Mbid };
        artist.Refresh(facts);
        return artist;
    }

    /// <summary>True when the facts are at least <see cref="RefreshAfter"/> old.</summary>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <returns>Whether a newer lookup replaces them.</returns>
    public bool IsStale(DateTime nowUtc) => nowUtc - FetchedAt >= RefreshAfter;

    /// <summary>Replaces every fact; nothing changes when the facts are invalid.</summary>
    /// <param name="facts">Facts of the same artist.</param>
    /// <exception cref="ArgumentException">The facts belong to another artist.</exception>
    /// <exception cref="DomainValidationException">A fact breaks a value limit.</exception>
    public void Refresh(ArtistFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.Mbid != Mbid)
        {
            throw new ArgumentException("The facts belong to another artist.", nameof(facts));
        }

        ArtistRules.Validate(facts);
        (Name, Type, Country, BeginYear, EndYear) = (facts.Name.Trim(), facts.Type, facts.Country, facts.BeginYear, facts.EndYear);
        (_genres, _members) = (facts.Genres.ToList(), facts.Members.ToList());
        (Website, WikidataId, Summary, FetchedAt) = (facts.Website, facts.WikidataId, facts.Summary, facts.FetchedAt);
    }
}
