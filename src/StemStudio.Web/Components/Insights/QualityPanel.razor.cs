using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Insights;

namespace StemStudio.Web.Components.Insights;

/// <summary>
/// The stem quality card (020 US1, FR-001..FR-004, FR-016): loads <c>getQualityReport</c>, polls it while queued or
/// running (the backend starts a report on its own after every separation), and requests a new one with
/// <c>requestQualityReport</c>. A failed run keeps the last ready stems, which stay visible.
/// </summary>
public sealed partial class QualityPanel
{
    /// <summary>The completed track of the page.</summary>
    [Parameter, EditorRequired]
    public TrackDto Track { get; set; } = default!;

    /// <summary>What the tip buttons do (US2); null shows tips without buttons.</summary>
    [Parameter]
    public ITipActions? Actions { get; set; }

    /// <inheritdoc />
    protected override bool NeedsPolling => false;

    /// <inheritdoc />
    protected override Task RefreshAsync(CancellationToken ct) => Task.CompletedTask;
}
