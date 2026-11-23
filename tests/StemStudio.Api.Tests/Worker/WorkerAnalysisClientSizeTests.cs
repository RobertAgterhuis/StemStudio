using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.InstrumentViews;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;

namespace StemStudio.Api.Tests.Worker;

/// <summary>
/// Security review S-L4: the registered worker analysis client buffers at most
/// <see cref="WorkerClient.MaxAnalysisResponseBytes"/> of a job body, so a hostile body is refused before it is parsed.
/// </summary>
public sealed class WorkerAnalysisClientSizeTests
{
    private const string JobId = "4f1c2b3a4d5e6f708192a3b4c5d6e7f8";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static IWorkerAnalysisClient Client(Func<HttpResponseMessage> respond)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInstrumentViews(new ConfigurationBuilder().Build());
        services.ConfigureHttpClientDefaults(client => client.ConfigurePrimaryHttpMessageHandler(() => new StubHttpMessageHandler(_ => Task.FromResult(respond()))));
        return services.BuildServiceProvider().GetRequiredService<IWorkerAnalysisClient>();
    }

    /// <summary>A completed drum-hits job with the given number of hits (the largest result kind per item).</summary>
    private static string DrumHitsJob(int hits)
    {
        var body = new StringBuilder($$"""{"analysisJobId": "{{JobId}}", "kind": "drum-hits", "status": "completed", "progress": 1, "result": {"kind": "drum-hits", "engine": "drum-onsets", "pieces": ["kick"], "hits": [""");
        for (var index = 0; index < hits; index++)
        {
            body.Append(index == 0 ? string.Empty : ", ").Append(System.Globalization.CultureInfo.InvariantCulture, $$"""{"timeMs": {{1_000_000 + index}}, "piece": "cymbals", "strength": 0.123456789}""");
        }

        return body.Append("]}}").ToString();
    }

    [Fact]
    public async Task TheLargestContractResult_FitsTheLimit()
    {
        var json = DrumHitsJob(ContractLimits.MaxDrumHits);
        json.Length.ShouldBeLessThan(WorkerClient.MaxAnalysisResponseBytes / 2, "at least twice the largest contract body");
        var client = Client(() => StubHttpMessageHandler.Json(HttpStatusCode.OK, json));

        var job = await client.GetAnalysisJobAsync(JobId, Ct);

        job!.Result!.Hits!.Count.ShouldBe(ContractLimits.MaxDrumHits);
    }

    [Fact]
    public async Task AFiftyMegabyteBody_IsRefusedAsAnUnusableService()
    {
        var padded = DrumHitsJob(1)[..^1] + new string(' ', 50 * 1024 * 1024) + "}";
        var client = Client(() => StubHttpMessageHandler.Json(HttpStatusCode.OK, padded));

        await Should.ThrowAsync<WorkerUnavailableException>(() => client.GetAnalysisJobAsync(JobId, Ct));
    }
}
