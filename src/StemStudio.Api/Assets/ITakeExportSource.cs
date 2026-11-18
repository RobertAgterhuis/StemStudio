using StemStudio.Domain.Rendering;

namespace StemStudio.Api.Assets;

/// <summary>The take of an export, resolved (feature 015, data-model 2.7).</summary>
/// <param name="Take">The take for the render source, or null when the export has none.</param>
/// <param name="Problem">404, 409 <c>take-not-ready</c> or 422 <c>take-not-in-track</c>, else null.</param>
public sealed record ResolvedTake(RenderTake? Take, IResult? Problem)
{
    /// <summary>No take.</summary>
    public static ResolvedTake None { get; } = new(null, null);
}

/// <summary>Resolves the take an export names (feature 015); implemented by the recording module.</summary>
public interface ITakeExportSource
{
    /// <summary>Resolves a take id of an export of a track.</summary>
    /// <param name="takeId">The take, or null.</param>
    /// <param name="trackId">The exported track.</param>
    /// <param name="fromSavedMix">True when the id comes from a saved mix (a stale id is ignored).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The take or the problem.</returns>
    Task<ResolvedTake> ResolveAsync(Guid? takeId, Guid trackId, bool fromSavedMix, CancellationToken cancellationToken);
}
