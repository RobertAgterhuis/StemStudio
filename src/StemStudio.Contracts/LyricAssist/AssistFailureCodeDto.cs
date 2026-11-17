using System.Text.Json.Serialization;

namespace StemStudio.Contracts.LyricAssist;

/// <summary>Why an assist failed (<c>LyricAssistSummary.failure.code</c>).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AssistFailureCodeDto>))]
public enum AssistFailureCodeDto
{
    /// <summary>The model backend is not reachable.</summary>
    [JsonStringEnumMemberName("backend-unavailable")]
    BackendUnavailable,

    /// <summary>The Claude token cap of the assist was reached.</summary>
    [JsonStringEnumMemberName("budget-exhausted")]
    BudgetExhausted,

    /// <summary>The model refused.</summary>
    [JsonStringEnumMemberName("refusal")]
    Refusal,

    /// <summary>The assist timed out.</summary>
    [JsonStringEnumMemberName("timeout")]
    Timeout,

    /// <summary>The API restarted while the assist ran.</summary>
    [JsonStringEnumMemberName("interrupted")]
    Interrupted,

    /// <summary>Unexpected error.</summary>
    [JsonStringEnumMemberName("internal")]
    Internal,
}
