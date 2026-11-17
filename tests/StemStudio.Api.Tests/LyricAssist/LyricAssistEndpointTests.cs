using System.Net;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Agent.LyricAssist;
using StemStudio.Api.LibraryCare.Gate;
using StemStudio.Api.LyricAssist;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Lyrics.Assist;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Tests.LyricAssist;

/// <summary>023 T027: create refusal order, layers, one assist globally, cancel, restart, sheet deleted, events and replay (FR-002, FR-004).</summary>
public sealed class LyricAssistEndpointTests : AssistTestBase
{
    private const string Text = "[verse]\nthe cat sat\non the mat nigth";

    [Fact]
    public async Task UnknownSheet_Is404AndInvalidRequestIs400()
    {
        var sheet = await SheetAsync(Text);

        using var missing = await CreateAsync(Guid.NewGuid(), Check());
        using var invalid = await CreateAsync(sheet.Id, new CreateLyricAssistRequest { Kind = AssistKindDto.Titles, ExpectedRevision = 1, Categories = [SuggestionCategoryDto.Spelling], Count = 9 });
        using var range = await CreateAsync(sheet.Id, Check() with { LineFrom = 3, LineTo = 2 });
        using var alternatives = await CreateAsync(sheet.Id, new CreateLyricAssistRequest { Kind = AssistKindDto.Alternatives, ExpectedRevision = 1 });
        using var instruction = await CreateAsync(sheet.Id, Check() with { Instruction = "darker" });

        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await invalid.FieldErrorsAsync("categories")).ShouldNotBeEmpty();
        (await invalid.FieldErrorsAsync("count")).ShouldNotBeEmpty();
        (await range.FieldErrorsAsync("lineFrom")).ShouldNotBeEmpty();
        (await alternatives.FieldErrorsAsync("lineFrom")).ShouldNotBeEmpty();
        (await instruction.FieldErrorsAsync("instruction")).ShouldNotBeEmpty();
        await ContractAssert.MatchesAsync(missing, "createLyricAssist");
        await ContractAssert.MatchesAsync(invalid, "createLyricAssist");
    }

    [Fact]
    public async Task StaleRevisionAndUnsupportedLanguage_Are409()
    {
        var english = await SheetAsync(Text);
        var german = await SheetAsync("ich bin hier", "de");

        using var stale = await CreateAsync(english.Id, Check(revision: 7));
        using var language = await CreateAsync(german.Id, Check());

        (await Code(stale), (await stale.JsonAsync()).GetProperty("currentRevision").GetInt32()).ShouldBe(("revision-conflict", 1));
        (await Code(language)).ShouldBe("language-unsupported");
        await ContractAssert.MatchesAsync(stale, "createLyricAssist");
        await ContractAssert.MatchesAsync(language, "createLyricAssist");
    }

    [Fact]
    public async Task SheetMatchingLibraryLyrics_IsNotOwnLyrics()
    {
        var track = await SeedTrackLyricsAsync("Paper Lanterns", AssistFixtureText.Corpus("t-fake-song.en.txt"));
        var sheet = await SheetAsync(AssistFixtureText.Sheet("not-own.en.txt"), title: "Lanterns");

        using var refused = await CreateAsync(sheet.Id, Check());

        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await refused.JsonAsync();
        (problem.GetProperty("code").GetString(), problem.GetProperty("matchedTrackId").GetGuid(), problem.GetProperty("overridable").GetBoolean())
            .ShouldBe(("not-own-lyrics", track.Id, true));
        problem.GetProperty("title").GetString()!.ShouldContain(track.Title);
        problem.GetRawText().ShouldNotContain("lantern", Case.Sensitive);
        await ContractAssert.MatchesAsync(refused, "createLyricAssist");
    }

    [Fact]
    public async Task ImitationInstruction_Is400()
    {
        var sheet = await SheetAsync(Text);

        using var refused = await CreateAsync(sheet.Id, new CreateLyricAssistRequest { Kind = AssistKindDto.Alternatives, ExpectedRevision = 1, LineFrom = 2, LineTo = 2, Instruction = "in the style of a famous band" });

        refused.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await refused.JsonAsync()).GetProperty("type").GetString().ShouldBe("https://stemstudio.local/problems/imitation");
        await ContractAssert.MatchesAsync(refused, "createLyricAssist");
    }

    [Fact]
    public async Task WithoutModel_WritingKindsAre409AndCheckRunsTheDictionaryLayer()
    {
        Assistants.Assistant = null;
        var sheet = await SheetAsync(Text);

        using var devices = await CreateAsync(sheet.Id, new CreateLyricAssistRequest { Kind = AssistKindDto.Devices, ExpectedRevision = 1 });
        var check = await RunToEndAsync(sheet.Id, Check());

        var problem = await devices.JsonAsync();
        (problem.GetProperty("code").GetString(), problem.GetProperty("reason").GetString(), problem.GetProperty("fix").GetString()).ShouldBe(("model-unavailable", "model-unavailable", Assistants.Fix));
        await ContractAssert.MatchesAsync(devices, "createLyricAssist");
        (check.Status, check.BlocksTotal).ShouldBe((AssistStatusDto.Completed, 0));
        check.Layers.Single(l => l.Layer == SuggestionSourceDto.Model).ShouldSatisfyAllConditions(l => l.Available.ShouldBeFalse(), l => l.Fix.ShouldBe(Assistants.Fix));
        check.Suggestions.Single().ShouldSatisfyAllConditions(
            s => s.Type.ShouldBe(SuggestionTypeDto.SpellingUnknownWord), s => s.Severity.ShouldBe(SuggestionSeverityDto.Improvement),
            s => s.Anchor.ShouldBe(new AnchorDto { Line = 3, Start = 11, Length = 5, Original = "nigth" }), s => s.Proposal.ShouldBe("night"));
    }

    [Fact]
    public async Task Check_DictionaryFirstThenModelBlocksAndCompletes()
    {
        var assistant = new ScriptedAssistant(async (job, sink, ct) =>
        {
            await sink.StartedAsync(new LyricModelInfo { Backend = Contracts.Agent.AgentBackendDto.Fake, Model = "scripted", UsingFallback = false }, ct);
            await sink.BlockCompletedAsync(new LyricBlockResult
            {
                BlockIndex = 0, Discarded = new DiscardedCountsDto { InvalidAnchor = 1 },
                Misses = [new ModelMissVerdict { Line = 3, Word = "nigth", Verdict = MissVerdict.Typo, Correction = "night" }],
                Suggestions = [Grammar(new AnchorDto { Line = 2, Start = 4, Length = 3, Original = "cat" })],
            }, ct);
            await sink.NoteAsync(AssistNoteCodeDto.QualityNote, "note", ct);
        });
        Assistants.Assistant = assistant;
        var sheet = await SheetAsync(Text);

        var assist = await RunToEndAsync(sheet.Id, Check());

        assistant.Jobs.Single().Blocks.Single().Misses.Single().Word.ShouldBe("nigth");
        (assist.Status, assist.Backend, assist.Model, assist.BlocksDone, assist.Discarded.InvalidAnchor).ShouldBe((AssistStatusDto.Completed, Contracts.Agent.AgentBackendDto.Fake, "scripted", 1, 1));
        assist.Suggestions.Select(s => (s.Type, s.Source)).ShouldBe([(SuggestionTypeDto.GrammarAgreement, SuggestionSourceDto.Model), (SuggestionTypeDto.SpellingTypo, SuggestionSourceDto.Model)]);
        var events = await EventsAsync(assist.AssistId, 0);
        events.Select(e => e.Type).ShouldBe(["assist.started", "suggestion.added", "suggestion.added", "assist.progress", "assist.note", "assist.completed"]);
        using var get = await Client.GetAsync(new Uri(ApiRoutes.LyricAssistById(assist.AssistId), UriKind.Relative), Ct);
        await ContractAssert.MatchesAsync(get, "getLyricAssist");
    }

    [Fact]
    public async Task OneAssistGlobally_AndCancelStopsTheRun()
    {
        var started = new TaskCompletionSource();
        Assistants.Assistant = new ScriptedAssistant(async (_, _, ct) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, ct);
        });
        var sheet = await SheetAsync(Text);
        using var first = await CreateAsync(sheet.Id, Check());
        var id = (await first.BodyAsync<LyricAssistSummaryDto>()).AssistId;
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);

        using var second = await CreateAsync(sheet.Id, Check());
        using var cancel = await Client.PostAsync(new Uri(ApiRoutes.LyricAssistCancel(id), UriKind.Relative), null, Ct);
        using var again = await Client.PostAsync(new Uri(ApiRoutes.LyricAssistCancel(id), UriKind.Relative), null, Ct);
        using var unknown = await Client.PostAsync(new Uri(ApiRoutes.LyricAssistCancel(Guid.NewGuid()), UriKind.Relative), null, Ct);

        first.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await ContractAssert.MatchesAsync(first, "createLyricAssist");
        ((await second.JsonAsync()).GetProperty("activeAssistId").GetGuid(), await Code(second)).ShouldBe((id, "assist-active"));
        (await cancel.BodyAsync<LyricAssistSummaryDto>()).Status.ShouldBe(AssistStatusDto.Cancelled);
        ((await again.JsonAsync()).GetProperty("type").GetString(), unknown.StatusCode).ShouldBe(("https://stemstudio.local/problems/assist-ended", HttpStatusCode.NotFound));
        await ContractAssert.MatchesAsync(cancel, "cancelLyricAssist");
        await ContractAssert.MatchesAsync(again, "cancelLyricAssist");
        (await EventsAsync(id, 0))[^1].Type.ShouldBe("assist.cancelled");
    }

    [Fact]
    public async Task DeletingTheSheet_CancelsTheRunningAssist()
    {
        var started = new TaskCompletionSource();
        var cancelled = false;
        Assistants.Assistant = new ScriptedAssistant(async (_, _, ct) =>
        {
            started.SetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, ct);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
                throw;
            }
        });
        var sheet = await SheetAsync(Text);
        (await CreateAsync(sheet.Id, Check())).Dispose();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);

        using var deleted = await Client.DeleteAsync(new Uri(ApiRoutes.LyricsSheet(sheet.Id), UriKind.Relative), Ct);

        (deleted.StatusCode, cancelled, Runner.RunningId).ShouldBe((HttpStatusCode.NoContent, true, null));
        (await Factory.WithScopeAsync(sp => sp.GetRequiredService<ILyricAssistRepository>().ActiveAsync(Ct))).ShouldBeEmpty();
    }

    [Fact]
    public async Task ModelFailure_FailsTheAssistAndKeepsDictionaryResults()
    {
        Assistants.Assistant = new ScriptedAssistant((_, _, _) => throw new LyricAssistException(AssistFailureCodeDto.BackendUnavailable, "Ollama is not running.", "Start Ollama."));
        var sheet = await SheetAsync(Text);

        var assist = await RunToEndAsync(sheet.Id, Check());

        (assist.Status, assist.Failure!.Code, assist.Failure.Fix).ShouldBe((AssistStatusDto.Failed, AssistFailureCodeDto.BackendUnavailable, "Start Ollama."));
        var failed = (await EventsAsync(assist.AssistId, 0))[^1];
        (failed.Type, failed.Data).ShouldBe(("assist.failed", """{"code":"backend-unavailable","message":"Ollama is not running.","fix":"Start Ollama."}"""));
    }

    [Fact]
    public async Task UnexpectedError_FailsAsInternalWithoutTheExceptionText()
    {
        Assistants.Assistant = new ScriptedAssistant((_, _, _) => throw new InvalidOperationException("secret detail"));
        var sheet = await SheetAsync(Text);

        var assist = await RunToEndAsync(sheet.Id, Check());

        (assist.Status, assist.Failure!.Code, assist.Failure.Message).ShouldBe((AssistStatusDto.Failed, AssistFailureCodeDto.Internal, LyricAssistRunner.InternalMessage));
        Factory.Logs.Entries.ShouldNotContain(e => e.Message.Contains("secret detail", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LibraryMaintenanceBetweenBlocks_HoldsTheRun()
    {
        var gate = Factory.Services.GetRequiredService<LibraryGate>();
        var firstBlock = new TaskCompletionSource();
        Assistants.Assistant = new ScriptedAssistant(async (job, sink, ct) =>
        {
            await sink.BlockCompletedAsync(new LyricBlockResult { BlockIndex = 0 }, ct);
            gate.EnterMaintenance();
            firstBlock.SetResult();
            await sink.BlockCompletedAsync(new LyricBlockResult { BlockIndex = 1 }, ct);
        });
        var sheet = await SheetAsync("the cat\n\nthe dog");
        using var created = await CreateAsync(sheet.Id, Check());
        var id = (await created.BodyAsync<LyricAssistSummaryDto>()).AssistId;
        await firstBlock.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        await Task.Delay(100, Ct);

        var held = await Factory.WithScopeAsync(sp => sp.GetRequiredService<ILyricAssistRepository>().FindAsync(id, Ct));
        gate.ExitMaintenance();
        await Runner.WhenIdleAsync();

        (held!.Status, held.BlocksDone).ShouldBe((AssistStatus.Running, 1));
        (await GetAssistAsync(id)).ShouldSatisfyAllConditions(a => a.Status.ShouldBe(AssistStatusDto.Completed), a => a.BlocksDone.ShouldBe(2));
    }

    [Fact]
    public async Task List_NewestFirstAndGetUnknownIs404()
    {
        var sheet = await SheetAsync(Text);
        var first = await RunToEndAsync(sheet.Id, Check());
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        var second = await RunToEndAsync(sheet.Id, Check());

        using var list = await Client.GetAsync(new Uri(ApiRoutes.LyricsAssists(sheet.Id), UriKind.Relative), Ct);
        using var listMissing = await Client.GetAsync(new Uri(ApiRoutes.LyricsAssists(Guid.NewGuid()), UriKind.Relative), Ct);
        using var unknown = await Client.GetAsync(new Uri(ApiRoutes.LyricAssistById(Guid.NewGuid()), UriKind.Relative), Ct);

        (await list.BodyAsync<List<LyricAssistSummaryDto>>()).Select(a => a.AssistId).ShouldBe([second.AssistId, first.AssistId]);
        (listMissing.StatusCode, unknown.StatusCode).ShouldBe((HttpStatusCode.NotFound, HttpStatusCode.NotFound));
        await ContractAssert.MatchesAsync(list, "listLyricAssists");
        await ContractAssert.MatchesAsync(unknown, "getLyricAssist");
    }

    [Fact]
    public async Task Events_ReplayAfterPositionAndCloseAfterTheTerminalEvent()
    {
        var sheet = await SheetAsync(Text);
        var assist = await RunToEndAsync(sheet.Id, Check());

        var all = await StreamTextAsync(ApiRoutes.LyricAssistEvents(assist.AssistId));
        var after = await StreamTextAsync(ApiRoutes.LyricAssistEvents(assist.AssistId, after: 1));
        using var header = new HttpRequestMessage(HttpMethod.Get, new Uri(ApiRoutes.LyricAssistEvents(assist.AssistId), UriKind.Relative));
        header.Headers.Add("Last-Event-ID", "1");
        using var headerResponse = await Client.SendAsync(header, Ct);
        using var invalid = await Client.GetAsync(new Uri(ApiRoutes.LyricAssistEvents(assist.AssistId) + "?after=x", UriKind.Relative), Ct);
        using var unknown = await Client.GetAsync(new Uri(ApiRoutes.LyricAssistEvents(Guid.NewGuid()), UriKind.Relative), Ct);

        all.ShouldStartWith("id: 1\nevent: assist.started\ndata: {");
        all.ShouldEndWith("event: assist.completed\ndata: " + (await EventsAsync(assist.AssistId, 0))[^1].Data + "\n\n");
        after.ShouldNotContain("assist.started");
        (await headerResponse.Content.ReadAsStringAsync(Ct)).ShouldBe(after);
        (invalid.StatusCode, unknown.StatusCode).ShouldBe((HttpStatusCode.BadRequest, HttpStatusCode.NotFound));
        await ContractAssert.MatchesAsync(unknown, "streamLyricAssistEvents");
    }

    [Fact]
    public async Task Events_SendKeepAliveWhileRunning()
    {
        var release = new TaskCompletionSource();
        Assistants.Assistant = new ScriptedAssistant((_, _, ct) => release.Task.WaitAsync(ct));
        var sheet = await SheetAsync(Text);
        using var created = await CreateAsync(sheet.Id, Check());
        var id = (await created.BodyAsync<LyricAssistSummaryDto>()).AssistId;

        var reading = StreamTextAsync(ApiRoutes.LyricAssistEvents(id));
        await Task.Delay(1500, Ct);
        release.SetResult();
        var text = await reading.WaitAsync(TimeSpan.FromSeconds(10), Ct);

        text.ShouldContain(": keep-alive\n\n");
        text.ShouldContain("event: assist.completed");
    }

    [Fact]
    public async Task Restart_FailsQueuedAndRunningAssistsAsInterrupted()
    {
        var dataDir = Directory.CreateTempSubdirectory("stemstudio-023-restart-").FullName;
        Guid assistId;
        using (var first = new ApiFactory(dataDir))
        {
            first.CreateClient().Dispose();
            assistId = await first.WithScopeAsync(async sp =>
            {
                var sheet = LyricsSheet.Create(Guid.NewGuid(), new LyricsContent("Song", "en", "the cat"), DateTime.UtcNow);
                await sp.GetRequiredService<ILyricsSheetRepository>().AddAsync(sheet, Ct);
                var assist = AssistRun.Create(Guid.NewGuid(), sheet, new AssistRequest { Kind = AssistKind.Check }, DateTime.UtcNow);
                assist.Start([], 1, DateTime.UtcNow);
                await sp.GetRequiredService<ILyricAssistRepository>().AddAsync(assist, Ct);
                return assist.Id;
            });
        }

        using var second = new ApiFactory(dataDir);
        using var client = second.CreateClient();
        using var response = await client.GetAsync(new Uri(ApiRoutes.LyricAssistById(assistId), UriKind.Relative), Ct);

        var assistDto = await response.BodyAsync<LyricAssistDto>();
        (assistDto.Status, assistDto.Failure!.Code).ShouldBe((AssistStatusDto.Failed, AssistFailureCodeDto.Interrupted));
    }

    [Fact]
    public async Task ModelTextCopyingLibraryLyrics_IsWithheldInProposalAndExplanation()
    {
        await SeedTrackLyricsAsync("Paper Lanterns", AssistFixtureText.Corpus("t-fake-song.en.txt"));
        const string Run = "we light the paper lanterns down by the river";
        var anchor = new AnchorDto { Line = 2, Start = 4, Length = 3, Original = "cat" };
        Assistants.Assistant = new ScriptedAssistant((_, sink, ct) => sink.BlockCompletedAsync(new LyricBlockResult
        {
            BlockIndex = 0,
            Suggestions = [Grammar(anchor) with { Proposal = Run }, Grammar(anchor) with { Explanation = "Like: " + Run }, Grammar(anchor)],
        }, ct));
        var sheet = await SheetAsync(Text);

        var assist = await RunToEndAsync(sheet.Id, Check());

        (assist.Suggestions.Select(x => x.Type), assist.Discarded.Verbatim).ShouldBe(([SuggestionTypeDto.GrammarAgreement, SuggestionTypeDto.SpellingUnknownWord], 2));
    }

    private static AssistantSuggestion Grammar(AnchorDto anchor) => new()
    {
        Category = SuggestionCategoryDto.Grammar, Type = SuggestionTypeDto.GrammarAgreement, Severity = SuggestionSeverityDto.Error, Anchor = anchor,
        Operation = SuggestionOperationDto.Replace, Proposal = "cats", Explanation = "Agreement.",
    };

    private static async Task<string?> Code(HttpResponseMessage response) => (await response.JsonAsync()).GetProperty("code").GetString();

    private Task<IReadOnlyList<AssistEvent>> EventsAsync(Guid assistId, int after) =>
        Factory.WithScopeAsync(sp => sp.GetRequiredService<ILyricAssistRepository>().EventsAsync(assistId, after, Ct));

    private async Task<string> StreamTextAsync(string route)
    {
        using var response = await Client.GetAsync(new Uri(route, UriKind.Relative), HttpCompletionOption.ResponseHeadersRead, Ct);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/event-stream");
        return await response.Content.ReadAsStringAsync(Ct);
    }

    private Task<Domain.Tracks.Track> SeedTrackLyricsAsync(string title, string text)
    {
        var track = SyncTestData.CompletedTrack(new string('a', 11));
        track.EnsureLyrics(SyncTestData.T0).Paste(LyricsDocument.FromUserText(text, null), SyncTestData.T0);
        return Factory.SeedAsync(track).ContinueWith(t => t.Result, TaskScheduler.Default);
    }
}

/// <summary>Reads the 023 fixture texts from the repository (tests/fixtures/lyrics/assist).</summary>
internal static class AssistFixtureText
{
    public static string Root { get; } = Path.Combine(FindRoot(), "tests", "fixtures", "lyrics", "assist");

    public static string Sheet(string file) => LyricsNormaliser.Normalize(Read(Path.Combine("sheets", file)));

    public static string Corpus(string file) => Read(Path.Combine("corpus", file));

    public static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative)).Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "StemStudio.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("StemStudio.slnx not found above the test output.");
    }
}
