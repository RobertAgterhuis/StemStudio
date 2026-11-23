using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.LibraryCare.Gate;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.Handouts;
using StemStudio.Api.Tests.TestSupport;

namespace StemStudio.Api.Tests.Insights;

/// <summary>
/// Backup gate policy (013 data-model 5.2, CTO 014/022, 020 orchestrator correction 17:05): only operations that delete or
/// replace library files wait behind the backup write gate. The 020 writes (quality request, handout creation, practice
/// note) edit the database, start jobs or add new assets, so they stay allowed during a backup.
/// </summary>
public sealed class InsightsGateTests : ApiTestBase
{
    private const string Id = "0b7e4f1c-6a2d-4c8e-9f10-2a3b4c5d6e7f";

    public static TheoryData<string, string, string?, string> UngatedOperations() => new()
    {
        { "POST", $"/api/tracks/{Id}/quality", null, "requestQualityReport" },
        { "POST", "/api/assets/handouts", $$"""{"source":{"trackId":"{{Id}}"},"family":"bass"}""", "createHandout" },
        { "PUT", $"/api/tracks/{Id}/practice-notes/bass", """{"text":"Ghost notes."}""", "setPracticeNote" },
    };

    [Theory]
    [MemberData(nameof(UngatedOperations))]
    public async Task BackupRunning_PassesTheWriteToItsHandler(string method, string path, string? body, string operation)
    {
        Factory.Services.GetRequiredService<LibraryGate>().TryBeginBackup().ShouldBeTrue();

        using var response = await SendAsync(method, path, body);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ContractAssert.MatchesAsync(response, operation);
    }

    [Fact]
    public async Task BackupRunning_StoresThePracticeNote()
    {
        var song = await Factory.SeedSongAsync();
        Factory.Services.GetRequiredService<LibraryGate>().TryBeginBackup().ShouldBeTrue();

        using var response = await SendAsync("PUT", $"/api/tracks/{song.Id:D}/practice-notes/bass", """{"text":"Ghost notes."}""");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ContractAssert.MatchesAsync(response, "setPracticeNote");
    }

    [Theory]
    [MemberData(nameof(UngatedOperations))]
    public async Task GateOpen_PassesTheWriteToItsHandler(string method, string path, string? body, string operation)
    {
        using var response = await SendAsync(method, path, body);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ContractAssert.MatchesAsync(response, operation);
    }

    private async Task<HttpResponseMessage> SendAsync(string method, string path, string? body)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        return await Client.SendAsync(request, Ct);
    }
}
