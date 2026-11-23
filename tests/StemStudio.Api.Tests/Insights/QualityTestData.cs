using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Insights;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Tests.Worker;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Insights;
using StemStudio.Domain.Insights;

namespace StemStudio.Api.Tests.Insights;

/// <summary>Helpers of the feature 020 quality report tests.</summary>
internal static class QualityTestData
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>One quality synchroniser pass in a fresh scope.</summary>
    public static Task<bool> SyncQualityAsync(this ApiFactory factory) => factory.WithScopeAsync(async sp =>
    {
        await sp.GetRequiredService<QualityJobApplier>().SyncAllAsync(CancellationToken.None);
        return true;
    });

    /// <summary>POST the quality request.</summary>
    public static Task<HttpResponseMessage> RequestQualityAsync(this HttpClient client, Guid trackId) =>
        client.PostAsync(new Uri(ApiRoutes.Quality(trackId), UriKind.Relative), null, Ct);

    /// <summary>GET the report.</summary>
    public static async Task<QualityReportDto> GetQualityAsync(this HttpClient client, Guid trackId) =>
        (await client.GetFromJsonAsync<QualityReportDto>(new Uri(ApiRoutes.Quality(trackId), UriKind.Relative), Ct))!;

    /// <summary>The only quality job of the fake worker.</summary>
    public static string OnlyJob(this FakeWorkerInsightsClient worker) => worker.QualityJobs.Keys.Single();

    /// <summary>A result with one measure per channel (good unless given), analyzer version <paramref name="version"/>.</summary>
    public static WorkerQualityResult Result(string version, params (string Channel, QualityRating Rating)[] stems) => new(
        version,
        1.5,
        stems.Select(stem => new WorkerStemQuality(
            stem.Channel,
            stem.Rating,
            stem.Rating == QualityRating.Good ? QualityReason.Clean : QualityReason.Bleed,
            120,
            0.6,
            -8,
            new BleedEstimate(stem.Rating == QualityRating.Good ? QualityLevel.Low : QualityLevel.Medium, 0.2, "drums", false, Reliability.Indicative),
            new ArtefactEstimate(QualityLevel.Low, 0.1, [], Reliability.Indicative))).ToList());

    /// <summary>A result for the six standard stems, all good.</summary>
    public static WorkerQualityResult SixGood(string version = "fake-1") =>
        Result(version, ("vocals", QualityRating.Good), ("drums", QualityRating.Good), ("bass", QualityRating.Good), ("guitar", QualityRating.Good), ("piano", QualityRating.Good), ("other", QualityRating.Good));

    /// <summary>Requests and completes a report through the API and the synchroniser.</summary>
    public static async Task MeasureAsync(this ApiTestFactoryPair pair, Guid trackId, WorkerQualityResult result)
    {
        (await pair.Client.RequestQualityAsync(trackId)).Dispose();
        var job = pair.Factory.InsightsWorker.QualityJobs.Keys.Max(StringComparer.Ordinal)!;
        pair.Factory.InsightsWorker.CompleteQuality(job, result);
        await pair.Factory.SyncQualityAsync();
    }
}

/// <summary>A factory with its client.</summary>
/// <param name="Factory">The factory.</param>
/// <param name="Client">Its client.</param>
internal sealed record ApiTestFactoryPair(ApiFactory Factory, HttpClient Client);
