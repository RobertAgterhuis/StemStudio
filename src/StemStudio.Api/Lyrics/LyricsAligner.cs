using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Lyrics;

/// <summary>The automatic alignment rule and the submission of alignment jobs (feature 006 US2).</summary>
public sealed class LyricsAligner
{
    /// <summary>Evaluates the automatic alignment rule of alignPending lyrics.</summary>
    /// <param name="track">The track.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public Task EvaluateAutoAsync(Track track, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Submits the alignment job of aligning lyrics without a job.</summary>
    /// <param name="track">The track.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public Task SubmitAsync(Track track, CancellationToken cancellationToken) => Task.CompletedTask;
}
