using Microsoft.Extensions.Logging;

namespace StemStudio.Agent.Tests.Fakes;

/// <summary>Captures log entries (level and formatted message including the exception text).</summary>
public sealed class CapturingLogger : ILogger
{
    private readonly List<(LogLevel Level, string Text)> _entries = [];
    private readonly Lock _gate = new();

    public IReadOnlyList<(LogLevel Level, string Text)> Entries
    {
        get
        {
            lock (_gate)
            {
                return [.. _entries];
            }
        }
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (_gate)
        {
            _entries.Add((logLevel, formatter(state, exception) + " " + exception));
        }
    }
}
