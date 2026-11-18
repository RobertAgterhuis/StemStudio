using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using StemStudio.Contracts;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Web.Api;
using StemStudio.Web.InstrumentViews;

namespace StemStudio.Web.Components.InstrumentViews;

/// <summary>
/// The chord sheet of the track page (016 US1): loads the chord analysis (polled while detecting) and, once ready, the
/// sheet at the display transpose and the capo of capo mode. Corrections reload the sheet.
/// </summary>
public sealed partial class ChordSheet
{
    private int _loadedCapo = -1;

    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    [Inject]
    private IOptions<StemStudioWebOptions> WebOptions { get; set; } = default!;

    /// <summary>The track.</summary>
    [Parameter, EditorRequired]
    public Guid TrackId { get; set; }

    /// <summary>The capo of capo mode (guitar family), 0..12.</summary>
    [Parameter]
    public int Capo { get; set; }

    private TrackAnalysisDto<ChordChartDocumentDto>? Analysis { get; set; }

    private ChordSheetDto? Sheet { get; set; }

    private ChordSheetSegments? Segments { get; set; }

    private int Transpose { get; set; }

    private (int Index, string Symbol)? Editing { get; set; }

    private string? Error { get; set; }

    private string EmptyText => Analysis is { Status: AnalysisStatusDto.Ready, Document.NoChords: true }
        ? "No chords were detected in this song."
        : AnalysisText.Status(Analysis, "Chord detection", "chords");

    private string ChordProUrl => new Uri(WebOptions.Value.PublicApiBaseUrl, ApiRoutes.ChordPro(TrackId, Transpose, Capo)).ToString();

    /// <inheritdoc />
    protected override bool NeedsPolling => AnalysisText.InProgress(Analysis);

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await RefreshAsync(Stopping);
        StartPolling();
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        if (Sheet is not null && Capo != _loadedCapo)
        {
            await LoadSheetAsync(Stopping);
        }
    }

    /// <inheritdoc />
    protected override async Task RefreshAsync(CancellationToken ct)
    {
        try
        {
            Analysis = await Api.GetChordsAsync(TrackId, ct);
            await LoadSheetAsync(ct);
        }
        catch (ApiException ex)
        {
            Error = ex.Error.UserMessage;
        }
    }

    private async Task LoadSheetAsync(CancellationToken ct)
    {
        if (Analysis is not { Status: AnalysisStatusDto.Ready, Document: { NoChords: false } document })
        {
            Sheet = null;
            Segments = null;
            return;
        }

        try
        {
            _loadedCapo = Capo;
            Sheet = await Api.GetChordSheetAsync(TrackId, Transpose, Capo, ct);
            Segments = ChordSheetSegments.Build(Sheet, document);
            Error = null;
        }
        catch (ApiException ex)
        {
            Error = ex.Error.UserMessage;
        }
    }

    private async Task SetTransposeAsync(int value)
    {
        Transpose = Math.Clamp(value, ContractLimits.MinDisplayTranspose, ContractLimits.MaxDisplayTranspose);
        await LoadSheetAsync(Stopping);
    }

    private void OpenEdit(int index, string symbol) => Editing = (index, symbol);

    private void CloseEdit() => Editing = null;

    private async Task OnEditedAsync(TrackAnalysisDto<ChordChartDocumentDto> analysis)
    {
        Analysis = analysis;
        Editing = null;
        await LoadSheetAsync(Stopping);
    }
}
