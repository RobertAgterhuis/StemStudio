using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Agent;

/// <summary>Where a tool is executed (tool catalogue rules).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ToolKindDto>))]
public enum ToolKindDto
{
    /// <summary>Calls the public StemStudio HTTP API over loopback.</summary>
    [JsonStringEnumMemberName("server")]
    Server,

    /// <summary>Executed by the open browser session (mixer page or layout).</summary>
    [JsonStringEnumMemberName("ui")]
    Ui,

    /// <summary>Changes only the run itself (plan, job wait).</summary>
    [JsonStringEnumMemberName("meta")]
    Meta,
}
