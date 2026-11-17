using StemStudio.Contracts.LyricAssist;
using StemStudio.Web.Api;

namespace StemStudio.Web.Components.Lyrics.Assist;

/// <summary>
/// The syllable gutter's counter (FR-017): counts the editor text after a typing pause through
/// <c>POST /api/lyric-assist/syllables</c> (local and deterministic, exempt from FR-002). A newer request cancels an older one.
/// </summary>
/// <param name="api">The API.</param>
/// <param name="time">The clock of the pause.</param>
public sealed class SyllableCounter(IStemStudioApiClient api, TimeProvider time) : IDisposable
{
    /// <summary>The typing pause before counting.</summary>
    public static readonly TimeSpan Pause = TimeSpan.FromMilliseconds(600);

    private CancellationTokenSource? _pending;

    /// <summary>The newest report, or null.</summary>
    public SyllableReportDto? Report { get; private set; }

    /// <summary>Counts after the pause unless a newer call comes first.</summary>
    /// <param name="language">The language.</param>
    /// <param name="text">The text.</param>
    /// <param name="target">The rhythm target, or null.</param>
    /// <param name="wait">False counts at once (switching the gutter on).</param>
    /// <returns>True when a new report arrived.</returns>
    public async Task<bool> CountAsync(AssistLanguageDto language, string text, RhythmTargetDto? target, bool wait = true)
    {
        var cts = Replace();
        try
        {
            await Task.Delay(wait ? Pause : TimeSpan.Zero, time, cts.Token);
            Report = await api.CountSyllablesAsync(new SyllableRequest { Language = language, Text = text, RhythmTarget = target }, cts.Token);
            return true;
        }
        catch (Exception ex) when (ex is OperationCanceledException or ApiException)
        {
            return false;
        }
    }

    /// <summary>The count of a 1-based line, or null.</summary>
    /// <param name="line">The line.</param>
    /// <returns>The line report.</returns>
    public SyllableLineDto? Line(int line) => Report?.Lines.FirstOrDefault(l => l.Line == line);

    /// <inheritdoc />
    public void Dispose()
    {
        _pending?.Cancel();
        _pending?.Dispose();
    }

    private CancellationTokenSource Replace()
    {
        Dispose();
        _pending = new CancellationTokenSource();
        return _pending;
    }
}
