namespace StemStudio.Contracts.Assets;

/// <summary>Output format of an export (backend contract 1.5.0).</summary>
/// <param name="Format">FLAC, WAV or MP3.</param>
/// <param name="BitDepth">16 or 24 for FLAC/WAV; null = 16 for one raw channel, else 24.</param>
/// <param name="Mp3BitrateKbps">192, 256 or 320 for MP3; null = 320.</param>
public sealed record ExportFormatDto(AudioFileFormatDto Format, int? BitDepth = null, int? Mp3BitrateKbps = null);
