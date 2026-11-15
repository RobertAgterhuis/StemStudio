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
}
