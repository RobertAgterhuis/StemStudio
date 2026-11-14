using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace StemStudio.Api.Tests.TestSupport;

/// <summary>Captures log entries for assertions.</summary>
internal sealed class ListLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public IReadOnlyList<LogEntry> Entries => _entries.ToArray();

    public IEnumerable<LogEntry> For(string categorySuffix) =>
        Entries.Where(entry => entry.Category.EndsWith(categorySuffix, StringComparison.Ordinal));

    public ILogger CreateLogger(string categoryName) => new ListLogger(categoryName, _entries);

    public void Dispose()
    {
    }

    private sealed class ListLogger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            entries.Enqueue(new LogEntry(category, logLevel, formatter(state, exception), exception));
    }
}

internal sealed record LogEntry(string Category, LogLevel Level, string Message, Exception? Exception);
