using StemStudio.Agent.Ports;
using StemStudio.Agent.Tools;
using StemStudio.Contracts.Generation;

namespace StemStudio.Agent.Runtime;

/// <summary>
/// Reads the generation and voice service states (<c>GET /api/generation/status</c>) and the lyric model state (023, the 011 agent
/// status that <c>GET /api/lyric-assist/status</c> mirrors) for the tool selection, cached 10 s (tool catalogue "+service"). A
/// failed read makes the services unknown, so their tools are not offered.
/// </summary>
/// <param name="generations">Generation actions.</param>
/// <param name="time">Clock.</param>
public sealed class ServiceStatusReader(IGenerationActions generations, TimeProvider time)
{
    /// <summary>Reason while the lyric model state cannot be read.</summary>
    public const string LyricModelUnknown = "The lyric model state is not known.";

    /// <summary>How long a read status is reused.</summary>
    public static TimeSpan CacheDuration { get; } = TimeSpan.FromSeconds(10);

    private volatile Snapshot? _cache;

    /// <summary>Reads the lyric model state; null keeps it unknown (suggest_lyric_lines is not offered).</summary>
    public Func<CancellationToken, Task<ServiceAvailability>>? LyricModel { get; init; }

    /// <summary>The lyric model state from the agent status: ready when the selected backend can answer now.</summary>
    /// <param name="status">Agent status service.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The availability.</returns>
    public static async Task<ServiceAvailability> LyricModelAsync(IAgentStatusService status, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(status);
        var current = await status.GetStatusAsync(cancellationToken);
        return current.Ready ? ServiceAvailability.Available : ServiceAvailability.Unavailable(current.Reason ?? LyricModelUnknown);
    }

    /// <summary>The tool context of a page.</summary>
    /// <param name="page">Page context.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The context.</returns>
    public async Task<ToolContext> ContextAsync(Contracts.Agent.PageContextDto page, CancellationToken cancellationToken)
    {
        var snapshot = await ReadAsync(cancellationToken);
        return ToolContext.From(page, snapshot.Generation, snapshot.Voice, snapshot.Lyric);
    }

    /// <summary>The page context with the last known service states, without reading them (unknown before the first read).</summary>
    /// <param name="page">Page context.</param>
    /// <returns>The tool context.</returns>
    public ToolContext Peek(Contracts.Agent.PageContextDto page) => _cache is { } cached
        ? ToolContext.From(page, cached.Generation, cached.Voice, cached.Lyric)
        : ToolContext.From(page, ServiceAvailability.Unknown, ServiceAvailability.Unknown, ServiceAvailability.Unknown);

    private async Task<Snapshot> ReadAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        if (_cache is { } cached && now - cached.ReadAt < CacheDuration)
        {
            return cached;
        }

        var status = await TryReadAsync(cancellationToken);
        var snapshot = new Snapshot(
            status is null ? ServiceAvailability.Unknown : Map(status.State, status.Reason),
            status is null ? ServiceAvailability.Unknown : Map(status.Voice.State, status.Voice.Reason),
            await TryLyricAsync(cancellationToken),
            now);
        _cache = snapshot;
        return snapshot;
    }

    private async Task<GenerationStatusDto?> TryReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await generations.GetStatusAsync(cancellationToken);
        }
        catch (AgentActionException)
        {
            return null;
        }
    }

    private async Task<ServiceAvailability> TryLyricAsync(CancellationToken cancellationToken)
    {
        if (LyricModel is null)
        {
            return ServiceAvailability.Unknown;
        }

        try
        {
            return await LyricModel(cancellationToken);
        }
        catch (Exception error) when (error is AgentActionException or HttpRequestException or InvalidOperationException)
        {
            return ServiceAvailability.Unavailable(LyricModelUnknown);
        }
    }

    private static ServiceAvailability Map(GenerationAvailabilityDto state, string? reason) =>
        state == GenerationAvailabilityDto.Ready ? ServiceAvailability.Available : ServiceAvailability.Unavailable(reason ?? "The generator is not available.");

    private sealed record Snapshot(ServiceAvailability Generation, ServiceAvailability Voice, ServiceAvailability Lyric, DateTimeOffset ReadAt);
}
