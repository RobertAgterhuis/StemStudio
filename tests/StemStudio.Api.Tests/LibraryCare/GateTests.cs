using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using StemStudio.Api.LibraryCare.Gate;
using StemStudio.Api.Sync;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts.LibraryCare;

namespace StemStudio.Api.Tests.LibraryCare;

/// <summary>013 T017: the library write gate (data-model 5.2, FR-002, FR-013).</summary>
public sealed class GateTests : ApiTestBase
{
    private const string Id = "0b7e4f1c-6a2d-4c8e-9f10-2a3b4c5d6e7f";

    private LibraryGate Gate => Factory.Services.GetRequiredService<LibraryGate>();

    public static TheoryData<string, string> GatedOperations() => new()
    {
        { "DELETE", $"/api/tracks/{Id}" },
        { "POST", $"/api/tracks/{Id}/retry" },
        { "PUT", $"/api/tracks/{Id}/artwork" },
        { "DELETE", $"/api/tracks/{Id}/artwork" },
        { "DELETE", $"/api/assets/{Id}" },
        { "DELETE", $"/api/generations/{Id}" },
        { "DELETE", $"/api/voice-profiles/{Id}" },
        { "DELETE", $"/api/styles/{Id}" },
        { "DELETE", $"/api/lyrics/{Id}" },
        { "DELETE", $"/api/agent/conversations/{Id}" },
    };

    [Theory]
    [MemberData(nameof(GatedOperations))]
    public async Task BackupRunning_RefusesTheGatedOperations(string method, string path)
    {
        Gate.TryBeginBackup().ShouldBeTrue();

        using var response = await SendAsync(method, path);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.JsonAsync()).GetProperty("type").GetString().ShouldBe(LibraryCareProblemTypes.BaseUri + LibraryCareProblemTypes.BackupRunning);
    }

    [Theory]
    [MemberData(nameof(GatedOperations))]
    public async Task GateOpen_PassesTheGatedOperationsToTheirHandlers(string method, string path)
    {
        using var response = await SendAsync(method, path);

        response.StatusCode.ShouldNotBe(HttpStatusCode.Conflict);
        response.StatusCode.ShouldNotBe(HttpStatusCode.ServiceUnavailable);
    }

    [Theory]
    [InlineData("GET", "/api/tracks")]
    [InlineData("GET", "/api/mixes")]
    [InlineData("GET", "/api/assets")]
    [InlineData("GET", "/api/styles")]
    public async Task BackupRunning_LetsReadsThrough(string method, string path)
    {
        Gate.TryBeginBackup().ShouldBeTrue();

        using var response = await SendAsync(method, path);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task BackupRunning_LetsJobStartsThrough()
    {
        Gate.TryBeginBackup().ShouldBeTrue();

        using var response = await Client.PostAsJsonAsync(new Uri("/api/imports", UriKind.Relative), new { url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ" }, Ct);

        response.StatusCode.ShouldNotBe(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("GET", "/api/tracks")]
    [InlineData("POST", "/api/imports")]
    [InlineData("GET", "/api/library-jobs")]
    [InlineData("GET", "/api/storage")]
    [InlineData("GET", "/API/tracks")]
    [InlineData("POST", "/Api/imports")]
    public async Task Maintenance_Answers503WithRetryAfter(string method, string path)
    {
        Gate.EnterMaintenance();

        using var response = await SendAsync(method, path);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        response.Headers.RetryAfter!.Delta.ShouldBe(TimeSpan.FromSeconds(5));
        (await response.JsonAsync()).GetProperty("type").GetString().ShouldBe(LibraryCareProblemTypes.BaseUri + LibraryCareProblemTypes.LibraryMaintenance);
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/api/library-care/status")]
    [InlineData($"/api/library-jobs/{Id}")]
    [InlineData("/API/Library-Care/Status")]
    [InlineData($"/API/LIBRARY-JOBS/{Id}")]
    public async Task Maintenance_KeepsTheThreeAllowedRoutes(string path)
    {
        Gate.EnterMaintenance();

        using var response = await SendAsync("GET", path);

        response.StatusCode.ShouldNotBe(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Maintenance_Ended_OpensEverythingAgain()
    {
        Gate.EnterMaintenance();
        Gate.ExitMaintenance();

        using var response = await SendAsync("GET", "/api/tracks");

        (response.StatusCode, Gate.Mode).ShouldBe((HttpStatusCode.OK, LibraryGateMode.Open));
    }

    [Fact]
    public void Modes_FollowTheTransitions()
    {
        var gate = new LibraryGate(TimeProvider.System);

        gate.TryBeginBackup().ShouldBeTrue();
        gate.TryBeginBackup().ShouldBeFalse();
        gate.Mode.ShouldBe(LibraryGateMode.BackupRunning);
        gate.EndBackup();
        gate.Mode.ShouldBe(LibraryGateMode.Open);
        gate.EnterMaintenance();
        (gate.IsMaintenance, gate.TryBeginBackup()).ShouldBe((true, false));
        gate.EndBackup();
        gate.Mode.ShouldBe(LibraryGateMode.Maintenance);
        gate.ExitMaintenance();
        gate.IsMaintenance.ShouldBeFalse();
    }

    [Fact]
    public async Task InFlightRequests_AreCountedAndAwaited()
    {
        var gate = new LibraryGate(TimeProvider.System);
        var first = gate.TrackRequest();
        var second = gate.TrackRequest();
        gate.InFlight.ShouldBe(2);

        var idle = gate.WaitForIdleAsync(TimeSpan.FromSeconds(30), Ct);
        first.Dispose();
        idle.IsCompleted.ShouldBeFalse();
        second.Dispose();
        second.Dispose();

        (await idle).ShouldBeTrue();
        gate.InFlight.ShouldBe(0);
    }

    [Fact]
    public async Task WaitForIdle_GivesUpAfterTheTimeout()
    {
        var clock = new FakeTimeProvider();
        var gate = new LibraryGate(clock);
        using var request = gate.TrackRequest();

        var idle = gate.WaitForIdleAsync(TimeSpan.FromSeconds(10), Ct);
        clock.Advance(TimeSpan.FromSeconds(11));

        (await idle).ShouldBeFalse();
    }

    [Fact]
    public async Task WaitForIdle_IsImmediateWithoutRequests() =>
        (await new LibraryGate(TimeProvider.System).WaitForIdleAsync(TimeSpan.FromSeconds(1), Ct)).ShouldBeTrue();

    [Fact]
    public async Task WaitWhileMaintenance_ReturnsWhenTheGateOpens()
    {
        var gate = new LibraryGate(TimeProvider.System);
        await gate.WaitWhileMaintenanceAsync(Ct);
        gate.EnterMaintenance();

        var waiting = gate.WaitWhileMaintenanceAsync(Ct);
        waiting.IsCompleted.ShouldBeFalse();
        gate.ExitMaintenance();

        await waiting.WaitAsync(TimeSpan.FromSeconds(5), Ct);
    }

    [Fact]
    public async Task Synchronisers_SkipTheirTickDuringMaintenance()
    {
        var clock = new FakeTimeProvider();
        var gate = new LibraryGate(clock);
        var services = new ServiceCollection().AddSingleton(gate).BuildServiceProvider();
        using var sync = new CountingSync(services.GetRequiredService<IServiceScopeFactory>(), clock);
        await sync.StartAsync(Ct);

        gate.EnterMaintenance();
        clock.Advance(TimeSpan.FromSeconds(1));
        await Task.Delay(50, Ct);
        sync.Passes.ShouldBe(0);
        gate.ExitMaintenance();
        clock.Advance(TimeSpan.FromSeconds(1));
        await Eventually.WaitUntilAsync(() => Task.FromResult(sync.Passes == 1));

        await sync.StopAsync(Ct);
    }

    private async Task<HttpResponseMessage> SendAsync(string method, string path)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method is "POST" or "PUT")
        {
            request.Content = JsonContent.Create(new { });
        }

        return await Client.SendAsync(request, Ct);
    }

    private sealed class CountingSync(IServiceScopeFactory scopes, TimeProvider clock)
        : PeriodicSyncService(scopes, clock, NullLogger.Instance)
    {
        private int _passes;

        public int Passes => Volatile.Read(ref _passes);

        protected override TimeSpan Interval => TimeSpan.FromSeconds(1);

        protected override Task PassAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _passes);
            return Task.CompletedTask;
        }
    }
}
