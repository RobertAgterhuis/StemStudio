using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Agent.LyricAssist;
using StemStudio.Agent.Ports;
using StemStudio.Api.LyricAssist;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Domain.Lyrics.Assist;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Tests.LyricAssist;

/// <summary>023 edge cases: model verdicts on misses, writing kinds' defaults, idle and orphan streams, undo/restore refusals, accept races.</summary>
public sealed class LyricAssistEdgeTests : AssistTestBase
{
    private const string Text = "the cat sat\non the mat nigth wordz zzz";

    [Fact]
    public async Task MissVerdicts_TypoLicenceKeepAndUnclassified()
    {
        Assistants.Assistant = new ScriptedAssistant(async (_, sink, ct) =>
        {
            await sink.StartedAsync(new LyricModelInfo { Backend = AgentBackendDto.Claude, Model = "claude-opus-5-5", UsingFallback = true }, ct);
            await sink.UsageAsync(new TokenUsageDto { InputTokens = 10, OutputTokens = 2, CachedInputTokens = 0 }, ct);
            await sink.BlockCompletedAsync(new LyricBlockResult
            {
                BlockIndex = 0,
                Misses =
                [
                    new ModelMissVerdict { Line = 2, Word = "nigth", Verdict = MissVerdict.Licence, LicenceType = "sound-spelling" },
                    new ModelMissVerdict { Line = 2, Word = "wordz", Verdict = MissVerdict.Keep },
                    new ModelMissVerdict { Line = 2, Word = "zzz", Verdict = MissVerdict.Typo, Correction = " " },
                ],
            }, ct);
        });
        var sheet = await SheetAsync(Text);
        (await Client.PutAsJsonAsync(new Uri(ApiRoutes.LyricsAssistSettings(sheet.Id), UriKind.Relative), new SheetAssistSettingsUpdate { Sensitivity = SensitivityDto.Strict }, Ct)).Dispose();

        var assist = await RunToEndAsync(sheet.Id, Check());

        assist.Suggestions.Select(s => (s.Anchor.Original, s.Type)).ShouldBe([("nigth", SuggestionTypeDto.LicenceSoundSpelling), ("zzz", SuggestionTypeDto.SpellingUnknownWord)]);
        (assist.Backend, assist.UsingFallback, assist.Usage!.InputTokens).ShouldBe((AgentBackendDto.Claude, true, 10));
    }

    [Theory]
    [InlineData("elision", SuggestionTypeDto.LicenceElision)]
    [InlineData("inversion", SuggestionTypeDto.LicenceInversion)]
    [InlineData("archaism", SuggestionTypeDto.LicenceArchaism)]
    [InlineData("code-switch", SuggestionTypeDto.LicenceCodeSwitch)]
    [InlineData("slang", SuggestionTypeDto.LicenceSlang)]
    [InlineData(null, SuggestionTypeDto.LicenceSlang)]
    public async Task LicenceVerdicts_MapToTheirTypes(string? licence, SuggestionTypeDto expected)
    {
        Assistants.Assistant = new ScriptedAssistant((_, sink, ct) => sink.BlockCompletedAsync(new LyricBlockResult
        {
            BlockIndex = 0,
            Misses = [new ModelMissVerdict { Line = 1, Word = "zzz", Verdict = MissVerdict.Licence, LicenceType = licence }],
        }, ct));
        var sheet = await SheetAsync("zzz the cat");
        (await Client.PutAsJsonAsync(new Uri(ApiRoutes.LyricsAssistSettings(sheet.Id), UriKind.Relative), new SheetAssistSettingsUpdate { Sensitivity = SensitivityDto.Strict }, Ct)).Dispose();

        var assist = await RunToEndAsync(sheet.Id, Check());

        assist.Suggestions.Single().Type.ShouldBe(expected);
    }

    [Theory]
    [InlineData(AssistKindDto.Continue, 2)]
    [InlineData(AssistKindDto.Titles, 5)]
    [InlineData(AssistKindDto.Alternatives, 3)]
    public async Task WritingKinds_GetTheirDefaults(AssistKindDto kind, int count)
    {
        var assistant = ScriptedAssistant.Empty();
        Assistants.Assistant = assistant;
        var sheet = await SheetAsync(Text);

        var assist = await RunToEndAsync(sheet.Id, new CreateLyricAssistRequest
        {
            Kind = kind,
            ExpectedRevision = 1,
            LineFrom = kind == AssistKindDto.Alternatives ? 1 : null,
            LineTo = kind == AssistKindDto.Alternatives ? 1 : null,
            Instruction = "  darker  ",
        });

        var job = assistant.Jobs.Single();
        (assist.Status, job.Count, job.Instruction).ShouldBe((AssistStatusDto.Completed, count, "darker"));
        if (kind == AssistKindDto.Continue)
        {
            job.LineTo.ShouldBe(2);
        }
    }

    [Fact]
    public async Task Devices_AcceptTheirCategoriesOnly()
    {
        var sheet = await SheetAsync(Text);

        using var wrong = await CreateAsync(sheet.Id, new CreateLyricAssistRequest { Kind = AssistKindDto.Devices, ExpectedRevision = 1, Categories = [SuggestionCategoryDto.Spelling] });
        using var tooMany = await CreateAsync(sheet.Id, Check() with { Categories = [SuggestionCategoryDto.Spelling, SuggestionCategoryDto.Grammar, SuggestionCategoryDto.Flow, SuggestionCategoryDto.Licence, SuggestionCategoryDto.Spelling, SuggestionCategoryDto.Flow] });
        using var missing = await Client.PostAsJsonAsync(new Uri(ApiRoutes.LyricsAssists(sheet.Id), UriKind.Relative), (object?)null, Ct);
        var ok = await RunToEndAsync(sheet.Id, new CreateLyricAssistRequest { Kind = AssistKindDto.Devices, ExpectedRevision = 1, Categories = [SuggestionCategoryDto.Device] });

        (wrong.StatusCode, tooMany.StatusCode, missing.StatusCode, ok.Status).ShouldBe((HttpStatusCode.BadRequest, HttpStatusCode.BadRequest, HttpStatusCode.BadRequest, AssistStatusDto.Completed));
    }

    [Fact]
    public async Task CancellingAnOrphanedQueuedAssist_WritesTheEvent()
    {
        var sheet = await SheetAsync(Text);
        var id = await Factory.WithScopeAsync(async sp =>
        {
            var stored = (await sp.GetRequiredService<ILyricsSheetRepository>().FindAsync(sheet.Id, Ct))!;
            var assist = AssistRun.Create(Guid.NewGuid(), stored, new AssistRequest { Kind = AssistKind.Check }, DateTime.UtcNow);
            await sp.GetRequiredService<ILyricAssistRepository>().AddAsync(assist, Ct);
            return assist.Id;
        });

        using var cancel = await Client.PostAsync(new Uri(ApiRoutes.LyricAssistCancel(id), UriKind.Relative), null, Ct);
        using var stream = await Client.GetAsync(new Uri(ApiRoutes.LyricAssistEvents(id), UriKind.Relative), Ct);

        (await cancel.BodyAsync<LyricAssistDto>()).Status.ShouldBe(AssistStatusDto.Cancelled);
        (await stream.Content.ReadAsStringAsync(Ct)).ShouldContain("event: assist.cancelled");
    }

    [Fact]
    public async Task Stream_ClosesWhenTheAssistIsGone()
    {
        var release = new TaskCompletionSource();
        Assistants.Assistant = new ScriptedAssistant((_, _, ct) => release.Task.WaitAsync(ct));
        var sheet = await SheetAsync(Text);
        using var created = await CreateAsync(sheet.Id, Check());
        var id = (await created.BodyAsync<LyricAssistDto>()).AssistId;
        using var stream = await Client.GetAsync(new Uri(ApiRoutes.LyricAssistEvents(id), UriKind.Relative), HttpCompletionOption.ResponseHeadersRead, Ct);
        using var reader = new StreamReader(await stream.Content.ReadAsStreamAsync(Ct));

        // Wait for the first event instead of a fixed delay (FLAKES: under load 300 ms was not always enough for the stream to start).
        (await ReadUntilAsync(reader, "event: assist.started")).ShouldBeTrue();
        await Factory.WithScopeAsync(async sp =>
        {
            await sp.GetRequiredService<Persistence.StemStudioDbContext>().LyricAssistEvents.Where(e => e.AssistId == id).ExecuteDeleteAsync(Ct);
            return true;
        });
        (await Client.DeleteAsync(new Uri(ApiRoutes.LyricsSheet(sheet.Id), UriKind.Relative), Ct)).Dispose();

        // The stream ends (the keep-alive tick finds the assist gone) instead of waiting for the idle timeout.
        // (The rest of the stored events may still arrive first; the point is that the stream ends.)
        var rest = await reader.ReadToEndAsync(Ct).WaitAsync(TimeSpan.FromSeconds(30), Ct);
        rest.ShouldNotContain("event: assist.completed");
    }

    private static async Task<bool> ReadUntilAsync(StreamReader reader, string line)
    {
        while (await reader.ReadLineAsync(Ct).AsTask().WaitAsync(TimeSpan.FromSeconds(30), Ct) is { } read)
        {
            if (read == line)
            {
                return true;
            }
        }

        return false;
    }

    [Fact]
    public async Task UndoAndRestore_RefuseMissingSheetsAndRequests()
    {
        var sheet = await SheetAsync(Text);

        using var missing = await Client.PostAsJsonAsync(new Uri(ApiRoutes.LyricsUndo(Guid.NewGuid()), UriKind.Relative), new RevisionRequest { ExpectedRevision = 1 }, Ct);
        using var noBody = await Client.PostAsJsonAsync(new Uri(ApiRoutes.LyricsUndo(sheet.Id), UriKind.Relative), (object?)null, Ct);
        using var stale = await Client.PostAsJsonAsync(new Uri(ApiRoutes.LyricsUndo(sheet.Id), UriKind.Relative), new RevisionRequest { ExpectedRevision = 3 }, Ct);
        using var nothing = await Client.PostAsJsonAsync(new Uri(ApiRoutes.LyricsUndo(sheet.Id), UriKind.Relative), new RevisionRequest { ExpectedRevision = 1 }, Ct);
        using var revisions = await Client.GetAsync(new Uri(ApiRoutes.LyricsRevisions(Guid.NewGuid()), UriKind.Relative), Ct);

        (missing.StatusCode, noBody.StatusCode, stale.StatusCode, revisions.StatusCode).ShouldBe((HttpStatusCode.NotFound, HttpStatusCode.BadRequest, HttpStatusCode.Conflict, HttpStatusCode.NotFound));
        (await nothing.JsonAsync()).GetProperty("code").GetString().ShouldBe("nothing-to-undo");
    }

    [Fact]
    public async Task Undo_LeavesAcceptedSuggestionsStaleWhenTheirTextIsGone()
    {
        Assistants.Assistant = new ScriptedAssistant((_, sink, ct) => sink.BlockCompletedAsync(new LyricBlockResult
        {
            BlockIndex = 0,
            Suggestions = [Typo(1, 4, "cat", "dog")],
        }, ct));
        var sheet = await SheetAsync("the cat sat");
        var assist = await RunToEndAsync(sheet.Id, Check());
        (await Client.PostAsJsonAsync(new Uri(ApiRoutes.LyricsSuggestionsAccept(sheet.Id), UriKind.Relative),
            new SuggestionDecisionRequest { SuggestionIds = [assist.Suggestions[0].SuggestionId], ExpectedRevision = 1 }, Ct)).Dispose();
        await Factory.WithScopeAsync(async sp =>
        {
            var revisions = sp.GetRequiredService<Persistence.StemStudioDbContext>().LyricsSheetRevisions;
            await revisions.Where(r => r.SheetId == sheet.Id && r.Revision == 1).ExecuteUpdateAsync(set => set.SetProperty(r => r.Text, "something else"), Ct);
            return true;
        });

        using var undo = await Client.PostAsJsonAsync(new Uri(ApiRoutes.LyricsUndo(sheet.Id), UriKind.Relative), new RevisionRequest { ExpectedRevision = 2 }, Ct);

        var result = await undo.BodyAsync<AcceptResultDto>();
        (result.Text, result.Reopened!.Count).ShouldBe(("something else", 0));
    }

    [Fact]
    public async Task Accept_AllStaleChangesNothingAndMarkScopeMustFit()
    {
        Assistants.Assistant = new ScriptedAssistant((_, sink, ct) => sink.BlockCompletedAsync(new LyricBlockResult
        {
            BlockIndex = 0,
            Suggestions = [Typo(1, 0, "the cat", "a cat")],
        }, ct));
        var sheet = await SheetAsync("the cat sat");
        var assist = await RunToEndAsync(sheet.Id, Check());
        var id = assist.Suggestions[0].SuggestionId;

        using var word = await Client.PostAsJsonAsync(new Uri(ApiRoutes.LyricsSuggestionIntentional(sheet.Id, id), UriKind.Relative),
            new MarkSuggestionIntentionalRequest { Scope = IntentionalScopeDto.Word, ExpectedRevision = 1 }, Ct);
        (await Client.PutAsJsonAsync(new Uri(ApiRoutes.LyricsSheet(sheet.Id), UriKind.Relative), new Contracts.Lyrics.LyricsInput { Title = "Song", Language = "en", Text = "a dog sat" }, Ct)).Dispose();
        using var accepted = await Client.PostAsJsonAsync(new Uri(ApiRoutes.LyricsSuggestionsAccept(sheet.Id), UriKind.Relative), new SuggestionDecisionRequest { SuggestionIds = [id], ExpectedRevision = 2 }, Ct);

        (await word.FieldErrorsAsync("scope")).ShouldNotBeEmpty();
        var result = await accepted.BodyAsync<AcceptResultDto>();
        (result.Revision, result.Applied.Count, result.Stale.Single()).ShouldBe((2, 0, id));
    }

    [Fact]
    public async Task EventLog_ReplacesOversizedPayloads()
    {
        var sheet = await SheetAsync(Text);
        var assist = await RunToEndAsync(sheet.Id, Check());

        var seq = await Factory.WithScopeAsync(sp => sp.GetRequiredService<AssistEventLog>().AppendAsync(assist.AssistId, LyricAssistEventTypes.Note, new { message = new string('x', 20_000) }, Ct));
        var events = await Factory.WithScopeAsync(sp => sp.GetRequiredService<ILyricAssistRepository>().EventsAsync(assist.AssistId, seq - 1, Ct));

        events.Single().Data.ShouldBe("{}");
    }

    [Fact]
    public async Task ModelLayer_NamesAndMissingFactory()
    {
        LyricModelLayer.Name(AgentBackendDto.Ollama).ShouldBe("Local AI (Ollama)");
        LyricModelLayer.Name(AgentBackendDto.Claude).ShouldBe("Claude (Anthropic)");
        LyricModelLayer.Name(AgentBackendDto.None).ShouldContain("none");
        var settings = Factory.Services.GetRequiredService<IAgentSettingsStore>();
        var layer = new LyricModelLayer(new ServiceCollection().BuildServiceProvider(), settings);

        var state = await layer.ResolveAsync("Song", AssistKind.Check, Ct);

        (state.Available, state.Reason, state.Fix).ShouldBe((false, LyricAssistProblemTypes.ModelUnavailable, LyricModelLayer.NotInstalledFix));
        (await layer.BackendAsync(Ct)).ShouldBe(state.Backend);
    }

    private static AssistantSuggestion Typo(int line, int start, string original, string proposal) => new()
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
