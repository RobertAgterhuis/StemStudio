using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Web.Api;
using StemStudio.Web.InstrumentViews;

namespace StemStudio.Web.Components.InstrumentViews;

/// <summary>Corrects one chord segment (<c>editChordSegment</c>); a 422 or 409 answer is shown in the dialog.</summary>
public sealed partial class ChordEditDialog
{
    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    /// <summary>The track.</summary>
    [Parameter, EditorRequired]
    public Guid TrackId { get; set; }

    /// <summary>0-based index of the segment.</summary>
    [Parameter, EditorRequired]
    public int SegmentIndex { get; set; }

    /// <summary>The segment as detected or last edited.</summary>
    [Parameter, EditorRequired]
    public ChordSegmentDto Segment { get; set; } = default!;

    /// <summary>The displayed symbol of the segment (e.g. "Am7/G").</summary>
    [Parameter]
    public string Symbol { get; set; } = string.Empty;

    /// <summary>The chart's vocabulary (simple charts offer major and minor only).</summary>
    [Parameter]
    public ChordVocabularyDto Vocabulary { get; set; }

    /// <summary>Saved; carries the updated analysis.</summary>
    [Parameter]
    public EventCallback<TrackAnalysisDto<ChordChartDocumentDto>> OnSaved { get; set; }

    /// <summary>Cancel or Escape.</summary>
    [Parameter]
    public EventCallback OnCancel { get; set; }

    private int Root { get; set; }

    private ChordQualityDto Quality { get; set; }

    private int Bass { get; set; }

    private bool NoChord => Root < 0;

    private bool Busy { get; set; }

    private string? Error { get; set; }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        Root = Segment.Root ?? -1;
        Quality = Segment.Quality is { } quality && ChordLabels.Qualities(Vocabulary).Contains(quality) ? quality : ChordQualityDto.Maj;
        Bass = Segment.Bass ?? -1;
    }

    private Task OnKeyAsync(KeyboardEventArgs args) => CloseOnEscapeAsync(args, OnCancel);

    private string Label() => NoChord ? "N" : ChordLabels.Harte(Root, Quality, Bass < 0 ? null : Bass);

    private async Task SaveAsync()
    {
        Busy = true;
        try
        {
            var analysis = await Api.EditChordSegmentAsync(TrackId, SegmentIndex, new EditChordRequest { Label = Label() });
            await OnSaved.InvokeAsync(analysis);
        }
        catch (ApiException ex)
        {
            Error = ex.Error.UserMessage;
        }
        finally
        {
            Busy = false;
        }
    }
}
