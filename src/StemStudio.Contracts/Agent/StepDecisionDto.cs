using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Agent;

/// <summary>Policy decision for a tool call (ADR-020 D5, data-model 6).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<StepDecisionDto>))]
public enum StepDecisionDto
{
    /// <summary>Executed without asking.</summary>
    [JsonStringEnumMemberName("auto")]
    Auto,

    /// <summary>Executed only after the user approves.</summary>
    [JsonStringEnumMemberName("approval_required")]
    ApprovalRequired,

    /// <summary>Refused by a guardrail; never executed.</summary>
    [JsonStringEnumMemberName("denied")]
    Denied,
}
