using Microsoft.Extensions.Time.Testing;
using StemStudio.Agent.Ports;
using StemStudio.Agent.Runtime;
using StemStudio.Agent.Tests.Fakes;

namespace StemStudio.Agent.Tests.Runtime;

/// <summary>The in-memory UI tool round trip (contracts/DELTA.md section 4): wait, complete once, time out, cancel.</summary>
public sealed class UiToolGatewayTests
{
    private readonly FakeTimeProvider _time = new(RunnerHarness.Start);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static UiToolRequest Request(Guid? run = null, Guid? step = null) => new()
    {
        RunId = run ?? Guid.NewGuid(),
        StepId = step ?? Guid.NewGuid(),
        Tool = "mixer_set_channel",
        Arguments = "{}",
        Timeout = TimeSpan.FromSeconds(15),
    };

    [Fact]
    public async Task TheAnswerCompletesTheWait_Once()
    {
        var gateway = new UiToolGateway(_time);
        var request = Request();

        var pending = gateway.RequestAsync(request, Token);
        var first = gateway.TryComplete(request.RunId, request.StepId, new UiToolResult { Ok = true, Result = """{"gain":0.5}""" });
        var second = gateway.TryComplete(request.RunId, request.StepId, new UiToolResult { Ok = true });

        (first, second).ShouldBe((true, false));
        (await pending).Result.ShouldBe("""{"gain":0.5}""");
    }

    [Fact]
    public void AnAnswerNobodyWaitsFor_IsNotExpected()
    {
        new UiToolGateway(_time).TryComplete(Guid.NewGuid(), Guid.NewGuid(), new UiToolResult { Ok = true }).ShouldBeFalse();
    }

    [Fact]
    public async Task AnAnswerForAnotherRun_IsNotExpected()
    {
        var gateway = new UiToolGateway(_time);
        var request = Request();
        var pending = gateway.RequestAsync(request, Token);

        gateway.TryComplete(Guid.NewGuid(), request.StepId, new UiToolResult { Ok = true }).ShouldBeFalse();
        gateway.TryComplete(request.RunId, request.StepId, new UiToolResult { Ok = true }).ShouldBeTrue();
        await pending;
    }

    [Fact]
    public async Task NoAnswer_TimesOutWithAReadableError_AndLateAnswersAreRejected()
    {
        var gateway = new UiToolGateway(_time);
        var request = Request();

        var pending = gateway.RequestAsync(request, Token);
        _time.Advance(TimeSpan.FromSeconds(16));
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(10), Token);

        (result.Ok, result.Error).ShouldBe((false, "The page did not respond within 15 seconds."));
        gateway.TryComplete(request.RunId, request.StepId, new UiToolResult { Ok = true }).ShouldBeFalse();
    }

    [Fact]
    public async Task Cancellation_EndsTheWait()
    {
        var gateway = new UiToolGateway(_time);
        var request = Request();
        using var cancel = new CancellationTokenSource();

        var pending = gateway.RequestAsync(request, cancel.Token);
        await cancel.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => pending);
        gateway.TryComplete(request.RunId, request.StepId, new UiToolResult { Ok = true }).ShouldBeFalse();
    }

    [Fact]
    public async Task AnswersBeforeTheEventIsHandled_AreNotLost()
    {
        var harness = RunnerHarness.Scripted([TestTools.Ui("mixer_mute")], TestTools.Turn(TestTools.Call("mixer_mute", "{}")), TestTools.Say("ok"));
        var gateway = new UiToolGateway(harness.Time);
        harness.Events.OnPublish = item =>
        {
            if (item.Type == Contracts.Agent.AgentEventTypes.UiRequested)
            {
                var step = System.Text.Json.JsonDocument.Parse(item.Payload).RootElement.GetProperty("stepId").GetGuid();
                gateway.TryComplete(item.RunId, step, new UiToolResult { Ok = true, Result = "{}" }).ShouldBeTrue();
            }
        };
        harness.UseUi(gateway);

        var run = await harness.ExecuteAsync();

        run.Status.ShouldBe(Contracts.Agent.AgentRunStatusDto.Completed);
    }
}
