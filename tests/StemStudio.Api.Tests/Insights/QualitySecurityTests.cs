using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Insights;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Domain.Insights;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Tests.Insights;

/// <summary>
/// Security review of the 020 quality report: no orphan worker jobs (finding 5), the automatic run re-reads the report under
/// the lock (6), the synchroniser filters in SQL and survives a corrupt report (7) and no worker text in the answer (9).
/// </summary>
public sealed class QualitySecurityTests : ApiTestBase
{
    [Fact]
    public async Task AFailedSave_AfterTheSubmit_DeletesTheWorkerJob()
    {
        var failure = new SaveFailure();
        using var factory = new ApiFactory { ConfigureServices = failure.AddTo };
        using var client = factory.CreateClient();
        var track = await factory.SeedAsync(SyncTestData.CompletedTrack());
        failure.When = context => context.ChangeTracker.Entries<Track>().Any(entry => entry.State == EntityState.Modified && entry.Entity.QualityReport?.Status == QualityReportStatus.Queued);

        using var response = await client.RequestQualityAsync(track.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        failure.Failures.ShouldBe(1);
        factory.InsightsWorker.Deleted.Single().ShouldStartWith("quality:");
        factory.InsightsWorker.QualityJobs.ShouldBeEmpty();
        (await factory.FindTrackAsync(track.Id))!.QualityReport.ShouldBeNull();
    }

    [Fact]
    public async Task AnInvalidWorkerJobId_StoresNothing_AndIsWorkerUnavailable()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        Factory.InsightsWorker.NextJobId = "../not-a-job";

        using var response = await Client.RequestQualityAsync(track.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        await ContractAssert.MatchesAsync(response, "requestQualityReport");
        Factory.InsightsWorker.Deleted.ShouldBe(["quality:../not-a-job"]);
        (await Factory.FindTrackAsync(track.Id))!.QualityReport.ShouldBeNull();
    }

    [Fact]
    public async Task ACancelledRequest_AfterTheSubmit_StillStoresTheReport()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        using var cancel = new CancellationTokenSource();
        Factory.InsightsWorker.AfterSubmit = () => cancel.CancelAsync();

        var result = await Factory.WithScopeAsync(sp => sp.GetRequiredService<QualityService>().RequestAsync(track.Id, cancel.Token));

        result.ShouldBeAssignableTo<IStatusCodeHttpResult>()!.StatusCode.ShouldBe(StatusCodes.Status202Accepted);
        (await Factory.FindTrackAsync(track.Id))!.QualityReport!.WorkerJobId.ShouldBe(Factory.InsightsWorker.OnlyJob());
        Factory.InsightsWorker.Deleted.ShouldBeEmpty();
    }
}
