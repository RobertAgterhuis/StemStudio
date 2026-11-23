using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Insights;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Api;
using StemStudio.Web.Insights;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.Insights;

/// <summary>
/// The stem quality card (020 US1, FR-001..FR-004, FR-016): loads <c>getQualityReport</c>, polls it while queued or
/// running (the backend starts a report on its own after every separation), and requests a new one with
/// <c>requestQualityReport</c>. A failed run keeps the last ready stems, which stay visible.
/// </summary>
public sealed partial class QualityPanel
{
    private DetailPhaseStatusDto? _detail;

    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    /// <summary>The completed track of the page.</summary>
    [Parameter, EditorRequired]
    public TrackDto Track { get; set; } = default!;

    /// <summary>What the tip buttons do (US2); null shows tips without buttons.</summary>
    [Parameter]
    public ITipActions? Actions { get; set; }

    private QualityReportDto? Report { get; set; }

    private string? Error { get; set; }

    private bool Requesting { get; set; }

    private bool Active => Report is { } report && QualityLayout.IsActive(report.Status);

    private bool CheckDisabled => Active || Requesting;

    private string CheckText => QualityLayout.CheckText(Report?.Status ?? QualityReportStatusDto.None);

    private string WireStatus => WireName.Of(Report?.Status ?? QualityReportStatusDto.None);

    private IReadOnlyList<QualityGroup> Groups => QualityLayout.Group(Report!.Stems);

    private string LiveText => Report?.Status switch
    {
        QualityReportStatusDto.Queued => "Stem quality check queued.",
        QualityReportStatusDto.Running => "Checking stem quality.",
        QualityReportStatusDto.Ready => "Stem quality ratings are ready.",
        QualityReportStatusDto.Failed => "The stem quality check failed.",
        _ => string.Empty,
    };

    /// <inheritdoc />
    protected override bool NeedsPolling => Active;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await LoadAsync(ct => Api.GetQualityReportAsync(Track.Id, ct), Stopping);
        StartPolling();
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        // The page's track poll sees an automatic report start (Track.quality) before this card does; the tips are
        // built from the track state, so they change when the detail phase changes, even without a new analysis.
        if (TrackChanged() && Report is not null && !Active)
        {
            await LoadAsync(ct => Api.GetQualityReportAsync(Track.Id, ct), Stopping);
            StartPolling();
        }
    }

    // True when the track shows a report start or a new detail phase status since the last parameters.
    private bool TrackChanged()
    {
        var detailChanged = _detail is { } seen && seen != Track.Detail.Status;
        _detail = Track.Detail.Status;
        return detailChanged || (Track.Quality is { } badge && QualityLayout.IsActive(badge.Status));
    }

    /// <inheritdoc />
    protected override Task RefreshAsync(CancellationToken ct) => LoadAsync(token => Api.GetQualityReportAsync(Track.Id, token), ct);

    private async Task CheckAsync()
    {
        Requesting = true;
        await LoadAsync(ct => Api.RequestQualityReportAsync(Track.Id, ct), Stopping);
        Requesting = false;
        StartPolling();
    }

    private async Task LoadAsync(Func<CancellationToken, Task<QualityReportDto>> call, CancellationToken ct)
    {
        try
        {
            Report = await call(ct);
            Error = null;
        }
        catch (ApiException ex)
        {
            Error = ex.Error.UserMessage;
        }
    }
}
