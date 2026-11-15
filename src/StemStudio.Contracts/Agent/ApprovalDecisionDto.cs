using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Agent;

/// <summary>The user's decision on an approval card.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ApprovalDecisionDto>))]
public enum ApprovalDecisionDto
{
    /// <summary>Execute the step.</summary>
    [JsonStringEnumMemberName("approve")]
    Approve,

    /// <summary>Do not execute; the model is told the user rejected it.</summary>
    [JsonStringEnumMemberName("reject")]
    Reject,
}
