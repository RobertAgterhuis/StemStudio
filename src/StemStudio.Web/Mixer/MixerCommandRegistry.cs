using System.Globalization;

namespace StemStudio.Web.Mixer;

/// <summary>
/// The mixer's command dispatcher (014 T067; data-model 5.2): maps a command id from the key map (<c>OnCommand</c>) to
/// the same action as its on-screen control. Ids with an argument (<c>mixer.seek:-5</c>, <c>mixer.channel.select:3</c>)
/// are registered by their base id and receive the number. Unknown ids are ignored, so a catalogue row whose feature
/// has not registered a handler never does anything.
/// <para>
/// Extension point for other features (015 recording): a partial file of <c>Components/Mixer/Mixer</c> implements
/// <c>partial void RegisterRecordingCommands(MixerCommandRegistry commands)</c> (the 015 hook) and calls <see cref="Add(string, Func{Task})"/>.
/// </para>
/// </summary>
public sealed class MixerCommandRegistry
{
    private readonly Dictionary<string, Func<double, Task>> _handlers = new(StringComparer.Ordinal);

    /// <summary>Base ids with a handler.</summary>
    public IReadOnlyCollection<string> Commands => _handlers.Keys;

    /// <summary>Registers a command without argument; a second registration of the same id replaces the first.</summary>
    /// <param name="command">Command id.</param>
    /// <param name="handler">The action.</param>
    public void Add(string command, Func<Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        Add(command, _ => handler());
    }

    /// <summary>Registers a command with a numeric argument (<c>base:argument</c>).</summary>
    /// <param name="command">Base command id.</param>
    /// <param name="handler">The action receiving the argument (0 when the id has none).</param>
    public void Add(string command, Func<double, Task> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(handler);
        _handlers[command] = handler;
    }

    /// <summary>True when a handler exists for the id (with or without argument).</summary>
    /// <param name="commandId">Command id.</param>
    /// <returns>Whether it is handled.</returns>
    public bool Handles(string commandId) => Parse(commandId) is { } parsed && _handlers.ContainsKey(parsed.Command);

    /// <summary>Runs the handler of a command id.</summary>
    /// <param name="commandId">Command id, for example <c>mixer.seek:-5</c>.</param>
    /// <returns>True when a handler ran; false for unknown ids or an unreadable argument.</returns>
    public async Task<bool> DispatchAsync(string commandId)
    {
        if (Parse(commandId) is not { } parsed || !_handlers.TryGetValue(parsed.Command, out var handler))
        {
            return false;
        }

        await handler(parsed.Argument);
        return true;
    }

    private static (string Command, double Argument)? Parse(string? commandId)
    {
        if (string.IsNullOrEmpty(commandId))
        {
            return null;
        }

        var parts = commandId.Split(':', 2);
        if (parts.Length == 1)
        {
            return (parts[0], 0);
        }

        return double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var argument) ? (parts[0], argument) : null;
    }
}
