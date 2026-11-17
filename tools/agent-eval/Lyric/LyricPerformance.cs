using System.Diagnostics;
using System.Globalization;
using System.Text;
using StemStudio.Agent.LyricAssist;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.LyricAssist;

namespace StemStudio.AgentEval.Lyric;

/// <summary>Timing of one performance run (023 T008 spike S-01 / SC-004).</summary>
/// <param name="FirstBlockSeconds">Time until the first block result.</param>
/// <param name="TotalSeconds">Time of the whole sheet.</param>
/// <param name="BlockSeconds">Time per block.</param>
/// <param name="Replies">Model replies.</param>
/// <param name="ValidReplies">Replies that passed the schema parser.</param>
/// <param name="Suggestions">Validated suggestions.</param>
internal sealed record PerformanceRun(double FirstBlockSeconds, double TotalSeconds, IReadOnlyList<double> BlockSeconds, int Replies, int ValidReplies, int Suggestions);

/// <summary>
/// The performance sheet run (<c>--suite lyric --sheet path</c>): the sheet is split into blocks of at most
/// <see cref="LyricAssistLimits.DefaultBlockLines"/> lines at blank lines (an approximation of the Domain <c>SheetBlocks</c>) and
/// checked by the real assistant; first-block and total time, per-block time and the JSON validity rate are reported.
/// </summary>
internal static class LyricPerformance
{
    /// <summary>The blocks of a sheet: runs of lines between blank lines, cut at the block size.</summary>
    /// <param name="text">Sheet text.</param>
    /// <param name="blockLines">Lines per block.</param>
    /// <returns>The blocks.</returns>
    public static IReadOnlyList<LyricBlock> Blocks(string text, int blockLines)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var blocks = new List<LyricBlock>();
        var start = -1;
        void Close(int last)
        {
            if (start >= 0)
            {
                blocks.Add(new LyricBlock { Index = blocks.Count, FirstLine = start + 1, LastLine = last + 1 });
            }
        }

        for (var index = 0; index < lines.Length; index++)
        {
            if (lines[index].Trim().Length == 0)
            {
                Close(index - 1);
                start = -1;
            }
            else if (start < 0)
            {
                start = index;
            }
            else if (index - start == blockLines)
            {
                Close(index - 1);
                start = index;
            }
        }

        Close(lines.Length - 1);
        return blocks;
    }

    /// <summary>Runs the sheet once.</summary>
    /// <param name="text">Sheet text.</param>
    /// <param name="language">Language.</param>
    /// <param name="model">Model.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The timing.</returns>
    public static async Task<PerformanceRun> RunAsync(string text, AssistLanguageDto language, ILyricModel model, CancellationToken cancellationToken)
    {
        var counting = new CountingModel(model);
        var sink = new TimingSink();
        var job = new LyricAssistJob
        {
            AssistId = Guid.NewGuid(),
            Kind = AssistKindDto.Check,
            Language = language,
            Sensitivity = SensitivityDto.Balanced,
            Title = "Performance",
            Text = text.Replace("\r\n", "\n", StringComparison.Ordinal),
            Blocks = Blocks(text, LyricAssistLimits.DefaultBlockLines),
        };
        await new LyricAssistant(counting, new HarnessVerification()).RunAsync(job, sink, cancellationToken);
        var blocks = sink.Times.Zip(sink.Times.Prepend(0)).Select(pair => pair.First - pair.Second).ToList();
        return new PerformanceRun(sink.Times.FirstOrDefault(), sink.Times.LastOrDefault(), blocks, counting.Replies, counting.Valid, sink.Suggestions);
    }

    /// <summary>The report of several runs.</summary>
    /// <param name="title">Title.</param>
    /// <param name="runs">The runs.</param>
    /// <returns>Markdown.</returns>
    public static string Report(string title, IReadOnlyList<PerformanceRun> runs)
    {
        var text = new StringBuilder().AppendLine(CultureInfo.InvariantCulture, $"# Lyric performance: {title}").AppendLine()
            .AppendLine("| Run | First block s | Total s | Blocks | Per block s (min/median/max) | Valid JSON | Suggestions |").AppendLine("|---|---|---|---|---|---|---|");
        foreach (var (run, index) in runs.Select((run, index) => (run, index + 1)))
        {
            var sorted = run.BlockSeconds.Order().ToList();
            var median = sorted.Count == 0 ? 0 : sorted[sorted.Count / 2];
            text.AppendLine(CultureInfo.InvariantCulture,
                $"| {index} | {run.FirstBlockSeconds:0.0} | {run.TotalSeconds:0.0} | {run.BlockSeconds.Count} | {sorted.FirstOrDefault():0.0} / {median:0.0} / {sorted.LastOrDefault():0.0} | {run.ValidReplies}/{run.Replies} | {run.Suggestions} |");
        }

        return text.ToString();
    }

    private sealed class CountingModel(ILyricModel inner) : ILyricModel, ILyricModelSelection
    {
        public int Replies { get; private set; }

        public int Valid { get; private set; }

        public Task<LyricModelInfo> SelectAsync(CancellationToken cancellationToken) =>
            inner is ILyricModelSelection selection ? selection.SelectAsync(cancellationToken) : Task.FromResult(new LyricModelInfo { Backend = AgentBackendDto.Fake, Model = "plain", UsingFallback = false });

        public async Task<LyricModelReply> CompleteAsync(LyricModelRequest request, CancellationToken cancellationToken)
        {
            var reply = await inner.CompleteAsync(request, cancellationToken);
            Replies++;
            Valid += LyricOutputParser.Parse(reply.Json) is null ? 0 : 1;
            return reply;
        }
    }

    private sealed class TimingSink : ILyricAssistSink
    {
        private readonly Stopwatch _watch = new();

        public List<double> Times { get; } = [];

        public int Suggestions { get; private set; }

        public Task StartedAsync(LyricModelInfo model, CancellationToken cancellationToken)
        {
            _watch.Start();
            return Task.CompletedTask;
        }

        public Task BlockCompletedAsync(LyricBlockResult result, CancellationToken cancellationToken)
        {
            Times.Add(_watch.Elapsed.TotalSeconds);
            Suggestions += result.Suggestions.Count;
            return Task.CompletedTask;
        }

        public Task NoteAsync(AssistNoteCodeDto code, string message, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task UsageAsync(TokenUsageDto usage, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
