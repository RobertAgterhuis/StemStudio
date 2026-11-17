using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Contracts.Lyrics;
using StemStudio.Web.Api;
using StemStudio.Web.Components.Lyrics.Assist;
using StemStudio.Web.Tests.Fakes;
using static StemStudio.Web.Tests.Fakes.LyricAssistData;

namespace StemStudio.Web.Tests.Songs;

/// <summary>The assistant session of one sheet (023 US1, US5, US6): explicit start, save first, Claude notice, refusals, the stream.</summary>
public sealed class LyricAssistSessionTests
{
    private readonly IStemStudioApiClient _api = Substitute.For<IStemStudioApiClient>();
    private readonly LyricAssistSession _session;

    public LyricAssistSessionTests()
    {
        _session = new LyricAssistSession(_api, new EditorDocument(Sheet()));
        _api.GetLyricAssistStatusAsync(Arg.Any<CancellationToken>()).Returns(Status());
        _api.CreateLyricAssistAsync(LyricsId, Arg.Any<CreateLyricAssistRequest>(), Arg.Any<CancellationToken>()).Returns(Assist(AssistStatusDto.Queued, AssistKindDto.Check, SpellingFix()));
        _api.StreamLyricAssistEventsAsync(AssistId, Arg.Any<int?>(), Arg.Any<CancellationToken>()).Returns(Events());
        _api.GetLyricAssistAsync(AssistId, Arg.Any<CancellationToken>()).Returns(Assist(AssistStatusDto.Completed, AssistKindDto.Check, SpellingFix()));
        _api.ListLyricSuggestionsAsync(LyricsId, Arg.Any<SuggestionStatusDto?>(), Arg.Any<CancellationToken>()).Returns(List());
    }

    private static readonly AssistRequest Check = new(AssistKindDto.Check);

    private async Task StartAsync(AssistRequest? request = null)
    {
        await _session.LoadStatusAsync();
        await _session.StartAsync(request ?? Check);
        await _session.Following;
    }

    [Fact]
    public async Task Start_PostsTheSavedRevision_AndAnnounces()
    {
        _api.StreamLyricAssistEventsAsync(AssistId, Arg.Any<int?>(), Arg.Any<CancellationToken>()).Returns(_ => Never());
        await _session.LoadStatusAsync();
        await _session.StartAsync(Check with { Instruction = "  darker  " });

        await _api.Received(1).CreateLyricAssistAsync(LyricsId, Arg.Is<CreateLyricAssistRequest>(r => r.Kind == AssistKindDto.Check && r.ExpectedRevision == 3 && r.Instruction == "darker"), Arg.Any<CancellationToken>());
        await _api.DidNotReceive().UpdateLyricsSheetAsync(Arg.Any<Guid>(), Arg.Any<LyricsInput>(), Arg.Any<CancellationToken>());
        _session.Live.ShouldBe("Checking 3 lines");
        _session.Board.Open.ShouldHaveSingleItem().SuggestionId.ShouldBe(Spelling);
    }

    [Fact]
    public async Task Start_WithUnsavedChanges_SavesFirst()
    {
        _session.Document.Text = Text + "\nnieuw";
        _api.UpdateLyricsSheetAsync(LyricsId, Arg.Any<LyricsInput>(), Arg.Any<CancellationToken>()).Returns(Sheet(text: Text + "\nnieuw", revision: 4));

        await StartAsync();

        await _api.Received(1).UpdateLyricsSheetAsync(LyricsId, Arg.Is<LyricsInput>(i => i.ExpectedRevision == 3 && i.Text.EndsWith("nieuw", StringComparison.Ordinal)), Arg.Any<CancellationToken>());
        await _api.Received(1).CreateLyricAssistAsync(LyricsId, Arg.Is<CreateLyricAssistRequest>(r => r.ExpectedRevision == 4), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Start_WhenTheSaveFails_SendsNothing_AndKeepsTheText()
    {
        _session.Document.Text = "mijn tekst";
        _api.UpdateLyricsSheetAsync(LyricsId, Arg.Any<LyricsInput>(), Arg.Any<CancellationToken>()).ThrowsAsync(Problem(LyricAssistProblemTypes.RevisionConflict));

        await StartAsync();

        await _api.DidNotReceive().CreateLyricAssistAsync(Arg.Any<Guid>(), Arg.Any<CreateLyricAssistRequest>(), Arg.Any<CancellationToken>());
        _session.Document.Text.ShouldBe("mijn tekst");
        _session.Error.ShouldStartWith("The lyrics were saved in another tab");
    }

    [Fact]
    public async Task Start_WithAnInvalidSave_ShowsTheProblem()
    {
        _session.Document.Title = " ";
        _api.UpdateLyricsSheetAsync(LyricsId, Arg.Any<LyricsInput>(), Arg.Any<CancellationToken>()).ThrowsAsync(Problem("validation", status: System.Net.HttpStatusCode.BadRequest));

        await StartAsync();

        _session.Error.ShouldBe("Problem validation");
    }

    [Fact]
    public async Task ClaudeNotice_HoldsTheRequest_UntilAcknowledged()
    {
        _api.GetLyricAssistStatusAsync(Arg.Any<CancellationToken>()).Returns(Status(AgentBackendDto.Claude, notice: true));
        _api.UpdateLyricAssistSettingsAsync(Arg.Any<LyricAssistSettingsUpdate>(), Arg.Any<CancellationToken>()).Returns(LyricAssistData.Settings());

        await StartAsync();

        _session.PendingNotice.ShouldBe(Check);
        _api.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IStemStudioApiClient.CreateLyricAssistAsync)).ShouldBe(0);

        await _session.AcknowledgeNoticeAsync();

        await _api.Received(1).UpdateLyricAssistSettingsAsync(Arg.Is<LyricAssistSettingsUpdate>(u => u.AcknowledgeClaudeNotice), Arg.Any<CancellationToken>());
        await _api.Received(1).CreateLyricAssistAsync(LyricsId, Arg.Any<CreateLyricAssistRequest>(), Arg.Any<CancellationToken>());
        (_session.PendingNotice, _session.Status!.ClaudeNoticeRequired).ShouldBe((null, false));
    }

    [Fact]
    public async Task ClaudeNotice_Cancelled_SendsNothing()
    {
        _api.GetLyricAssistStatusAsync(Arg.Any<CancellationToken>()).Returns(Status(AgentBackendDto.Claude, notice: true));

        await StartAsync();
        _session.CancelNotice();

        _session.PendingNotice.ShouldBeNull();
        _api.ReceivedCalls().Select(c => c.GetMethodInfo().Name).ShouldBe([nameof(IStemStudioApiClient.GetLyricAssistStatusAsync)]);
    }

    [Fact]
    public async Task ClaudeNotice_RequiredByTheServer_OpensTheNotice()
    {
        _api.CreateLyricAssistAsync(LyricsId, Arg.Any<CreateLyricAssistRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(Problem(LyricAssistProblemTypes.ClaudeNoticeRequired));

        await StartAsync();

        (_session.PendingNotice, _session.Refusal).ShouldBe((Check, null));
    }

    [Fact]
    public async Task AcknowledgeFailure_ShowsTheError_AndStartsNothing()
    {
        _api.GetLyricAssistStatusAsync(Arg.Any<CancellationToken>()).Returns(Status(AgentBackendDto.Claude, notice: true));
        _api.UpdateLyricAssistSettingsAsync(Arg.Any<LyricAssistSettingsUpdate>(), Arg.Any<CancellationToken>()).ThrowsAsync(new ApiException(ApiError.Unreachable));

        await StartAsync();
        await _session.AcknowledgeNoticeAsync();

        _session.Error.ShouldNotBeNull();
        await _api.DidNotReceive().CreateLyricAssistAsync(Arg.Any<Guid>(), Arg.Any<CreateLyricAssistRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotOwn_IsARefusal_WithTheTrackAndTheOverride()
    {
        _api.CreateLyricAssistAsync(LyricsId, Arg.Any<CreateLyricAssistRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(NotOwn());

        await StartAsync();

        var refusal = _session.Refusal.ShouldNotBeNull();
        (refusal.IsNotOwn, refusal.Overridable, refusal.MatchedTrackId, refusal.MatchedTrackTitle).ShouldBe((true, true, TrackId, "Fake Song"));
        refusal.Message.ShouldBe("This text matches the lyrics of Fake Song. The assistant works on your own lyrics only.");
        _session.Run.ShouldBeNull();
    }

    [Theory]
    [InlineData(LyricAssistProblemTypes.Imitation, "{}", "The assistant does not write like a named artist. Describe the mood or the theme instead.")]
    [InlineData(LyricAssistProblemTypes.AssistActive, "{}", "A check is running. Wait for it or cancel it.")]
    [InlineData(LyricAssistProblemTypes.ModelUnavailable, """{"reason":"Ollama is not running.","fix":"Start Ollama."}""", "Ollama is not running.")]
    [InlineData(LyricAssistProblemTypes.ModelUnavailable, "{}", "Problem model-unavailable")]
    [InlineData(LyricAssistProblemTypes.LanguageUnsupported, "{}", "Problem language-unsupported")]
    public async Task Refusals_AreReadable(string code, string extensions, string message)
    {
        _api.CreateLyricAssistAsync(LyricsId, Arg.Any<CreateLyricAssistRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(Problem(code, extensions));

        await StartAsync();

        (_session.Refusal!.Code, _session.Refusal.Message, _session.Error).ShouldBe((code, message, null));
    }

    [Fact]
    public async Task OtherErrors_AreErrors_NotRefusals()
    {
        _api.CreateLyricAssistAsync(LyricsId, Arg.Any<CreateLyricAssistRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(new ApiException(ApiError.Unreachable));

        await StartAsync();

        (_session.Refusal, _session.Error).ShouldBe((null, ApiError.Unreachable.UserMessage));
    }

    [Fact]
    public async Task Stream_FillsTheRun_AndAnnouncesTheEnd()
    {
        var changes = 0;
        _session.Changed += () => changes++;
        _api.StreamLyricAssistEventsAsync(AssistId, Arg.Any<int?>(), Arg.Any<CancellationToken>()).Returns(Events(
            (LyricAssistEventTypes.Started, new LyricAssistStartedEventDto { AssistId = AssistId, Layers = [Layer(SuggestionSourceDto.Dictionary, "OpenTaal")], Backend = "ollama", Model = "qwen3.5:4b", UsingFallback = true, BlocksTotal = 2, EstimatedSeconds = 120 }),
            (LyricAssistEventTypes.Progress, new LyricAssistProgressEventDto { BlocksDone = 1, BlocksSkipped = 0, BlocksTotal = 2 }),
            (LyricAssistEventTypes.SuggestionAdded, GrammarFix()),
            (LyricAssistEventTypes.Note, new LyricAssistNoteEventDto { Code = AssistNoteCodeDto.VerbatimWithheld, Message = "withheld" }),
            (LyricAssistEventTypes.UsageUpdated, new TokenUsageDto { InputTokens = 10, OutputTokens = 2, CachedInputTokens = 0 }),
            (LyricAssistEventTypes.Completed, Completed(spelling: 1, grammar: 1))));

        await StartAsync();

        var run = _session.Run.ShouldNotBeNull();
        (run.Status, run.Model, run.UsingFallback, run.EstimatedSeconds, run.Percent, run.Withheld, run.Usage!.InputTokens, run.DurationMs)
            .ShouldBe((AssistStatusDto.Completed, "qwen3.5:4b", true, 120d, 100, 1, 10, 1200L));
        _session.Board.Open.Select(s => s.SuggestionId).ShouldBe([Spelling, Grammar]);
        _session.Live.ShouldBe("Check finished: 2 suggestions - 1 spelling, 1 grammar");
        changes.ShouldBe(6);
        await _api.DidNotReceive().GetLyricAssistAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Stream_Failed_ShowsTheMessage()
    {
        _api.StreamLyricAssistEventsAsync(AssistId, Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(Events((LyricAssistEventTypes.Failed, new LyricAssistFailedEventDto { Code = "timeout", Message = "The model took too long.", Fix = "Try again." })));

        await StartAsync();

        (_session.Run!.Status, _session.Run.Failure!.Fix, _session.Live).ShouldBe((AssistStatusDto.Failed, "Try again.", "The model took too long."));
    }

    [Fact]
    public async Task Stream_Cancelled_IsAnnounced()
    {
        _api.StreamLyricAssistEventsAsync(AssistId, Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(Events((LyricAssistEventTypes.Cancelled, new LyricAssistCancelledEventDto { AssistId = AssistId })));

        await StartAsync();

        (_session.Run!.Status, _session.Live).ShouldBe((AssistStatusDto.Cancelled, "Check cancelled"));
    }

    [Fact]
    public async Task Stream_EndingEarly_IsCompletedFromTheSnapshot()
    {
        _api.GetLyricAssistAsync(AssistId, Arg.Any<CancellationToken>()).Returns(Assist(AssistStatusDto.Completed, AssistKindDto.Check, SpellingFix(), GrammarFix()) with
        {
            Counts = CategoryCountsDto.None with { Spelling = 1, Grammar = 1 },
        });

        await StartAsync();

        (_session.Run!.Status, _session.Board.Open.Count, _session.Live).ShouldBe((AssistStatusDto.Completed, 2, "Check finished: 2 suggestions - 1 spelling, 1 grammar"));
    }

    [Fact]
    public async Task Stream_Breaking_FallsBackToTheSnapshot()
    {
        _api.StreamLyricAssistEventsAsync(AssistId, Arg.Any<int?>(), Arg.Any<CancellationToken>()).Returns(_ => Broken());

        await StartAsync();

        _session.Run!.Status.ShouldBe(AssistStatusDto.Completed);
    }

    [Fact]
    public async Task Stream_BreakingWithoutASnapshot_KeepsTheRun()
    {
        _api.StreamLyricAssistEventsAsync(AssistId, Arg.Any<int?>(), Arg.Any<CancellationToken>()).Returns(_ => Broken());
        _api.GetLyricAssistAsync(AssistId, Arg.Any<CancellationToken>()).ThrowsAsync(new ApiException(ApiError.Unreachable));

        await StartAsync();

        _session.Run!.Status.ShouldBe(AssistStatusDto.Queued);
    }

    [Fact]
    public async Task Cancel_SendsTheCancel_OnlyWhileRunning()
    {
        _api.StreamLyricAssistEventsAsync(AssistId, Arg.Any<int?>(), Arg.Any<CancellationToken>()).Returns(_ => Never());
        _api.CancelLyricAssistAsync(AssistId, Arg.Any<CancellationToken>()).Returns(Assist(AssistStatusDto.Cancelled));
        await _session.LoadStatusAsync();
        await _session.StartAsync(Check);

        await _session.CancelAsync();
        await _session.CancelAsync();

        await _api.Received(1).CancelLyricAssistAsync(AssistId, Arg.Any<CancellationToken>());
        _session.Run!.Status.ShouldBe(AssistStatusDto.Cancelled);
    }

    [Fact]
    public async Task Open_ShowsAnExistingAssist_OfThisSheetOnly()
    {
        await _session.OpenAsync(AssistId);
        _session.Run!.AssistId.ShouldBe(AssistId);

        var other = new LyricAssistSession(_api, new EditorDocument(Sheet() with { Id = Guid.NewGuid() }));
        await other.OpenAsync(AssistId);
        other.Run.ShouldBeNull();
    }

    [Fact]
    public void Language_DisablesOtherLanguages_WithTheReason()
    {
        new LyricAssistSession(_api, new EditorDocument(Sheet("de"))).LanguageReason.ShouldBe("The assistant works on Dutch and English lyrics only.");
        new LyricAssistSession(_api, new EditorDocument(Sheet("unknown"))).LanguageReason!.ShouldStartWith("Choose the language");
        _session.LanguageReason.ShouldBeNull();
    }

    [Theory]
    [InlineData(AgentBackendDto.Ollama, true, true)]
    [InlineData(AgentBackendDto.Ollama, false, false)]
    [InlineData(AgentBackendDto.None, true, false)]
    public async Task ModelReady_NeedsAReadyBackend(AgentBackendDto backend, bool ready, bool expected)
    {
        _api.GetLyricAssistStatusAsync(Arg.Any<CancellationToken>()).Returns(Status(backend, ready));

        await _session.LoadStatusAsync();

        _session.ModelReady.ShouldBe(expected);
    }

    private static async IAsyncEnumerable<LyricAssistEvent> Broken()
    {
        await Task.Yield();
        throw new IOException("connection reset");
#pragma warning disable CS0162 // An iterator needs a yield.
        yield break;
#pragma warning restore CS0162
    }

    private static async IAsyncEnumerable<LyricAssistEvent> Never()
    {
        await Task.Delay(Timeout.Infinite, TestContext.Current.CancellationToken);
        yield break;
    }
}
