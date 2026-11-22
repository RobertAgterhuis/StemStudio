using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Web.Api;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.InstrumentViews;

/// <summary>A key region with its index.</summary>
/// <param name="Index">0-based index.</param>
/// <param name="Region">The region.</param>
public sealed record CurrentRegion(int Index, KeyViewRegionDto Region);

/// <summary>
/// Loads the key view for the transpose, the written instrument and the chord status, and follows the play position to the
/// current region without engine interop (the regions carry their times).
/// </summary>
public sealed partial class MixerKey
{
    private (int Transpose, WrittenInstrumentDto? Instrument, AnalysisStatusDto Status)? _loaded;

    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    /// <summary>The track.</summary>
    [Parameter, EditorRequired]
    public Guid TrackId { get; set; }

    /// <summary>The 012 transpose in semitones.</summary>
    [Parameter]
    public int TransposeSemitones { get; set; }

    /// <summary>The play position in seconds.</summary>
    [Parameter]
    public double Position { get; set; }

    /// <summary>The written instrument of the orchestral family, or null.</summary>
    [Parameter]
    public WrittenInstrumentDto? Instrument { get; set; }

    /// <summary>The chord analysis status (the key comes from it; a change loads the key again).</summary>
    [Parameter]
    public AnalysisStatusDto ChordStatus { get; set; }

    private KeyViewDto? View { get; set; }

    private string Status => WireName.Of(View?.Status ?? ChordStatus);

    private CurrentRegion? Current
    {
        get
        {
            var ms = Position * 1000;
            var regions = View?.Regions ?? [];
            var index = regions.ToList().FindIndex(r => r.StartMs <= ms && ms < r.EndMs);
            return index < 0 ? null : new CurrentRegion(index, regions[index]);
        }
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        var wanted = (TransposeSemitones, Instrument, ChordStatus);
        if (_loaded != wanted)
        {
            _loaded = wanted;
            View = await LoadAsync();
        }
    }

    private async Task<KeyViewDto?> LoadAsync()
    {
        try
        {
            return await Api.GetKeyAsync(TrackId, TransposeSemitones, Instrument);
        }
        catch (ApiException)
        {
            return null;
        }
    }
}
