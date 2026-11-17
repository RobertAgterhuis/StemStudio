using System.Text.Json.Nodes;
using StemStudio.Agent.LyricAssist;
using StemStudio.Agent.LyricAssist.Fakes;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.LyricAssist;
using static StemStudio.Agent.Tests.LyricAssist.LyricFixtures;

namespace StemStudio.Agent.Tests.LyricAssist;

/// <summary>023 T031: LyricAssistant.RunAsync (blocks in order, streaming, notes, retry, budget, cancellation, adversarial model; SC-003, SC-005).</summary>
public sealed class LyricAssistantTests
{
    private const string Script = """
        {"version":1,"blocks":[
          {"items":[],"misses":[{"line":3,"word":"fluisterd","verdict":"typo","correction":"fluistert"}]},
          {"items":[
            {"type":"licence.archaism","severity":"idea","line":8,"original":"Gij zijt","proposal":null,"explanation":"Archaïsch, bewust."},
            {"type":"grammar.verb-form","severity":"error","line":10,"original":"word","proposal":"wordt","explanation":"Derde persoon: wordt."}]},
          {"items":[],"misses":[{"line":14,"word":"vergeeten","verdict":"typo","correction":"vergeten"},{"line":15,"word":"Zelmara","verdict":"keep"}]}
        ]}
        """;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static LyricAssistant Assistant(ILyricModel model, LyricAssistOptions? options = null, TimeProvider? time = null) =>
        new(model, new FakeVerification(), options, time);

    [Fact]
    public async Task Check_RunsTheBlocksInOrder_AndStreamsEachResult()
    {
        var model = new ScriptedLyricModel(LyricScript.Parse(Script));
        var sink = new RecordingLyricSink();

        await Assistant(model).RunAsync(Job(), sink, Token);

        sink.Order.ShouldBe(["started", "block 0", "block 1", "block 2"]);
        sink.Model.ShouldBe(ScriptedLyricModel.Info);
        sink.Blocks[0].Misses.Single().Correction.ShouldBe("fluistert");
        sink.Blocks[1].Suggestions.Select(suggestion => suggestion.Type).ShouldBe([SuggestionTypeDto.LicenceArchaism, SuggestionTypeDto.GrammarVerbForm]);
        sink.Blocks[2].Misses.Select(miss => miss.Verdict).ShouldBe([MissVerdict.Typo, MissVerdict.Keep]);
        sink.Usage.ShouldBeEmpty();
        model.Requests.Select(request => request.BlockIndex).ShouldBe([0, 1, 2]);
        model.Requests.ShouldAllBe(request => request.Temperature == 0.2 && request.Kind == AssistKindDto.Check && request.MaxOutputTokens == null);
        model.Requests[1].UserPrompt.ShouldContain("*8| Gij zijt");
    }

    [Fact]
    public async Task EveryRequest_HasItsOwnRandomBoundary()
    {
        var model = new ScriptedLyricModel(LyricScript.Parse(Script));

        await Assistant(model).RunAsync(Job(), new RecordingLyricSink(), Token);

        model.Requests.Select(request => request.UserPrompt.Split('\n')[0]).Distinct().Count().ShouldBe(3);
    }

    [Fact]
    public async Task InvalidOutput_IsRetriedOnceStricter_ThenTheBlockIsSkippedWithANote()
    {
        var model = new SequenceModel("not json", "{\"items\":{}}", Script2(), Script2(), Script2());
        var sink = new RecordingLyricSink();

        await Assistant(model).RunAsync(Job(), sink, Token);

        sink.Blocks[0].Skipped.ShouldBeTrue();
        sink.Blocks[1].Skipped.ShouldBeFalse();
        sink.Notes.Single().Code.ShouldBe(AssistNoteCodeDto.BlockSkipped);
        sink.Notes.Single().Message.ShouldBe("Lines 1-6 were skipped: the model did not answer in the expected format.");
        model.Requests[1].SystemPrompt.ShouldEndWith(LyricPromptBuilder.StricterInstruction);
        model.Requests[0].SystemPrompt.ShouldNotContain(LyricPromptBuilder.StricterInstruction);
        sink.Order.ShouldBe(["started", "block 0", "note BlockSkipped", "block 1", "block 2"]);
    }

    [Fact]
    public async Task AValidRetry_IsUsed()
    {
        var model = new SequenceModel("oops", Script2(), Script2(), Script2());
        var sink = new RecordingLyricSink();

        await Assistant(model).RunAsync(Job(), sink, Token);

        sink.Blocks.ShouldAllBe(block => !block.Skipped);
        sink.Notes.ShouldBeEmpty();
    }

    [Fact]
    public async Task ATimedOutBlock_IsSkipped_AndTheNextBlockRuns()
    {
        var model = new HangingFirstModel();
        var options = new LyricAssistOptions { LocalBlockTimeout = TimeSpan.FromMilliseconds(50) };
        var sink = new RecordingLyricSink();

        await Assistant(model, options).RunAsync(Job(), sink, Token);

        sink.Blocks.Select(block => block.Skipped).ShouldBe([true, false, false]);
        sink.Notes.Single().Message.ShouldBe("Lines 1-6 were skipped: the model took too long.");
    }

    [Fact]
    public async Task Cancellation_StopsBetweenBlocks()
    {
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var sink = new CancellingSink(cancel);

        await Should.ThrowAsync<OperationCanceledException>(() => Assistant(new ScriptedLyricModel(LyricScript.Parse(Script))).RunAsync(Job(), sink, cancel.Token));

        sink.Blocks.Count.ShouldBe(1);
    }

    [Fact]
    public async Task TheFallbackModel_IsAnnouncedAsANote()
    {
        var sink = new RecordingLyricSink();

        await Assistant(new SequenceModel(Fallback, Script2(), Script2(), Script2())).RunAsync(Job(), sink, Token);

        sink.Notes.First().ShouldBe((AssistNoteCodeDto.FallbackModel, LyricAssistant.FallbackNote));
        sink.Order[1].ShouldBe("note FallbackModel");
    }

    [Fact]
    public async Task ClaudeUsage_IsSummedAndReported_AndTheBudgetStopsTheAssist()
    {
        var claude = new LyricModelInfo { Backend = AgentBackendDto.Claude, Model = "claude-opus-5-5", UsingFallback = false };
        var model = new SequenceModel(claude, Script2(), Script2(), Script2()) { Usage = new TokenUsageDto { InputTokens = 1500, OutputTokens = 300, CachedInputTokens = 10 } };
        var options = new LyricAssistOptions { MaxInputTokensPerAssist = 3000, MaxOutputTokensPerAssist = 1000 };
        var sink = new RecordingLyricSink();

        var error = await Should.ThrowAsync<LyricAssistException>(() => Assistant(model, options).RunAsync(Job(), sink, Token));

        error.Code.ShouldBe(AssistFailureCodeDto.BudgetExhausted);
        error.Message.ShouldContain("3000 input");
        sink.Blocks.Count.ShouldBe(2);
        sink.Usage.Select(usage => usage.InputTokens).ShouldBe([1500, 3000]);
        sink.Usage[^1].CachedInputTokens.ShouldBe(20);
        model.Requests.Select(request => request.MaxOutputTokens).ShouldBe([1000, 700]);
    }

    [Fact]
    public async Task VerbatimWithheld_IsANote()
    {
        var verification = new FakeVerification();
        verification.Library.Add("wordt");
        var sink = new RecordingLyricSink();

        await new LyricAssistant(new ScriptedLyricModel(LyricScript.Parse(Script)), verification).RunAsync(Job(), sink, Token);

        sink.Blocks[1].Discarded.Verbatim.ShouldBe(1);
        sink.Notes.Single().Code.ShouldBe(AssistNoteCodeDto.VerbatimWithheld);
    }

    [Theory]
    [InlineData(AssistKindDto.Check, false)]
    [InlineData(AssistKindDto.Devices, false)]
    [InlineData(AssistKindDto.Alternatives, true)]
    [InlineData(AssistKindDto.Continue, true)]
    public async Task TheAdversarialModel_NeverProducesASuggestion(AssistKindDto kind, bool brokenFirst)
    {
        var model = new AdversarialLyricModel { BrokenFirst = brokenFirst };
        var sink = new RecordingLyricSink();
        var job = Job(kind) with { LineFrom = 3, LineTo = 3, Count = 2 };

        await Assistant(model).RunAsync(job, sink, Token);

        sink.Blocks.ShouldAllBe(block => block.Suggestions.Count == 0 && block.Misses.Count == 0);
        sink.Blocks.Sum(block => block.Discarded.InvalidAnchor + block.Discarded.InvalidSchema + block.Discarded.FailedVerification).ShouldBeGreaterThan(30);
        model.Requests.Count.ShouldBe(brokenFirst ? 6 : 3);
    }

    [Fact]
    public async Task NullArguments_AreRejected_AndModelsWithoutSelectionReportTheScriptedInfo()
    {
        var assistant = Assistant(new SequenceModel(Script2()));

        await Should.ThrowAsync<ArgumentNullException>(() => assistant.RunAsync(null!, new RecordingLyricSink(), Token));
        await Should.ThrowAsync<ArgumentNullException>(() => assistant.RunAsync(Job(), null!, Token));
        var sink = new RecordingLyricSink();
        await Assistant(new PlainModel()).RunAsync(Job() with { Blocks = [Block0] }, sink, Token);
        sink.Model.ShouldBe(ScriptedLyricModel.Info);
        AdversarialLyricModel.Hostile()["items"]!.AsArray().Count.ShouldBeLessThanOrEqualTo(40);
    }

    private static readonly LyricModelInfo Fallback = new() { Backend = AgentBackendDto.Ollama, Model = "qwen3.5:4b", UsingFallback = true };

    private static string Script2() => new JsonObject { ["items"] = new JsonArray() }.ToJsonString();

    /// <summary>Answers the given texts in order (the last one repeats) with a fixed model and optional usage.</summary>
    private sealed class SequenceModel(LyricModelInfo info, params string[] outputs) : ILyricModel, ILyricModelSelection
    {
        public SequenceModel(params string[] outputs)
            : this(ScriptedLyricModel.Info, outputs)
        {
        }

        public List<LyricModelRequest> Requests { get; } = [];

        public TokenUsageDto? Usage { get; init; }

        public Task<LyricModelInfo> SelectAsync(CancellationToken cancellationToken) => Task.FromResult(info);

        public Task<LyricModelReply> CompleteAsync(LyricModelRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var json = outputs[Math.Min(Requests.Count - 1, outputs.Length - 1)];
            return Task.FromResult(new LyricModelReply { Json = json, Model = info, Usage = Usage });
        }
    }

    private sealed class HangingFirstModel : ILyricModel
    {
        private int _calls;

        public async Task<LyricModelReply> CompleteAsync(LyricModelRequest request, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _calls) == 1)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            return new LyricModelReply { Json = Script2(), Model = ScriptedLyricModel.Info };
        }
    }

    private sealed class PlainModel : ILyricModel
    {
        public Task<LyricModelReply> CompleteAsync(LyricModelRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new LyricModelReply { Json = Script2(), Model = ScriptedLyricModel.Info });
    }

    private sealed class CancellingSink(CancellationTokenSource cancel) : ILyricAssistSink
    {
        public List<LyricBlockResult> Blocks { get; } = [];

        public Task StartedAsync(LyricModelInfo model, CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task BlockCompletedAsync(LyricBlockResult result, CancellationToken cancellationToken)
        {
            Blocks.Add(result);
            await cancel.CancelAsync();
        }

        public Task NoteAsync(AssistNoteCodeDto code, string message, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task UsageAsync(TokenUsageDto usage, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
