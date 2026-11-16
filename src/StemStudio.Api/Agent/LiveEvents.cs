using System.Threading.Channels;
using StemStudio.Agent.Model;
using StemStudio.Contracts.Agent;

namespace StemStudio.Api.Agent;

/// <summary>
/// Reads live run events for <see cref="SseWriter"/> with the keep-alive interval: a read that is still pending when the interval
/// elapses is kept for the next call, so no event is lost.
/// </summary>
/// <param name="reader">Events of the subscription.</param>
/// <param name="clock">Clock of the interval.</param>
internal sealed class LiveEvents(ChannelReader<RunEvent> reader, TimeProvider clock)
{
    private static readonly TimeSpan KeepAlive = TimeSpan.FromSeconds(AgentContractLimits.KeepAliveSeconds);
    private Task<RunEvent>? _pending;

    /// <summary>The next event, or null when the keep-alive interval elapsed first.</summary>
    /// <param name="cancellationToken">Request aborted.</param>
    /// <returns>The event or null.</returns>
    public async Task<RunEvent?> NextAsync(CancellationToken cancellationToken)
    {
        _pending ??= reader.ReadAsync(cancellationToken).AsTask();
        using var tick = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (await Task.WhenAny(_pending, Task.Delay(KeepAlive, clock, tick.Token)) != _pending)
        {
            return null;
        }

        await tick.CancelAsync();
        var live = await _pending;
        _pending = null;
        return live;
    }
}
