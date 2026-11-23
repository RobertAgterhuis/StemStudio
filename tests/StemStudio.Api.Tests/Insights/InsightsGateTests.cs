using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.LibraryCare.Gate;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.Handouts;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts.LibraryCare;

namespace StemStudio.Api.Tests.Insights;

/// <summary>
/// Security review 020, finding 10: the new write endpoints (quality request, handout creation, practice note) wait behind
/// the backup write gate like the earlier gated operations (<c>x-stemstudio-write-gate: backupRunning</c>).
/// </summary>
public sealed class InsightsGateTests : ApiTestBase
{
    private const string Id = "0b7e4f1c-6a2d-4c8e-9f10-2a3b4c5d6e7f";

    public static TheoryData<string, string, string?, string> GatedOperations() => new()
    {
        { "POST", $"/api/tracks/{Id}/quality", null, "requestQualityReport" },
        { "POST", "/api/assets/handouts", $$"""{"source":{"trackId":"{{Id}}"},"family":"bass"}""", "createHandout" },
        { "PUT", $"/api/tracks/{Id}/practice-notes/bass", """{"text":"Ghost notes."}""", "setPracticeNote" },
    };

    [Theory]
    [MemberData(nameof(GatedOperations))]
    public async Task BackupRunning_RefusesTheWrite(string method, string path, string? body, string operation)
    {
        var song = await Factory.SeedSongAsync();
        Factory.Services.GetRequiredService<LibraryGate>().TryBeginBackup().ShouldBeTrue();

        using var response = await SendAsync(method, path.Replace(Id, song.Id.ToString("D"), StringComparison.Ordinal), body?.Replace(Id, song.Id.ToString("D"), StringComparison.Ordinal));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.JsonAsync()).GetProperty("type").GetString().ShouldBe(LibraryCareProblemTypes.BaseUri + LibraryCareProblemTypes.BackupRunning);
        Factory.InsightsWorker.Calls.ShouldBeEmpty();
        if (operation != "setPracticeNote")
        {
            // setPracticeNote documents no 409 yet (contract follow-up in the hand-off).
            await ContractAssert.MatchesAsync(response, operation);
        }
    }

    [Theory]
    [MemberData(nameof(GatedOperations))]
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
