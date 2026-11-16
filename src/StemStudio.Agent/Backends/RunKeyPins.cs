using StemStudio.Agent.Model;

namespace StemStudio.Agent.Backends;

/// <summary>
/// The Claude key each run started with (CK-FR-016): the first turn of a run (no model turn yet) takes the current key and
/// pins it; later turns reuse the pin. A run without a pin (dropped, or resumed after a restart) takes the current key.
/// Bounded: the oldest pin is dropped first. Keys stay in memory only.
/// </summary>
/// <param name="capacity">Maximum number of pinned runs.</param>
internal sealed class RunKeyPins(int capacity)
{
    private readonly Lock _gate = new();
    private readonly Dictionary<Guid, ClaudeApiKey> _pins = [];
    private readonly Queue<Guid> _order = new();

    /// <summary>The key of a turn of a run.</summary>
    /// <param name="run">The run.</param>
    /// <param name="current">Resolves the current key; throws when none is usable.</param>
    /// <returns>The pinned or current key.</returns>
    public ClaudeApiKey For(AgentRun run, Func<ClaudeApiKey> current)
    {
        lock (_gate)
        {
            if (run.ModelTurns > 0 && _pins.TryGetValue(run.Id, out var pinned))
            {
                return pinned;
            }

            var key = current();
            Pin(run.Id, key);
            return key;
        }
    }

    private void Pin(Guid run, ClaudeApiKey key)
    {
        if (!_pins.ContainsKey(run))
        {
            _order.Enqueue(run);
        }

        _pins[run] = key;
        while (_order.Count > capacity)
        {
            _pins.Remove(_order.Dequeue());
        }
    }
}
