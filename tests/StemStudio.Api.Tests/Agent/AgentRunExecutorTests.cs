using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Agent.Ports;
using StemStudio.Api.Agent;

namespace StemStudio.Api.Tests.Agent;

/// <summary>
/// Flake round 022/023: a failing end-of-run bookkeeping step (for example a database that stays locked) must not end the
/// executor. Before the fix the exception left <c>ExecuteAsync</c>, which stops the whole host (BackgroundService default
/// <c>StopHost</c>) and disposed the services under the tests that were still running.
/// </summary>
public sealed class AgentRunExecutorTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task FailingBookkeeping_IsLogged_AndTheNextRunStillExecutes()
    {
        var runs = Substitute.For<IRunStore>();
        runs.GetRunAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("database is locked"));
        var agent = Substitute.For<IAgentRunService>();
        var second = Guid.NewGuid();
        var executed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        agent.ExecuteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            if (call.Arg<Guid>() == second)
            {
                executed.TrySetResult();
                return Task.CompletedTask;
            }

            throw new InvalidOperationException("boom");
        });
        await using var services = new ServiceCollection().AddSingleton(agent).BuildServiceProvider();
        var lifecycle = new AgentRunLifecycle(runs, Substitute.For<IConversationStore>(), Substitute.For<IRunEventSink>(), new FakeTimeProvider());
        using var executor = new AgentRunExecutor(services.GetRequiredService<IServiceScopeFactory>(), lifecycle, NullLogger<AgentRunExecutor>.Instance);
        await executor.StartAsync(Ct);

        executor.Enqueue(Guid.NewGuid());
        executor.Enqueue(second);

        await executed.Task.WaitAsync(TimeSpan.FromSeconds(30), Ct);
        executor.ExecuteTask!.IsCompleted.ShouldBeFalse();
        await executor.StopAsync(Ct);
    }
}
