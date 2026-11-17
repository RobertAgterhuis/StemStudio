using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Agent.LyricAssist;
using StemStudio.Agent.Ports;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Contracts.Lyrics;

namespace StemStudio.Api.Tests.LyricAssist;

/// <summary>Integration review findings (022+023 round): one assist at a time under concurrency, text changes during a run, stream end, run-time Claude notice.</summary>
public sealed class LyricAssistReviewTests : AssistTestBase
{
    private const string Text = "[verse]\nthe cat sat\non the mat nigth";

    [Fact]
    public async Task ConcurrentCreates_StartExactlyOneAssist()
    {
        var release = new TaskCompletionSource();
        Assistants.Assistant = new ScriptedAssistant(async (_, _, ct) => await release.Task.WaitAsync(ct));
        var sheet = await SheetAsync(Text);

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => CreateAsync(sheet.Id, Check())));
        release.SetResult();
        await Runner.WhenIdleAsync();

        responses.Count(r => r.StatusCode == HttpStatusCode.Accepted).ShouldBe(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(5);
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task ASaveDuringTheRun_MovesLaterSuggestionsWithTheText()
    {
        var sheet = await SheetAsync(Text);
        Assistants.Assistant = new ScriptedAssistant(async (_, sink, ct) =>
        {
            using var saved = await Client.PutAsJsonAsync(new Uri(ApiRoutes.LyricsSheet(sheet.Id), UriKind.Relative),
                new LyricsInput { Title = "Song", Language = "en", Text = "[intro]\nnew first line\n" + Text, ExpectedRevision = 1 }, ct);
            saved.StatusCode.ShouldBe(HttpStatusCode.OK);
            await sink.BlockCompletedAsync(new LyricBlockResult
            {
                BlockIndex = 0,
                Suggestions = [Grammar(new AnchorDto { Line = 2, Start = 4, Length = 3, Original = "cat" })],
            }, ct);
        });

        var assist = await RunToEndAsync(sheet.Id, Check());

        assist.Suggestions.Single(s => s.Source == SuggestionSourceDto.Model).Anchor.ShouldBe(new AnchorDto { Line = 4, Start = 4, Length = 3, Original = "cat" });
    }

    [Fact]
    public async Task ReconnectAfterTheTerminalEvent_EndsTheStreamAtOnce()
    {
        var sheet = await SheetAsync(Text);
        var assist = await RunToEndAsync(sheet.Id, Check());
        var last = (await Factory.WithScopeAsync(sp => sp.GetRequiredService<Persistence.Repositories.ILyricAssistRepository>().EventsAsync(assist.AssistId, 0, Ct)))[^1].Seq;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var response = await Client.GetAsync(new Uri(ApiRoutes.LyricAssistEvents(assist.AssistId, after: last), UriKind.Relative), HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        var body = await response.Content.ReadAsStringAsync(timeout.Token);

        body.ShouldNotContain("event:");
    }

    [Fact]
    public async Task ASwitchToClaudeBeforeTheRun_NeedsTheLyricNotice_AndSendsNothing()
    {
        var settings = Factory.Services.GetRequiredService<IAgentSettingsStore>();
        await settings.SaveAsync((await settings.GetAsync(Ct)) with { Backend = AgentBackendDto.Ollama }, Ct);
        var switched = false;
        Assistants.OnResolve = () =>
        {
            if (!switched)
            {
                switched = true;
                settings.SaveAsync(settings.GetAsync(CancellationToken.None).GetAwaiter().GetResult() with { Backend = AgentBackendDto.Claude }, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        };
        var calls = 0;
        Assistants.Assistant = new ScriptedAssistant((_, _, _) =>
        {
            calls++;
            return Task.CompletedTask;
        });
        var sheet = await SheetAsync(Text);

        var assist = await RunToEndAsync(sheet.Id, Check());

        calls.ShouldBe(0);
        (assist.Status, assist.Failure!.Code).ShouldBe((AssistStatusDto.Failed, AssistFailureCodeDto.BackendUnavailable));
        assist.Failure.Message.ShouldContain("notice");
    }

    [Fact]
    public async Task TwoChangesOnTheSameRevision_TheSecondIsARevisionConflict_AndHistoryMatchesTheText()
    {
        var sheet = await SheetAsync(Text);
        using var first = Factory.Services.CreateScope();
        using var second = Factory.Services.CreateScope();
        var a = (await first.ServiceProvider.GetRequiredService<Persistence.Repositories.ILyricsSheetRepository>().FindAsync(sheet.Id, Ct))!;
        var b = (await second.ServiceProvider.GetRequiredService<Persistence.Repositories.ILyricsSheetRepository>().FindAsync(sheet.Id, Ct))!;
        var stamp = new Api.LyricAssist.RevisionStamp(Domain.Lyrics.Assist.RevisionCause.UserSave);

        await first.ServiceProvider.GetRequiredService<Api.LyricAssist.LyricsSheetChanges>()
            .ChangeAsync(a, s => s.Update(new Domain.Lyrics.LyricsContent("Song", "en", "first writer"), DateTime.UtcNow), stamp, Ct);
        var conflict = await Should.ThrowAsync<Api.LyricAssist.LyricsRevisionConflictException>(() => second.ServiceProvider.GetRequiredService<Api.LyricAssist.LyricsSheetChanges>()
            .ChangeAsync(b, s => s.Update(new Domain.Lyrics.LyricsContent("Song", "en", "second writer"), DateTime.UtcNow), stamp, Ct));

        conflict.CurrentRevision.ShouldBe(2);
        using var get = await Client.GetAsync(new Uri(ApiRoutes.LyricsSheet(sheet.Id), UriKind.Relative), Ct);
        (await get.BodyAsync<LyricsSheetDto>()).Text.ShouldBe("first writer");
    }

    [Fact]
    public async Task ARevisionConflictException_IsA409RevisionConflictProblem()
    {
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var handled = await new Api.LyricAssist.LyricsRevisionConflictHandler().TryHandleAsync(context, new Api.LyricAssist.LyricsRevisionConflictException(4), Ct);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync(Ct);
        body.ShouldContain("revision-conflict");
        body.ShouldContain("\"currentRevision\":4");
        (await new Api.LyricAssist.LyricsRevisionConflictHandler().TryHandleAsync(new DefaultHttpContext(), new InvalidOperationException(), Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task UndefinedEnumNumbers_Are400()
    {
        var sheet = await SheetAsync(Text);

        using var kind = await Client.PostAsync(new Uri(ApiRoutes.LyricsAssists(sheet.Id), UriKind.Relative),
            new StringContent("{\"kind\":99,\"expectedRevision\":1}", System.Text.Encoding.UTF8, "application/json"), Ct);
        using var status = await Client.GetAsync(new Uri(ApiRoutes.LyricsSheet(sheet.Id) + "/suggestions?status=99", UriKind.Relative), Ct);

        (kind.StatusCode, status.StatusCode).ShouldBe((HttpStatusCode.BadRequest, HttpStatusCode.BadRequest));
    }

    private static AssistantSuggestion Grammar(AnchorDto anchor) => new()
    {
        Category = SuggestionCategoryDto.Grammar,
        Type = SuggestionTypeDto.GrammarAgreement,
        Severity = SuggestionSeverityDto.Improvement,
        Anchor = anchor,
        Operation = SuggestionOperationDto.Replace,
        Proposal = "dog",
        Explanation = "Agreement.",
    };
}
