using StemStudio.Contracts.Agent;

namespace StemStudio.Contracts.LyricAssist;

/// <summary>Model layer of <c>GET /api/lyric-assist/status</c>; mirrors the 011 agent status.</summary>
public sealed record LyricAssistModelStatusDto
{
    /// <summary>Selected agent backend.</summary>
    public required AgentBackendDto Backend { get; init; }

    /// <summary>True when the model layer can run now.</summary>
    public required bool Ready { get; init; }

    /// <summary>Model the next request would use.</summary>
    public string? ActiveModel { get; init; }

    /// <summary>True while the CPU fallback model is used because a GPU job runs.</summary>
    public required bool UsingFallback { get; init; }

    /// <summary>Readable reason when not ready.</summary>
    public string? Reason { get; init; }

    /// <summary>Readable fix when not ready.</summary>
    public string? Fix { get; init; }

    /// <summary>Standing quality note (SC-001), e.g. Dutch grammar with the local model; at most 300 characters.</summary>
    public string? QualityNote { get; init; }
}
