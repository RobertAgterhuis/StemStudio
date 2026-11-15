using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Tests.Worker;

/// <summary>Device members of the worker client against the worker 1.2.0 examples (feature 003, T021).</summary>
public sealed class WorkerDeviceClientTests
{
    private const string JobId = WorkerExamples.JobId;
    private const string DetailJobId = "7c1e9a2b4d6f48e0a3b5c7d9e1f20a4b";
    private const string Gpu = "NVIDIA GeForce RTX 5070 Ti";

    private static readonly OpenApiContract WorkerContract =
        OpenApiContract.Load(Path.Combine(AppContext.BaseDirectory, "Contract", "worker-api.openapi.yaml"));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetJob_ParsesTheGpuDevice()
    {
        var (client, _) = WorkerClientTests.Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, "separating-job"));

        var job = (await client.GetJobAsync(JobId, Ct))!;

        job.Device.ShouldBe(new WorkerJobDevice(ProcessingDeviceKind.Gpu, Gpu, false, null));
    }

    [Fact]
    public async Task GetJob_ParsesTheCpuDevice()
    {
        var (client, _) = WorkerClientTests.Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, "completed-job"));

        var job = (await client.GetJobAsync(JobId, Ct))!;

        job.Device.ShouldBe(new WorkerJobDevice(ProcessingDeviceKind.Cpu, null, false, null));
    }

    [Fact]
    public async Task GetDetailJob_ParsesTheDeviceWithItsFallback()
    {
        var (client, _) = WorkerClientTests.Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, "detail-running-job"));

        var job = (await client.GetDetailJobAsync(DetailJobId, Ct))!;

        job.Device!.Kind.ShouldBe(ProcessingDeviceKind.Gpu);
        job.Device.Name.ShouldBe(Gpu);
        job.Device.CpuFallback.ShouldBeTrue();
        job.Device.FallbackReason!.ShouldContain("ran out of memory");
    }

    [Theory]
    [InlineData("queued-job")]
    [InlineData("separating-job")]
    public async Task GetJob_A110BodyWithoutDeviceParsesToNull(string example)
    {
        var body = WithoutDevice(example);
        var (client, _) = WorkerClientTests.Create(_ => StubHttpMessageHandler.Json(HttpStatusCode.OK, body));

        var job = (await client.GetJobAsync(JobId, Ct))!;

        job.Device.ShouldBeNull();
    }

    [Fact]
    public async Task GetDetailJob_A110BodyWithoutDeviceParsesToNull()
    {
        var body = WithoutDevice("detail-running-job");
        var (client, _) = WorkerClientTests.Create(_ => StubHttpMessageHandler.Json(HttpStatusCode.OK, body));

        var job = (await client.GetDetailJobAsync(DetailJobId, Ct))!;

        (job.Status, job.Device).ShouldBe((WorkerDetailJobState.Running, null));
    }

    [Fact]
    public async Task GetHealth_ParsesTheGpuDeviceInfo()
    {
        var (client, handler) = WorkerClientTests.Create(_ => StubHttpMessageHandler.Json(HttpStatusCode.OK, HealthExample("gpu")));

        var health = await client.GetHealthAsync(Ct);

        (health.Status, health.Separator, health.Device, health.ContractVersion).ShouldBe(("ok", "demucs", "auto", "1.6.0"));
        health.DeviceInfo.ShouldBe(new WorkerDeviceInfo { Kind = ProcessingDeviceKind.Gpu, Name = Gpu, ComputeCapability = "12.0", TotalMemoryMiB = 16303, FreeMemoryMiB = 15469 });
        var request = handler.Requests.Single();
        (request.Method, request.Uri.AbsolutePath).ShouldBe((HttpMethod.Get, "/health"));
    }

    [Fact]
    public async Task GetHealth_ParsesTheCpuDeviceInfo()
    {
        var (client, _) = WorkerClientTests.Create(_ => StubHttpMessageHandler.Json(HttpStatusCode.OK, HealthExample("cpu")));

        var health = await client.GetHealthAsync(Ct);

        health.DeviceInfo.ShouldBe(new WorkerDeviceInfo { Kind = ProcessingDeviceKind.Cpu });
    }

    [Fact]
    public async Task GetHealth_ParsesTheReasonWhenTheGpuIsUnavailable()
    {
        var (client, _) = WorkerClientTests.Create(_ => StubHttpMessageHandler.Json(HttpStatusCode.OK, HealthExample("gpuUnavailable")));

        var health = await client.GetHealthAsync(Ct);

        health.DeviceInfo.Kind.ShouldBe(ProcessingDeviceKind.Cpu);
        health.DeviceInfo.Reason!.ShouldStartWith("No usable NVIDIA GPU");
    }

    [Fact]
    public async Task GetHealth_AnUnresponsiveWorkerIsUnavailableAfterTheHealthTimeout()
    {
        var clock = new FakeTimeProvider();
        var client = UnresponsiveClient(clock);

        var call = client.GetHealthAsync(Ct);
        clock.Advance(WorkerTimeouts.Health - TimeSpan.FromMilliseconds(1));
        call.IsCompleted.ShouldBeFalse();
        clock.Advance(TimeSpan.FromMilliseconds(1));

        await Should.ThrowAsync<WorkerUnavailableException>(() => call);
    }

    [Fact]
    public void HealthTimeout_IsTwoSeconds()
    {
        WorkerTimeouts.Health.ShouldBe(TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task GetHealth_ServerErrorsAreUnavailable(HttpStatusCode status)
    {
        var (client, _) = WorkerClientTests.Create(_ => new HttpResponseMessage(status));

        await Should.ThrowAsync<WorkerUnavailableException>(() => client.GetHealthAsync(Ct));
    }

    [Fact]
    public async Task GetHealth_ABodyWithoutDeviceInfoIsUnavailable()
    {
        var body = JsonNode.Parse(HealthExample("cpu"))!.AsObject();
        body.Remove("deviceInfo");
        var (client, _) = WorkerClientTests.Create(_ => StubHttpMessageHandler.Json(HttpStatusCode.OK, body.ToJsonString()));

        await Should.ThrowAsync<WorkerUnavailableException>(() => client.GetHealthAsync(Ct));
    }

    [Fact]
    public async Task Registration_ResolvesAClientThatReadsTheHealth()
    {
        var stub = new StubHttpMessageHandler(_ => Task.FromResult(StubHttpMessageHandler.Json(HttpStatusCode.OK, HealthExample("gpu"))));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["StemStudio:WorkerBaseUrl"] = "http://worker:8000" }).Build();
        var services = new ServiceCollection();
        services.AddWorkerClient(configuration).ConfigurePrimaryHttpMessageHandler(() => stub);
        await using var provider = services.BuildServiceProvider();

        var health = await provider.GetRequiredService<IWorkerClient>().GetHealthAsync(Ct);

        health.DeviceInfo.Kind.ShouldBe(ProcessingDeviceKind.Gpu);
    }

    private static string HealthExample(string name) =>
        WorkerContract.Root["paths"]!["/health"]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["examples"]![name]!["value"]!.ToJsonString();

    private static string WithoutDevice(string example)
    {
        var body = JsonNode.Parse(WorkerExamples.Read(example))!.AsObject();
        body.Remove("device");
        return body.ToJsonString();
    }

    private static WorkerClient UnresponsiveClient(TimeProvider clock)
    {
        var http = new HttpClient(new UnresponsiveHandler()) { BaseAddress = new Uri("http://worker:8000") };
        return new WorkerClient(http, NullLogger<WorkerClient>.Instance, clock);
    }

    /// <summary>Never answers; completes only when the request is cancelled.</summary>
    private sealed class UnresponsiveHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
