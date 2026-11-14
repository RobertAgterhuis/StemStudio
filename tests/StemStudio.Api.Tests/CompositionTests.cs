using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using StemStudio.Api.Sync;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Domain;

namespace StemStudio.Api.Tests;

public sealed class CompositionTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void ProblemStatus_UsesBadHttpRequestStatus()
    {
        ProblemDetailsCustomizer.StatusCodeFor(new BadHttpRequestException("too large", 413)).ShouldBe(413);
        ProblemDetailsCustomizer.StatusCodeFor(new InvalidOperationException()).ShouldBe(500);
    }

    [Theory]
    [InlineData(400, false, true)]
    [InlineData(400, true, false)]
    [InlineData(404, false, false)]
    public void Customize_AddsErrorsOnlyToPlain400Problems(int status, bool alreadyHasErrors, bool expectAdded)
    {
        var problem = new ProblemDetails { Status = status };
        if (alreadyHasErrors)
        {
            problem.Extensions["errors"] = "kept";
        }

        ProblemDetailsCustomizer.Customize(new ProblemDetailsContext { HttpContext = new DefaultHttpContext(), ProblemDetails = problem });

        (problem.Extensions.TryGetValue("errors", out var errors) && errors is Dictionary<string, string[]>).ShouldBe(expectAdded);
    }

    [Fact]
    public void Customize_LeavesValidationProblemsAlone()
    {
        var problem = new HttpValidationProblemDetails { Status = 400 };

        ProblemDetailsCustomizer.Customize(new ProblemDetailsContext { HttpContext = new DefaultHttpContext(), ProblemDetails = problem });

        problem.Extensions.ShouldNotContainKey("errors");
    }

    [Fact]
    public async Task Validation_GroupsDomainErrorsUnderPrefixedPaths()
    {
        var result = ApiProblems.Validation(
            [new DomainError("metronome.bpmOverride", "a"), new DomainError("metronome.bpmOverride", "b"), new DomainError("", "c")],
            "settings");
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider() };
        context.Response.Body = new MemoryStream();

        await result.ExecuteAsync(context);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync(Ct);
        body.ShouldContain("\"settings.metronome.bpmOverride\":[\"a\",\"b\"]");
        body.ShouldContain("\"settings\":[\"c\"]");
    }

    [Fact]
    public async Task ConnectionString_CanBeConfiguredExplicitly()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"stemstudio-explicit-{Guid.NewGuid():N}.db");
        using var factory = new ApiFactory();
        using var configured = factory.WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:StemStudio", $"Data Source={dbPath};Pooling=False"));
        using var client = configured.CreateClient();

        using var response = await client.GetAsync(new Uri(ApiRoutes.Tracks, UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        File.Exists(dbPath).ShouldBeTrue();
        File.Exists(Path.Combine(factory.DataDir, "stemstudio.db")).ShouldBeFalse();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(dbPath);
    }

    [Fact]
    public async Task JobSyncService_StopsWhenDisposedAndDisposesWithoutStart()
    {
        var synchronizer = Substitute.For<ITrackSynchronizer>();
        var services = new ServiceCollection().AddScoped(_ => synchronizer).BuildServiceProvider();
        var clock = new FakeTimeProvider();
        var options = Options.Create(new StemStudioApiOptions { JobPollIntervalMs = 1000 });
        using (new JobSyncService(services.GetRequiredService<IServiceScopeFactory>(), options, clock, NullLogger<JobSyncService>.Instance))
        {
        }

        var service = new JobSyncService(services.GetRequiredService<IServiceScopeFactory>(), options, clock, NullLogger<JobSyncService>.Instance);
        await service.StartAsync(Ct);
        service.Dispose();

        await service.ExecuteTask!.ContinueWith(_ => { }, Ct, TaskContinuationOptions.None, TaskScheduler.Default).WaitAsync(TimeSpan.FromSeconds(10), Ct);
        service.ExecuteTask.IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public async Task JobSyncService_StopAsyncEndsTheLoop()
    {
        var services = new ServiceCollection().AddScoped(_ => Substitute.For<ITrackSynchronizer>()).BuildServiceProvider();
        using var service = new JobSyncService(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new StemStudioApiOptions()),
            new FakeTimeProvider(),
            NullLogger<JobSyncService>.Instance);
        await service.StartAsync(Ct);

        await service.StopAsync(Ct);

        service.ExecuteTask!.IsCompleted.ShouldBeTrue();
    }
}
