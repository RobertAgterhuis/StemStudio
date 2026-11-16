using StemStudio.Agent.Ports;
using StemStudio.Agent.Tools;
using StemStudio.Contracts.Generation;

namespace StemStudio.Agent.Runtime;

/// <summary>
/// Reads the generation and voice service states for the tool selection (<c>GET /api/generation/status</c>, cached 10 s,
/// tool catalogue 1.0.0 "+service"). A failed read makes both services unknown, so their tools are not offered.
/// </summary>
/// <param name="generations">Generation actions.</param>
/// <param name="time">Clock.</param>
public sealed class ServiceStatusReader(IGenerationActions generations, TimeProvider time)
{
    /// <summary>How long a read status is reused.</summary>
    public static TimeSpan CacheDuration { get; } = TimeSpan.FromSeconds(10);

    private volatile Snapshot? _cache;

    /// <summary>The tool context of a page.</summary>
    /// <param name="page">Page context.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The context.</returns>
    public async Task<ToolContext> ContextAsync(Contracts.Agent.PageContextDto page, CancellationToken cancellationToken)
    {
        var (generation, voice) = await ReadAsync(cancellationToken);
        return ToolContext.From(page, generation, voice);
    }

    /// <summary>The page context with the last known service states, without reading them (unknown before the first read).</summary>
    /// <param name="page">Page context.</param>
    /// <returns>The tool context.</returns>
    public ToolContext Peek(Contracts.Agent.PageContextDto page) => _cache is { } cached
        ? ToolContext.From(page, cached.Generation, cached.Voice)
        : ToolContext.From(page, ServiceAvailability.Unknown, ServiceAvailability.Unknown);

    private async Task<(ServiceAvailability Generation, ServiceAvailability Voice)> ReadAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        if (_cache is { } cached && now - cached.ReadAt < CacheDuration)
        {
            return (cached.Generation, cached.Voice);
        }

        var status = await TryReadAsync(cancellationToken);
        var result = status is null ? (ServiceAvailability.Unknown, ServiceAvailability.Unknown) : (Map(status.State, status.Reason), Map(status.Voice.State, status.Voice.Reason));
        _cache = new Snapshot(result.Item1, result.Item2, now);
        return result;
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

    private static ServiceAvailability Map(GenerationAvailabilityDto state, string? reason) =>
        state == GenerationAvailabilityDto.Ready ? ServiceAvailability.Available : ServiceAvailability.Unavailable(reason ?? "The generator is not available.");

    private sealed record Snapshot(ServiceAvailability Generation, ServiceAvailability Voice, DateTimeOffset ReadAt);
}
