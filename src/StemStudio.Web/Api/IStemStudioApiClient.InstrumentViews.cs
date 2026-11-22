using StemStudio.Contracts.Assets;
using StemStudio.Contracts.InstrumentViews;

namespace StemStudio.Web.Api;

/// <summary>Instrument views: chords, key, capo, drum hits, reference melody and the catalogue (feature 016, backend contract 1.15.0).</summary>
public partial interface IStemStudioApiClient
{
    /// <summary><c>GET /api/tracks/{id}/chords</c> (<c>getChords</c>; status <c>none</c> when never detected).</summary>
    /// <param name="trackId">Track.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The analysis with the chart once ready.</returns>
    Task<TrackAnalysisDto<ChordChartDocumentDto>> GetChordsAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/tracks/{id}/chords</c> (<c>detectChords</c>; 409 <c>edits-exist</c> without overwrite).</summary>
    /// <param name="trackId">Track.</param>
    /// <param name="request">Vocabulary, engine and the overwrite flag.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The queued analysis.</returns>
    Task<TrackAnalysisDto<ChordChartDocumentDto>> DetectChordsAsync(Guid trackId, DetectChordsRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /api/tracks/{id}/chords</c> (<c>deleteChords</c>).</summary>
    /// <param name="trackId">Track.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when the chart is removed.</returns>
    Task DeleteChordsAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary><c>PUT /api/tracks/{id}/chords/segments/{index}</c> (<c>editChordSegment</c>, Advanced).</summary>
    /// <param name="trackId">Track.</param>
    /// <param name="segmentIndex">0-based segment index.</param>
    /// <param name="request">The new Harte label.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The analysis with the edited chart.</returns>
    Task<TrackAnalysisDto<ChordChartDocumentDto>> EditChordSegmentAsync(Guid trackId, int segmentIndex, EditChordRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/tracks/{id}/chords/sheet</c> (<c>getChordSheet</c>; 409 <c>chords-not-ready</c>).</summary>
    /// <param name="trackId">Track.</param>
    /// <param name="transpose">012 transpose, -12..12.</param>
    /// <param name="capo">Capo of capo mode, 0..12.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The chord-over-lyrics rows.</returns>
    Task<ChordSheetDto> GetChordSheetAsync(Guid trackId, int transpose, int capo, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/tracks/{id}/key</c> (<c>getKey</c>, S11).</summary>
    /// <param name="trackId">Track.</param>
    /// <param name="transpose">012 transpose, -12..12.</param>
    /// <param name="instrument">Written instrument of the orchestral family, or null for concert pitch.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The key view.</returns>
    Task<KeyViewDto> GetKeyAsync(Guid trackId, int transpose, WrittenInstrumentDto? instrument, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/tracks/{id}/chords/capo-suggestions</c> (<c>getCapoSuggestions</c>, S12).</summary>
    /// <param name="trackId">Track.</param>
    /// <param name="transpose">012 transpose, -12..12.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The shortcut and up to three suggestions.</returns>
    Task<CapoSuggestionsDto> GetCapoSuggestionsAsync(Guid trackId, int transpose, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/tracks/{id}/drum-hits</c> (<c>getDrumHits</c>).</summary>
    /// <param name="trackId">Track.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The analysis with the hit map once ready.</returns>
    Task<TrackAnalysisDto<DrumHitsDocumentDto>> GetDrumHitsAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/tracks/{id}/drum-hits</c> (<c>detectDrumHits</c>; 409 <c>drums-not-split</c>).</summary>
    /// <param name="trackId">Track.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The queued analysis.</returns>
    Task<TrackAnalysisDto<DrumHitsDocumentDto>> DetectDrumHitsAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /api/tracks/{id}/drum-hits</c> (<c>deleteDrumHits</c>).</summary>
    /// <param name="trackId">Track.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when the hit map is removed.</returns>
    Task DeleteDrumHitsAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/tracks/{id}/vocal-pitch</c> (<c>getVocalPitch</c>).</summary>
    /// <param name="trackId">Track.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The analysis with the reference melody once ready.</returns>
    Task<TrackAnalysisDto<VocalPitchDocumentDto>> GetVocalPitchAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/tracks/{id}/vocal-pitch</c> (<c>analyseVocalPitch</c>; 409 <c>no-vocals</c>).</summary>
    /// <param name="trackId">Track.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The queued analysis with its estimate.</returns>
    Task<TrackAnalysisDto<VocalPitchDocumentDto>> AnalyseVocalPitchAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /api/tracks/{id}/vocal-pitch</c> (<c>deleteVocalPitch</c>).</summary>
    /// <param name="trackId">Track.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when the melody is removed.</returns>
    Task DeleteVocalPitchAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/instruments/catalogue</c> (<c>getInstrumentCatalogue</c>; static, cached by <c>InstrumentCatalogueCache</c>).</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Families, tunings, written instruments, clefs and tolerances.</returns>
    Task<InstrumentCatalogueDto> GetInstrumentCatalogueAsync(CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/assets?kind=score&amp;trackId=&amp;channel=</c> (<c>listAssets</c> channel filter, 1.15.0): score items of one channel, newest first.</summary>
    /// <param name="trackId">Track.</param>
    /// <param name="channel">Channel id, <c>&lt;stem&gt;</c> or <c>&lt;parent&gt;.&lt;detail&gt;</c>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The items.</returns>
    Task<AssetListDto> ListChannelScoresAsync(Guid trackId, string channel, CancellationToken cancellationToken = default);
}
