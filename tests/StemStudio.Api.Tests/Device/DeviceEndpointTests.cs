using System.Net;
using System.Net.Http.Json;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Health;
using StemStudio.Contracts.Tracks;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Tests.Device;

/// <summary><c>GET /api/device</c> (backend contract 1.3.0 <c>getProcessingDevice</c>; feature 003, T040, FR-006).</summary>
public sealed class DeviceEndpointTests : ApiTestBase
{
    private const string Gpu = "NVIDIA GeForce RTX 5070 Ti";
    private const string NoCuda = "No usable NVIDIA GPU: CUDA is not available in this container (is the GPU option enabled and the driver current?).";

    private static readonly WorkerDeviceInfo GpuInfo = new()
    {
        Kind = ProcessingDeviceKind.Gpu,
        Name = Gpu,
        ComputeCapability = "12.0",
        TotalMemoryMiB = 16303,
        FreeMemoryMiB = 15469,
    };

    [Fact]
    public async Task GpuWorker_IsMappedFromTheWorkerDeviceInfo()
    {
        Worker.OnGetHealth = () => Task.FromResult(FakeWorkerClient.Health(GpuInfo));

        var device = await GetDeviceAsync();

        device.ShouldBe(new ProcessingDeviceDto { Reachable = true, Kind = ProcessingDeviceKindDto.Gpu, Name = Gpu, TotalMemoryMiB = 16303 });
    }

    [Fact]
    public async Task CpuWorker_CarriesTheReasonWhyTheGpuIsNotUsed()
    {
        Worker.OnGetHealth = () => Task.FromResult(FakeWorkerClient.Health(new WorkerDeviceInfo { Kind = ProcessingDeviceKind.Cpu, Reason = NoCuda }));

        var device = await GetDeviceAsync();

        device.ShouldBe(new ProcessingDeviceDto { Reachable = true, Kind = ProcessingDeviceKindDto.Cpu, Reason = NoCuda });
    }

    [Fact]
    public async Task DefaultCpuWorker_IsReachableWithoutAReason()
    {
        var device = await GetDeviceAsync();

        device.ShouldBe(new ProcessingDeviceDto { Reachable = true, Kind = ProcessingDeviceKindDto.Cpu });
    }

    [Fact]
    public async Task UnreachableWorker_AnswersOkWithReachableFalse()
    {
        Worker.OnGetHealth = () => throw new WorkerUnavailableException("The processing service did not answer in time.");

        var device = await GetDeviceAsync();

        device.ShouldBe(ProcessingDeviceDto.Unreachable);
    }

    [Fact]
    public async Task RejectingWorker_AnswersOkWithReachableFalse()
    {
        Worker.OnGetHealth = () => throw new WorkerRequestException(HttpStatusCode.NotFound, "Not Found");

        (await GetDeviceAsync()).Reachable.ShouldBeFalse();
    }

    [Fact]
    public async Task Answers_AreCachedForTenSeconds()
    {
        Worker.OnGetHealth = () => Task.FromResult(FakeWorkerClient.Health(GpuInfo));
        await GetDeviceAsync();
        Factory.Clock.Advance(TimeSpan.FromSeconds(10) - TimeSpan.FromMilliseconds(1));
        await GetDeviceAsync();
        Worker.HealthCalls.ShouldBe(1);

        Factory.Clock.Advance(TimeSpan.FromMilliseconds(1));
        Worker.OnGetHealth = () => throw new WorkerUnavailableException();

        (await GetDeviceAsync()).Reachable.ShouldBeFalse();
        Worker.HealthCalls.ShouldBe(2);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Response_MatchesTheContract(bool reachable)
    {
        Worker.OnGetHealth = () => reachable ? Task.FromResult(FakeWorkerClient.Health(GpuInfo)) : throw new WorkerUnavailableException();

        using var response = await Client.GetAsync(new Uri(ApiRoutes.Device, UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ContractAssert.MatchesAsync(response, "getProcessingDevice");
    }

    private async Task<ProcessingDeviceDto> GetDeviceAsync()
    {
        using var response = await Client.GetAsync(new Uri(ApiRoutes.Device, UriKind.Relative), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ProcessingDeviceDto>(ApiFactory.Json, Ct))!;
    }
}
