namespace StemStudio.Domain.Tracks;

/// <summary>Actionable rejection messages for import URLs (SC-007).</summary>
internal static class SourceUrlMessages
{
    public const string Empty = "Enter a YouTube video link or a Spotify track link.";
    public const string TooLong = "The link is longer than 2048 characters.";
    public const string Malformed = "This is not a valid link. Paste the full address, for example https://youtu.be/<video id>.";
    public const string Scheme = "Only http and https links are supported.";
    public const string Host = "Only YouTube video links and Spotify track links are supported. Playlists and albums are not.";
    public const string YoutubePlaylist = "Playlists are not supported. Open a single video and paste its link.";
    public const string YoutubeMissingId = "The link does not contain a valid YouTube video ID.";
    public const string YoutubeUnsupportedPath = "Only links to a single YouTube video are supported (watch?v=..., youtu.be/... or shorts/...).";
    public const string SpotifyMissingId = "The link does not contain a valid Spotify track ID.";
    public const string SpotifyAlbum = "Spotify albums are not supported. Paste a link to a single track.";
    public const string SpotifyPlaylist = "Spotify playlists are not supported. Paste a link to a single track.";
    public const string SpotifyArtist = "Spotify artist pages are not supported. Paste a link to a single track.";
    public const string SpotifyPodcast = "Spotify podcasts (episodes and shows) are not supported. Paste a link to a music track.";
    public const string SpotifyOther = "Only Spotify track links (open.spotify.com/track/...) are supported.";
}
