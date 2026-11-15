using StemStudio.Contracts.Artists;
using StemStudio.Contracts.Tracks;
using StemStudio.Domain.Artists;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Metadata;

/// <summary>Maps a track's lookup state and its artist to <see cref="BandInfoDto"/> (FR-008, FR-014): facts only when found.</summary>
public static class BandInfoMapper
{
    /// <summary>Licence of MusicBrainz core data and Wikidata.</summary>
    public const string Cc0 = "CC0";

    /// <summary>Maps the band information of a track.</summary>
    /// <param name="track">The track.</param>
    /// <param name="artist">Its artist, or null.</param>
    /// <returns>The DTO.</returns>
    public static BandInfoDto ToDto(Track track, Artist? artist)
    {
        ArgumentNullException.ThrowIfNull(track);
        var metadata = track.Metadata;
        var info = new BandInfoDto { Status = Enum.Parse<MetadataStatusDto>(metadata.Status.ToString()), Reason = metadata.Reason };
        return metadata.Status == MetadataStatus.Found ? WithFacts(info, track, artist) : info;
    }

    private static BandInfoDto WithFacts(BandInfoDto info, Track track, Artist? artist) => info with
    {
        Recording = track.Metadata.RecordingTitle is { } title ? new RecordingInfoDto(title, track.Metadata.Album, track.Metadata.Year) : null,
        Artist = artist is null ? null : ToDto(artist),
        Sources = Sources(track, artist),
    };

    /// <summary>Maps an artist.</summary>
    /// <param name="artist">The artist.</param>
    /// <returns>The DTO.</returns>
    public static ArtistInfoDto ToDto(Artist artist)
    {
        ArgumentNullException.ThrowIfNull(artist);
        var info = new ArtistInfoDto
        {
            Mbid = artist.Mbid,
            Name = artist.Name,
            Type = artist.Type is { } type ? Enum.Parse<ArtistTypeDto>(type.ToString()) : null,
            Country = artist.Country,
            BeginYear = artist.BeginYear,
            EndYear = artist.EndYear,
        };
        return WithDetails(info, artist);
    }

    private static ArtistInfoDto WithDetails(ArtistInfoDto info, Artist artist) => info with
    {
        Genres = artist.Genres,
        Members = artist.Members.Select(member => new ArtistMemberDto(member.Name, member.FromYear, member.ToYear)).ToList(),
        Website = artist.Website,
        Summary = ToDto(artist.Summary),
    };

    private static WikipediaSummaryDto? ToDto(WikipediaSummary? summary) => summary is null
        ? null
        : new WikipediaSummaryDto
        {
            Language = summary.Language,
            Title = summary.Title,
            Extract = summary.Extract,
            PageUrl = summary.PageUrl,
            HistoryUrl = summary.HistoryUrl,
        };

    private static List<DataSourceDto> Sources(Track track, Artist? artist)
    {
        var sources = new List<DataSourceDto>();
        Add(sources, track.Metadata.RecordingMbid, id => new DataSourceDto(DataSourceDto.MusicBrainz, $"https://musicbrainz.org/recording/{id}", Cc0));
        Add(sources, artist?.WikidataId, id => new DataSourceDto(DataSourceDto.Wikidata, $"https://www.wikidata.org/wiki/{id}", Cc0));
        Add(sources, artist?.Summary?.PageUrl, url => new DataSourceDto(DataSourceDto.Wikipedia, url, WikipediaSummaryDto.CcBySa40));
        Add(sources, CoverArtArchiveGroup(track), id => new DataSourceDto(DataSourceDto.CoverArtArchive, $"https://coverartarchive.org/release-group/{id}", null));
        return sources;
    }

    private static Guid? CoverArtArchiveGroup(Track track) =>
        track.Artwork?.Source == ArtworkSource.CoverArtArchive ? track.Metadata.ReleaseGroupMbid : null;

    private static void Add<T>(List<DataSourceDto> sources, T? value, Func<T, DataSourceDto> source)
    {
        if (value is not null)
        {
            sources.Add(source(value));
        }
    }
}
