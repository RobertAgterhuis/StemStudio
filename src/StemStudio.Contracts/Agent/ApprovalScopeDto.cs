using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Agent;

/// <summary>What an approval covers (data-model 4.3).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ApprovalScopeDto>))]
public enum ApprovalScopeDto
{
    /// <summary>Only this step.</summary>
    [JsonStringEnumMemberName("once")]
    Once,

    /// <summary>Also later heavy steps of the same run whose tool and track match a plan item; never destructive steps or compensations.</summary>
    [JsonStringEnumMemberName("plan_heavy")]
    PlanHeavy,
}
