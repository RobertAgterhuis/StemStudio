namespace StemStudio.Contracts.Artists;

/// <summary>A source the band information came from, with its licence (FR-014).</summary>
/// <param name="Name">One of <see cref="MusicBrainz"/>, <see cref="Wikidata"/>, <see cref="Wikipedia"/>, <see cref="CoverArtArchive"/>.</param>
/// <param name="Url">The record or article (https).</param>
/// <param name="Licence"><c>CC0</c>, <c>CC BY-SA 4.0</c>, or null when none is stated (Cover Art Archive).</param>
public sealed record DataSourceDto(string Name, string Url, string? Licence)
{
    /// <summary>MusicBrainz (core data CC0).</summary>
    public const string MusicBrainz = "MusicBrainz";

    /// <summary>Wikidata (CC0).</summary>
    public const string Wikidata = "Wikidata";

    /// <summary>Wikipedia (CC BY-SA 4.0).</summary>
    public const string Wikipedia = "Wikipedia";

    /// <summary>Cover Art Archive (no licence stated; pictures are shown in the app only).</summary>
    public const string CoverArtArchive = "Cover Art Archive";
}
