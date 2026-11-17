using StemStudio.Contracts.Agent;
using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Agent.LyricAssist;

/// <summary>
/// Where the assistant reports (implemented by the backend's <c>LyricAssistService</c>, which applies the Domain
/// <c>SuggestionFilter</c> (sensitivity, intentional marks, personal words), persists suggestions and events and streams them).
/// </summary>
public interface ILyricAssistSink
{
    /// <summary>Called once before the first model call.</summary>
    /// <param name="model">The backend and model that will run.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task StartedAsync(LyricModelInfo model, CancellationToken cancellationToken);

    /// <summary>Called once per block, in block order, also for a skipped block.</summary>
    /// <param name="result">Validated suggestions, miss verdicts and discarded counts of the block.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task BlockCompletedAsync(LyricBlockResult result, CancellationToken cancellationToken);

    /// <summary>A note for the pane (block skipped, fallback model, quality note, verbatim withheld).</summary>
    /// <param name="code">Note code.</param>
    /// <param name="message">Readable message (at most 300 characters).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task NoteAsync(AssistNoteCodeDto code, string message, CancellationToken cancellationToken);

    /// <summary>Running Claude token usage of the assist (not called for local models).</summary>
    /// <param name="usage">Usage so far.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task UsageAsync(TokenUsageDto usage, CancellationToken cancellationToken);
}
