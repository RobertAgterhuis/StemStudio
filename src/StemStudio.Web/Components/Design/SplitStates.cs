using StemStudio.Contracts.Tracks;

namespace StemStudio.Web.Components.Design;

/// <summary>Maps job states to the state of the split visual (ui-test-ids 1.3.0 <c>data-state</c>).</summary>
public static class SplitStates
{
    /// <summary>Standard separation: queued or failed is idle, completed is done, everything else runs.</summary>
    /// <param name="status">The track status.</param>
    /// <returns>The split state.</returns>
    public static SplitState Of(TrackStatusDto status) => status switch
    {
        TrackStatusDto.Completed => SplitState.Done,
        TrackStatusDto.Queued or TrackStatusDto.Failed => SplitState.Idle,
        _ => SplitState.Running,
    };

    /// <summary>Detail phase: running runs, completed is done, every other status is idle.</summary>
    /// <param name="status">The detail phase status.</param>
    /// <returns>The split state.</returns>
    public static SplitState Of(DetailPhaseStatusDto status) => status switch
    {
        DetailPhaseStatusDto.Running => SplitState.Running,
        DetailPhaseStatusDto.Completed => SplitState.Done,
        _ => SplitState.Idle,
    };
}
