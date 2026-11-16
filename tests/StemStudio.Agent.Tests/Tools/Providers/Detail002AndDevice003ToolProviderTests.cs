using NSubstitute;
using StemStudio.Agent.Ports;
using StemStudio.Agent.Tests.Fixtures;
using StemStudio.Agent.Tools.Providers;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.Health;
using StemStudio.Contracts.Tracks;
using static StemStudio.Agent.Tests.Tools.Providers.ProviderTestKit;

namespace StemStudio.Agent.Tests.Tools.Providers;

/// <summary>T028: detail separation (002) and processing device (003) tools.</summary>
public sealed class Detail002AndDevice003ToolProviderTests
{
    private static readonly Guid TrackId = Guid.NewGuid();
    private readonly IStemStudioActions _actions = Actions();

    private static ProcessingDeviceDto Gpu => new() { Reachable = true, Kind = ProcessingDeviceKindDto.Gpu, Name = "RTX 4070" };

    private static ProcessingDeviceDto Cpu => new() { Reachable = true, Kind = ProcessingDeviceKindDto.Cpu };

    [Fact]
    public void Tools_MatchTheCatalogue()
    {
        MatchesCatalogue(new Detail002ToolProvider(), "002");
        MatchesCatalogue(new Device003ToolProvider(), "003");
        Tool(new Detail002ToolProvider(), "start_detail_separation").TargetArgument.ShouldBe("trackId");
    }

    [Theory]
    [InlineData(true, 240, 4.0, "GPU (RTX 4070)")]
    [InlineData(false, 240, 30.0, "CPU")]
    [InlineData(true, 480, 8.0, "GPU (RTX 4070)")]
    public async Task StartDetail_EstimatesFromTheDeviceAndTheDuration(bool gpu, double seconds, double minutes, string device)
    {
        _actions.Tracks.GetDeviceAsync(Arg.Any<CancellationToken>()).Returns(gpu ? Gpu : Cpu);
        _actions.Tracks.GetTrackAsync(TrackId, Arg.Any<CancellationToken>()).Returns(Dtos.Track(TrackId) with { DurationSeconds = seconds });
        var tool = Tool(new Detail002ToolProvider(), "start_detail_separation");

        var estimate = await tool.Estimator!(Invocation(tool, $$"""{"trackId":"{{TrackId}}"}""", _actions), TestContext.Current.CancellationToken);

        (estimate.EstimatedMinutes, estimate.Device).ShouldBe((minutes, device));
        estimate.Effect.ShouldBe("Detail separation of a track (Hero of the Day)");
    }

    [Fact]
    public async Task StartDetail_QueuesThePhase_WithJobEstimateAndCancelUndo()
    {
        _actions.Tracks.GetDeviceAsync(Arg.Any<CancellationToken>()).Returns(Gpu);
        _actions.Tracks.GetTrackAsync(TrackId, Arg.Any<CancellationToken>()).Returns(Dtos.Track(TrackId));
        _actions.Tracks.StartDetailAsync(TrackId, Arg.Any<CancellationToken>()).Returns(Dtos.Track(TrackId).WithDetail(DetailPhaseStatusDto.Queued));

        var result = await InvokeAsync(Tool(new Detail002ToolProvider(), "start_detail_separation"), $$"""{"trackId":"{{TrackId}}"}""", _actions);

        result.Projection["detailStatus"]!.GetValue<string>().ShouldBe("queued");
        result.Projection["device"]!.GetValue<string>().ShouldBe("GPU (RTX 4070)");
        result.Projection["jobRef"]!["phase"]!.GetValue<string>().ShouldBe("detail");
        (result.Job!.Phase, result.Job.TrackId, result.EstimatedMinutes).ShouldBe((JobPhaseDto.Detail, TrackId, 4.3));
        (result.Undo!.Tool, result.Undo.Arguments).ShouldBe(("cancel_detail_separation", $$"""{"trackId":"{{TrackId}}"}"""));
    }

    [Fact]
    public async Task CancelDetail_ReportsTheStatus()
    {
        _actions.Tracks.CancelDetailAsync(TrackId, Arg.Any<CancellationToken>()).Returns(Dtos.Track(TrackId).WithDetail(DetailPhaseStatusDto.Cancelled));

        var result = await InvokeAsync(Tool(new Detail002ToolProvider(), "cancel_detail_separation"), $$"""{"trackId":"{{TrackId}}"}""", _actions);

        Json(result.Projection).ShouldBe("""{"detailStatus":"cancelled"}""");
    }

    [Fact]
    public async Task GetProcessingDevice_ProjectsTheDevice()
    {
        _actions.Tracks.GetDeviceAsync(Arg.Any<CancellationToken>()).Returns(Gpu with { TotalMemoryMiB = 12000 });

        var result = await InvokeAsync(Tool(new Device003ToolProvider(), "get_processing_device"), "{}", _actions);

        Json(result.Projection).ShouldBe("""{"reachable":true,"kind":"gpu","name":"RTX 4070","reason":null}""");
    }
}
