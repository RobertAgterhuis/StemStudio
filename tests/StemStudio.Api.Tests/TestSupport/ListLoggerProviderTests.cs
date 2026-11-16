using Microsoft.Extensions.Logging;

namespace StemStudio.Api.Tests.TestSupport;

/// <summary>The capturing logger records log scopes too, so leak searches also cover scope state (quality-gate R3).</summary>
public sealed class ListLoggerProviderTests
{
    [Fact]
    public void BeginScope_RecordsTheFormattedStateAndItsValues()
    {
        using var provider = new ListLoggerProvider();
        var logger = provider.CreateLogger("Test.Category");

        using (logger.BeginScope("Run {RunId}", "r-1"))
        using (logger.BeginScope(new Dictionary<string, object?> { ["Peer"] = "192.168.1.20" }))
        {
            logger.LogTrace("inside");
        }

        provider.Scopes.ShouldBe(["Run r-1", "Peer=192.168.1.20"], ignoreOrder: true);
        provider.Entries.ShouldHaveSingleItem().Level.ShouldBe(LogLevel.Trace);
    }
}
