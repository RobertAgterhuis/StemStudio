using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Agent;

/// <summary>How a step is reversed by undo.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<UndoKindDto>))]
public enum UndoKindDto
{
    /// <summary>Not reversible by undo.</summary>
    [JsonStringEnumMemberName("none")]
    None,

    /// <summary>An automatic inverse call restores the previous state.</summary>
    [JsonStringEnumMemberName("inverse")]
    Inverse,

    /// <summary>Deletes what the call created (asks for approval unless fresh).</summary>
    [JsonStringEnumMemberName("compensate")]
    Compensate,
}
