using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Web.Services;

namespace StemStudio.Web.Api;

/// <summary>Instrument views (feature 016, backend contract 1.15.0).</summary>
public sealed partial class StemStudioApiClient
{
    /// <inheritdoc />
    public Task<TrackAnalysisDto<ChordChartDocumentDto>> GetChordsAsync(Guid trackId, CancellationToken cancellationToken = default) =>
        SendAsync<TrackAnalysisDto<ChordChartDocumentDto>>(HttpMethod.Get, ApiRoutes.Chords(trackId), null, cancellationToken);

    /// <inheritdoc />
    public Task<TrackAnalysisDto<ChordChartDocumentDto>> DetectChordsAsync(Guid trackId, DetectChordsRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<TrackAnalysisDto<ChordChartDocumentDto>>(HttpMethod.Post, ApiRoutes.Chords(trackId), JsonBody(request), cancellationToken);

    /// <inheritdoc />
    public Task DeleteChordsAsync(Guid trackId, CancellationToken cancellationToken = default) =>
        _transport.SendAsync(HttpMethod.Delete, ApiRoutes.Chords(trackId), null, cancellationToken);

    /// <inheritdoc />
    public Task<TrackAnalysisDto<ChordChartDocumentDto>> EditChordSegmentAsync(
        Guid trackId, int segmentIndex, EditChordRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<TrackAnalysisDto<ChordChartDocumentDto>>(HttpMethod.Put, ApiRoutes.ChordSegment(trackId, segmentIndex), JsonBody(request), cancellationToken);

    /// <inheritdoc />
    public Task<ChordSheetDto> GetChordSheetAsync(Guid trackId, int transpose, int capo, CancellationToken cancellationToken = default) =>
        SendAsync<ChordSheetDto>(HttpMethod.Get, ApiRoutes.ChordSheet(trackId, transpose, capo), null, cancellationToken);

    /// <inheritdoc />
    public Task<KeyViewDto> GetKeyAsync(Guid trackId, int transpose, WrittenInstrumentDto? instrument, CancellationToken cancellationToken = default) =>
        SendAsync<KeyViewDto>(HttpMethod.Get, ApiRoutes.Key(trackId, transpose, instrument is { } id ? WireName.Of(id) : null), null, cancellationToken);

    /// <inheritdoc />
    public Task<CapoSuggestionsDto> GetCapoSuggestionsAsync(Guid trackId, int transpose, CancellationToken cancellationToken = default) =>
        SendAsync<CapoSuggestionsDto>(HttpMethod.Get, ApiRoutes.CapoSuggestions(trackId, transpose), null, cancellationToken);

    /// <inheritdoc />
    public Task<TrackAnalysisDto<DrumHitsDocumentDto>> GetDrumHitsAsync(Guid trackId, CancellationToken cancellationToken = default) =>
        SendAsync<TrackAnalysisDto<DrumHitsDocumentDto>>(HttpMethod.Get, ApiRoutes.DrumHits(trackId), null, cancellationToken);

    /// <inheritdoc />
    public Task<TrackAnalysisDto<DrumHitsDocumentDto>> DetectDrumHitsAsync(Guid trackId, CancellationToken cancellationToken = default) =>
        SendAsync<TrackAnalysisDto<DrumHitsDocumentDto>>(HttpMethod.Post, ApiRoutes.DrumHits(trackId), null, cancellationToken);

    /// <inheritdoc />
    public Task DeleteDrumHitsAsync(Guid trackId, CancellationToken cancellationToken = default) =>
        _transport.SendAsync(HttpMethod.Delete, ApiRoutes.DrumHits(trackId), null, cancellationToken);

    /// <inheritdoc />
    public Task<TrackAnalysisDto<VocalPitchDocumentDto>> GetVocalPitchAsync(Guid trackId, CancellationToken cancellationToken = default) =>
        SendAsync<TrackAnalysisDto<VocalPitchDocumentDto>>(HttpMethod.Get, ApiRoutes.VocalPitch(trackId), null, cancellationToken);

    /// <inheritdoc />
    public Task<TrackAnalysisDto<VocalPitchDocumentDto>> AnalyseVocalPitchAsync(Guid trackId, CancellationToken cancellationToken = default) =>
        SendAsync<TrackAnalysisDto<VocalPitchDocumentDto>>(HttpMethod.Post, ApiRoutes.VocalPitch(trackId), null, cancellationToken);

    /// <inheritdoc />
    public Task DeleteVocalPitchAsync(Guid trackId, CancellationToken cancellationToken = default) =>
        _transport.SendAsync(HttpMethod.Delete, ApiRoutes.VocalPitch(trackId), null, cancellationToken);

    /// <inheritdoc />
    public Task<InstrumentCatalogueDto> GetInstrumentCatalogueAsync(CancellationToken cancellationToken = default) =>
        SendAsync<InstrumentCatalogueDto>(HttpMethod.Get, ApiRoutes.InstrumentCatalogue, null, cancellationToken);

    /// <inheritdoc />
    public Task<AssetListDto> ListChannelScoresAsync(Guid trackId, string channel, CancellationToken cancellationToken = default) =>
        SendAsync<AssetListDto>(
            HttpMethod.Get, $"{ApiRoutes.Assets}?kind=score&trackId={trackId}&channel={Uri.EscapeDataString(channel)}", null, cancellationToken);
}
