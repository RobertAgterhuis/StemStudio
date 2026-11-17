using System.Globalization;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Web.Components.Lyrics.Assist;

/// <summary>Texts of the status line: the layer/model line and the unavailable reason with its fix (FR-027, US5).</summary>
public static class StatusText
{
    /// <summary>"Spelling: OpenTaal dictionary - grammar and flow: qwen3.5:9b, local".</summary>
    /// <param name="status">The status.</param>
    /// <param name="run">The newest assist, or null.</param>
    /// <returns>The line.</returns>
    public static string Layers(LyricAssistStatusDto status, AssistRun? run)
    {
        var model = run?.Model ?? status.Model.ActiveModel;
        var backend = status.Model.Backend;
        var right = backend == AgentBackendDto.None || model is null ? "off" : $"{model}, {Where(backend)}";
        var fallback = (run?.UsingFallback ?? status.Model.UsingFallback) ? Fallback(run?.EstimatedSeconds) : string.Empty;
        return $"Spelling: {status.Dictionary.Name} - grammar and flow: {right}{fallback}";
    }

    /// <summary>Why the assistant (or a part of it) is unavailable, with the fix, or null.</summary>
    /// <param name="session">The session.</param>
    /// <returns>The text.</returns>
    public static string? Unavailable(LyricAssistSession session) => session switch
    {
        { LanguageReason: { } reason } => reason,
        { Status: { Dictionary.Available: false } s } => Join("Spelling is unavailable: " + (s.Dictionary.Reason ?? "the dictionary could not be loaded."), s.Dictionary.Fix),
        { Status: { } s, ModelReady: false } => Join("Grammar, flow and writing help need the AI model: " + ModelReason(s.Model), s.Model.Fix),
        _ => null,
    };

    /// <summary>Why the writing buttons are disabled (title text), or null.</summary>
    /// <param name="session">The session.</param>
    /// <returns>The reason.</returns>
    public static string? WritingDisabled(LyricAssistSession session) =>
        session.Status is not null && !session.ModelReady ? "Needs the AI model: " + ModelReason(session.Status.Model) : null;

    private static string ModelReason(LyricAssistModelStatusDto model) =>
        model.Reason ?? (model.Backend == AgentBackendDto.None ? "it is switched off in Settings." : "it is not reachable.");

    private static string Where(AgentBackendDto backend) => backend switch
    {
        AgentBackendDto.Claude => "Claude, online",
        AgentBackendDto.Ollama => "local",
        _ => "test model",
    };

    private static string Fallback(double? seconds) => seconds is { } s
        ? $" (smaller model on the processor while a heavy job runs, about {Math.Max(1, (int)Math.Ceiling(s)).ToString(CultureInfo.InvariantCulture)} s)"
        : " (smaller model on the processor while a heavy job runs)";

    private static string Join(string reason, string? fix) => fix is { Length: > 0 } ? $"{reason} {fix}" : reason;
}
