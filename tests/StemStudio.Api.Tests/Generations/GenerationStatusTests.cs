using System.Net;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Api.Tests.Generations;

/// <summary>009 T053: <c>GET /api/generation/status</c> (FR-010, US6).</summary>
public sealed class GenerationStatusTests : ApiTestBase
{
    [Fact]
    public async Task Ready_ListsModelsAndTheBytesToDownload()
    {
        using var response = await GetAsync();

        var status = await response.BodyAsync<GenerationStatusDto>();
        (status.State, status.Reason).ShouldBe((GenerationAvailabilityDto.Ready, null));
        status.Device.ShouldBe(new Contracts.Health.ProcessingDeviceDto { Reachable = true, Kind = ProcessingDeviceKindDto.Gpu, Name = "NVIDIA GeForce RTX 5070 Ti", TotalMemoryMiB = 16303 });
        status.Models.Select(model => model.Id).ShouldBe(["acestep-v15-turbo", "acestep-vae", "qwen3-embedding-0.6b", "acestep-5hz-lm-1.7b"]);
        status.DownloadBytesNeeded.ShouldBe(4_790_000_000L + 340_000_000 + 1_190_000_000 + 3_710_000_000);
        status.Voice.State.ShouldBe(GenerationAvailabilityDto.NotInstalled);
        await ContractAssert.MatchesAsync(response, "getGenerationStatus");
    }

    [Fact]
    public async Task NoGpu_IsUnavailableWithTheProbeReason()
    {
        Factory.Generator.HealthExample = "NoGpuHealth";

        using var response = await GetAsync();

        var status = await response.BodyAsync<GenerationStatusDto>();
        (status.State, status.Reason).ShouldBe((GenerationAvailabilityDto.Unavailable, "CUDA is not available (no NVIDIA GPU or driver visible in the container)."));
        (status.Device!.Kind, status.Device.Name, status.Device.Reason).ShouldBe((ProcessingDeviceKindDto.Cpu, null, status.Reason));
        await ContractAssert.MatchesAsync(response, "getGenerationStatus");
    }

    [Fact]
    public async Task NoBaseUrl_IsNotInstalled()
    {
        using var factory = new ApiFactory { GeneratorInstalled = false };
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri(ApiRoutes.GenerationStatus, UriKind.Relative), Ct);

        var status = await response.BodyAsync<GenerationStatusDto>();
        (status.State, status.Reason, status.Device, status.DownloadBytesNeeded).ShouldBe((GenerationAvailabilityDto.NotInstalled, "The generator service is not installed.", null, 0L));
        status.Models.ShouldBeEmpty();
        factory.Generator.HealthCalls.ShouldBe(0);
        await ContractAssert.MatchesAsync(response, "getGenerationStatus");
    }

    [Fact]
    public async Task Unreachable_IsNotInstalled()
    {
        Factory.Generator.Override = _ => throw new HttpRequestException("refused");

        using var response = await GetAsync();

        (await response.BodyAsync<GenerationStatusDto>()).State.ShouldBe(GenerationAvailabilityDto.NotInstalled);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TheProbeIsCachedForTenSeconds()
    {
        (await GetAsync()).Dispose();
        Factory.Generator.HealthExample = "NoGpuHealth";

        using var cached = await GetAsync();
        Factory.Clock.Advance(TimeSpan.FromSeconds(10));
        using var fresh = await GetAsync();

        (await cached.BodyAsync<GenerationStatusDto>()).State.ShouldBe(GenerationAvailabilityDto.Ready);
        (await fresh.BodyAsync<GenerationStatusDto>()).State.ShouldBe(GenerationAvailabilityDto.Unavailable);
        Factory.Generator.HealthCalls.ShouldBe(2);
    }

    private Task<HttpResponseMessage> GetAsync() => Client.GetAsync(new Uri(ApiRoutes.GenerationStatus, UriKind.Relative), Ct);
}
