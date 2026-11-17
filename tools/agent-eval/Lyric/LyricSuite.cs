using System.Diagnostics;
using System.Text.Json.Nodes;
using StemStudio.Agent.LyricAssist;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.LyricAssist;

namespace StemStudio.AgentEval.Lyric;

/// <summary>
/// The lyric suite (023 research R-09, T066/T067): every case of lyric-eval-set.json runs through the real
/// <see cref="LyricAssistant"/> (prompt builder, schema parser, validator) with the given model and is graded by
/// <see cref="LyricGrader"/>. The dictionary layer and the Domain checks live in the API (StemStudio.Agent references no Domain
/// project), so the harness uses <see cref="HarnessVerification"/>: the model layer is measured, not the dictionary baseline.
/// </summary>
internal static class LyricSuite
{
    /// <summary>Runs every case <paramref name="runs"/> times.</summary>
    /// <param name="set">The set.</param>
    /// <param name="model">The model per case.</param>
    /// <param name="runs">Repetitions.</param>
    /// <param name="log">Progress log.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The grades.</returns>
    public static async Task<IReadOnlyList<LyricGrade>> RunAsync(LyricEvalSet set, Func<LyricCase, ILyricModel> model, int runs, TextWriter log, CancellationToken cancellationToken)
    {
        var grades = new List<LyricGrade>();
        foreach (var evalCase in set.Cases)
        {
            for (var run = 0; run < runs; run++)
            {
                var grade = await RunCaseAsync(evalCase, model(evalCase), cancellationToken);
                grades.Add(grade);
                await log.WriteLineAsync($"lyric {evalCase.Id} run {run + 1}: found {grade.Found}/{grade.Expected}, errors {grade.Errors}, trap hits {grade.TrapHits}/{grade.Traps}, creative {grade.CreativePassed?.ToString() ?? "-"}, {grade.Seconds:0.0} s");
            }
        }

        return grades;
    }

    /// <summary>Runs one case.</summary>
    /// <param name="evalCase">The case.</param>
    /// <param name="model">The model.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The grade.</returns>
    public static async Task<LyricGrade> RunCaseAsync(LyricCase evalCase, ILyricModel model, CancellationToken cancellationToken)
    {
        var sink = new CollectingSink();
        var watch = Stopwatch.StartNew();
        try
        {
            await new LyricAssistant(model, new HarnessVerification()).RunAsync(Job(evalCase), sink, cancellationToken);
        }
        catch (LyricAssistException error)
        {
            sink.Failure = error.Message;
        }

        return LyricGrader.Grade(evalCase, sink.Suggestions, watch.Elapsed.TotalSeconds);
    }

    /// <summary>The assist of a case: one block over the whole sheet (every case has at most 8 lines).</summary>
    /// <param name="evalCase">The case.</param>
    /// <returns>The job.</returns>
    public static LyricAssistJob Job(LyricCase evalCase) => new()
    {
        AssistId = Guid.NewGuid(),
        Kind = evalCase.AssistKind,
        Language = evalCase.Language,
        Sensitivity = SensitivityDto.Balanced,
        Title = Title(evalCase),
        Text = evalCase.Text,
        Blocks = [new LyricBlock { Index = 0, FirstLine = 1, LastLine = evalCase.Lines.Count }],
        LineFrom = evalCase.AssistKind == AssistKindDto.Alternatives ? evalCase.Line : null,
        LineTo = evalCase.AssistKind is AssistKindDto.Alternatives or AssistKindDto.Continue ? evalCase.Line : null,
        Count = evalCase.Count,
        RhythmTarget = evalCase.RhythmTarget,
    };

    /// <summary>The sheet title of a case.</summary>
    /// <param name="evalCase">The case.</param>
    /// <returns>The title.</returns>
    public static string Title(LyricCase evalCase) => "Eval " + evalCase.Id;

    /// <summary>
    /// The self-test (CI, no model): the ideal scripted output of every case must reach 100 % recall and precision and 0 % licence
    /// false positives, and a model that flags every trap as an error must be caught.
    /// </summary>
    /// <param name="set">The set.</param>
    /// <param name="log">Log.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The failures (empty when the grading works).</returns>
    public static async Task<IReadOnlyList<string>> SelfTestAsync(LyricEvalSet set, TextWriter log, CancellationToken cancellationToken)
    {
        var ideal = await RunAsync(set, IdealModel, 1, TextWriter.Null, cancellationToken);
        var traps = await RunAsync(set, TrapModel, 1, TextWriter.Null, cancellationToken);
        var failures = new List<string>();
        Expect(failures, LyricGrader.Recall(ideal) == 1, $"ideal recall {LyricGrader.Recall(ideal):P0}");
        Expect(failures, LyricGrader.Precision(ideal) == 1, $"ideal precision {LyricGrader.Precision(ideal):P0}");
        Expect(failures, LyricGrader.LicenceFalsePositives(ideal) == 0, $"ideal licence FP {LyricGrader.LicenceFalsePositives(ideal):P0}");
        Expect(failures, ideal.Where(grade => grade.CreativePassed is not null).All(grade => grade.CreativePassed == true), "an ideal creative case failed");
        Expect(failures, LyricGrader.LicenceFalsePositives(traps) > 0.9, $"trap model licence FP only {LyricGrader.LicenceFalsePositives(traps):P0}");
        foreach (var failure in failures)
        {
            await log.WriteLineAsync("lyric self-test: " + failure);
        }

        await log.WriteLineAsync($"lyric self-test: {set.Cases.Count} cases, {failures.Count} grading failures.");
        return failures;
    }

    /// <summary>A model that answers the ideal output of the case.</summary>
    /// <param name="evalCase">The case.</param>
    /// <returns>The model.</returns>
    public static ILyricModel IdealModel(LyricCase evalCase) => new FixedModel(IdealOutputs.For(evalCase));

    /// <summary>A model that flags every trap span (or the first line) as an error.</summary>
    /// <param name="evalCase">The case.</param>
    /// <returns>The model.</returns>
    public static ILyricModel TrapModel(LyricCase evalCase) => new FixedModel(IdealOutputs.Traps(evalCase));

    private static void Expect(List<string> failures, bool condition, string message)
    {
        if (!condition)
        {
            failures.Add(message);
        }
    }

    private sealed class FixedModel(JsonObject output) : ILyricModel
    {
        public Task<LyricModelReply> CompleteAsync(LyricModelRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new LyricModelReply { Json = output.ToJsonString(), Model = new LyricModelInfo { Backend = AgentBackendDto.Fake, Model = "ideal", UsingFallback = false } });
    }

    private sealed class CollectingSink : ILyricAssistSink
    {
        public List<AssistantSuggestion> Suggestions { get; } = [];

        public string? Failure { get; set; }

        public Task StartedAsync(LyricModelInfo model, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task BlockCompletedAsync(LyricBlockResult result, CancellationToken cancellationToken)
        {
            Suggestions.AddRange(result.Suggestions);
            return Task.CompletedTask;
        }

        public Task NoteAsync(AssistNoteCodeDto code, string message, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task UsageAsync(TokenUsageDto usage, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
