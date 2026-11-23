using StemStudio.Api.Worker;

namespace StemStudio.Api.Handouts;

/// <summary>The size of a handout job as the worker measures it (worker 1.11.0 <c>jobs.handout_limits</c>).</summary>
/// <param name="BodyBytes">The JSON body.</param>
/// <param name="TextChars">Characters of every string of the document.</param>
/// <param name="Cells">Layout cells.</param>
/// <param name="NotationBytes">The distinct notation PDFs together.</param>
public sealed record HandoutJobSize(long BodyBytes, long TextChars, long Cells, long NotationBytes);

/// <summary>Stub (red).</summary>
public static class HandoutJobLimits
{
    /// <summary>Stub.</summary>
    /// <param name="request">The request.</param>
    /// <param name="notationSizes">Sizes.</param>
    /// <returns>The size.</returns>
    public static HandoutJobSize Measure(WorkerHandoutJobRequest request, IReadOnlyDictionary<string, long> notationSizes) => new(0, 0, 0, 0);

    /// <summary>Stub.</summary>
    /// <param name="size">The size.</param>
    /// <returns>Null.</returns>
    public static string? Refusal(HandoutJobSize size) => null;
}
