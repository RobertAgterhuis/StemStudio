using System.Collections.Concurrent;
using System.Net;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using StemStudio.TestDoubles.ClaudeKey;
using StemStudio.Web.Agent;
using StemStudio.Web.Api;
using StemStudio.Web.Components.Settings;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Settings;

/// <summary>
/// qa F8 [sec]: the Web side's own logs at Trace - the typed agent client registered through <c>IHttpClientFactory</c> as in
/// <c>Program.cs</c> (request/response logging of every handler), and the circuit rendering and saving the key section - never
/// hold the key in any of its search forms. Positive controls prove the capture ran at the most verbose level.
/// </summary>
public sealed class ClaudeKeyWebTraceLogTests : PageTestContext
{
    private readonly CapturingLoggerProvider _logs = new();

    [Fact]
    public async Task TypedAgentClient_NeverLogsTheKeyAtTrace()
    {
        using var handler = new SseHttpHandler();
        handler.Json(HttpStatusCode.OK, AgentData.Serialize(ClaudeKeyData.Stored()));
        handler.Problem(HttpStatusCode.UnprocessableContent,
            $$"""{"type":"https://stemstudio.local/problems/claude-key-invalid","title":"t","status":422,"detail":"{{ClaudeKeySentinels.Stored}}","reason":"prefix"}""");
        await using var services = ClientServices(handler);
        var client = services.GetRequiredService<IAgentApiClient>();

        await client.StoreClaudeKeyAsync(ClaudeKeySentinels.Stored, Xunit.TestContext.Current.CancellationToken);
        await Should.ThrowAsync<ApiException>(() => client.StoreClaudeKeyAsync(ClaudeKeySentinels.Stored, Xunit.TestContext.Current.CancellationToken));

        _logs.Entries.ShouldContain(entry => entry.Level == LogLevel.Trace && entry.Category.StartsWith("System.Net.Http.HttpClient", StringComparison.Ordinal));
        LogsShouldBeClean();
    }

    [Fact]
    public void Circuit_SavingTheKey_NeverLogsItAtTrace()
    {
        Services.AddSingleton<ILoggerFactory>(new LoggerFactory([_logs], new LoggerFilterOptions { MinLevel = LogLevel.Trace }));
        AgentApi.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(AgentData.Settings());
        AgentApi.StoreClaudeKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ClaudeKeyData.Stored());
        var cut = RenderAtLevel<SettingsPage>(ExperienceLevel.Advanced, _ => { });
        cut.WaitForElement("[data-testid=settings-agent-claude-key]");

        cut.Find("[data-testid=settings-agent-claude-key-input]").Input(ClaudeKeySentinels.Stored);
        cut.Find("[data-testid=settings-agent-claude-key-save]").Click();

        cut.WaitForAssertion(() => cut.Find("[data-testid=settings-agent-claude-key-status]").GetAttribute("data-state").ShouldBe("stored"));
        _logs.Entries.ShouldContain(entry => entry.Level <= LogLevel.Debug && entry.Category.StartsWith("Microsoft.AspNetCore.Components", StringComparison.Ordinal));
        LogsShouldBeClean();
    }

    private ServiceProvider ClientServices(SseHttpHandler handler)
    {
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace).AddProvider(_logs));
        services.AddHttpClient<IAgentApiClient, AgentApiClient>(client => client.BaseAddress = new Uri("http://api.test"))
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        return services.BuildServiceProvider();
    }

    // bUnit's own test renderer (category Bunit.*) logs dispatched event arguments at Debug, so it would show the typed value;
    // it is the test harness, not StemStudio or ASP.NET Core code, and is the only category left out.
    private void LogsShouldBeClean()
    {
        var production = _logs.Entries.Where(entry => !entry.Category.StartsWith("Bunit.", StringComparison.Ordinal));
        foreach (var line in production.Select(entry => entry.Message + entry.Exception).Concat(_logs.Scopes))
        {
            ClaudeKeySentinels.SearchForms(ClaudeKeySentinels.Stored)
                .ShouldAllBe(form => !line.Contains(form, StringComparison.Ordinal), "a search form of the sentinel was found in a log line or scope");
        }
    }

    private sealed record Entry(string Category, LogLevel Level, string Message, Exception? Exception);

    /// <summary>Records every entry and every scope's state.</summary>
    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<Entry> _entries = new();
        private readonly ConcurrentQueue<string> _scopes = new();

        public IReadOnlyList<Entry> Entries => [.. _entries];

        public IReadOnlyList<string> Scopes => [.. _scopes];

        public ILogger CreateLogger(string categoryName) => new Logger(categoryName, this);

        public void Dispose()
        {
        }

        private sealed class Logger(string category, CapturingLoggerProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
            {
                owner._scopes.Enqueue(state.ToString() ?? string.Empty);
                return null;
            }

            public bool IsEnabled(LogLevel logLevel) => true;

            // sig-exempt: interface-mandated signature (ILogger.Log)
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                owner._entries.Enqueue(new Entry(category, logLevel, formatter(state, exception), exception));
        }
    }
}
