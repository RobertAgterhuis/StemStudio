using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Agent.Ports;
using StemStudio.Agent.Runtime;
using StemStudio.Agent.Tools;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.Generation;

namespace StemStudio.Agent.Tests.Runtime;

/// <summary>023 T055: the +service lyricModel state of the tool selection (mirrors the 011 agent status).</summary>
public sealed class LyricModelStatusTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static IGenerationActions Generations()
    {
        var generations = Substitute.For<IGenerationActions>();
        generations.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(new GenerationStatusDto { State = GenerationAvailabilityDto.Ready, Voice = new VoiceServiceStatusDto { State = GenerationAvailabilityDto.Ready } });
        return generations;
    }

    [Fact]
    public async Task TheAgentStatus_DecidesTheLyricModelState()
    {
        var status = Substitute.For<IAgentStatusService>();
        status.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(
            Status(true, null), Status(false, "Ollama is not running."), Status(false, null));

        (await ServiceStatusReader.LyricModelAsync(status, Token)).Ready.ShouldBeTrue();
        (await ServiceStatusReader.LyricModelAsync(status, Token)).Reason.ShouldBe("Ollama is not running.");
        (await ServiceStatusReader.LyricModelAsync(status, Token)).Reason.ShouldBe(ServiceStatusReader.LyricModelUnknown);
        await Should.ThrowAsync<ArgumentNullException>(() => ServiceStatusReader.LyricModelAsync(null!, Token));
    }

    [Fact]
    public async Task TheReader_CachesTheLyricState_AndAFailedReadIsUnavailable()
    {
        var calls = 0;
        var ready = new ServiceStatusReader(Generations(), TimeProvider.System) { LyricModel = _ => { calls++; return Task.FromResult(ServiceAvailability.Available); } };
        var failing = new ServiceStatusReader(Generations(), TimeProvider.System) { LyricModel = _ => throw new HttpRequestException("down") };
        var none = new ServiceStatusReader(Generations(), TimeProvider.System);
        var page = new PageContextDto { Page = "lyrics" };

        (await ready.ContextAsync(page, Token)).LyricModel.Ready.ShouldBeTrue();
        (await ready.ContextAsync(page, Token)).LyricModel.Ready.ShouldBeTrue();
        ready.Peek(page).LyricModel.Ready.ShouldBeTrue();
        calls.ShouldBe(1);
        (await failing.ContextAsync(page, Token)).LyricModel.Reason.ShouldBe(ServiceStatusReader.LyricModelUnknown);
        (await none.ContextAsync(page, Token)).LyricModel.ShouldBe(ServiceAvailability.Unknown);
        none.Peek(page).LyricModel.ShouldBe(ServiceAvailability.Unknown);
    }

    [Fact]
    public async Task AFailedGenerationRead_KeepsTheLyricState()
    {
        var generations = Substitute.For<IGenerationActions>();
        generations.GetStatusAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new AgentActionException());
        var reader = new ServiceStatusReader(generations, TimeProvider.System) { LyricModel = _ => Task.FromResult(ServiceAvailability.Available) };

        var context = await reader.ContextAsync(new PageContextDto { Page = "lyrics" }, Token);

        context.Generation.Ready.ShouldBeFalse();
        context.LyricModel.Ready.ShouldBeTrue();
    }

    private static AgentStatusDto Status(bool ready, string? reason) => new()
    {
        Backend = AgentBackendDto.Ollama,
        Ready = ready,
        Reason = reason,
        UsingFallback = false,
        Ollama = new OllamaStatusDto { Reachable = ready, DefaultPresent = ready, FallbackPresent = ready },
        Claude = new ClaudeStatusDto { KeyConfigured = false, NoticeAcknowledged = false, Model = ClaudeModelDto.ClaudeOpus55 },
    };
}
