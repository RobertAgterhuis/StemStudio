using System.Globalization;
using StemStudio.Contracts.Artists;

namespace StemStudio.Web.Services;

/// <summary>Readable band info facts (feature 005, FR-008): type, active years, country, members, sources.</summary>
public static class BandFacts
{
    /// <summary>"&lt;album&gt; (&lt;year&gt;)", or the album alone without a year.</summary>
    /// <param name="album">Album title.</param>
    /// <param name="year">First release year, if known.</param>
    /// <returns>The text.</returns>
    public static string Album(string album, int? year) => year is { } y ? $"{album} ({y})" : album;

    /// <summary>The artist type in sentence case, e.g. "Group".</summary>
    /// <param name="type">The MusicBrainz type.</param>
    /// <returns>The text.</returns>
    public static string Type(ArtistTypeDto type) => type.ToString();

    /// <summary>"2001 - present", "1981 - 2001", or null when the start is unknown.</summary>
    /// <param name="artist">The artist.</param>
    /// <returns>The text, or null.</returns>
    public static string? Years(ArtistInfoDto artist) =>
        artist.BeginYear is { } begin ? Span(begin, artist.EndYear, "present") : null;

    /// <summary>"Ada Sample (since 2001)", "Bo Waveform (2001 - 2015)" or the name alone.</summary>
    /// <param name="member">The member.</param>
    /// <returns>The text.</returns>
    public static string Member(ArtistMemberDto member) => member switch
    {
        { FromYear: { } from, ToYear: { } to } => $"{member.Name} ({Span(from, to, string.Empty)})",
        { FromYear: { } from } => $"{member.Name} (since {from})",
        _ => member.Name,
    };

    /// <summary>The English country name of an ISO 3166-1 alpha-2 code; the code itself when it is unknown.</summary>
    /// <param name="code">The code, e.g. <c>NL</c>.</param>
    /// <returns>For example "Netherlands".</returns>
    public static string Country(string code)
    {
        try
        {
            return new RegionInfo(code).EnglishName;
        }
        catch (ArgumentException)
        {
            return code;
        }
    }

    /// <summary>"MusicBrainz (CC0)", or the name alone when no licence is stated.</summary>
    /// <param name="source">The data source.</param>
    /// <returns>The text.</returns>
    public static string Source(DataSourceDto source) => source.Licence is { } licence ? $"{source.Name} ({licence})" : source.Name;

    /// <summary>True when the members row is shown: a group with at least one known member.</summary>
    /// <param name="artist">The artist.</param>
    /// <returns>Whether to show members.</returns>
    public static bool ShowsMembers(ArtistInfoDto artist) => artist.Type == ArtistTypeDto.Group && artist.Members.Count > 0;

    private static string Span(int from, int? to, string open) => $"{from} - {(to is { } end ? end.ToString(CultureInfo.InvariantCulture) : open)}";
}
