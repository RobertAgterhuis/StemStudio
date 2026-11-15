using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StemStudio.Api.Detail;
using StemStudio.Api.Sync;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Tracks;
using StemStudio.Domain.Tracks;
using StemStudio.Domain.Tracks.Detail;

namespace StemStudio.Api.Tests.Sync;

/// <summary>Worker job devices copied to tracks and detail phases and served (feature 003, T029; FR-006).</summary>
public sealed class DeviceSyncTests : IDisposable
{
    private const string Gpu = "NVIDIA GeForce RTX 5070 Ti";
    private const string Oom = "The GPU ran out of memory during Mega 53-stem; the step was repeated on the CPU.";

    private static readonly WorkerJobDevice OnGpu = new(ProcessingDeviceKind.Gpu, Gpu, false, null);
    private static readonly WorkerJobDevice WithFallback = new(ProcessingDeviceKind.Gpu, Gpu, true, Oom);
    private static readonly WorkerJobDevice Invalid = new(ProcessingDeviceKind.Cpu, Gpu, false, null);

    private readonly ApiFactory _factory = new();

    public DeviceSyncTests() => _factory.CreateClient().Dispose();

    private FakeWorkerClient Worker => _factory.Worker;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task EveryStandardPoll_CopiesTheDevice()
    {
        var track = await _factory.SeedAsync(SyncTestData.UrlTrack());
        ReportJob(WorkerJobState.Separating, OnGpu);
        await SyncTracksAsync();
        (await _factory.FindTrackAsync(track.Id))!.Device.ShouldBe(new JobDevice(ProcessingDeviceKind.Gpu, Gpu, false, null));

        ReportJob(WorkerJobState.Analysing, WithFallback);
        await SyncTracksAsync();

        (await _factory.FindTrackAsync(track.Id))!.Device.ShouldBe(new JobDevice(ProcessingDeviceKind.Gpu, Gpu, true, Oom));
    }

    [Fact]
    public async Task TheCompletedJobsDevice_IsKeptOnTheCompletedTrack()
    {
        var track = await _factory.SeedAsync(SyncTestData.UrlTrack());
        ReportJob(WorkerJobState.Completed, WithFallback);

        await SyncTracksAsync();

        var synced = (await _factory.FindTrackAsync(track.Id))!;
        (synced.Status, synced.Device!.FallbackReason).ShouldBe((TrackStatus.Completed, Oom));
    }

    [Fact]
    public async Task InvalidWorkerDevice_IsLoggedAndIgnoredWithoutFailingTheTrack()
    {
        var track = await _factory.SeedAsync(SyncTestData.UrlTrack());
        ReportJob(WorkerJobState.Separating, OnGpu);
        await SyncTracksAsync();

        ReportJob(WorkerJobState.Analysing, Invalid);
        await SyncTracksAsync();

        var synced = (await _factory.FindTrackAsync(track.Id))!;
        (synced.Status, synced.Device!.Name).ShouldBe((TrackStatus.Analysing, Gpu));
        _factory.Logs.Entries.ShouldContain(entry => entry.Level == LogLevel.Warning && entry.Message.Contains("device", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EveryDetailPoll_CopiesTheDeviceToTheDetailPhase()
    {
        var track = await _factory.SeedAsync(SyncTestData.DetailQueued());
        ReportDetailJob(WorkerDetailJobState.Running, OnGpu);
        await SyncDetailAsync();
        (await DetailPhaseAsync(track)).Device!.CpuFallback.ShouldBeFalse();

        ReportDetailJob(WorkerDetailJobState.Completed, WithFallback);
        await SyncDetailAsync();

        var phase = await DetailPhaseAsync(track);
        (phase.Status, phase.Device!.FallbackReason).ShouldBe((DetailPhaseStatus.Completed, Oom));
        (await _factory.FindTrackAsync(track.Id))!.Device.ShouldBeNull();
    }

    [Fact]
    public async Task InvalidWorkerDetailDevice_IsIgnored()
    {
        var track = await _factory.SeedAsync(SyncTestData.DetailQueued());
        ReportDetailJob(WorkerDetailJobState.Running, Invalid);

        await SyncDetailAsync();

        var phase = await DetailPhaseAsync(track);
        (phase.Status, phase.Device).ShouldBe((DetailPhaseStatus.Running, null));
    }

    [Fact]
    public async Task ServedTrack_CarriesBothDevicesAndMatchesTheContract()
    {
        var track = await _factory.SeedAsync(SyncTestData.DetailQueued());
        await _factory.UpdateTrackAsync(track.Id, t =>
        {
            t.ApplyDetailProgress(new DetailProgress(DetailPhaseStatus.Running, "Splitting drums (1 of 3)", "drums", 12), SyncTestData.T0);
            t.ApplyDetailDevice(new JobDevice(ProcessingDeviceKind.Gpu, Gpu, true, Oom), SyncTestData.T0);
        });

        var dto = await GetServedTrackAsync(track.Id);

        dto.Detail.Device.ShouldBe(new JobDeviceDto(ProcessingDeviceKindDto.Gpu, Gpu, true, Oom));
        dto.Device.ShouldBeNull();
    }

    [Fact]
    public async Task ServedTrack_WithAStandardCpuDeviceMatchesTheContract()
    {
        var track = SyncTestData.UrlTrack();
        track.ApplyDevice(new JobDevice(ProcessingDeviceKind.Cpu, null, false, null), SyncTestData.T0);
        await _factory.SeedAsync(track);

        var dto = await GetServedTrackAsync(track.Id);

        (dto.Device!.DisplayText(), dto.Device.CpuFallback).ShouldBe(("CPU", false));
    }

    /// <summary>GET /api/tracks/{id}, asserted against the backend 1.3.0 contract.</summary>
    private async Task<TrackDto> GetServedTrackAsync(Guid trackId)
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(new Uri(ApiRoutes.Track(trackId), UriKind.Relative), Ct);
        await ContractAssert.MatchesAsync(response, "getTrack");
        return (await response.Content.ReadFromJsonAsync<TrackDto>(ApiFactory.Json, Ct))!;
    }

    private void ReportJob(WorkerJobState state, WorkerJobDevice device) =>
        Worker.OnGetJob = id => Task.FromResult<WorkerJob?>(FakeWorkerClient.Job(id, state, 50) with { Device = device });

    private void ReportDetailJob(WorkerDetailJobState state, WorkerJobDevice device) =>
        Worker.OnGetDetailJob = id => Task.FromResult<WorkerDetailJob?>(FakeWorkerClient.DetailJob(id, state, 40, "drums") with { Device = device });

    private async Task<DetailPhase> DetailPhaseAsync(Track track) => (await _factory.FindTrackAsync(track.Id))!.DetailPhase!;

    private Task<bool> SyncTracksAsync() => _factory.WithScopeAsync(async sp =>
    {
        await sp.GetRequiredService<ITrackSynchronizer>().SyncAllAsync(Ct);
        return true;
    });

    private Task<bool> SyncDetailAsync() => _factory.WithScopeAsync(async sp =>
    {
        await sp.GetRequiredService<DetailSynchronizer>().SyncAllAsync(Ct);
        return true;
    });
}
