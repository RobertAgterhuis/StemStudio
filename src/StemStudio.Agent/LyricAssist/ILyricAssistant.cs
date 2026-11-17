namespace StemStudio.Agent.LyricAssist;

/// <summary>
/// The tool-less lyric assistant (ADR-037 D1, contracts/DELTA.md section 2): runs the model layer of one assist block by block and
/// reports validated suggestions to the sink. It never touches persistence and never sees anything but the job.
/// </summary>
public interface ILyricAssistant
{
    /// <summary>Runs the model layer of an assist; returns when every block is done, skipped or the run is cancelled.</summary>
    /// <param name="job">The assist (sheet text, blocks, request).</param>
    /// <param name="sink">Receives the model choice, block results, notes and usage, in order.</param>
    /// <param name="cancellationToken">Cancels between blocks and inside a model call.</param>
    /// <returns>A task that completes when the run ends.</returns>
    /// <exception cref="LyricAssistException">The assist failed (backend unavailable, budget exhausted, refusal, timeout).</exception>
    Task RunAsync(LyricAssistJob job, ILyricAssistSink sink, CancellationToken cancellationToken);
}
