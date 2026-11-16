using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Agent.Ports;
using StemStudio.Agent.Tests.Fakes;
using StemStudio.Agent.Tests.Fixtures;
using StemStudio.Agent.Tools.Providers;
using StemStudio.Contracts;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.Generation;
using static StemStudio.Agent.Tests.Fakes.TestTools;
using static StemStudio.Agent.Tests.Tools.Providers.ProviderTestKit;

namespace StemStudio.Agent.Tests.Tools.Providers;

/// <summary>
/// Defense in depth for <c>convert_take_voice</c> (FR-025, ADR-019): the agent checks that the profile exists and its consent is
/// current before an approval card or a job; the backend's 409 stays authoritative. Messages never carry personal data.
/// </summary>
public sealed class VoiceConsentTests
{
    private static readonly Guid GenerationId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000005");
    private static readonly Guid ProfileId = Guid.Parse("ffffffff-0000-0000-0000-000000000006");
    private readonly IStemStudioActions _actions = Actions();

    private static string Args => $$"""{"generationId":"{{GenerationId}}","index":1,"profileId":"{{ProfileId}}"}""";

    private static VoiceProfileSummary Profile(string version) => new() { ProfileId = ProfileId, DisplayName = "Anna private name", ConsentStatementVersion = version, DurationSeconds = 30 };

    private static GenerationDto Conversion() => new()
    {
        Id = Guid.NewGuid(),
        Kind = GenerationKindDto.VoiceConversion,
        Status = GenerationStateDto.Queued,
        Progress = 0,
        CreatedAt = Dtos.At,
        UpdatedAt = Dtos.At,
    };

    public static TheoryData<string> Refused => new() { "none", "0-outdated" };

    [Theory]
    [MemberData(nameof(Refused))]
    public async Task UnknownProfileOrStaleConsent_IsRefusedByPreconditionEstimatorAndHandler(string version)
    {
        _actions.Voices.ListVoiceProfilesAsync(Arg.Any<CancellationToken>()).Returns(version == "none" ? [] : [Profile(version)]);
        var tool = Tool(new SongToolProvider(), "convert_take_voice");
        var call = Invocation(tool, Args, _actions);

        var pre = await Should.ThrowAsync<AgentActionException>(() => tool.Precondition!(call, TestContext.Current.CancellationToken));
        var estimate = await Should.ThrowAsync<AgentActionException>(() => tool.Estimator!(call, TestContext.Current.CancellationToken));
        var handler = await Should.ThrowAsync<AgentActionException>(() => tool.Handler!(call, TestContext.Current.CancellationToken));

        pre.Message.ShouldBe(version == "none" ? SongToolProvider.UnknownProfile : SongToolProvider.ConsentNotCurrent);
        (estimate.Message, handler.Message).ShouldBe((pre.Message, pre.Message));
        pre.Message.ShouldNotContain("Anna");
        await _actions.Voices.DidNotReceive().CreateVoiceConversionAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<VoiceConversionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CurrentConsent_Proceeds()
    {
        _actions.Voices.ListVoiceProfilesAsync(Arg.Any<CancellationToken>()).Returns([Profile(ContractLimits.ConsentStatementVersion)]);
        _actions.Voices.CreateVoiceConversionAsync(GenerationId, 1, Arg.Any<VoiceConversionRequest>(), Arg.Any<CancellationToken>()).Returns(Conversion());
        var tool = Tool(new SongToolProvider(), "convert_take_voice");

        await tool.Precondition!(Invocation(tool, Args, _actions), TestContext.Current.CancellationToken);
        await InvokeAsync(tool, Args, _actions);

        await _actions.Voices.Received(1).CreateVoiceConversionAsync(GenerationId, 1, Arg.Any<VoiceConversionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Runner_StaleConsent_FailsTheStepWithoutACard()
    {
        var harness = new RunnerHarness(new StemStudio.Agent.Backends.Fakes.ScriptedChatClient([Turn(Call("convert_take_voice", Args)), Say("ok")]), [.. new SongToolProvider().GetTools()], "Sing it with my voice", "voices");
        harness.Actions.Voices.ListVoiceProfilesAsync(Arg.Any<CancellationToken>()).Returns([Profile("0-outdated")]);

        await harness.ExecuteAsync();

        var step = harness.Runs.Steps.Single();
        (step.Status, step.Error).ShouldBe((AgentStepStatusDto.Failed, SongToolProvider.ConsentNotCurrent));
        harness.Events.Types.ShouldNotContain(AgentEventTypes.ApprovalRequested);
        await harness.Actions.Voices.DidNotReceive().CreateVoiceConversionAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<VoiceConversionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Runner_Backend409_IsStillAFailedStep()
    {
        var harness = new RunnerHarness(new StemStudio.Agent.Backends.Fakes.ScriptedChatClient([Turn(Call("convert_take_voice", Args)), Say("ok")]), [.. new SongToolProvider().GetTools()], "Sing it with my voice", "voices");
        harness.Actions.Voices.ListVoiceProfilesAsync(Arg.Any<CancellationToken>()).Returns([Profile(ContractLimits.ConsentStatementVersion)]);
        harness.Actions.Voices.CreateVoiceConversionAsync(GenerationId, 1, Arg.Any<VoiceConversionRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AgentActionException(409, "consent-outdated", "Consent outdated"));
        await harness.SeedAsync();
        var service = new StemStudio.Agent.Runtime.AgentRunService(harness.Services());
        var execution = service.ExecuteAsync(harness.Run.Id, TestContext.Current.CancellationToken);

        var card = await harness.Events.WaitForAsync(AgentEventTypes.ApprovalRequested);
        var payload = System.Text.Json.JsonDocument.Parse(card.Payload).RootElement;
        await service.DecideAsync(harness.Run.Id, payload.GetProperty("stepId").GetGuid(), new DecideApprovalRequest { Decision = ApprovalDecisionDto.Approve, ArgumentsHash = payload.GetProperty("argumentsHash").GetString()! }, TestContext.Current.CancellationToken);
        await execution.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        var step = harness.Runs.Steps.Single();
        (step.Status, step.Error).ShouldBe((AgentStepStatusDto.Failed, "Renew the consent for this voice on the Voices page"));
    }
}
