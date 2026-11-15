using StemStudio.Domain.Mixes;

namespace StemStudio.Domain.Rendering;

/// <summary>How the channels of an export are rendered (feature 007, data-model 4.2).</summary>
public enum ExportMode
{
    /// <summary>Gain 1, pan 0, master 1; mute and solo ignored.</summary>
    Raw,

    /// <summary>Faders, pans and master of a mix.</summary>
    Mix,
}

/// <summary>Audio file format of an export.</summary>
public enum AudioFormat
{
    /// <summary>FLAC (lossless).</summary>
    Flac,

    /// <summary>WAV (PCM).</summary>
    Wav,

    /// <summary>MP3 (constant bit rate).</summary>
    Mp3,
}

/// <summary>The resolved output format: a bit depth for FLAC/WAV, a bit rate for MP3, the other value null.</summary>
/// <param name="Format">File format.</param>
/// <param name="BitDepth">16 or 24 (FLAC/WAV), else null.</param>
/// <param name="Mp3BitrateKbps">192, 256 or 320 (MP3), else null.</param>
public sealed record ExportFormat(AudioFormat Format, int? BitDepth, int? Mp3BitrateKbps);

/// <summary>A time range of an export in seconds.</summary>
/// <param name="StartSeconds">Start (&gt;= 0).</param>
/// <param name="EndSeconds">End (&gt; start, &lt;= the track duration).</param>
public sealed record ExportRange(double StartSeconds, double EndSeconds);

/// <summary>The mixer state an export uses (a mix without name, metronome and loop).</summary>
/// <param name="MasterVolume">Master volume 0..1.</param>
/// <param name="Channels">Standard channel settings.</param>
public sealed record ExportMix(double MasterVolume, IReadOnlyList<ChannelSettings> Channels)
{
    /// <summary>Expanded parents.</summary>
    public IReadOnlyList<string> Expanded { get; init; } = [];

    /// <summary>Detail channel settings.</summary>
    public IReadOnlyList<DetailChannelSettings> DetailChannels { get; init; } = [];
}

/// <summary>The normalised export request, stored with the item and used again by retry (data-model 4.2).</summary>
public sealed record ExportRecipe
{
    /// <summary>The source track.</summary>
    public required Guid TrackId { get; init; }

    /// <summary>Raw or mixed.</summary>
    public required ExportMode Mode { get; init; }

    /// <summary>Explicit selection, or null for every audible channel (mix only).</summary>
    public IReadOnlyList<string>? Channels { get; init; }

    /// <summary>Snapshot of the mix used (mix only).</summary>
    public ExportMix? Mix { get; init; }

    /// <summary>Time range, or null for the whole track.</summary>
    public ExportRange? Range { get; init; }

    /// <summary>Resolved output format.</summary>
    public required ExportFormat Format { get; init; }
}
