namespace StemStudio.Contracts;

/// <summary>Validation limits that are part of the public contract (spec FR-003, FR-014, FR-016).</summary>
public static class ContractLimits
{
    /// <summary>Maximum upload size in bytes (200 MiB).</summary>
    public const long MaxUploadBytes = 200L * 1024 * 1024;

    /// <summary>Maximum track duration in seconds (15 minutes).</summary>
    public const double MaxDurationSeconds = 900;

    /// <summary>Allowed upload file extensions (lower case, with dot).</summary>
    public static readonly IReadOnlyList<string> UploadExtensions = [".mp3", ".wav", ".flac"];

    /// <summary>Minimum mix name length (after trimming).</summary>
    public const int MixNameMinLength = 1;

    /// <summary>Maximum mix name length (after trimming).</summary>
    public const int MixNameMaxLength = 100;

    /// <summary>Default channel fader volume (80 %).</summary>
    public const double DefaultChannelVolume = 0.8;

    /// <summary>Default master volume.</summary>
    public const double DefaultMasterVolume = 1.0;

    /// <summary>Default metronome volume.</summary>
    public const double DefaultMetronomeVolume = 0.7;

    /// <summary>Lowest manual BPM override.</summary>
    public const double MinBpm = 30;

    /// <summary>Highest manual BPM override.</summary>
    public const double MaxBpm = 300;

    /// <summary>Lowest metronome offset in milliseconds.</summary>
    public const int MinOffsetMs = -2000;

    /// <summary>Highest metronome offset in milliseconds.</summary>
    public const int MaxOffsetMs = 2000;

    /// <summary>Lowest beats per bar.</summary>
    public const int MinBeatsPerBar = 1;

    /// <summary>Highest beats per bar.</summary>
    public const int MaxBeatsPerBar = 12;

    /// <summary>Maximum entries of <c>MixSettings.expanded</c> (one per splittable parent).</summary>
    public const int MaxExpandedParents = 5;

    /// <summary>Maximum entries of <c>MixSettings.detailChannels</c>.</summary>
    public const int MaxDetailChannels = 50;

    /// <summary>Maximum detail stems of one split parent (including <c>rest</c>).</summary>
    public const int MaxDetailStemsPerParent = 10;

    /// <summary>Largest own picture upload in bytes (10 MiB, feature 005); enforced while streaming.</summary>
    public const long MaxArtworkUploadBytes = 10L * 1024 * 1024;

    /// <summary>Allowed own picture media types (feature 005).</summary>
    public static readonly IReadOnlyList<string> ArtworkMediaTypes = ["image/jpeg", "image/png", "image/webp"];

    /// <summary>Longest title or artist of a metadata correction (feature 005).</summary>
    public const int MetadataNameMaxLength = 500;

    /// <summary>Longest library item name (feature 007).</summary>
    public const int AssetNameMaxLength = 120;

    /// <summary>Most channels of one export (feature 007).</summary>
    public const int MaxExportChannels = 56;

    /// <summary>Shortest export range in seconds (feature 007).</summary>
    public const double MinExportRangeSeconds = 0.5;

    /// <summary>Default free-space guard of exports (1 GiB, <c>StemStudio:MinFreeDiskBytes</c>).</summary>
    public const long MinFreeDiskBytesDefault = 1L * 1024 * 1024 * 1024;

    /// <summary>Export formats (feature 007).</summary>
    public static readonly IReadOnlyList<string> ExportFormats = ["flac", "wav", "mp3"];

    /// <summary>FLAC/WAV bit depths (feature 007).</summary>
    public static readonly IReadOnlyList<int> ExportBitDepths = [16, 24];

    /// <summary>MP3 constant bit rates in kbps (feature 007).</summary>
    public static readonly IReadOnlyList<int> Mp3Bitrates = [192, 256, 320];

    /// <summary>Most items per <c>GET /api/assets</c> page (default 100).</summary>
    public const int MaxAssetPageSize = 200;

    /// <summary>Channel id of an export: a standard stem or <c>&lt;parent&gt;.&lt;detail&gt;</c> (same as the engine's ChannelId).</summary>
    public const string ChannelIdPattern = @"^(vocals|drums|bass|guitar|piano|other)(\.[a-z][a-z-]{0,31})?$";

    /// <summary>
    /// Channel id of an export channel list (backend-api 1.14.0 <c>ExportChannelId</c>): a <see cref="ChannelIdPattern"/> value or the
    /// literal <c>take</c> (the take channel). <see cref="ChannelIdPattern"/> itself is unchanged (scores and agent tools use it).
    /// </summary>
    public const string ExportChannelIdPattern = @"^(take|(vocals|drums|bass|guitar|piano|other)(\.[a-z][a-z-]{0,31})?)$";

    /// <summary>Longest lyrics text in characters (feature 006).</summary>
    public const int MaxLyricsChars = 20_000;

    /// <summary>Most lines of a lyrics document (feature 006).</summary>
    public const int MaxLyricsLines = 400;

    /// <summary>Largest absolute lyrics offset in milliseconds (feature 006).</summary>
    public const int MaxLyricsOffsetMs = 5000;

    /// <summary>Step of the lyrics offset in milliseconds (feature 006).</summary>
    public const int LyricsOffsetStepMs = 10;

    /// <summary>ISO 639 language code of lyrics (feature 006).</summary>
    public const string LyricsLanguagePattern = "^[a-z]{2,3}$";

    /// <summary>Shortest score range in seconds (feature 008).</summary>
    public const double MinScoreRangeSeconds = 2;

    /// <summary>Instrument profiles of sheet music (feature 008).</summary>
    public static readonly IReadOnlyList<string> ScoreProfiles = ["vocals", "bass", "guitar", "keys", "piano", "drums"];

    /// <summary>Score resolutions (feature 008).</summary>
    public static readonly IReadOnlyList<string> ScoreResolutions = ["sixteenth", "eighth"];

    /// <summary>The 30 key names of a key override in music21 spelling, <c>-</c> = flat (feature 008).</summary>
    public static readonly IReadOnlyList<string> KeyNames =
    [
        "C major", "G major", "D major", "A major", "E major", "B major", "F# major", "C# major",
        "F major", "B- major", "E- major", "A- major", "D- major", "G- major", "C- major",
        "A minor", "E minor", "B minor", "F# minor", "C# minor", "G# minor", "D# minor", "A# minor",
        "D minor", "G minor", "C minor", "F minor", "B- minor", "E- minor", "A- minor",
    ];

    /// <summary>Shortest restyle range and song in seconds (features 009 and 010).</summary>
    public const double MinGenerationSeconds = 10;

    /// <summary>Longest restyle range, in-place track and song in seconds (features 009 and 010).</summary>
    public const double MaxGenerationSeconds = 480;

    /// <summary>Most takes per generation (feature 009).</summary>
    public const int MaxTakes = 2;

    /// <summary>Longest style name (feature 009).</summary>
    public const int StyleNameMaxLength = 60;

    /// <summary>Longest style sub-genre (feature 009).</summary>
    public const int StyleSubGenreMaxLength = 40;

    /// <summary>Longest style description (feature 009).</summary>
    public const int StyleDescriptionMaxLength = 300;

    /// <summary>Most instrument or mood tags of a style (feature 009).</summary>
    public const int StyleMaxTags = 8;

    /// <summary>Longest style tag (feature 009).</summary>
    public const int StyleTagMaxLength = 30;

    /// <summary>Longest model caption (features 009 and 010).</summary>
    public const int CaptionMaxLength = 512;

    /// <summary>Most characters of lyrics after normalisation (feature 010).</summary>
    public const int LyricsSheetMaxLength = 4096;

    /// <summary>Longest lyrics sheet or song title (feature 010).</summary>
    public const int LyricsTitleMaxLength = 80;

    /// <summary>Largest lyrics upload in bytes (64 KiB, feature 010).</summary>
    public const long MaxLyricsUploadBytes = 64L * 1024;

    /// <summary>Allowed lyrics upload extensions (lower case, with dot; feature 010).</summary>
    public static readonly IReadOnlyList<string> LyricsUploadExtensions = [".txt", ".lrc", ".md"];

    /// <summary>Lyrics density in words per second above which the song page warns (feature 010).</summary>
    public const double LyricsDensityWarningWordsPerSecond = 4.0;

    /// <summary>Longest free caption of a song (feature 010).</summary>
    public const int FreeCaptionMaxLength = 300;

    /// <summary>Longest delivery text of a song (feature 010).</summary>
    public const int DeliveryMaxLength = 40;

    /// <summary>Slowest and fastest song tempo (feature 010).</summary>
    public const int MinSongBpm = 40;

    /// <summary>Fastest song tempo (feature 010).</summary>
    public const int MaxSongBpm = 220;

    /// <summary>Key of a song, e.g. <c>E minor</c> (feature 010).</summary>
    public const string SongKeyPattern = "^[A-G](#|b)? (major|minor)$";

    /// <summary>Time signatures of a song (beats per bar; feature 010).</summary>
    public static readonly IReadOnlyList<string> SongTimeSignatures = ["2", "3", "4", "6"];

    /// <summary>Lyrics languages of ACE-Step 1.5 plus <c>unknown</c> (feature 010; contract <c>LyricsLanguage</c>).</summary>
    public static readonly IReadOnlyList<string> LyricsLanguages =
    [
        "ar", "az", "bg", "bn", "ca", "cs", "da", "de", "el", "en", "es", "fa", "fi", "fr", "he", "hi",
        "hr", "ht", "hu", "id", "is", "it", "ja", "ko", "la", "lt", "ms", "ne", "nl", "no", "pa", "pl",
        "pt", "ro", "ru", "sa", "sk", "sr", "sv", "sw", "ta", "te", "th", "tl", "tr", "uk", "ur", "vi",
        "yue", "zh", "unknown",
    ];

    /// <summary>Shortest voice sample in seconds (feature 010).</summary>
    public const double MinVoiceSampleSeconds = 10;

    /// <summary>Longest voice sample in seconds (feature 010).</summary>
    public const double MaxVoiceSampleSeconds = 60;

    /// <summary>Largest voice sample upload in bytes (20 MiB, feature 010).</summary>
    public const long MaxVoiceSampleBytes = 20L * 1024 * 1024;

    /// <summary>Longest voice profile display name (feature 010).</summary>
    public const int VoiceNameMaxLength = 60;

    /// <summary>Longest person name of a consent (feature 010).</summary>
    public const int ConsentPersonNameMaxLength = 80;

    /// <summary>Current version of the voice consent statement (feature 010, ADR-019 D3).</summary>
    public const string ConsentStatementVersion = "consent.v1";

    /// <summary>Largest pitch shift of a voice conversion in semitones (feature 010).</summary>
    public const int MaxPitchShift = 12;
}
