using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Agent;

/// <summary>Risk class of a tool, fixed in code (tool catalogue rules).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ToolRiskDto>))]
public enum ToolRiskDto
{
    /// <summary>No state change.</summary>
    [JsonStringEnumMemberName("read")]
    Read,

    /// <summary>Reversible state change.</summary>
    [JsonStringEnumMemberName("change")]
    Change,

    /// <summary>Starts a short background job (under about 2 minutes on the reference GPU).</summary>
    [JsonStringEnumMemberName("job")]
    Job,

    /// <summary>Long GPU/CPU job or large disk use; always needs approval.</summary>
    [JsonStringEnumMemberName("heavy")]
    Heavy,

    /// <summary>Deletes or overwrites without inverse; always needs approval.</summary>
    [JsonStringEnumMemberName("destructive")]
    Destructive,
}
