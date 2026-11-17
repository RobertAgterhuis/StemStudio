using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StemStudio.Agent.LyricAssist;
using StemStudio.Agent.Ports;
using StemStudio.Api.LyricAssist;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Contracts.Lyrics;

namespace StemStudio.Api.Tests.LyricAssist;

/// <summary>
/// 023 fake stack (DELTA.md 4, CTO mid-wave checkpoint): with the agent backend <c>fake</c> and <c>STEMSTUDIO_LYRIC_ASSIST_FAKE_SCRIPT</c>
/// naming the <c>e2e/lyric-scripts</c> folder, the real ai-agent factory answers with <c>ScriptedLyricModel</c> per sheet title and kind.
/// </summary>
public sealed class FakeStackTests : IDisposable
{
    private readonly IEnvironmentReader _environment = Substitute.For<IEnvironmentReader>();
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public FakeStackTests()
    {
        _environment.GetVariable(LyricAssistantFactory.FakeScriptVariable).Returns(Path.Combine(Path.GetDirectoryName(AssistFixtureText.Root)!, "..", "..", "..", "e2e", "lyric-scripts"));
        _factory = new ApiFactory
        {
            Settings = new Dictionary<string, string?> { ["StemStudio:Agent:Backend"] = "fake" },
            ConfigureServices = services => services.AddSingleton(_environment),
        };
        _client = _factory.CreateClient();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Check_UsesTheScriptOfTheSheetTitle()
    {
        using var created = await _client.PostAsJsonAsync(new Uri(ApiRoutes.LyricsSheets, UriKind.Relative),
            new LyricsInput { Title = "Zomeravond", Language = "nl", Text = AssistFixtureText.Sheet("zomeravond.nl.txt") }, Ct);
        var sheet = await created.BodyAsync<LyricsSheetDto>();
        (await _client.PutAsJsonAsync(new Uri(ApiRoutes.LyricsAssistSettings(sheet.Id), UriKind.Relative), new SheetAssistSettingsUpdate { Sensitivity = SensitivityDto.Strict }, Ct)).Dispose();

        using var assist = await _client.PostAsJsonAsync(new Uri(ApiRoutes.LyricsAssists(sheet.Id), UriKind.Relative), new CreateLyricAssistRequest { Kind = AssistKindDto.Check, ExpectedRevision = 1 }, Ct);
        var id = (await assist.BodyAsync<LyricAssistDto>()).AssistId;
        await _factory.Services.GetRequiredService<LyricAssistRunner>().WhenIdleAsync();
        using var result = await _client.GetAsync(new Uri(ApiRoutes.LyricAssistById(id), UriKind.Relative), Ct);
        var done = await result.BodyAsync<LyricAssistDto>();

        (done.Status, done.Backend, done.Model).ShouldBe((AssistStatusDto.Completed, Contracts.Agent.AgentBackendDto.Fake, "scripted"));
        done.Suggestions.ShouldContain(s => s.Anchor.Original == "fluisterd" && s.Type == SuggestionTypeDto.SpellingTypo && s.Proposal == "fluistert");
        done.Suggestions.ShouldContain(s => s.Type == SuggestionTypeDto.LicenceArchaism && s.Source == SuggestionSourceDto.Model);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}
