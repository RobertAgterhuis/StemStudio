using StemStudio.Contracts.Generation;

namespace StemStudio.Web.Generation;

/// <summary>Readable texts and state rules of generations (features 009 and 010).</summary>
public static class GenerationText
{
    private static readonly Dictionary<GenerationStateDto, string> StateLabels = new()
    {
        [GenerationStateDto.Queued] = "Queued",
        [GenerationStateDto.WaitingForStems] = "Preparing the stems",
        [GenerationStateDto.WaitingForGpu] = "Waiting for the GPU",
        [GenerationStateDto.DownloadingModel] = "Downloading the model",
        [GenerationStateDto.Running] = "Running",
        [GenerationStateDto.Completed] = "Completed",
        [GenerationStateDto.Failed] = "Failed",
        [GenerationStateDto.Cancelled] = "Cancelled",
    };

    /// <summary>The readable status, e.g. "Waiting for the GPU".</summary>
    /// <param name="state">The status.</param>
    /// <returns>The label.</returns>
    public static string Label(GenerationStateDto state) => StateLabels[state];

    /// <summary>True while the generation is queued, waiting, downloading or running (it can be cancelled and is polled).</summary>
    /// <param name="state">The status.</param>
    /// <returns>Whether it is still in progress.</returns>
    public static bool IsActive(GenerationStateDto state) => !IsTerminal(state);

    /// <summary>True for completed, failed and cancelled.</summary>
    /// <param name="state">The status.</param>
    /// <returns>Whether nothing changes any more.</returns>
    public static bool IsTerminal(GenerationStateDto state) =>
        state is GenerationStateDto.Completed or GenerationStateDto.Failed or GenerationStateDto.Cancelled;

    /// <summary>True for failed and cancelled generations (they can be retried with new seeds).</summary>
    /// <param name="state">The status.</param>
    /// <returns>Whether retry is offered.</returns>
    public static bool CanRetry(GenerationStateDto state) => state is GenerationStateDto.Failed or GenerationStateDto.Cancelled;
}
