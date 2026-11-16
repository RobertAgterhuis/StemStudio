using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Agent;

/// <summary>State of an approval; the first decision wins.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ApprovalStatusDto>))]
public enum ApprovalStatusDto
{
    /// <summary>Waiting for the user.</summary>
    [JsonStringEnumMemberName("pending")]
    Pending,

    /// <summary>Approved by the user.</summary>
    [JsonStringEnumMemberName("approved")]
    Approved,

    /// <summary>Rejected by the user.</summary>
    [JsonStringEnumMemberName("rejected")]
    Rejected,

    /// <summary>No longer decidable (run ended, 24 h, API restart).</summary>
    [JsonStringEnumMemberName("expired")]
    Expired,
}
