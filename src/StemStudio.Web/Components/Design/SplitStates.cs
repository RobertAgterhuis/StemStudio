using StemStudio.Contracts.Tracks;

namespace StemStudio.Web.Components.Design;

/// <summary>Maps job states to the split visual. Red stub (T031).</summary>
public static class SplitStates
{
    /// <summary>Stub.</summary>
    /// <param name="status">Status.</param>
    /// <returns>State.</returns>
    public static SplitState Of(TrackStatusDto status) => status == TrackStatusDto.Completed ? SplitState.Idle : SplitState.Idle;
}
