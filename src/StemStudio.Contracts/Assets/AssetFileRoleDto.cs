using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Assets;

/// <summary>Role of a file of a library item (1.5.0: <c>audio</c>; 1.7.0: the three files of a score item).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AssetFileRoleDto>))]
public enum AssetFileRoleDto
{
    /// <summary>The audio file of an audio item.</summary>
    [JsonStringEnumMemberName("audio")]
    Audio,

    /// <summary>The PDF of a score item (1.7.0).</summary>
    [JsonStringEnumMemberName("pdf")]
    Pdf,

    /// <summary>The MusicXML file of a score item (1.7.0).</summary>
    [JsonStringEnumMemberName("musicxml")]
    MusicXml,

    /// <summary>The MIDI file of a score item (1.7.0).</summary>
    [JsonStringEnumMemberName("midi")]
    Midi,
}
