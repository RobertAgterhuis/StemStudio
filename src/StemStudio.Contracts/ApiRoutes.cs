using System.Globalization;

namespace StemStudio.Contracts;

/// <summary>Relative routes of the backend HTTP API (see backend-api.openapi.yaml).</summary>
public static partial class ApiRoutes
{
    /// <summary>Liveness endpoint.</summary>
    public const string Health = "/health";

    /// <summary>The worker's active processing device (contract 1.3.0).</summary>
    public const string Device = "/api/device";

    /// <summary>Import from a YouTube or Spotify URL (JSON body).</summary>
    public const string Imports = "/api/imports";

    /// <summary>Import from an uploaded audio file (multipart/form-data, field <c>file</c>).</summary>
    public const string ImportUpload = "/api/imports/upload";

    /// <summary>Track collection.</summary>
    public const string Tracks = "/api/tracks";

    /// <summary>Mix collection; supports the <c>trackId</c> query filter.</summary>
    public const string Mixes = "/api/mixes";

    /// <summary>Route of a single track.</summary>
    /// <param name="trackId">Track identifier.</param>
    /// <returns>The relative URL.</returns>
    public static string Track(Guid trackId) => $"{Tracks}/{trackId}";

    /// <summary>Route that retries a failed track.</summary>
    /// <param name="trackId">Track identifier.</param>
    /// <returns>The relative URL.</returns>
    public static string RetryTrack(Guid trackId) => $"{Track(trackId)}/retry";

    /// <summary>Route of one stem audio file (FLAC, supports HTTP Range).</summary>
    /// <param name="trackId">Track identifier.</param>
    /// <param name="stemName">Stem name, see <see cref="StemNames"/>.</param>
    /// <returns>The relative URL.</returns>
    public static string Stem(Guid trackId, string stemName) => $"{Track(trackId)}/stems/{stemName}";

    /// <summary>Start, "Split further" or retry the detail phase: <c>POST /api/tracks/{id}/detail</c>.</summary>
    /// <param name="trackId">Track id.</param>
    /// <returns>The route.</returns>
    public static string StartDetail(Guid trackId) => $"{Track(trackId)}/detail";

    /// <summary>Cancel the detail phase: <c>POST /api/tracks/{id}/detail/cancel</c>.</summary>
    /// <param name="trackId">Track id.</param>
    /// <returns>The route.</returns>
    public static string CancelDetail(Guid trackId) => $"{StartDetail(trackId)}/cancel";

    /// <summary>One detail stem file: <c>GET /api/tracks/{id}/detail-stems/{parent}/{name}</c>.</summary>
    /// <param name="trackId">Track id.</param>
    /// <param name="parent">Parent stem name.</param>
    /// <param name="name">Detail stem name.</param>
    /// <returns>The route.</returns>
    public static string DetailStem(Guid trackId, string parent, string name) => $"{Track(trackId)}/detail-stems/{parent}/{name}";

    /// <summary>Route of a single mix.</summary>
    /// <param name="mixId">Mix identifier.</param>
    /// <returns>The relative URL.</returns>
    public static string Mix(Guid mixId) => $"{Mixes}/{mixId}";

    /// <summary>Route that duplicates a mix.</summary>
    /// <param name="mixId">Mix identifier.</param>
    /// <returns>The relative URL.</returns>
    public static string DuplicateMix(Guid mixId) => $"{Mix(mixId)}/duplicate";

    /// <summary>The track's own picture: <c>PUT</c> (multipart upload) and <c>DELETE</c> (back to automatic), contract 1.4.0.</summary>
    /// <param name="trackId">Track id.</param>
    /// <returns>The route.</returns>
    public static string TrackArtworkUpload(Guid trackId) => $"{Track(trackId)}/artwork";

    /// <summary>The picture shown for a track as JPEG: <c>GET /api/tracks/{id}/artwork/{variant}[?v=version]</c> (1.4.0).</summary>
    /// <param name="trackId">Track id.</param>
    /// <param name="variant"><see cref="ArtworkCover"/> or <see cref="ArtworkThumb"/>.</param>
    /// <param name="version">Artwork version (12 hex characters) for an immutable cache URL, or null.</param>
    /// <returns>The route.</returns>
    public static string TrackArtwork(Guid trackId, string variant, string? version = null) =>
        version is null ? $"{TrackArtworkUpload(trackId)}/{variant}" : $"{TrackArtworkUpload(trackId)}/{variant}?v={version}";

    /// <summary>Artwork variant: 600 px cover.</summary>
    public const string ArtworkCover = "cover";

    /// <summary>Artwork variant: 160 px thumbnail.</summary>
    public const string ArtworkThumb = "thumb";

    /// <summary>Band information of a track: <c>GET /api/tracks/{id}/band-info</c> (1.4.0).</summary>
    /// <param name="trackId">Track id.</param>
    /// <returns>The route.</returns>
    public static string BandInfo(Guid trackId) => $"{Track(trackId)}/band-info";

    /// <summary>Retry or correct the lookup: <c>POST /api/tracks/{id}/metadata/lookup</c> (1.4.0).</summary>
    /// <param name="trackId">Track id.</param>
    /// <returns>The route.</returns>
    public static string MetadataLookup(Guid trackId) => $"{Track(trackId)}/metadata/lookup";

    /// <summary>Library collection: <c>GET /api/assets</c> (1.5.0).</summary>
    public const string Assets = "/api/assets";

    /// <summary>Create an audio export: <c>POST /api/assets/audio-exports</c> (1.5.0).</summary>
    public const string AudioExports = "/api/assets/audio-exports";

    /// <summary>Create a stem pack: <c>POST /api/assets/stem-packs</c> (1.5.0).</summary>
    public const string StemPacks = "/api/assets/stem-packs";

    /// <summary>One library item: <c>GET</c>, <c>PATCH</c> (rename), <c>DELETE</c>.</summary>
    /// <param name="assetId">Item id.</param>
    /// <returns>The route.</returns>
    public static string Asset(Guid assetId) => $"{Assets}/{assetId}";

    /// <summary>Retry a failed item: <c>POST /api/assets/{id}/retry</c>.</summary>
    /// <param name="assetId">Item id.</param>
    /// <returns>The route.</returns>
    public static string RetryAsset(Guid assetId) => $"{Asset(assetId)}/retry";

    /// <summary>A file of a ready item: <c>GET /api/assets/{id}/files/{role}[?inline=true]</c>.</summary>
    /// <param name="assetId">Item id.</param>
    /// <param name="role">File role, e.g. <see cref="AssetFileAudio"/>.</param>
    /// <param name="inline">True for playback (<c>Content-Disposition: inline</c>).</param>
    /// <returns>The route.</returns>
    public static string AssetFile(Guid assetId, string role, bool inline = false) =>
        inline ? $"{Asset(assetId)}/files/{role}?inline=true" : $"{Asset(assetId)}/files/{role}";

    /// <summary>File role of the audio file of an audio item.</summary>
    public const string AssetFileAudio = "audio";

    /// <summary>File role of the PDF of a score item (1.7.0).</summary>
    public const string AssetFilePdf = "pdf";

    /// <summary>File role of the MusicXML file of a score item (1.7.0).</summary>
    public const string AssetFileMusicXml = "musicxml";

    /// <summary>File role of the MIDI file of a score item (1.7.0).</summary>
    public const string AssetFileMidi = "midi";

    /// <summary>The lyrics of a track: <c>GET</c>, <c>PUT</c> (enter text), <c>DELETE</c> (1.6.0).</summary>
    /// <param name="trackId">Track id.</param>
    /// <returns>The route.</returns>
    public static string Lyrics(Guid trackId) => $"{Track(trackId)}/lyrics";

    /// <summary>Search the lyrics again: <c>POST /api/tracks/{id}/lyrics/search</c> (1.6.0).</summary>
    /// <param name="trackId">Track id.</param>
    /// <returns>The route.</returns>
    public static string LyricsSearch(Guid trackId) => $"{Lyrics(trackId)}/search";

    /// <summary>Align the lyrics text: <c>POST /api/tracks/{id}/lyrics/align</c> (1.6.0).</summary>
    /// <param name="trackId">Track id.</param>
    /// <returns>The route.</returns>
    public static string LyricsAlign(Guid trackId) => $"{Lyrics(trackId)}/align";

    /// <summary>Cancel the alignment: <c>POST /api/tracks/{id}/lyrics/align/cancel</c> (1.6.0).</summary>
    /// <param name="trackId">Track id.</param>
    /// <returns>The route.</returns>
    public static string LyricsAlignCancel(Guid trackId) => $"{LyricsAlign(trackId)}/cancel";

    /// <summary>Store the lyrics offset: <c>PUT /api/tracks/{id}/lyrics/offset</c> (1.6.0).</summary>
    /// <param name="trackId">Track id.</param>
    /// <returns>The route.</returns>
    public static string LyricsOffset(Guid trackId) => $"{Lyrics(trackId)}/offset";

    /// <summary>Create a score item: <c>POST /api/assets/scores</c> (1.7.0).</summary>
    public const string Scores = "/api/assets/scores";

    /// <summary>Sheet music options: <c>GET /api/scores/options</c> (1.7.0).</summary>
    public const string ScoreOptions = "/api/scores/options";

    /// <summary>Sheet music options of a track channel.</summary>
    /// <param name="trackId">Track id.</param>
    /// <param name="channel">Channel id (e.g. <c>bass</c>, <c>vocals.lead</c>).</param>
    /// <returns>The route with the query.</returns>
    public static string ScoreOptionsForChannel(Guid trackId, string channel) => $"{ScoreOptions}?trackId={trackId}&channel={Uri.EscapeDataString(channel)}";

    /// <summary>Sheet music options of a library audio item.</summary>
    /// <param name="assetId">Item id.</param>
    /// <returns>The route with the query.</returns>
    public static string ScoreOptionsForAsset(Guid assetId) => $"{ScoreOptions}?assetId={assetId}";

    /// <summary>Generation availability: <c>GET /api/generation/status</c> (1.8.0).</summary>
    public const string GenerationStatus = "/api/generation/status";

    /// <summary>The style library: <c>GET</c> (filters), <c>POST</c> (create) (1.8.0).</summary>
    public const string Styles = "/api/styles";

    /// <summary>One style: <c>GET</c>, <c>PUT</c>, <c>DELETE</c> (1.8.0).</summary>
    /// <param name="styleId">Style id.</param>
    /// <returns>The route.</returns>
    public static string Style(Guid styleId) => $"{Styles}/{styleId}";

    /// <summary>Copy a style into a user style: <c>POST</c> (1.8.0).</summary>
    /// <param name="styleId">Style id.</param>
    /// <returns>The route.</returns>
    public static string DuplicateStyle(Guid styleId) => $"{Style(styleId)}/duplicate";

    /// <summary>Restyle a completed track: <c>POST /api/tracks/{id}/restyles</c> (1.8.0).</summary>
    /// <param name="trackId">Track id.</param>
    /// <returns>The route.</returns>
    public static string Restyles(Guid trackId) => $"{Track(trackId)}/restyles";

    /// <summary>Generations, newest first: <c>GET</c> (<c>?kind=</c>, <c>?sourceTrackId=</c>) (1.8.0).</summary>
    public const string Generations = "/api/generations";

    /// <summary>One generation: <c>GET</c>, <c>DELETE</c> (1.8.0).</summary>
    /// <param name="generationId">Generation id.</param>
    /// <returns>The route.</returns>
    public static string Generation(Guid generationId) => $"{Generations}/{generationId}";

    /// <summary>Cancel a generation: <c>POST</c> (1.8.0).</summary>
    /// <param name="generationId">Generation id.</param>
    /// <returns>The route.</returns>
    public static string CancelGeneration(Guid generationId) => $"{Generation(generationId)}/cancel";

    /// <summary>Retry a failed or cancelled generation: <c>POST</c> (1.8.0).</summary>
    /// <param name="generationId">Generation id.</param>
    /// <returns>The route.</returns>
    public static string RetryGeneration(Guid generationId) => $"{Generation(generationId)}/retry";

    /// <summary>One take of a generation (base of the take routes).</summary>
    /// <param name="generationId">Generation id.</param>
    /// <param name="index">Take number from 1.</param>
    /// <returns>The route.</returns>
    public static string Take(Guid generationId, int index) => string.Create(CultureInfo.InvariantCulture, $"{Generation(generationId)}/takes/{index}");

    /// <summary>Take audio with Range support: <c>GET</c> (1.8.0).</summary>
    /// <param name="generationId">Generation id.</param>
    /// <param name="index">Take number from 1.</param>
    /// <returns>The route.</returns>
    public static string TakeAudio(Guid generationId, int index) => $"{Take(generationId, index)}/audio";

    /// <summary>Import a take as a track: <c>POST</c> (1.8.0).</summary>
    /// <param name="generationId">Generation id.</param>
    /// <param name="index">Take number from 1.</param>
    /// <returns>The route.</returns>
    public static string ImportTake(Guid generationId, int index) => $"{Take(generationId, index)}/import";

    /// <summary>"Use my voice" on a song take: <c>POST</c> (1.9.0).</summary>
    /// <param name="generationId">Generation id.</param>
    /// <param name="index">Take number from 1.</param>
    /// <returns>The route.</returns>
    public static string VoiceConversions(Guid generationId, int index) => $"{Take(generationId, index)}/voice-conversions";

    /// <summary>The converted vocal of a voice-conversion take: <c>GET</c> (1.9.0).</summary>
    /// <param name="generationId">Generation id.</param>
    /// <param name="index">Take number from 1.</param>
    /// <returns>The route.</returns>
    public static string TakeVocal(Guid generationId, int index) => $"{Take(generationId, index)}/vocal";

    /// <summary>Saved lyrics sheets: <c>GET</c> (<c>?q=</c>), <c>POST</c> (1.9.0). Not the synced lyrics of a track (<see cref="Lyrics(Guid)"/>).</summary>
    public const string LyricsSheets = "/api/lyrics";

    /// <summary>Upload a lyrics file: <c>POST</c> multipart (1.9.0).</summary>
    public const string LyricsSheetUpload = "/api/lyrics/upload";

    /// <summary>Propose section markers: <c>POST</c> (1.9.0).</summary>
    public const string LyricsSections = "/api/lyrics/sections";

    /// <summary>One lyrics sheet: <c>GET</c>, <c>PUT</c>, <c>DELETE</c> (1.9.0).</summary>
    /// <param name="lyricsId">Sheet id.</param>
    /// <returns>The route.</returns>
    public static string LyricsSheet(Guid lyricsId) => $"{LyricsSheets}/{lyricsId}";

    /// <summary>Generate a song: <c>POST /api/songs</c> (1.9.0).</summary>
    public const string Songs = "/api/songs";

    /// <summary>Voice profiles: <c>GET</c>, <c>POST</c> multipart (1.9.0).</summary>
    public const string VoiceProfiles = "/api/voice-profiles";

    /// <summary>One voice profile: <c>GET</c>, <c>DELETE</c> (<c>?deleteTakes=</c>) (1.9.0).</summary>
    /// <param name="profileId">Profile id.</param>
    /// <returns>The route.</returns>
    public static string VoiceProfile(Guid profileId) => $"{VoiceProfiles}/{profileId}";

    /// <summary>The normalised sample of a profile: <c>GET</c> with Range (1.9.0).</summary>
    /// <param name="profileId">Profile id.</param>
    /// <returns>The route.</returns>
    public static string VoiceSample(Guid profileId) => $"{VoiceProfile(profileId)}/sample";

    /// <summary>The current consent statement: <c>GET</c> (1.9.0).</summary>
    public const string VoiceConsent = "/api/consent/voice";
}
