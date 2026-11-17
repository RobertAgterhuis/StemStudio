using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Agent.LyricAssist;
using StemStudio.Agent.Ports;
using StemStudio.Api.Agent.Loopback;
using StemStudio.Api.Tests.Generations;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Api.Tests.LyricAssist;

/// <summary>023 T056: loopback actions of the eight lyric tools (FR-018 - FR-020) without any text, and the song snapshot's assisted lines (FR-024).</summary>
public sealed class LoopbackLyricAssistActionsTests : AssistTestBase
{
    private const string Text = "the cat sat\non the mat";
    private const string Secret = "zebra";

    private LoopbackLyricAssistActions Actions => new LoopbackLyricAssistActions(new LoopbackClient(Client));

    [Fact]
    public async Task EightTools_WorkWithoutText()
    {
        Assistants.Assistant = new ScriptedAssistant((_, sink, ct) => sink.BlockCompletedAsync(new LyricBlockResult
        {
            BlockIndex = 0,
            Suggestions = [Suggestion(1, 4, "cat", Secret), Suggestion(2, 7, "mat", "rug")],
        }, ct));
        var sheet = await SheetAsync(Text, title: "Lanterns");

        var state = await Actions.GetSheetStateAsync(sheet.Id, Ct);
        var started = await Actions.StartAssistAsync(sheet.Id, new CreateLyricAssistRequest { Kind = AssistKindDto.Check, ExpectedRevision = state.Revision }, Ct);
        await Runner.WhenIdleAsync();
        var done = await Actions.GetAssistAsync(started.AssistId, Ct);
        var list = await Actions.ListSuggestionsAsync(sheet.Id, SuggestionStatusDto.Open, null, Ct);
        var accepted = await Actions.AcceptAsync(sheet.Id, new SuggestionDecisionRequest { SuggestionIds = [list.Items[0].SuggestionId], ExpectedRevision = list.Revision }, Ct);
        var mark = await Actions.MarkIntentionalAsync(sheet.Id, list.Items[1].SuggestionId, new MarkSuggestionIntentionalRequest { Scope = IntentionalScopeDto.Word, ExpectedRevision = accepted.Revision }, Ct);
        await Actions.DeleteIntentionalMarkAsync(sheet.Id, mark.MarkId, Ct);
        var undone = await Actions.UndoAsync(sheet.Id, accepted.Revision, Ct);
        var rhymes = await Actions.FindRhymesAsync("night", AssistLanguageDto.En, 5, Ct);
        var syllables = await Actions.CountSyllablesAsync(sheet.Id, Ct);

        (state.Revision, state.Language, state.Title).ShouldBe((1, "en", "Lanterns"));
        (done.Status, done.Counts.Spelling).ShouldBe((AssistStatusDto.Completed, 2));
        list.Items.Select(i => i.Line).ShouldBe([1, 2]);
        (accepted.Revision, accepted.Applied, accepted.Stale).ShouldBe((2, 1, 0));
        mark.Scope.ShouldBe(IntentionalScopeDto.Word);
        undone.Revision.ShouldBe(3);
        rhymes.Rhymes.Count.ShouldBeLessThanOrEqualTo(5);
        syllables.Lines.Count.ShouldBe(2);
        foreach (var result in new object[] { state, started, done, list, accepted, mark, undone })
        {
            JsonSerializer.Serialize(result).ShouldNotContain(Secret);
            JsonSerializer.Serialize(result).ShouldNotContain("cat sat");
        }
    }

    [Fact]
    public async Task Refusals_BecomeAgentActionErrors()
    {
        var sheet = await SheetAsync(Text);

        var stale = await Should.ThrowAsync<AgentActionException>(() => Actions.StartAssistAsync(sheet.Id, new CreateLyricAssistRequest { Kind = AssistKindDto.Check, ExpectedRevision = 9 }, Ct));
        var missing = await Should.ThrowAsync<AgentActionException>(() => Actions.GetSheetStateAsync(Guid.NewGuid(), Ct));

        (stale.StatusCode, missing.StatusCode).ShouldBe((409, 404));
        Factory.Services.GetRequiredService<ILyricAssistActions>().ShouldBeOfType<LoopbackLyricAssistActions>();
    }

    [Fact]
    public async Task Song_SnapshotsTheAssistedLineCount()
    {
        Assistants.Assistant = new ScriptedAssistant((_, sink, ct) => sink.BlockCompletedAsync(new LyricBlockResult { BlockIndex = 0, Suggestions = [Suggestion(1, 4, "cat", "dog")] }, ct));
        var sheet = await SheetAsync(Text);
        var assist = await RunToEndAsync(sheet.Id, Check());
        (await Client.PostAsJsonAsync(new Uri(ApiRoutes.LyricsSuggestionsAccept(sheet.Id), UriKind.Relative),
            new SuggestionDecisionRequest { SuggestionIds = [assist.Suggestions[0].SuggestionId], ExpectedRevision = 1 }, Ct)).Dispose();

        using var song = await Client.PostAsJsonAsync(new Uri(ApiRoutes.Songs, UriKind.Relative),
            new SongRequest { LyricsId = sheet.Id, Language = "en", StyleId = RestyleEndpointTests.JazzTrio, LeadVoice = LeadVoiceDto.Female, DurationSeconds = 60 }, Ct);
        using var inline = await Client.PostAsJsonAsync(new Uri(ApiRoutes.Songs, UriKind.Relative),
            new SongRequest { LyricsText = "la la la", Language = "en", StyleId = RestyleEndpointTests.JazzTrio, LeadVoice = LeadVoiceDto.Female, DurationSeconds = 60 }, Ct);

        (await song.BodyAsync<GenerationDto>()).Song!.Lyrics.AssistedLineCount.ShouldBe(1);
        (await inline.BodyAsync<GenerationDto>()).Song!.Lyrics.AssistedLineCount.ShouldBe(0);
        await Contract.ContractAssert.MatchesAsync(song, "createSong");
    }

    private static AssistantSuggestion Suggestion(int line, int start, string original, string proposal) => new()
    {
        Category = SuggestionCategoryDto.Spelling,
        Type = SuggestionTypeDto.SpellingTypo,
        Severity = SuggestionSeverityDto.Error,
        Anchor = new AnchorDto { Line = line, Start = start, Length = original.Length, Original = original },
        Operation = SuggestionOperationDto.Replace,
        Proposal = proposal,
        Explanation = "Typo.",
    };
}
