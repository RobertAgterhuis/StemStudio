using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace StemStudio.Api.Tests.TestSupport;

/// <summary>Captures log entries for assertions.</summary>
internal sealed class ListLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    private readonly ConcurrentQueue<string> _scopes = new();

    public IReadOnlyList<LogEntry> Entries => _entries.ToArray();

    /// <summary>The state of every scope begun on a logger of this provider, as text (leak searches cover it, quality-gate R3).</summary>
    public IReadOnlyList<string> Scopes => _scopes.ToArray();

    public IEnumerable<LogEntry> For(string categorySuffix) =>
        Entries.Where(entry => entry.Category.EndsWith(categorySuffix, StringComparison.Ordinal));

    public ILogger CreateLogger(string categoryName) => new ListLogger(categoryName, _entries, _scopes);

    public void Dispose()
    {
    }

    private sealed class ListLogger(string category, ConcurrentQueue<LogEntry> entries, ConcurrentQueue<string> scopes) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            scopes.Enqueue(ScopeText(state));
            return null;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        // sig-exempt: interface-mandated signature (ILogger.Log)
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            entries.Enqueue(new LogEntry(category, logLevel, formatter(state, exception), exception));

        // A formatted scope ("Run {RunId}") prints its message; any other key/value state prints its pairs.
        private static string ScopeText(object state)
        {
            var text = state.ToString() ?? string.Empty;
            return state is IEnumerable<KeyValuePair<string, object?>> pairs && text == state.GetType().ToString()
                ? string.Join(", ", pairs.Select(pair => $"{pair.Key}={pair.Value}"))
                : text;
        }
    }
}

internal sealed record LogEntry(string Category, LogLevel Level, string Message, Exception? Exception);
