using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Assets;

/// <summary>Audio file format of an export.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AudioFileFormatDto>))]
public enum AudioFileFormatDto
{
    /// <summary>FLAC (lossless, 16 or 24 bit).</summary>
    [JsonStringEnumMemberName("flac")]
    Flac,

    /// <summary>WAV (PCM, 16 or 24 bit).</summary>
    [JsonStringEnumMemberName("wav")]
    Wav,

    /// <summary>MP3 (constant bit rate 192, 256 or 320 kbps).</summary>
    [JsonStringEnumMemberName("mp3")]
    Mp3,
}
