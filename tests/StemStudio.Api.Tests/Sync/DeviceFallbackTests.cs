using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Detail;
using StemStudio.Api.Sync;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Tests.Worker;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Tracks;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Tests.Sync;

/// <summary>CPU fallbacks and devices of older workers end to end through the API (feature 003, T036; FR-004, FR-006).</summary>
public sealed class DeviceFallbackTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public DeviceFallbackTests() => _client = _factory.CreateClient();

    private FakeWorkerClient Worker => _factory.Worker;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task FallbackReasonOfTheRunningDetailJobExample_ReachesTheDetailPhaseDto()
    {
        var (exampleClient, _) = WorkerClientTests.Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, "detail-running-job"));
        var example = (await exampleClient.GetDetailJobAsync(SyncTestData.DetailJobId, Ct))!;
        var track = await _factory.SeedAsync(SyncTestData.DetailQueued());
        Worker.OnGetDetailJob = _ => Task.FromResult<WorkerDetailJob?>(example);

        await SyncDetailAsync();

        var device = (await GetTrackAsync(track.Id)).Detail.Device!;
        (device.Kind, device.CpuFallback).ShouldBe((ProcessingDeviceKindDto.Gpu, true));
        device.FallbackReason.ShouldBe(example.Device!.FallbackReason);
    }

    [Fact]
    public async Task A110WorkerJobWithoutDevice_KeepsTheDeviceNull()
    {
        var track = await _factory.SeedAsync(SyncTestData.UrlTrack());
        Worker.OnGetJob = id => Task.FromResult<WorkerJob?>(FakeWorkerClient.Job(id, WorkerJobState.Separating, 30));

        await SyncTracksAsync();

        var dto = await GetTrackAsync(track.Id);
        (dto.Status, dto.Device).ShouldBe((TrackStatusDto.Separating, null));
    }

    [Fact]
    public async Task RetryingAFailedTrack_ClearsItsDevice()
    {
        var track = SyncTestData.UrlTrack();
        track.ApplyDevice(new JobDevice(ProcessingDeviceKind.Gpu, "NVIDIA GeForce RTX 5070 Ti", false, null), SyncTestData.T0);
        track.Fail("The GPU test kernel failed.", SyncTestData.T0);
        await _factory.SeedAsync(track);

        using var response = await _client.PostAsync(new Uri(ApiRoutes.RetryTrack(track.Id), UriKind.Relative), null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await response.Content.ReadFromJsonAsync<TrackDto>(ApiFactory.Json, Ct))!.Device.ShouldBeNull();
        (await _factory.FindTrackAsync(track.Id))!.Device.ShouldBeNull();
    }

    private async Task<TrackDto> GetTrackAsync(Guid id) =>
        (await _client.GetFromJsonAsync<TrackDto>(new Uri(ApiRoutes.Track(id), UriKind.Relative), ApiFactory.Json, Ct))!;

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
