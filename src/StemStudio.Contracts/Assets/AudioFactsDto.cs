namespace StemStudio.Contracts.Assets;

/// <summary>Facts of a ready audio item (backend contract 1.5.0).</summary>
public sealed record AudioFactsDto
{
    /// <summary>File format.</summary>
    public required AudioFileFormatDto Format { get; init; }

    /// <summary>16 or 24 for FLAC/WAV; null for MP3.</summary>
    public int? BitDepth { get; init; }

    /// <summary>Bit rate for MP3; null otherwise.</summary>
    public int? Mp3BitrateKbps { get; init; }

    /// <summary>Always 44100.</summary>
    public int SampleRate { get; init; } = 44_100;

    /// <summary>Always 2.</summary>
    public int Channels { get; init; } = 2;

    /// <summary>Duration in seconds.</summary>
    public required double DurationSeconds { get; init; }

    /// <summary>Peak of the written samples in dBFS (&lt;= 0).</summary>
    public required double PeakDbfs { get; init; }

    /// <summary>0, or the negative gain applied to avoid clipping ("Lowered by X dB").</summary>
    public required double AppliedGainDb { get; init; }
}
