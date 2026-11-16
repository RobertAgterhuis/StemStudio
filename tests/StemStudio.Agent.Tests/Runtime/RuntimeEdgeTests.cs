using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Agent.Ports;
using StemStudio.Agent.Runtime;
using StemStudio.Agent.Tests.Fakes;
using StemStudio.Agent.Tools;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.Generation;
using static StemStudio.Agent.Tests.Fakes.TestTools;

namespace StemStudio.Agent.Tests.Runtime;

/// <summary>Edge cases of the runtime: service states, navigation, missing records.</summary>
public sealed class RuntimeEdgeTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ServiceStatus_MapsUnavailableServices_AndCachesForTenSeconds()
    {
        var generations = Substitute.For<IGenerationActions>();
        generations.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(new GenerationStatusDto
        {
            State = GenerationAvailabilityDto.NotInstalled,
            Reason = "Generator not installed",
            Voice = new VoiceServiceStatusDto { State = GenerationAvailabilityDto.Unavailable },
        });
        var time = new FakeTimeProvider(RunnerHarness.Start);
        var reader = new ServiceStatusReader(generations, time);

        var first = await reader.ContextAsync(new PageContextDto { Page = " Styles " }, Token);
        await reader.ContextAsync(new PageContextDto { Page = "styles" }, Token);
        time.Advance(TimeSpan.FromSeconds(11));
        await reader.ContextAsync(new PageContextDto { Page = "styles" }, Token);

        (first.Page, first.Generation.Ready, first.Generation.Reason, first.Voice.Reason).ShouldBe(("styles", false, "Generator not installed", "The generator is not available."));
        await generations.Received(2).GetStatusAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ServiceStatus_FailedRead_IsUnknown()
    {
        var generations = Substitute.For<IGenerationActions>();
        generations.GetStatusAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new AgentActionException("down"));

        var context = await new ServiceStatusReader(generations, TimeProvider.System).ContextAsync(new PageContextDto { Page = "tracks" }, Token);

        (context.Generation, context.Voice).ShouldBe((ServiceAvailability.Unknown, ServiceAvailability.Unknown));
    }

    [Fact]
    public void ContextLine_IncludesTheOpenMix()
    {
        var track = Guid.NewGuid();
        var mix = Guid.NewGuid();

        SystemPrompt.ContextLine(new PageContextDto { Page = "mixer", TrackId = track, MixId = mix }).ShouldBe($"Current page: mixer; open track: {track}; open mix: {mix}.");
        SystemPrompt.ContextLine(new PageContextDto { Page = "tracks" }).ShouldBe("Current page: tracks.");
    }

    [Fact]
    public async Task Navigate_ChangesThePageOfTheNextTurn()
    {
        var navigate = Ui("navigate", ToolRiskDto.Read) with { Schema = ToolSchema.Of(Param.Choice("page", "styles", "tracks")) };
        var harness = RunnerHarness.Scripted([navigate, Ui("open_track", ToolRiskDto.Read)], Turn(Call("navigate", """{"page":"styles"}""")), Turn(Call("open_track")), Say("ok"));

        await harness.ExecuteAsync();

        harness.Script.Requests[1].Messages.ShouldContain(message => message.Text == "Current page: styles.");
        harness.Script.Requests[2].Messages.ShouldContain(message => message.Text == "Current page: track.");
    }

    [Fact]
    public async Task RunWithoutItsCommand_IsLeftAlone()
    {
        var harness = RunnerHarness.Scripted([Echo()], Say("ok"));
        await harness.Runs.CreateRunAsync(harness.Run, Token);

        await harness.Runner().ExecuteAsync(harness.Run.Id, Token);

        harness.Events.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task MissingConversation_StillEndsTheRun()
    {
        var harness = RunnerHarness.Scripted([Echo()], Say("ok"));
        await harness.Conversations.AppendMessageAsync(harness.Command, Token);
        await harness.Runs.CreateRunAsync(harness.Run, Token);

        await harness.Runner().ExecuteAsync(harness.Run.Id, Token);

        (await harness.CurrentRunAsync())!.Status.ShouldBe(AgentRunStatusDto.Completed);
    }

    [Fact]
    public async Task EmptyAnswer_IsStoredAsAnEmptyMessage_AndTextIsCapped()
    {
        var harness = RunnerHarness.Scripted([Echo()], Say(new string('a', 16_100)));

        await harness.ExecuteAsync();

        harness.Conversations.Messages[^1].Text.Length.ShouldBe(AgentContractLimits.AssistantMessageMaxLength);
    }
}
