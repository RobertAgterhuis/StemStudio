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
}
