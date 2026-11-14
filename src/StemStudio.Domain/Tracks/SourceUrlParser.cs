namespace StemStudio.Domain.Tracks;

/// <summary>Validates YouTube and Spotify URLs against the allow-list and derives their keys.</summary>
internal static class SourceUrlParser
{
    /// <summary>Maximum accepted URL length (contract <c>ImportUrlRequest.url</c>).</summary>
    public const int MaxUrlLength = 2048;

    private static readonly Dictionary<string, string> RejectedSpotifyKinds = new(StringComparer.Ordinal)
    {
        ["album"] = SourceUrlMessages.SpotifyAlbum,
        ["playlist"] = SourceUrlMessages.SpotifyPlaylist,
        ["artist"] = SourceUrlMessages.SpotifyArtist,
        ["episode"] = SourceUrlMessages.SpotifyPodcast,
        ["show"] = SourceUrlMessages.SpotifyPodcast,
    };

    /// <summary>Parses a user-supplied URL.</summary>
    /// <param name="input">Raw input.</param>
    /// <returns>The parse result.</returns>
    public static SourceKeyParseResult Parse(string? input)
    {
        var url = input?.Trim() ?? string.Empty;
        var error = CheckShape(url);
        if (error is not null)
        {
            return SourceKeyParseResult.Failure(error);
        }

        return ParseUri(url);
    }

    private static string? CheckShape(string url) => url.Length switch
    {
        0 => SourceUrlMessages.Empty,
        > MaxUrlLength => SourceUrlMessages.TooLong,
        _ => null,
    };

    private static SourceKeyParseResult ParseUri(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return SourceKeyParseResult.Failure(SourceUrlMessages.Malformed);
        }

        return uri.Scheme is "http" or "https" ? ParseHost(uri) : SourceKeyParseResult.Failure(SourceUrlMessages.Scheme);
    }

    private static SourceKeyParseResult ParseHost(Uri uri) => uri.Host switch
    {
        "youtube.com" or "www.youtube.com" or "m.youtube.com" or "music.youtube.com" => ParseYoutube(uri),
        "youtu.be" => ParseShortLink(Segments(uri)),
        "open.spotify.com" => ParseSpotify(Segments(uri)),
        _ => SourceKeyParseResult.Failure(SourceUrlMessages.Host),
    };

    private static SourceKeyParseResult ParseYoutube(Uri uri)
    {
        var segments = Segments(uri);
        return segments switch
        {
            ["watch"] => ParseWatchQuery(uri.Query),
            ["shorts", var id] => Youtube(id),
            ["shorts"] => SourceKeyParseResult.Failure(SourceUrlMessages.YoutubeMissingId),
            ["playlist", ..] => SourceKeyParseResult.Failure(SourceUrlMessages.YoutubePlaylist),
            _ => SourceKeyParseResult.Failure(SourceUrlMessages.YoutubeUnsupportedPath),
        };
    }

    private static SourceKeyParseResult ParseWatchQuery(string query)
    {
        var videoId = QueryString.Get(query, "v");
        if (videoId is not null)
        {
            return Youtube(videoId);
        }

        var message = QueryString.Get(query, "list") is null ? SourceUrlMessages.YoutubeMissingId : SourceUrlMessages.YoutubePlaylist;
        return SourceKeyParseResult.Failure(message);
    }

    private static SourceKeyParseResult ParseShortLink(string[] segments) => segments is [var id]
        ? Youtube(id)
        : SourceKeyParseResult.Failure(SourceUrlMessages.YoutubeMissingId);

    private static SourceKeyParseResult ParseSpotify(string[] segments)
    {
        var path = segments is [var first, .. var rest] && first.StartsWith("intl-", StringComparison.Ordinal) ? rest : segments;
        return path switch
        {
            ["track", var id] => Spotify(id),
            [] or ["track", ..] => SourceKeyParseResult.Failure(SourceUrlMessages.SpotifyMissingId),
            [var kind, ..] when RejectedSpotifyKinds.TryGetValue(kind, out var message) => SourceKeyParseResult.Failure(message),
            _ => SourceKeyParseResult.Failure(SourceUrlMessages.SpotifyOther),
        };
    }

    private static SourceKeyParseResult Youtube(string id) => FromId(SourceType.Youtube, id, SourceUrlMessages.YoutubeMissingId);

    private static SourceKeyParseResult Spotify(string id) => FromId(SourceType.Spotify, id, SourceUrlMessages.SpotifyMissingId);

    private static SourceKeyParseResult FromId(SourceType type, string id, string missingIdMessage) => SourceIds.IsValid(type, id)
        ? SourceKeyParseResult.Success(SourceKey.Create(type, id))
        : SourceKeyParseResult.Failure(missingIdMessage);

    private static string[] Segments(Uri uri) => uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
}
