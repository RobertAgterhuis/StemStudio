namespace Samples;

// Simulates tests/StemStudio.Api.Tests/TestSupport/ListLoggerProvider.cs (ILogger.Log is fixed).
public sealed class ListLogger : ILogger
{
    // sig-exempt: interface-mandated signature (ILogger.Log)
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Sink(formatter(state, exception));

    // sig-exempt:
    public void NoReason(int a, int b, int c, int d, int e) => Sink(a);

    // sig-exempt: kept for symmetry
    public void NotNeeded(int a) => Sink(a);

    // sig-exempt: too far away

    public void TooFar(int a, int b, int c, int d, int e) => Sink(a);

    public void Sink(object value) { }
}
